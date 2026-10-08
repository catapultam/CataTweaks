using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using HarmonyLib;
using FullSerializer;
using FullSerializer.Internal;
using ModelShark;
using TMPro;
using PavonisInteractive.TerraInvicta;
using PavonisInteractive.TerraInvicta.Actions;
using PavonisInteractive.TerraInvicta.Audio;
using UnityEngine;
using UnityEngine.UI;
using UnityModManagerNet;

namespace CataTweaks;

// Settings live in the Unity Mod Manager window (Ctrl+F10 by default), under this mod's entry.
// UMM draws one control per [Draw] field, writes the settings file on Save (see GetPath: not
// in the mod folder, because the game empties that of anything the Workshop item lacks), and
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
    [Draw("Remember campaign options between sessions", Tooltip = "The Customize Campaign screen opens with the options from your last campaign. A custom faction name is saved for each faction, and appears again when you select that faction.")]
    public bool persistCampaignOptions = true;

    // Habs renamed "Name (Template, Location)" when a saved template is applied, and saving a hab
    // as a template round-trips that name plus its map icon, overwriting the old one in place.
    [Draw("Hab template naming", Tooltip = "When you apply a hab template, the hab is renamed to Name (Template, Orbit, Body) and takes the map icon of the template. When you save a hab as a template, the template takes the name in brackets and replaces the template that has that name. You can apply a template that changes nothing, to rename a hab.")]
    public bool habTemplateNaming = true;

    // Saving a priority preset under an existing custom preset's name overwrites it, instead of
    // the save button going dead.
    [Draw("Priority presets overwrite in place", Tooltip = "When you save a priority preset with the name of an existing custom preset, it replaces that preset. Control points that use the old preset move to the new one. The new preset stays the default preset if the old one was the default.")]
    public bool priorityPresetOverwrite = true;

    // Control points re-apply their preset when a nation's valid priorities change, instead of
    // the profile flipping to 'Custom'.
    [Draw("Nations keep their preset when priorities change", Tooltip = "When a nation gains or loses a priority, its preset is applied again. The nation does not change to Custom.")]
    public bool presetTrackingOnNationChanges = true;

    // Full-width dividing rules between stations on the ship construction screen.
    [Draw("Dividers between stations in the ship construction screen", Tooltip = "The ship construction list shows a line between the shipyards of one station and the shipyards of the next station.")]
    public bool stationDividers = false;

    [Draw("One construction queue per station", Tooltip = "All shipyards on a station share one ordered queue. Idle compatible yards take the next ship, with each yard retaining its own construction speed. Ships already under construction keep their progress.")]
    public bool pooledStationConstruction = true;

    [Draw("Automatic shipyard rally (experimental)", Tooltip = "Experimental. Requires One construction queue per station. Adds a fleet dropdown to station shipyard cards. Completed ships take the earliest affordable rendezvous and merge on arrival. Disabling this option stops new rallies; rallies already ordered finish normally.")]
    public bool shipyardRally = true;

    private bool previousStationDividers;
    private bool previousPooledConstruction = true;

    internal void NormalizeShipyardOptions(string changed = null)
    {
        if (stationDividers && pooledStationConstruction)
        {
            if (changed == nameof(stationDividers)
                || (changed == null && stationDividers != previousStationDividers
                    && pooledStationConstruction == previousPooledConstruction))
                pooledStationConstruction = false;
            else stationDividers = false;
        }
        previousStationDividers = stationDividers;
        previousPooledConstruction = pooledStationConstruction;
    }

    // Fleet-detected notifications name the hab's orbit or body as well as the hab itself.
    [Draw("Fleet detections name the orbit or body", Tooltip = "A fleet detection notice gives the orbit and the body of the hab. For a landed fleet it gives the body.")]
    public bool fleetDetectionLocation = true;

    // The nation panel's name becomes a dropdown of the nations we hold a control point in,
    // with the map mode picker's arrows either side of it.
    [Draw("Nation picker on the nation panel", Tooltip = "The nation name on the nation panel becomes a dropdown. The dropdown lists the nations where you hold a control point, in name order. Previous and next arrows are on each side of it, and they continue from the last nation to the first.")]
    public bool nationCycleButtons = true;

    // Portraits stop tracking the age cut-off, so the customize screen stops offering what look
    // like two different sets of art. Off by default: it changes how every councilor looks.
    [Draw("Councilor portraits do not change with age", Tooltip = "A councilor keeps the same portrait, icon and video at every age, and the customize screen offers the same portraits for every councilor. A councilor over 55 no longer switches to a second, older version of the art.")]
    public bool unagedCouncilorPortraits = false;

    // Solar mirrors boost orbital stations inward of them, not just surface bases.
    [Draw("Solar mirrors boost orbital stations", Tooltip = "Solar mirrors add power to orbital stations and to surface bases. A mirror lights the stations that orbit inward of it. A mirror at a Lagrange point lights the same stations as before. Surface bases do not change.")]
    public bool solarMirrorsBoostStations = false;

    // Review Failed Projects favors the expensive missed project instead of the cheapest.
    // The ceiling on what a mirror-lit station's solar modules may produce, as a multiple of their
    // unlit output. 8 is vanilla's own figure and the default; lower it to blunt mirrors without
    // switching them off. Surface bases keep vanilla's 8x either way - this patch only ever runs
    // for stations.
    [Draw("Solar mirror orbital boost cap", DrawType.Slider, Min = 2f, Max = 8f, Tooltip = "The maximum output of the solar modules of a mirror-lit station, as a multiple of their unlit output. This applies to orbital stations only. The standard limit is 8.")]
    public float solarMirrorOutputCap = 8f;

    [Draw("Project review favors the expensive project", Tooltip = "Review Failed Projects weights each project by its availability chance multiplied by its research cost. An expensive project is more likely to be selected.")]
    public bool expensiveFirstProjectReview = true;

    // Launch facility priorities pick their region properly: vanilla drops the filter that was
    // meant to keep occupied regions out, and then weights the roll by existing boost times
    // 500000, so a region that already launches outdraws the equator by three orders of
    // magnitude and the nation never opens a better site.
    [Draw("Boost priority prefers better launch sites", Tooltip = "A completed Boost priority can build its launch facilities in the best available region of the nation. The best region is the eligible region nearest the equator. A region under occupation is not eligible. The Boost priority tooltip shows the range of the eligible regions.")]
    public bool betterLaunchSites = true;

    // How much of the roll is taken away from vanilla's weighting and handed to the best site.
    // A straight mix of the two distributions, so the number means what it says: at 40%, two
    // completions in five go to the best site and the other three roll as vanilla does. 0 is
    // vanilla's weighting, kept for the bug fixes alone; 100% always builds at the best site.
    [Draw("Launch site focus", DrawType.Slider, Min = 0f, Max = 1f, Tooltip = "How often a completed Boost priority builds in the best available region. At 0% the game selects the region as the standard game does, which almost always adds to a region that already has launch facilities. At 100% the nation always builds in the best region.")]
    public float launchSiteFocus = 0.5f;

    // Both halves of the same change to dormancy: who it can reach, and whether it is final.
    // Off by default, as a change to the shape of the campaign rather than a repair.
    [Draw("Any faction can go dormant, and can come back", Tooltip = "The campaign option Allow AI Factions To Be Disabled spares three factions. In a game with all factions these are the Servants, the Protectorate and Humanity First. These three factions can now go dormant on the same terms as the others. Only the aliens and your own faction cannot. A faction that is dormant comes back when it holds two control points again. It comes back with no money, no orgs and no income from its founding or from events. This setting does nothing if the campaign option is off.")]
    public bool factionDormancy = false;

    // "Demand Claim" only blocks when the two nations are at war with each other, instead of
    // when the target is at war with anyone at all.
    [Draw("Demand Claim ignores the target's other wars", Tooltip = "A war blocks Demand Claim only if it is a war between the two nations in the claim.")]
    public bool demandClaimDespiteOtherWars = true;

    // A nation that holds another nation's original capital borrows that nation's claims for as
    // long as it holds it, so unification no longer has to be worked strictly from the outside in.
    [Draw("Holding a capital borrows that nation's claims", Tooltip = "While you hold the original capital of a dormant nation, you can use the claims of that nation. Your claim on the capital must not be hostile. You lose the borrowed claims when you lose the capital, or when your claim on it becomes hostile. A claim that the other nation held as hostile stays hostile for you. A nation that still holds territory is excluded. A region you take through a borrowed claim is held by a hostile claim, so you must legitimize a capital you take that way before you can borrow its claims. A unification is not a conquest: a region that comes with one, and that you then borrow a peaceful claim on, is held peacefully. After any hostile regions, investment in Government and Unity also legitimizes the claims you borrow as hostile, one at a time: the claim becomes peaceful for the nation you borrow it from, so it stays peaceful for whoever holds that capital. A project that needs a nation to exist, such as Greater Indonesia, is also available while a dormant nation's capital is held without a hostile claim: its claims go to the dormant nation, and whoever holds the capital borrows them. The region screen goes on crediting the claim to the nation it came from, so you can always see which capital is carrying it.")]
    public bool inheritedCapitalClaims = true;

    // A nation that comes out of a war without its own capital, sitting on the capital of a
    // dormant nation instead, becomes that nation.
    [Draw("A nation left on a dormant nation's capital becomes that nation", Tooltip = "When a war ends and a nation has lost its original capital to an enemy in that war, the nation looks at its new capital. If that region is the original capital of a dormant nation, and the dormant nation has a claim on it, the dormant nation comes back with all the regions of the nation. Each faction keeps its control points and their priorities. Stats, armies, nuclear weapons, space funding and accumulated investment move across. Alliances, rivalries and federation membership do not. The change happens on the next day after the nation is at peace with everyone.")]
    public bool dormantCapitalIdentity = true;

    // Repeatable projects granting control point capacity or resources (Management, Audience,
    // Commercial, Operations Research) scale their payoff by this fraction of the base value per
    // repeat, matching the way their cost already scales. 0 disables the patches entirely and
    // restores vanilla. 0.03 puts the grind at roughly ten times the cost per point of the
    // median one-off project, so it stays a deliberately poor last resort.
    [Draw("Repeatable management project scaling", DrawType.Slider, Min = 0f, Max = 0.2f, Tooltip = "Management Research, Audience Research, Commercial Research and Operations Research pay this percentage of their base reward in addition, for each repeat you have already completed. Management Research grants control point capacity. The other three grant Influence, Money and Operations. At 0% each repeat pays the standard flat amount.")]
    public float repeatableProjectScaling = 0.03f;

    // Control points join orgs, habs and projects on the diplomacy table, and the AI values
    // them: it asks a price for its own and pays for yours. On by default while it is new.
    [Draw("Allow trading control points", Tooltip = "You can put control points on the diplomacy table with orgs, habs and projects. They are on a separate tab, listed by nation. The AI gives them a value, and refuses a deal that it does not accept.")]
    public bool tradeControlPoints = true;

    // How heavily the AI weighs a control point against everything else on the table. The
    // arithmetic under it - income, investment points, armies, an executive multiplier, and a
    // tenth of the value to a holder that has abandoned the nation - is in ControlPointTrade
    // .Value. This is the one knob over the top of it, because that arithmetic has no vanilla
    // scale to be calibrated against.
    [Draw("Control point base value", DrawType.Slider, Min = 0.5f, Max = 5f, Tooltip = "The value that the AI gives a control point in a trade, against orgs, habs, projects and resources. A higher value makes the AI ask more for its own control points and pay more for yours. A control point in a nation that its holder has abandoned is worth one tenth of this value.")]
    public float controlPointTradeValue = 2f;

    // Why a seat is suppressed is not written down anywhere: a crackdown mission and the
    // holder's own Disable Control Points button both end at ResolveCrackdownEffect and set the
    // same two fields, and the voluntary one does not touch permaAbandonedNations either. So
    // this is one switch over both rather than a guess at which is which. Suppression travels
    // with the seat whatever it is set to, so a trade and a trade back cannot clear one. It
    // covers abandoned seats too: abandoning a nation suppresses its seats, and the toggle that
    // marks a nation abandoned is an automation switch a player can flip at will, so gating on
    // it would be gating on nothing.
    [Draw("Allow trading suppressed control points", Tooltip = "You can put a control point that is under a crackdown on the diplomacy table. This includes a control point in a nation that its holder has abandoned. The crackdown moves with the control point, and the new owner completes the remaining time.")]
    public bool tradeSuppressedControlPoints = false;

    // A pact is not a rule the game enforces: what ends one is the hate a hostile mission hands
    // the other side, so leaving a pact standing means not handing over that hate.
    [Draw("Allow purge of friendly control points", Tooltip = "When you purge a suppressed control point of a faction that has a non-aggression pact or a truce with you, that faction does not become angry and the pact continues. A purge of any other control point makes the faction angry.")]
    public bool friendlyPurge = false;

    // Vanilla marks a target whose faction has a pact with an inline icon in the target list and
    // then drops it from the line it writes for the target actually chosen, which is the moment
    // it matters.
    [Draw("Warn before a mission breaks a pact", Tooltip = "A mission against a faction that has a non-aggression pact or a truce with you marks the selected target. The game asks you to confirm before it assigns the councilor.")]
    public bool warnOnPactBreak = true;

    // Show Triggered Projects is counted as a difficulty change by the Customize Campaign screen,
    // which marks the campaign custom and costs it the three difficulty win achievements.
    [Draw("Show Triggered Projects keeps achievements", Tooltip = "The Show Triggered Projects campaign option does not mark a campaign as custom difficulty. The Normal, Veteran and Brutal victory achievements stay available in that campaign. Every other campaign option still marks a campaign as custom difficulty.")]
    public bool triggeredProjectsKeepAchievements = true;

    // Only the default the Customize Campaign screen starts from. The campaign's own answer is
    // stored in its save, so changing this never reaches a campaign already under way.
    [Draw("New campaigns: unused spy slots become councilor slots", Tooltip = "Sets the default for the Customize Campaign option with the same name.")]
    public bool spySlotsAsCouncilSlots = true;

    // Only the default the Customize Campaign screen starts from.
    [Draw("New campaigns: Restored Empires claims", Tooltip = "Sets the default for the Customize Campaign option with the same name.")]
    public bool restoredEmpires = true;

    // Remembered per faction, so a name written for one never follows you to another.
    public List<FactionNames> savedFactionNames = new List<FactionNames>();

    // Where the settings file lives, for both the load and the save: UMM asks this for each.
    //
    // Not the mod folder. Terra Invicta resyncs a Workshop mod folder against the subscribed
    // copy on every launch - "CataTweaks: need to check for update" in the player log - and
    // deletes every file the Workshop item does not contain, Settings.xml with them. A setting
    // written there therefore lasts until the next start and no longer, which is to say that
    // nobody who installs from the Workshop can keep a setting at all.
    //
    // The game's own folder under My Games is where PlayerOptions.TIProfile sits, one level
    // down in Saves, and nothing sweeps it. MyDocuments is asked for rather than assumed,
    // because the folder moves under OneDrive redirection. The game's own GetSaveFolderPath
    // would be the more faithful answer, since a player can point the game somewhere else
    // entirely, but it caches what it computes and it is called here long before the game has
    // read that preference - so calling it now would cache the wrong folder for the game too.
    public override string GetPath(UnityModManager.ModEntry modEntry)
    {
        try
        {
            string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            if (!string.IsNullOrEmpty(documents))
            {
                return Path.Combine(documents, "My Games", "TerraInvicta", "CataTweaks.xml");
            }
        }
        catch (Exception)
        {
            // Fall through to the mod folder, which is better than not saving at all.
        }
        return Path.Combine(modEntry.Path, "Settings.xml");
    }

    // StreamWriter will not create the folder, and a player who has never launched the game
    // does not have one.
    public override void Save(UnityModManager.ModEntry modEntry)
    {
        try
        {
            string folder = Path.GetDirectoryName(GetPath(modEntry));
            if (!string.IsNullOrEmpty(folder))
            {
                Directory.CreateDirectory(folder);
            }
        }
        catch (Exception e)
        {
            modEntry.Logger.Error("Could not prepare the settings folder - " + e.Message);
        }
        Save(this, modEntry);
    }

    // UMM calls this the moment a control moves, so this is where a live toggle takes hold for
    // the patches that hold state of their own. Nothing here may throw: it runs from the GUI, and
    // from the main menu where there is no campaign to walk.
    public void OnChange()
    {
        try
        {
            NormalizeShipyardOptions();
            StationSolarMirrors.Invalidate();
            StationConstruction.RefreshSettings();
        }
        catch (Exception e)
        {
            Main.mod?.Logger.Error("Applying a setting change failed - " + e);
        }
    }

    // Settings written by an earlier version sit in the mod folder, where the game deletes them
    // on its next Workshop sync. Anything still there is carried over before the first load,
    // while it is still there to carry. The original is left where it is: the sync will take it.
    internal static void MigrateModFolderFile(UnityModManager.ModEntry modEntry)
    {
        try
        {
            string old = Path.Combine(modEntry.Path, "Settings.xml");
            string current = new Settings().GetPath(modEntry);
            if (!File.Exists(old) || File.Exists(current) || old == current)
            {
                return;
            }
            string folder = Path.GetDirectoryName(current);
            if (!string.IsNullOrEmpty(folder))
            {
                Directory.CreateDirectory(folder);
            }
            File.Copy(old, current);
        }
        catch (Exception e)
        {
            modEntry.Logger.Error("Could not move the old settings file - " + e.Message);
        }
    }

    // One-time move from the pre-GUI Settings.txt. FromJsonOverwrite fills the fields it
    // recognizes and ignores the rest, so the old file's values survive the change of format;
    // the original is kept under a new name rather than deleted.
    internal static void MigrateLegacyFile(Settings target, UnityModManager.ModEntry modEntry)
    {
        // Against the file that is actually in use, not the mod folder's: settings now live
        // outside the mod folder, and a Settings.txt that turns up beside a reinstalled mod
        // would otherwise be read over the top of them and saved.
        string legacy = Path.Combine(modEntry.Path, "Settings.txt");
        if (!File.Exists(legacy) || File.Exists(target.GetPath(modEntry)))
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
        Settings.MigrateModFolderFile(modEntry);
        settings = UnityModManager.ModSettings.Load<Settings>(modEntry);
        Settings.MigrateLegacyFile(settings, modEntry);
        settings.NormalizeShipyardOptions(nameof(Settings.pooledStationConstruction));
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
        // Read now rather than from the first claim query that needs it, which would land the
        // file read and its regex in the middle of a frame.
        _ = ModTemplates.Claims.Count;
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
        if (!Main.settings.stationDividers || Main.settings.pooledStationConstruction)
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
    // Clear even after the divider option switches off, so old spacers cannot be mistaken
    // for real cards when the list manager rebuilds the grid.
    private static void Prefix(FleetsScreenController __instance)
    {
        StationDivider.Clear(__instance);
        // ListManagerBase clones a live card as its template. Restore native geometry
        // first so newly added cards do not inherit an already-reflowed section layout.
        if (__instance.shipyardGridList != null)
            foreach (object entry in __instance.shipyardGridList)
                if (entry is ShipyardGridItemController card)
                {
                    card.GetComponent<StationConstructionSlots>()?.Hide();
                    card.GetComponent<StationConstructionRallyPicker>()?.Hide();
                }
    }
}

// A station queue is stored in vanilla's queues: paid heads stay in their yards and the
// unpaid tail lives in the highest-tier yard. Nothing new needs save serialization. Never
// return this combined view from TIFactionState.GetShipyardQueue: simulation and actions
// need the actual mutable per-module lists.
internal static class StationConstruction
{
    internal static bool dispatching;
    internal static int dailyUpdateDepth;
    internal static int editingDepth;

    internal static bool Enabled(TIFactionState faction, TIHabModuleState yard) =>
        Main.settings.pooledStationConstruction && faction != null && faction.isActivePlayer
        && yard?.ref_hab != null && yard.ref_hab.IsStation;

    internal static bool Enabled(ShipyardGridItemController card) =>
        Enabled(card.controller.activePlayer, card.shipyardIdx);

    internal static List<TIHabModuleState> Yards(TIFactionState faction, TIHabState hab) =>
        faction.nShipyardQueues.Keys.Where(y => y.ref_hab == hab)
            .OrderByDescending(y => y.tier).ThenBy(y => y.ID).ToList();

    internal static string YardName(TIHabModuleState yard) => yard.moduleTemplate.displayName;

    internal static List<ShipConstructionQueueItem> View(TIFactionState faction, TIHabModuleState yard)
    {
        if (!Enabled(faction, yard)) return faction.GetShipyardQueue(yard);
        var items = Yards(faction, yard.ref_hab).SelectMany(y => faction.GetShipyardQueue(y)).ToList();
        return items.Where(i => i.costPaid).Concat(items.Where(i => !i.costPaid)).ToList();
    }

    internal static bool Compatible(ShipConstructionQueueItem item, TIHabModuleState yard) =>
        item.isRefit || item.shipDesign.CanBuildAtShipyard(yard);

    private static void Transfer(TIFactionState faction, ShipConstructionQueueItem item, TIHabModuleState to)
    {
        if (item.shipyard == to) return;
        var from = item.shipyard;
        item.shipyard = to;
        try { Reprice(faction, item); }
        catch { item.shipyard = from; throw; }
        faction.GetShipyardQueue(from).Remove(item);
        faction.GetShipyardQueue(to).Add(item);
    }

    private static void Reprice(TIFactionState faction, ShipConstructionQueueItem item)
    {
        var to = item.shipyard;
        var cost = item.isRefit
            ? item.shipDesign.RefitResourceCost(to, item.refit_originalShipDesign, true, true, item.originalSpaceShipState)
            : item.shipDesign.spaceResourceConstructionCost(false, to);
        if (item.isRefit) cost.GetRefundCost(out item.resourcesRefund);
        if (item.resourcesCost.GetSingleCostValue(FactionResource.Boost) > 0f)
            cost = TISpaceShipTemplate.MixedResourceConstructionCost(faction, to.hab, cost, faction.AvailableSpaceResources());
        item.UpdateResourcesCost(cost);
    }

    internal static void Normalize(TIFactionState faction, TIHabState hab)
    {
        var yards = Yards(faction, hab);
        if (yards.Count == 0) return;
        // Snapshot before moving; the anchor's existing tail always precedes incoming work.
        var waiting = yards.SelectMany(y => faction.GetShipyardQueue(y)).Where(i => !i.costPaid).ToList();
        foreach (var item in waiting) Transfer(faction, item, yards[0]);
    }

    internal static void Dispatch(TIFactionState faction, TIHabState hab)
    {
        if (dispatching || dailyUpdateDepth > 0 || editingDepth > 0) return;
        var yards = Yards(faction, hab);
        if (yards.Count == 0 || !Enabled(faction, yards[0])) return;
        dispatching = true;
        try
        {
            Normalize(faction, hab);
            var waiting = View(faction, yards[0]).Where(i => !i.costPaid).ToList();
            var idle = yards.Where(y => y.active && !faction.GetShipyardQueue(y).Any(i => i.costPaid)).ToList();
            foreach (var item in waiting)
            {
                var yard = idle.Where(y => Compatible(item, y))
                    .OrderBy(y => item.isRefit
                        ? item.shipDesign.RefitResourceCost(y, item.refit_originalShipDesign, true, true, item.originalSpaceShipState).completionTime_days
                        : item.shipDesign.hullTemplate.constructionTime_Days(y)).FirstOrDefault();
                if (yard == null) continue; // A larger hull must not block a smaller free yard.
                Transfer(faction, item, yard);
                var queue = faction.GetShipyardQueue(yard);
                queue.Remove(item);
                queue.Insert(0, item);
                Reprice(faction, item);
                if (!faction.AttemptInitiateShipConstruction(yard)) break; // Keep resource priority.
                idle.Remove(yard);
            }
            Normalize(faction, hab);
            // Failed starts and skipped incompatible hulls retain their relative priority.
            var tail = faction.GetShipyardQueue(yards[0]);
            tail.RemoveAll(i => !i.costPaid);
            tail.AddRange(waiting.Where(i => !i.costPaid));
        }
        finally { dispatching = false; }
    }

    internal static void DispatchAll(TIFactionState faction)
    {
        foreach (var hab in faction.nShipyardQueues.Keys.Where(y => Enabled(faction, y))
            .Select(y => y.ref_hab).Distinct().ToList()) Dispatch(faction, hab);
    }

    internal static void RefreshSettings()
    {
        foreach (var screen in UnityEngine.Object.FindObjectsOfType<FleetsScreenController>())
            screen.RefreshConstructionManager();
    }

    internal static TIHabModuleState BuildYard(ShipyardGridItemController card)
    {
        var design = card.controller.constructionManagerSelectedDesign;
        return Yards(card.controller.activePlayer, card.shipyardIdx.ref_hab)
            .Where(y => design != null && design.CanBuildAtShipyard(y))
            .OrderByDescending(y => y.active).ThenBy(y => design.hullTemplate.constructionTime_Days(y))
            .FirstOrDefault();
    }

    internal static float FinishTime(TIFactionState faction, TIHabModuleState representative, TISpaceShipTemplate design)
    {
        var yards = Enabled(faction, representative) ? Yards(faction, representative.ref_hab)
            : new List<TIHabModuleState> { representative };
        var available = yards.Where(y => y.active).ToDictionary(y => y,
            y => faction.GetShipyardQueue(y).Where(i => i.costPaid).Sum(i => i.daysToCompletion));
        foreach (var item in View(faction, representative).Where(i => !i.costPaid))
        {
            var yard = available.Keys.Where(y => Compatible(item, y)).OrderBy(y => available[y] +
                (item.isRefit ? item.shipDesign.RefitResourceCost(y, item.refit_originalShipDesign, true, true, item.originalSpaceShipState).completionTime_days
                    : item.shipDesign.hullTemplate.constructionTime_Days(y))).FirstOrDefault();
            if (yard != null) available[yard] += item.isRefit
                ? item.shipDesign.RefitResourceCost(yard, item.refit_originalShipDesign, true, true, item.originalSpaceShipState).completionTime_days
                : item.shipDesign.hullTemplate.constructionTime_Days(yard);
        }
        return available.Keys.Where(y => design.CanBuildAtShipyard(y)).Select(y => available[y] +
            design.hullTemplate.constructionTime_Days(y)).DefaultIfEmpty(float.PositiveInfinity).Min();
    }

    internal static void Buttons(ShipyardGridItemController card)
    {
        var queue = View(card.controller.activePlayer, card.shipyardIdx);
        var selected = card.selectedQueueItem;
        int index = queue.IndexOf(selected);
        card.RemoveShipButton.interactable = index >= 0;
        card.ClearQueueButton.interactable = queue.Any(i => !i.costPaid);
        card.MoveUpinQueueButton.interactable = index > 0 && !selected.costPaid && !queue[index - 1].costPaid;
        card.MoveDownInQueueButton.interactable = index >= 0 && index < queue.Count - 1 && !selected.costPaid;
        if (!card.controller.refitScrollviews.activeSelf)
        {
            var yard = BuildYard(card);
            card.AddShipButton.interactable = yard != null;
            if (yard != null)
                card.AddToQueueButtonText.SetText(Loc.T("UI.Fleets.AddToConstructionQueueButtonDays",
                    card.controller.constructionManagerSelectedDesign.hullTemplate.constructionTime_Days(yard).ToString("N0")));
        }
    }

    internal static void Apply(FleetsScreenController screen)
    {
        var seen = new HashSet<TIHabState>();
        foreach (object entry in screen.shipyardGridList)
        {
            var card = entry as ShipyardGridItemController;
            if (card == null || !Enabled(card) || !card.gameObject.activeSelf) continue;
            if (!seen.Add(card.shipyardIdx.ref_hab)) card.gameObject.SetActive(false);
        }
        var grid = screen.shipyardGridList.GetComponent<GridLayoutGroup>();
        if (grid != null)
        {
            var size = grid.GetComponent<StationConstructionGrid>();
            if (size == null) size = grid.gameObject.AddComponent<StationConstructionGrid>();
            size.Apply(grid);
        }
    }
}

// Station cards form one horizontal row, using the viewport's full available height.
// Keep stock geometry and scroll settings so disabling pooled queues restores vanilla.
internal sealed class StationConstructionGrid : MonoBehaviour
{
    private GridLayoutGroup grid;
    private RectTransform content;
    private ScrollRect scroll;
    private RectTransform originalScrollContent;
    private ContentSizeFitter fitter;
    private Vector2 originalCellSize, originalAnchorMin, originalAnchorMax, originalPivot, originalSize, originalPosition;
    private GridLayoutGroup.Constraint originalConstraint;
    private GridLayoutGroup.Axis originalAxis;
    private GridLayoutGroup.Corner originalCorner;
    private TextAnchor originalAlignment;
    private int originalCount;
    private bool originalHorizontal, originalVertical, originalFitterEnabled, horizontalBarActive, verticalBarActive, applied;
    private Scrollbar originalHorizontalBar, originalVerticalBar, horizontalBar;
    private ScrollRect.ScrollbarVisibility originalHorizontalVisibility;
    private float barHeight;

    internal static float CardHeight(float viewportHeight, float padding, float scrollbarHeight) =>
        Math.Max(1f, viewportHeight - padding - scrollbarHeight);

    internal static float ContentWidth(int count, float cardWidth, float spacing, float padding, float viewportWidth) =>
        Math.Max(viewportWidth, Math.Max(0, count) * cardWidth + Math.Max(0, count - 1) * spacing + padding);

    internal void Apply(GridLayoutGroup target)
    {
        if (grid == null)
        {
            grid = target;
            content = (RectTransform)grid.transform;
            scroll = grid.GetComponentInParent<ScrollRect>();
            fitter = grid.GetComponent<ContentSizeFitter>();
            originalCellSize = grid.cellSize;
            originalConstraint = grid.constraint; originalCount = grid.constraintCount;
            originalAxis = grid.startAxis; originalCorner = grid.startCorner; originalAlignment = grid.childAlignment;
            originalAnchorMin = content.anchorMin; originalAnchorMax = content.anchorMax;
            originalPivot = content.pivot; originalSize = content.sizeDelta; originalPosition = content.anchoredPosition;
            if (fitter != null) originalFitterEnabled = fitter.enabled;
            if (scroll != null)
            {
                originalHorizontal = scroll.horizontal; originalVertical = scroll.vertical;
                originalScrollContent = scroll.content;
                originalHorizontalBar = scroll.horizontalScrollbar; originalVerticalBar = scroll.verticalScrollbar;
                originalHorizontalVisibility = scroll.horizontalScrollbarVisibility;
                horizontalBarActive = originalHorizontalBar != null && originalHorizontalBar.gameObject.activeSelf;
                verticalBarActive = originalVerticalBar != null && originalVerticalBar.gameObject.activeSelf;
            }
        }
        Resize();
    }

    private void CreateScrollbar()
    {
        if (originalHorizontalBar != null) { horizontalBar = originalHorizontalBar; return; }
        if (originalVerticalBar != null)
            horizontalBar = UnityEngine.Object.Instantiate(originalVerticalBar, scroll.transform);
        else
        {
            var go = new GameObject("Scrollbar", typeof(RectTransform), typeof(Image), typeof(Scrollbar));
            go.transform.SetParent(scroll.transform, false);
            go.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.15f);
            horizontalBar = go.GetComponent<Scrollbar>();
            var handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            handle.transform.SetParent(go.transform, false);
            horizontalBar.handleRect = (RectTransform)handle.transform;
            horizontalBar.targetGraphic = handle.GetComponent<Image>();
        }
        horizontalBar.name = "CataTweaksHorizontalShipyards";
        horizontalBar.onValueChanged = new Scrollbar.ScrollEvent();
        horizontalBar.direction = Scrollbar.Direction.LeftToRight;
        var rect = (RectTransform)horizontalBar.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.offsetMin = new Vector2(0f, 0f);
        rect.offsetMax = new Vector2(0f, 16f);
    }

    private void Resize()
    {
        if (!Main.settings.pooledStationConstruction || scroll == null)
        {
            if (applied) Restore();
            return;
        }
        if (!applied)
        {
            applied = true;
            if (horizontalBar == null) CreateScrollbar();
            barHeight = Math.Max(0f, ((RectTransform)horizontalBar.transform).rect.height);
            scroll.StopMovement();
            scroll.horizontal = true;
            scroll.vertical = false;
            scroll.content = content;
            scroll.verticalScrollbar = null;
            if (originalVerticalBar != null) originalVerticalBar.gameObject.SetActive(false);
            scroll.horizontalScrollbar = horizontalBar;
            scroll.horizontalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            horizontalBar.gameObject.SetActive(true);
            if (fitter != null) fitter.enabled = false;
            content.anchorMin = content.anchorMax = new Vector2(0f, 1f);
            content.pivot = new Vector2(0f, 1f);
            content.anchoredPosition = Vector2.zero;
        }
        grid.constraint = GridLayoutGroup.Constraint.FixedRowCount;
        grid.constraintCount = 1;
        grid.startAxis = GridLayoutGroup.Axis.Horizontal;
        grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
        grid.childAlignment = TextAnchor.UpperLeft;
        var viewport = scroll.viewport ?? (RectTransform)scroll.transform;
        if (viewport.rect.height <= 0f) return;
        float height = CardHeight(viewport.rect.height, grid.padding.vertical, barHeight);
        int count = 0;
        foreach (Transform child in grid.transform)
            if (child.gameObject.activeSelf && child.GetComponent<ShipyardGridItemController>() != null) count++;
        float width = ContentWidth(count, originalCellSize.x, grid.spacing.x, grid.padding.horizontal, viewport.rect.width);
        var cell = new Vector2(originalCellSize.x, height);
        var size = new Vector2(width, height + grid.padding.vertical);
        if (grid.cellSize != cell || content.sizeDelta != size)
        {
            grid.cellSize = cell;
            content.sizeDelta = size;
            LayoutRebuilder.MarkLayoutForRebuild(content);
        }
        content.anchoredPosition = new Vector2(Math.Max(Math.Min(0f, viewport.rect.width - width),
            Math.Min(0f, content.anchoredPosition.x)), 0f);
    }

    private void Restore()
    {
        applied = false;
        scroll.StopMovement();
        scroll.horizontal = originalHorizontal; scroll.vertical = originalVertical;
        scroll.content = originalScrollContent;
        scroll.horizontalScrollbar = originalHorizontalBar; scroll.verticalScrollbar = originalVerticalBar;
        scroll.horizontalScrollbarVisibility = originalHorizontalVisibility;
        if (horizontalBar != null && horizontalBar != originalHorizontalBar) horizontalBar.gameObject.SetActive(false);
        if (originalHorizontalBar != null) originalHorizontalBar.gameObject.SetActive(horizontalBarActive);
        if (originalVerticalBar != null) originalVerticalBar.gameObject.SetActive(verticalBarActive);
        grid.cellSize = originalCellSize;
        grid.constraint = originalConstraint; grid.constraintCount = originalCount;
        grid.startAxis = originalAxis; grid.startCorner = originalCorner; grid.childAlignment = originalAlignment;
        content.anchorMin = originalAnchorMin; content.anchorMax = originalAnchorMax;
        content.pivot = originalPivot; content.sizeDelta = originalSize; content.anchoredPosition = originalPosition;
        if (fitter != null) fitter.enabled = originalFitterEnabled;
        LayoutRebuilder.MarkLayoutForRebuild(content);
    }
    private void LateUpdate() { if (grid != null) Resize(); }
}

// A thin, non-interactive progress strip inside a Currently Building slot row.
internal sealed class StationConstructionProgress : MonoBehaviour
{
    private RectTransform track;
    private RectTransform fill;
    private Image fillImage;

    internal static float Fraction(ShipConstructionQueueItem item)
    {
        if (item == null || !item.costPaid || item.durationInDays <= 0f) return 0f;
        return Math.Max(0f, Math.Min(1f, 1f - item.daysToCompletion / item.durationInDays));
    }

    internal void Show(ShipConstructionQueueItem item, bool active)
    {
        if (track == null)
        {
            var go = new GameObject("CataTweaksSlotProgress", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            track = (RectTransform)go.transform;
            track.SetParent(transform, false);
            track.anchorMin = new Vector2(0f, 0f);
            track.anchorMax = new Vector2(1f, 0f);
            track.pivot = new Vector2(0.5f, 0f);
            track.offsetMin = new Vector2(5f, 1f);
            track.offsetMax = new Vector2(-5f, 4f);
            go.GetComponent<LayoutElement>().ignoreLayout = true;
            var background = go.GetComponent<Image>();
            background.color = new Color(1f, 1f, 1f, 0.18f);
            background.raycastTarget = false;
            var fillObject = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fill = (RectTransform)fillObject.transform;
            fill.SetParent(track, false);
            fill.anchorMin = Vector2.zero;
            fill.offsetMin = fill.offsetMax = Vector2.zero;
            fillImage = fillObject.GetComponent<Image>();
            fillImage.raycastTarget = false;
        }
        track.gameObject.SetActive(true);
        track.SetAsLastSibling();
        fill.anchorMax = new Vector2(Fraction(item), 1f);
        fill.offsetMin = fill.offsetMax = Vector2.zero;
        fillImage.color = active ? new Color(0.25f, 0.75f, 1f, 0.9f) : new Color(1f, 0.6f, 0.2f, 0.9f);
    }

    internal void Hide() { if (track != null) track.gameObject.SetActive(false); }
}

[HarmonyPatch(typeof(ShipyardGridItemController), nameof(ShipyardGridItemController.UpdateConstructionQueue))]
internal static class StationConstructionSlotRows
{
    private static readonly MethodInfo ScrollRoutine = AccessTools.Method(typeof(ShipyardGridItemController), "SetQueueScrollRect");

    private static bool Prefix(ShipyardGridItemController __instance, ref Sprite ___defaultButtonSprite)
    {
        var card = __instance;
        var slots = card.GetComponent<StationConstructionSlots>();
        if (!StationConstruction.Enabled(card))
        {
            slots?.Hide();
            return true;
        }
        if (slots == null) slots = card.gameObject.AddComponent<StationConstructionSlots>();
        slots.Render(card);
        var faction = card.controller.activePlayer;
        var waiting = StationConstruction.View(faction, card.shipyardIdx).Where(i => !i.costPaid).ToList();
        int count = waiting.Count;
        card.queueList.SetListSize<ShipConstructionQueueListItemController>(count);
        if (___defaultButtonSprite == null && count > 0)
            ___defaultButtonSprite = card.queueList.transform.GetChild(0).GetComponent<ShipConstructionQueueListItemController>().button.image.sprite;
        int index = 0;
        foreach (object entry in card.queueList)
        {
            var row = (ShipConstructionQueueListItemController)entry;
            row.Init(card, ___defaultButtonSprite);
            row.button.interactable = true;
            row.UpdateListItem(waiting[index], index);
            index++;
        }
        if (card.gameObject.activeSelf)
            card.StartCoroutine((System.Collections.IEnumerator)ScrollRoutine.Invoke(card, new object[] { count > 4 }));
        return false;
    }
}

// Currently Building grows with the number of slots within the taller card. The waiting
// queue uses the remaining space, with room reserved for two queue rows. Stations with
// more slots than fit still scroll independently of their waiting queue.
internal sealed class StationConstructionSlots : MonoBehaviour
{
    private const float BuildingWellBottomPadding = 9f;
    private sealed class RectState
    {
        private readonly RectTransform target;
        private readonly Vector2 anchorMin, anchorMax, pivot, size, position;
        internal RectState(RectTransform rect)
        {
            target = rect;
            anchorMin = rect.anchorMin; anchorMax = rect.anchorMax; pivot = rect.pivot;
            size = rect.sizeDelta; position = rect.anchoredPosition;
        }
        internal void Restore()
        {
            target.anchorMin = anchorMin; target.anchorMax = anchorMax; target.pivot = pivot;
            target.sizeDelta = size; target.anchoredPosition = position;
        }
    }
    private sealed class Slot
    {
        internal RectTransform Rect;
        internal TMP_Text Label;
        internal TMP_Text TimeCounter;
        internal TMP_Text ClassName;
        internal Image Background;
        internal Button Button;
        internal StationConstructionProgress Progress;
        internal ShipConstructionQueueItem Item;
    }
    private readonly List<Slot> slots = new List<Slot>();
    private ShipyardGridItemController card;
    private RectTransform viewport;
    private RectTransform content;
    private ScrollRect scroll;
    private bool originalTextEnabled;
    private float rowHeight;
    private RectTransform cardRect, queueRect, queueTitleRect;
    private RectState queueState, queueTitleState, currentConstructionState;
    private float topInset, bottomInset, sectionGap, titleGap, titleHeight;
    private float slotLeft, slotWidth, queueLeft, queueWidth, titleLeft, titleWidth;
    private RectTransform buildingFrame, buildingTitleRect, backgroundUpperRight;
    private RectState buildingFrameState, buildingTitleState, queueWellState;
    private RectTransform buildingWell, queueWell;
    private float wellLeft, wellWidth, queueWellWidth, wellBottomInset;
    private bool originalRightBackgroundActive;
    private ShipConstructionQueueListItemController slotRowTemplate;
    // Keep the neutral sprite when ListManager clones a live card with selected rows.
    [SerializeField] private Sprite slotDefaultSprite;
    private float buildingFrameLeft, buildingFrameWidth, buildingHeader, buildingFooter;
    private float buildingTitleLeft, buildingTitleWidth, buildingTitleHeight, buildingTitleInset;
    private bool originalQueueBarActive;

    internal static float BuildingHeight(int slotCount, float rowHeight, float available)
    {
        if (available <= 0f || rowHeight <= 0f) return 0f;
        float queueReserve = Math.Min(2f * rowHeight, available / 2f);
        return Math.Min(Math.Max(1, slotCount) * rowHeight, available - queueReserve);
    }

    private Bounds BoundsInCard(RectTransform rect)
    {
        var corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        var bounds = new Bounds(cardRect.InverseTransformPoint(corners[0]), Vector3.zero);
        for (int i = 1; i < corners.Length; i++) bounds.Encapsulate(cardRect.InverseTransformPoint(corners[i]));
        return bounds;
    }

    private void Create(ShipyardGridItemController owner)
    {
        card = owner;
        originalTextEnabled = card.currentConstruction.enabled;
        rowHeight = Mathf.Max(40f, card.currentConstruction.fontSize * 2.4f + 12f);
        cardRect = (RectTransform)card.transform;
        // Runtime children can come along when the game clones a live card; its new
        // component has no slot records for that copied panel, so replace it cleanly.
        var copiedPanel = cardRect.Find("CataTweaksCurrentlyBuildingSlots");
        if (copiedPanel != null)
        {
            copiedPanel.gameObject.SetActive(false);
            UnityEngine.Object.Destroy(copiedPanel.gameObject);
        }
        queueRect = (RectTransform)card.shipQueueScrollRect.transform;
        queueTitleRect = card.constructionQueueTitle.rectTransform;
        queueState = new RectState(queueRect);
        queueTitleState = new RectState(queueTitleRect);
        currentConstructionState = new RectState(card.currentConstruction.rectTransform);
        var currentBounds = BoundsInCard(card.currentConstruction.rectTransform);
        var queueBounds = BoundsInCard(queueRect);
        var titleBounds = BoundsInCard(queueTitleRect);
        topInset = cardRect.rect.yMax - currentBounds.max.y;
        bottomInset = queueBounds.min.y - cardRect.rect.yMin;
        sectionGap = Mathf.Max(4f, currentBounds.min.y - titleBounds.max.y);
        titleGap = Mathf.Max(2f, titleBounds.min.y - queueBounds.max.y);
        titleHeight = titleBounds.size.y;
        slotLeft = currentBounds.min.x; slotWidth = currentBounds.size.x;
        queueLeft = queueBounds.min.x; queueWidth = queueBounds.size.x;
        titleLeft = titleBounds.min.x; titleWidth = titleBounds.size.x;
        buildingTitleRect = card.currentConstructionTitle.rectTransform;
        // These are the installed game's prefab paths, not localized display strings.
        // Its full-height detail background has a separate, fixed-height right cap;
        // it does not have independent enclosing Images for the two sections.
        buildingFrame = buildingTitleRect.parent as RectTransform;
        backgroundUpperRight = queueRect.parent.Find("Background/Background Upper Right") as RectTransform;
        slotRowTemplate = card.queueList.GetComponentsInChildren<ShipConstructionQueueListItemController>(true).First();
        if (slotDefaultSprite == null) slotDefaultSprite = slotRowTemplate.idleButtonImage.sprite;
        var nativeDefaultSprite = AccessTools.Field(typeof(ShipyardGridItemController), "defaultButtonSprite").GetValue(card) as Sprite;
        if (nativeDefaultSprite != null) slotDefaultSprite = nativeDefaultSprite;
        if (buildingFrame != null && buildingFrame != cardRect)
        {
            buildingFrameState = new RectState(buildingFrame);
            buildingTitleState = new RectState(buildingTitleRect);
            var buildingBounds = BoundsInCard(buildingFrame);
            var buildingTitleBounds = BoundsInCard(buildingTitleRect);
            topInset = cardRect.rect.yMax - buildingBounds.max.y;
            buildingFrameLeft = buildingBounds.min.x; buildingFrameWidth = buildingBounds.size.x;
            buildingHeader = Math.Max(0f, buildingBounds.max.y - currentBounds.max.y);
            // The inner value box ends at the last slot. Extra space belongs to
            // the enclosing well, not this box or the individual queue-row cards.
            buildingFooter = Math.Max(1f, currentBounds.min.y - buildingBounds.min.y);
            buildingTitleLeft = buildingTitleBounds.min.x; buildingTitleWidth = buildingTitleBounds.size.x;
            buildingTitleHeight = buildingTitleBounds.size.y;
            buildingTitleInset = buildingBounds.max.y - buildingTitleBounds.max.y;
            sectionGap = Math.Max(BuildingWellBottomPadding + 8f, buildingBounds.min.y - titleBounds.max.y);
            var sourceWell = queueRect.parent.Find("Background")?.GetComponent<Image>();
            if (sourceWell != null)
            {
                queueWell = sourceWell.rectTransform;
                queueWellState = new RectState(queueWell);
                var wellBounds = BoundsInCard(queueWell);
                wellLeft = wellBounds.min.x;
                queueWellWidth = wellBounds.size.x;
                wellWidth = BoundsInCard((RectTransform)queueRect.parent).size.x;
                wellBottomInset = wellBounds.min.y - cardRect.rect.yMin;
                var copiedWell = queueRect.parent.Find("CataTweaksCurrentlyBuildingWell");
                if (copiedWell != null)
                {
                    copiedWell.gameObject.SetActive(false);
                    UnityEngine.Object.Destroy(copiedWell.gameObject);
                }
                var well = new GameObject("CataTweaksCurrentlyBuildingWell", typeof(RectTransform), typeof(Image));
                buildingWell = (RectTransform)well.transform;
                buildingWell.SetParent(queueRect.parent, false);
                buildingWell.SetSiblingIndex(queueWell.GetSiblingIndex() + 1);
                var image = well.GetComponent<Image>();
                image.sprite = sourceWell.sprite;
                image.type = sourceWell.type;
                image.color = sourceWell.color;
                image.material = sourceWell.material;
                image.pixelsPerUnitMultiplier = sourceWell.pixelsPerUnitMultiplier;
                image.fillCenter = sourceWell.fillCenter;
                image.raycastTarget = false;
                if (backgroundUpperRight != null) originalRightBackgroundActive = backgroundUpperRight.gameObject.activeSelf;
            }
        }
        else { buildingFrame = null; }
        originalQueueBarActive = card.shipQueueScrollRect.verticalScrollbar != null
            && card.shipQueueScrollRect.verticalScrollbar.gameObject.activeSelf;
        var panel = new GameObject("CataTweaksCurrentlyBuildingSlots", typeof(RectTransform),
            typeof(Image), typeof(RectMask2D), typeof(ScrollRect), typeof(LayoutElement));
        viewport = (RectTransform)panel.transform;
        viewport.SetParent(cardRect, false);
        panel.GetComponent<LayoutElement>().ignoreLayout = true;
        panel.GetComponent<Image>().color = Color.clear;
        var list = new GameObject("Slots", typeof(RectTransform));
        content = (RectTransform)list.transform;
        content.SetParent(viewport, false);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        scroll = panel.GetComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = card.shipQueueScrollRect.scrollSensitivity;
    }

    private Slot AddSlot()
    {
        // Reuse the native queue row's Image and Button, including sliced sprites,
        // hover/pressed transitions and navigation. Replace only its content/action.
        var go = UnityEngine.Object.Instantiate(slotRowTemplate.gameObject, content, false);
        go.name = "Slot";
        var copiedController = go.GetComponent<ShipConstructionQueueListItemController>();
        var copiedBackground = copiedController.idleButtonImage;
        foreach (Transform child in go.transform)
        {
            child.gameObject.SetActive(false);
            UnityEngine.Object.Destroy(child.gameObject);
        }
        UnityEngine.Object.Destroy(copiedController);
        var slot = new Slot { Rect = (RectTransform)go.transform };
        slot.Rect.SetParent(content, false);
        slot.Rect.anchorMin = new Vector2(0f, 1f);
        slot.Rect.anchorMax = new Vector2(1f, 1f);
        slot.Rect.pivot = new Vector2(0.5f, 1f);
        slot.Rect.sizeDelta = new Vector2(0f, rowHeight);
        slot.Background = copiedBackground;
        slot.Background.sprite = slotDefaultSprite;
        slot.Background.overrideSprite = null;
        slot.Button = go.GetComponent<Button>();
        slot.Button.onClick = new Button.ButtonClickedEvent();
        slot.Button.onClick.AddListener(() =>
        {
            if (slot.Item == null) return;
            AudioManager.PlayOneShot("event:/SFX/UI_SFX/trig_SFX_ListLineSelect");
            card.SetSelectedQueueItem(slot.Item);
        });
        slot.Label = SlotText(slot.Rect, TextAlignmentOptions.MidlineLeft);
        var labelRect = slot.Label.rectTransform;
        labelRect.anchorMin = new Vector2(0f, 0.5f);
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(5f, 0f);
        labelRect.offsetMax = new Vector2(-5f, -1f);
        slot.Label.fontSize = card.currentConstruction.fontSize * 0.9f;
        slot.TimeCounter = SlotText(slot.Rect, TextAlignmentOptions.MidlineLeft);
        slot.ClassName = SlotText(slot.Rect, TextAlignmentOptions.MidlineRight);
        slot.Progress = go.AddComponent<StationConstructionProgress>();
        go.SetActive(true);
        slots.Add(slot);
        return slot;
    }

    private TMP_Text SlotText(RectTransform parent, TextAlignmentOptions alignment)
    {
        var text = UnityEngine.Object.Instantiate(card.currentConstruction, parent);
        // Copy only the text appearance, without the stock value-box shading.
        foreach (var image in text.GetComponentsInChildren<Image>(true)) image.enabled = false;
        text.enabled = true;
        text.raycastTarget = false;
        text.alignment = alignment;
        text.enableAutoSizing = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Ellipsis;
        return text;
    }

    private void AlignSlotText(Slot slot)
    {
        float width = Math.Max(0f, slot.Rect.rect.width - 10f);
        float counterWidth = Math.Min(width * 0.4f, slot.TimeCounter.GetPreferredValues(slot.TimeCounter.text).x + 8f);
        var counter = slot.TimeCounter.rectTransform;
        counter.anchorMin = Vector2.zero;
        counter.anchorMax = new Vector2(0f, 0.5f);
        counter.offsetMin = new Vector2(5f, 5f);
        counter.offsetMax = new Vector2(5f + counterWidth, 0f);
        var shipClass = slot.ClassName.rectTransform;
        shipClass.anchorMin = Vector2.zero;
        shipClass.anchorMax = new Vector2(1f, 0.5f);
        shipClass.offsetMin = new Vector2(counterWidth + 13f, 5f);
        shipClass.offsetMax = new Vector2(-5f, 0f);
    }

    internal void Render(ShipyardGridItemController owner)
    {
        if (viewport == null) Create(owner);
        card.currentConstruction.enabled = false;
        viewport.gameObject.SetActive(true);
        if (buildingWell != null)
        {
            buildingWell.gameObject.SetActive(true);
            if (backgroundUpperRight != null) backgroundUpperRight.gameObject.SetActive(false);
        }
        var faction = card.controller.activePlayer;
        var yards = StationConstruction.Yards(faction, card.shipyardIdx.ref_hab);
        while (slots.Count < yards.Count) AddSlot();
        while (slots.Count > yards.Count)
        {
            UnityEngine.Object.Destroy(slots[slots.Count - 1].Rect.gameObject);
            slots.RemoveAt(slots.Count - 1);
        }
        for (int index = 0; index < yards.Count; index++)
        {
            var yard = yards[index];
            var slot = slots[index];
            slot.Item = faction.GetShipyardQueue(yard).FirstOrDefault(i => i.costPaid);
            slot.Rect.anchoredPosition = new Vector2(0f, -index * rowHeight);
            slot.Button.interactable = slot.Item != null;
            slot.Background.sprite = slot.Item != null && slot.Item == card.selectedQueueItem
                ? slot.Button.spriteState.pressedSprite : slotDefaultSprite;
            string yardName = StationConstruction.YardName(yard);
            string text = yardName;
            if (!yard.active && slot.Item != null)
                text = TIUtilities.RedLine(text + " " + Loc.T("UI.Fleets.Inactive"));
            slot.Label.SetText(text);
            slot.TimeCounter.SetText(slot.Item != null
                ? Loc.T("UI.Fleets.ConstructionTime", slot.Item.daysToCompletion.ToString("N0"))
                : Loc.T(yard.active ? "UI.Fleets.ShipyardIdle" : "UI.Fleets.Inactive"));
            slot.ClassName.SetText(slot.Item?.shipDesign.fullClassName ?? string.Empty);
            AlignSlotText(slot);
            slot.Progress.Show(slot.Item, yard.active);
        }
        content.sizeDelta = new Vector2(0f, slots.Count * rowHeight);
        Align();
    }

    private void Align()
    {
        float top = cardRect.rect.yMax - topInset;
        float bottom = cardRect.rect.yMin + bottomInset;
        bool framed = buildingFrame != null;
        float overhead = sectionGap + titleHeight + titleGap + (framed ? buildingHeader + buildingFooter : 0f);
        float available = Math.Max(0f, top - bottom - overhead);
        float buildingHeight = BuildingHeight(slots.Count, rowHeight, available);
        if (framed)
        {
            float buildingFrameHeight = buildingHeader + buildingHeight + buildingFooter;
            Place(buildingFrame, buildingFrameLeft, top, buildingFrameWidth, buildingFrameHeight);
            Place(buildingTitleRect, buildingTitleLeft, top - buildingTitleInset, buildingTitleWidth, buildingTitleHeight);
            Place(viewport, slotLeft, top - buildingHeader, slotWidth, buildingHeight);
            float titleTop = top - buildingFrameHeight - sectionGap;
            if (buildingWell != null)
            {
                // Two native bordered wells, with a small exposed gap between them.
                Place(buildingWell, wellLeft, top, wellWidth, buildingFrameHeight + BuildingWellBottomPadding);
                float queueWellTop = titleTop + 4f;
                Place(queueWell, wellLeft, queueWellTop, queueWellWidth,
                    Math.Max(0f, queueWellTop - cardRect.rect.yMin - wellBottomInset));
            }
            // The title is a child of the scroll rect, so place its parent first.
            float queueTop = titleTop - titleHeight - titleGap;
            Place(queueRect, queueLeft, queueTop, queueWidth, Math.Max(0f, queueTop - bottom));
            Place(queueTitleRect, titleLeft, titleTop, titleWidth, titleHeight);
        }
        else
        {
            Place(viewport, slotLeft, top, slotWidth, buildingHeight);
            float titleTop = top - buildingHeight - sectionGap;
            float queueTop = titleTop - titleHeight - titleGap;
            Place(queueRect, queueLeft, queueTop, queueWidth, Math.Max(0f, queueTop - bottom));
            Place(queueTitleRect, titleLeft, titleTop, titleWidth, titleHeight);
        }
        // Keep the hidden stock text aligned with the slots. Its parent's native
        // value-box Image stretches with the resized Currently Building container.
        Place(card.currentConstruction.rectTransform, slotLeft,
            framed ? top - buildingHeader : top, slotWidth, buildingHeight);
        for (int index = 0; index < slots.Count; index++)
        {
            slots[index].Rect.sizeDelta = new Vector2(0f, rowHeight);
            slots[index].Rect.anchoredPosition = new Vector2(0f, -index * rowHeight);
            AlignSlotText(slots[index]);
        }
        content.sizeDelta = new Vector2(0f, slots.Count * rowHeight);
        scroll.enabled = content.rect.height > buildingHeight;
        // The queue now has a variable number of visible rows; the stock four-row cutoff
        // no longer describes its viewport. Align runs after that stock coroutine too.
        var waitingViewport = card.shipQueueScrollRect.viewport ?? queueRect;
        // A stretched content RectTransform can be as tall as the viewport even when
        // only one row exists. Measure the rows' preferred height instead of that rect.
        float waitingHeight = LayoutUtility.GetPreferredHeight((RectTransform)card.queueList.transform);
        if (waitingHeight <= 0f && card.queueList.size > 0)
        {
            int measured = 0;
            foreach (Transform child in card.queueList.transform)
            {
                if (!child.gameObject.activeSelf || child.GetComponent<ShipConstructionQueueListItemController>() == null) continue;
                waitingHeight += ((RectTransform)child).rect.height;
                if (++measured == card.queueList.size) break;
            }
            var layout = card.queueList.GetComponent<VerticalLayoutGroup>();
            if (layout != null) waitingHeight += layout.padding.vertical + Math.Max(0, measured - 1) * layout.spacing;
        }
        bool overflowing = card.queueList.size > 0 && waitingHeight > waitingViewport.rect.height + 1f;
        card.shipQueueScrollRect.enabled = overflowing;
        if (card.shipQueueScrollRect.verticalScrollbar != null)
            card.shipQueueScrollRect.verticalScrollbar.gameObject.SetActive(overflowing);
    }
    private void Place(RectTransform rect, float left, float top, float width, float height)
    {
        rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
        rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
        rect.position = cardRect.TransformPoint(new Vector3(left + width * rect.pivot.x,
            top - height * (1f - rect.pivot.y), 0f));
    }
    private void LateUpdate() { if (viewport != null && viewport.gameObject.activeSelf) Align(); }
    internal void Hide()
    {
        if (viewport == null) return;
        viewport.gameObject.SetActive(false);
        card.currentConstruction.enabled = originalTextEnabled;
        buildingFrameState?.Restore();
        if (buildingWell != null)
        {
            buildingWell.gameObject.SetActive(false);
            queueWellState.Restore();
            if (backgroundUpperRight != null) backgroundUpperRight.gameObject.SetActive(originalRightBackgroundActive);
        }
        buildingTitleState?.Restore();
        queueState.Restore();
        queueTitleState.Restore();
        currentConstructionState.Restore();
        if (card.shipQueueScrollRect.verticalScrollbar != null)
            card.shipQueueScrollRect.verticalScrollbar.gameObject.SetActive(originalQueueBarActive);
    }
}

// Rally settings belong to the hab; an in-flight merge intent belongs to the new
// fleet. Weak keys keep campaigns separate even when their numeric IDs are reused.
internal static class StationConstructionRally
{
    internal static bool Enabled => Main.settings.pooledStationConstruction && Main.settings.shipyardRally;
    internal const string SaveKey = "CataTweaks_RallyFleetID";
    private sealed class Record { internal int fleetID; }
    private static readonly ConditionalWeakTable<TIGameState, Record> records = new ConditionalWeakTable<TIGameState, Record>();
    [ThreadStatic] internal static TIHabModuleState completingYard;
    [ThreadStatic] internal static Report notification;
    [ThreadStatic] internal static TISpaceFleetState adoptingTargetTrajectory;
    internal sealed class Report
    {
        internal bool started;
        internal string summary, detail, failure;
        internal TISpaceShipState ship;
    }

    internal static int TargetID(TIGameState state) => state != null && records.TryGetValue(state, out var record) ? record.fleetID : 0;
    internal static void SetTarget(TIGameState state, int fleetID)
    {
        if (state == null) return;
        if (fleetID <= 0) records.Remove(state);
        else records.GetValue(state, _ => new Record()).fleetID = fleetID;
    }
    internal static void WriteSave(object instance, fsData data)
    {
        if (!(instance is TIHabState) && !(instance is TISpaceFleetState)) return;
        int id = TargetID((TIGameState)instance);
        if (id > 0 && data.IsDictionary) data.AsDictionary[SaveKey] = new fsData((long)id);
    }
    internal static void ReadSave(object instance, fsData data)
    {
        if (!(instance is TIHabState) && !(instance is TISpaceFleetState)) return;
        int id = 0;
        if (data.IsDictionary && data.AsDictionary.TryGetValue(SaveKey, out var value)
            && value.IsInt64 && value.AsInt64 > 0 && value.AsInt64 <= int.MaxValue) id = (int)value.AsInt64;
        SetTarget((TIGameState)instance, id);
    }
    internal static TISpaceFleetState ResolveTarget(TIGameState state)
    {
        int id = TargetID(state);
        return id > 0 && new GameStateID(id).TryGetState<TISpaceFleetState>(out var fleet) && !fleet.deleted ? fleet : null;
    }
    internal static Trajectory LeastTime(IEnumerable<Trajectory> trajectories, double availableDV_mps)
    {
        if (trajectories == null || double.IsNaN(availableDV_mps) || availableDV_mps < 0) return null;
        return trajectories.Where(t => t != null && t.arrivalTime != null && t.launchTime != null
                && !t.destroyOnArrival && !double.IsNaN(t.DV_mps) && !double.IsInfinity(t.DV_mps)
                && t.DV_mps >= 0 && t.DV_mps <= availableDV_mps)
            .OrderBy(t => t.finalArrivalTime.ToJulianDateInSeconds()).ThenBy(t => t.DV_mps).FirstOrDefault();
    }
    internal static bool FuelFailure(TransferResult result, double minimumDV_kps, double availableDV_mps) =>
        result?.Result == TransferResult.Outcome.Fail_InsufficientDV
        || (!double.IsNaN(minimumDV_kps) && !double.IsInfinity(minimumDV_kps)
            && minimumDV_kps >= 0 && minimumDV_kps * 1000 > availableDV_mps);

    internal static Report Start(TISpaceShipState ship, TIHabModuleState yard)
    {
        var report = new Report { ship = ship };
        var source = ship.fleet;
        var hab = yard.ref_hab;
        var target = ResolveTarget(hab);
        string targetName = target?.GetDisplayName(source.faction) ?? Loc.T("UI.CataTweaks.RallyMissingFleet");
        string originName = hab.displayName;
        report.failure = Loc.T("UI.CataTweaks.RallyUnavailable", targetName, originName);
        if (target == null || target == source || target.faction != source.faction || target.ships.Count == 0
            || target.landed || source.ships.Count != 1 || source.inCombatOrWaitingForCombat || source.transferAssigned) return report;
        // The native completion routine starts a battle after its notification if
        // hostile fleets are docked here. Do not issue a departure before that check.
        if (hab.dockedFleets.Any(f => !f.faction.permanentAlly(source.faction))) return report;
        if (source.CanMerge(target) && target.CanMerge(source))
        {
            string location = target.GetLocationDescription(source.faction, capitalize: false, expand: true);
            // Let construction finish before removing its newly created fleet.
            SetTarget(source, (int)target.ID);
            report.started = true;
            report.summary = report.detail = Loc.T("UI.CataTweaks.RallyJoined", ship.displayName, targetName, location);
            return report;
        }
        var operation = new TransferOperation();
        if (source.currentDeltaV_mps <= 0)
        {
            report.failure = Loc.T("UI.CataTweaks.RallyNoDV", targetName, ship.displayName, originName);
            return report;
        }
        if (!operation.ActorCanPerformOperation(source, target) || !operation.ValidTransferDestinationForFleet(source, target)) return report;
        Trajectory[] choices = null;
        var result = MasterTransferPlanner.RequestTrajectories(source, target, 64, trajectories => choices = trajectories,
            out double minimumDV_kps, usePlaceholderTrajectories: false, stopOnFirstSuccess: false);
        var trajectory = LeastTime(choices, source.currentDeltaV_mps);
        if (trajectory == null)
        {
            if (FuelFailure(result, minimumDV_kps, source.currentDeltaV_mps))
                report.failure = Loc.T("UI.CataTweaks.RallyNoDV", targetName, ship.displayName, originName);
            return report;
        }
        string destination = target.GetLocationDescription(source.faction, capitalize: false, expand: true);
        if (!operation.OnOperationConfirm(source, target, null, trajectory))
        {
            if (source.transferAssigned) source.AbortTransfer(-1);
            return report;
        }
        SetTarget(source, (int)target.ID);
        report.started = true;
        report.summary = Loc.T("UI.CataTweaks.RallySummary", ship.displayName, targetName);
        report.detail = Loc.T("UI.CataTweaks.RallyDetail", ship.displayName, ship.template.fullClassName, originName,
            targetName, destination, trajectory.launchTime.ToCustomDateString(), trajectory.finalArrivalTime.ToCustomDateString(),
            trajectory.DV_kps.ToString("N2"));
        return report;
    }

    internal static void TryMerge(TISpaceFleetState source)
    {
        if (source == null || source.deleted || TargetID(source) <= 0) return;
        var target = ResolveTarget(source);
        if (target == null || target.faction != source.faction)
        {
            SetTarget(source, 0);
            return;
        }
        if (!source.inCombatOrWaitingForCombat && !target.inCombatOrWaitingForCombat
            && source.CanMerge(target) && target.CanMerge(source))
        {
            SetTarget(source, 0);
            new MergeFleetOperation().ExecuteOperation(target, source);
        }
    }
}

// TIGameStateConverter calls these base methods only for full records, not ID
// references. Unknown extension fields are ignored by an unmodded game.
[HarmonyPatch(typeof(fsReflectedConverter), nameof(fsReflectedConverter.TrySerialize))]
internal static class StationConstructionRallySave
{
    private static void Postfix(object instance, fsData serialized, fsResult __result)
    {
        if (!__result.Failed) StationConstructionRally.WriteSave(instance, serialized);
    }
}
[HarmonyPatch(typeof(fsReflectedConverter), nameof(fsReflectedConverter.TryDeserialize))]
internal static class StationConstructionRallyLoad
{
    private static void Postfix(object instance, fsData data, fsResult __result)
    {
        if (!__result.Failed) StationConstructionRally.ReadSave(instance, data);
    }
}
[HarmonyPatch(typeof(TIFactionState), nameof(TIFactionState.CompleteShipConstruction))]
internal static class StationConstructionRallyCompletion
{
    private static void Prefix(TIFactionState __instance, TIHabModuleState shipyardIdx, bool refitCancel, out TIHabModuleState __state)
    {
        __state = StationConstructionRally.completingYard;
        StationConstructionRally.completingYard = !refitCancel && __instance.isActivePlayer ? shipyardIdx : null;
    }
    private static void Finalizer(TIHabModuleState __state) => StationConstructionRally.completingYard = __state;
    private static void Postfix(TIFactionState __instance)
    {
        if (__instance.isActivePlayer)
            foreach (var fleet in __instance.fleets.ToList()) StationConstructionRally.TryMerge(fleet);
    }
}
[HarmonyPatch(typeof(TINotificationQueueState), nameof(TINotificationQueueState.LogShipComplete))]
internal static class StationConstructionRallyNotification
{
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod) =>
        NotificationTemplateName.UseOriginal(instructions, __originalMethod);

    private static void Prefix(TISpaceShipState ship, bool isACancelledRefit, out StationConstructionRally.Report __state)
    {
        __state = StationConstructionRally.notification;
        StationConstructionRally.notification = null;
        var yard = StationConstructionRally.completingYard;
        if (!StationConstructionRally.Enabled || isACancelledRefit || yard == null || StationConstructionRally.TargetID(yard.ref_hab) <= 0) return;
        try { StationConstructionRally.notification = StationConstructionRally.Start(ship, yard); }
        catch (Exception e)
        {
            Main.mod?.Logger.Error("Automatic ship rally failed: " + e);
            StationConstructionRally.notification = new StationConstructionRally.Report
            {
                ship = ship,
                failure = Loc.T("UI.CataTweaks.RallyUnavailable", Loc.T("UI.CataTweaks.RallyMissingFleet"), yard.ref_hab.displayName)
            };
        }
    }
    private static void Finalizer(StationConstructionRally.Report __state) => StationConstructionRally.notification = __state;
}
[HarmonyPatch(typeof(TINotificationQueueState), "AddItem")]
internal static class StationConstructionRallyEvent
{
    private static void Prefix(NotificationQueueItem item)
    {
        var report = StationConstructionRally.notification;
        if (item.templateName != "LogShipComplete" || report == null) return;
        if (!report.started)
        {
            item.itemDetail = report.failure + "\n\n" + item.itemDetail;
            return;
        }
        item.templateName = "CataTweaks_ShipRally";
        item.itemHeadline = Loc.T("UI.CataTweaks.RallyHeadline");
        item.itemSummary = report.summary;
        item.itemDetail = report.detail;
        item.notificationDelegates.Clear();
        item.gotoGameState = report.ship.fleet;
    }
}
[HarmonyPatch(typeof(TISpaceFleetState), nameof(TISpaceFleetState.ArriveFleet))]
internal static class StationConstructionRallyArrival
{
    private static void Postfix(TISpaceFleetState __instance, bool startupFix)
    {
        if (!startupFix) StationConstructionRally.TryMerge(__instance);
    }
}
[HarmonyPatch(typeof(TIFactionState), nameof(TIFactionState.ShipConstructionQueueDailyUpdate))]
internal static class StationConstructionRallyRetry
{
    private static void Postfix(TIFactionState __instance)
    {
        if (__instance.isActivePlayer)
            foreach (var fleet in __instance.fleets.ToList()) StationConstructionRally.TryMerge(fleet);
    }
}
[HarmonyPatch(typeof(TISpaceFleetState), nameof(TISpaceFleetState.AbortTransfer))]
internal static class StationConstructionRallyAbort
{
    private static void Prefix(TISpaceFleetState __instance) => StationConstructionRally.SetTarget(__instance, 0);
}
[HarmonyPatch(typeof(CancelFleetOperation), nameof(CancelFleetOperation.ExecuteOperation))]
internal static class StationConstructionRallyCancel
{
    private static void Prefix(TIGameState actorState) => StationConstructionRally.SetTarget(actorState.ref_fleet, 0);
}
[HarmonyPatch(typeof(TISpaceFleetState), nameof(TISpaceFleetState.AssignTrajectory))]
internal static class StationConstructionRallyReroute
{
    private static void Prefix(TISpaceFleetState __instance, Trajectory __0)
    {
        if (StationConstructionRally.adoptingTargetTrajectory == __instance) return;
        int target = StationConstructionRally.TargetID(__instance);
        if (target > 0 && (__0?.destinationFleet == null || (int)__0.destinationFleet.ID != target))
            StationConstructionRally.SetTarget(__instance, 0);
    }
}

// Rendezvous with a moving fleet adopts its remaining transfer before ArriveFleet
// returns. That native continuation is not a player redirecting the rally.
[HarmonyPatch(typeof(TISpaceFleetState), nameof(TISpaceFleetState.AssumeTargetFleetTrajectory))]
internal static class StationConstructionRallyAdoptTrajectory
{
    private static void Prefix(TISpaceFleetState __instance, out TISpaceFleetState __state)
    {
        __state = StationConstructionRally.adoptingTargetTrajectory;
        StationConstructionRally.adoptingTargetTrajectory = __instance;
    }
    private static void Finalizer(TISpaceFleetState __state) => StationConstructionRally.adoptingTargetTrajectory = __state;
}

internal sealed class StationConstructionRallyPicker : MonoBehaviour
{
    private ShipyardGridItemController card;
    private TMP_Dropdown dropdown;
    private RectTransform buttons, detail;
    private Vector2 buttonPosition, detailMin;
    private readonly List<TISpaceFleetState> choices = new List<TISpaceFleetState>();
    private float nextRefresh;
    private bool applied;
    internal void Apply(ShipyardGridItemController owner)
    {
        if (dropdown == null)
        {
            var source = GeneralControlsController.Singleton?.mapModeDropdown;
            if (source == null) return;
            card = owner;
            var copied = card.transform.Find("CataTweaksRallyDropdown");
            if (copied != null) { copied.gameObject.SetActive(false); UnityEngine.Object.Destroy(copied.gameObject); }
            dropdown = UnityEngine.Object.Instantiate(source, card.transform);
            dropdown.name = "CataTweaksRallyDropdown";
            // The map-mode caption is a sibling of the source dropdown. Cloning
            // only the dropdown leaves that reference pointing at the map UI.
            // Keep a caption inside this control so RefreshShownValue updates it.
            if (dropdown.captionText == null || !dropdown.captionText.transform.IsChildOf(dropdown.transform))
            {
                TMP_Text nativeCaption = source.captionText ?? source.itemText;
                dropdown.captionText = nativeCaption != null
                    ? UnityEngine.Object.Instantiate(nativeCaption, dropdown.transform)
                    : new GameObject("Caption", typeof(RectTransform), typeof(TextMeshProUGUI))
                        .GetComponent<TextMeshProUGUI>();
                dropdown.captionText.transform.SetParent(dropdown.transform, false);
                dropdown.captionText.name = "Caption";
                // Instantiating an active dropdown may refresh its inherited
                // external caption before we can replace the reference.
                source.RefreshShownValue();
            }
            dropdown.captionText.gameObject.SetActive(true);
            dropdown.captionText.raycastTarget = false;
            dropdown.captionText.overflowMode = TextOverflowModes.Ellipsis;
            var captionRect = dropdown.captionText.rectTransform;
            captionRect.anchorMin = Vector2.zero; captionRect.anchorMax = Vector2.one;
            captionRect.offsetMin = new Vector2(6f, 1f); captionRect.offsetMax = new Vector2(-24f, -1f);
            if (dropdown.captionImage != null && !dropdown.captionImage.transform.IsChildOf(dropdown.transform))
                dropdown.captionImage = null;
            foreach (var localizer in dropdown.GetComponentsInChildren<UITextLocalizer>(true))
                UnityEngine.Object.Destroy(localizer);
            dropdown.onValueChanged = new TMP_Dropdown.DropdownEvent();
            dropdown.onValueChanged.AddListener(index =>
            {
                if (index == 0) StationConstructionRally.SetTarget(card.shipyardIdx.ref_hab, 0);
                else if (index > 0 && index < choices.Count && choices[index] != null)
                    StationConstructionRally.SetTarget(card.shipyardIdx.ref_hab, (int)choices[index].ID);
            });
            Loc.SwapFonts(dropdown.gameObject);
            var rect = dropdown.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero; rect.anchorMax = new Vector2(1f, 0f); rect.pivot = new Vector2(0.5f, 0f);
            rect.offsetMin = new Vector2(4f, 4f); rect.offsetMax = new Vector2(-4f, 28f);
            var layout = dropdown.GetComponent<LayoutElement>();
            if (layout != null) layout.ignoreLayout = true;
            buttons = (RectTransform)card.ClearQueueButton.transform.parent;
            detail = (RectTransform)card.shipQueueScrollRect.transform.parent;
            buttonPosition = buttons.anchoredPosition;
            detailMin = detail.offsetMin;
        }
        if (!applied)
        {
            buttons.anchoredPosition = buttonPosition + new Vector2(0f, 28f);
            // Reserve space in the whole detail area, including its wells, rather
            // than shortening only the queue viewport behind the raised buttons.
            detail.offsetMin = detailMin + new Vector2(0f, 28f);
            applied = true;
        }
        dropdown.gameObject.SetActive(true);
        Refresh();
    }
    private void Refresh()
    {
        if (dropdown.IsExpanded || card.shipyardIdx?.ref_faction == null) return;
        var faction = card.shipyardIdx.ref_faction;
        int selectedID = StationConstructionRally.TargetID(card.shipyardIdx.ref_hab);
        choices.Clear(); choices.Add(null);
        choices.AddRange(faction.fleets.Where(f => !f.deleted && !f.landed && f.ships.Count > 0)
            .OrderBy(f => f.GetDisplayName(faction)).ThenBy(f => f.ID));
        var options = choices.Select(f => f == null ? Loc.T("UI.CataTweaks.RallyNone")
            : Loc.T("UI.CataTweaks.RallyFleet", f.GetDisplayName(faction))).ToList();
        int index = choices.FindIndex(f => f != null && (int)f.ID == selectedID);
        if (selectedID > 0 && index < 0)
        {
            // Preserve the saved selection until the player changes it. Completion
            // reports an unavailable target rather than silently losing the setting.
            choices.Add(null);
            options.Add(Loc.T("UI.CataTweaks.RallyFleet", Loc.T("UI.CataTweaks.RallyMissingFleet")));
            index = options.Count - 1;
        }
        dropdown.ClearOptions(); dropdown.AddOptions(options); dropdown.SetValueWithoutNotify(Math.Max(0, index));
        dropdown.RefreshShownValue();
        nextRefresh = Time.unscaledTime + 1f;
    }
    private void LateUpdate()
    {
        if (!StationConstructionRally.Enabled) { Hide(); return; }
        if (applied && dropdown != null && Time.unscaledTime >= nextRefresh) Refresh();
    }
    internal void Hide()
    {
        if (dropdown == null || !applied) return;
        dropdown.gameObject.SetActive(false);
        buttons.anchoredPosition = buttonPosition;
        detail.offsetMin = detailMin;
        applied = false;
    }
}
[HarmonyPatch(typeof(ShipyardGridItemController), nameof(ShipyardGridItemController.UpdateGridItem))]
internal static class StationConstructionRallyCard
{
    private static void Prefix(ShipyardGridItemController __instance)
    {
        if (__instance.controller?.activePlayer?.isActivePlayer != true) return;
        if (!StationConstructionRally.Enabled || __instance.shipyardIdx?.ref_hab?.IsStation != true)
        {
            __instance.GetComponent<StationConstructionRallyPicker>()?.Hide();
            return;
        }
        var picker = __instance.GetComponent<StationConstructionRallyPicker>()
            ?? __instance.gameObject.AddComponent<StationConstructionRallyPicker>();
        picker.Apply(__instance);
    }
}

[HarmonyPatch(typeof(TIHabModuleState), nameof(TIHabModuleState.FullSummary))]
internal static class StationConstructionHabTooltip
{
    internal static ShipConstructionQueueItem CurrentJob(TIFactionState faction, TIHabModuleState yard)
    {
        if (faction?.nShipyardQueues == null || yard == null
            || !faction.nShipyardQueues.TryGetValue(yard, out var queue)) return null;
        // Use this module's physical slot, not the shared station queue. Unpowered
        // modules still retain their paid job even though buildingShip is false.
        return queue.FirstOrDefault(item => item.costPaid);
    }

    internal static string Append(string summary, TIHabModuleState module)
    {
        if (string.IsNullOrEmpty(summary) || module?.moduleTemplate?.allowsShipConstruction != true) return summary;
        var faction = module.ref_faction;
        if (faction == null || !faction.isActivePlayer) return summary;
        var item = CurrentJob(faction, module);
        if (item?.shipDesign == null) return summary;
        string days = item.daysToCompletion.ToString("N0");
        string text = item.isRefit
            ? Loc.T("UI.Fleets.RefitDays", days) + ": "
                + (item.originalSpaceShipState?.displayName ?? item.refit_originalShipDesign?.fullClassName ?? item.shipDesign.fullClassName)
                + " → " + item.shipDesign.fullClassName
            : Loc.T("UI.Fleets.CurrentlyBuildingTitle") + ": "
                + Loc.T("UI.Fleets.OtherQueueItem", item.shipDesign.fullClassName, days);
        if (!module.active) text = TIUtilities.RedLine(text + " (" + Loc.T("UI.Fleets.Inactive") + ")");
        return summary.TrimEnd() + "\n\n" + text + "\n";
    }

    private static void Postfix(TIHabModuleState habModule, ref string __result) => __result = Append(__result, habModule);
}

[HarmonyPatch(typeof(HabModuleListItem), "SetTooltipText")]
internal static class StationConstructionHabListTooltip
{
    private static void Postfix(HabModuleListItem __instance, ref string __result)
    {
        if (!__instance.prospective)
            __result = StationConstructionHabTooltip.Append(__result, __instance.GetModuleState());
    }
}

[HarmonyPatch(typeof(TIFactionState), nameof(TIFactionState.AttemptInitiateShipConstruction))]
internal static class StationConstructionStart
{
    private static bool Prefix(TIFactionState __instance, TIHabModuleState shipyard, ref bool __result)
    {
        if (!StationConstruction.Enabled(__instance, shipyard)) return true;
        if (StationConstruction.dispatching) return true;
        __result = false;
        return false; // The dispatcher alone chooses which unpaid head starts next.
    }
}

[HarmonyPatch(typeof(TIFactionState), nameof(TIFactionState.AddShipToShipyardQueue))]
internal static class StationConstructionAdd
{
    private static void Prefix(TIFactionState __instance, ref TIHabModuleState shipyard)
    {
        if (!StationConstruction.Enabled(__instance, shipyard)) return;
        StationConstruction.Normalize(__instance, shipyard.ref_hab);
        shipyard = StationConstruction.Yards(__instance, shipyard.ref_hab)[0];
    }
    private static void Postfix(TIFactionState __instance, TIHabModuleState shipyard, ref bool __result)
    {
        if (!StationConstruction.Enabled(__instance, shipyard)) return;
        StationConstruction.Dispatch(__instance, shipyard.ref_hab);
        __result = true; // Enqueue succeeds even when resources are unavailable.
    }
}

[HarmonyPatch(typeof(TIFactionState), nameof(TIFactionState.ShipConstructionQueueDailyUpdate))]
internal static class StationConstructionDaily
{
    private static void Prefix() { StationConstruction.dailyUpdateDepth++; }
    private static Exception Finalizer(TIFactionState __instance, Exception __exception)
    {
        StationConstruction.dailyUpdateDepth--;
        if (__exception == null) StationConstruction.DispatchAll(__instance);
        return __exception;
    }
}

[HarmonyPatch(typeof(TIFactionState), nameof(TIFactionState.CompleteShipConstruction))]
internal static class StationConstructionCompleted
{
    private static void Postfix(TIFactionState __instance, TIHabModuleState shipyardIdx, bool refitCancel)
    {
        if (!refitCancel && StationConstruction.Enabled(__instance, shipyardIdx))
            StationConstruction.Dispatch(__instance, shipyardIdx.ref_hab);
    }
}

[HarmonyPatch(typeof(TIFactionState), nameof(TIFactionState.RemoveShipyardFromFaction))]
internal static class StationConstructionLostYard
{
    private static void Prefix(TIFactionState __instance, TIHabModuleState shipyard)
    {
        if (!StationConstruction.Enabled(__instance, shipyard)) return;
        var survivor = StationConstruction.Yards(__instance, shipyard.ref_hab).FirstOrDefault(y => y != shipyard);
        if (survivor == null) return;
        // Losing a slot cancels its paid construction as usual, but not the station backlog.
        foreach (var item in __instance.GetShipyardQueue(shipyard).Where(i => !i.costPaid).ToList())
        {
            __instance.GetShipyardQueue(shipyard).Remove(item);
            item.shipyard = survivor;
            __instance.GetShipyardQueue(survivor).Add(item);
        }
    }
}

[HarmonyPatch]
internal static class StationConstructionQueueView
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (string name in new[] { "UpdateGridItem", "UpdateConstructionQueue", "SetAddClearShipButtons", "get_selectedItemIndex" })
            yield return AccessTools.Method(typeof(ShipyardGridItemController), name);
    }
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var original = AccessTools.Method(typeof(TIFactionState), nameof(TIFactionState.GetShipyardQueue));
        var view = AccessTools.Method(typeof(StationConstruction), nameof(StationConstruction.View));
        foreach (var instruction in instructions)
        {
            if (instruction.Calls(original))
            {
                instruction.opcode = OpCodes.Call;
                instruction.operand = view;
            }
            yield return instruction;
        }
    }
}

[HarmonyPatch(typeof(ShipyardGridItemController), nameof(ShipyardGridItemController.UpdateGridItem))]
internal static class StationConstructionCard
{
    private static void Postfix(ShipyardGridItemController __instance)
    {
        if (!StationConstruction.Enabled(__instance)) return;
        var yards = StationConstruction.Yards(__instance.controller.activePlayer, __instance.shipyardIdx.ref_hab);
        var queue = StationConstruction.View(__instance.controller.activePlayer, __instance.shipyardIdx);
        int busy = queue.Count(i => i.costPaid);
        __instance.shipyardDetails.SetText(Loc.T("UI.CataTweaks.StationShipyards", yards.Count, yards.Count(y => y.active)));
        string summary = Loc.T("UI.CataTweaks.StationSlots", busy, yards.Count);
        if (busy == 0 && queue.Count > 0) summary += "\n" + __instance.currentConstruction.text;
        __instance.currentConstruction.SetText(summary);
        StationConstruction.Buttons(__instance);
    }
}

[HarmonyPatch(typeof(ShipConstructionQueueListItemController), nameof(ShipConstructionQueueListItemController.UpdateListItem))]
internal static class StationConstructionQueueRow
{
    private static void Postfix(ShipConstructionQueueListItemController __instance, ShipConstructionQueueItem item)
    {
        var progress = __instance.GetComponent<StationConstructionProgress>();
        progress?.Hide();
        __instance.button.interactable = true;
        if (!StationConstruction.Enabled(item.shipyard.ref_faction, item.shipyard)) return;
        string text = item.costPaid
            ? Loc.T("UI.CataTweaks.StationBuildingYard",
                StationConstruction.YardName(item.shipyard),
                item.shipDesign.fullClassName, item.daysToCompletion.ToString("N0"))
            : Loc.T("UI.Fleets.OtherQueueItem", item.shipDesign.fullClassName, item.daysToCompletion.ToString("N0"));
        if (item.costPaid && !item.shipyard.active) text = TIUtilities.RedLine(text + " " + Loc.T("UI.Fleets.Inactive"));
        __instance.className.SetText(text);
    }
}

[HarmonyPatch]
internal static class StationConstructionButtons
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (string name in new[] { "SetButtons", "OnNewClassSelectedInFleetController" })
            yield return AccessTools.Method(typeof(ShipyardGridItemController), name);
    }
    private static void Postfix(ShipyardGridItemController __instance)
    {
        if (StationConstruction.Enabled(__instance)) StationConstruction.Buttons(__instance);
    }
}

[HarmonyPatch(typeof(ShipyardGridItemController), nameof(ShipyardGridItemController.CanAddShip))]
internal static class StationConstructionCanAdd
{
    private static bool Prefix(ShipyardGridItemController __instance, ref bool __result)
    {
        if (!StationConstruction.Enabled(__instance) || __instance.controller.refitScrollviews.activeSelf) return true;
        __result = StationConstruction.BuildYard(__instance) != null;
        return false;
    }
}

[HarmonyPatch]
internal static class StationConstructionCanEdit
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (string name in new[] { "CanMoveUpInQueue", "CanMoveDownInQueue", "CanRemoveFromQueue" })
            yield return AccessTools.PropertyGetter(typeof(ShipyardGridItemController), name);
    }
    private static bool Prefix(ShipyardGridItemController __instance, MethodBase __originalMethod, ref bool __result)
    {
        if (!StationConstruction.Enabled(__instance)) return true;
        var queue = StationConstruction.View(__instance.controller.activePlayer, __instance.shipyardIdx);
        var selected = __instance.selectedQueueItem;
        int index = queue.IndexOf(selected);
        switch (__originalMethod.Name)
        {
            case "get_CanRemoveFromQueue": __result = index >= 0; break;
            case "get_CanMoveUpInQueue":
                __result = index > 0 && !selected.costPaid && !queue[index - 1].costPaid; break;
            default: __result = index >= 0 && index < queue.Count - 1 && !selected.costPaid; break;
        }
        return false;
    }
}

[HarmonyPatch]
internal static class StationConstructionQueueControls
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (string name in new[] { "OnClearQueueButtonClicked", "OnRemoveFromQueueClicked",
            "OnMoveUpInQueueClicked", "OnMoveDowninQueueClicked", "ToggleUseEarthResources" })
            yield return AccessTools.Method(typeof(ShipyardGridItemController), name);
    }
    private static bool Prefix(ShipyardGridItemController __instance, MethodBase __originalMethod)
    {
        var card = __instance;
        if (!StationConstruction.Enabled(card)) return true;
        var faction = card.controller.activePlayer;
        var queue = StationConstruction.View(faction, card.shipyardIdx);
        var selected = card.selectedQueueItem;
        int index = queue.IndexOf(selected);
        StationConstruction.editingDepth++;
        try
        {
            switch (__originalMethod.Name)
            {
                case "OnClearQueueButtonClicked":
                    foreach (var item in queue.Where(i => !i.costPaid).ToList())
                        faction.playerControl.StartAction(new RemoveShipFromShipyardQueueAction(item.shipyard, item));
                    card.SetSelectedQueueItem(null);
                    break;
                case "OnRemoveFromQueueClicked":
                    if (index >= 0)
                        faction.playerControl.StartAction(new RemoveShipFromShipyardQueueAction(selected.shipyard, selected));
                    card.SetSelectedQueueItem(null);
                    break;
                case "ToggleUseEarthResources":
                    foreach (var yard in StationConstruction.Yards(faction, card.shipyardIdx.ref_hab))
                        faction.playerControl.StartAction(new UpdatePayMethodForConstructionQueueAction(yard, card.allowPayFromEarthToggle.isOn));
                    break;
                default:
                    int next = index + (__originalMethod.Name == "OnMoveUpInQueueClicked" ? -1 : 1);
                    if (index < 0 || next < 0 || next >= queue.Count || selected.costPaid || queue[next].costPaid) break;
                    StationConstruction.Normalize(faction, card.shipyardIdx.ref_hab);
                    var actual = faction.GetShipyardQueue(selected.shipyard);
                    int target = actual.IndexOf(queue[next]);
                    faction.playerControl.StartAction(new RepositionShipinConstructionQueueAction(selected.shipyard, selected, target));
                    break;
            }
        }
        finally { StationConstruction.editingDepth--; }
        AudioManager.PlayOneShot("event:/SFX/UI_SFX/trig_SFX_GenericConfirm");
        StationConstruction.Dispatch(faction, card.shipyardIdx.ref_hab);
        card.controller.RefreshConstructionManager();
        return false;
    }
}

[HarmonyPatch]
internal static class StationConstructionFilter
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(FleetsScreenController), nameof(FleetsScreenController.FilterShipLists));
        yield return AccessTools.Method(typeof(FleetsScreenController), nameof(FleetsScreenController.RefreshShipyards));
    }
    [HarmonyPriority(Priority.First)]
    private static void Postfix(FleetsScreenController __instance) { StationConstruction.Apply(__instance); }
}

[HarmonyPatch(typeof(FleetsScreenController), nameof(FleetsScreenController.OnClickAddToFastestQueue))]
internal static class StationConstructionFastest
{
    private static bool Prefix(FleetsScreenController __instance)
    {
        if (!Main.settings.pooledStationConstruction) return true;
        var design = __instance.constructionManagerSelectedDesign;
        if (design == null) return false;
        var cards = new List<ShipyardGridItemController>();
        foreach (object entry in __instance.shipyardGridList)
            if (entry is ShipyardGridItemController card && card.gameObject.activeSelf) cards.Add(card);
        var best = cards.OrderBy(c => StationConstruction.FinishTime(__instance.activePlayer, c.shipyardIdx, design)).FirstOrDefault();
        if (best != null && !float.IsPositiveInfinity(StationConstruction.FinishTime(__instance.activePlayer, best.shipyardIdx, design)))
        {
            AudioManager.PlayOneShot("event:/SFX/UI_SFX/trig_SFX_InitiateBuildShip");
            __instance.activePlayer.playerControl.StartAction(new AddShipDesignToConstructionQueueAction(
                best.shipyardIdx, design, best.allowPayFromEarth, 1f, null));
            __instance.RefreshConstructionManager();
        }
        return false;
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
    // A sweep is only ever run by the outermost call, because the sweep re-enters the method it
    // is a postfix on. A mirror is a Station module and a power consumer, so refreshing a
    // station's power grid can power one up or, in the deficit loop, shut one down - and either
    // toggle runs SetPowerStatus, which calls ChangeSolarMirrorBonus, which lands here again and
    // starts another sweep before the first has returned. Since each toggle really does change
    // the module's state, SetPowerStatus does not stop it at its own no-change guard: a mirror
    // turned on at one depth and off at the next goes round for as long as the stack holds.
    //
    // That is why the crash had nothing to say for itself. A stack overflow in Mono is not an
    // exception anything can catch - not the try/catch in OnChange, not Harmony, not Unity's
    // logger. The process simply ends.
    //
    // Vanilla cannot reach this: ChangeSolarMirrorBonus refreshes surfaceBases, and every solar
    // mirror module in TIHabModuleTemplate.json is habType Station, so a surface base can never
    // hold the module whose toggle would call back in. Pointing the same refresh at stations is
    // what closes the loop, so the guard belongs here rather than in the tweak that reads it.
    private static bool refreshing;

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
        // Above the guard: a nested toggle still has to drop the cache, or the sweep that is
        // already running would go on handing out the bonus as it stood before the toggle.
        StationSolarMirrors.Invalidate();
        if (refreshing)
        {
            return;
        }
        refreshing = true;
        try
        {
            foreach (TIOrbitState orbit in StationOrbits(__instance))
            {
                // stationsInOrbit builds a fresh list on every read, so the loop is already
                // reading a copy and a refresh that moves a station cannot disturb it.
                foreach (TIHabState station in orbit.stationsInOrbit)
                {
                    if (station != null && station.faction == faction)
                    {
                        station.UpdatePowerManagement(changeBy > 0, null, faction.player.isAI);
                    }
                }
            }
        }
        finally
        {
            refreshing = false;
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
// the Nth repeat of Management Research costs N times its base while always granting the same
// flat 5 capacity. Research per point is therefore 300N in the base game (cost 1500), 120N in
// Broken Earth (600) and 60N in Adamantine Sky (300). It diverges in each of them, with the
// cumulative cost of N points growing as the square of N. Meanwhile the rest of the cap - global
// freebies, councilor attributes, one admin module per station, a fixed list of one-off projects
// - is hard-bounded, while maintenance cost scales with national GDP forever. The repeatable is
// the only unbounded source and vanilla prices it out of reach, so the cap stops rising long
// before GDP does. Audience, Commercial and Operations Research have the same shape with
// Influence, Money and Operations.
//
// With scaling r the Nth repeat pays base x (1 + r(N-1)), rounded to whole units, so research per
// point converges on base/(grant x r) instead of diverging. At the default 0.03 that is 10,000
// per point in the base game and 4,000 in Broken Earth, well above any one-off capacity project,
// so grinding this stays a poor option rather than a pointless one. Set the scaling to 0 for
// stock behavior.
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
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod) =>
        NotificationTemplateName.UseOriginal(instructions, __originalMethod);

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

// Native notification methods select their template with GetCurrentMethod().Name.
// In a Harmony replacement that names the generated wrapper, not the native method,
// so AddItem drops the notification for a missing template even with the tweak off.
internal static class NotificationTemplateName
{
    internal static IEnumerable<CodeInstruction> UseOriginal(IEnumerable<CodeInstruction> instructions, MethodBase original)
    {
        var codes = instructions.ToList();
        var currentMethod = AccessTools.Method(typeof(MethodBase), nameof(MethodBase.GetCurrentMethod));
        var methodName = AccessTools.PropertyGetter(typeof(MemberInfo), nameof(MemberInfo.Name));
        for (int i = 0; i + 1 < codes.Count; i++)
        {
            if (!codes[i].Calls(currentMethod) || !codes[i + 1].Calls(methodName)) continue;
            // Keep both instructions' labels and exception blocks in place.
            codes[i].opcode = OpCodes.Ldstr;
            codes[i].operand = original.Name;
            codes[i + 1].opcode = OpCodes.Nop;
            codes[i + 1].operand = null;
            return codes;
        }
        throw new InvalidOperationException("Notification template lookup not found in " + original.Name);
    }
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
// excluded - it can still speak for itself, and its claims are not going spare. Hostility carries
// across unchanged - a claim the other nation held hostilely stays hostile for you, because the
// point is to skip the merge dance, not to launder a grievance into a peaceful merger.
//
// Borrowed claims are never written anywhere. They are worked out afresh each time the game reads
// a nation's claims: the getters of TINationState.claims and hostileClaims return the nation's own
// list with the borrowed ones merged in. Every reader in the game goes through those two getters -
// the AI, federations, effects, war and the UI alike - so nothing has to notice a capital changing
// hands, a project ungating a claim or a nation coming back: the next read gives the new answer.
// An earlier version wrote the borrowed claims into the real lists and kept them in step with a
// hook for every event that could move them; each missed or mistimed event was a bug.
//
// Writes go the other way. Every place the game adds or removes a claim is one of a handful of
// TINationState methods, and those are pointed at the backing fields, so a write lands in the
// nation's own list and never in a merged copy that is thrown away. A claim the game hands out is
// the nation's own outright - including the one it gives a nation on a region it conquers, which
// vanilla makes hostile when the nation had no claim of its own. A conquest made on a borrowed
// claim is therefore held hostile, and a capital held hostile lends nothing, so a run of conquests
// has to stop and legitimize each capital before that capital's claims come along.
//
// Saves read the same getters, so while one is written they answer with the nation's own lists.
// A save written with this on is a vanilla save, and turning the tweak off loses nothing.
[HarmonyPatch]
internal static class InheritedCapitalClaims
{
    private static readonly AccessTools.FieldRef<TINationState, List<TIRegionState>> ownClaims =
        AccessTools.FieldRefAccess<TINationState, List<TIRegionState>>("<claims>k__BackingField");
    private static readonly AccessTools.FieldRef<TINationState, List<TIRegionState>> ownHostileClaims =
        AccessTools.FieldRefAccess<TINationState, List<TIRegionState>>("<hostileClaims>k__BackingField");

    // originalCapital is set during world init and never moves afterwards (SetCapital changes the
    // working capital, not this), so the region -> nation map is built once and only rebuilt if the
    // nation count changes. Scanning every nation per region on every read would be far too slow.
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

    // While a save is being written the getters answer with the nation's own lists only.
    private static bool saving;

    // Nothing is merged until the campaign is up. A load reads these getters while it is still
    // filling the game states in, and two things go wrong if they answer then: the load can fill
    // a merged copy instead of the real list, losing the claims, and CapitalHolder asks
    // GameStateManager.AllNations, which caches its answer - asked mid-load it caches a list with
    // no nations in it, and the load dies on the first Min over them.
    private static bool ready;

    [HarmonyPatch(typeof(GameStateManager), nameof(GameStateManager.LoadAllGameStates))]
    [HarmonyPrefix]
    private static void BeforeLoad() => ready = false;

    [HarmonyPatch(typeof(GameStateManager), nameof(GameStateManager.ClearAllGameStates))]
    [HarmonyPrefix]
    private static void BeforeClear() => ready = false;

    // A capital about to fall. CompleteOccupationofRegion draws up what the conqueror takes from
    // the claims it holds the moment the capital falls, before it holds the capital - so the claims
    // that capital brings would arrive one conquest late. For the length of that one call the
    // conqueror is read as already holding what it is about to take, and the loser, if it is
    // about to lose everything, as already dormant.
    private sealed class Capture
    {
        internal TINationState winner;
        internal TINationState goingDormant;
        internal readonly HashSet<TIRegionState> incoming = new HashSet<TIRegionState>();
    }

    private static Capture capture;

    // What the nation borrows right now, as region -> borrowed as hostile. Null when nothing.
    // Reads only the backing fields, never the patched getters, so it cannot recurse into itself.
    private static Dictionary<TIRegionState, bool> Borrowed(TINationState nation)
    {
        if (!ready || !Main.settings.inheritedCapitalClaims || saving || nation == null || nation.alienNation)
        {
            return null;
        }
        List<TIRegionState> own = ownClaims(nation);
        List<TIRegionState> ownHostile = ownHostileClaims(nation);
        if (own == null || ownHostile == null || nation.regions == null)
        {
            return null;                             // still being built
        }
        Capture pending = capture?.winner == nation ? capture : null;
        IEnumerable<TIRegionState> holding = pending == null ? nation.regions
            : nation.regions.Concat(pending.incoming.Where(r => !nation.regions.Contains(r)));
        Dictionary<TIRegionState, bool> want = null;
        foreach (TIRegionState capital in holding)
        {
            foreach (KeyValuePair<TIRegionState, bool> lent in LentThrough(nation, capital, pending))
            {
                want ??= new Dictionary<TIRegionState, bool>();
                // two sources disagreeing on hostility: the peaceful reading wins
                want[lent.Key] = want.TryGetValue(lent.Key, out bool seen) ? seen && lent.Value : lent.Value;
            }
        }
        return want;
    }

    // What one capital lends the nation, as claim -> hostile. Backing fields only, like Borrowed.
    private static IEnumerable<KeyValuePair<TIRegionState, bool>> LentThrough(TINationState nation,
        TIRegionState capital, Capture pending)
    {
        List<TIRegionState> own = ownClaims(nation);
        // Holding it in anger lends nothing. A capital not yet taken counts only on a peaceful
        // claim of our own: one we reach through a borrowed claim will be held hostile.
        bool held = nation.regions.Contains(capital);
        if (ownHostileClaims(nation).Contains(capital) || (!held && !own.Contains(capital)))
        {
            yield break;
        }
        TINationState source = CapitalHolder(capital);
        // Dormant nations only. A living nation that moved its capital still speaks for itself,
        // and borrowing from it would be leeching rather than skipping the merge dance.
        if (source == null || source == nation || source.alienNation
            || (source.extant && source != pending?.goingDormant))
        {
            yield break;
        }
        List<TIRegionState> theirs = ownClaims(source);
        List<TIRegionState> theirHostile = ownHostileClaims(source);
        if (theirs == null || theirHostile == null)
        {
            yield break;
        }
        foreach (TIRegionState claim in theirs)
        {
            if (claim == null || claim.nation == nation || own.Contains(claim) || !claim.ClaimedBy(source))
            {
                continue;                            // ours already, or gated behind an unfinished project
            }
            bool hostile = theirHostile.Contains(claim);
            if (!held && hostile)
            {
                continue;                            // a capital not yet taken hands over only what it is owed
            }
            yield return new KeyValuePair<TIRegionState, bool>(claim, hostile);
        }
    }

    // Borrowed claims the nation can only borrow as hostile right now: what legitimizing works on.
    private static List<TIRegionState> HostileBorrowed(TINationState nation)
    {
        Dictionary<TIRegionState, bool> borrowed = Borrowed(nation);
        return borrowed == null ? new List<TIRegionState>()
            : borrowed.Where(pair => pair.Value).Select(pair => pair.Key).ToList();
    }

    // Legitimizing reaches the claims you borrow. Vanilla's counter - one tick for each Government
    // or Unity priority completed, numPrioritiesForLegitimize to fill - normally runs only while
    // the nation holds a region by a hostile claim, and each time it fills it makes one such
    // region peaceful. Here it also runs while the nation borrows a hostile claim, and once no
    // hostile region is left, filling it makes one borrowed claim peaceful at its source: the
    // dormant nation's own claim loses its hostility. It is still borrowed - it goes with the
    // capital - but it is peaceful for whoever holds that capital from then on.
    [HarmonyPatch(typeof(TINationState), nameof(TINationState.canAccumulateLegitimizeClaimTriggers), MethodType.Getter)]
    [HarmonyPostfix]
    private static void CountsWhileBorrowingHostile(TINationState __instance, ref bool __result)
    {
        __result = __result || HostileBorrowed(__instance).Count > 0;
    }

    // Hostile regions first, as vanilla has it; hostile borrowed claims only once none are left,
    // so the counter's target reads the same in vanilla's tooltip and in its completion.
    [HarmonyPatch(typeof(TINationState), nameof(TINationState.CandidateLegitimizeClaimRegions))]
    [HarmonyPostfix]
    private static void HostileBorrowedAreCandidates(TINationState __instance, List<TIRegionState> __result)
    {
        if (__result.Count == 0)
        {
            __result.AddRange(HostileBorrowed(__instance));
        }
    }

    [HarmonyPatch(typeof(TINationState), nameof(TINationState.OnLegitimizeClaimPriorityComplete))]
    [HarmonyPrefix]
    private static bool LegitimizeAtSource(TINationState __instance, ref bool __result)
    {
        TIRegionState claim = __instance.GetNextLegitimizeClaimRegion();
        if (claim == null || claim.nation == __instance)
        {
            return true;                             // vanilla's own case: a region it holds
        }
        // Every capital lending it hostile, since a claim is only borrowed hostile when no source
        // lends it peacefully.
        bool changed = false;
        foreach (TIRegionState capital in __instance.regions)
        {
            if (LentThrough(__instance, capital, null).Any(lent => lent.Key == claim && lent.Value))
            {
                CapitalHolder(capital).RemoveHostileClaim(claim);
                changed = true;
            }
        }
        if (!changed)
        {
            return true;
        }
        TINotificationQueueState.LogNationsGainClaims(new Dictionary<TINationState, List<TIRegionState>>
        {
            { __instance, new List<TIRegionState> { claim } }
        }, null);
        __result = true;
        return false;
    }

    // Vanilla's tooltip line for the counter promises the region loses its hostile status and
    // unrest falls. For a borrowed claim it says what will happen instead.
    [HarmonyPatch(typeof(PriorityListItemController), nameof(PriorityListItemController.priorityTipStr))]
    [HarmonyPostfix]
    private static void DescribeBorrowedLegitimize(TINationState nation, PriorityType priority, ref string __result)
    {
        if ((priority != PriorityType.Government && priority != PriorityType.Unity) || __result == null)
        {
            return;
        }
        TIRegionState claim = nation?.GetNextLegitimizeClaimRegion();
        if (claim == null || claim.nation == nation)
        {
            return;
        }
        object[] args =
        {
            claim.displayName, nation.accumulatedLegitimizeClaimTriggers,
            TIGlobalConfig.globalConfig.numPrioritiesForLegitimize
        };
        __result = __result.Replace(Loc.T("UI.Nation.LegitimizeClaim", args),
            Loc.T("UI.CataTweaks.LegitimizeBorrowedClaims", args));
    }

    // Forty-five projects - Greater Indonesia, Commonwealth Restored and the rest - name a nation
    // that must exist, and do nothing but hand that nation claims. With its capital in someone's
    // peaceful hands a dormant nation's claims are still in use, so such a project counts that
    // nation as present: completing it gives the dormant nation the claims, and the holder
    // borrows them.
    internal static bool PresentThroughCapital(TINationState nation)
    {
        if (nation == null || nation.extant)
        {
            return nation != null;
        }
        if (!ready || !Main.settings.inheritedCapitalClaims || nation.alienNation)
        {
            return false;
        }
        TIRegionState capital = nation.originalCapital;
        TINationState holder = capital?.nation;
        return holder != null && holder != nation && !holder.alienNation
            && ownHostileClaims(holder)?.Contains(capital) == false;
    }

    // The requirement is one extant test at the top of PrereqsSatisfied; it becomes this one.
    [HarmonyPatch(typeof(TIProjectTemplate), nameof(TIProjectTemplate.PrereqsSatisfied))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> NationPresentThroughCapital(IEnumerable<CodeInstruction> code)
    {
        MethodInfo extant = AccessTools.PropertyGetter(typeof(TINationState), nameof(TINationState.extant));
        MethodInfo present = AccessTools.Method(typeof(InheritedCapitalClaims), nameof(PresentThroughCapital));
        foreach (CodeInstruction instruction in code)
        {
            if (instruction.Calls(extant))
            {
                instruction.opcode = OpCodes.Call;       // same stack shape: nation in, bool out
                instruction.operand = present;
            }
            yield return instruction;
        }
    }

    // A project is rolled for a faction when a prerequisite completes, and again for one gated on
    // a nation when that nation gains independence. A gate that opens because a capital changed
    // hands has no such moment, so these rolls are made here, on load and monthly: once per
    // faction per project, as vanilla's own independence roll does, skipping any the faction has
    // already triggered, missed, has available or has completed. A roll that fails files the
    // project as missed, exactly like a vanilla one.
    private static void RollOpenedProjects()
    {
        if (!ready || !Main.settings.inheritedCapitalClaims)
        {
            return;
        }
        List<TIProjectTemplate> opened = TIGlobalResearchState.GetAllProjects()
            .Where(p => !string.IsNullOrEmpty(p.requiresNation))
            .Where(p => { TINationState n = p.requiredNationState; return n != null && !n.extant && PresentThroughCapital(n); })
            .ToList();
        if (opened.Count == 0)
        {
            return;
        }
        foreach (TIFactionState faction in GameStateManager.AllFactions().Where(f => f.IsActiveHumanFaction))
        {
            foreach (TIProjectTemplate project in opened)
            {
                if (faction.missedProjects?.Contains(project.dataName) == true
                    || faction.completedProjects.Contains(project) || faction.availableProjects.Contains(project)
                    || faction.ProjectAlreadyTriggered(project))
                {
                    continue;
                }
                faction.RollToAddProjectTrigger(project);
            }
        }
    }

    [HarmonyPatch(typeof(PavonisInteractive.TerraInvicta.Systems.PeriodicUpdates.NationPeriodicUpdate), "OnMonthlyUpdate")]
    [HarmonyPostfix]
    private static void Monthly() => RollOpenedProjects();

    [HarmonyPatch(typeof(TINationState), nameof(TINationState.claims), MethodType.Getter)]
    [HarmonyPostfix]
    private static void WithBorrowedClaims(TINationState __instance, ref List<TIRegionState> __result)
    {
        Dictionary<TIRegionState, bool> borrowed = Borrowed(__instance);
        if (borrowed == null)
        {
            return;
        }
        var merged = new List<TIRegionState>(__result.Count + borrowed.Count);
        merged.AddRange(__result);
        merged.AddRange(borrowed.Keys);
        __result = merged;
    }

    [HarmonyPatch(typeof(TINationState), nameof(TINationState.hostileClaims), MethodType.Getter)]
    [HarmonyPostfix]
    private static void WithBorrowedHostileClaims(TINationState __instance, ref List<TIRegionState> __result)
    {
        Dictionary<TIRegionState, bool> borrowed = Borrowed(__instance);
        if (borrowed == null || !borrowed.ContainsValue(true))
        {
            return;
        }
        var merged = new List<TIRegionState>(__result);
        merged.AddRange(borrowed.Where(pair => pair.Value).Select(pair => pair.Key));
        __result = merged;
    }

    // Every method that writes to either list, read off the decompiled game: each reaches the list
    // through its getter, which would hand it a merged copy. Pointing the getter call at the
    // backing field keeps the write on the real list. A read inside these methods sees the
    // nation's own claims too, which is what each of them means: SetClaim asks whether the claim
    // is new to the nation, not whether it can currently be borrowed.
    [HarmonyPatch]
    private static class WritersUseOwnLists
    {
        private static readonly MethodInfo getClaims =
            AccessTools.PropertyGetter(typeof(TINationState), nameof(TINationState.claims));
        private static readonly MethodInfo getHostileClaims =
            AccessTools.PropertyGetter(typeof(TINationState), nameof(TINationState.hostileClaims));

        private static IEnumerable<MethodBase> TargetMethods() => new[]
        {
            nameof(TINationState.SetClaim), nameof(TINationState.RemoveClaim),
            nameof(TINationState.RemoveHostileClaim), nameof(TINationState.SetCapital),
            nameof(TINationState.TransferRegionsControlTo), nameof(TINationState.InitWithTemplate),
            nameof(TINationState.SetAllBilaterals), nameof(TINationState.PostGlobalGameStateCreateInit_2),
        }.Select(name => (MethodBase)AccessTools.Method(typeof(TINationState), name));

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> code)
        {
            FieldInfo claimsField = AccessTools.Field(typeof(TINationState), "<claims>k__BackingField");
            FieldInfo hostileField = AccessTools.Field(typeof(TINationState), "<hostileClaims>k__BackingField");
            foreach (CodeInstruction instruction in code)
            {
                // Same stack shape: the getter and the field load both take the nation, leave the list.
                if (instruction.Calls(getClaims))
                {
                    instruction.opcode = OpCodes.Ldfld;
                    instruction.operand = claimsField;
                }
                else if (instruction.Calls(getHostileClaims))
                {
                    instruction.opcode = OpCodes.Ldfld;
                    instruction.operand = hostileField;
                }
                yield return instruction;
            }
        }
    }

    // Taking a capital hands the conqueror every region of the loser it claims, and that list is
    // drawn up once, from the claims it holds the moment the capital falls. This decides what the
    // conqueror is about to take, and leaves it in force until the capture is done - the same
    // condition vanilla tests, so what it assumes is always what happens.
    [HarmonyPatch(typeof(TIRegionState), "CompleteOccupationofRegion")]
    [HarmonyPrefix]
    private static void BeforeCapture(TIRegionState __instance, TIArmyState army, out Capture __state)
    {
        __state = capture;
        TINationState loser = __instance.nation;
        TINationState winner = __instance.leadOccupier ?? army?.homeNation;
        if (!Main.settings.inheritedCapitalClaims || loser == null || winner == null || winner == loser
            || !((__instance == loser.capital && (!loser.alienNation || loser.regions.Count == 1))
                 || loser.regions.All(r => r.IsFullyOccupied())))
        {
            return;
        }
        capture = Plan(winner, loser, __instance.GetOccupyingAlliance(ordered: true));
    }

    // Finalizer, not a postfix: a capture that throws must not leave its assumption behind.
    [HarmonyPatch(typeof(TIRegionState), "CompleteOccupationofRegion")]
    [HarmonyFinalizer]
    private static void AfterCapture(Capture __state) => capture = __state;

    // What the winner will hold is what it claims of the loser, and what it claims depends on the
    // capitals among that, so this runs to a fixed point. It first assumes the loser goes dormant -
    // the loser's own peaceful claims cover its own core, so borrowing them is what takes the lot -
    // and keeps that only if it comes true: every region the loser has is claimed by the winner or
    // by an ally at war with it, which is exactly when vanilla leaves the loser with nothing.
    private static Capture Plan(TINationState winner, TINationState loser, List<TINationState> alliance)
    {
        foreach (bool dormant in new[] { true, false })
        {
            capture = new Capture { winner = winner, goingDormant = dormant ? loser : null };
            for (int pass = 0; pass < 8; pass++)          // each pass can only add capitals; 8 is plenty
            {
                var next = new HashSet<TIRegionState>(loser.regions.Where(r => winner.claims.Contains(r)));
                if (next.SetEquals(capture.incoming))
                {
                    break;
                }
                capture.incoming.Clear();
                capture.incoming.UnionWith(next);
            }
            if (!dormant || loser.regions.All(r => capture.incoming.Contains(r)
                || alliance.Any(x => x != winner && x.IsAtWarWith(loser) && x.claims.Contains(r))))
            {
                return capture;
            }
        }
        return capture;
    }

    // A merger. AbsorbNation moves the joining nation's regions with the same transfer a conquest
    // uses, and that gives the absorber a claim of its own on every region it did not claim
    // before - hostile, as a seizure. Vanilla does the same, but here the absorber now holds the
    // joiner's capital and borrows its claims, and its own hostile claim on a region hides the
    // peaceful one it borrows: a claim the joiner had legitimized would come out hostile again.
    // So each region the merger seizes that way, and borrows peacefully once it is done, has its
    // new claim made peaceful. Conquest never comes through here with regions to move - vanilla
    // calls AbsorbNation on a loser that is already dormant - so conquests stay hostile.
    private sealed class Merge
    {
        internal List<TIRegionState> moving;
        internal HashSet<TIRegionState> claimedBefore;
    }

    [HarmonyPatch(typeof(TINationState), nameof(TINationState.AbsorbNation))]
    [HarmonyPrefix]
    private static void BeforeMerge(TINationState __instance, TINationState joiningNationState, out Merge __state)
    {
        __state = null;
        // Vanilla's democracy rule makes the whole merger hostile, claimed or not; that stands.
        if (!ready || !Main.settings.inheritedCapitalClaims || __instance.alienNation
            || joiningNationState == null || !joiningNationState.extant
            || __instance.HostileClaimDueToDemocracy(joiningNationState))
        {
            return;
        }
        __state = new Merge
        {
            moving = new List<TIRegionState>(joiningNationState.regions),
            claimedBefore = new HashSet<TIRegionState>(ownClaims(__instance)),
        };
    }

    // Runs to a fixed point, like Plan: a capital the joiner held made peaceful on one pass lends
    // its own dormant nation's claims on the next.
    [HarmonyPatch(typeof(TINationState), nameof(TINationState.AbsorbNation))]
    [HarmonyPostfix]
    private static void AfterMerge(TINationState __instance, Merge __state)
    {
        if (__state == null)
        {
            return;
        }
        List<TIRegionState> ownHostile = ownHostileClaims(__instance);
        List<TIRegionState> seized = __state.moving
            .Where(r => r.nation == __instance && !__state.claimedBefore.Contains(r) && ownHostile.Contains(r))
            .ToList();
        bool changed = true;
        while (changed && seized.Count > 0)
        {
            changed = false;
            foreach (TIRegionState region in seized.ToList())
            {
                if (__instance.regions.Any(capital => !ownHostile.Contains(capital)
                    && LendsPeacefully(__instance, capital, region)))
                {
                    __instance.RemoveHostileClaim(region);   // writes the backing list, so ownHostile sees it
                    seized.Remove(region);
                    changed = true;
                }
            }
        }
    }

    // Whether the capital lends the nation a peaceful claim on the region, by LentThrough's rules
    // but without its skip for a region the nation already holds or claims. Backing fields only.
    private static bool LendsPeacefully(TINationState nation, TIRegionState capital, TIRegionState region)
    {
        TINationState source = CapitalHolder(capital);
        return source != null && source != nation && !source.alienNation && !source.extant
            && ownClaims(source)?.Contains(region) == true
            && ownHostileClaims(source)?.Contains(region) == false
            && region.ClaimedBy(source);
    }

    [HarmonyPatch(typeof(GameStateManager), nameof(GameStateManager.SaveAllGameStates))]
    [HarmonyPrefix]
    private static void BeforeSave() => saving = true;

    // Finalizer, not a postfix: a save that throws must not leave the getters stuck on own lists.
    [HarmonyPatch(typeof(GameStateManager), nameof(GameStateManager.SaveAllGameStates))]
    [HarmonyFinalizer]
    private static void AfterSave() => saving = false;

    // A new campaign or a load can bring a different set of nations.
    [HarmonyPatch(typeof(GameControl), nameof(GameControl.CompleteInit))]
    [HarmonyPostfix]
    private static void OnCampaignReady()
    {
        capitalOf = null;
        capture = null;
        saving = false;
        ready = true;
        RollOpenedProjects();
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
            bool council = Array.IndexOf(Projects, project.dataName) >= 0;
            bool empires = Array.IndexOf(RestoredEmpires.Projects, project.dataName) >= 0;
            if (!council && !empires)
            {
                return true;
            }
            bool hidden =
                (council && !CampaignFlags.CouncilPool)
                || (empires && !CampaignFlags.RestoredEmpires)
                // requiresNation is not the gate it looks like: PrereqsSatisfied only rejects a
                // project whose required nation exists and is gone, so one naming a nation the
                // scenario never had passes. Rome exists in Broken Earth alone, and its two
                // projects would otherwise be offered in every start with nothing to claim.
                || (!string.IsNullOrEmpty(project.requiresNation)
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
            // Below vanilla's ceiling the clamp never bound, so its answer already counts every
            // effect and the pool cannot bite: two spies still leave six seats. Nothing to do,
            // and the effects walk below is worth skipping on a getter this warm.
            if (__result < Pool - 2)
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

// Tweak 13: control points can be put on the diplomacy table, next to orgs, habs and projects.
//
// Most of this exists in the game already and was never wired up. TradeOffer carries a
// controlPoints list, ProcessTrade hands each entry over through ChangeControlPointOwner under a
// ControlPointChangeCause.Trade of its own, DiplomacyController holds serialized playerCPsTab and
// aiCPsTab fields, and the shipped localization has UI.Notifications.Diplomacy.TabCPs. What is
// missing is the UI that fills the list - EvaluateTrade even clears it - and any valuation:
// TradeAI's categories are orgs, resources, projects, habs and treaties, so a control point
// scores zero for both sides of a deal.
//
// The valuation is the category TradeAI never had, added over the top of ScoreAgreement rather
// than inside it, and the verdict EvaluateTrade reached before a control point was in the offer
// is worked out again once one is. A seat in a nation its holder has abandoned is worth a tenth
// to that holder and full value to everyone else, so abandoned seats go cheaply and a faction
// pushes its own into the offers it builds.
// ponytail: the arithmetic in Value is not calibrated against anything vanilla, so there is a
// slider over it rather than a constant to argue about.
internal static class ControlPointTrade
{
    // TradeItemType has no control point member and an enum cannot be extended, so these rows
    // carry values from outside it. That is what makes them safe to add: every vanilla switch
    // on itemType falls through to its default and every comparison against it misses, so the
    // table scan in EvaluateTrade, the tab toggles and the row cleanup all pass our rows by. We
    // do those three jobs ourselves below. The second value marks a nation heading.
    private const TradeItemType CPItem = (TradeItemType)100;

    private const TradeItemType CPGroupItem = (TradeItemType)101;

    private const string TabKey = "UI.Notifications.Diplomacy.TabCPs";

    // Which control point a table row stands for. DiplomacyTableListItem has a field for an org,
    // a hab and a project, and none for a control point. Rows are destroyed and rebuilt whenever
    // the screen reloads its banks, and this map goes with them.
    private static readonly Dictionary<GameObject, TIControlPoint> rows =
        new Dictionary<GameObject, TIControlPoint>();

    // The other direction, for an offer that names a control point the player did not pick.
    private static readonly Dictionary<TIControlPoint, DiplomacyBankListItem> banks =
        new Dictionary<TIControlPoint, DiplomacyBankListItem>();

    // One per nation per side: a heading that opens and closes that nation's control points.
    private sealed class Group
    {
        internal bool player;
        internal DiplomacyBankListItem heading;
        internal readonly List<GameObject> bankRows = new List<GameObject>();
        internal bool open;
    }

    private static readonly List<Group> groups = new List<Group>();

    private static readonly FieldInfo playerOffer =
        AccessTools.Field(typeof(DiplomacyController), "playerTradeOffer");

    private static readonly FieldInfo aiOffer =
        AccessTools.Field(typeof(DiplomacyController), "aiTradeOffer");

    // Whether each side's list is open. Vanilla keeps a pair of bools per category and its
    // ToggleTradeItems switches on the item type, returning at the default it cannot name, so
    // the control point tab needs both halves of that here.
    private static bool playerVisible;

    private static bool aiVisible;

    // Aliens take control points by enthralling rather than by deal, and a control point inside
    // an alien nation is forced back to the alien faction the moment it changes hands. A
    // suppressed one is left out because the handover re-enables its benefits, which would make
    // a trade and a trade back a way to shrug off a crackdown.
    private static bool Tradeable(TIControlPoint point)
    {
        if (point == null || point.nation == null || point.nation.alienNation)
        {
            return false;
        }
        return !point.benefitsDisabled || Main.settings.tradeSuppressedControlPoints;
    }

    // Abandoning a nation self-disables the holder's seats there, so an abandoned seat reads as
    // suppressed like any other. This says nothing about whether a seat can be traded - the
    // toggle behind it is free to flip, so it would gate nothing - and only sets what the holder
    // thinks the seat is still worth.
    internal static bool Abandoned(TIControlPoint point, TIFactionState faction)
    {
        List<TINationState> abandoned = faction?.permaAbandonedNations;
        return point?.nation != null && abandoned != null && abandoned.Contains(point.nation);
    }

    internal static void Build(DiplomacyController ui)
    {
        playerVisible = false;
        aiVisible = false;
        TIFactionState player = GameControl.control?.activePlayer;
        TIFactionState other = ui.tradingFaction;
        if (player == null || other == null || player.IsAlienFaction || other.IsAlienFaction)
        {
            return;
        }
        Side(ui, player.controlPoints, ui.playerCPsTab,
            ui.playerBankItemsContent, ui.playerTableItemsContent, true);
        Side(ui, other.controlPoints, ui.aiCPsTab,
            ui.aiBankItemsContent, ui.aiTableItemsContent, false);
    }

    // The tab is already in the prefab and nothing in the game ever shows or wires it. Vanilla
    // positions a tab by moving it to the end of the bank list and appending its rows after it,
    // which is why this runs before the rows are added. A side with nothing to offer keeps its
    // tab hidden, as the hab and project tabs do.
    private static void Side(DiplomacyController ui, List<TIControlPoint> points,
        DiplomacyBankListItem tab, GameObject bank, GameObject table, bool player)
    {
        if (bank == null || tab == null)
        {
            return;
        }
        tab.transform.SetSiblingIndex(bank.transform.childCount - 1);
        int added = Add(ui, points, tab, bank, table, player);
        tab.gameObject.SetActive(added > 0);
        if (added == 0)
        {
            return;
        }
        if (tab.tabText != null)
        {
            tab.tabText.text = "+";
        }
        // The tab was duplicated from the projects tab in the editor and its localizer still
        // carries the projects key, which is why it reads "Projects". Writing the text is not
        // enough: UITextLocalizer.Start runs on the first activation and puts the key's string
        // back. The key itself has to change, and UI.Notifications.Diplomacy.TabCPs is already
        // in every shipped language.
        Relabel(tab, TabKey, Loc.T(TabKey));
        Wire(tab, () => ToggleTab(ui, tab, player));
    }

    // One heading per nation, then that nation's control points under it, all closed. Vanilla's
    // rows carry the item's own name; here the nation is on the heading, so a row only needs to
    // say which control point of that nation it is.
    private static int Add(DiplomacyController ui, List<TIControlPoint> points,
        DiplomacyBankListItem tab, GameObject bank, GameObject table, bool player)
    {
        int added = 0;
        if (points == null || table == null || ui.bankItemPrefab == null
            || ui.tableItemPrefab == null)
        {
            return 0;
        }
        foreach (IGrouping<TINationState, TIControlPoint> nation in points
            .Where(Tradeable)
            .GroupBy(point => point.nation)
            .OrderBy(group => group.Key.displayName))
        {
            Group group = Heading(ui, tab, bank, nation.Key, player);
            foreach (TIControlPoint point in nation.OrderBy(x => x.positionInNation))
            {
                TIControlPoint captured = point;

                GameObject tableRow =
                    UnityEngine.Object.Instantiate(ui.tableItemPrefab, table.transform);
                Loc.SwapFonts(tableRow);
                DiplomacyTableListItem tableItem = tableRow.GetComponent<DiplomacyTableListItem>();
                tableItem.itemType = CPItem;
                tableItem.itemDescription.text = captured.displayName;
                tableItem.itemIcon.sprite = captured.GetIcon(true, false);
                tableItem.diplomacyController = ui;
                tableItem.HideOrgData();
                tableItem.tooltipTrigger.enabled = true;
                tableItem.tooltipTrigger.SetDelegate("BodyText", () => captured.displayName);
                tableItem.DisableGameobject();

                GameObject bankRow =
                    UnityEngine.Object.Instantiate(ui.bankItemPrefab, bank.transform);
                Loc.SwapFonts(bankRow);
                DiplomacyBankListItem bankItem = bankRow.GetComponent<DiplomacyBankListItem>();
                bankItem.itemType = CPItem;
                bankItem.quantityText.text = captured.controlPointTypeDisplayName;
                bankItem.itemIcon.sprite = captured.GetIcon(true, false);
                bankItem.diplomacyController = ui;
                bankItem.HideOrgData();
                bankItem.tooltipTrigger.enabled = true;
                bankItem.tooltipTrigger.SetDelegate("BodyText", () => captured.displayName);
                bankItem.tooltipTrigger.tipPosition = TipPosition.MouseLeftMiddle;
                bankItem.dealTableLink = tableRow;
                // Closed until its nation is opened, as vanilla leaves its own rows closed
                // until their tab is.
                bankRow.SetActive(false);

                if (captured.benefitsDisabled)
                {
                    MarkSuppressed(tableItem.itemIcon);
                    MarkSuppressed(bankItem.itemIcon);
                }

                rows[tableRow] = captured;
                banks[captured] = bankItem;
                group?.bankRows.Add(bankRow);
                added++;
            }
            // The same mark on the flag, so a nation holding something suppressed says so while
            // it is closed. One suppressed seat is enough to earn it.
            if (group?.heading != null && nation.Any(point => point.benefitsDisabled))
            {
                MarkSuppressed(group.heading.itemIcon);
            }
        }
        return added;
    }

    // The crackdown decal is an editor-wired Image on the nation screen's control point grid
    // item and exists nowhere else: there is no path for it in the globals and nothing for it in
    // AssetCacheManager, so it cannot be loaded, only borrowed. The nation screen is built with
    // the campaign and keeps its grid items in the scene while they are inactive, which is what
    // FindObjectsOfTypeAll reaches and GameObject.Find would not. Held until it is destroyed
    // with the campaign, which Unity's own == reports as null.
    private static GameObject decal;

    // As a fraction of the icon it sits on, anchored at the corner.
    private const float DecalSize = 0.5f;

    private static GameObject DecalSource()
    {
        if (decal == null)
        {
            foreach (ControlPointGridItemController item in
                Resources.FindObjectsOfTypeAll<ControlPointGridItemController>())
            {
                if (item != null && item.crackdownStatusPanel != null
                    && item.crackdownStatusPanel.sprite != null)
                {
                    decal = item.crackdownStatusPanel.gameObject;
                    break;
                }
            }
        }
        return decal;
    }

    // The decal is cut for a grid cell and lands here on a row icon, so it is anchored rather
    // than left at the size it came with: a quarter of the icon, in the lower left corner.
    private static void MarkSuppressed(Image icon)
    {
        GameObject source = DecalSource();
        if (icon == null || source == null)
        {
            return;
        }
        GameObject mark = UnityEngine.Object.Instantiate(source, icon.transform);
        mark.name = "CataTweaksSuppressed";
        RectTransform rect = mark.transform as RectTransform;
        if (rect != null)
        {
            rect.localScale = Vector3.one;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = new Vector2(DecalSize, DecalSize);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.pivot = Vector2.zero;
        }
        Image image = mark.GetComponent<Image>();
        if (image != null)
        {
            // The source sits on the prefab with its Image disabled until a crackdown turns it
            // on, and a click on a row must still reach the row.
            image.enabled = true;
            image.raycastTarget = false;
        }
        mark.SetActive(true);
    }

    // A clone of the tab, which is the only object to hand that looks like a heading and has a
    // button and a +/- on it. The flag stands in for the tab's icon.
    private static Group Heading(DiplomacyController ui, DiplomacyBankListItem tab, GameObject bank,
        TINationState nation, bool player)
    {
        GameObject clone = UnityEngine.Object.Instantiate(tab.gameObject, bank.transform);
        Loc.SwapFonts(clone);
        DiplomacyBankListItem item = clone.GetComponent<DiplomacyBankListItem>();
        if (item == null)
        {
            UnityEngine.Object.Destroy(clone);
            return null;
        }
        item.itemType = CPGroupItem;
        item.diplomacyController = ui;
        // A heading is not something to put on the table, and a clone arrives with the link its
        // source had.
        item.dealTableLink = null;
        if (item.tabText != null)
        {
            item.tabText.text = "+";
        }
        if (item.itemIcon != null && nation.flag != null)
        {
            item.itemIcon.sprite = nation.flag;
            // The slot was cut for a square tab icon; a flag is not square.
            item.itemIcon.preserveAspect = true;
            item.itemIcon.gameObject.SetActive(true);
        }
        // A nation's name is not a localization key, so the localizer has to go rather than be
        // repointed, or it would overwrite the name on its first frame.
        Relabel(item, null, nation.displayName);
        if (item.tooltipTrigger != null)
        {
            // The clone brings a trigger whose delegate could not survive Instantiate.
            item.tooltipTrigger.enabled = false;
        }
        clone.SetActive(false);
        Group group = new Group { player = player, heading = item };
        Wire(item, () => ToggleGroup(group));
        groups.Add(group);
        return group;
    }

    // UITextLocalizer overwrites the text it sits on from a key, on its first frame and again
    // whenever the language changes. Repointing it at another key is the only way to make a new
    // label stick; where there is no key for the text, the localizer goes.
    private static void Relabel(DiplomacyBankListItem item, string key, string text)
    {
        foreach (UITextLocalizer localizer in item.GetComponentsInChildren<UITextLocalizer>(true))
        {
            if (item.tabText != null && localizer.gameObject == item.tabText.gameObject)
            {
                continue;
            }
            if (key == null)
            {
                UnityEngine.Object.Destroy(localizer);
            }
            else
            {
                localizer.displayText = key;
                localizer.LocalizeText(key);
            }
        }
        foreach (TMP_Text label in item.GetComponentsInChildren<TMP_Text>(true))
        {
            if (item.tabText == null || label != item.tabText)
            {
                label.text = text;
            }
        }
    }

    // A tab button arrives with whatever the editor wired to it, and a clone brings that along:
    // RemoveAllListeners does not clear a persistent listener, and a fresh event does. Setup
    // runs once per negotiation, so re-assigning is idempotent.
    private static void Wire(DiplomacyBankListItem item, UnityEngine.Events.UnityAction action)
    {
        Button button = item.button != null ? item.button : item.GetComponent<Button>();
        if (button == null)
        {
            return;
        }
        button.onClick = new Button.ButtonClickedEvent();
        button.onClick.AddListener(action);
        button.interactable = true;
    }

    // The tab shows and hides the nation headings. Closing it closes every nation with it, so
    // reopening starts from the headings rather than from whatever was left open.
    private static void ToggleTab(DiplomacyController ui, DiplomacyBankListItem tab, bool player)
    {
        try
        {
            bool show = !(player ? playerVisible : aiVisible);
            if (player)
            {
                playerVisible = show;
            }
            else
            {
                aiVisible = show;
            }
            if (tab != null && tab.tabText != null)
            {
                tab.tabText.text = show ? "-" : "+";
            }
            foreach (Group group in groups)
            {
                if (group.player != player)
                {
                    continue;
                }
                if (!show)
                {
                    Open(group, false);
                }
                if (group.heading != null)
                {
                    group.heading.gameObject.SetActive(show);
                }
            }
            Click(show);
        }
        catch (Exception e)
        {
            Main.mod?.Logger.Error("Could not open the control point list - " + e.Message);
        }
    }

    private static void ToggleGroup(Group group)
    {
        try
        {
            Open(group, !group.open);
            Click(group.open);
        }
        catch (Exception e)
        {
            Main.mod?.Logger.Error("Could not open a nation's control points - " + e.Message);
        }
    }

    private static void Open(Group group, bool open)
    {
        group.open = open;
        if (group.heading != null && group.heading.tabText != null)
        {
            group.heading.tabText.text = open ? "-" : "+";
        }
        foreach (GameObject row in group.bankRows)
        {
            if (row != null)
            {
                row.SetActive(open);
            }
        }
    }

    private static void Click(bool opening)
    {
        AudioManager.PlayOneShot(opening
            ? "event:/SFX/UI_SFX/trig_SFX_CycleForward"
            : "event:/SFX/UI_SFX/trig_SFX_CycleBack");
    }

    // Read the deal table back into the offer vanilla has just built. EvaluateTrade clears
    // controlPoints on both offers and never refills them, so this is the only writer.
    private static int Collect(GameObject content, TradeOffer offer)
    {
        int added = 0;
        if (content == null || offer == null || offer.controlPoints == null)
        {
            return 0;
        }
        for (int i = 0; i < content.transform.childCount; i++)
        {
            GameObject row = content.transform.GetChild(i).gameObject;
            TIControlPoint point;
            if (row.activeSelf && rows.TryGetValue(row, out point) && point != null
                && !offer.controlPoints.Contains(point))
            {
                offer.controlPoints.Add(point);
                added++;
            }
        }
        return added;
    }

    // EvaluateTrade reads the table, scores what it found and only then stores the two offers,
    // so by the time a control point joins them the verdict on screen was reached without it.
    // The tail of that method therefore has to be run again here, which is also the moment the
    // AI's answer stops being a foregone conclusion.
    private static readonly FieldInfo hate =
        AccessTools.Field(typeof(DiplomacyController), "hateModifier");

    internal static void Fill(DiplomacyController ui)
    {
        TradeOffer fromPlayer = playerOffer.GetValue(ui) as TradeOffer;
        TradeOffer fromAI = aiOffer.GetValue(ui) as TradeOffer;
        int onTable = Collect(ui.playerTableItemsContent, fromPlayer)
            + Collect(ui.aiTableItemsContent, fromAI);
        TIFactionState player = GameControl.control?.activePlayer;
        // An offer the player has not touched is the AI's own, and vanilla leaves that one
        // acceptable by definition.
        if (onTable == 0 || fromPlayer == null || fromAI == null || player == null
            || ui.tradingFaction == null || !ui.touchedAIOffer)
        {
            return;
        }
        TradeOffer.TradeAgreement agreement = (A: fromPlayer, B: fromAI);
        float favorability;
        bool acceptable = TradeAI.IsAgreementAcceptable(agreement, ui.tradingFaction, player,
            out favorability);
        if (acceptable)
        {
            bool meaningful = TradeAI.ScoreAgreement(agreement, ui.tradingFaction)
                >= TemplateManager.global.meaningfulTradeThreshold;
            bool good = favorability >= 1f
                || favorability - TradeAI.GetMinimumAgreementFavorability(ui.tradingFaction, player)
                    >= TemplateManager.global.goodTradeThreshold;
            Say(ui, (meaningful && good) ? "TradeValueHigh" : "TradeValueEqual");
            hate?.SetValue(ui, (meaningful && good) ? 2f : 1f);
        }
        else
        {
            Say(ui, (favorability != 0f) ? "TradeValueLow" : "TradeValueVeryLow");
        }
        ui.executeTradeButton.interactable = acceptable;
    }

    private static void Say(DiplomacyController ui, string key)
    {
        if (ui.aiFeedbackDialogText != null)
        {
            ui.aiFeedbackDialogText.text = Loc.T("UI.Notifications.Diplomacy." + key);
        }
    }

    // What one control point is worth to one faction, on the scale TradeAI already uses for
    // orgs and habs: six months of the income it carries, valued the way that faction values
    // income, plus what the investment points and the armies behind it are worth. The executive
    // is worth more than a seat because it carries the nation, which is the same call vanilla's
    // own unused EvaluateControlPoint makes.
    //
    // A nation the faction has walked away from is worth a tenth, which is what makes a faction
    // hand its abandoned seats over for almost nothing while the faction across the table, which
    // has not abandoned that nation, still counts them at full value.
    private const float InvestmentPointValue = 100f;

    private const float ArmyValue = 200f;

    private const float ExecutiveMultiplier = 2f;

    private const float AbandonedDiscount = 0.1f;

    // A suppressed seat pays nothing until the suppression runs out, and it runs out on whoever
    // holds it by then, so both sides of the table should want it less.
    private const float SuppressedDiscount = 0.5f;

    // How many abandoned seats a faction will push across the table in one of its own offers.
    private const int MaxDumped = 2;

    internal static float Value(TIControlPoint point, TIFactionState faction)
    {
        if (point == null || point.nation == null || faction == null)
        {
            return 0f;
        }
        TINationState nation = point.nation;
        float value = AIEvaluators.EvaluateMonthlyResourceIncome_Trade(faction, FactionResource.Money,
            nation.GetMonthlyMoneyIncomeFromControlPoint(faction));
        value += AIEvaluators.EvaluateMonthlyResourceIncome_Trade(faction, FactionResource.Research,
            nation.GetMonthlyResearchFromControlPoint(faction));
        value += nation.GetInvestmentFromControlPoint() * InvestmentPointValue;
        value += nation.GetNumArmiesAtControlPoint(point.positionInNation) * ArmyValue;
        value *= point.executive ? ExecutiveMultiplier : 1f;
        // Only the faction that holds the seat gets the discount for having abandoned it. Read
        // for anyone, the auto-abandon toggle would be a way to talk down the value of what the
        // other side is handing over, and it costs nothing to flip.
        if (point.faction == faction && Abandoned(point, faction))
        {
            value *= AbandonedDiscount;
        }
        else if (point.benefitsDisabled)
        {
            value *= SuppressedDiscount;
        }
        return Mathf.Max(0f, value) * Main.settings.controlPointTradeValue;
    }

    // The same shape as vanilla's hab category: what is received counts once, what is given up
    // counts against distrust, and ScoreAgreement weights a negative category three times over.
    internal static float Score(TradeOffer.TradeAgreement agreement, TIFactionState scorer)
    {
        TIFactionState other = agreement.Factions.FirstOrDefault(x => x != scorer);
        TradeOffer mine = agreement.GetOffer(scorer);
        TradeOffer theirs = agreement.GetOtherPartysOffer(scorer);
        if (mine?.controlPoints == null || theirs?.controlPoints == null
            || (mine.controlPoints.Count == 0 && theirs.controlPoints.Count == 0))
        {
            return 0f;
        }
        float received = theirs.controlPoints.Sum(point => Value(point, scorer));
        float given = mine.controlPoints.Sum(point => Value(point, scorer))
            * (0f - Distrust(scorer, other) * 1.09f);
        float category = received + given;
        return (category > 0f) ? category : (3f * category);
    }

    // TradeAI.GetDistrust is private and there is no public stand-in. A permanent ally sits at
    // 1.025 and an ordinary rival somewhere above 1, so that is what the fallback answers with.
    private static readonly MethodInfo distrust =
        AccessTools.Method(typeof(TradeAI), "GetDistrust");

    private static float Distrust(TIFactionState judge, TIFactionState other)
    {
        if (distrust == null || judge == null || other == null)
        {
            return 1.1f;
        }
        return (float)distrust.Invoke(null, new object[] { judge, other });
    }

    // A faction pushes the seats it has given up on across the table in its own offers, which is
    // the only way a control point reaches a table the player did not build: TradeAI cannot be
    // taught a new category, so nothing else would ever put one in an AI offer.
    internal static void Dump(TIFactionState creator, TIFactionState recipient,
        TradeOffer.TradeAgreement agreement)
    {
        if (creator == null || recipient == null || creator.IsAlienFaction
            || recipient.IsAlienFaction || creator.controlPoints == null)
        {
            return;
        }
        TradeOffer offer = agreement.GetOffer(creator);
        if (offer?.controlPoints == null)
        {
            return;
        }
        foreach (TIControlPoint point in creator.controlPoints
            .Where(x => Tradeable(x) && Abandoned(x, creator) && Value(x, recipient) > 0f)
            .OrderByDescending(x => Value(x, recipient))
            .Take(MaxDumped))
        {
            if (!offer.controlPoints.Contains(point))
            {
                offer.controlPoints.Add(point);
            }
        }
    }

    // ChangeControlPointOwner calls EnableBenefits on the way in, which clears a crackdown
    // outright. Left alone, a trade and a trade back would be the cheapest way to shrug one off,
    // so what is being served is written down before the handover and put back after it.
    //
    // The expiry date itself is put back rather than a fresh crackdown of the same length being
    // served: ResolveCrackdownEffect counts in whole months and rounds its expiry on to a
    // mission phase, so a seat with a day left would come out of the trade suppressed for
    // another month and a seat traded twice would come out worse still. Both setters are
    // private, which is what the reflection is for.
    private static readonly MethodInfo setExpiry =
        AccessTools.PropertySetter(typeof(TIControlPoint), "crackdownExpiration");

    private static readonly MethodInfo setDisabled =
        AccessTools.PropertySetter(typeof(TIControlPoint), "benefitsDisabled");

    internal static Dictionary<TIControlPoint, TIDateTime> RecordSuppression(TradeOffer offer)
    {
        Dictionary<TIControlPoint, TIDateTime> until =
            new Dictionary<TIControlPoint, TIDateTime>();
        if (offer?.controlPoints == null)
        {
            return until;
        }
        foreach (TIControlPoint point in offer.controlPoints)
        {
            if (point != null && point.benefitsDisabled && point.crackdownExpiration != null
                && point.crackdownExpiration.DifferenceInDays(TITimeState.Now()) > 0.0)
            {
                until[point] = point.crackdownExpiration;
            }
        }
        return until;
    }

    internal static void RestoreSuppression(TIFactionState owner,
        Dictionary<TIControlPoint, TIDateTime> until)
    {
        if (until == null || owner == null || setExpiry == null || setDisabled == null)
        {
            return;
        }
        foreach (KeyValuePair<TIControlPoint, TIDateTime> entry in until)
        {
            if (entry.Key != null && entry.Key.faction == owner)
            {
                setDisabled.Invoke(entry.Key, new object[] { true });
                setExpiry.Invoke(entry.Key, new object[] { entry.Value });
            }
        }
    }

    // An offer the AI built names its control points; this puts them on the table, since
    // vanilla's PreFillTable only knows how to place orgs, habs and projects.
    internal static void Prefill(TradeOffer offer)
    {
        if (offer?.controlPoints == null)
        {
            return;
        }
        foreach (TIControlPoint point in offer.controlPoints)
        {
            DiplomacyBankListItem row;
            if (banks.TryGetValue(point, out row) && row != null)
            {
                row.AddToTable(1f, playAudio: false);
            }
        }
    }

    // Vanilla's own cleanup only destroys org, hab and project rows, so ours would survive into
    // the next negotiation and offer control points that had already changed hands. The tabs
    // themselves belong to the screen and are only hidden again.
    internal static void Clear(DiplomacyController ui)
    {
        rows.Clear();
        banks.Clear();
        groups.Clear();
        Sweep(ui.playerBankItemsContent);
        Sweep(ui.aiBankItemsContent);
        Sweep(ui.playerTableItemsContent);
        Sweep(ui.aiTableItemsContent);
        Hide(ui.playerCPsTab);
        Hide(ui.aiCPsTab);
    }

    private static void Hide(DiplomacyBankListItem tab)
    {
        if (tab != null)
        {
            tab.gameObject.SetActive(false);
        }
    }

    private static void Sweep(GameObject content)
    {
        if (content == null)
        {
            return;
        }
        for (int i = 0; i < content.transform.childCount; i++)
        {
            GameObject row = content.transform.GetChild(i).gameObject;
            DiplomacyBankListItem bankItem = row.GetComponent<DiplomacyBankListItem>();
            DiplomacyTableListItem tableItem = row.GetComponent<DiplomacyTableListItem>();
            if ((bankItem != null && (bankItem.itemType == CPItem
                    || bankItem.itemType == CPGroupItem))
                || (tableItem != null && tableItem.itemType == CPItem))
            {
                UnityEngine.Object.Destroy(row);
            }
        }
    }
}

[HarmonyPatch(typeof(DiplomacyController), "LoadBankValues")]
internal static class ControlPointTradeRows
{
    private static void Postfix(DiplomacyController __instance)
    {
        try
        {
            if (Main.settings.tradeControlPoints)
            {
                ControlPointTrade.Build(__instance);
            }
        }
        catch (Exception e)
        {
            Main.mod?.Logger.Error("Could not list control points for trade - " + e.Message);
        }
    }
}

// Not gated on the setting: rows built while it was on still have to be cleaned up after it goes
// off. LoadBankValues calls this before it rebuilds, so the map empties before it fills again.
[HarmonyPatch(typeof(DiplomacyController), "CleanupOldTradeItems")]
internal static class ControlPointTradeCleanup
{
    private static void Postfix(DiplomacyController __instance)
    {
        try
        {
            ControlPointTrade.Clear(__instance);
        }
        catch (Exception e)
        {
            Main.mod?.Logger.Error("Could not clear traded control points - " + e.Message);
        }
    }
}

// EvaluateTrade assigns the two offers on its way out, and OnClickTradeButton hands those same
// objects to DiplomacyTradeAction, so adding to them here is all it takes for a trade to carry.
[HarmonyPatch(typeof(DiplomacyController), nameof(DiplomacyController.EvaluateTrade))]
internal static class ControlPointTradeOffer
{
    private static void Postfix(DiplomacyController __instance)
    {
        try
        {
            if (Main.settings.tradeControlPoints)
            {
                ControlPointTrade.Fill(__instance);
            }
        }
        catch (Exception e)
        {
            Main.mod?.Logger.Error("Could not add control points to a trade - " + e.Message);
        }
    }
}

// A suppressed seat keeps its suppression when it changes hands. Not gated on the setting: if a
// control point is in an offer at all it got there through this mod, and the suppression should
// follow it whatever the setting says by the time the action runs.
[HarmonyPatch(typeof(TIFactionState), nameof(TIFactionState.ProcessTrade))]
internal static class ControlPointTradeSuppression
{
    private static void Prefix(TradeOffer acceptedOffer,
        out Dictionary<TIControlPoint, TIDateTime> __state)
    {
        __state = null;
        try
        {
            __state = ControlPointTrade.RecordSuppression(acceptedOffer);
        }
        catch (Exception e)
        {
            Main.mod?.Logger.Error("Could not read a traded crackdown - " + e.Message);
        }
    }

    private static void Postfix(TIFactionState __instance,
        Dictionary<TIControlPoint, TIDateTime> __state)
    {
        try
        {
            ControlPointTrade.RestoreSuppression(__instance, __state);
        }
        catch (Exception e)
        {
            Main.mod?.Logger.Error("Could not carry a crackdown over a trade - " + e.Message);
        }
    }
}

// TradeAI scores an agreement one category at a time and its categories are a private enum, so
// the control points in an offer are worth nothing to it. This is the category it never had.
[HarmonyPatch(typeof(TradeAI), nameof(TradeAI.ScoreAgreement))]
internal static class ControlPointTradeValue
{
    private static void Postfix(TradeOffer.TradeAgreement agreement, TIFactionState scorer,
        ref float __result)
    {
        try
        {
            if (Main.settings.tradeControlPoints)
            {
                __result += ControlPointTrade.Score(agreement, scorer);
            }
        }
        catch (Exception e)
        {
            Main.mod?.Logger.Error("Could not value control points in a trade - " + e.Message);
        }
    }
}

// The AI's own offers, where it pushes the seats it has abandoned.
[HarmonyPatch(typeof(TradeAI), nameof(TradeAI.CreateTradeAgreement))]
internal static class ControlPointTradeDump
{
    private static void Postfix(TIFactionState agreementCreator, TIFactionState agreementRecipient,
        ref TradeOffer.TradeAgreement __result)
    {
        try
        {
            if (Main.settings.tradeControlPoints)
            {
                ControlPointTrade.Dump(agreementCreator, agreementRecipient, __result);
            }
        }
        catch (Exception e)
        {
            Main.mod?.Logger.Error("Could not offer abandoned control points - " + e.Message);
        }
    }
}

// PreFillTable places an offer's orgs, habs and projects on the table and knows nothing of
// control points, so an offer carrying one would arrive invisible and be dropped by the next
// read of the table.
[HarmonyPatch(typeof(DiplomacyController), nameof(DiplomacyController.PreFillTable))]
internal static class ControlPointTradePrefill
{
    private static void Postfix(TradeOffer aiOffer, TradeOffer playerOffer)
    {
        try
        {
            if (Main.settings.tradeControlPoints)
            {
                ControlPointTrade.Prefill(aiOffer);
                ControlPointTrade.Prefill(playerOffer);
            }
        }
        catch (Exception e)
        {
            Main.mod?.Logger.Error("Could not place offered control points - " + e.Message);
        }
    }
}

// Tweak 14: purging a suppressed seat does not break a pact, and a mission that would break one
// says so on the target it is pointed at.
//
// A pact is not a rule the game enforces. A non-aggression pact is a faction goal, and what ends
// it is hate: FactionGoal_NonAggressionPact checks each day whether the other side's hate has
// risen since yesterday and runs BreakPactAction when it has, and the hate comes from the hate
// array on the mission template, handed out in ResolveMission. So to leave a pact standing there
// is nothing to suppress but that one gain of hate, from the seat's owner toward whoever purged
// it, and only while that purge resolves.
//
// The warning is a gap rather than an invention. FillOutTargetDropdown already marks a target
// whose faction has a pact with an inline sprite, and SetDropdownCaption then rewrites the line
// for the selected target from scratch, without it. The mark is therefore visible while the list
// is open and gone the moment a target is picked, which is when it matters.
internal static class FriendlyPurge
{
    private static TIFactionState victim;

    private static TIFactionState attacker;

    // The pact this mission would break, or null: what is about to take hate, from a faction
    // that has a pact with the one sending the councilor.
    internal static TIFactionState Pact(TIMissionTemplate template, TIGameState target,
        TIFactionState actor)
    {
        if (template == null || target == null || actor == null || template.hate == null
            || !template.hate.Any(x => x > 0f) || Exempt(template, target, actor))
        {
            return null;
        }
        List<TIFactionState> harmed = target.ref_factions ?? new List<TIFactionState>();
        if (target.ref_faction != null && !harmed.Contains(target.ref_faction))
        {
            harmed = harmed.Concat(new[] { target.ref_faction }).ToList();
        }
        return harmed.FirstOrDefault(x => x != null && x != actor
            && (x.HasNAP(actor) || x.HasTruce(actor)));
    }

    // Purging a seat that is already suppressed, from a faction we have a pact with. Without a
    // pact there is nothing to violate and the hate stands as vanilla wrote it.
    internal static bool Exempt(TIMissionTemplate template, TIGameState target, TIFactionState actor)
    {
        if (!Main.settings.friendlyPurge || template == null || actor == null
            || template != TIFactionState.purgeMission)
        {
            return false;
        }
        TIControlPoint point = target?.ref_controlPoint;
        return point != null && point.benefitsDisabled && point.faction != null
            && point.faction != actor
            && (point.faction.HasNAP(actor) || point.faction.HasTruce(actor));
    }

    // Purging aborts the other councilors aimed at the same seat, and aborting resolves their
    // missions, so a resolution can run inside a resolution. Each one puts back what it found.
    internal static TIFactionState[] Begin(TIMissionState mission)
    {
        TIFactionState[] previous = new TIFactionState[2] { victim, attacker };
        TICouncilorState councilor = mission?.councilor;
        if (councilor?.faction != null
            && Exempt(mission.missionTemplate, mission.target, councilor.faction))
        {
            victim = mission.target.ref_controlPoint.faction;
            attacker = councilor.faction;
        }
        else
        {
            victim = null;
            attacker = null;
        }
        return previous;
    }

    internal static void End(TIFactionState[] previous)
    {
        if (previous != null && previous.Length == 2)
        {
            victim = previous[0];
            attacker = previous[1];
        }
    }

    internal static bool Swallow(TIFactionState gaining, TIFactionState towards)
    {
        return victim != null && gaining == victim && towards == attacker;
    }
}

[HarmonyPatch(typeof(TIMissionState), nameof(TIMissionState.ResolveMission))]
internal static class FriendlyPurgeResolve
{
    private static void Prefix(TIMissionState __instance, out TIFactionState[] __state)
    {
        __state = null;
        try
        {
            __state = FriendlyPurge.Begin(__instance);
        }
        catch (Exception e)
        {
            Main.mod?.Logger.Error("Could not check a purge against a pact - " + e.Message);
        }
    }

    // A finalizer rather than a postfix: it runs even when the resolution throws, and hate held
    // back is global while it is held, so a leak would go on swallowing hate between those two
    // factions for the rest of the campaign. The exception is handed back untouched.
    private static Exception Finalizer(TIFactionState[] __state, Exception __exception)
    {
        FriendlyPurge.End(__state);
        return __exception;
    }
}

[HarmonyPatch(typeof(TIFactionState), nameof(TIFactionState.GainFactionHate))]
internal static class FriendlyPurgeHate
{
    private static bool Prefix(TIFactionState __instance, TIFactionState enemyCouncil)
    {
        try
        {
            return !FriendlyPurge.Swallow(__instance, enemyCouncil);
        }
        catch (Exception e)
        {
            Main.mod?.Logger.Error("Could not hold back hate for a purge - " + e.Message);
            return true;
        }
    }
}

[HarmonyPatch(typeof(CouncilorMissionCanvasController), "SetDropdownCaption")]
internal static class PactWarningOnTarget
{
    private static readonly FieldInfo target =
        AccessTools.Field(typeof(CouncilorMissionCanvasController), "currentTarget");

    private static readonly MethodInfo mission =
        AccessTools.PropertyGetter(typeof(CouncilorMissionCanvasController), "missionTemplate");

    private static void Postfix(CouncilorMissionCanvasController __instance)
    {
        try
        {
            if (!Main.settings.warnOnPactBreak || target == null || mission == null
                || __instance.targetDropdown?.captionText == null)
            {
                return;
            }
            TIFactionState pact = FriendlyPurge.Pact(
                mission.Invoke(__instance, null) as TIMissionTemplate,
                target.GetValue(__instance) as TIGameState,
                GameControl.control?.activePlayer);
            if (pact == null)
            {
                return;
            }
            // The mark alone, prefixed. A contested mission's caption ends in UI.MissionPhase.ToHit,
            // which is "<rcol>{0}</rcol>" - a right-column tag - so anything appended after it is
            // laid out in that column and prints on top of the name. Which pact it is, and what
            // breaking it would cost, is the confirmation's job anyway.
            __instance.targetDropdown.captionText.SetText(
                TemplateManager.global.warningInlineSpritePath + " "
                + __instance.targetDropdown.captionText.text);
        }
        catch (Exception e)
        {
            Main.mod?.Logger.Error("Could not warn about a pact - " + e.Message);
        }
    }
}

// The confirmation itself. The screen already owns a yes/no panel - the one that asks whether to
// end the phase with councilors still idle - so it is borrowed rather than built: its two labels
// are rewritten for as long as the question is up and put back when it closes.
//
// The answer is taken at the two methods its buttons call - OnConfirmGoForwardClicked and
// OnDeclineGoForwardClicked, wired to them in the editor - rather than by pointing the buttons
// somewhere else. The panel sits inactive until it is asked for, and an inactive object has no
// components to reach through, so there is nothing there to point anywhere until it is already up.
[HarmonyPatch(typeof(CouncilorMissionCanvasController),
    nameof(CouncilorMissionCanvasController.OnConfirmMissionClick))]
internal static class PactConfirm
{
    private static readonly FieldInfo target =
        AccessTools.Field(typeof(CouncilorMissionCanvasController), "currentTarget");

    private static readonly MethodInfo mission =
        AccessTools.PropertyGetter(typeof(CouncilorMissionCanvasController), "missionTemplate");

    private static bool answered;

    // Which target the question is about, and, by being set at all, that one is up.
    private static TIGameState asked;

    private static string promptWas;

    private static bool Prefix(CouncilorMissionCanvasController __instance)
    {
        try
        {
            if (answered)
            {
                answered = false;
                return true;
            }
            if (!Main.settings.warnOnPactBreak || target == null || mission == null
                || __instance.unassignedWarningPanel == null)
            {
                return true;
            }
            TIFactionState pact = FriendlyPurge.Pact(
                mission.Invoke(__instance, null) as TIMissionTemplate,
                target.GetValue(__instance) as TIGameState,
                GameControl.control?.activePlayer);
            if (pact == null)
            {
                return true;
            }
            Ask(__instance, pact);
            return false;
        }
        catch (Exception e)
        {
            Main.mod?.Logger.Error("Could not ask about a pact - " + e.Message);
            return true;
        }
    }

    private static void Ask(CouncilorMissionCanvasController ui, TIFactionState pact)
    {
        // The panel is only modal by convention, so it can be left up and walked away from -
        // closing the screen with it open, say. Whatever was borrowed last time goes back before
        // anything is borrowed again, or the second ask would save our own lines as the originals
        // and vanilla would never get its prompt back.
        if (asked != null)
        {
            Close(ui);
        }
        // Vanilla still cycles the target behind the panel - Tab runs CycleTargetForward whether
        // this is up or not - so an answer only stands for the target it was asked about.
        asked = target.GetValue(ui) as TIGameState;
        promptWas = ui.unassignedWarningPrompt != null ? ui.unassignedWarningPrompt.text : null;

        // The header is left as vanilla wrote it - "Warning!" - because the panel carries two
        // warning icons either side of it, and a header long enough to say anything runs under
        // them. The faction is named in the prompt instead, in its own colour.
        //
        // Our own key, in all fourteen: nothing the game ships says that a pact is only at risk,
        // and the game has no English fallback - a missing key prints itself at the player.
        ui.unassignedWarningPrompt?.SetText(Loc.T(
            pact.HasNAP(GameControl.control.activePlayer)
                ? "UI.CataTweaks.PactWarning_NAP"
                : "UI.CataTweaks.PactWarning_Truce",
            pact.displayNameWithColor));

        AudioManager.PlayOneShot("event:/SFX/UI_SFX/trig_SFX_BadUI");
        ui.unassignedWarningPanel.SetActive(value: true);
    }

    // A click on one of the two buttons. True means the question was ours and vanilla's own
    // handler - which would finalize the whole mission phase - does not run.
    internal static bool Answer(CouncilorMissionCanvasController ui, bool yes)
    {
        try
        {
            if (asked == null || ui == null || ui.unassignedWarningPanel == null)
            {
                return false;
            }
            TIGameState was = asked;
            TIGameState now = target.GetValue(ui) as TIGameState;
            Close(ui);
            if (!yes)
            {
                AudioManager.PlayOneShot("event:/SFX/UI_SFX/trig_SFX_Decline");
                return true;
            }
            // A target that moved while the question was up goes back through the check rather
            // than through the answer, which is to say it gets asked about in its own right.
            answered = ReferenceEquals(now, was);
            ui.OnConfirmMissionClick();
            return true;
        }
        catch (Exception e)
        {
            Main.mod?.Logger.Error("Could not answer about a pact - " + e.Message);
            return false;
        }
    }

    // Vanilla writes that label once when the screen is built, so it has to go back as it was or
    // its own prompt would ask about a pact the next time it opens.
    private static void Close(CouncilorMissionCanvasController ui)
    {
        if (promptWas != null)
        {
            ui.unassignedWarningPrompt?.SetText(promptWas);
        }
        promptWas = null;
        asked = null;
        ui.unassignedWarningPanel.SetActive(value: false);
    }
}

[HarmonyPatch(typeof(CouncilorMissionCanvasController),
    nameof(CouncilorMissionCanvasController.OnConfirmGoForwardClicked))]
internal static class PactConfirmYes
{
    private static bool Prefix(CouncilorMissionCanvasController __instance)
    {
        return !PactConfirm.Answer(__instance, yes: true);
    }
}

[HarmonyPatch(typeof(CouncilorMissionCanvasController),
    nameof(CouncilorMissionCanvasController.OnDeclineGoForwardClicked))]
internal static class PactConfirmNo
{
    private static bool Prefix(CouncilorMissionCanvasController __instance)
    {
        return !PactConfirm.Answer(__instance, yes: false);
    }
}

// Tweak 15: the nation panel's name becomes a dropdown of the nations we hold a control point
// in, with a previous and a next arrow either side, stepping through them by name and wrapping.
//
// The layout is the map mode picker's, lifted whole: the finder's mapModeDropdown sits between
// two arrow buttons under one parent, and that parent is cloned into the nation header in the
// name label's place. Everything in the clone the editor wired - the dropdown's change handler,
// the arrows' clicks - is replaced with a fresh event, since RemoveAllListeners leaves a
// persistent listener where it is. Navigation is the game's own path: GotoGameState fires
// RegionStateSelected for the capital, ShowNationPanel listens for that, and the panel refreshes
// itself, which is also what refills the dropdown.
internal static class NationCycle
{
    private const string PickerName = "CataTweaksNationPicker";

    private static GameObject picker;

    private static TMP_Dropdown dropdown;

    private static Button left;

    private static Button right;

    // Set while the dropdown is being refilled, so a value written by us is not read as a pick.
    private static bool filling;

    private static List<TINationState> shown = new List<TINationState>();

    // Every nation we hold a seat in, in name order. Re-read each time rather than kept: seats
    // come and go, and there is nothing to keep in sync.
    private static List<TINationState> Ours()
    {
        TIFactionState player = GameControl.control?.activePlayer;
        if (player?.controlPoints == null)
        {
            return new List<TINationState>();
        }
        return player.controlPoints
            .Select(point => point?.nation)
            .Where(nation => nation != null && nation.extant)
            .Distinct()
            .OrderBy(nation => nation.displayName)
            .ToList();
    }

    private static void Go(TINationState nation, NationInfoController ui)
    {
        if (nation != null && nation != ui.nation)
        {
            AudioManager.PlayOneShot("event:/SFX/UI_SFX/trig_SFX_CycleForward");
            TIUtilities.GotoGameState(nation);
        }
    }

    // From a nation we hold nothing in, next is the first of ours and previous the last.
    private static void Step(NationInfoController ui, int direction)
    {
        List<TINationState> ours = Ours();
        if (ours.Count == 0)
        {
            return;
        }
        int at = ours.IndexOf(ui.nation);
        int to = at < 0
            ? (direction > 0 ? 0 : ours.Count - 1)
            : (at + direction + ours.Count) % ours.Count;
        Go(ours[to], ui);
    }

    // The label the collapsed dropdown writes its value to: one inside the dropdown for
    // preference, otherwise any other in the clone, never the item template's own label (that
    // one is the pattern the open list is stamped from) and never an arrow's.
    private static TMP_Text Caption()
    {
        return picker.GetComponentsInChildren<TMP_Text>(true)
            .Where(label => label != dropdown.itemText
                && (dropdown.template == null || !label.transform.IsChildOf(dropdown.template))
                && !label.transform.IsChildOf(left.transform)
                && !label.transform.IsChildOf(right.transform))
            .OrderByDescending(label => label.transform.IsChildOf(dropdown.transform))
            .FirstOrDefault();
    }

    // Built once, the first time the panel is filled. Unity's == reports a destroyed picker as
    // null, so a panel rebuilt between campaigns gets a fresh one.
    internal static void Ensure(NationInfoController ui)
    {
        if (picker != null)
        {
            return;
        }
        TMP_Text name = ui.nationNameText;
        TMP_Dropdown source = GeneralControlsController.Singleton?.mapModeDropdown;
        if (name == null || name.transform.parent == null || source == null
            || source.transform.parent == null)
        {
            return;
        }
        Transform header = name.transform.parent;
        Transform old = header.Find(PickerName);
        if (old != null)
        {
            UnityEngine.Object.Destroy(old.gameObject);
        }
        picker = UnityEngine.Object.Instantiate(source.transform.parent.gameObject, header);
        picker.name = PickerName;
        Loc.SwapFonts(picker);
        dropdown = picker.GetComponentInChildren<TMP_Dropdown>(true);
        // The arrows are the buttons that are not the dropdown's own, in the order they sit:
        // the one before the dropdown steps back, the one after steps forward.
        List<Button> arrows = picker.GetComponentsInChildren<Button>(true)
            .Where(button => dropdown == null || !button.transform.IsChildOf(dropdown.transform))
            .OrderBy(button => button.transform.GetSiblingIndex())
            .ToList();
        if (dropdown == null || arrows.Count < 2)
        {
            UnityEngine.Object.Destroy(picker);
            picker = null;
            return;
        }
        left = arrows.First();
        right = arrows.Last();
        // Anything else that shares the map picker's parent is not part of the layout wanted.
        foreach (Transform child in picker.transform)
        {
            bool keep = child == dropdown.transform || child == left.transform
                || child == right.transform || child.GetComponentInChildren<Button>(true) == left
                || child.GetComponentInChildren<Button>(true) == right
                || child.GetComponentInChildren<TMP_Dropdown>(true) == dropdown;
            child.gameObject.SetActive(keep);
        }
        // The collapsed dropdown shows nothing but its caption label, and the map picker keeps
        // that label beside the dropdown rather than inside it - so the sweep just above had
        // hidden it, and a caption wired to something outside the cloned parent would still
        // point at the original anyway, writing our nation's name onto the map mode picker. Take
        // a label from inside the clone, and make sure it is on.
        if (dropdown.captionText == null
            || !dropdown.captionText.transform.IsChildOf(picker.transform))
        {
            dropdown.captionText = Caption();
        }
        if (dropdown.captionText != null)
        {
            dropdown.captionText.gameObject.SetActive(true);
            for (Transform up = dropdown.captionText.transform; up != null && up != picker.transform;
                up = up.parent)
            {
                up.gameObject.SetActive(true);
            }
        }
        foreach (UITextLocalizer localizer in picker.GetComponentsInChildren<UITextLocalizer>(true))
        {
            UnityEngine.Object.Destroy(localizer);
        }
        foreach (TooltipTrigger tip in picker.GetComponentsInChildren<TooltipTrigger>(true))
        {
            tip.enabled = false;
        }
        left.onClick = new Button.ButtonClickedEvent();
        left.onClick.AddListener(() => Step(ui, -1));
        right.onClick = new Button.ButtonClickedEvent();
        right.onClick.AddListener(() => Step(ui, 1));
        dropdown.onValueChanged = new TMP_Dropdown.DropdownEvent();
        dropdown.onValueChanged.AddListener(index =>
        {
            if (!filling && index >= 0 && index < shown.Count)
            {
                Go(shown[index], ui);
            }
        });
        RectTransform slot = name.transform as RectTransform;
        RectTransform rect = picker.transform as RectTransform;
        if (slot != null && rect != null)
        {
            rect.SetSiblingIndex(slot.GetSiblingIndex());
            rect.localScale = Vector3.one;
        }
        name.gameObject.SetActive(false);
        picker.SetActive(true);
        Place(ui);
    }

    // Where the picker's own panel starts, measured from the globe beside the flag. It moves
    // the panel and everything in it together, so it sets where the assembly sits in the row and
    // nothing about the spacing inside it.
    private const float FlagGap = 4f;

    // Between an arrow and the dropdown.
    private const float ArrowGap = 6f;

    // Between an arrow and the end of the panel it sits in - the dark ground behind the whole
    // assembly. This is the margin that reads as breathing room at the arrow's point, because
    // the point is the part nearest the end; the panel's own edges do not move with it.
    private const float EdgeInset = 8f;

    private static readonly Vector3[] corners = new Vector3[4];

    // The picker takes the name's slot and is then pulled left until its back arrow sits against
    // the flag, with the arrows at the two ends and the dropdown filling what is between them.
    //
    // Re-done on every refresh rather than once: the slot is not measurable until the header has
    // been laid out, and the panel resizes with the window, so reading it again each time is
    // both simpler than waiting for a first valid frame and correct afterwards. Everything is
    // computed from the slot rather than from where the picker is now, so it does not drift.
    private static void Place(NationInfoController ui)
    {
        RectTransform rect = picker.transform as RectTransform;
        RectTransform slot = ui.nationNameText?.transform as RectTransform;
        if (rect == null || slot == null || rect.parent == null)
        {
            return;
        }
        rect.anchorMin = slot.anchorMin;
        rect.anchorMax = slot.anchorMax;
        rect.pivot = slot.pivot;
        rect.anchoredPosition = slot.anchoredPosition;
        rect.sizeDelta = slot.sizeDelta;
        float flag = LeftReach(rect);
        if (!float.IsNegativeInfinity(flag))
        {
            // Either way, not only outward: the gap is measured from the flags, so the left edge
            // goes wherever that puts it and the width takes up the difference, leaving the
            // right edge where it was.
            float shift = flag + FlagGap - Edge(rect, rect.parent, false);
            rect.anchoredPosition += new Vector2(shift * (1f - rect.pivot.x), 0f);
            rect.sizeDelta -= new Vector2(shift, 0f);
        }
        // And out to the right as far as the next thing along the header. The name's slot stops
        // well short of the buttons on the end of the row, and stopping where it stopped left
        // the forward arrow stranded in the middle of a gap.
        RectTransform next = Neighbour(rect);
        if (next != null)
        {
            float reach = Edge(next, rect.parent, false) - Edge(rect, rect.parent, true);
            if (reach > 0f)
            {
                rect.anchoredPosition += new Vector2(reach * rect.pivot.x, 0f);
                rect.sizeDelta += new Vector2(reach, 0f);
            }
        }
        Layout(rect);
    }

    // Where the picker starts: the right edge of whatever the header has immediately to its
    // left. Naming the flag was not enough - the globe beside it is a separate object, and that
    // is the one the arrow ends up against - so nothing is named here. Anything that reaches
    // past our own right edge is a background or a neighbour on the other side, not something
    // we are sitting next to, and anything inside the picker is ours.
    private static float LeftReach(RectTransform rect)
    {
        float mine = Edge(rect, rect.parent, false);
        float right = Edge(rect, rect.parent, true);
        float at = float.NegativeInfinity;
        foreach (RectTransform other in rect.parent.GetComponentsInChildren<RectTransform>())
        {
            if (other == rect || other == rect.parent || other.IsChildOf(rect))
            {
                continue;
            }
            float edge = Edge(other, rect.parent, true);
            if (edge <= right && edge > at && Edge(other, rect.parent, false) < mine)
            {
                at = edge;
            }
        }
        return at;
    }

    // The nearest thing to the right of the picker in the header, which is where the picker
    // stops. The name is inactive by now and the flag is behind us, so what is left is the row
    // of buttons on the end.
    private static RectTransform Neighbour(RectTransform rect)
    {
        float from = Edge(rect, rect.parent, true);
        RectTransform nearest = null;
        float at = float.MaxValue;
        foreach (Transform child in rect.parent)
        {
            RectTransform other = child as RectTransform;
            if (other == null || other == rect || !child.gameObject.activeSelf)
            {
                continue;
            }
            float left = Edge(other, rect.parent, false);
            if (left >= from && left < at)
            {
                at = left;
                nearest = other;
            }
        }
        return nearest;
    }

    private static float Edge(RectTransform of, Transform space, bool right)
    {
        of.GetWorldCorners(corners);
        return space.InverseTransformPoint(right ? corners[2] : corners[0]).x;
    }

    // The arrows against the two ends, the dropdown across the middle. The map picker's own
    // layout group would undo all of it, so it is switched off rather than worked around.
    private static void Layout(RectTransform rect)
    {
        RectTransform back = Slot(left.transform);
        RectTransform forward = Slot(right.transform);
        RectTransform list = Slot(dropdown.transform);
        if (back == null || forward == null || list == null)
        {
            return;
        }
        foreach (LayoutGroup group in picker.GetComponents<LayoutGroup>())
        {
            group.enabled = false;
        }
        float height = list.rect.height;
        End(back, 0f, EdgeInset);
        End(forward, 1f, 0f - EdgeInset);
        float before = back.rect.width + EdgeInset + ArrowGap;
        float after = forward.rect.width + EdgeInset + ArrowGap;
        list.anchorMin = new Vector2(0f, 0.5f);
        list.anchorMax = new Vector2(1f, 0.5f);
        list.pivot = new Vector2(0.5f, 0.5f);
        list.sizeDelta = new Vector2(0f - (before + after), height);
        list.anchoredPosition = new Vector2((before - after) / 2f, 0f);
    }

    // The arrows and the dropdown may each sit inside a wrapper of their own, and it is the
    // wrapper that has to move.
    private static RectTransform Slot(Transform inner)
    {
        Transform at = inner;
        while (at != null && at.parent != picker.transform)
        {
            at = at.parent;
        }
        return at as RectTransform;
    }

    private static void End(RectTransform arrow, float edge, float inset)
    {
        Vector2 size = arrow.rect.size;
        arrow.anchorMin = new Vector2(edge, 0.5f);
        arrow.anchorMax = new Vector2(edge, 0.5f);
        arrow.pivot = new Vector2(edge, 0.5f);
        arrow.sizeDelta = size;
        arrow.anchoredPosition = new Vector2(inset, 0f);
    }

    internal static void Show(NationInfoController ui, bool on)
    {
        if (picker != null)
        {
            picker.SetActive(on);
        }
        if (ui.nationNameText != null)
        {
            ui.nationNameText.gameObject.SetActive(!on || picker == null);
        }
    }

    // The dropdown holds our nations, with the one on screen selected. A nation we hold nothing
    // in is not in the list, so it is shown as an extra entry at the top that picking does
    // nothing with; the arrows still lead into the list from it.
    internal static void Refresh(NationInfoController ui)
    {
        if (picker == null || dropdown == null)
        {
            return;
        }
        shown = Ours();
        int current = shown.IndexOf(ui.nation);
        if (current < 0 && ui.nation != null)
        {
            shown.Insert(0, ui.nation);
            current = 0;
        }
        filling = true;
        try
        {
            dropdown.ClearOptions();
            dropdown.AddOptions(shown.Select(nation => nation.displayName).ToList());
            dropdown.SetValueWithoutNotify(Mathf.Max(0, current));
            dropdown.RefreshShownValue();
        }
        finally
        {
            filling = false;
        }
        bool somewhere = shown.Count(nation => nation != ui.nation) > 0;
        left.interactable = somewhere;
        right.interactable = somewhere;
        Place(ui);
    }
}

[HarmonyPatch(typeof(NationInfoController), "UpdatePrimaryDisplayElements")]
internal static class NationCycleButtons
{
    private static void Postfix(NationInfoController __instance)
    {
        try
        {
            if (!Main.settings.nationCycleButtons)
            {
                NationCycle.Show(__instance, false);
                return;
            }
            NationCycle.Ensure(__instance);
            NationCycle.Show(__instance, true);
            NationCycle.Refresh(__instance);
        }
        catch (Exception e)
        {
            Main.mod?.Logger.Error("Could not place the nation picker - " + e.Message);
        }
    }
}

// Tweak 16: launch facilities get built at the best site the nation has, and the region picker's
// dropped filters are put back.
//
// TINationState.OnBoostPriorityComplete chooses the region a completed Launch Facilities priority
// builds in, and that region's latitude alone decides what it pays: BoostIncrease is
// (boostPriorityIncreaseAtEquator - |boostLatitude| / boostLatitudeDivisor) * spaceResourceToTons,
// which on stock globals is (4 - |lat| / 25) * 0.1 a year - ten times as much at the equator as at
// a pole. boostLatitude is authored per region as its best launch latitude, not its centroid.
//
// Vanilla has two paths through that choice and both are bent:
//
//   - With no launch capacity anywhere, it narrows the regions to the ones not under occupation
//     and then throws that list away: the next line re-filters from the full list instead of from
//     the narrowed one, so the filter never reaches the result. GrantSpaceFlightProgram carries
//     the same copy-paste for a nation's first pad.
//   - With capacity somewhere, it weights a random roll by boostPerMonth * 500000 plus
//     sqrt(90.1 - |boostLatitude|), over every region - the unoccupied list it builds first is
//     never read at all. The latitude term spans 9.5 at the equator to 0.3 at a pole; a single
//     completed priority is worth about 16000 of the same units. So latitude decides nothing, the
//     roll goes back to whichever region got there first, and that region may be one an enemy is
//     sitting on.
//
// The weighting is kept and the best site mixed into it: launchSiteFocus is the share of
// completions that skip the roll and build at the best site, and the rest roll as vanilla does.
// 0% is therefore vanilla's own distribution over a correctly filtered list, and 100% always
// builds at the best site.
internal static class LaunchSites
{
    internal static bool Enabled => Main.settings.betterLaunchSites;

    // Vanilla's filter, this time applied. Falling back to the full list when nothing qualifies
    // is vanilla's idiom too: a nation occupied everywhere still has to build somewhere. Nothing
    // downstream writes to the list, so the fallback hands back the nation's own.
    internal static List<TIRegionState> Candidates(TINationState nation)
    {
        List<TIRegionState> open = nation.regions
            .Where(region => region.NoOccupationUnderwayOrComplete()).ToList();
        return open.Count > 0 ? open : nation.regions;
    }

    // Vanilla's own tie-breaking chain, lifted from the no-capacity path: nearest the equator,
    // then coastal, then not a colony, then easternmost. Vanilla walks it as four filters; sorted
    // by the same four keys it is the same answer, since false sorts before true.
    internal static TIRegionState BestSite(List<TIRegionState> candidates) =>
        candidates
            .OrderBy(region => Mathf.Abs(region.boostLatitude))
            .ThenByDescending(region => region.isCoastal)
            .ThenBy(region => region.colonyRegion)
            .ThenByDescending(region => region.longitude)
            .FirstOrDefault();

    // Whether the nation has anywhere to add to, which is what decides between a roll and a
    // certainty. The picker and the tooltip have to agree on it or the tooltip offers a range
    // that nothing can produce.
    internal static bool Launching(List<TIRegionState> candidates) =>
        candidates.Any(region => region.boostPerYear_dekatons > 0f);

    // One end of the priority tooltip's range: what a completed priority pays at the best, or at
    // the worst, of the eligible regions. Sometimes there is no worst - at full focus, and for a
    // nation with nothing to add to, every completion goes to the best site - and the range then
    // collapses to the single figure vanilla already prints when its two ends agree. The return
    // is Harmony's own "run the original", so the patches below are one line each.
    internal static bool Gain(TINationState nation, bool best, ref float gain)
    {
        if (!Enabled || nation.regions.Count == 0)
        {
            return true;
        }
        List<TIRegionState> candidates = Candidates(nation);
        bool equatorial = best || Main.settings.launchSiteFocus >= 1f || !Launching(candidates);
        gain = nation.BoostIncrease(equatorial
            ? candidates.Min(region => Mathf.Abs(region.boostLatitude))
            : candidates.Max(region => Mathf.Abs(region.boostLatitude)));
        return false;
    }
}

[HarmonyPatch(typeof(TINationState), "OnBoostPriorityComplete")]
internal static class LaunchSitePicker
{
    private static bool Prefix(TINationState __instance)
    {
        if (!LaunchSites.Enabled)
        {
            return true;
        }
        List<TIRegionState> candidates = LaunchSites.Candidates(__instance);
        if (candidates.Count == 0)
        {
            // A nation with no regions left. Vanilla throws here; there is nothing to build.
            return false;
        }
        TIRegionState region = Pick(candidates);
        region.ChangeSpaceFacilityValue(SpaceFacilityType.launchFacility,
            __instance.BoostIncrease(region.boostLatitude));
        return false;
    }

    private static TIRegionState Pick(List<TIRegionState> candidates)
    {
        // Nothing to add to, or the focus roll came up: build at the best site. Vanilla takes its
        // own deterministic path on the same first condition.
        if (!LaunchSites.Launching(candidates)
            || TIUtilities.RandomFloatValue() < Main.settings.launchSiteFocus)
        {
            return LaunchSites.BestSite(candidates);
        }
        // Vanilla's weighting verbatim, over the regions that are actually eligible.
        return candidates.SelectRandomWeightedItem(region =>
            region.boostPerMonth_dekatons * 500000f
            + Mathf.Sqrt(90.1f - Mathf.Abs(region.boostLatitude)));
    }
}

// A nation's first pad, from GrantSpaceFlightProgram, where the same filter is dropped the same
// way. The pad is moved afterwards rather than the method replaced, because the pick is only the
// first half of it: the rest moves every control point's spaceflight priority onto Launch
// Facilities and Mission Control, tells the federation, and files the notification. That is
// fifteen lines of vanilla to keep in step forever for the sake of one region.
//
// __state is what every region produced before the call, so the pad is found by whichever region
// went up. Vanilla's pick is deterministic and could be recomputed instead, but that would make
// BestSite stand for two things at once - this mod's choice of site and a model of vanilla's -
// and the day they stopped agreeing the boost would be taken off a region that never got it,
// where ChangeSpaceFacilityValue's clamp at zero would swallow the difference in silence. A
// nation that already had a program returns early and builds nothing, so there is nothing to find.
[HarmonyPatch(typeof(TINationState), "GrantSpaceFlightProgram")]
internal static class FirstLaunchSite
{
    private static void Prefix(TINationState __instance, out float[] __state) =>
        __state = __instance.spaceFlightProgram || !LaunchSites.Enabled
            ? null
            : __instance.regions.Select(region => region.boostPerYear_dekatons).ToArray();

    private static void Postfix(TINationState __instance, float[] __state)
    {
        if (__state == null)
        {
            return;
        }
        List<TIRegionState> regions = __instance.regions;
        TIRegionState built = null;
        float pad = 0f;
        for (int i = 0; i < __state.Length && i < regions.Count; i++)
        {
            float gained = regions[i].boostPerYear_dekatons - __state[i];
            if (gained > pad)
            {
                built = regions[i];
                pad = gained;
            }
        }
        TIRegionState wanted = LaunchSites.BestSite(LaunchSites.Candidates(__instance));
        if (built == null || built == wanted)
        {
            return;
        }
        // Exactly what vanilla just added, so a region that already launched keeps what it had.
        // A pad this small is under maxSTOFighters' one-dekaton threshold at either end of the
        // move, so no fighter is stranded by it.
        built.ChangeSpaceFacilityValue(SpaceFacilityType.launchFacility, -pad);
        wanted.ChangeSpaceFacilityValue(SpaceFacilityType.launchFacility, pad);
    }
}

// The priority's tooltip offers the range between the worst and the best region the nation has,
// including regions under occupation, which cannot be built in at all. Both ends now come from
// the eligible list, and from the focus setting.
[HarmonyPatch(typeof(TINationState), "BoostGainLow")]
internal static class LaunchSiteGainLow
{
    private static bool Prefix(TINationState __instance, ref float __result) =>
        LaunchSites.Gain(__instance, best: false, ref __result);
}

[HarmonyPatch(typeof(TINationState), "BoostGainHigh")]
internal static class LaunchSiteGainHigh
{
    private static bool Prefix(TINationState __instance, ref float __result) =>
        LaunchSites.Gain(__instance, best: true, ref __result);
}

// BestBoostLatitude finds the region nearest the equator by |latitude| and then returns the signed
// figure, which every one of its three readers treats as a distance from the equator: the AI's
// nation score pays (90 - latitude) / 3, so a nation at 60S scores as though it sat past the north
// pole; IsUsefulForBoost's "<= 25" is true of every southern nation, Chile included; and the event
// that hands a faction a space program takes MinBy of it, which finds the most southerly nation
// rather than the most equatorial one. The magnitude is what all three meant.
[HarmonyPatch(typeof(TINationState), "BestBoostLatitude", MethodType.Getter)]
internal static class BestBoostLatitudeMagnitude
{
    private static void Postfix(ref float __result)
    {
        if (LaunchSites.Enabled)
        {
            __result = Mathf.Abs(__result);
        }
    }
}

// Tweak 17: faction dormancy reaches every faction but the aliens and the player, and a faction
// that comes back into control points is no longer stuck in it.
//
// The campaign option "Allow AI Factions To Be Disabled" lets a human AI faction that has been
// reduced to nothing - no fleets, habs, councilors or control points on a daily tick, once
// councilor turns fall below twice a month, which is campaign year 15 - cease operating for good.
// The two halves below are the same change seen from either end: who the option can reach, and
// whether what it does is final.
//
// TIFactionState.CanBeDisabled spares five factions. Two are structural and stay: the alien
// faction, and whoever is playing. Three are spared for the story and are what this drops:
//
//   - the alien proxy, whichever faction in the campaign has the lowest positive willProxy on its
//     ideology, which is the Servants in a game with every faction;
//   - the alien appeaser, the lowest positive willAppease, which is the Protectorate;
//   - the faction with the highest ideology x, which is Humanity First, then the Resistance, then
//     whoever is left - and this one only applies while the player is not very anti-alien
//     themselves, so playing either of the first two already switches it off.
//
// None of the three is a named faction, and in a game missing the usual holder the role moves to
// whoever fits best - without the Servants, the Protectorate is both proxy and appeaser. So the
// waiver is written as the rule that is left rather than as a list of exceptions to vanilla's:
// the four conditions below are CanBeDisabled with the three story clauses struck out. A faction
// that vanilla refused for any other reason fails them here too, which is why they can be tested
// on their own rather than after asking which exemption applied.
[HarmonyPatch(typeof(TIFactionState), "CanBeDisabled")]
internal static class DormancyReachesEveryone
{
    private static void Postfix(TIFactionState __instance, ref bool __result)
    {
        if (__result || !Main.settings.factionDormancy)
        {
            return;
        }
        __result = TIGlobalValuesState.CanDisableFactions
            && TIMissionPhaseState.phasesPerMonth < 2f
            && !__instance.IsAlienFaction
            && !__instance.isActivePlayer;
    }
}

// The other half. Dormancy is one-way in the vanilla game: TIFactionState.defeated is set once and
// nothing ever clears it, and nothing stops a dormant faction being given a control point either,
// because of the thirteen places the flag is read not one of them is control point ownership. So
// the aliens' Enthrall Elites and Terrorize missions go on handing seats to the alien proxy and
// the alien appeaser long after those two have gone quiet, a seat on the diplomacy table can be
// traded to one the same way, and the seat is then held by a faction that will never act on it
// and can never lose it to anybody else.
//
// Waking one costs nothing that has to be rebuilt by hand. The council has four slots whatever
// the faction holds, the AI's task groups gate on the flag alone and go back to recruiting and
// setting goals the moment it clears, and CheckForDefeated is called daily for a dormant faction
// - Daily0000FactionUpdate calls it before it returns on the flag - so the hook is already there.
//
// What does not come back is what the defeat routine spent: base incomes from the faction's
// founding and from events were zeroed, the treasury was emptied, and the orgs in its unassigned
// pool were taken. A faction that returns runs on what its seats bring in and nothing else, which
// is a fair price for having been put down. There is no notification either way, because the game
// has one for a faction going quiet and none for a faction coming back.
[HarmonyPatch(typeof(TIFactionState), "CheckForDefeated")]
internal static class DormancyCanBeUndone
{
    // Two rather than one, so a single seat changing hands cannot flip a faction awake and back
    // to dormant on consecutive days, running the defeat routine and its notification each time.
    // ponytail: a constant rather than a setting, worth a slider only if it wants tuning.
    private const int SeatsToReturn = 2;

    private static void Prefix(TIFactionState __instance)
    {
        if (Main.settings.factionDormancy && __instance.defeated
            && __instance.controlPoints.Count >= SeatsToReturn)
        {
            // Vanilla's check runs next and reads the flag again. With seats in hand the faction
            // no longer answers to Defeated() either, so it is not put straight back down.
            __instance.defeated = false;
        }
    }
}

// Tweak 18: Show Triggered Projects stops counting as a difficulty change.
//
// StartMenuController.ValidateCustomDifficultySettings compares every campaign option against its
// default in one long condition, and ends it with "&& !showtriggeredProjectsToggle.isOn". So the
// option that only displays which projects have been triggered or missed puts the campaign in
// custom difficulty, next to the sliders for research speed and alien progression. The campaign
// then carries scenarioCustomizations.customDifficulty for good, and at a victory that flag is
// what holds back normalWin, veteranWin and brutalWin. No other achievement reads it, and playing
// with mods does not gate achievements at all.
//
// The condition is not copied here. The toggle is turned off for the length of the call and put
// back afterwards, so the vanilla test runs as though the option were unset and every other term
// in it goes on deciding the answer as it always did. The restore is a finalizer rather than a
// postfix: a postfix is skipped when the original throws, which would leave the toggle reading
// off, and the screen writes that toggle straight into the campaign a moment later.
[HarmonyPatch(typeof(StartMenuController), "ValidateCustomDifficultySettings")]
internal static class TriggeredProjectsKeepAchievements
{
    private static void Prefix(StartMenuController __instance, out bool __state)
    {
        Toggle toggle = __instance.showtriggeredProjectsToggle;
        __state = Main.settings.triggeredProjectsKeepAchievements && toggle != null && toggle.isOn;
        if (__state)
        {
            toggle.SetIsOnWithoutNotify(false);
        }
    }

    private static Exception Finalizer(StartMenuController __instance, bool __state,
        Exception __exception)
    {
        if (__state)
        {
            __instance.showtriggeredProjectsToggle.SetIsOnWithoutNotify(true);
        }
        return __exception;
    }
}

// Tweak 19: councilor portraits stop changing with age, and the customize screen offers one set.
//
// Every appearance template carries two images, portraitYoung and portraitOld, and
// TICouncilorState.useOldPortrait chooses between them at age > TICouncilorAppearanceTemplate
// .ageCutPoint, which is 55. The customize screen caches its grid at twice the template count -
// one young entry and one old entry for each - and then shows only the half that matches the
// councilor in hand:
//
//     councilorAppearanceGrid.SetListSize<CouncilorAppearanceGridItem>(list.Count * 2);
//     ...
//     if (item.old == currentCouncilor.useOldPortrait && ...)
//
// So two councilors a year either side of 55 are offered what look like two different sets of
// portraits with every filter set the same, and no control on that screen reaches it.
//
// The flag feeds the portrait, the icon, the idle video and that grid filter, and nothing else,
// so holding it false is purely cosmetic. Every councilor keeps the young art, the screen offers
// one selection, and a face no longer changes on a birthday. Aliens already answer false.
[HarmonyPatch(typeof(TICouncilorState), "useOldPortrait", MethodType.Getter)]
internal static class UnagedCouncilorPortraits
{
    private static void Postfix(ref bool __result)
    {
        if (Main.settings.unagedCouncilorPortraits)
        {
            __result = false;
        }
    }
}

// Tweak 20: a nation that loses its capital in a war and is left seated on a dormant nation's
// capital becomes that nation.
//
// Broken Earth's Westralia holds Perth and Java. Take Perth and vanilla leaves a rump Westralia
// governing Jakarta, while Indonesia - whose capital Jakarta is, and which claims it - sits dormant.
// So when the war ends, the rump takes the identity its new capital implies: Indonesia comes back
// with every region the rump held, and the rump goes dormant in its place.
//
// Vanilla's revival path does the work - Independence, as a secession or release does - so the
// dormant nation is reinitialized the way the game always does it, and the rump's wars, alliances,
// rivalries, breakaways and federation seat are wound up by the transfer that empties it. Those
// are deliberately not carried over: it is a different nation. What Independence leaves behind is
// put across by hand, as AbsorbNation does for a merger: control point owners and their priorities,
// nuclear weapons, space funding and accumulated investment.
//
// "Lost its capital in this war" is read off the map rather than tracked: at the moment a war
// ends, the nation's original capital is held by the enemy it just made peace with. The swap waits
// for the next daily update, and for the nation to be at peace with everyone - wars end inside
// region transfers, which is no place to empty a nation. ponytail: the queue is in memory only, so
// a reload between the peace and the next day drops it.
[HarmonyPatch]
internal static class DormantCapitalIdentity
{
    private static readonly HashSet<TINationState> pending = new HashSet<TINationState>();

    private static readonly MethodInfo independence = AccessTools.Method(typeof(TINationState), "Independence");
    private static readonly MethodInfo nuclearProgram =
        AccessTools.PropertySetter(typeof(TINationState), nameof(TINationState.nuclearProgram));

    // Every peace - a full war's end, a separate peace, a nation knocked out - passes through here
    // once per pair of enemies.
    [HarmonyPatch(typeof(TINationState), "EndWarWithSingleEnemy")]
    [HarmonyPostfix]
    private static void OnPeace(TINationState __instance, TINationState otherNation)
    {
        if (!Main.settings.dormantCapitalIdentity)
        {
            return;
        }
        Mark(__instance, otherNation);
        Mark(otherNation, __instance);
    }

    private static void Mark(TINationState nation, TINationState enemy)
    {
        if (nation?.originalCapital != null && enemy != null && nation.originalCapital.nation == enemy)
        {
            pending.Add(nation);
        }
    }

    [HarmonyPatch(typeof(PavonisInteractive.TerraInvicta.Systems.PeriodicUpdates.NationPeriodicUpdate), "OnDailyUpdate")]
    [HarmonyPostfix]
    private static void Daily()
    {
        if (pending.Count == 0)
        {
            return;
        }
        foreach (TINationState nation in pending.ToList())
        {
            if (nation.extant && nation.wars.Count > 0)
            {
                continue;                          // still fighting someone: wait for that peace too
            }
            pending.Remove(nation);
            if (Main.settings.dormantCapitalIdentity)
            {
                TryBecomeHeir(nation);
            }
        }
    }

    [HarmonyPatch(typeof(GameControl), nameof(GameControl.CompleteInit))]
    [HarmonyPostfix]
    private static void OnCampaignReady() => pending.Clear();

    private static void TryBecomeHeir(TINationState rump)
    {
        TIRegionState seat = rump.capital;
        if (!rump.extant || rump.alienNation || seat == null || rump.regions.Contains(rump.originalCapital))
        {
            return;
        }
        TINationState heir = GameStateManager.AllNations().FirstOrDefault(x => x.originalCapital == seat);
        if (heir == null || heir == rump || heir.extant || heir.alienNation || !seat.ClaimedBy(heir))
        {
            return;
        }

        // Everything Independence would lose, read before the rump is emptied.
        var owners = rump.controlPoints.OrderBy(cp => cp.positionInNation)
            .Select(cp => (cp.faction, priorities: new Dictionary<PriorityType, int>(cp.controlPointPriorities)))
            .ToList();
        List<TIControlPoint> rumpPoints = rump.controlPoints.ToList();
        TIFactionState executive = rump.executiveFaction;
        int nukes = rump.numNuclearWeapons;
        bool nuclear = rump.nuclearProgram;
        float funding = rump.spaceFunding_year;
        float fundingIncome = rump.spaceFundingIncome_year;
        Dictionary<PriorityType, float> invested =
            Enums.PriorityTypes.ToDictionary(p => p, p => rump.GetAccumulatedInvestmentPoints(p));

        // Amicable, and with the executive as the acting faction for control points: the amicable
        // branch otherwise reads the parent's executive faction after the parent has been emptied,
        // and an empty nation has no executive control point to read. Reporting is suppressed and
        // redone below, once the control points show their real owners.
        independence.Invoke(heir, new object[]
        {
            executive, rump, rump, rump.regions.ToList(), true, true, true, ControlPointChangeCause.Independence
        });

        if (heir.regions.Contains(seat))
        {
            heir.SetCapital(seat);
        }
        // Lined up from the top, the way the game itself treats control points: the top one is the
        // executive, a nation that grows gains new ones at the bottom and hands them to its highest
        // owner, and a nation that shrinks sheds from the bottom. The revived nation's count is
        // its own until the next control point update settles it from the GDP, so a mismatch here
        // is filled and trimmed the same way.
        TIFactionState grower = owners.LastOrDefault(o => o.faction != null).faction;
        int offset = owners.Count - heir.numControlPoints;
        for (int i = 0; i < heir.numControlPoints; i++)
        {
            TIControlPoint cp = heir.GetControlPoint(i);
            if (cp == null)
            {
                break;
            }
            int from = i + offset;
            if (from < 0)
            {
                heir.ChangeControlPointOwner(i, ControlPointChangeCause.Growth, grower);
                continue;
            }
            heir.ChangeControlPointOwner(i, ControlPointChangeCause.Independence, owners[from].faction);
            foreach (KeyValuePair<PriorityType, int> priority in owners[from].priorities)
            {
                cp.SetControlPointPriority(priority.Key, priority.Value, skipUpdate: true, bulkUpdate: true);
            }
            // One unbatched set flushes the batch, as ApplyInvestmentTemplateToControlPoint does.
            if (owners[from].priorities.TryGetValue(PriorityType.Economy, out int economy))
            {
                cp.SetControlPointPriority(PriorityType.Economy, economy);
            }
        }
        // AbsorbNation clears the joining nation's control points the same way, so no faction is
        // left holding a seat in a nation with no regions.
        foreach (TIControlPoint cp in rumpPoints)
        {
            rump.ChangeControlPointOwner(cp.positionInNation, ControlPointChangeCause.Annexation, null);
        }

        if (nuclear && !heir.nuclearProgram)
        {
            nuclearProgram.Invoke(heir, new object[] { true });
        }
        if (nukes > 0)
        {
            heir.ChangeNumNuclearWeapons(nukes);
            rump.ChangeNumNuclearWeapons(-nukes);
        }
        heir.ChangeAnnualSpaceFundingValue(funding);
        rump.ChangeAnnualSpaceFundingValue(-fundingIncome);
        foreach (KeyValuePair<PriorityType, float> pair in invested)
        {
            if (pair.Value > 0f && heir.ValidPriority(pair.Key))
            {
                heir.ModifyAccumulatedInvestment(pair.Key, pair.Value, multiply: false, triggerUpdate: false);
            }
        }
        heir.SetDataDirty();
        TINotificationQueueState.LogIndependence(heir, rump);
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

    // Settings that only mean something while another is on, and gray out with it.
    private static readonly Dictionary<string, string> DependsOn = new Dictionary<string, string>
    {
        { nameof(Settings.shipyardRally), nameof(Settings.pooledStationConstruction) },
        { "solarMirrorOutputCap", "solarMirrorsBoostStations" },
        { "launchSiteFocus", "betterLaunchSites" },
        { "controlPointTradeValue", "tradeControlPoints" },
        { "tradeSuppressedControlPoints", "tradeControlPoints" },
    };

    private static readonly Dictionary<string, string> Suffixes = new Dictionary<string, string>
    {
        { "solarMirrorOutputCap", "x" },
        { "controlPointTradeValue", "x" },
    };

    // Stored as a fraction, read by a player as a percentage.
    private static readonly HashSet<string> Percents = new HashSet<string>
    {
        "repeatableProjectScaling",
        "launchSiteFocus",
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
            .Where(f => f.GetCustomAttribute<DrawAttribute>() != null
                && (f.FieldType == typeof(bool) || f.FieldType == typeof(float)));

    private static Transform Toggle(Transform list, Transform source, TMP_Text title,
        FieldInfo field, DrawAttribute draw, Action refresh)
    {
        bool pooledConstruction = field.Name == nameof(Settings.pooledStationConstruction);
        bool rally = field.Name == nameof(Settings.shipyardRally);
        Transform row = CloneRow(list, source, field.Name, pooledConstruction
            ? Loc.T("UI.CataTweaks.PooledConstructionSetting")
            : rally ? Loc.T("UI.CataTweaks.RallySetting") : draw.Label, title, null);
        if (row == null)
        {
            return null;
        }
        Tooltip(row, pooledConstruction ? Loc.T("UI.CataTweaks.PooledConstructionTooltip")
            : rally ? Loc.T("UI.CataTweaks.RallyTooltip") : draw.Tooltip);
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
            Main.settings.NormalizeShipyardOptions(field.Name);
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
            // Not Commit: this fires every frame of a drag, and Commit writes Settings.xml and runs
            // OnChange. The patches read the field directly, so the value is already live; the
            // bookkeeping waits for the screen to close.
            pendingSave = true;
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
        // Selecting either shipyard mode switches off the other and updates both controls.
        foreach (string name in new[] { nameof(Settings.stationDividers), nameof(Settings.pooledStationConstruction) })
            if (rows.TryGetValue(name, out Transform modeRow))
                modeRow.GetComponentInChildren<Toggle>(true)?.SetIsOnWithoutNotify((bool)typeof(Settings).GetField(name).GetValue(Main.settings));
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

    private static bool pendingSave;

    // Sliders defer their commit to here rather than paying for it on every frame of a drag.
    [HarmonyPatch(typeof(OptionsMenuController), nameof(OptionsMenuController.OnDisable))]
    internal static class CommitOnClose
    {
        private static void Postfix()
        {
            if (pendingSave)
            {
                pendingSave = false;
                Commit();
            }
        }
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
    internal static Transform Row<T>(Transform control) where T : Component
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
    private const string CouncilPoolKey = Prefix + "councilPool";
    private const string RestoredEmpiresKey = Prefix + "restoredEmpires";

    private static ScenarioCustomizations Current =>
        TIGlobalValuesState.GlobalValues != null
            ? TIGlobalValuesState.GlobalValues.scenarioCustomizations
            : GameControl.control?.scenarioCustomizationsStartup;

    // A campaign started without going through Customize Campaign records nothing, so the answer
    // falls back to the default in Settings.xml rather than to a hardcoded one.
    // Read per claim row per claim query, so the key is a compile-time constant rather than a
    // concatenation, and nothing here allocates.
    // Expanded Council runs the council when it is installed, so the pool stands aside: the flag
    // stays in the save, and the pool comes back if that mod is removed.
    internal static bool CouncilPool =>
        !OtherMods.ExpandedCouncil && Get(CouncilPoolKey, Main.settings.spySlotsAsCouncilSlots);

    internal static bool RestoredEmpires => Get(RestoredEmpiresKey, Main.settings.restoredEmpires);

    private static bool Get(string key, bool fallback)
    {
        ScenarioCustomizations customizations = Current;
        if (customizations?.customFactionText == null)
        {
            return fallback;
        }
        return customizations.customFactionText.TryGetValue(
            key, out ScenarioCustomizations.CustomFactionText entry)
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
                Write(startup, option.Name, option.Chosen && !option.Locked());
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

    internal static bool Hidden(TIBilateralTemplate row) =>
        row != null && !CampaignFlags.RestoredEmpires && ModTemplates.Claims.Contains(row.dataName);

    // Both methods, because they answer for different callers. BilateralIsActive is what a
    // claim query goes through, and BilateralIsInScenario is what the loop in TIFactionState
    // tests when it hands out the claims of a completed project. Rows behind the vanilla
    // Commonwealth Restored and Greater Dominion reach only the second one.
    [HarmonyPatch]
    internal static class HideClaims
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(TIBilateralTemplate),
                nameof(TIBilateralTemplate.BilateralIsActive));
            yield return AccessTools.Method(typeof(TIBilateralTemplate),
                nameof(TIBilateralTemplate.BilateralIsInScenario));
        }

        private static void Postfix(TIBilateralTemplate __instance, ref bool __result)
        {
            if (__result && Hidden(__instance))
            {
                __result = false;
            }
        }
    }
}

// Other mods CataTweaks makes room for. Each is recognised by what it has actually patched, under
// the Harmony id it registers, never by its version: any release that takes over the same methods
// is recognised, one that stops touching them is left alone, and a failed check means not there.
//
// Checked afresh when the start menu comes up and again when a campaign is ready, both after Unity
// Mod Manager has loaded every mod; readers in between take the last answer. Before the first
// check the answer is no, and nothing reads it that early.
[HarmonyPatch]
internal static class OtherMods
{
    // Expanded Council (Workshop 3810239494) raises the council to ten and lays out a twelve-seat
    // council screen of its own, with the two spy seats fixed at the end. The spy slot pool would
    // fight it over both - which cap wins would come down to patch order - so it stands aside.
    internal static bool ExpandedCouncil { get; private set; }

    [HarmonyPatch(typeof(GameControl), nameof(GameControl.CompleteInit))]
    [HarmonyPostfix]
    private static void OnCampaignReady() => Detect();

    internal static void Detect()
    {
        bool was = ExpandedCouncil;
        ExpandedCouncil = Patched("ExpandedCouncil",
            AccessTools.PropertyGetter(typeof(TIFactionState), nameof(TIFactionState.maxCouncilSize)),
            AccessTools.Method(typeof(CouncilGridController), nameof(CouncilGridController.UpdateCouncilorGrid)));
        if (ExpandedCouncil && !was)
        {
            Main.mod?.Logger.Log("Expanded Council is installed; the spy slot pool stands aside for it.");
        }
    }

    private static bool Patched(string owner, params MethodBase[] methods)
    {
        try
        {
            return methods.Any(method => method != null
                && Harmony.GetPatchInfo(method)?.Owners.Contains(owner) == true);
        }
        catch (Exception e)
        {
            Main.mod?.Logger.Error("Could not check for " + owner + " - " + e.Message);
            return false;
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
        // True when another mod has taken over what the option does: it is held off and greyed out.
        internal Func<bool> Locked = () => false;
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
            Locked = () => OtherMods.ExpandedCouncil,
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
        OtherMods.Detect();
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
            Transform sourceRow = VanillaOptionsRows.Row<Toggle>(template.transform);
            Transform grid = sourceRow.parent;
            Transform list = grid != null ? grid.parent : null;
            if (list == null)
            {
                return;
            }

            foreach (Option option in Options)
            {
                option.Chosen = !option.Locked() && option.Default();
            }

            if (list.Find(HeaderName) == null)
            {
                // Cloned from "Your Faction Names" rather than built, so it carries that section
                // header's own font, size and spacing without having to guess at them.
                Transform styleRow = VanillaOptionsRows.Row<TMP_Text>(headerStyle.transform);
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

        bool locked = option.Locked();
        VanillaOptionsRows.Tooltip(cell, locked && option.Name == "councilPool"
            ? option.Tip + "\n\n" + Loc.T("UI.CataTweaks.CouncilPoolYieldsToExpandedCouncil")
            : option.Tip);

        Toggle toggle = cell.GetComponentInChildren<Toggle>(true);
        // A fresh event drops the editor-wired call to OnToggleVariableProjectUnlocks.
        toggle.onValueChanged = new Toggle.ToggleEvent();
        toggle.isOn = option.Chosen && !locked;
        toggle.interactable = !locked;
        toggle.onValueChanged.AddListener(on => option.Chosen = on && !option.Locked());
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

    // Namelist keys, as the game stores them. Not the dropdown index, which moves when a mod
    // adds a list, and not the label, which changes with the display language.
    public string smallShips;
    public string mediumShips;
    public string largeShips;
    public string habs;
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

    // Maps a namelist key to the label its dropdown shows.
    private static readonly FieldInfo nameLists =
        AccessTools.Field(typeof(StartMenuController), "nameListsToAdd");

    private static string SelectedList(StartMenuController menu, TMP_Dropdown dropdown)
    {
        if (dropdown == null || dropdown.value < 0 || dropdown.value >= dropdown.options.Count)
        {
            return null;
        }
        string label = dropdown.options[dropdown.value].text;
        Dictionary<string, string> lists =
            nameLists?.GetValue(menu) as Dictionary<string, string>;
        if (lists == null)
        {
            return null;
        }
        foreach (KeyValuePair<string, string> pair in lists)
        {
            if (pair.Value == label)
            {
                return pair.Key;
            }
        }
        return null;
    }

    private static void SelectList(TMP_Dropdown dropdown, string key)
    {
        if (dropdown == null || string.IsNullOrEmpty(key))
        {
            return;
        }
        string label = TIUtilities.LocalizedNamelistIDX(key);
        int index = dropdown.options.FindIndex(option => option.text == label);
        if (index < 0)
        {
            return;
        }
        dropdown.value = index;
        dropdown.RefreshShownValue();
    }

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
                    smallShips = SelectedList(__instance, __instance.smallShipNameListIdxDropdown),
                    mediumShips = SelectedList(__instance, __instance.mediumShipNameListIdxDropdown),
                    largeShips = SelectedList(__instance, __instance.largeShipNameListIdxDropdown),
                    habs = SelectedList(__instance, __instance.habNameListIdxDropdown),
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
                SelectList(__instance.smallShipNameListIdxDropdown, saved.smallShips);
                SelectList(__instance.mediumShipNameListIdxDropdown, saved.mediumShips);
                SelectList(__instance.largeShipNameListIdxDropdown, saved.largeShips);
                SelectList(__instance.habNameListIdxDropdown, saved.habs);
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
