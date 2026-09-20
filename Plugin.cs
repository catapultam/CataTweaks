using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;
using ModelShark;
using TMPro;
using PavonisInteractive.TerraInvicta;
using PavonisInteractive.TerraInvicta.Actions;
using UnityEngine;
using UnityEngine.UI;
using UnityModManagerNet;

namespace CataTweaks;

// Settings live in the Unity Mod Manager window (Ctrl+F10 by default), under this mod's entry.
// UMM draws one control per [Draw] field, writes Settings.xml into the mod folder on Save, and
// calls OnChange the moment a control moves - so every toggle takes effect immediately, with no
// restart. Each patch reads its setting when it runs rather than gating at patch time, which is
// what makes that possible; the cost is that a disabled patch still runs one comparison.
//
// The file must NOT be named .json. Terra Invicta's own template loader (ModTemplateManager
// .LoadJsonMods) globs every enabled mod file whose path contains ".json" except ModInfo.json,
// parses it as List<JObject>, and on failure logs a warning and BREAKS out of the loop - so one
// unparsable file stops template loading for every mod after it, and CataTweaks sorts early
// alphabetically. UMM's .xml keeps us out of that glob entirely, as the old .txt did.
public class Settings : UnityModManager.ModSettings, IDrawable
{
    // Campaign Options are re-applied from the last campaign you started, instead of the screen
    // resetting to stock values every session.
    [Draw("Remember campaign options between sessions", Tooltip = "The Customize Campaign screen opens with the options from the last campaign you started, rather than resetting to defaults. Custom faction names are remembered per faction and reappear only when that faction is selected.")]
    public bool persistCampaignOptions = true;

    // Habs renamed "Name (Template, Location)" when a saved template is applied, and saving a hab
    // as a template round-trips that name plus its map icon, overwriting the old one in place.
    [Draw("Hab Template Tweaks & QOL", Tooltip = "Applying a saved hab template renames the hab to Name (Template, Orbit, Body) and applies the template's map icon. Saving a hab as a template names it after the text in brackets and overwrites the existing template of that name. A template that would change nothing can still be applied, so a hab can be renamed on its own.")]
    public bool habTemplateNaming = true;

    // Saving a priority preset under an existing custom preset's name overwrites it, instead of
    // the save button going dead.
    [Draw("Priority presets overwrite in place", Tooltip = "Saving a priority preset under an existing custom name replaces it. Control points using the old version move to the new one, and it stays the default preset if it was.")]
    public bool priorityPresetOverwrite = true;

    // Control points re-apply their preset when a nation's valid priorities change, instead of
    // the profile flipping to 'Custom'.
    [Draw("Nations keep their preset when priorities change", Tooltip = "Re-applies a nation's preset when its set of valid priorities changes, instead of the nation switching to Custom.")]
    public bool presetTrackingOnNationChanges = true;

    // Full-width dividing rules between stations on the ship construction screen.
    [Draw("Dividers between stations in Ship Construction UI", Tooltip = "Draws a rule between one station's shipyards and the next in the construction list.")]
    public bool stationDividers = true;

    // Fleet-detected notifications name the hab's orbit or body as well as the hab itself.
    [Draw("Fleet detections name the orbit or body", Tooltip = "Fleet detection notices name the hab's orbit and body, and the body for a landed fleet.")]
    public bool fleetDetectionLocation = true;

    // Solar mirrors boost orbital stations inward of them, not just surface bases.
    [Draw("Solar mirrors boost orbital stations", Tooltip = "Solar mirrors add power to orbital stations as well as surface bases. A mirror lights targets orbiting inward of it, and Lagrange point mirrors light everything they already credit. Surface bases are unchanged.")]
    public bool solarMirrorsBoostStations = false;

    // Review Failed Projects favors the expensive missed project instead of the cheapest.
    // The ceiling on what a mirror-lit station's solar modules may produce, as a multiple of their
    // unlit output. 8 is vanilla's own figure and the default; lower it to blunt mirrors without
    // switching them off. Surface bases keep vanilla's 8x either way - this patch only ever runs
    // for stations.
    [Draw("Solar mirror orbital boost cap", DrawType.Slider, Min = 2f, Max = 8f, Tooltip = "Maximum output of a mirror-lit station's solar modules, as a multiple of their unlit output. Vanilla's ceiling is 8. Applies to stations only.")]
    public float solarMirrorOutputCap = 8f;

    [Draw("Project review favors the expensive project", Tooltip = "Review Failed Projects weights each candidate by availability chance times research cost rather than divided by it.")]
    public bool expensiveFirstProjectReview = true;

    // "Demand Claim" only blocks when the two nations are at war with each other, instead of
    // when the target is at war with anyone at all.
    [Draw("Demand Claim ignores the target's other wars", Tooltip = "Demand Claim is blocked only by a war between the two nations involved, rather than by the target being at war with anyone.")]
    public bool demandClaimDespiteOtherWars = true;

    // A nation that holds another nation's original capital borrows that nation's claims for as
    // long as it holds it, so unification no longer has to be worked strictly from the outside in.
    [Draw("Holding a capital borrows that nation's claims", Tooltip = "While you hold a dormant nation's original capital, and your claim on that capital is not hostile, that nation's claims are yours to use. Lose the capital, or have that claim turn hostile, and the borrowed claims go with it. Claims the other nation held hostilely stay hostile for you, and nations that still hold territory of their own are excluded.")]
    public bool inheritedCapitalClaims = true;

    // Repeatable projects granting control point capacity or resources (Management, Audience,
    // Commercial, Operations Research) scale their payoff by this fraction of the base value per
    // repeat, matching the way their cost already scales. 0 disables the patches entirely and
    // restores vanilla. 0.03 puts the grind at roughly ten times the cost per point of the
    // median one-off project, so it stays a deliberately poor last resort.
    [Draw("Repeatable Management Project Scaling", DrawType.Slider, Min = 0f, Max = 0.2f, Tooltip = "Management Research, Audience Research, Commercial Research and Operations Research pay this much more of their base reward on each repeat. Management Research grants control point capacity; the other three grant Influence, Money and Operations. At 0% every repeat pays the flat vanilla amount.")]
    public float repeatableProjectScaling = 0.03f;

    // Only the default the Customize Campaign screen starts from. The campaign's own answer is
    // stored in its save, so changing this never reaches a campaign already under way.
    [Draw("New campaigns: unused spy slots become councilor slots", Tooltip = "Default for the Customize Campaign option of the same name.")]
    public bool spySlotsAsCouncilSlots = true;

    // Only the default the Customize Campaign screen starts from.
    [Draw("New campaigns: Restored Empires claims", Tooltip = "Default for the Customize Campaign option of the same name.")]
    public bool restoredEmpires = true;

    // Remembered per faction, so a name written for one never follows you to another.
    public List<FactionNames> savedFactionNames = new List<FactionNames>();

    public override void Save(UnityModManager.ModEntry modEntry) => Save(this, modEntry);

    // UMM calls this the moment a control moves, so this is where a live toggle takes hold for
    // the patches that hold state of their own. Nothing here may throw: it runs from the GUI, and
    // from the main menu where there is no campaign to walk.
    public void OnChange()
    {
        try
        {
            StationSolarMirrors.Invalidate();
            InheritedCapitalClaims.OnSettingChanged();
        }
        catch (Exception e)
        {
            Main.mod?.Logger.Error("Applying a setting change failed - " + e);
        }
    }

    // One-time move from the pre-GUI Settings.txt. FromJsonOverwrite fills the fields it
    // recognizes and ignores the rest, so the old file's values survive the change of format;
    // the original is kept under a new name rather than deleted.
    internal static void MigrateLegacyFile(Settings target, UnityModManager.ModEntry modEntry)
    {
        string legacy = Path.Combine(modEntry.Path, "Settings.txt");
        if (!File.Exists(legacy) || File.Exists(Path.Combine(modEntry.Path, "Settings.xml")))
        {
            return;
        }
        try
        {
            JsonUtility.FromJsonOverwrite(File.ReadAllText(legacy), target);
            target.Save(modEntry);
            File.Move(legacy, legacy + ".migrated");
        }
        catch (Exception e)
        {
            modEntry.Logger.Error("Could not carry Settings.txt over to Settings.xml - " + e.Message);
        }
    }
}

public static class Main
{
    internal static Settings settings = new Settings();
    internal static UnityModManager.ModEntry mod;

    public static bool Load(UnityModManager.ModEntry modEntry)
    {
        mod = modEntry;
        settings = UnityModManager.ModSettings.Load<Settings>(modEntry);
        Settings.MigrateLegacyFile(settings, modEntry);
        // No OnGUI: every setting has a row on the game's own Settings screen, under the
        // CataTweaks tab, and a second copy in the mod manager would only be somewhere else to
        // look. [Draw] stays as the one place a setting's label and tooltip are written.
        Harmony harmony = new Harmony(modEntry.Info.Id);
        harmony.PatchAll(Assembly.GetExecutingAssembly());
        try
        {
            harmony.Patch(ReviewFavorsExpensiveProjects.TargetMethod(), prefix: new HarmonyMethod(
                AccessTools.Method(typeof(ReviewFavorsExpensiveProjects), "Prefix")));
        }
        catch (Exception e)
        {
            modEntry.Logger.Error("Review Failed Projects tweak could not attach - " + e.Message);
        }
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
        if (!Main.settings.habTemplateNaming)
        {
            return;
        }
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
        if (!Main.settings.habTemplateNaming)
        {
            return;
        }
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
        if (!Main.settings.habTemplateNaming)
        {
            return;
        }
        if (__result == null)
        {
            return;
        }
        __result.SetDisplayName(HabNamer.SlugOf(__instance.displayName, HabLocation.Body(__instance)));
        // Tweak 2c: remember the hab's custom map icon on the template, so applying it restores
        // the icon alongside the modules and name. symbolTexture is inert for hab designs
        // (TIHabState.iconResource overrides it with the faction station/base icon) and already
        // serializes with saved designs, so this needs no new save data.
        __result.symbolTexture = __instance.customHabIconResource;
    }
}

// Tweak 2b: saving a hab template under an existing name overwrites the old one.
[HarmonyPatch(typeof(TIFactionState), nameof(TIFactionState.SaveHabDesign))]
internal static class HabTemplateOverwrite
{
    private static void Prefix(TIFactionState __instance, TIHabTemplate habDesign)
    {
        if (!Main.settings.habTemplateNaming)
        {
            return;
        }
        foreach (TIHabTemplate old in __instance.habDesigns
                     .Where(d => d.displayName == habDesign.displayName).ToList())
        {
            __instance.DeleteHabDesign(old.dataName);
        }
    }
}

// Tweak 3a: saving a priority preset under an existing custom preset's name overwrites it
// (built-in presets are never overwritten). If the overwritten preset was the faction's
// default, the new preset becomes the default - control points only copy weights, so no
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
        if (!Main.settings.priorityPresetOverwrite)
        {
            return;
        }
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
    // Read by the two patches below even when this one isn't applied, so it stays put.
    internal static readonly FieldInfo proposedField =
        AccessTools.Field(typeof(NationInfoController), "proposedPriorityPreset");

    private static readonly FieldInfo duplicatedField =
        AccessTools.Field(typeof(NationInfoController), "duplicatedPreset");

    private static void Postfix(NationInfoController __instance)
    {
        if (!Main.settings.priorityPresetOverwrite)
        {
            return;
        }
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
// ponytail: in-memory cache only - after a save reload the first tick primes the cache,
// so a validity flip landing exactly in that gap is missed; per-CP persistence if ever needed.
[HarmonyPatch(typeof(PavonisInteractive.TerraInvicta.Systems.PeriodicUpdates.NationPeriodicUpdate), "DailyNationUpdateTask")]
internal static class ReapplyPresetsOnValidityChange
{
    private static readonly Dictionary<GameStateID, HashSet<PriorityType>> lastInvalid =
        new Dictionary<GameStateID, HashSet<PriorityType>>();

    private static void Postfix(TINationState nation)
    {
        if (!Main.settings.presetTrackingOnNationChanges)
        {
            return;
        }
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
        // Centered in the gap above the given row. With no spacing to sit in it lands on the top
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
        if (!Main.settings.stationDividers)
        {
            return;
        }
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
    private static void Prefix(FleetsScreenController __instance)
    {
        if (!Main.settings.stationDividers)
        {
            return;
        }
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
    // Master switch. The patches below are always attached and check it when they run, so the
    // toggle takes effect immediately; with it off they return before touching anything.
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
                // same center and are comparable. Strictly outward only: a mirror level with or
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
        if (!StationSolarMirrors.Enabled)
        {
            return;
        }
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
            // Re-apply the ceiling rather than letting the bonus run past it. Vanilla's is 8x.
            __result = Mathf.Min(__result + bonus,
                (int)(Main.settings.solarMirrorOutputCap * powerValue));
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
        if (!StationSolarMirrors.Enabled)
        {
            return;
        }
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

// Tweak 7: Review Failed Projects favors the EXPENSIVE missed project instead of the cheapest.
// Vanilla weights each candidate by factionAvailableChance / researchCost, so a 200-research
// throwaway outdraws a 5000-research drive by 25:1 and the review is worst exactly when it
// matters most. This inverts the divide to a multiply, so cost raises a project's odds instead
// of sinking them; availability chance still scales it, keeping genuinely rare projects rare.
//
// The picker is a local function inside TIEffectsState.ProcessInstantEffect, so it compiles to a
// mangled name and its parameter is compiler-generated - hence the runtime name search and the
// positional __0 argument.
//
// No [HarmonyPatch] attribute, so PatchAll skips this one and Main attaches it by hand: the
// target is found by name at runtime, and a rename upstream throwing inside PatchAll would
// abandon every patch after it. Attached alone, it fails alone and says so in the log.
internal static class ReviewFavorsExpensiveProjects
{
    internal static MethodBase TargetMethod()
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

    internal static bool Prefix(TIFactionState __0)
    {
        if (!Main.settings.expensiveFirstProjectReview)
        {
            return true;
        }
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
        if (!Main.settings.priorityPresetOverwrite)
        {
            return;
        }
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
        if (!Main.settings.priorityPresetOverwrite)
        {
            return true;
        }
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
    private static void Postfix(TINationState actingNation, ref IList<TIGameState> __result)
    {
        if (!Main.settings.demandClaimDespiteOtherWars)
        {
            return;
        }
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

// Tweak 9: repeatable income projects scale their payoff alongside their cost.
//
// TIProjectTemplate.GetResearchCost multiplies a repeatable's cost by (1 + times completed), so
// the Nth repeat of Management Research costs N x 600 - but always grants the same flat
// Effect_BSBE_CPMaintenanceBonus5. Research per point of capacity is therefore 120N and diverges:
// 3,240 by the 27th repeat, 12,000 by the 100th, with the cumulative cost of N points growing as
// ~12N^2. Meanwhile the rest of the cap - global freebies, councilor attributes, one admin module
// per station, a fixed list of one-off projects - is hard-bounded, while maintenance cost scales
// with national GDP forever. The repeatable is the only unbounded source and vanilla prices it
// out of reach, so the cap stops rising long before GDP does. Audience, Commercial and
// Operations Research have the same shape with Influence, Money and Operations.
//
// With scaling r the Nth repeat pays base x (1 + r(N-1)), rounded to whole units, so research per
// point converges on cost/r instead of diverging. At the default 0.03 Management Research settles
// around 4,000: about ten times the median one-off CP-cap project (417 across those a faction can
// actually take) and twice the worst one in the game, so grinding this remains strictly worse
// than every alternative while ceasing to be pointless. Set the scaling to 0 for stock behavior.
//
// Keyed on payload shape - repeatable, with positive resourcesGranted or a negative
// ControlPointMaintenance effect - rather than on project names, so another project of the same
// shape is covered without a code change.
internal static class RepeatableScaling
{
    internal static float Rate => Main.settings.repeatableProjectScaling;

    // What the Nth completion pays, rounded - the one figure both applied and displayed. The
    // first completion is left exactly as vanilla has it.
    internal static float Scaled(float baseValue, int repeat) =>
        repeat <= 1
            ? baseValue
            : (float)Math.Round(baseValue * (1f + Rate * (repeat - 1)), MidpointRounding.AwayFromZero);

    internal static int Completions(TIFactionState faction, TIProjectTemplate project) =>
        faction.completedProjects.Count(x => x == project);

    // ControlPointMaintenance effects are stored negative: they reduce maintenance.
    internal static IEnumerable<TIEffectTemplate> CapEffects(TIProjectTemplate project) =>
        project.Effects.Where(e => e != null && e.value < 0f
            && e.GetContexts().Contains(Context.ControlPointMaintenance));

    private static bool IsGrant(ResourceValue x) => x.resource != FactionResource.None && x.value > 0f;

    internal static bool Applies(TIProjectTemplate project) =>
        project.repeatable && (project.resourcesGranted.Any(IsGrant) || CapEffects(project).Any());

    internal static ResourceValue[] ScaledGrants(TIProjectTemplate project, int repeat) =>
        project.resourcesGranted
            .Select(x => IsGrant(x) ? new ResourceValue(x.resource, Scaled(x.value, repeat)) : x)
            .ToArray();

    internal static bool Visible(TIFactionState faction) =>
        faction?.completedProjects != null && !faction.IsAlienFaction;
}

// The cap side is retroactive: recomputed from the repeat count, so it covers repeats finished
// before the mod was installed. Vanilla already counts base once per completion via stacked
// effect instances, so only the rounded uplift of each repeat is added.
[HarmonyPatch(typeof(TIFactionState), nameof(TIFactionState.GetControlPointMaintenanceFreebieCap))]
internal static class RepeatableCapScalesWithCost
{
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
        float extra = 0f;
        foreach (KeyValuePair<TIProjectTemplate, int> pair in counts)
        {
            foreach (TIEffectTemplate effect in RepeatableScaling.CapEffects(pair.Key))
            {
                float baseCap = -effect.value;
                for (int repeat = 2; repeat <= pair.Value; repeat++)
                {
                    extra += RepeatableScaling.Scaled(baseCap, repeat) - baseCap;
                }
            }
        }
        return extra;
    }

    private static void Postfix(TIFactionState __instance, ref float __result)
    {
        if (RepeatableScaling.Rate <= 0f)
        {
            return;
        }
        if (!RepeatableScaling.Visible(__instance))
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

// Resource grants are forward-only: what earlier repeats granted is already spent. Vanilla pays
// base inside OnProjectComplete, so only the rounded uplift is added.
[HarmonyPatch(typeof(TIFactionState), nameof(TIFactionState.OnProjectComplete))]
internal static class RepeatableGrantsScaleWithCost
{
    // A postfix runs even when vanilla returns early (slot project already completed), so only
    // pay out if this call actually recorded a completion.
    // -1 can never match a real completion count, so with the tweak off the postfix's own
    // "did this call record a completion" check is what gates it.
    private static void Prefix(TIFactionState __instance, out int __state) =>
        __state = RepeatableScaling.Rate > 0f ? (__instance.completedProjects?.Count ?? 0) : -1;

    private static void Postfix(TIFactionState __instance, TIProjectTemplate project, bool startup, int __state)
    {
        if (RepeatableScaling.Rate <= 0f)
        {
            return;
        }
        if (startup || project == null || !project.repeatable || !RepeatableScaling.Visible(__instance)
            || __instance.completedProjects.Count <= __state)
        {
            return;
        }
        // completedProjects already includes this completion (AddCompletedProject runs first).
        int repeat = RepeatableScaling.Completions(__instance, project);
        // Routing mirrors vanilla's grant loop.
        foreach (ResourceValue item in project.resourcesGranted)
        {
            float value = item.value > 0f ? RepeatableScaling.Scaled(item.value, repeat) - item.value : 0f;
            if (value <= 0f)
            {
                continue;
            }
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

// UI: the benefit lines show the scaled payoff in place of vanilla's base figure. Vanilla renders
// them from the shared effect and resource templates, so the exact vanilla line is rebuilt and
// swapped for one rendered from scaled values - an effect clone keeps the template's wording.
// The completion notice is built after the project is recorded, so it describes the repeat just
// finished; the research screen describes the next one. The archive keeps vanilla's text.
[HarmonyPatch(typeof(TIProjectTemplate), nameof(TIProjectTemplate.BenefitsDescription))]
internal static class RepeatableBenefitsDescription
{
    private static readonly MethodInfo memberwiseClone = AccessTools.Method(typeof(object), "MemberwiseClone");

    private static string GrantsLine(ResourceValue[] values) =>
        Loc.T("UI.Science.GrantsResources", TIUtilities.BuildResourceValueString(values));

    private static void Postfix(TIProjectTemplate __instance, TIFactionState faction,
        TechBenefitsContext benefitsContext, ref string __result)
    {
        if (RepeatableScaling.Rate <= 0f)
        {
            return;
        }
        if (benefitsContext == TechBenefitsContext.Archive || !RepeatableScaling.Visible(faction)
            || !RepeatableScaling.Applies(__instance))
        {
            return;
        }
        int repeat = RepeatableScaling.Completions(faction, __instance)
            + (benefitsContext == TechBenefitsContext.JustCompleted ? 0 : 1);
        if (repeat <= 1)
        {
            return;
        }
        foreach (TIEffectTemplate effect in RepeatableScaling.CapEffects(__instance))
        {
            var scaled = (TIEffectTemplate)memberwiseClone.Invoke(effect, null);
            scaled.value = -RepeatableScaling.Scaled(-effect.value, repeat);
            __result = __result.Replace(effect.description(faction, null), scaled.description(faction, null));
        }
        __result = __result.Replace(GrantsLine(__instance.resourcesGranted.ToArray()),
            GrantsLine(RepeatableScaling.ScaledGrants(__instance, repeat)));
    }
}

// UI: the "This is a repeatable project..." line also says the payoff grows, and what the next
// attempt will pay. The vanilla line is rebuilt exactly and replaced with the longer wording.
[HarmonyPatch(typeof(TIProjectTemplate), nameof(TIProjectTemplate.WarningsDescription))]
internal static class RepeatableWarningsDescription
{
    // Vanilla's "next attempt" cost is really the attempt in progress - the same repeat the
    // benefit lines describe - so the prediction here is the one after it.
    private static void Postfix(TIProjectTemplate __instance, TIFactionState faction,
        TechBenefitsContext context, ref string __result)
    {
        if (RepeatableScaling.Rate <= 0f)
        {
            return;
        }
        if (!RepeatableScaling.Visible(faction) || !RepeatableScaling.Applies(__instance))
        {
            return;
        }
        int done = RepeatableScaling.Completions(faction, __instance);
        int next = done + (context == TechBenefitsContext.JustCompleted ? 1 : 2);
        float modifier = TIGlobalValuesState.GetResearchSpeedModifier();
        string sprite = TemplateManager.global.researchInlineSpritePath;
        // Rebuilt exactly as vanilla renders it, to find the line to replace.
        string repeating = Loc.T("UI.Science.Repeating",
            (__instance.researchCost / modifier).ToString("N0"),
            (__instance.researchCost * (float)(1 + done) / modifier).ToString("N0"),
            sprite);

        var payoff = new List<string>();
        ResourceValue[] grants = RepeatableScaling.ScaledGrants(__instance, next)
            .Where(x => x.resource != FactionResource.None && x.value > 0f).ToArray();
        if (grants.Length > 0)
        {
            payoff.Add(TIUtilities.BuildResourceValueString(grants));
        }
        float cap = RepeatableScaling.CapEffects(__instance)
            .Sum(e => RepeatableScaling.Scaled(-e.value, next));
        if (cap > 0f)
        {
            payoff.Add(TIUtilities.FormatBigOrSmallNumber(cap) + " control point management capacity");
        }
        string extended = "This is a repeatable project. The required research to finish it increases by "
            + $"{(__instance.researchCost / modifier).ToString("N0")}{sprite} each time it is repeated. "
            + $"Additionally, the reward for this project will increase by {RepeatableScaling.Rate:P0} "
            + "each time it is repeated. Our next attempt at this project will cost "
            + $"{(__instance.researchCost * next / modifier).ToString("N0")}{sprite} "
            + $"and will grant {string.Join(", ", payoff)}.";
        __result = __result.Replace(TIUtilities.GreenLine(repeating), TIUtilities.GreenLine(extended));
    }
}

// Tweak 10: "a new Protectorate fleet docked at Montezuma Base" also says where that base is.
//
// TISpaceFleetState.GetLocationDescription already builds the longer form - "docked at Montezuma
// Base, Low Mars Orbit" - when its expand argument is true, and orbit names carry the body. The
// fleet-detected notification just asks for the short form, which names a hab the player has no
// reason to be able to place. So the argument is flipped for the duration of that one call
// rather than reworded here: landed fleets name their body, docked and transferring ones their
// orbit, and every other case is already self-describing.
[HarmonyPatch(typeof(TINotificationQueueState), nameof(TINotificationQueueState.LogFleetDetected))]
internal static class FleetDetectedLocation
{
    // ThreadStatic: the flag is only meant for the notification's own two calls, so a hab list
    // rendering on another thread inside the same window keeps the short form.
    [ThreadStatic]
    internal static bool expanding;

    // The flag is the gate: with the tweak off it never sets, and nothing expands.
    private static void Prefix() => expanding = Main.settings.fleetDetectionLocation;

    // Finalizer rather than Postfix: it also runs if the notification throws.
    private static void Finalizer() => expanding = false;
}

[HarmonyPatch(typeof(TISpaceFleetState), nameof(TISpaceFleetState.GetLocationDescription))]
internal static class FleetDetectedLocationExpand
{
    private static void Prefix(ref bool expand) => expand |= FleetDetectedLocation.expanding;
}

// Tweak 11: a nation that holds another nation's original capital borrows that nation's claims.
//
// TINationState.AbsorbNation moves regions, control points, nuclear weapons, the space program and
// half the accumulated investment - and not one claim. The absorbed nation keeps its claim list
// while it sits dormant, so the only route to anything it claimed is to release it, rebuild its
// government, let it expand and take it back. That is what forces unification to run strictly
// outside in: merge inward first and every claim past that point is stranded.
//
// So the claims are borrowed, not granted. While you hold a dormant nation's original capital and
// your claim on that capital is not hostile, its claims are yours; lose the capital, or have the
// claim on it turn hostile, and they go again. A nation still holding territory of its own is
// excluded - it can still speak for itself, and its claims are not going spare. Hostility carries across unchanged - a claim the other
// nation held hostilely stays hostile for you, because the point is to skip the merge dance, not
// to launder a grievance into a peaceful merger.
//
// Recomputed when a nation is absorbed, when regions change hands, when a hostile claim turns
// peaceful, when any project completes (claims can be gated behind one), monthly as a backstop, and
// for every nation on load - a load starts from vanilla claims, since borrowed ones are never written.
// Borrowed claims never reach a save file: SaveAllGameStates is bracketed so they are handed back
// before serialization and lent again afterwards. A save written with this on is a vanilla save,
// and turning the tweak off loses nothing.
[HarmonyPatch]
internal static class InheritedCapitalClaims
{
    // What we lent each nation, and whether we lent it hostile, so a recompute takes back exactly
    // what it gave and never touches a claim the nation owns in its own right. RemoveClaim also
    // clears the hostile flag, so the flag has to be remembered here rather than read back off the
    // nation. ponytail: in-memory only - borrowed claims are rebuilt on the first tick after a
    // load, so nothing needs to survive a reload.
    private static readonly Dictionary<TINationState, Dictionary<TIRegionState, bool>> lent =
        new Dictionary<TINationState, Dictionary<TIRegionState, bool>>();

    // originalCapital is set during world init and never moves afterwards (SetCapital changes the
    // working capital, not this), so the region -> nation map is built once and only rebuilt if the
    // nation count changes. Scanning every nation per region per day is otherwise millions of
    // comparisons a turn.
    private static Dictionary<TIRegionState, TINationState> capitalOf;
    private static int mappedNations = -1;

    private static TINationState CapitalHolder(TIRegionState region)
    {
        TINationState[] all = GameStateManager.AllNations();
        if (capitalOf == null || mappedNations != all.Length)
        {
            capitalOf = new Dictionary<TIRegionState, TINationState>();
            foreach (TINationState nation in all)
            {
                if (nation.originalCapital != null)
                {
                    capitalOf[nation.originalCapital] = nation;
                }
            }
            mappedNations = all.Length;
        }
        return capitalOf.TryGetValue(region, out TINationState source) ? source : null;
    }

    // SetClaim on a region already claimed calls RemoveHostileClaim, which is one of our triggers, so
    // lending a claim would recurse back in here mid-lend. One flag, because recomputes are always
    // sequential - RecomputeAll's loop sets and clears it once per nation.
    private static bool recomputing;

    // Called when the setting moves in the UMM window: hand every borrowed claim back on the way
    // off, so turning the tweak off mid-campaign leaves the map exactly as vanilla had it.
    internal static void OnSettingChanged()
    {
        if (Main.settings.inheritedCapitalClaims)
        {
            RecomputeAll();
            return;
        }
        foreach (KeyValuePair<TINationState, Dictionary<TIRegionState, bool>> pair in lent)
        {
            foreach (TIRegionState region in pair.Value.Keys)
            {
                pair.Key.RemoveClaim(region);
            }
        }
        lent.Clear();
    }

    internal static void Recompute(TINationState nation)
    {
        if (!Main.settings.inheritedCapitalClaims || recomputing
            || nation == null || nation.alienNation || nation.claims == null)
        {
            return;
        }
        recomputing = true;
        try
            {
            var want = new Dictionary<TIRegionState, bool>();      // region -> borrow it as hostile
            foreach (TIRegionState capital in nation.regions)
            {
                // The claim on that capital has to exist and be peaceful for its nation to answer to us.
                // ClaimedBy, not claims.Contains: a claim whose unlock project is unresearched sits in
                // the list already and is only gated at query time, and an unearned claim is no claim.
                if (!capital.ClaimedBy(nation) || nation.hostileClaims.Contains(capital))
                {
                    continue;
                }
                TINationState source = CapitalHolder(capital);
                // Dormant nations only. A living nation that moved its capital still speaks for itself,
                // and borrowing from it would be leeching rather than skipping the merge dance.
                if (source == null || source == nation || source.alienNation || source.claims == null
                    || source.extant)
                {
                    continue;
                }
                foreach (TIRegionState claim in source.claims)
                {
                    if (claim == null || claim.nation == nation || !claim.ClaimedBy(source))
                    {
                        continue;                    // gated behind a project nobody has finished
                    }
                    bool hostile = source.hostileClaims.Contains(claim);
                    // two sources disagreeing on hostility: the peaceful reading wins
                    want[claim] = want.TryGetValue(claim, out bool seen) ? (seen && hostile) : hostile;
                }
            }
            if (!lent.TryGetValue(nation, out Dictionary<TIRegionState, bool> held))
            {
                held = lent[nation] = new Dictionary<TIRegionState, bool>();
            }
            foreach (TIRegionState region in held.Keys.ToList())
            {
                if (!want.ContainsKey(region) || want[region] != held[region])
                {
                    nation.RemoveClaim(region);       // gone, or its hostility changed: re-lend below
                    held.Remove(region);
                }
            }
            foreach (KeyValuePair<TIRegionState, bool> pair in want)
            {
                if (held.ContainsKey(pair.Key) || nation.claims.Contains(pair.Key))
                {
                    continue;                        // already lent, or theirs in their own right
                }
                nation.SetClaim(pair.Key, pair.Value, pair.Value);
                held[pair.Key] = pair.Value;
            }
        }
        finally
        {
            recomputing = false;
        }
    }

    // Recompute the moment a merger or conquest changes who holds what, and once a day after that,
    // so a capital that slips away takes its borrowed claims with it.
    [HarmonyPatch(typeof(TINationState), nameof(TINationState.AbsorbNation))]
    [HarmonyPostfix]
    private static void OnAbsorb(TINationState __instance) => Recompute(__instance);

    // A hostile claim turning peaceful is the one way a capital starts answering to us without any
    // region changing hands.
    [HarmonyPatch(typeof(TINationState), nameof(TINationState.RemoveHostileClaim))]
    [HarmonyPostfix]
    private static void OnClaimFlip(TINationState __instance) => Recompute(__instance);

    // Finishing a project can ungate claims anywhere on the map, for any nation, so this one sweeps
    // everybody. Research completion already stalls for a beat; a pass over the nation list is lost
    // in it.
    [HarmonyPatch(typeof(TIFactionState), "AddCompletedProject", typeof(TIProjectTemplate))]
    [HarmonyPostfix]
    private static void OnResearch() => RecomputeAll();

    // Monthly, not daily: the event hooks catch every case where a capital changes hands, so this is
    // only a backstop for claims that shift without one - research unlocking a claim on a capital we
    // already hold, or a claim's hostility flipping. A month's lag on those is nothing.
    [HarmonyPatch(typeof(TINationState), "MonthlyNationUpdate")]
    [HarmonyPostfix]
    private static void Monthly(TINationState __instance) => Recompute(__instance);

    // Regions changing hands outside a merger - a peace deal, a seizure - can hand over or take
    // away a capital, so recompute both sides rather than waiting for the next day.
    [HarmonyPatch(typeof(TINationState), nameof(TINationState.TransferRegionsControlTo))]
    [HarmonyPostfix]
    private static void OnTransfer(TINationState __instance, TINationState newNation)
    {
        Recompute(__instance);
        Recompute(newNation);
    }

    // A load starts from vanilla claims - borrowed ones were never written - so lend them back.
    [HarmonyPatch(typeof(GameStateManager), nameof(GameStateManager.LoadAllGameStates))]
    [HarmonyPostfix]
    private static void OnLoad()
    {
        lent.Clear();
        capitalOf = null;
        RecomputeAll();
    }

    private static void RecomputeAll()
    {
        foreach (TINationState nation in GameStateManager.AllNations())
        {
            Recompute(nation);
        }
    }

    // Saves stay vanilla: hand every borrowed claim back, let the game serialize, then lend again.
    [HarmonyPatch(typeof(GameStateManager), nameof(GameStateManager.SaveAllGameStates))]
    [HarmonyPrefix]
    private static void BeforeSave()
    {
        recomputing = true;   // no trigger may re-lend while the claims are handed back
        foreach (KeyValuePair<TINationState, Dictionary<TIRegionState, bool>> pair in lent)
        {
            foreach (TIRegionState region in pair.Value.Keys)
            {
                pair.Key.RemoveClaim(region);
            }
        }
    }

    // Finalizer, not a postfix: a save that throws still has to give the claims back.
    [HarmonyPatch(typeof(GameStateManager), nameof(GameStateManager.SaveAllGameStates))]
    [HarmonyFinalizer]
    private static void AfterSave()
    {
        foreach (KeyValuePair<TINationState, Dictionary<TIRegionState, bool>> pair in lent)
        {
            foreach (KeyValuePair<TIRegionState, bool> claim in pair.Value)
            {
                pair.Key.SetClaim(claim.Key, claim.Value, claim.Value);
            }
        }
        recomputing = false;
    }
}

// Tweak 12: the two turned-councilor slots double as councilor slots.
//
// The council screen has eight slots: six for your councilors, two for councilors you have turned
// in other factions. The spy slots sit idle in most campaigns - the second one especially, since
// even the AI only values a spy when it has none - while a fully developed council is stuck at six.
//
// So the eight become one pool. Finish both council size projects and every spy slot you are not
// using is a councilor slot instead: no spies, eight councilors; one spy, seven; two spies, six,
// exactly as vanilla. Turning is blocked when the pool is full, so the trade runs both ways.
//
// Nothing before both size projects changes. A council still capped at four or five keeps its two
// spy slots untouched, so this is a late reward for a finished council rather than an early buff.
//
// Both caps are one expression each and every consumer reads them - the recruit button, the AI's
// influence budgeting, emptyCouncilorSlots, the Turn mission's targeting condition - so the AI
// plays the mechanic without being taught it. What it will not do is hold a slot open for a spy:
// it fills councilor slots as soon as it can afford one, so AI factions will tend to run eight
// councilors and no spies. That is the intended trade, taken in the direction the numbers favor.
internal static class CouncilSlotPool
{
    // The council grid is built for eight. Not a setting: a larger pool would need a bigger grid.
    internal const int Pool = 8;



    // What a council seats before any CouncilSize effect is counted.
    private const float Base = 4f;

    // Ours, hidden entirely when the campaign was not started with the pool enabled: a project
    // that cannot do anything should not be sitting in the tech tree asking to be researched.
    private static readonly string[] Projects =
    {
        "Project_CataTweaks_CouncilSize7",
        "Project_CataTweaks_CouncilSize8",
    };

    [HarmonyPatch(typeof(TIFactionState), nameof(TIFactionState.AddAvailableProject),
        typeof(TIProjectTemplate), typeof(ProjectTrigger))]
    internal static class HideProjects
    {
        private static bool Prefix(TIProjectTemplate project, ref bool __result)
        {
            if (project == null)
            {
                return true;
            }
            bool ours = Array.IndexOf(Projects, project.dataName) >= 0
                || Array.IndexOf(RestoredEmpires.Projects, project.dataName) >= 0;
            bool hidden =
                (!CampaignFlags.CouncilPool && Array.IndexOf(Projects, project.dataName) >= 0)
                || (!CampaignFlags.RestoredEmpires
                    && Array.IndexOf(RestoredEmpires.Projects, project.dataName) >= 0)
                // requiresNation is not the gate it looks like: PrereqsSatisfied only rejects a
                // project whose required nation exists and is gone, so one naming a nation the
                // scenario never had passes. Rome exists in Broken Earth alone, and its two
                // projects would otherwise be offered in every start with nothing to claim.
                || (ours && !string.IsNullOrEmpty(project.requiresNation)
                    && project.requiredNationState == null);
            if (!hidden)
            {
                return true;
            }
            __result = false;
            return false;
        }
    }

    [HarmonyPatch(typeof(TIFactionState), nameof(TIFactionState.maxCouncilSize), MethodType.Getter)]
    internal static class MaxCouncilSize
    {
            private static void Postfix(TIFactionState __instance, ref int __result)
        {
            if (!CampaignFlags.CouncilPool || __instance.turnedCouncilors == null)
            {
                return;
            }
            // Vanilla clamps to six however many CouncilSize effects are held, which is what stops
            // the two projects this mod adds from counting. Recompute without that clamp, then
            // clamp to what the pool has left after spies instead.
            int earned = (int)(Base + TIEffectsState.SumEffectsModifiers(
                Context.CouncilSize, __instance, Base));
            __result = Mathf.Clamp(Mathf.Min(earned, Pool - __instance.turnedCouncilors.Count),
                0, Pool);
        }
    }

    // The whole vanilla condition is "fewer than two spies". Now the pool has to have room too,
    // and the same condition name comes back so the UI explains the block the way it always has.
    [HarmonyPatch(typeof(TIMissionCondition_HasSpySlot), nameof(TIMissionCondition_HasSpySlot.CanTarget))]
    internal static class HasSpySlot
    {
            private static void Postfix(TICouncilorState councilor, ref string __result)
        {
            if (!CampaignFlags.CouncilPool)
            {
                return;
            }
            TIFactionState faction = councilor?.faction;
            if (__result == "_Pass" && faction != null
                && faction.councilors.Count + faction.turnedCouncilors.Count >= Pool)
            {
                __result = nameof(TIMissionCondition_HasSpySlot);
            }
        }
    }

    // Vanilla packs the grid into a fixed TICouncilorState[8] with the turned councilors written at
    // index 6 - so a seventh councilor lands on top of a spy and a ninth would be off the end of the
    // array. Councilors now fill from the front and spies from the back, which leaves the vanilla
    // layout alone at six and two. The array is sized off the actual counts as well as the pool,
    // because a councilor can be turned by a route that never consults the cap: an assassination
    // can hand a rival faction the victim as a vengeful defector.
    [HarmonyPatch(typeof(CouncilGridController), nameof(CouncilGridController.UpdateCouncilorGrid))]
    internal static class Grid
    {
            private static bool Prefix(CouncilGridController __instance)
        {
            if (!CampaignFlags.CouncilPool)
            {
                return true;
            }
            TIFactionState player = GameControl.control.activePlayer;
            int size = Mathf.Max(Pool, player.councilors.Count + player.turnedCouncilors.Count);
            TICouncilorState[] slots = new TICouncilorState[size];
            int next = 0;
            foreach (TICouncilorState councilor in player.councilors)
            {
                slots[next++] = councilor;
            }
            next = size - 1;
            foreach (TICouncilorState spy in player.turnedCouncilors)
            {
                slots[next--] = spy;
            }

            __instance.councilorGrid.SetListSize<CouncilorGridItemController>(size);
            CouncilorGridItemController[] items =
                __instance.councilorGrid.GetComponentsInChildren<CouncilorGridItemController>(true);
            for (int i = 0; i < items.Length && i < size; i++)
            {
                items[i].Init(__instance, i);
                if (slots[i] == null)
                {
                    items[i].primaryPanel.SetActive(false);
                    continue;
                }
                items[i].UpdateListItem(slots[i]);
                items[i].primaryPanel.SetActive(true);
                if (items[i].councilorVideo.clip != null && !items[i].councilorVideo.isPlaying)
                {
                    if (!__instance.councilorVideo.isPrepared)
                    {
                        TIUtilities.TryPrepareVideo(items[i].councilorVideo);
                    }
                    __instance.StartCoroutine(
                        __instance.PlayVideoWhenPrepared(items[i].councilorVideo));
                }
            }
            return false;
        }
    }
}

// Every [Draw] setting mirrored onto the vanilla Gameplay options tab, under a CataTweaks header.
//
// The tab is hand-wired - OptionsMenuController holds a serialized Toggle or Slider and a separate
// TMP_Text title per setting, each bound to its own handler in the editor - so there is no list to
// append to and a row has to be cloned from one that already exists. The list itself is a
// VerticalLayoutGroup, so sibling order is position and nothing needs placing by hand.
//
// The rows are driven off the same [Draw] attributes UMM reads, so a new setting shows up in both
// windows at once and the two cannot drift apart. Vanilla's own store is left alone: SavePlayerConfig
// deletes and rewrites PlayerOptions.TIProfile from a fixed list of keys, so anything added there
// would be dropped the next time the player touched any vanilla option. These write Settings.xml.
// OptionsMenuController lives inside the Gameplay pane, so its OnEnable does not fire until that
// pane is activated - our tab would appear only after clicking Gameplay, which is a strange way to
// meet it. The manager's own Start runs while the screen is being built, and it can reach the
// controller whether the pane is active or not.
[HarmonyPatch(typeof(TabbedPaneManager), "Start")]
internal static class VanillaOptionsTabAtStartup
{
    private static void Postfix(TabbedPaneManager __instance)
    {
        OptionsMenuController menu = __instance.GetComponentInChildren<OptionsMenuController>(true);
        if (menu != null)
        {
            VanillaOptionsRows.Build(menu);
        }
    }
}

[HarmonyPatch(typeof(OptionsMenuController), "OnEnable")]
internal static class VanillaOptionsRows
{
    // The row that opens each section, and what to call it. Order comes from the order the
    // settings are declared in, so a field moved in Settings moves here too.
    private static readonly Dictionary<string, string> Sections = new Dictionary<string, string>
    {
        { "persistCampaignOptions", "Quality of Life and UI" },
        { "solarMirrorsBoostStations", "Gameplay" },
    };
    private const string PaneName = "CataTweaks_Pane";

    // Fixed for the life of a campaign and headed for the new-campaign screen instead.
    private static readonly string[] Excluded =
    {
        "spySlotsAsCouncilSlots",
        "restoredEmpires",
    };

    // Settings that only mean something while another is on, and gray out with it.
    private static readonly Dictionary<string, string> DependsOn = new Dictionary<string, string>
    {
        { "solarMirrorOutputCap", "solarMirrorsBoostStations" },
    };

    private static readonly Dictionary<string, string> Suffixes = new Dictionary<string, string>
    {
        { "solarMirrorOutputCap", "x" },
    };

    // Stored as a fraction, read by a player as a percentage.
    private static readonly HashSet<string> Percents = new HashSet<string>
    {
        "repeatableProjectScaling",
    };


    private static void Postfix(OptionsMenuController __instance) => Build(__instance);

    internal static void Build(OptionsMenuController __instance)
    {
        try
        {
            // Unity's == reports a destroyed object as null; C#'s ?. does not, and reaching
            // through one for .transform throws from native code.
            Toggle toggleTemplate = __instance.showEarthLightsToggle;
            Slider sliderTemplate = __instance.maxShipsInCombatSlider;
            if (toggleTemplate == null || sliderTemplate == null
                || __instance.showEarthLightsToggleTitle == null)
            {
                return;
            }
            Transform toggleSource = Row<Toggle>(toggleTemplate.transform);
            Transform sliderSource = Row<Slider>(sliderTemplate.transform);
            if (toggleSource == null || sliderSource == null || toggleSource.parent == null)
            {
                return;
            }
            // Our own tab if one can be built, otherwise the end of the Gameplay list.
            Transform list = CataTab(toggleSource) ?? toggleSource.parent;
            EnsureClipping(list);

            // Only needed when sharing vanilla's list; our own tab is already all ours.
            if (list == toggleSource.parent)
            {
                Header(list, __instance.showEarthLightsToggleTitle, "CataTweaks_Header",
                    "CataTweaks").SetAsLastSibling();
            }

            for (Transform t = list; t != null; t = t.parent)
            {
                if (t.name == "CataTweaks_Pane")
                {
                    Unlock(t);
                    break;
                }
            }

            Dictionary<string, Transform> rows = new Dictionary<string, Transform>();
            Action refresh = () => Refresh(rows);

            foreach (FieldInfo field in Drawn())
            {
                if (Sections.TryGetValue(field.Name, out string section))
                {
                    Header(list, __instance.showEarthLightsToggleTitle,
                        "CataTweaks_Section_" + field.Name, section).SetAsLastSibling();
                }
                DrawAttribute draw = field.GetCustomAttribute<DrawAttribute>();
                Transform row = field.FieldType == typeof(bool)
                    ? Toggle(list, toggleSource, __instance.showEarthLightsToggleTitle,
                        field, draw, refresh)
                    : Slider(list, sliderSource, __instance.maxShipsInCombatTitle,
                        __instance.maxShipsInCombatValue, field, draw);
                if (row != null)
                {
                    rows[field.Name] = row;
                    row.SetAsLastSibling();
                }
            }
            refresh();
        }
        catch (Exception e)
        {
            // An options screen that throws is worse than one without our rows.
            Main.mod?.Logger.Error("Could not add the options rows - " + e);
        }
    }

    // Declaration order, which is the order they read in the UMM window too.
    private static IEnumerable<FieldInfo> Drawn() =>
        typeof(Settings).GetFields()
            .Where(f => Array.IndexOf(Excluded, f.Name) < 0
                && f.GetCustomAttribute<DrawAttribute>() != null
                && (f.FieldType == typeof(bool) || f.FieldType == typeof(float)));

    private static Transform Toggle(Transform list, Transform source, TMP_Text title,
        FieldInfo field, DrawAttribute draw, Action refresh)
    {
        Transform row = CloneRow(list, source, field.Name, draw.Label, title, null);
        if (row == null)
        {
            return null;
        }
        Tooltip(row, draw.Tooltip);
        Toggle toggle = row.GetComponentInChildren<Toggle>(true);
        // Set every time, not inherited from the row we copied: a clone keeps whatever state its
        // source happened to be in at that instant, and in a campaign the row is cloned while the
        // screen is still loading. Refresh runs after this and turns off anything gated.
        toggle.interactable = true;
        // Assigning a fresh event drops the editor-wired persistent call too; RemoveAllListeners
        // would only clear runtime ones and leave vanilla's own handler firing from our row.
        toggle.onValueChanged = new Toggle.ToggleEvent();
        toggle.isOn = (bool)field.GetValue(Main.settings);
        toggle.onValueChanged.AddListener(on =>
        {
            field.SetValue(Main.settings, on);
            refresh();
            Commit();
        });
        return row;
    }

    private static Transform Slider(Transform list, Transform source, TMP_Text title,
        TMP_Text value, FieldInfo field, DrawAttribute draw)
    {
        Transform row = CloneRow(list, source, field.Name, draw.Label, title, value);
        if (row == null)
        {
            return null;
        }
        Tooltip(row, draw.Tooltip);
        Suffixes.TryGetValue(field.Name, out string suffix);
        readouts.TryGetValue(field.Name, out TMP_Text readout);
        bool percent = Percents.Contains(field.Name);
        string format = draw.Max <= 1.0 ? "0.00" : "0.0";
        Func<float, string> show = v => percent
            ? (v * 100f).ToString("0") + "%"
            : v.ToString(format) + suffix;

        Slider slider = row.GetComponentInChildren<Slider>(true);
        slider.interactable = true;
        slider.onValueChanged = new Slider.SliderEvent();
        slider.wholeNumbers = false;
        slider.minValue = (float)draw.Min;
        slider.maxValue = (float)draw.Max;
        // Set before the listener attaches, so opening the screen is not itself a change.
        slider.value = Mathf.Clamp((float)field.GetValue(Main.settings),
            slider.minValue, slider.maxValue);
        readout?.SetText(show(slider.value));
        slider.onValueChanged.AddListener(v =>
        {
            field.SetValue(Main.settings, v);
            readout?.SetText(show(v));
            Commit();
        });
        return row;
    }

    // Two things make this more than SetText. The Gameplay rows we clone carry no TooltipTrigger
    // at all - vanilla gives that screen none - so one has to be added, with a style borrowed from
    // a trigger that already exists. And where a clone does bring one (the campaign screen's), it
    // arrives with valueOnDemand set and its delegate gone, because a delegate cannot survive
    // Instantiate: it would call nothing and render blank however much text we set.
    internal static void Tooltip(Transform row, string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            foreach (TooltipTrigger off in row.GetComponentsInChildren<TooltipTrigger>(true))
            {
                off.enabled = false;
            }
            return;
        }

        TooltipTrigger[] triggers = row.GetComponentsInChildren<TooltipTrigger>(true);
        if (triggers.Length == 0)
        {
            TooltipStyle style = Style();
            if (style == null)
            {
                return;
            }
            TooltipTrigger added = row.gameObject.AddComponent<TooltipTrigger>();
            added.tooltipStyle = style;
            triggers = new[] { added };
        }
        foreach (TooltipTrigger trigger in triggers)
        {
            trigger.enabled = true;
            trigger.SetText("BodyText", text);
            if (trigger.parameterizedTextFields == null)
            {
                continue;
            }
            foreach (ParameterizedTextField field in trigger.parameterizedTextFields)
            {
                field.valueOnDemand = false;
                field.del = null;
            }
        }
    }

    // Any style the game already uses, so ours look like every other tooltip.
    private static TooltipStyle cachedStyle;

    private static TooltipStyle Style()
    {
        if (cachedStyle == null)
        {
            cachedStyle = Resources.FindObjectsOfTypeAll<TooltipTrigger>()
                .Select(t => t.tooltipStyle)
                .FirstOrDefault(style => style != null);
        }
        return cachedStyle;
    }

    // A setting whose master switch is off is left visible but dead, rather than hidden: it still
    // says what it would do.
    private static void Refresh(Dictionary<string, Transform> rows)
    {
        foreach (KeyValuePair<string, string> pair in DependsOn)
        {
            if (!rows.TryGetValue(pair.Key, out Transform row))
            {
                continue;
            }
            FieldInfo master = typeof(Settings).GetField(pair.Value);
            bool on = master != null && (bool)master.GetValue(Main.settings);
            // Every control in the row, not just the first found: the row builders set
            // interactable true unconditionally, so anything missed here would stay live.
            foreach (Selectable control in row.GetComponentsInChildren<Selectable>(true))
            {
                control.interactable = on;
            }
            foreach (TMP_Text text in row.GetComponentsInChildren<TMP_Text>(true))
            {
                text.alpha = on ? 1f : 0.35f;
            }
        }
    }

    // Clone `source` once, name it after its setting and relabel it. Where the source row carries
    // a second text showing its number, that text is remembered so the caller can keep it current.
    private static Transform CloneRow(Transform list, Transform source, string name, string label,
        TMP_Text title, TMP_Text value)
    {
        if (title == null)
        {
            return null;
        }
        Transform existing = list.Find(name);
        if (existing != null)
        {
            return existing;
        }

        TMP_Text[] before = source.GetComponentsInChildren<TMP_Text>(true);
        int labelIndex = Array.IndexOf(before, title);
        int valueIndex = value != null ? Array.IndexOf(before, value) : -1;

        GameObject clone = UnityEngine.Object.Instantiate(source.gameObject, list);
        clone.name = name;

        TMP_Text[] texts = clone.GetComponentsInChildren<TMP_Text>(true);
        if (labelIndex >= 0 && labelIndex < texts.Length)
        {
            texts[labelIndex].SetText(label);
        }
        if (valueIndex >= 0 && valueIndex < texts.Length)
        {
            readouts[name] = texts[valueIndex];
        }
        return clone.transform;
    }

    // Which TMP_Text in each cloned row shows its number - resolved once, when the row is built.
    private static readonly Dictionary<string, TMP_Text> readouts =
        new Dictionary<string, TMP_Text>();

    // Built rather than cloned from a row: a header has no control to strip out, and where the
    // label sits inside a row's hierarchy is exactly the guesswork this avoids. It borrows its
    // font and color from a real row label so it belongs to the screen it is sitting on.
    private static Transform Header(Transform list, TMP_Text style, string name, string label)
    {
        Transform existing = list.Find(name);
        if (existing != null)
        {
            return existing;
        }
        GameObject header = new GameObject(name, typeof(RectTransform));
        header.transform.SetParent(list, worldPositionStays: false);

        TextMeshProUGUI text = header.AddComponent<TextMeshProUGUI>();
        text.font = style.font;
        text.fontSize = style.fontSize;
        text.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
        text.color = style.color;
        text.alignment = TextAlignmentOptions.BottomLeft;
        text.SetText(label);

        LayoutElement size = header.AddComponent<LayoutElement>();
        size.minHeight = style.fontSize * 2.4f;
        size.preferredHeight = size.minHeight;
        return header.transform;
    }

    private static void Commit()
    {
        Main.settings.OnChange();
        if (Main.mod != null)
        {
            Main.settings.Save(Main.mod);
        }
    }

    // The outermost container that still holds exactly this one control. The rows nest - the
    // earth-lights row is a DynamicEarthLights inside another DynamicEarthLights - and only the
    // outer one is a sibling of the other settings, so stopping at the first ancestor holding
    // both control and label drops the clone into a container with its single slot already taken.
    private static Transform Row<T>(Transform control) where T : Component
    {
        if (control == null)
        {
            return null;
        }
        Transform row = control;
        while (row.parent != null && row.parent.GetComponentsInChildren<T>(true).Length == 1)
        {
            row = row.parent;
        }
        return row;
    }

    // A CataTweaks tab of its own, cloned from the Gameplay one, rather than eleven rows bolted to
    // the end of somebody else's list.
    //
    // TabbedPaneManager finds its tabs with GetComponentsInChildren<TabbedPaneController>, so a
    // clone parented under it registers itself, and TabbedPaneController.Start wires its own button
    // with `paneManager.Toggle(this)` - a closure over the clone, not the original. The one thing
    // that does not take care of itself is the serialized `tab` field pointing at the button: a
    // clone inherits the *original's* button, so clicking Gameplay would drive both panes.
    //
    // Hence the inactive holder. Awake runs the moment an active object is instantiated and caches
    // the button, so the clone is built parented to a disabled object, corrected there, and only
    // then moved under the manager - which is when Awake and Start finally run, with the right
    // button in hand.
    private static Transform CataTab(Transform anyRowInGameplay)
    {
        TabbedPaneController gameplay = anyRowInGameplay.GetComponentInParent<TabbedPaneController>();
        TabbedPaneManager manager = gameplay != null
            ? gameplay.GetComponentInParent<TabbedPaneManager>() : null;
        if (gameplay == null || manager == null)
        {
            return null;
        }

        // Where the pane actually goes, and so where to look for one already made: Find only
        // searches direct children, and under the manager it would never see it.
        Transform container = gameplay.transform.parent;
        Transform made = container != null ? container.Find(PaneName) : null;
        if (made != null)
        {
            return Content(made);
        }

        FieldInfo tabField = AccessTools.Field(typeof(TabbedPaneController), "tab");
        GameObject sourceButton = tabField?.GetValue(gameplay) as GameObject;
        if (sourceButton == null || sourceButton.transform.parent == null)
        {
            Main.mod?.Logger.Log("No tab button behind the Gameplay pane; staying on its list.");
            return null;
        }

        GameObject holder = new GameObject("CataTweaks_TabBuild");
        holder.SetActive(false);
        try
        {
            GameObject pane = UnityEngine.Object.Instantiate(gameplay.gameObject, holder.transform);
            pane.name = PaneName;

            GameObject button = UnityEngine.Object.Instantiate(sourceButton,
                sourceButton.transform.parent);
            button.name = PaneName + "_Tab";
            button.transform.SetSiblingIndex(sourceButton.transform.GetSiblingIndex() + 1);
            foreach (TMP_Text text in button.GetComponentsInChildren<TMP_Text>(true))
            {
                text.SetText("CataTweaks");
            }

            tabField.SetValue(pane.GetComponent<TabbedPaneController>(), button);

            // Everything cloned from the Gameplay pane is vanilla's rows; the list itself, with its
            // layout group and scroll rect, is what we wanted. DestroyImmediate because the pane is
            // inactive and about to be read again this frame - a deferred Destroy would leave the
            // old rows in place while we filled it.
            Transform content = Content(pane.transform);
            if (content == null)
            {
                return null;
            }
            foreach (Transform child in content.Cast<Transform>().ToList())
            {
                UnityEngine.Object.DestroyImmediate(child.gameObject);
            }

            // A second OptionsMenuController on the clone would run its own OnEnable against the
            // rows we just deleted - and vanilla's OnEnable calls resetTutorialButton.SetActive
            // unguarded, inside a branch that only runs once a campaign is loaded. It lives on a
            // child of the pane, not its root, so this has to search the whole clone.
            foreach (OptionsMenuController stowaway in
                pane.GetComponentsInChildren<OptionsMenuController>(true))
            {
                UnityEngine.Object.DestroyImmediate(stowaway);
            }

            // Beside the Gameplay pane, not under the manager: the manager's own children are the
            // tab button row and the container the panes sit in, so adding a pane directly to it
            // lands a full-size panel in the tab bar's layout.
            pane.transform.SetParent(container, worldPositionStays: false);
            pane.transform.SetSiblingIndex(gameplay.transform.GetSiblingIndex() + 1);

            // Awake and Start run here, with the corrected button.
            pane.SetActive(true);

            // The manager hands this to each tab in its own Awake, which ran long before ours.
            TabbedPaneController added = pane.GetComponent<TabbedPaneController>();
            added.activeTabHeightOffset = manager.tabVerticalSpacing;

            Unlock(pane.transform);
            return Content(pane.transform);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(holder);
        }
    }

    // The pane nests: a GameplayTabPane inside a GameplayTabPane, the same doubling as the rows.
    // Both carry a CanvasGroup, and TabbedPaneController.Show only ever sets the one on its own
    // object, so the inner group keeps whatever it held when the clone was taken. In a campaign
    // that is interactable=false, and a CanvasGroup beats Selectable.interactable, which is why
    // the rows drew greyed out and ignored clicks however many times they were set interactable.
    internal static void Unlock(Transform pane)
    {
        foreach (CanvasGroup group in pane.GetComponentsInChildren<CanvasGroup>(true))
        {
            group.interactable = true;
            group.blocksRaycasts = true;
            group.alpha = 1f;
        }
    }

    private static Transform Content(Transform pane)
    {
        ScrollRect scroll = pane.GetComponentInChildren<ScrollRect>(true);
        return scroll != null ? scroll.content : null;
    }

    // Vanilla's list fit its viewport exactly, so the stencil Mask sitting there was never asked
    // to clip anything; with our rows past the bottom it still does not, and they draw over the
    // menu behind the panel. RectMask2D clips by rect, needs no stencil state, and is harmless
    // alongside the Mask already there.
    private static void EnsureClipping(Transform list)
    {
        ScrollRect scroll = list.GetComponentInParent<ScrollRect>();
        RectTransform viewport = scroll != null ? scroll.viewport : list.parent as RectTransform;
        if (viewport == null || viewport.GetComponent<RectMask2D>() != null)
        {
            return;
        }
        viewport.gameObject.AddComponent<RectMask2D>();
    }

}

// Per-campaign settings, chosen on Customize Campaign and stored in the save.
//
// They ride in ScenarioCustomizations.customFactionText under keys no faction is named. That
// dictionary is only ever reached by key, in TIFactionState and TISpaceAssetState, and never
// enumerated, so extra entries are inert. Its sibling customFactionStartingNationGroup would not
// do: two sites iterate that one and match on the int value, then resolve the key as a faction, so
// a foreign key there is an NRE waiting for a group id to collide.
//
// Plain dictionary entries rather than fields of our own mean a save written with this mod still
// loads without it. A subclass of ScenarioCustomizations would serialize a $type that cannot
// resolve, and the save would not open at all.
internal static class CampaignFlags
{
    private const string Prefix = "CataTweaks.";

    private static ScenarioCustomizations Current =>
        TIGlobalValuesState.GlobalValues != null
            ? TIGlobalValuesState.GlobalValues.scenarioCustomizations
            : GameControl.control?.scenarioCustomizationsStartup;

    // A campaign started without going through Customize Campaign records nothing, so the answer
    // falls back to the default in Settings.xml rather than to a hardcoded one.
    internal static bool CouncilPool => Get("councilPool", Main.settings.spySlotsAsCouncilSlots);

    internal static bool RestoredEmpires => Get("restoredEmpires", Main.settings.restoredEmpires);

    private static bool Get(string name, bool fallback)
    {
        ScenarioCustomizations customizations = Current;
        if (customizations?.customFactionText == null)
        {
            return fallback;
        }
        return customizations.customFactionText.TryGetValue(
            Prefix + name, out ScenarioCustomizations.CustomFactionText entry)
            ? entry.customDisplayName == "1"
            : fallback;
    }

    internal static void Write(ScenarioCustomizations customizations, string name, bool on)
    {
        if (customizations?.customFactionText == null)
        {
            return;
        }
        customizations.customFactionText.Remove(Prefix + name);
        customizations.customFactionText.Add(Prefix + name,
            new ScenarioCustomizations.CustomFactionText(on ? "1" : "0", "", "", "", "", "", "", ""));
    }

    // SetCustomCampaignOptions builds the whole object from the screen and ends by cloning it into
    // GameControl, so the entries go onto the clone the campaign will actually be built from.
    [HarmonyPatch(typeof(StartMenuController), "SetCustomCampaignOptions")]
    internal static class Store
    {
        private static void Postfix()
        {
            ScenarioCustomizations startup = GameControl.control?.scenarioCustomizationsStartup;
            foreach (CampaignOptionsRows.Option option in CampaignOptionsRows.Options)
            {
                Write(startup, option.Name, option.Chosen);
            }
        }
    }
}

// The dataNames this mod's own templates add, read from the files it ships rather than listed
// here twice. Regex rather than a JSON parser: one capture on a file we wrote ourselves is not
// worth a dependency, and a miss costs a claim staying live rather than anything breaking.
internal static class ModTemplates
{
    private static HashSet<string> claims;

    internal static HashSet<string> Claims
    {
        get
        {
            if (claims == null)
            {
                claims = Read("TIBilateralTemplate.json");
            }
            return claims;
        }
    }

    private static HashSet<string> Read(string file)
    {
        HashSet<string> found = new HashSet<string>();
        try
        {
            string path = Path.Combine(Main.mod?.Path ?? "", file);
            if (!File.Exists(path))
            {
                return found;
            }
            foreach (Match match in Regex.Matches(File.ReadAllText(path),
                "\"dataName\"\\s*:\\s*\"([^\"]+)\""))
            {
                found.Add(match.Groups[1].Value);
            }
        }
        catch (Exception e)
        {
            Main.mod?.Logger.Error("Could not read " + file + " - " + e.Message);
        }
        return found;
    }
}

// Restored Empires, folded in and switchable per campaign.
//
// Its four projects hide the same way the council ones do. Its claims mostly gate themselves:
// almost every row hangs off one of those projects, and BilateralIsActive is already
// projectUnlock?.SomeoneHasDoneIt() ?? true, so a hidden project takes its claims with it. The few
// ungated rows, Jamaica in Broken Earth and Ireland for the UK, need the gate applied directly.
// BilateralIsActive is the same funnel the game itself checks through TIRegionState.ClaimedBy, so
// nothing routes around it.
internal static class RestoredEmpires
{
    internal static readonly string[] Projects =
    {
        "Project_TheSunNeverSets",
        "Project_MareNostrum",
        "Project_ImperiumSineFine",
        "Project_NewLiberia",
    };

    [HarmonyPatch(typeof(TIBilateralTemplate), nameof(TIBilateralTemplate.BilateralIsActive))]
    internal static class HideClaims
    {
        private static void Postfix(TIBilateralTemplate __instance, ref bool __result)
        {
            if (__result && !CampaignFlags.RestoredEmpires
                && ModTemplates.Claims.Contains(__instance.dataName))
            {
                __result = false;
            }
        }
    }
}

// The per-campaign toggles, at the bottom of Customize Campaign under a header of their own.
[HarmonyPatch(typeof(StartMenuController), "Initialize")]
internal static class CampaignOptionsRows
{
    internal class Option
    {
        internal string Name;
        internal string Label;
        internal string Tip;
        internal Func<bool> Default;
        internal bool Chosen;
    }

    internal static readonly Option[] Options =
    {
        new Option
        {
            Name = "councilPool",
            Label = "Spy Slots Become Councilor Slots",
            Tip = "The council screen's eight slots become one pool shared between your councilors "
                + "and councilors you have turned in other factions. Every spy you hold costs a "
                + "councilor slot: no spies seats eight, two spies seats six.\n\n"
                + "Seats seven and eight are unlocked by two projects, Deep Cover Handlers and "
                + "Shadow Cabinet, which continue the chain after Covert Operations. With this "
                + "off, both projects are hidden.",
            Default = () => Main.settings.spySlotsAsCouncilSlots,
        },
        new Option
        {
            Name = "restoredEmpires",
            Label = "Restored Empires Claims",
            Tip = "Adds claim chains and projects that give old powers a route back to the map.\n\n"
                + "The Sun Never Sets extends the United Kingdom past the settler dominions, and "
                + "Commonwealth Restored gains Delhi, Calcutta and Ireland. New Liberia lets the "
                + "Dominion of America reach the West African coast. Both apply in every scenario, "
                + "though New Liberia opens only a few regions outside Broken Earth.\n\n"
                + "Mare Nostrum and Imperium Sine Fine give Rome the Mediterranean world, the "
                + "eastern roads and both American seaboards. They appear in Broken Earth only, "
                + "which is the one scenario the Roman Republic exists in, and the only one where "
                + "these chains reach the whole map.\n\n"
                + "Claims on territories that fought their way out are hostile; small dependencies "
                + "are peaceful. With this off, the projects are hidden and the claims are inert.",
            Default = () => Main.settings.restoredEmpires,
        },
    };

    private const string HeaderName = "CataTweaks_CampaignHeader";
    private const string GridName = "CataTweaks_CampaignGrid";

    private static void Postfix(StartMenuController __instance)
    {
        try
        {
            Toggle template = __instance.variableProjectUnlocksToggle;
            TMP_Text title = __instance.variableProjectUnlocksText;
            TMP_Text headerStyle = __instance.campaignOptionsFactionNamesHeaderText;
            if (template == null || title == null || headerStyle == null)
            {
                return;
            }

            // The toggles sit in a GridLayoutGroup of their own, so anything added there takes a
            // toggle-sized cell and pushes the grid over what follows. Sections live one level up,
            // in the scroll content: the header goes there, and so does a grid of our own, which
            // keeps vanilla's cell sizing for the toggles inside it.
            Transform sourceRow = Outermost<Toggle>(template.transform);
            Transform grid = sourceRow.parent;
            Transform list = grid != null ? grid.parent : null;
            if (list == null)
            {
                return;
            }

            foreach (Option option in Options)
            {
                option.Chosen = option.Default();
            }

            if (list.Find(HeaderName) == null)
            {
                // Cloned from "Your Faction Names" rather than built, so it carries that section
                // header's own font, size and spacing without having to guess at them.
                Transform styleRow = Outermost<TMP_Text>(headerStyle.transform);
                GameObject made = UnityEngine.Object.Instantiate(styleRow.gameObject, list);
                made.name = HeaderName;
                foreach (TMP_Text text in made.GetComponentsInChildren<TMP_Text>(true))
                {
                    text.SetText("CATATWEAKS");
                }
            }

            if (list.Find(GridName) == null)
            {
                GameObject ours = UnityEngine.Object.Instantiate(grid.gameObject, list);
                ours.name = GridName;

                // Keep one cell per option and discard the rest. DestroyImmediate because the
                // survivors are read back in this same call.
                List<Transform> cells = ours.transform.Cast<Transform>().ToList();
                for (int i = Options.Length; i < cells.Count; i++)
                {
                    UnityEngine.Object.DestroyImmediate(cells[i].gameObject);
                }
                if (cells.Count < Options.Length)
                {
                    Main.mod?.Logger.Log("Campaign grid had "
                        + cells.Count + " cells for " + Options.Length + " options.");
                    return;
                }

                int labelIndex = Array.IndexOf(
                    sourceRow.GetComponentsInChildren<TMP_Text>(true), title);
                for (int i = 0; i < Options.Length; i++)
                {
                    Build(cells[i], Options[i], labelIndex, title);
                }
            }

            list.Find(HeaderName).SetAsLastSibling();
            list.Find(GridName).SetAsLastSibling();
            PersistCampaignOptions.WatchPanel(list, __instance);
        }
        catch (Exception e)
        {
            // A start menu that throws is a start menu nobody can use.
            Main.mod?.Logger.Error("Could not add the campaign options - " + e);
        }
    }

    private static void Build(Transform cell, Option option, int labelIndex, TMP_Text title)
    {
        cell.name = "CataTweaks_" + option.Name;

        TMP_Text[] texts = cell.GetComponentsInChildren<TMP_Text>(true);
        if (labelIndex >= 0 && labelIndex < texts.Length)
        {
            // Take the vanilla label's type settings as well as its slot: with autosizing on, a
            // longer string alone would render at a different size to its neighbors.
            TMP_Text label = texts[labelIndex];
            label.font = title.font;
            label.fontSize = title.fontSize;
            label.fontSizeMin = title.fontSizeMin;
            label.fontSizeMax = title.fontSizeMax;
            label.enableAutoSizing = title.enableAutoSizing;
            label.fontStyle = title.fontStyle;
            label.alignment = title.alignment;
            label.SetText(option.Label);
        }

        VanillaOptionsRows.Tooltip(cell, option.Tip);

        Toggle toggle = cell.GetComponentInChildren<Toggle>(true);
        // A fresh event drops the editor-wired call to OnToggleVariableProjectUnlocks.
        toggle.onValueChanged = new Toggle.ToggleEvent();
        toggle.isOn = option.Chosen;
        toggle.onValueChanged.AddListener(on => option.Chosen = on);
    }

    // Same nesting as the options rows: the control sits inside a container that is the real row.
    private static Transform Outermost<T>(Transform control) where T : Component
    {
        Transform row = control;
        while (row.parent != null && row.parent.GetComponentsInChildren<T>(true).Length == 1)
        {
            row = row.parent;
        }
        return row;
    }
}

// Custom faction names, remembered against the faction they were written for.
public class FactionNames
{
    public string faction;
    public string displayName;
    public string adjective;
    public string leader;
    public string fleet;
}

// Campaign options carried between sessions.
//
// The game already stores them: StoreCampaignOptions writes TIPlayerProfileManager
// .storedCampaignOptions when a campaign starts, and the Previous Campaign Settings button reads
// them back. All that is missing is applying them without being asked, which is what
// SetDefaultCampaignOptions is doing when it resets the screen instead.
//
// Faction names are not part of that store at all, so they are kept here, keyed by faction. A name
// written for the Resistance must not follow you to the Servants, which is why they restore from
// LoadFactionTextFields, the one place the game fills those four boxes for a chosen faction.
internal static class PersistCampaignOptions
{
    private static readonly FieldInfo scenarioField =
        AccessTools.Field(typeof(StartMenuController), "selectedScenario");

    private static readonly MethodInfo setPrevious =
        AccessTools.Method(typeof(StartMenuController), "SetPreviousCampaignOptions");

    internal static string SelectedFaction(StartMenuController menu)
    {
        try
        {
            PavonisInteractive.TerraInvicta.Systems.Bootstrap.IScenario scenario =
                scenarioField?.GetValue(menu)
                    as PavonisInteractive.TerraInvicta.Systems.Bootstrap.IScenario;
            return scenario?.activePlayerFaction?.dataName;
        }
        catch (Exception)
        {
            return null;
        }
    }

    // Vanilla stores the options from exactly one place, OnLaunchCustomCampaignClicked, so
    // nothing you set is remembered unless you go through with starting the campaign. This saves
    // them as the panel closes, whichever way it closes: cancel, escape, or launch.
    internal class Saver : MonoBehaviour
    {
        internal StartMenuController menu;

        private static readonly MethodInfo store =
            AccessTools.Method(typeof(StartMenuController), "StoreCampaignOptions");

        private void OnDisable()
        {
            try
            {
                if (!Main.settings.persistCampaignOptions || menu == null || store == null)
                {
                    return;
                }
                // Our own postfix on this rides along, so faction names are saved with it.
                store.Invoke(menu, null);
                TIPlayerProfileManager.SavePlayerConfig();
            }
            catch (Exception e)
            {
                Main.mod?.Logger.Error("Could not store campaign options - " + e.Message);
            }
        }
    }

    internal static void WatchPanel(Transform anyChild, StartMenuController menu)
    {
        for (Transform t = anyChild; t != null; t = t.parent)
        {
            if (t.name != "CustomizeCampaignPanel")
            {
                continue;
            }
            Saver saver = t.GetComponent<Saver>() ?? t.gameObject.AddComponent<Saver>();
            saver.menu = menu;
            return;
        }
        Main.mod?.Logger.Log("No CustomizeCampaignPanel above " + anyChild.name
            + "; options will only be stored on launch.");
    }

    // Where the screen would otherwise reset to stock values.
    [HarmonyPatch(typeof(StartMenuController), "SetDefaultCampaignOptions")]
    internal static class ApplyStored
    {
        private static void Postfix(StartMenuController __instance)
        {
            try
            {
                if (!Main.settings.persistCampaignOptions || setPrevious == null
                    || !TIPlayerProfileManager.storedCampaignOptions.isValid)
                {
                    return;
                }
                setPrevious.Invoke(__instance, null);
            }
            catch (Exception e)
            {
                Main.mod?.Logger.Error("Could not restore campaign options - " + e.Message);
            }
        }
    }

    // Written when a campaign is actually started, alongside the game's own store.
    [HarmonyPatch(typeof(StartMenuController), "StoreCampaignOptions")]
    internal static class RememberNames
    {
        private static void Postfix(StartMenuController __instance)
        {
            try
            {
                string faction = SelectedFaction(__instance);
                if (!Main.settings.persistCampaignOptions || string.IsNullOrEmpty(faction))
                {
                    return;
                }
                Main.settings.savedFactionNames.RemoveAll(x => x != null && x.faction == faction);
                Main.settings.savedFactionNames.Add(new FactionNames
                {
                    faction = faction,
                    displayName = __instance.customDisplayNameInput.text,
                    adjective = __instance.customAdjectiveInput.text,
                    leader = __instance.customLeaderAddressInput.text,
                    fleet = __instance.customFleetInput.text,
                });
                if (Main.mod != null)
                {
                    Main.settings.Save(Main.mod);
                }
            }
            catch (Exception e)
            {
                Main.mod?.Logger.Error("Could not remember faction names - " + e.Message);
            }
        }
    }

    // Runs whenever the four name boxes are filled for a faction, including after switching to a
    // different one, so a remembered name only ever reappears on the faction it was written for.
    [HarmonyPatch(typeof(StartMenuController), "LoadFactionTextFields")]
    internal static class RestoreNames
    {
        private static void Postfix(StartMenuController __instance)
        {
            try
            {
                string faction = SelectedFaction(__instance);
                if (!Main.settings.persistCampaignOptions || string.IsNullOrEmpty(faction))
                {
                    return;
                }
                FactionNames saved = Main.settings.savedFactionNames
                    .FirstOrDefault(x => x != null && x.faction == faction);
                if (saved == null)
                {
                    return;
                }
                Fill(__instance.customDisplayNameInput, saved.displayName);
                Fill(__instance.customAdjectiveInput, saved.adjective);
                Fill(__instance.customLeaderAddressInput, saved.leader);
                Fill(__instance.customFleetInput, saved.fleet);
            }
            catch (Exception e)
            {
                Main.mod?.Logger.Error("Could not restore faction names - " + e.Message);
            }
        }

        // An empty remembered value means the player never renamed that field, so the faction's
        // own name stays rather than being blanked.
        private static void Fill(TMP_InputField field, string value)
        {
            if (field != null && !string.IsNullOrEmpty(value))
            {
                field.SetTextWithoutNotify(value);
            }
        }
    }
}
