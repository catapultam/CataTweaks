// Runs the real queue dispatcher against the installed game's types without starting Unity.
// Pricing, eligibility and payment are stubbed at their game boundaries. This verifies queue
// mutation and scheduling; it cannot verify prefab layout or real resource/refit calculations.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using CataTweaks;
using HarmonyLib;
using FullSerializer;
using PavonisInteractive.TerraInvicta;

internal static class Program
{
    private static Type pool;
    private static readonly Dictionary<TIHabModuleState, Yard> yards = new Dictionary<TIHabModuleState, Yard>();
    private static readonly Dictionary<TISpaceShipTemplate, int> minimumTier = new Dictionary<TISpaceShipTemplate, int>();
    private static bool afford = true, player = true, station = true;
    private static int checks;
    private static int nextID;
    private sealed class Yard { internal TIHabState Hab; internal int Tier; internal bool Active; internal float Days; }

    private static int Main(string[] args)
    {
        string managed = Path.Combine(args.Length > 0 ? args[0] : @"C:\Program Files (x86)\Steam\steamapps\common\Terra Invicta", "TerraInvicta_Data", "Managed");
        AppDomain.CurrentDomain.AssemblyResolve += (_, e) =>
        {
            string name = new AssemblyName(e.Name).Name + ".dll";
            foreach (string dir in new[] { managed, Path.Combine(managed, "UnityModManager") })
            {
                string path = Path.Combine(dir, name);
                if (File.Exists(path)) return Assembly.LoadFrom(path);
            }
            return null;
        };
        try { Run(); return 0; }
        catch (Exception e) { Console.Error.WriteLine(e); return 1; }
    }

    private static T Bare<T>()
    {
        var value = (T)FormatterServices.GetUninitializedObject(typeof(T));
        if (value is TIGameState state) state.ID = new GameStateID(++nextID);
        return value;
    }
    private static object Call(string name, params object[] args) => AccessTools.Method(pool, name, args.Select(a => a.GetType()).ToArray()).Invoke(null, args);
    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        checks++;
    }
    private static void Stub(Harmony harmony, MethodBase method, string prefix)
    {
        if (method == null) throw new Exception("Missing stub target: " + prefix);
        harmony.Patch(method, prefix: new HarmonyMethod(typeof(Program), prefix) { priority = Priority.First });
    }
    private static bool Player(ref bool __result) { __result = player; return false; }
    private static bool Station(ref bool __result) { __result = station; return false; }
    private static bool Hab(TIHabModuleState __instance, ref TIHabState __result) { __result = yards[__instance].Hab; return false; }
    private static bool Tier(TIHabModuleState __instance, ref int __result) { __result = yards[__instance].Tier; return false; }
    private static bool Active(TIHabModuleState __instance, ref bool __result) { __result = yards[__instance].Active; return false; }
    private static bool Compatible(TISpaceShipTemplate __instance, TIHabModuleState __0, ref bool __result)
    { __result = yards[__0].Tier >= minimumTier[__instance]; return false; }
    private static bool Duration(TIHabModuleState __0, ref float __result) { __result = yards[__0].Days; return false; }
    private static bool NoPrice() => false;
    private static bool RefitCost(TIHabModuleState __0, ref TIResourcesCost __result)
    {
        __result = Bare<TIResourcesCost>();
        __result.SetCompletionTime_Days(yards[__0].Days);
        return false;
    }
    private static bool Pay(TIFactionState __instance, TIHabModuleState __0, ref bool __result)
    {
        __result = afford;
        if (afford) __instance.GetShipyardQueue(__0)[0].costPaid = true;
        return false;
    }

    private static (TIFactionState faction, TIHabState hab, TIHabModuleState high, TIHabModuleState low) Fixture()
    {
        yards.Clear(); minimumTier.Clear(); afford = player = station = true;
        var faction = Bare<TIFactionState>();
        AccessTools.Property(typeof(TIFactionState), "nShipyardQueues").SetValue(faction,
            new Dictionary<TIHabModuleState, List<ShipConstructionQueueItem>>());
        var hab = Bare<TIHabState>();
        var high = Bare<TIHabModuleState>(); var low = Bare<TIHabModuleState>();
        yards[high] = new Yard { Hab = hab, Tier = 3, Active = true, Days = 10 };
        yards[low] = new Yard { Hab = hab, Tier = 1, Active = true, Days = 20 };
        faction.nShipyardQueues[high] = new List<ShipConstructionQueueItem>();
        faction.nShipyardQueues[low] = new List<ShipConstructionQueueItem>();
        AccessTools.Field(pool, "dailyUpdateDepth").SetValue(null, 0);
        AccessTools.Field(pool, "editingDepth").SetValue(null, 0);
        return (faction, hab, high, low);
    }
    private static ShipConstructionQueueItem Enqueue(TIFactionState faction, TIHabModuleState yard, int tier = 1, bool paid = false)
    {
        var design = Bare<TISpaceShipTemplate>();
        var hull = Bare<TIShipHullTemplate>();
        AccessTools.Field(typeof(TISpaceShipTemplate), "_hullTemplate").SetValue(design, hull);
        minimumTier[design] = tier;
        var item = Bare<ShipConstructionQueueItem>();
        AccessTools.Field(typeof(ShipConstructionQueueItem), "_shipDesign").SetValue(item, design);
        item.shipyard = yard; item.costPaid = paid; item.daysToCompletion = 7;
        faction.GetShipyardQueue(yard).Add(item);
        return item;
    }
    private static List<ShipConstructionQueueItem> View(TIFactionState faction, TIHabModuleState yard) =>
        (List<ShipConstructionQueueItem>)Call("View", faction, yard);

    private static void Run()
    {
        pool = typeof(Settings).Assembly.GetType("CataTweaks.StationConstruction", true);
        var harmony = new Harmony("CataTweaks.ConstructionCheck");
        // Unity's Mono implements engine ECalls that desktop CLR cannot JIT. Check target
        // existence here; attaching UI/completion patches must be smoke-tested inside Unity.
        foreach (var type in typeof(Settings).Assembly.GetTypes().Where(t => t.Name.StartsWith("StationConstruction")
            && t.GetCustomAttributes(typeof(HarmonyPatch), false).Length > 0))
        {
            var targetMethods = AccessTools.Method(type, "TargetMethods");
            var targets = targetMethods != null ? (IEnumerable<MethodBase>)targetMethods.Invoke(null, null)
                : type.GetCustomAttributes(typeof(HarmonyPatch), false).Cast<HarmonyPatch>()
                    .Select(p => (MethodBase)AccessTools.Method(p.info.declaringType, p.info.methodName));
            Assert(targets.All(m => m != null), type.Name + " has valid game patch targets");
        }
        Stub(harmony, AccessTools.PropertyGetter(typeof(TIFactionState), "isActivePlayer"), nameof(Player));
        Stub(harmony, AccessTools.PropertyGetter(typeof(TIHabState), "IsStation"), nameof(Station));
        Stub(harmony, AccessTools.PropertyGetter(typeof(TIHabModuleState), "ref_hab"), nameof(Hab));
        Stub(harmony, AccessTools.PropertyGetter(typeof(TIHabModuleState), "tier"), nameof(Tier));
        Stub(harmony, AccessTools.PropertyGetter(typeof(TIHabModuleState), "active"), nameof(Active));
        Stub(harmony, AccessTools.Method(typeof(TISpaceShipTemplate), "CanBuildAtShipyard"), nameof(Compatible));
        Stub(harmony, AccessTools.Method(typeof(TISpaceShipTemplate), "RefitResourceCost",
            new[] { typeof(TIHabModuleState), typeof(TISpaceShipTemplate), typeof(bool), typeof(bool), typeof(TISpaceShipState) }), nameof(RefitCost));
        Stub(harmony, AccessTools.Method(typeof(TIShipHullTemplate), "constructionTime_Days", new[] { typeof(TIHabModuleState) }), nameof(Duration));
        Stub(harmony, AccessTools.Method(pool, "Reprice"), nameof(NoPrice));
        Stub(harmony, AccessTools.Method(typeof(TIFactionState), "AttemptInitiateShipConstruction"), nameof(Pay));

        var f = Fixture();
        var a = Enqueue(f.faction, f.high); var b = Enqueue(f.faction, f.high); var c = Enqueue(f.faction, f.high);
        Call("Dispatch", f.faction, f.hab);
        Assert(a.costPaid && b.costPaid && !c.costPaid, "Fill both slots, retain one waiting ship");
        Assert(a.shipyard == f.high && b.shipyard == f.low, "Use each yard's speed");
        Assert(a.daysToCompletion == 7 && b.daysToCompletion == 7, "Dispatcher does not advance time");
        Assert(View(f.faction, f.high).SequenceEqual(new[] { a, b, c }), "Combined queue shows paid slots before waiting tail");
        Assert(!ReferenceEquals(View(f.faction, f.high), f.faction.GetShipyardQueue(f.high)), "UI view does not expose a mutable physical queue");

        f = Fixture();
        a = Enqueue(f.faction, f.high, paid: true); b = Enqueue(f.faction, f.low, paid: true);
        c = Enqueue(f.faction, f.low);
        Call("Dispatch", f.faction, f.hab);
        Assert(a.shipyard == f.high && b.shipyard == f.low && a.daysToCompletion == 7, "Existing paid construction remains pinned");
        Assert(c.shipyard == f.high && !c.costPaid, "Legacy pending queues consolidate without restarting work");
        f.faction.GetShipyardQueue(f.low).Remove(b);
        Call("Dispatch", f.faction, f.hab);
        Assert(c.costPaid && c.shipyard == f.low, "Next waiting ship fills a newly freed slot");

        f = Fixture();
        a = Enqueue(f.faction, f.high, paid: true); b = Enqueue(f.faction, f.high, tier: 3);
        c = Enqueue(f.faction, f.high);
        Call("Dispatch", f.faction, f.hab);
        Assert(!b.costPaid && c.costPaid && c.shipyard == f.low, "Small yard skips a hull it cannot build");

        f = Fixture(); yards[f.high].Active = false;
        a = Enqueue(f.faction, f.high, tier: 3); b = Enqueue(f.faction, f.high);
        Call("Dispatch", f.faction, f.hab);
        Assert(!a.costPaid && b.costPaid && b.shipyard == f.low, "Inactive yard never starts work");

        f = Fixture(); afford = false; yards[f.low].Days = 5;
        a = Enqueue(f.faction, f.high); b = Enqueue(f.faction, f.high); c = Enqueue(f.faction, f.high);
        Call("Dispatch", f.faction, f.hab);
        Assert(View(f.faction, f.high).SequenceEqual(new[] { a, b, c }), "Resource failure preserves priority after transferring to a faster yard");
        Assert(f.faction.GetShipyardQueue(f.low).Count == 0 && !a.costPaid, "Failed start returns unpaid work to the saved tail");
        afford = true; Call("Dispatch", f.faction, f.hab);
        Assert(a.costPaid && b.costPaid && !c.costPaid, "Resource recovery starts queued work in order");

        f = Fixture(); a = Enqueue(f.faction, f.high);
        AccessTools.Field(pool, "dailyUpdateDepth").SetValue(null, 1);
        Call("Dispatch", f.faction, f.hab);
        Assert(!a.costPaid, "Daily update cannot dispatch into a yard still to be advanced");
        AccessTools.Field(pool, "dailyUpdateDepth").SetValue(null, 0);
        AccessTools.Field(pool, "editingDepth").SetValue(null, 1);
        Call("Dispatch", f.faction, f.hab);
        Assert(!a.costPaid, "Batch edits cannot start items being cleared");

        f = Fixture(); a = Enqueue(f.faction, f.high); station = false;
        Call("Dispatch", f.faction, f.hab);
        Assert(!a.costPaid && ReferenceEquals(View(f.faction, f.high), f.faction.GetShipyardQueue(f.high)), "Surface bases use vanilla queues");
        station = true; player = false; Call("Dispatch", f.faction, f.hab);
        Assert(!a.costPaid, "AI factions use vanilla scheduling");

        f = Fixture(); a = Enqueue(f.faction, f.high, paid: true);
        b = Enqueue(f.faction, f.high); c = Enqueue(f.faction, f.high);
        var lostYard = typeof(Settings).Assembly.GetType("CataTweaks.StationConstructionLostYard", true);
        AccessTools.Method(lostYard, "Prefix").Invoke(null, new object[] { f.faction, f.high });
        Assert(f.faction.GetShipyardQueue(f.high).SequenceEqual(new[] { a }), "Lost yard retains only its paid construction for vanilla cancellation");
        Assert(f.faction.GetShipyardQueue(f.low).SequenceEqual(new[] { b, c }) && b.shipyard == f.low && c.shipyard == f.low,
            "Lost yard preserves pending items and their ownership in order");

        f = Fixture(); a = Enqueue(f.faction, f.high);
        var settings = (Settings)AccessTools.Field(typeof(Settings).Assembly.GetType("CataTweaks.Main"), "settings").GetValue(null);
        settings.pooledStationConstruction = false;
        Call("Dispatch", f.faction, f.hab);
        Assert(!a.costPaid && ReferenceEquals(View(f.faction, f.high), f.faction.GetShipyardQueue(f.high)), "Disabled setting restores vanilla queue access");
        settings.pooledStationConstruction = true;

        f = Fixture(); a = Enqueue(f.faction, f.high); b = Enqueue(f.faction, f.high);
        a.isRefit = b.isRefit = true;
        AccessTools.Field(typeof(ShipConstructionQueueItem), "_originalShipDesign").SetValue(a, a.shipDesign);
        AccessTools.Field(typeof(ShipConstructionQueueItem), "_originalShipDesign").SetValue(b, b.shipDesign);
        a.originalSpaceShipState = Bare<TISpaceShipState>(); b.originalSpaceShipState = Bare<TISpaceShipState>();
        Call("Dispatch", f.faction, f.hab);
        Assert(a.costPaid && b.costPaid && a.shipyard != b.shipyard, "Refits occupy parallel station slots");
        Assert(a.originalSpaceShipState != null && b.originalSpaceShipState != null, "Refit source ships survive queue transfer");

        var options = new Settings();
        var normalize = AccessTools.Method(typeof(Settings), "NormalizeShipyardOptions");
        Assert(options.pooledStationConstruction && !options.stationDividers, "Shipyard modes are exclusive by default");
        options.stationDividers = true;
        normalize.Invoke(options, new object[] { nameof(Settings.stationDividers) });
        Assert(options.stationDividers && !options.pooledStationConstruction, "Enabling dividers switches off shared queues");
        options.pooledStationConstruction = true;
        normalize.Invoke(options, new object[] { nameof(Settings.pooledStationConstruction) });
        Assert(options.pooledStationConstruction && !options.stationDividers, "Enabling shared queues switches off dividers");
        options.stationDividers = true;
        normalize.Invoke(options, new object[] { null });
        Assert(options.stationDividers && !options.pooledStationConstruction, "Mod-manager changes detect the newly enabled divider option");
        options.pooledStationConstruction = true;
        normalize.Invoke(options, new object[] { null });
        Assert(options.pooledStationConstruction && !options.stationDividers, "Mod-manager changes detect the newly enabled shared queue option");
        options.stationDividers = options.pooledStationConstruction = true;
        normalize.Invoke(options, new object[] { nameof(Settings.pooledStationConstruction) });
        Assert(!options.stationDividers, "Old settings with both enabled normalize to shared queues on load");
        options.stationDividers = options.pooledStationConstruction = false;
        normalize.Invoke(options, new object[] { null });
        Assert(!options.stationDividers && !options.pooledStationConstruction, "Both optional shipyard modes may be disabled");

        var progress = AccessTools.Method(typeof(Settings).Assembly.GetType("CataTweaks.StationConstructionProgress", true), "Fraction");
        Func<ShipConstructionQueueItem, float> fraction = item => (float)progress.Invoke(null, new object[] { item });
        Assert(fraction(null) == 0f, "Idle slot progress is empty");
        a.resourcesCost = Bare<TIResourcesCost>(); a.resourcesCost.SetCompletionTime_Days(100f);
        a.daysToCompletion = 75f;
        Assert(fraction(a) == 0.25f, "Slot progress reflects completed work");
        a.daysToCompletion = -1f;
        Assert(fraction(a) == 1f, "Completed progress clamps to full");
        a.daysToCompletion = 150f;
        Assert(fraction(a) == 0f, "Progress cannot be negative");
        a.resourcesCost.SetCompletionTime_Days(0f);
        Assert(fraction(a) == 0f, "Zero-duration progress is safe");
        a.resourcesCost.SetCompletionTime_Days(100f); a.daysToCompletion = 25f; a.costPaid = false;
        Assert(fraction(a) == 0f, "Unstarted work has no slot progress");

        var sectionHeight = AccessTools.Method(typeof(Settings).Assembly.GetType("CataTweaks.StationConstructionSlots", true), "BuildingHeight");
        Func<int, float, float, float> height = (count, row, available) =>
            (float)sectionHeight.Invoke(null, new object[] { count, row, available });
        Assert(height(1, 24f, 240f) == 24f, "One slot uses one full-height construction row");
        Assert(height(4, 24f, 240f) == 96f, "Currently Building grows with slot count");
        Assert(height(20, 24f, 240f) == 192f, "Large slot counts preserve two waiting queue rows");
        Assert(height(20, 24f, 240f) == height(8, 24f, 240f), "Overflow slots scroll instead of expanding outside the card");
        Assert(height(2, 24f, 20f) == 10f, "Small available areas preserve space for both sections");
        Assert(height(2, 24f, 0f) == 0f, "Not-yet-sized card geometry is safe");

        var gridType = typeof(Settings).Assembly.GetType("CataTweaks.StationConstructionGrid", true);
        var cardHeight = AccessTools.Method(gridType, "CardHeight");
        var contentWidth = AccessTools.Method(gridType, "ContentWidth");
        Assert((float)cardHeight.Invoke(null, new object[] { 720f, 12f, 16f }) == 692f,
            "Cards fill viewport height while reserving padding and the horizontal scrollbar");
        Assert((float)cardHeight.Invoke(null, new object[] { 1000f, 12f, 16f }) == 972f,
            "Card height responds to a taller viewport");
        Assert((float)cardHeight.Invoke(null, new object[] { 0f, 12f, 16f }) == 1f,
            "Initial viewport measurement cannot produce negative card height");
        Assert((float)contentWidth.Invoke(null, new object[] { 3, 300f, 10f, 20f, 600f }) == 940f,
            "Horizontal content includes each card, spacing and padding");
        Assert((float)contentWidth.Invoke(null, new object[] { 1, 300f, 10f, 20f, 600f }) == 600f,
            "A single card does not create unnecessary horizontal overflow");
        Assert((float)contentWidth.Invoke(null, new object[] { 0, 300f, 10f, 20f, 600f }) == 600f,
            "Filtering out all shipyards preserves valid viewport-sized content");
        var tooltipJob = AccessTools.Method(typeof(Settings).Assembly.GetType("CataTweaks.StationConstructionHabTooltip", true), "CurrentJob");
        Func<TIFactionState, TIHabModuleState, ShipConstructionQueueItem> currentJob = (faction, yard) =>
            (ShipConstructionQueueItem)tooltipJob.Invoke(null, new object[] { faction, yard });
        f = Fixture();
        Assert(currentJob(f.faction, f.high) == null, "Idle yard tooltip has no construction annotation");
        Assert(currentJob(f.faction, Bare<TIHabModuleState>()) == null, "Module without a registered queue has no job");
        a = Enqueue(f.faction, f.high);
        Assert(currentJob(f.faction, f.high) == null, "Waiting unpaid work is not labeled under construction");
        b = Enqueue(f.faction, f.high, paid: true);
        c = Enqueue(f.faction, f.low, paid: true);
        Assert(currentJob(f.faction, f.high) == b && currentJob(f.faction, f.low) == c,
            "Each module tooltip describes its own physical slot, not the station queue");
        yards[f.high].Active = false;
        Assert(currentJob(f.faction, f.high) == b, "Paused paid construction remains visible in an unpowered yard tooltip");
        b.isRefit = true;
        Assert(currentJob(f.faction, f.high) == b && currentJob(f.faction, f.high).isRefit,
            "Paid refit remains identifiable in the yard tooltip");
        f.faction.GetShipyardQueue(f.high).Remove(b);
        Assert(currentJob(f.faction, f.high) == null, "Completed or cancelled work disappears from the tooltip");
        CheckRally();
        Console.WriteLine(checks + " patch target, scheduler, tooltip and rally checks passed.");
    }

    private static Trajectory Route(int arrivalDays, double dv, int launchDays = 0)
    {
        var route = Bare<Trajectory_Microthrust>();
        var start = new DateTime(2112, 1, 1);
        AccessTools.PropertySetter(typeof(Trajectory), "launchTime").Invoke(route, new object[] { new TIDateTime(start.AddDays(launchDays)) });
        AccessTools.PropertySetter(typeof(Trajectory), "arrivalTime").Invoke(route, new object[] { new TIDateTime(start.AddDays(arrivalDays)) });
        AccessTools.PropertySetter(typeof(Trajectory), "boostDV_mps").Invoke(route, new object[] { dv });
        return route;
    }

    private static void CheckRally()
    {
        var rally = typeof(Settings).Assembly.GetType("CataTweaks.StationConstructionRally", true);
        var settings = (Settings)AccessTools.Field(typeof(Settings).Assembly.GetType("CataTweaks.Main"), "settings").GetValue(null);
        Func<bool> enabled = () => (bool)AccessTools.Property(rally, "Enabled").GetValue(null);
        settings.pooledStationConstruction = settings.shipyardRally = true;
        Assert(enabled(), "Rally is enabled when its own option and shared queues are on");
        settings.shipyardRally = false;
        Assert(!enabled(), "Rally can be disabled independently of shared queues");
        settings.pooledStationConstruction = false;
        Assert(!enabled(), "Rally remains off when both options are off");
        settings.shipyardRally = true;
        Assert(!enabled(), "The rally option cannot run without shared queues");
        var dependencies = (Dictionary<string, string>)AccessTools.Field(typeof(Settings).Assembly.GetType("CataTweaks.VanillaOptionsRows"), "DependsOn").GetValue(null);
        Assert(dependencies[nameof(Settings.shipyardRally)] == nameof(Settings.pooledStationConstruction),
            "The rally settings control is disabled while its shared-queue prerequisite is off");
        settings.pooledStationConstruction = true;
        Func<string, object[], object> invoke = (method, args) => AccessTools.Method(rally, method).Invoke(null, args);
        Action<TIGameState, int> set = (state, id) => invoke("SetTarget", new object[] { state, id });
        Func<TIGameState, int> target = state => (int)invoke("TargetID", new object[] { state });
        var hab = Bare<TIHabState>();
        var fleet = Bare<TISpaceFleetState>();
        Assert(target(hab) == 0, "Old campaigns default to no rally");
        set(hab, 42); set(fleet, 43);
        Assert(target(hab) == 42 && target(fleet) == 43, "Station selection and in-flight merge intent are independent");
        var otherCampaign = Bare<TIHabState>(); otherCampaign.ID = hab.ID;
        Assert(target(otherCampaign) == 0, "Reused state IDs cannot leak rally choices across campaigns");
        var save = fsData.CreateDictionary(); save.AsDictionary["nativeField"] = new fsData("preserved");
        invoke("WriteSave", new object[] { hab, save });
        Assert(save.AsDictionary["CataTweaks_RallyFleetID"].AsInt64 == 42 && save.AsDictionary["nativeField"].AsString == "preserved",
            "Save extension preserves native fields and records the station's selected fleet");
        invoke("ReadSave", new object[] { otherCampaign, save });
        Assert(target(otherCampaign) == 42, "Station rally selection survives serialization round trip");
        var flightSave = fsData.CreateDictionary(); invoke("WriteSave", new object[] { fleet, flightSave });
        var loadedFleet = Bare<TISpaceFleetState>(); invoke("ReadSave", new object[] { loadedFleet, flightSave });
        Assert(target(loadedFleet) == 43, "An in-progress rally retains its merge intent after loading");
        invoke("ReadSave", new object[] { otherCampaign, fsData.CreateDictionary() });
        Assert(target(otherCampaign) == 0, "Loading a save without rally metadata clears stale state");
        var malformed = fsData.CreateDictionary(); malformed.AsDictionary["CataTweaks_RallyFleetID"] = new fsData("bad");
        invoke("ReadSave", new object[] { otherCampaign, malformed });
        Assert(target(otherCampaign) == 0, "Malformed optional save metadata is ignored");
        malformed.AsDictionary["CataTweaks_RallyFleetID"] = new fsData((long)int.MaxValue + 1);
        invoke("ReadSave", new object[] { otherCampaign, malformed });
        Assert(target(otherCampaign) == 0, "Out-of-range optional fleet IDs are ignored");
        set(hab, 0);
        Assert(target(hab) == 0 && target(fleet) == 43, "Disabling future station rallies does not cancel an already dispatched ship");
        var unsetSave = fsData.CreateDictionary(); invoke("WriteSave", new object[] { hab, unsetSave });
        Assert(!unsetSave.AsDictionary.ContainsKey("CataTweaks_RallyFleetID"), "Disabled rally settings need no save field");
        var yardSave = fsData.CreateDictionary(); var yard = Bare<TIHabModuleState>(); set(yard, 44);
        invoke("WriteSave", new object[] { yard, yardSave });
        Assert(!yardSave.AsDictionary.ContainsKey("CataTweaks_RallyFleetID"), "Save extension is limited to habs and fleets");

        Func<Trajectory[], double, Trajectory> least = (routes, dv) => (Trajectory)invoke("LeastTime", new object[] { routes, dv });
        var fast = Route(1, 1500); var affordable = Route(3, 900); var slow = Route(8, 200);
        Assert(least(new[] { slow, affordable, fast }, 1000) == affordable, "Pick the fastest affordable route, not the cheapest or an over-budget route");
        Assert(least(new[] { slow, affordable, fast }, 1500) == fast, "A route at exactly the fuel budget is eligible");
        Assert(least(new[] { fast }, 1000) == null, "No rendezvous is assigned when every route exceeds available delta-v");
        Assert(least(new Trajectory[0], 1000) == null && least(null, 1000) == null, "An unsuccessful planner result leaves the ship at its yard");
        var delay = Route(2, 800, launchDays: 1);
        Assert(least(new[] { affordable, delay }, 1000) == delay, "Least time uses arrival time including launch delays");
        var staged = Route(1, 800); staged.nextTrajectory = Route(9, 0);
        Assert(least(new[] { staged, affordable }, 1000) == affordable, "Compare final arrival for multi-leg routes");
        var destructive = Route(1, 0); destructive.exitsSolarSystem = true;
        Assert(least(new[] { destructive, affordable }, 1000) == affordable, "A destructive trajectory cannot be used as a rally");
        var invalid = Route(1, double.NaN);
        Assert(least(new[] { invalid, affordable }, 1000) == affordable && least(new[] { affordable }, double.NaN) == null,
            "Invalid fuel values cannot authorize a transfer");
        var sameTimeCheaper = Route(3, 700);
        Assert(least(new[] { affordable, sameTimeCheaper }, 1000) == sameTimeCheaper, "Break equal-arrival ties by fuel cost");
        Func<TransferResult, double, double, bool> fuelFailure = (result, minimum, available) =>
            (bool)invoke("FuelFailure", new object[] { result, minimum, available });
        Assert(fuelFailure(new TransferResult(TransferResult.Outcome.Fail_InsufficientDV), -1, 1000), "Native insufficient-delta-v failure gets the fuel warning");
        Assert(fuelFailure(new TransferResult(TransferResult.Outcome.Fail_CodePathNotImplemented), 1.5, 1000), "Minimum required fuel is converted from km/s to m/s correctly");
        Assert(!fuelFailure(new TransferResult(TransferResult.Outcome.Fail_InsufficientAcceleration), -1, 1000), "A thrust failure is not mislabeled as insufficient delta-v");
        Assert(!fuelFailure(null, 1, 1000) && !fuelFailure(null, double.NaN, 1000), "Unknown or exactly affordable fuel requirements do not get a false fuel warning");

        var cancelPatch = typeof(Settings).Assembly.GetType("CataTweaks.StationConstructionRallyAbort", true);
        AccessTools.Method(cancelPatch, "Prefix").Invoke(null, new object[] { fleet });
        Assert(target(fleet) == 0, "Aborting a rally transfer cancels automatic merging");
        set(fleet, 43);
        var reroutePatch = typeof(Settings).Assembly.GetType("CataTweaks.StationConstructionRallyReroute", true);
        AccessTools.Method(reroutePatch, "Prefix").Invoke(null, new object[] { fleet, affordable });
        Assert(target(fleet) == 0, "Changing the transfer destination cancels automatic merging");
        set(fleet, 43);
        AccessTools.Field(rally, "adoptingTargetTrajectory").SetValue(null, fleet);
        AccessTools.Method(reroutePatch, "Prefix").Invoke(null, new object[] { fleet, affordable });
        Assert(target(fleet) == 43, "Adopting a moving target's onward trajectory preserves automatic merging");
        AccessTools.Field(rally, "adoptingTargetTrajectory").SetValue(null, null);
        AccessTools.Method(reroutePatch, "Prefix").Invoke(null, new object[] { fleet, affordable });
        Assert(target(fleet) == 0, "Manual rerouting still cancels merging after rendezvous adoption ends");

        var reportType = rally.GetNestedType("Report", BindingFlags.NonPublic);
        var report = Activator.CreateInstance(reportType, true);
        AccessTools.Field(reportType, "failure").SetValue(report, "Insufficient delta-v.");
        AccessTools.Field(rally, "notification").SetValue(null, report);
        var notification = new NotificationQueueItem { templateName = "LogShipComplete", itemDetail = "Native completion dialogue." };
        var eventPatch = typeof(Settings).Assembly.GetType("CataTweaks.StationConstructionRallyEvent", true);
        AccessTools.Method(eventPatch, "Prefix").Invoke(null, new object[] { notification });
        Assert(notification.itemDetail == "Insufficient delta-v.\n\nNative completion dialogue." && notification.templateName == "LogShipComplete",
            "A failed rally prepends its reason and retains the native ship-constructed dialogue");
        var unrelated = new NotificationQueueItem { templateName = "LogFleetArrival", itemDetail = "Unrelated event." };
        AccessTools.Method(eventPatch, "Prefix").Invoke(null, new object[] { unrelated });
        Assert(unrelated.itemDetail == "Unrelated event.", "Rally failure context cannot modify unrelated notifications");
        AccessTools.Field(rally, "notification").SetValue(null, null);
    }
}
