using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using PavonisInteractive.TerraInvicta;
using PavonisInteractive.TerraInvicta.Actions;
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
        __result?.SetDisplayName(HabNamer.SlugOf(__instance.displayName, HabLocation.Body(__instance)));
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
