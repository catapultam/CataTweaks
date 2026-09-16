using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using PavonisInteractive.TerraInvicta;
using PavonisInteractive.TerraInvicta.Actions;
using UnityEngine;
using UnityEngine.UI;
using UnityModManagerNet;

namespace CataTweaks;

// Settings.txt in the mod folder, written with defaults on first run. Edit it and restart the
// game: values are read once at load, because Harmony decides at patch time whether a patch
// applies at all. UMM's own settings are XML, so this uses Unity's JsonUtility instead.
//
// The contents are JSON but the file must NOT be named .json. Terra Invicta's own template
// loader (ModTemplateManager.LoadJsonMods) globs every enabled mod file whose path contains
// ".json" except ModInfo.json, parses it as List<JObject>, and on failure logs a warning and
// BREAKS out of the loop - so one unparsable file stops template loading for every mod after
// it, and CataTweaks sorts early alphabetically. A settings object is not a template array, so
// .txt keeps us out of that glob entirely.
[Serializable]
public class Settings
{
    // Habs renamed "Name (Template, Location)" when a saved template is applied, and saving a hab
    // as a template round-trips that name plus its map icon, overwriting the old one in place.
    public bool habTemplateNaming = true;

    // Saving a priority preset under an existing custom preset's name overwrites it, instead of
    // the save button going dead.
    public bool priorityPresetOverwrite = true;

    // Control points re-apply their preset when a nation's valid priorities change, instead of
    // the profile flipping to 'Custom'.
    public bool presetTrackingOnNationChanges = true;

    // Full-width dividing rules between stations on the ship construction screen.
    public bool stationDividers = true;

    // Solar mirrors boost orbital stations inward of them, not just surface bases.
    public bool solarMirrorsBoostStations = false;

    // Review Failed Projects favours the expensive missed project instead of the cheapest.
    public bool expensiveFirstProjectReview = true;

    // "Demand Claim" only blocks when the two nations are at war with each other, instead of
    // when the target is at war with anyone at all.
    public bool demandClaimDespiteOtherWars = true;

    // Repeatable projects granting control point capacity or resources (Management, Audience,
    // Commercial, Operations Research) scale their payoff by this fraction of the base value per
    // repeat, matching the way their cost already scales. 0 disables the patches entirely and
    // restores vanilla. 0.03 puts the grind at roughly ten times the cost per point of the
    // median one-off project, so it stays a deliberately poor last resort.
    public float managementResearchEffectScaling = 0.03f;

    internal static Settings Load(UnityModManager.ModEntry modEntry)
    {
        string path = Path.Combine(modEntry.Path, "Settings.txt");
        try
        {
            if (File.Exists(path))
            {
                // Never rewritten once it exists, so hand edits and comments-by-absence survive.
                return JsonUtility.FromJson<Settings>(File.ReadAllText(path)) ?? new Settings();
            }
        }
        catch (Exception e)
        {
            // A typo in the file shouldn't take the whole mod down with it.
            modEntry.Logger.Error("Settings.txt unreadable, using defaults - " + e.Message);
            return new Settings();
        }
        Settings fresh = new Settings();
        File.WriteAllText(path, JsonUtility.ToJson(fresh, true));
        return fresh;
    }
}

public static class Main
{
    internal static Settings settings = new Settings();

    public static bool Load(UnityModManager.ModEntry modEntry)
    {
        // Must precede PatchAll: Harmony calls each patch class's Prepare() while patching, and
        // those read the settings to decide whether that patch is applied at all.
        settings = Settings.Load(modEntry);
        new Harmony(modEntry.Info.Id).PatchAll(Assembly.GetExecutingAssembly());
        modEntry.Logger.Log("CataTweaks loaded.");
        return true;
    }
}

internal static class HabLocation
{
    internal static string Body(TIHabState hab)
    {
        return hab.ref_naturalSpaceObject?.displayName ?? "Space";
    }

    // "Orbit, Body" / "Site, Body"; just "Body" where the body has a single orbit/site
    // (Lagrange points), since the orbit name would only repeat it.
    internal static string Of(TIHabState hab)
    {
        TINaturalSpaceObjectState body = hab.ref_naturalSpaceObject;
        if (body == null)
        {
            return "Space";
        }
        bool single = hab.IsBase
            ? (body as TISpaceBodyState)?.habSites.Length <= 1
            : body.orbits.Count(o => !o.isAdHocOrbit) <= 1;
        string place = hab.IsBase ? hab.ref_habSite?.displayName : hab.ref_orbit?.displayName;
        return single || place == null ? body.displayName : place + ", " + body.displayName;
    }
}

// Tweak 1: habs renamed to "Custom Name (Template, Location)" when a saved hab template is
// applied; the custom name is whatever the hab was called before (minus an older suffix).
[HarmonyPatch(typeof(ApplyHabTemplateAction), nameof(ApplyHabTemplateAction.Execute))]
internal static class RenameOnTemplateApply
{
    private static bool Prepare() => Main.settings.habTemplateNaming;

    private static void Postfix(ApplyHabTemplateAction __instance)
    {
        TIHabState hab = __instance.habID.GetState<TIHabState>();
        TIHabTemplate design = __instance.habDesign;
        if (hab == null || design == null || string.IsNullOrEmpty(design.displayName))
        {
            return;
        }
        hab.SetDisplayName(HabNamer.Name(hab.displayName, design.displayName, HabLocation.Of(hab)));
        // Tweak 1c: restore the icon the template remembers (see Tweak 2c). Set directly rather
        // than via ChangeHabBio, which also renames and clears the icon when given an empty one.
        if (!string.IsNullOrEmpty(design.symbolTexture))
        {
            hab.SetCustomIconString(design.symbolTexture);
        }
    }
}

// Tweak 1b: the Apply button stays enabled when the hab already has the template's modules
// but its name doesn't carry the "(Template, Location)" suffix (built by hand, renamed, or
// from a pre-mod save). Vanilla disables it because nothing would be built; applying then just
// renames the hab via Tweak 1 (ApplySavedTemplate is a no-op with nothing to build).
[HarmonyPatch(typeof(HabitatsScreenController), nameof(HabitatsScreenController.OnHabTemplateSelected))]
internal static class ApplyTemplateForRename
{
    private static bool Prepare() => Main.settings.habTemplateNaming;

    private static readonly FieldInfo dropdownField =
        AccessTools.Field(typeof(HabitatsScreenController), "habTemplateDropdown");

    private static void Postfix(HabitatsScreenController __instance)
    {
        TIHabState hab = __instance.habToDisplay;
        var dropdown = (Dictionary<int, string>)dropdownField.GetValue(__instance);
        if (__instance.managementQueryConfirmButton.interactable || hab == null
            || !dropdown.TryGetValue(__instance.managementQueryTemplateDropdown.value, out string dataName)
            || string.IsNullOrEmpty(dataName))
        {
            return;
        }
        TIHabTemplate design = TemplateManager.Find<TIHabTemplate>(dataName);
        if (design == null || !hab.CanApplySavedTemplate(design))
        {
            return;
        }
        string newName = HabNamer.Name(hab.displayName, design.displayName, HabLocation.Of(hab));
        if (newName == hab.displayName)
        {
            return;
        }
        List<TIHabModuleTemplate> toBuild = hab.ApplySavedTemplate(design, prospectiveOnly: true,
            __instance.managementQueryToggle.isOn, out _, out _, out _);
        if (toBuild.Count == 0)
        {
            __instance.managementQueryText.SetText(__instance.managementQueryText.text + "\nRename to " + newName);
            __instance.managementQueryConfirmButton.interactable = true;
        }
    }
}

// Tweak 2a: saving a hab as a template names the template after the template name inside
// the hab's "(Template, Location)" suffix instead of vanilla's "name-description" plus a
// timestamp on collision. Round-trips with Tweak 1: update hab "Freeport (Mining, ...)",
// save, and the "Mining" template is overwritten in place.
[HarmonyPatch(typeof(TIHabState), nameof(TIHabState.ConvertToTemplate))]
internal static class HabTemplateCleanName
{
    private static bool Prepare() => Main.settings.habTemplateNaming;

    private static void Postfix(TIHabState __instance, TIHabTemplate __result)
    {
        if (__result == null)
        {
            return;
        }
        __result.SetDisplayName(HabNamer.SlugOf(__instance.displayName, HabLocation.Body(__instance)));
        // Tweak 2c: remember the hab's custom map icon on the template, so applying it restores
        // the icon alongside the modules and name. symbolTexture is inert for hab designs
        // (TIHabState.iconResource overrides it with the faction station/base icon) and already
        // serialises with saved designs, so this needs no new save data.
        __result.symbolTexture = __instance.customHabIconResource;
    }
}

// Tweak 2b: saving a hab template under an existing name overwrites the old one.
[HarmonyPatch(typeof(TIFactionState), nameof(TIFactionState.SaveHabDesign))]
internal static class HabTemplateOverwrite
{
    private static bool Prepare() => Main.settings.habTemplateNaming;

    private static void Prefix(TIFactionState __instance, TIHabTemplate habDesign)
    {
        foreach (TIHabTemplate old in __instance.habDesigns
                     .Where(d => d.displayName == habDesign.displayName).ToList())
        {
            __instance.DeleteHabDesign(old.dataName);
        }
    }
}

// Tweak 3a: saving a priority preset under an existing custom preset's name overwrites it
// (built-in presets are never overwritten). If the overwritten preset was the faction's
// default, the new preset becomes the default — control points only copy weights, so no
// other references exist.
[HarmonyPatch(typeof(TIFactionState), nameof(TIFactionState.SaveCustomPresetDesign))]
internal static class PriorityPresetOverwrite
{
    private static bool Prepare() => Main.settings.priorityPresetOverwrite;

    internal sealed class OverwriteState
    {
        public bool wasDefault;
        public List<TIControlPoint> reapplyTo = new List<TIControlPoint>();
    }

    private static void Prefix(TIFactionState __instance, TIPriorityPresetTemplate priorityPreset, out OverwriteState __state)
    {
        __state = new OverwriteState();
        foreach (TIPriorityPresetTemplate old in __instance.customPresets
                     .Where(p => p.displayName == priorityPreset.displayName && p.customDesign).ToList())
        {
            __state.wasDefault |= old == __instance.defaultPriorityPreset;
            // Control points currently on the old version (the ones the UI labels with its
            // name) get the new version re-applied after the save, so countries track the
            // profile by name instead of falling back to 'Custom'.
            __state.reapplyTo.AddRange(__instance.controlPoints.Where(cp =>
                old.MatchesPreset(cp.controlPointPriorities, cp.nation.InvalidPriorities)));
            __instance.DeleteCustomPresetDesign(old);
        }
    }

    private static void Postfix(TIFactionState __instance, TIPriorityPresetTemplate priorityPreset, OverwriteState __state)
    {
        if (__state.wasDefault)
        {
            __instance.defaultPriorityPreset = priorityPreset;
            __instance.defaultPriorityPresetTemplateName = priorityPreset.dataName;
        }
        foreach (TIControlPoint cp in __state.reapplyTo.Distinct())
        {
            cp.nation.ApplyInvestmentTemplateToControlPoint(cp.positionInNation, priorityPreset);
        }
    }
}

// Tweak 3b: enable the preset save button on a name collision that the overwrite above
// can handle. Vanilla disables it for any name or settings duplicate; we re-enable only
// for name-only collisions with overwritable (custom, non-default) presets.
[HarmonyPatch(typeof(NationInfoController), "UpdateDesignPresetPanel")]
internal static class PriorityPresetSaveButton
{
    private static bool Prepare() => Main.settings.priorityPresetOverwrite;

    // Read by the two patches below even when this one isn't applied, so it stays put.
    internal static readonly FieldInfo proposedField =
        AccessTools.Field(typeof(NationInfoController), "proposedPriorityPreset");

    private static readonly FieldInfo duplicatedField =
        AccessTools.Field(typeof(NationInfoController), "duplicatedPreset");

    private static void Postfix(NationInfoController __instance)
    {
        var proposed = (TIPriorityPresetTemplate)proposedField.GetValue(__instance);
        var settingsDuplicate = (TIPriorityPresetTemplate)duplicatedField.GetValue(__instance);
        TIFactionState player = __instance.activePlayer;
        if (proposed == null || player == null || settingsDuplicate != null)
        {
            return;
        }
        if (!proposed.ValidPreset_Global() || proposed.displayName == Loc.T("UI.Nation.Custom"))
        {
            return;
        }
        var sameName = player.ValidPresetsForFaction()
            .Where(p => p.displayName == proposed.displayName).ToList();
        if (sameName.Count > 0 && sameName.All(p => p.customDesign))
        {
            __instance.savePresetButton.interactable = true;
        }
    }
}

// Tweak 3e: when a nation's set of valid priorities changes (spaceflight program gained,
// military founded, army/navy/STO/space-defense/nuke techs unlocked, federation changes...),
// control points tracking a preset re-apply it so newly valid priorities pick up the
// preset's weights instead of the label flipping to 'Custom'. Generic: a daily per-nation
// check caches the validity set and, on change, matches each control point against the
// OLD validity (which is what the CP's weights still reflect) to find its tracked preset.
// ponytail: in-memory cache only — after a save reload the first tick primes the cache,
// so a validity flip landing exactly in that gap is missed; per-CP persistence if ever needed.
[HarmonyPatch(typeof(PavonisInteractive.TerraInvicta.Systems.PeriodicUpdates.NationPeriodicUpdate), "DailyNationUpdateTask")]
internal static class ReapplyPresetsOnValidityChange
{
    private static bool Prepare() => Main.settings.presetTrackingOnNationChanges;

    private static readonly Dictionary<GameStateID, HashSet<PriorityType>> lastInvalid =
        new Dictionary<GameStateID, HashSet<PriorityType>>();

    private static void Postfix(TINationState nation)
    {
        var current = new HashSet<PriorityType>(nation.InvalidPriorities);
        if (lastInvalid.TryGetValue(nation.ID, out HashSet<PriorityType> previous) && !previous.SetEquals(current))
        {
            List<PriorityType> oldInvalid = previous.ToList();
            foreach (TIControlPoint cp in nation.controlPoints)
            {
                if (cp.faction == null)
                {
                    continue;
                }
                TIPriorityPresetTemplate tracked = TemplateManager.IterateByClass<TIPriorityPresetTemplate>()
                    .FirstOrDefault(t => !t.deleted && t.ValidPresetForFaction(cp.faction)
                                         && t.MatchesPreset(cp.controlPointPriorities, oldInvalid));
                if (tracked != null)
                {
                    nation.ApplyInvestmentTemplateToControlPoint(cp.positionInNation, tracked);
                }
            }
        }
        lastInvalid[nation.ID] = current;
    }
}

// Tweak 5: full-width rules between stations in the ship construction screen's shipyard grid.
// One station can host several shipyards and the grid already sorts them together (body, then
// hab name, then tier); this fills out each station's last row with blanks so the next station
// starts on a fresh row, and draws a rule across the boundary. The grid is a GridLayoutGroup, so
// every cell is the same size and a full-width rule cannot be a cell - it opts out of the layout
// and is positioned at the row boundary instead.
internal static class StationDivider
{
    private const string SpacerName = "CataTweaksSpacer";
    private const string RuleName = "CataTweaksRule";

    private static GridLayoutGroup Grid(FleetsScreenController screen)
    {
        return screen.shipyardGridList != null
            ? screen.shipyardGridList.GetComponent<GridLayoutGroup>()
            : null;
    }

    // ListManagerBase sizes the card list from transform.childCount and destroys anything past
    // the new size, so our extra children have to be gone before it rebuilds - and gone now,
    // not at end of frame, or it would delete real cards in their place.
    internal static void Clear(FleetsScreenController screen)
    {
        GridLayoutGroup grid = Grid(screen);
        if (grid == null)
        {
            return;
        }
        for (int i = grid.transform.childCount - 1; i >= 0; i--)
        {
            Transform child = grid.transform.GetChild(i);
            if (child.name == SpacerName || child.name == RuleName)
            {
                UnityEngine.Object.DestroyImmediate(child.gameObject);
            }
        }
    }

    private static int Columns(GridLayoutGroup grid)
    {
        if (grid.constraint == GridLayoutGroup.Constraint.FixedColumnCount)
        {
            return Mathf.Max(1, grid.constraintCount);
        }
        float width = ((RectTransform)grid.transform).rect.width - grid.padding.horizontal;
        float step = grid.cellSize.x + grid.spacing.x;
        return step > 0f ? Mathf.Max(1, Mathf.FloorToInt((width + grid.spacing.x) / step)) : 1;
    }

    private static void AddSpacer(GridLayoutGroup grid, int siblingIndex)
    {
        var go = new GameObject(SpacerName, typeof(RectTransform));
        go.transform.SetParent(grid.transform, worldPositionStays: false);
        go.transform.SetSiblingIndex(siblingIndex);
    }

    private static void AddRule(GridLayoutGroup grid, int row)
    {
        var go = new GameObject(RuleName, typeof(RectTransform), typeof(Image));
        var rect = (RectTransform)go.transform;
        rect.SetParent(grid.transform, worldPositionStays: false);
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(0f, 3f);
        // Centred in the gap above the given row. With no spacing to sit in it lands on the top
        // edge of that row's cards, and draws over them as the last sibling, so it stays visible.
        float y = grid.padding.top + row * (grid.cellSize.y + grid.spacing.y) - grid.spacing.y / 2f;
        rect.anchoredPosition = new Vector2(0f, -y);
        go.AddComponent<LayoutElement>().ignoreLayout = true;
        Image rule = go.GetComponent<Image>();
        rule.color = new Color(1f, 1f, 1f, 0.5f);
        rule.raycastTarget = false;
        rect.SetAsLastSibling();
    }

    internal static void Apply(FleetsScreenController screen)
    {
        Clear(screen);
        GridLayoutGroup grid = Grid(screen);
        if (grid == null)
        {
            return;
        }
        int cols = Columns(grid);
        int cells = 0;
        TIHabState previous = null;
        // Iterating the list manager walks its own snapshot, not the transform, so inserting
        // children as we go is safe. Inactive cards are skipped by the layout and by us.
        foreach (object entry in screen.shipyardGridList)
        {
            if (!(entry is ShipyardGridItemController card) || card.shipyardIdx?.ref_hab == null
                || !card.gameObject.activeSelf)
            {
                continue;
            }
            TIHabState hab = card.shipyardIdx.ref_hab;
            if (previous != null && hab != previous)
            {
                int pad = (cols - cells % cols) % cols;
                for (int i = 0; i < pad; i++)
                {
                    // Re-read the index each time: each insert pushes the card down one.
                    AddSpacer(grid, card.transform.GetSiblingIndex());
                    cells++;
                }
                AddRule(grid, cells / cols);
            }
            cells++;
            previous = hab;
        }
    }
}

[HarmonyPatch(typeof(FleetsScreenController), nameof(FleetsScreenController.FilterShipLists))]
internal static class StationDividerApply
{
    private static bool Prepare() => Main.settings.stationDividers;

    private static void Postfix(FleetsScreenController __instance)
    {
        StationDivider.Apply(__instance);
    }
}

// Strip the spacers before the grid is rebuilt; RefreshConstructionManager calls FilterShipLists
// on its way out, which puts them back.
[HarmonyPatch(typeof(FleetsScreenController), nameof(FleetsScreenController.RefreshConstructionManager))]
internal static class StationDividerClear
{
    // Must share stationDividers with the Apply patch: clearing without adding would be
    // pointless, and adding without clearing would let ListManagerBase delete real cards.
    private static bool Prepare() => Main.settings.stationDividers;

    private static void Prefix(FleetsScreenController __instance)
    {
        StationDivider.Clear(__instance);
    }
}

// Tweak 6: solar mirrors boost orbital stations, not just surface bases. Vanilla builds the
// whole mechanism - station-mounted mirrors, per-body per-faction accumulation, mirror mass
// scaling with semiMajorAxis^2, placement bans at L2/L3 and solar L4/L5, and an 8x output cap -
// but only ever spends it on bases: SolarPowerOutput adds the bonus when the location is a
// space body, a hab site, or a hab that IsBase. Stations are excluded by that condition.
//
// Stations can't use the body-wide solarMirrorBonus total, because it cannot say which orbit a
// mirror sat in. So they get their own sum with a direction rule: a mirror only lights targets
// inward of itself. A mirror in high Mars orbit boosts medium and low; one in medium boosts low
// only, never high. Lagrange points sit outside the orbit ladder entirely, so they light
// everything they credit under vanilla's rules - Sun-Earth L1 covers every Earth and Luna orbit.
internal static class StationSolarMirrors
{
    // Master switch, read from Settings.txt. Harmony calls Prepare() on each patch class below
    // and skips patching when it returns false, so with this off the game runs entirely stock -
    // the code stays here but never touches SolarPowerOutput or ChangeSolarMirrorBonus.
    internal static bool Enabled => Main.settings.solarMirrorsBoostStations;

    // Frame-scoped, matching how the game caches its own hot per-hab queries (see
    // TIHabState.OkayModules). SolarPowerOutput is called from power management, the hab screen
    // and the AI planner, so the walk below can repeat many times within one recalculation;
    // expiring on frame change keeps it correct without event plumbing.
    private static int cachedFrame = -1;
    private static readonly Dictionary<TIOrbitState, Dictionary<TIFactionState, int>> cache =
        new Dictionary<TIOrbitState, Dictionary<TIFactionState, int>>();

    internal static void Invalidate()
    {
        cache.Clear();
        cachedFrame = Time.frameCount;
    }

    private static int PoweredMirrorValue(TIHabState station)
    {
        int total = 0;
        foreach (TIHabModuleState module in station.CompletedModules())
        {
            if (module.powered && !module.destroyed
                && module.moduleTemplate.SpecialRules.Contains(HabModuleSpecialRule.SolarMirror))
            {
                total += (int)module.moduleTemplate.specialRulesValue;
            }
        }
        return total;
    }

    // Mirrors owned by this faction that light the given orbit. Mirrors are few (one module slot
    // on a station), so iterating the faction's habs is cheaper than walking every orbit of the
    // body and its Lagrange points.
    private static int Compute(TIOrbitState targetOrbit, TIFactionState faction)
    {
        TISpaceBodyState targetBody = targetOrbit.barycenter?.ref_spaceBody;
        if (targetBody == null)
        {
            return 0;
        }
        int total = 0;
        foreach (TIHabState mirrorHab in faction.habs)
        {
            if (mirrorHab == null || !mirrorHab.IsStation)
            {
                continue;
            }
            TIOrbitState mirrorOrbit = mirrorHab.orbitState;
            if (mirrorOrbit?.barycenter == null)
            {
                continue;
            }
            int value = PoweredMirrorValue(mirrorHab);
            if (value == 0)
            {
                continue;
            }
            if (mirrorOrbit.barycenter.isSpaceBodyState)
            {
                // Same barycenter object, so the two semi-major axes are measured against the
                // same centre and are comparable. Strictly outward only: a mirror level with or
                // inside its target does nothing for it.
                if (mirrorOrbit.barycenter == targetOrbit.barycenter
                    && mirrorOrbit.semiMajorAxis_m > targetOrbit.semiMajorAxis_m)
                {
                    total += value;
                }
            }
            else if (mirrorOrbit.barycenter.isLagrangePointState)
            {
                // Outside the orbit ladder: reaches every orbit of whatever vanilla credits.
                TILagrangePointState point = mirrorOrbit.ref_lagrangePoint;
                if (point.secondaryObject.isaMoon)
                {
                    if (point.secondaryObject == targetBody)
                    {
                        total += value;
                    }
                }
                else if (point.lagrangeValue == LagrangeValue.L1
                         && (point.secondaryObject == targetBody
                             || point.secondaryObject.naturalSatellites.Contains(targetBody)))
                {
                    total += value;
                }
            }
        }
        return total;
    }

    internal static int BonusFor(TIHabState station, TIFactionState faction)
    {
        TIOrbitState orbit = station?.orbitState;
        if (orbit == null || faction == null)
        {
            return 0;
        }
        if (cachedFrame != Time.frameCount)
        {
            Invalidate();
        }
        if (!cache.TryGetValue(orbit, out Dictionary<TIFactionState, int> byFaction))
        {
            byFaction = new Dictionary<TIFactionState, int>();
            cache[orbit] = byFaction;
        }
        if (!byFaction.TryGetValue(faction, out int bonus))
        {
            bonus = Compute(orbit, faction);
            byFaction[faction] = bonus;
        }
        return bonus;
    }
}

[HarmonyPatch(typeof(TIHabModuleState), nameof(TIHabModuleState.SolarPowerOutput))]
internal static class StationSolarMirrorOutput
{
    private static bool Prepare() => StationSolarMirrors.Enabled;

    private static void Postfix(TIGameState location, float powerValue, TIFactionState faction,
        int tier, bool skipMirrors, ref int __result)
    {
        if (skipMirrors || faction == null || !location.isHabState)
        {
            return;
        }
        TIHabState station = location.ref_hab;
        if (station == null || !station.IsStation)
        {
            return;
        }
        int bonus = StationSolarMirrors.BonusFor(station, faction) * tier;
        if (bonus > 0)
        {
            // Re-apply vanilla's ceiling rather than letting the bonus run past it.
            __result = Mathf.Min(__result + bonus, (int)(8f * powerValue));
        }
    }
}

// Vanilla only refreshes surfaceBases when a mirror toggles, so station power grids would show
// stale output until something else forced a recalculation.
[HarmonyPatch(typeof(TISpaceBodyState), nameof(TISpaceBodyState.ChangeSolarMirrorBonus))]
internal static class StationSolarMirrorRefresh
{
    private static bool Prepare() => StationSolarMirrors.Enabled;

    private static void Postfix(TISpaceBodyState __instance, int changeBy, TIFactionState faction)
    {
        if (changeBy == 0 || faction == null)
        {
            return;
        }
        StationSolarMirrors.Invalidate();
        foreach (TIOrbitState orbit in StationOrbits(__instance))
        {
            foreach (TIHabState station in orbit.stationsInOrbit)
            {
                if (station.faction == faction)
                {
                    station.UpdatePowerManagement(changeBy > 0, null, faction.player.isAI);
                }
            }
        }
    }

    // The body's own orbits plus those around its Lagrange points, since a station at L1 orbits
    // the point rather than the body and would otherwise never be refreshed.
    private static IEnumerable<TIOrbitState> StationOrbits(TISpaceBodyState body)
    {
        foreach (TIOrbitState orbit in body.orbits)
        {
            yield return orbit;
        }
        foreach (TILagrangePointState point in body.lagrangePoints)
        {
            foreach (TIOrbitState orbit in point.orbits)
            {
                yield return orbit;
            }
        }
    }
}

// Tweak 7: Review Failed Projects favours the EXPENSIVE missed project instead of the cheapest.
// Vanilla weights each candidate by factionAvailableChance / researchCost, so a 200-research
// throwaway outdraws a 5000-research drive by 25:1 and the review is worst exactly when it
// matters most. This inverts the divide to a multiply, so cost raises a project's odds instead
// of sinking them; availability chance still scales it, keeping genuinely rare projects rare.
//
// The picker is a local function inside TIEffectsState.ProcessInstantEffect, so it compiles to a
// mangled name and its parameter is compiler-generated - hence the runtime name search and the
// positional __0 argument.
[HarmonyPatch]
internal static class ReviewFavoursExpensiveProjects
{
    // Harmony calls Prepare() before TargetMethod(), so turning this off also silences the
    // throw below rather than taking the mod down over a tweak that isn't wanted.
    private static bool Prepare() => Main.settings.expensiveFirstProjectReview;

    private static MethodBase TargetMethod()
    {
        MethodBase target = AccessTools.GetDeclaredMethods(typeof(TIEffectsState))
            .FirstOrDefault(m => m.Name.Contains("GrantMissedProjectToFaction"));
        if (target == null)
        {
            // Loud rather than silent: a rename upstream would otherwise no-op the whole tweak.
            throw new InvalidOperationException(
                "CataTweaks: could not find GrantMissedProjectToFaction on TIEffectsState.");
        }
        return target;
    }

    private static bool Prefix(TIFactionState __0)
    {
        if (__0?.missedProjects == null || __0.missedProjects.Count == 0)
        {
            return false;
        }
        var candidates = new List<TIProjectTemplate>();
        var weights = new List<float>();
        float total = 0f;
        foreach (string name in __0.missedProjects)
        {
            TIProjectTemplate project = TemplateManager.Find<TIProjectTemplate>(name);
            if (project == null)
            {
                continue;
            }
            // Reversed from vanilla's chance / cost. Floor keeps a zero-cost or zero-chance
            // project selectable rather than silently unreachable.
            float weight = Mathf.Max(project.factionAvailableChance * project.researchCost, 0.0001f);
            candidates.Add(project);
            weights.Add(weight);
            total += weight;
        }
        if (candidates.Count == 0)
        {
            return false;
        }
        float roll = TIUtilities.RandomFloatValue() * total;
        TIProjectTemplate picked = candidates[candidates.Count - 1];
        for (int i = 0; i < candidates.Count; i++)
        {
            roll -= weights[i];
            if (roll <= 0f)
            {
                picked = candidates[i];
                break;
            }
        }
        // AddAvailableProject also runs RemoveMissedProjectFromList, so the winner leaves the queue.
        __0.AddAvailableProject(picked);
        TINotificationQueueState.LogProjectTriggered(__0, picked, special: true);
        return false;
    }
}

// Tweak 3c: selecting a custom preset from the dropdown carries its name into the
// proposed preset (vanilla blanks it), so overwrite-after-edit needs no retyping.
[HarmonyPatch(typeof(NationInfoController), "DuplicateSelectedPreset")]
internal static class PriorityPresetKeepName
{
    private static bool Prepare() => Main.settings.priorityPresetOverwrite;

    private static void Postfix(NationInfoController __instance, TIPriorityPresetTemplate presetToDuplicate)
    {
        if (presetToDuplicate != null && presetToDuplicate.customDesign
            && !string.IsNullOrEmpty(presetToDuplicate.displayName))
        {
            var proposed = (TIPriorityPresetTemplate)PriorityPresetSaveButton.proposedField.GetValue(__instance);
            proposed.SetDisplayName(presetToDuplicate.displayName);
        }
    }
}

// Tweak 3d: the name field accepts a name that collides with a custom (overwritable)
// preset instead of rejecting it with an error sound. Built-in name collisions and
// empty names still go through the vanilla rejection path.
[HarmonyPatch(typeof(NationInfoController), "OnNewPresetNameEntered")]
internal static class PriorityPresetNameEntry
{
    private static bool Prepare() => Main.settings.priorityPresetOverwrite;

    private static bool Prefix(NationInfoController __instance)
    {
        string name = __instance.inputPresetName.text.Trim();
        if (name.Length == 0)
        {
            return true;
        }
        var matches = __instance.activePlayer.ValidPresetsForFaction()
            .Where(p => p.displayName == name).ToList();
        if (matches.Count == 0 || matches.Any(p => !p.customDesign))
        {
            return true;
        }
        var proposed = (TIPriorityPresetTemplate)PriorityPresetSaveButton.proposedField.GetValue(__instance);
        proposed.SetDisplayName(name);
        AccessTools.Method(typeof(NationInfoController), "UpdateDesignPresetPanel")
            .Invoke(__instance, new object[] { true, false });
        return false;
    }
}

// Tweak 8: "Demand Claim" (TransferRegionsOption) stops requiring the target nation to be at
// peace with EVERYONE. Vanilla filters candidate regions on item.nation.atWar, which is just
// "wars.Count > 0" - not "at war with the nation asking". TINationState has IsAtWarWith(nation)
// and the policy's own description says "a nation that's not at war", so the blanket test looks
// like the intent was the narrower one. The consequence of the wide reading is severe: a nation
// fighting anyone at all can never cede territory to anyone, and an alien war that never ends
// freezes every peaceful border change on the map for the rest of the campaign - including
// transfers between two nations the same faction already controls, where the game otherwise
// skips the diplomatic response entirely (TransferRegionsOption.PromptPolicyResponse enacts
// immediately when policyTarget.ref_faction == enactingNation.executiveFaction).
//
// Allowed() is just GetPossibleTargets().Count > 0, so postfixing this one method restores both
// the menu entry and the target list. Every other vanilla condition is re-tested here unchanged,
// so the only regions this can add are ones vanilla rejected for the war reason alone.
[HarmonyPatch(typeof(TransferRegionsOption), nameof(TransferRegionsOption.GetPossibleTargets))]
internal static class DemandClaimDespiteOtherWars
{
    private static bool Prepare() => Main.settings.demandClaimDespiteOtherWars;

    private static void Postfix(TINationState actingNation, ref IList<TIGameState> __result)
    {
        if (actingNation == null || __result == null)
        {
            return;
        }
        foreach (TIRegionState region in actingNation.ExternalClaims())
        {
            TINationState owner = region.nation;
            if (owner == null || __result.Contains(region))
            {
                continue;
            }
            // Vanilla's conditions, minus the blanket atWar test.
            if (!actingNation.CanImproveRelationsYet(owner)
                || !owner.ExecutivePowerConsolidated
                || region == owner.capital
                || actingNation.ClaimWillBeHostile(region)
                || (actingNation.rivals.Contains(owner) && !actingNation.CanEndRivalry(owner)))
            {
                continue;
            }
            // The relaxed test: only a war between these two blocks the transfer.
            if (owner.IsAtWarWith(actingNation))
            {
                continue;
            }
            // Vanilla's alien-nation gate, unchanged.
            if (actingNation.alienNation
                && !TIEffectsState.CheckForAnyEffectInContext(
                    Context.CanTransferTerritoryToAliens, owner.executiveFaction))
            {
                continue;
            }
            __result.Add(region);
        }
    }
}

// Tweak 9: repeatable projects that grant control point capacity scale their effect alongside
// their cost.
//
// TIProjectTemplate.GetResearchCost multiplies a repeatable's cost by (1 + times completed), so
// the Nth repeat of Management Research costs N x 600 - but always grants the same flat
// Effect_BSBE_CPMaintenanceBonus5. Research per point of capacity is therefore 120N and diverges:
// 3,240 by the 27th repeat, 12,000 by the 100th, with the cumulative cost of N points growing as
// ~12N^2. Meanwhile the rest of the cap - global freebies, councilor attributes, one admin module
// per station, a fixed list of one-off projects - is hard-bounded, while maintenance cost scales
// with national GDP forever. The repeatable is the only unbounded source and vanilla prices it
// out of reach, so the cap stops rising long before GDP does.
//
// With scaling r the Nth repeat grants base x (1 + r(N-1)), so research per point converges on
// 120/r instead of diverging. At the default 0.03 that is ~4,000: about ten times the median
// one-off CP-cap project (417 across those a faction can actually take) and twice the worst one
// in the game, so grinding this remains strictly worse than every alternative while ceasing to
// be pointless. Set the scaling to 0 for stock behaviour.
//
// Vanilla already grants base x N via N stacked effect instances, so only the difference is
// added here: base x r x N(N-1)/2. Keyed on "repeatable, with a negative ControlPointMaintenance
// effect" rather than on Project_ManagementResearch by name, so another project of the same shape
// is covered without a code change.
[HarmonyPatch(typeof(TIFactionState), nameof(TIFactionState.GetControlPointMaintenanceFreebieCap))]
internal static class RepeatableCapScalesWithCost
{
    private static bool Prepare() => Main.settings.managementResearchEffectScaling > 0f;

    private struct Cached
    {
        public int completedCount;
        public float extra;
    }

    // The cap is read by the ledger, the nations screen and the AI planner, several times per
    // frame, and completedProjects runs to hundreds of entries. Recompute only when the list
    // grows. ponytail: in-memory and keyed on the faction object, so a save reload just
    // repopulates it; bounded by the faction count either way.
    private static readonly Dictionary<TIFactionState, Cached> cache =
        new Dictionary<TIFactionState, Cached>();

    // Capacity one completion grants, as a positive number. ControlPointMaintenance effects are
    // stored negative because they reduce maintenance rather than raising a cap.
    internal static float CapPerCompletion(TIProjectTemplate project)
    {
        float total = 0f;
        foreach (TIEffectTemplate effect in project.Effects)
        {
            if (effect != null && effect.value < 0f
                && effect.GetContexts().Contains(Context.ControlPointMaintenance))
            {
                total -= effect.value;
            }
        }
        return total;
    }

    private static float Compute(TIFactionState faction)
    {
        var counts = new Dictionary<TIProjectTemplate, int>();
        foreach (TIProjectTemplate project in faction.completedProjects)
        {
            if (project == null || !project.repeatable)
            {
                continue;
            }
            counts.TryGetValue(project, out int seen);
            counts[project] = seen + 1;
        }
        float rate = Main.settings.managementResearchEffectScaling;
        float extra = 0f;
        foreach (KeyValuePair<TIProjectTemplate, int> pair in counts)
        {
            float perCompletion = CapPerCompletion(pair.Key);
            if (perCompletion > 0f)
            {
                // The uplift only, summed over repeats: base x r x (0 + 1 + ... + (N-1)).
                extra += perCompletion * rate * pair.Value * (pair.Value - 1) / 2f;
            }
        }
        return extra;
    }

    private static void Postfix(TIFactionState __instance, ref float __result)
    {
        if (__instance == null || __instance.IsAlienFaction || __instance.completedProjects == null)
        {
            return;
        }
        int completed = __instance.completedProjects.Count;
        if (!cache.TryGetValue(__instance, out Cached entry) || entry.completedCount != completed)
        {
            entry = new Cached { completedCount = completed, extra = Compute(__instance) };
            cache[__instance] = entry;
        }
        __result += entry.extra;
    }
}

// Tweak 9, continued: the resource-granting repeatables (Audience, Commercial and Operations
// Research) get the same base x (1 + r(N-1)) payoff. Vanilla already paid base inside
// OnProjectComplete, so only the uplift is added. Unlike the cap this is forward-only: resources
// granted by earlier repeats are already spent and are not topped up.
[HarmonyPatch(typeof(TIFactionState), nameof(TIFactionState.OnProjectComplete))]
internal static class RepeatableGrantsScaleWithCost
{
    private static bool Prepare() => Main.settings.managementResearchEffectScaling > 0f;

    // A postfix runs even when vanilla returns early (slot project already completed), so only
    // pay out if this call actually recorded a completion.
    private static void Prefix(TIFactionState __instance, out int __state) =>
        __state = __instance.completedProjects?.Count ?? 0;

    private static void Postfix(TIFactionState __instance, TIProjectTemplate project, bool startup, int __state)
    {
        if (startup || project == null || !project.repeatable || __instance.IsAlienFaction
            || __instance.completedProjects == null || __instance.completedProjects.Count <= __state)
        {
            return;
        }
        // completedProjects already includes this completion (AddCompletedProject runs first).
        int n = __instance.completedProjects.Count(x => x == project);
        float uplift = Main.settings.managementResearchEffectScaling * (n - 1);
        if (uplift <= 0f)
        {
            return;
        }
        // Routing mirrors vanilla's grant loop.
        foreach (ResourceValue item in project.resourcesGranted)
        {
            if (item.value <= 0f)
            {
                continue;
            }
            float value = item.value * uplift;
            switch (item.resource)
            {
                case FactionResource.Projects:
                case FactionResource.MissionControl:
                    __instance.ChangeBaseResourceIncome(item.resource, value);
                    break;
                case FactionResource.None:
                    break;
                default:
                    __instance.AddToCurrentResource(value, item.resource,
                        suppressFactionResourcesUpdatedEvent: false, "Project Completion");
                    break;
            }
        }
    }
}

// Tweak 9, UI: vanilla's benefit lines render from shared templates and can't be made
// per-faction, so append one line with the true scaled payoff for this repeat. Shown for all four
// projects so the cap (retroactive) and resources (forward-only) read the same way.
[HarmonyPatch(typeof(TIProjectTemplate), nameof(TIProjectTemplate.BenefitsDescription))]
internal static class RepeatableScalingDescription
{
    private static bool Prepare() => Main.settings.managementResearchEffectScaling > 0f;

    private static void Postfix(TIProjectTemplate __instance, TIFactionState faction,
        TechBenefitsContext benefitsContext, ref string __result)
    {
        if (benefitsContext == TechBenefitsContext.Archive || !__instance.repeatable
            || faction == null || faction.IsAlienFaction || faction.completedProjects == null)
        {
            return;
        }
        ResourceValue[] granted = __instance.resourcesGranted
            .Where(x => x.resource != FactionResource.None && x.value > 0f).ToArray();
        float cap = RepeatableCapScalesWithCost.CapPerCompletion(__instance);
        if (granted.Length == 0 && cap <= 0f)
        {
            return;
        }
        // The completion notice is built after the project is recorded, so it describes the
        // repeat just finished; the research screen describes the next one.
        int repeat = faction.completedProjects.Count(x => x == __instance)
            + (benefitsContext == TechBenefitsContext.JustCompleted ? 0 : 1);
        float mult = 1f + Main.settings.managementResearchEffectScaling * (repeat - 1);
        var parts = new List<string>();
        if (granted.Length > 0)
        {
            parts.Add(TIUtilities.BuildResourceValueString(
                granted.Select(x => new ResourceValue(x.resource, x.value * mult)).ToArray()));
        }
        if (cap > 0f)
        {
            parts.Add($"+{cap * mult:0.#} control point capacity");
        }
        __result += $"Repeat {repeat}: x{mult:0.00} -> {string.Join(", ", parts)}\n";
    }
}
