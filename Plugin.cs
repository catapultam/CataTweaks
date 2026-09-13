using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using PavonisInteractive.TerraInvicta;
using PavonisInteractive.TerraInvicta.Actions;
using UnityEngine;
using UnityEngine.UI;
using UnityModManagerNet;

namespace CataTweaks;

public static class Main
{
    public static bool Load(UnityModManager.ModEntry modEntry)
    {
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
