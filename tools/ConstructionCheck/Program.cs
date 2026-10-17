// Runs the real queue dispatcher against the installed game's types without starting Unity.
// Pricing, eligibility and payment are stubbed at their game boundaries. This verifies queue
// mutation and scheduling; it cannot verify prefab layout or real resource/refit calculations.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.CompilerServices;
using CataTweaks;
using HarmonyLib;
using FullSerializer;
using PavonisInteractive.TerraInvicta;
using PavonisInteractive.TerraInvicta.Actions;

internal static class Program
{
    private static Type pool;
    private static readonly Dictionary<TIHabModuleState, Yard> yards = new Dictionary<TIHabModuleState, Yard>();
    private static readonly Dictionary<TISpaceShipTemplate, int> minimumTier = new Dictionary<TISpaceShipTemplate, int>();
    private static bool afford = true, player = true, station = true;
    // Another mod's start patch: it refuses these items, or any item in these yards.
    private static readonly HashSet<ShipConstructionQueueItem> refuseItems = new HashSet<ShipConstructionQueueItem>();
    private static readonly HashSet<TIHabModuleState> refuseYards = new HashSet<TIHabModuleState>();
    private static readonly Dictionary<ShipConstructionQueueItem, int> attempts = new Dictionary<ShipConstructionQueueItem, int>();
    // A patch that pays for this item and still returns false.
    private static ShipConstructionQueueItem payThenRefuse;
    private static int day;
    private static TIFactionState owner;
    private static int checks;
    private static int solarRefreshes;
    private static bool solarIncreased;
    private static int nextID;
    private static string gameDir;
    private sealed class Yard { internal TIHabState Hab; internal int Tier; internal bool Active; internal float Days; }

    private static int Main(string[] args)
    {
        gameDir = args.Length > 0 ? args[0] : @"C:\Program Files (x86)\Steam\steamapps\common\Terra Invicta";
        string managed = Path.Combine(gameDir, "TerraInvicta_Data", "Managed");
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
    // A type of the mod, by its name without the namespace.
    private static Type Mod(string name) => typeof(Settings).Assembly.GetType("CataTweaks." + name, true);
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
    private static bool SolarRefresh(bool wasEnabled, float wasCap)
    {
        solarRefreshes++;
        var settings = (Settings)AccessTools.Field(typeof(Main), "settings").GetValue(null);
        solarIncreased = settings.solarMirrorsBoostStations
            && (!wasEnabled || settings.solarMirrorOutputCap > wasCap);
        return false;
    }
    private static bool RefitCost(TIHabModuleState __0, ref TIResourcesCost __result)
    {
        __result = Bare<TIResourcesCost>();
        __result.SetCompletionTime_Days(yards[__0].Days);
        return false;
    }
    private static bool Pay(TIFactionState __instance, TIHabModuleState __0, ref bool __result)
    {
        var head = __instance.GetShipyardQueue(__0)[0];
        attempts[head] = attempts.TryGetValue(head, out int n) ? n + 1 : 1;
        __result = afford && !refuseItems.Contains(head) && !refuseYards.Contains(__0);
        if (__result || head == payThenRefuse) head.costPaid = true;
        if (head == payThenRefuse) __result = false;
        return false;
    }
    private static bool Afford(ref bool __result) { __result = afford; return false; }
    private static bool Today(ref int __result) { __result = day; return false; }
    private static bool Owner(ref TIFactionState __result) { __result = owner; return false; }
    private static int Attempts(ShipConstructionQueueItem item) => attempts.TryGetValue(item, out int n) ? n : 0;

    private static (TIFactionState faction, TIHabState hab, TIHabModuleState high, TIHabModuleState low) Fixture()
    {
        yards.Clear(); minimumTier.Clear(); afford = player = station = true;
        refuseItems.Clear(); refuseYards.Clear(); attempts.Clear(); payThenRefuse = null;
        var faction = Bare<TIFactionState>();
        owner = faction;
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
        item.resourcesCost = Bare<TIResourcesCost>();
        faction.GetShipyardQueue(yard).Add(item);
        return item;
    }
    private static List<ShipConstructionQueueItem> View(TIFactionState faction, TIHabModuleState yard) =>
        (List<ShipConstructionQueueItem>)Call("View", faction, yard);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference AddUnloadedFaction(object cache, Type entryType)
    {
        var faction = Bare<TIFactionState>();
        AccessTools.Method(cache.GetType(), "Add").Invoke(cache,
            new[] { (object)faction, Activator.CreateInstance(entryType, true) });
        return new WeakReference(faction);
    }

    private static void CampaignRegressionChecks()
    {
        var settings = new Settings();
        AccessTools.Field(typeof(Main), "settings").SetValue(null, settings);
        var refresh = AccessTools.Method(Mod("StationSolarMirrorRefresh"), "RefreshAll");
        var solarHarness = new Harmony("CataTweaks.SolarSettingsCheck");
        Stub(solarHarness, refresh, nameof(SolarRefresh));
        Action applySolar = () => AccessTools.Method(typeof(Settings), "ApplySolarOptions").Invoke(settings, null);
        Action rememberSolar = () => AccessTools.Method(typeof(Settings), "RememberSolarOptions").Invoke(settings, null);
        try
        {
            rememberSolar();
            applySolar();
            Assert(solarRefreshes == 0, "An unrelated settings change cannot reactivate station modules");
            settings.solarMirrorOutputCap = 5f;
            applySolar();
            Assert(solarRefreshes == 0, "Changing the cap while mirrors are disabled does not rebalance stations");
            settings.solarMirrorsBoostStations = true;
            applySolar();
            Assert(solarRefreshes == 1 && solarIncreased, "Enabling mirrors immediately rebalances increased supply");
            settings.solarMirrorOutputCap = 2f;
            applySolar();
            Assert(solarRefreshes == 2 && !solarIncreased, "Lowering the live cap rebalances decreased supply");
            settings.solarMirrorOutputCap = 4f;
            applySolar();
            Assert(solarRefreshes == 3 && solarIncreased, "Increasing the live cap rebalances increased supply");
            settings.solarMirrorsBoostStations = false;
            applySolar();
            Assert(solarRefreshes == 4 && !solarIncreased, "Disabling mirrors immediately removes phantom power");
            applySolar();
            Assert(solarRefreshes == 4, "Repeated saves with unchanged options do not change power flags");
            settings.solarMirrorsBoostStations = true;
            rememberSolar();
            applySolar();
            Assert(solarRefreshes == 4, "Loaded solar options establish a baseline without reactivating modules");
        }
        finally { solarHarness.Unpatch(refresh, HarmonyPatchType.Prefix, solarHarness.Id); }
        var flags = Mod("CampaignFlags");
        var customizations = Bare<ScenarioCustomizations>();
        Action freeze = () => AccessTools.Method(flags, "FreezeDefaults").Invoke(null, new object[] { customizations });
        freeze();
        Assert(customizations.customFactionText["CataTweaks.restoredEmpires"].customDisplayName == "1",
            "A legacy campaign adopts the current empire default once");
        settings.restoredEmpires = false;
        settings.spySlotsAsCouncilSlots = !settings.spySlotsAsCouncilSlots;
        freeze();
        Assert(customizations.customFactionText["CataTweaks.restoredEmpires"].customDisplayName == "1",
            "Changing new-campaign defaults cannot change the loaded campaign");
        AccessTools.Method(flags, "Write").Invoke(null, new object[] { customizations, "CataTweaks.restoredEmpires", false });
        freeze();
        Assert(customizations.customFactionText["CataTweaks.restoredEmpires"].customDisplayName == "0",
            "An explicitly disabled campaign option survives migration");

        var cap = Mod("RepeatableCapScalesWithCost");
        var entryType = cap.GetNestedType("Cached", BindingFlags.NonPublic);
        var cacheField = AccessTools.Field(cap, "cache");
        var cache = cacheField.GetValue(null);
        var faction = Bare<TIFactionState>();
        AccessTools.Field(typeof(TIGameState), "template").SetValue(faction, Bare<TIFactionTemplate>());
        faction.completedProjects = new List<TIProjectTemplate>();
        var entry = Activator.CreateInstance(entryType, true);
        AccessTools.Field(entryType, "completedCount").SetValue(entry, 0);
        AccessTools.Field(entryType, "rate").SetValue(entry, 0.03f);
        AccessTools.Field(entryType, "extra").SetValue(entry, 37f);
        AccessTools.Method(cache.GetType(), "Add").Invoke(cache, new[] { (object)faction, entry });
        Func<float> query = () =>
        {
            object[] args = { faction, 100f };
            AccessTools.Method(cap, "Postfix").Invoke(null, args);
            return (float)args[1];
        };
        settings.repeatableProjectScaling = 0.03f;
        Assert(query() == 137f, "An unchanged scaling rate reuses the capacity bonus");
        settings.repeatableProjectScaling = 0.06f;
        Assert(query() == 100f, "A changed scaling rate immediately discards a stale capacity bonus");
        settings.repeatableProjectScaling = 0f;
        Assert(query() == 100f, "Disabling scaling restores base capacity");
        var weak = AddUnloadedFaction(cache, entryType);
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        Assert(!weak.IsAlive, "Capacity cache does not keep an unloaded faction alive");
        var presets = Mod("ReapplyPresetsOnValidityChange");
        var validity = (System.Collections.IDictionary)AccessTools.Field(presets, "lastInvalid").GetValue(null);
        validity[new GameStateID(999)] = new HashSet<PriorityType>();
        AccessTools.Method(presets, "Reset").Invoke(null, null);
        Assert(validity.Count == 0, "Campaign reset removes validity snapshots before IDs can be reused");
        var mirrorOutput = Mod("StationSolarMirrorOutput");
        Func<int, int, float, int> lit = (unlit, bonus, limit) =>
            (int)AccessTools.Method(mirrorOutput, "WithMirrors").Invoke(null, new object[] { unlit, bonus, limit });
        Assert(lit(40, 1, 2f) == 41, "Mirror cap uses distance-adjusted output and cannot reduce natural output");
        Assert(lit(4, 100, 2f) == 8, "Mirror cap remains relative to a distant station's solar output");
        Assert(lit(40, 100, 1f) == 40, "A one-times cap leaves an unaffected station's supply unchanged");

        var identity = Mod("DormantCapitalIdentity");
        var pending = (HashSet<TINationState>)AccessTools.Field(identity, "pending").GetValue(null);
        var nation = Bare<TINationState>();
        pending.Add(nation);
        var save = fsData.CreateDictionary();
        save.AsDictionary["nativeField"] = new fsData("preserved");
        AccessTools.Method(identity, "WriteSave").Invoke(null, new object[] { nation, save });
        Assert(save.AsDictionary["nativeField"].AsString == "preserved",
            "Pending identity metadata preserves native nation fields");
        AccessTools.Method(identity, "Reset").Invoke(null, null);
        var loadedNation = Bare<TINationState>();
        AccessTools.Method(identity, "ReadSave").Invoke(null, new object[] { loadedNation, save });
        Assert(pending.Contains(loadedNation) && !pending.Contains(nation),
            "A reload restores pending identity conversion onto the loaded nation");
        AccessTools.Method(identity, "ReadSave").Invoke(null, new object[] { loadedNation, fsData.CreateDictionary() });
        Assert(!pending.Contains(loadedNation), "A save without pending identity metadata does not queue conversion");
        save.AsDictionary["CataTweaks_DormantCapitalPending"] = new fsData("invalid");
        AccessTools.Method(identity, "ReadSave").Invoke(null, new object[] { loadedNation, save });
        Assert(!pending.Contains(loadedNation), "Malformed pending identity metadata is ignored");
    }

    private static void Run()
    {
        // Faction and nation static initializers query these missions even though the dispatcher
        // never uses them. Seed the lookup before Harmony initializes either game type.
        var manager = Bare<TemplateManager>();
        var missions = new Dictionary<string, Dictionary<Type, TIDataTemplate>>();
        foreach (string name in new[] { "PassTechnology", "Contact", "Protect", "SetNationalPolicy" })
        {
            var mission = Bare<TIMissionTemplate>();
            mission.cost = new TIMissionCost_Flat { resourceType = FactionResource.Influence, value = 1f };
            AccessTools.Property(typeof(TIDataTemplate), "dataName").SetValue(mission, name);
            missions[name] = new Dictionary<Type, TIDataTemplate> { [typeof(TIMissionTemplate)] = mission };
        }
        AccessTools.Field(typeof(TemplateManager), "templatesByName").SetValue(manager, missions);
        AccessTools.Field(typeof(TemplateManager), "self").SetValue(null, manager);
        CampaignRegressionChecks();
        pool = Mod("StationConstruction");
        var harmony = new Harmony("CataTweaks.ConstructionCheck");
        // Unity's Mono implements engine ECalls that desktop CLR cannot JIT. Check target
        // existence here; attaching UI/completion patches must be smoke-tested inside Unity.
        foreach (var type in typeof(Settings).Assembly.GetTypes().Where(t => (t.Name.StartsWith("StationConstruction") || t.Name == "FleetManagerCommission")
            && t.GetCustomAttributes(typeof(HarmonyPatch), false).Length > 0))
        {
            var targetMethods = AccessTools.Method(type, "TargetMethods");
            var targets = targetMethods != null ? (IEnumerable<MethodBase>)targetMethods.Invoke(null, null)
                : type.GetCustomAttributes(typeof(HarmonyPatch), false).Cast<HarmonyPatch>()
                    .Select(p => (MethodBase)AccessTools.Method(p.info.declaringType, p.info.methodName));
            Assert(targets.All(m => m != null), type.Name + " has valid game patch targets");
        }
        Func<string, string, int> priority = (type, method) => ((HarmonyPriority)AccessTools.Method(
            Mod(type), method).GetCustomAttributes(typeof(HarmonyPriority), false)
            .Single()).info.priority;
        Assert(priority("StationConstructionRallyCompletion", "Postfix") == Priority.Last,
            "The rally merge runs after Fleet Manager's completion postfix has found the new fleet");
        Assert(priority("StationConstructionAdd", "Postfix") == Priority.First,
            "The shared queue's enqueue result is final before other mods' postfixes read it");
        Assert(priority("FleetManagerCommission", "Prefix") == Priority.Last,
            "The commission claim runs after every prefix that may cancel the completion");
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
        Stub(harmony, AccessTools.Method(typeof(TIResourcesCost), "CanAfford",
            new[] { typeof(TIFactionState), typeof(float), typeof(List<FactionResource>), typeof(float) }), nameof(Afford));
        Stub(harmony, AccessTools.Method(pool, "Today"), nameof(Today));
        Stub(harmony, AccessTools.PropertyGetter(typeof(TIHabModuleState), "ref_faction"), nameof(Owner));

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

        f = Fixture(); a = Enqueue(f.faction, f.high); b = Enqueue(f.faction, f.high);
        refuseItems.Add(a);
        Call("Dispatch", f.faction, f.hab);
        Assert(!a.costPaid && b.costPaid, "An item refused by another patch does not block the ships behind it");
        Assert(Attempts(a) == 2, "A refused item is tried once in each idle yard");
        Assert(View(f.faction, f.high).SequenceEqual(new[] { b, a }) && a.shipyard == f.high, "A refused item keeps its place at the head of the waiting tail");
        Call("Dispatch", f.faction, f.hab);
        Assert(Attempts(a) == 2, "A refused item is not tried again on the same day");
        refuseItems.Clear(); day++;
        Call("Dispatch", f.faction, f.hab);
        Assert(a.costPaid && a.shipyard == f.low, "A refused item that becomes valid starts on the next day");

        f = Fixture(); a = Enqueue(f.faction, f.high); b = Enqueue(f.faction, f.high);
        refuseYards.Add(f.high);
        Call("Dispatch", f.faction, f.hab);
        Assert(a.costPaid && a.shipyard == f.low && !b.costPaid, "A yard that refuses does not stop an item starting in another yard");
        int tries = Attempts(b);
        Call("Dispatch", f.faction, f.hab);
        Assert(tries == 1 && Attempts(b) == 1, "A refusing yard is not tried again for the same item on the same day");

        f = Fixture(); a = Enqueue(f.faction, f.high); b = Enqueue(f.faction, f.high);
        payThenRefuse = a;
        Call("Dispatch", f.faction, f.hab);
        Assert(Attempts(a) == 1 && a.costPaid && a.shipyard == f.high, "An item paid by a start that returned false is not tried in another yard");
        Assert(b.costPaid && b.shipyard == f.low, "A yard whose start was paid is no longer idle");
        Assert(!(bool)Call("Refused", a, f.high), "A paid item is not recorded as refused");

        f = Fixture(); a = Enqueue(f.faction, f.high, paid: true); a.daysToCompletion = 0f;
        var finish = AccessTools.Method(pool, "FinishTime");
        var design = Enqueue(f.faction, f.low).shipDesign; f.faction.GetShipyardQueue(f.low).Clear();
        Assert((float)finish.Invoke(null, new object[] { f.faction, f.high, design }) == 11f,
            "A paid job held at zero days keeps its yard busy for at least one day");

        var add = Mod("StationConstructionAdd");
        var addPrefix = AccessTools.Method(add, "Prefix");
        var addPostfix = AccessTools.Method(add, "Postfix");
        f = Fixture(); afford = false; b = Enqueue(f.faction, f.high);
        // Harmony skips the prefix after an earlier prefix refuses, so __state keeps its default.
        var post = new object[] { f.faction, f.low, 0, false, false };
        addPostfix.Invoke(null, post);
        Assert(!(bool)post[4], "An enqueue refused before this prefix ran keeps its false result");
        post = new object[] { f.faction, f.low, 0, true, false };
        addPostfix.Invoke(null, post);
        Assert(!(bool)post[4], "A skipped prefix is never read as an empty station queue");
        var addArgs = new object[] { f.faction, f.low, null };
        addPrefix.Invoke(null, addArgs);
        post = new object[] { f.faction, f.low, addArgs[2], false, false };
        addPostfix.Invoke(null, post);
        Assert(!(bool)post[4], "An enqueue refused after this prefix ran keeps its false result");
        afford = true;
        addPrefix.Invoke(null, addArgs);
        a = Enqueue(f.faction, f.low);
        post = new object[] { f.faction, f.low, addArgs[2], true, false };
        addPostfix.Invoke(null, post);
        Assert((bool)post[4] && a.costPaid && b.costPaid && a.shipyard != b.shipyard,
            "A direct enqueue into any yard succeeds and joins the shared queue");
        Assert(priority("StationConstructionStart", "Prefix") == Priority.First, "The start block runs before other mods' start prefixes");

        f = Fixture();
        var action = Bare<AddShipDesignToConstructionQueueAction>();
        var actionYard = AccessTools.Field(typeof(AddShipDesignToConstructionQueueAction), "shipyard");
        actionYard.SetValue(action, f.low);
        var addYard = AccessTools.Method(Mod("StationConstructionAddYard"), "Prefix");
        addYard.Invoke(null, new object[] { action });
        Assert((TIHabModuleState)actionYard.GetValue(action) == f.high, "The add action targets the anchor yard before any enqueue prefix runs");
        station = false; actionYard.SetValue(action, f.low);
        addYard.Invoke(null, new object[] { action });
        Assert((TIHabModuleState)actionYard.GetValue(action) == f.low, "Bases keep the chosen yard");

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
        var lostYard = Mod("StationConstructionLostYard");
        AccessTools.Method(lostYard, "Prefix").Invoke(null, new object[] { f.faction, f.high });
        Assert(f.faction.GetShipyardQueue(f.high).SequenceEqual(new[] { a }), "Lost yard retains only its paid construction for vanilla cancellation");
        Assert(f.faction.GetShipyardQueue(f.low).SequenceEqual(new[] { b, c }) && b.shipyard == f.low && c.shipyard == f.low,
            "Lost yard preserves pending items and their ownership in order");

        f = Fixture(); a = Enqueue(f.faction, f.high);
        var settings = (Settings)AccessTools.Field(Mod("Main"), "settings").GetValue(null);
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

        var progress = AccessTools.Method(Mod("StationConstructionProgress"), "Fraction");
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

        var sectionHeight = AccessTools.Method(Mod("StationConstructionSlots"), "BuildingHeight");
        Func<int, float, float, float> height = (count, row, available) =>
            (float)sectionHeight.Invoke(null, new object[] { count, row, available });
        Assert(height(1, 24f, 240f) == 24f, "One slot uses one full-height construction row");
        Assert(height(4, 24f, 240f) == 96f, "Currently Building grows with slot count");
        Assert(height(20, 24f, 240f) == 192f, "Large slot counts preserve two waiting queue rows");
        Assert(height(20, 24f, 240f) == height(8, 24f, 240f), "Overflow slots scroll instead of expanding outside the card");
        Assert(height(2, 24f, 20f) == 10f, "Small available areas preserve space for both sections");
        Assert(height(2, 24f, 0f) == 0f, "Not-yet-sized card geometry is safe");

        var gridType = Mod("StationConstructionGrid");
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
        var tooltipJob = AccessTools.Method(Mod("StationConstructionHabTooltip"), "CurrentJob");
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
        CheckFleetManager();
        CheckCapitalClaims();
        CheckSpaceNations();
        Console.WriteLine(checks + " campaign, patch target, scheduler, tooltip, rally, Fleet Manager, capital claim and Space Nations checks passed.");
    }

    private static Trajectory Route(int arrivalDays, double dv, int launchDays = 0)
    {
        var route = Bare<Trajectory_Impulse>();
        var start = new DateTime(2112, 1, 1);
        AccessTools.PropertySetter(typeof(Trajectory), "launchTime").Invoke(route, new object[] { new TIDateTime(start.AddDays(launchDays)) });
        AccessTools.PropertySetter(typeof(Trajectory), "arrivalTime").Invoke(route, new object[] { new TIDateTime(start.AddDays(arrivalDays)) });
        AccessTools.PropertySetter(typeof(Trajectory), "boostDV_mps").Invoke(route, new object[] { dv });
        return route;
    }

    private static void CheckRally()
    {
        var rally = Mod("StationConstructionRally");
        var settings = (Settings)AccessTools.Field(Mod("Main"), "settings").GetValue(null);
        Func<bool> enabled = () => (bool)AccessTools.Property(rally, "Enabled").GetValue(null);
        settings.pooledStationConstruction = settings.shipyardRally = true;
        Assert(enabled(), "Rally is enabled when its own option and shared queues are on");
        settings.shipyardRally = false;
        Assert(!enabled(), "Rally can be disabled independently of shared queues");
        settings.pooledStationConstruction = false;
        Assert(!enabled(), "Rally remains off when both options are off");
        settings.shipyardRally = true;
        Assert(!enabled(), "The rally option cannot run without shared queues");
        var dependencies = (Dictionary<string, string>)AccessTools.Field(Mod("VanillaOptionsRows"), "DependsOn").GetValue(null);
        Assert(dependencies[nameof(Settings.shipyardRally)] == nameof(Settings.pooledStationConstruction),
            "The rally settings control is disabled while its shared-queue prerequisite is off");
        settings.pooledStationConstruction = true;
        Func<string, object[], object> invoke = RallyCall;
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

        var cancelPatch = Mod("StationConstructionRallyAbort");
        AccessTools.Method(cancelPatch, "Prefix").Invoke(null, new object[] { fleet });
        Assert(target(fleet) == 0, "Aborting a rally transfer cancels automatic merging");
        set(fleet, 43);
        var reroutePatch = Mod("StationConstructionRallyReroute");
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
        var eventPatch = Mod("StationConstructionRallyEvent");
        AccessTools.Method(eventPatch, "Prefix").Invoke(null, new object[] { notification });
        Assert(notification.itemDetail == "Insufficient delta-v.\n\nNative completion dialogue." && notification.templateName == "LogShipComplete",
            "A failed rally prepends its reason and retains the native ship-constructed dialogue");
        var unrelated = new NotificationQueueItem { templateName = "LogFleetArrival", itemDetail = "Unrelated event." };
        AccessTools.Method(eventPatch, "Prefix").Invoke(null, new object[] { unrelated });
        Assert(unrelated.itemDetail == "Unrelated event.", "Rally failure context cannot modify unrelated notifications");
        AccessTools.Field(rally, "notification").SetValue(null, null);
    }
    private static int notices;
    private static bool Noticed() { notices++; return false; }
    private static void Throws() => throw new InvalidOperationException("notice");
    private static IEnumerable UnreadableOrders(bool allowChild) => throw new InvalidOperationException("orders");
    private static bool CommissionLine(ref string __result)
    {
        __result = "commission line";
        return false;
    }
    // A Fleet Manager lookup that throws, as an ambiguous overload does.
    private sealed class ThrowingAssembly : Assembly
    {
        public override Type GetType(string name, bool throwOnError) => throw new AmbiguousMatchException(name);
    }
    // The stand-ins, except for a queue estimate that Harmony cannot patch.
    private abstract class UnpatchableCommission
    {
        protected abstract float SumQueueDays(TIFactionState faction, TIHabModuleState shipyard);
    }
    private sealed class UnpatchableAssembly : Assembly
    {
        public override Type GetType(string name, bool throwOnError) => name == "TIFleetRework.FleetCommissionManager"
            ? typeof(UnpatchableCommission) : typeof(TIFleetRework.MPBridge).Assembly.GetType(name, throwOnError);
    }

    private static void CheckFleetManager()
    {
        var compat = Mod("FleetManagerCompat");
        var rally = Mod("StationConstructionRally");
        var settings = (Settings)AccessTools.Field(Mod("Main"), "settings").GetValue(null);
        var failedParts = (ICollection<string>)AccessTools.Field(compat, "failed").GetValue(null);
        Action reset = () =>
        {
            foreach (string flag in new[] { "bound", "bindFailed" }) AccessTools.Field(compat, flag).SetValue(null, false);
            failedParts.Clear();
        };
        Func<string, bool> flagOf = name => (bool)AccessTools.Field(compat, name).GetValue(null);
        Action<Harmony, Assembly> bind = (harmony, source) => AccessTools.Method(compat, "Bind").Invoke(null, new object[] { harmony, source });

        // Order selection, without Fleet Manager.
        var orderType = compat.GetNestedType("Order", BindingFlags.NonPublic | BindingFlags.Public);
        var orderList = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(orderType));
        Func<int, int, string, object> order = (id, yard, design) =>
        {
            var o = Activator.CreateInstance(orderType, true);
            AccessTools.Field(orderType, "id").SetValue(o, id);
            AccessTools.Field(orderType, "yard").SetValue(o, yard);
            AccessTools.Field(orderType, "design").SetValue(o, design);
            return o;
        };
        Func<object, int> idOf = o => o == null ? 0 : (int)AccessTools.Field(orderType, "id").GetValue(o);
        var station = new HashSet<int> { 1, 2, 3 }; // Yard 9 is on another station.
        Func<string, int> pick = design => idOf(AccessTools.Method(compat, "Pick").Invoke(null, new object[] { orderList, design, station }));
        Assert(pick("Frigate") == 0, "No open order means no commission claim");
        orderList.Add(order(7, 2, "Frigate"));
        orderList.Add(order(5, 3, "Frigate"));
        orderList.Add(order(3, 9, "Frigate"));
        orderList.Add(order(2, 1, "Cruiser"));
        Assert(pick("Frigate") == 5, "The oldest order of the design on the station is filled first, whatever its yard");
        orderList.Add(order(4, 1, "Frigate"));
        Assert(pick("Frigate") == 4, "Orders from several fleets for one design are filled oldest first");
        Assert(pick("Destroyer") == 0, "Orders for another design are not claimed");

        Assert(!(bool)AccessTools.Method(compat, "InReserve").Invoke(null, new object[] { Bare<TISpaceFleetState>() }),
            "Without Fleet Manager the bridge is off and no fleet counts as reserve");
        bind(null, new ThrowingAssembly());
        Assert(!flagOf("bound") && flagOf("bindFailed"), "A lookup that throws, such as an ambiguous overload, turns the bridge off instead of failing");
        reset();
        bind(null, typeof(object).Assembly);
        Assert(!flagOf("bound") && flagOf("bindFailed"), "Missing Fleet Manager types turn the bridge off instead of failing");
        reset();
        var unpatchable = new Harmony("CataTweaks.ConstructionCheck.Unpatchable");
        bind(unpatchable, new UnpatchableAssembly());
        var badTarget = AccessTools.Method(typeof(UnpatchableCommission), "SumQueueDays");
        Assert(!flagOf("bound") && flagOf("bindFailed") && (Harmony.GetPatchInfo(badTarget)?.Postfixes.Count ?? 0) == 0,
            "A queue estimate that cannot be patched turns the bridge off and leaves nothing patched");
        reset();

        // The bridge against stand-ins with Fleet Manager's names and signatures.
        var stubs = new Harmony("CataTweaks.ConstructionCheck.FleetManagerStubs");
        bind(stubs, typeof(TIFleetRework.MPBridge).Assembly);
        Assert(flagOf("bound"), "The bridge binds to members with Fleet Manager's signatures");
        var states = (Dictionary<Type, Dictionary<GameStateID, TIGameState>>)AccessTools.Field(typeof(GameStateManager), "gamestates").GetValue(null);
        var registered = new Dictionary<GameStateID, TIGameState>();
        states[typeof(TIFleetRework.TIFleetCommissionOrder)] = registered;
        var f = Fixture();
        Func<TIHabModuleState, string, TISpaceFleetState, TIFleetRework.TIFleetCommissionOrder> commission = (yard, design, target) =>
        {
            var o = Bare<TIFleetRework.TIFleetCommissionOrder>();
            o.shipyardID = yard.ID; o.shipTemplateDataName = design; o.target = target; o.faction = f.faction;
            registered[o.ID] = o;
            return o;
        };
        settings.pooledStationConstruction = true;
        var fleetA = Bare<TISpaceFleetState>(); var fleetB = Bare<TISpaceFleetState>();
        var older = commission(f.high, "Frigate", fleetA);
        var newer = commission(f.low, "Frigate", fleetB);
        var built = Bare<ShipConstructionQueueItem>(); built.shipDesignTemplateName = "Frigate";
        Func<TIHabModuleState, ShipConstructionQueueItem, (TISpaceFleetState fleet, bool held)> claimHeld = (yard, item) =>
        {
            var args = new object[] { f.faction, yard, item, false };
            var fleet = (TISpaceFleetState)AccessTools.Method(compat, "ClaimOrder").Invoke(null, args);
            return (fleet, (bool)args[3]);
        };
        Func<TIHabModuleState, TISpaceFleetState> claim = yard => claimHeld(yard, built).fleet;
        TIFleetRework.MPBridge.pushes.Clear();
        Assert(claim(f.low) == fleetA && older.shipyardID == f.low.ID && newer.shipyardID == f.low.ID,
            "A ship from the shared queue claims the oldest station order, which moves to the building yard");
        Assert(TIFleetRework.MPBridge.pushes.Count == 1 && TIFleetRework.MPBridge.pushes[0].Any(i => (int)i == (int)older.ID),
            "A moved order is sent to multiplayer clients");
        older.deliveredShipID = new GameStateID(999);
        Assert(claim(f.low) == fleetB, "A delivered order is not claimed again");
        newer.faction = Bare<TIFactionState>();
        Assert(claim(f.low) == fleetB, "Like Fleet Manager's FindNextOrder, an order is filled whatever its faction");
        newer.faction = null;
        Assert(claim(f.low) == fleetB, "An order with no faction is still filled");
        registered.Clear();
        var dead = commission(f.high, "Frigate", fleetB);
        var live = commission(f.high, "Frigate", fleetA);
        AccessTools.Property(typeof(TIGameState), "archived").SetValue(fleetB, true);
        Assert(claim(f.low) == null && dead.shipyardID == f.low.ID && live.shipyardID == f.high.ID,
            "When the oldest order's fleet is gone, that order goes to the building yard for Fleet Manager to close, and the rally keeps the ship");
        registered.Clear();
        var later = commission(f.high, "Frigate", fleetA);
        settings.pooledStationConstruction = false;
        TIFleetRework.MPBridge.pushes.Clear();
        Assert(claim(f.low) == null && later.shipyardID == f.high.ID && TIFleetRework.MPBridge.pushes.Count == 0,
            "Without the shared queue CataTweaks does not touch orders");
        settings.pooledStationConstruction = true;
        TIFleetRework.MPBridge.remote = true;
        Assert(claim(f.low) == null && later.shipyardID == f.high.ID && TIFleetRework.MPBridge.pushes.Count == 0,
            "A remote client follows Fleet Manager's own rule: the station's oldest order on another yard is not this ship's");
        var remoteFleet = Bare<TISpaceFleetState>();
        var atYard = commission(f.low, "Frigate", remoteFleet);
        Assert(claim(f.low) == remoteFleet && later.shipyardID == f.high.ID && atYard.shipyardID == f.low.ID
            && TIFleetRework.MPBridge.pushes.Count == 0,
            "A remote client reads the oldest order on the building yard, which the host's Fleet Manager fills, and moves nothing");
        TIFleetRework.MPBridge.remote = false;
        registered.Remove(atYard.ID);
        built.isRefit = true;
        Assert(claim(f.low) == null && later.shipyardID == f.high.ID, "A refit claims no commission order");
        built.isRefit = false;
        TIFleetRework.FleetRegistry.throwReserve = true;
        Assert(!(bool)AccessTools.Method(compat, "InReserve").Invoke(null, new object[] { fleetA }) && claim(f.low) == fleetA
            && failedParts.Count == 1 && failedParts.Contains("reserve check"),
            "A failed reserve check turns off only the reserve check; commission orders keep working");
        TIFleetRework.FleetRegistry.throwReserve = false;
        failedParts.Clear();

        // The commission line on the completion notice.
        Stub(stubs, AccessTools.Method(rally, "Commissioned"), nameof(CommissionLine));
        registered.Clear();
        f = Fixture();
        settings.shipyardRally = true;
        var commissionFleet = Bare<TISpaceFleetState>();
        commission(f.high, "Frigate", commissionFleet);
        var head = Enqueue(f.faction, f.low, paid: true); head.shipDesignTemplateName = "Frigate";
        RallyCall("SetTarget", f.hab, 4242);
        var claimPatch = Mod("FleetManagerCommission");
        var claimArgs = new object[] { f.faction, f.low, false, null, null };
        AccessTools.Method(claimPatch, "Prefix").Invoke(null, claimArgs);
        Assert((TISpaceFleetState)AccessTools.Field(claimPatch, "fleet").GetValue(null) == commissionFleet,
            "The completion prefix claims the order for the queue head");
        AccessTools.Field(rally, "completingYard").SetValue(null, f.low);
        var noticeArgs = new object[] { Bare<TISpaceShipState>(), false, null };
        var notification = AccessTools.Method(Mod("StationConstructionRallyNotification"), "Prefix");
        notification.Invoke(null, noticeArgs);
        var report = AccessTools.Field(rally, "notification").GetValue(null);
        var reportType = rally.GetNestedType("Report", BindingFlags.NonPublic);
        Assert(report != null && !(bool)AccessTools.Field(reportType, "started").GetValue(report)
            && (string)AccessTools.Field(reportType, "failure").GetValue(report) == "commission line",
            "A commissioned ship does not rally, and its completion notice gets the commission line");
        AccessTools.Method(claimPatch, "Finalizer").Invoke(null, new object[] { claimArgs[4] });
        Assert(AccessTools.Field(claimPatch, "fleet").GetValue(null) == null, "The finalizer restores the claim");
        AccessTools.Method(claimPatch, "Finalizer").Invoke(null, new object[] { null });
        AccessTools.Field(rally, "notification").SetValue(null, null);
        var thrower = new Harmony("CataTweaks.ConstructionCheck.NoticeThrows");
        thrower.Patch(AccessTools.Method(rally, "Commissioned"), prefix: new HarmonyMethod(typeof(Program), nameof(Throws)));
        AccessTools.Field(claimPatch, "fleet").SetValue(null, commissionFleet);
        notification.Invoke(null, new object[] { Bare<TISpaceShipState>(), false, null });
        Assert(AccessTools.Field(rally, "notification").GetValue(null) == null,
            "An error in the commission line throws nothing into the game and the ship still does not rally");
        thrower.UnpatchAll(thrower.Id);
        AccessTools.Field(claimPatch, "fleet").SetValue(null, null);
        // With the order work off, the rally holds back ships of a design with an open order.
        failedParts.Add("commission orders");
        claimArgs = new object[] { f.faction, f.low, false, null, null };
        AccessTools.Method(claimPatch, "Prefix").Invoke(null, claimArgs);
        Assert(AccessTools.Field(claimPatch, "fleet").GetValue(null) == null && (bool)AccessTools.Field(claimPatch, "held").GetValue(null),
            "With the order work off, a ship of a design with an open order is held back from the rally");
        notification.Invoke(null, new object[] { Bare<TISpaceShipState>(), false, null });
        Assert(AccessTools.Field(rally, "notification").GetValue(null) == null, "A held ship does not rally");
        AccessTools.Method(claimPatch, "Finalizer").Invoke(null, new object[] { claimArgs[4] });
        Assert(!(bool)AccessTools.Field(claimPatch, "held").GetValue(null), "The finalizer restores the hold");
        var other = Bare<ShipConstructionQueueItem>(); other.shipDesignTemplateName = "Destroyer";
        Assert(!claimHeld(f.low, other).held, "With the order work off, a design with no open order is not held back");
        var elsewhere = Bare<ShipConstructionQueueItem>(); elsewhere.shipDesignTemplateName = "Cruiser";
        commission(f.high, "Cruiser", commissionFleet);
        Assert(!claimHeld(f.low, elsewhere).held,
            "With the order work off, an order on another yard does not hold the ship: Fleet Manager fills only the building yard's");
        Assert(claimHeld(f.low, head).held, "With the order work off, an order on the building yard holds the ship");
        var ordersField = AccessTools.Field(compat, "orders");
        var readOrders = ordersField.GetValue(null);
        ordersField.SetValue(null, AccessTools.Method(typeof(Program), nameof(UnreadableOrders)));
        Assert(claimHeld(f.low, other).held, "When the orders cannot be read, every new ship is held back");
        ordersField.SetValue(null, readOrders);
        failedParts.Clear();
        var unknownYard = Bare<TIHabModuleState>(); // Not in the fixture's yards: reading its hab throws.
        f.faction.nShipyardQueues[unknownYard] = new List<ShipConstructionQueueItem>();
        commission(unknownYard, "Destroyer", commissionFleet);
        var result = claimHeld(unknownYard, other);
        Assert(result.fleet == null && result.held && failedParts.Contains("commission orders"),
            "An error inside the claim turns the order work off and Fleet Manager's own rule holds the ship; it never leads to a rally");
        f.faction.nShipyardQueues.Remove(unknownYard);
        failedParts.Clear();
        Assert(!claimHeld(f.low, other).held, "With the order work on, nothing is held back");
        reset();
        AccessTools.Field(compat, "bindFailed").SetValue(null, true);
        var unbound = claimHeld(f.low, other);
        Assert(unbound.fleet == null && unbound.held, "When Fleet Manager is there but the bind failed, every new ship is held back");
        built.isRefit = true;
        Assert(!claimHeld(f.low, built).held, "A refit is never held back by an order");
        built.isRefit = false;
        var refit = Bare<ShipConstructionQueueItem>(); refit.isRefit = true; refit.originalSpaceShipState = Bare<TISpaceShipState>();
        var refitFleet = Bare<TISpaceFleetState>(); refit.originalSpaceShipState.fleet = refitFleet;
        Assert(claimHeld(f.low, refit).held, "When the bind failed, a refit is held back too");
        reset();
        Assert(!claimHeld(f.low, refit).held, "Without Fleet Manager no refit is held back");
        bind(stubs, typeof(TIFleetRework.MPBridge).Assembly);
        TIFleetRework.FleetRegistry.tracked.Add(refit.originalSpaceShipState);
        Assert(!claimHeld(f.low, refit).held,
            "A refit of a ship in a division of a fleet outside the reserve may rally: Fleet Manager does not keep the refitted ship");
        TIFleetRework.FleetRegistry.reserveOrders.Add(refitFleet);
        Assert(claimHeld(f.low, refit).held,
            "A refit of a ship in a reserve fleet is held back from the rally, as Fleet Manager keeps it in the fleet's division");
        var notRefit = Bare<ShipConstructionQueueItem>(); notRefit.originalSpaceShipState = refit.originalSpaceShipState;
        Assert(!claimHeld(f.low, notRefit).held,
            "A new ship is never held as a reserve refit, even with an old ship from a reserve fleet on the item");
        var gone = Bare<ShipConstructionQueueItem>(); gone.isRefit = true; gone.originalSpaceShipState = Bare<TISpaceShipState>();
        gone.originalSpaceShipState.fleet = refitFleet;
        AccessTools.Property(typeof(TIGameState), "archived").SetValue(gone.originalSpaceShipState, true);
        Assert(!claimHeld(f.low, gone).held,
            "A refit whose old ship is archived is not held, as Fleet Manager's refit patch skips it");
        var orphan = Bare<ShipConstructionQueueItem>(); orphan.isRefit = true;
        Assert(!claimHeld(f.low, orphan).held,
            "A refit with no old ship is not held, as Fleet Manager's refit patch has nothing to keep");
        var refitHead = Enqueue(f.faction, f.low, paid: true); refitHead.isRefit = true;
        refitHead.originalSpaceShipState = refit.originalSpaceShipState;
        f.faction.GetShipyardQueue(f.low).Remove(refitHead); f.faction.GetShipyardQueue(f.low).Insert(0, refitHead);
        var refitArgs = new object[] { f.faction, f.low, false, null, null };
        AccessTools.Method(claimPatch, "Prefix").Invoke(null, refitArgs);
        Assert((bool)AccessTools.Field(claimPatch, "held").GetValue(null), "The completion prefix holds that refit");
        AccessTools.Method(claimPatch, "Finalizer").Invoke(null, new object[] { refitArgs[4] });
        f.faction.GetShipyardQueue(f.low).Remove(refitHead);
        TIFleetRework.FleetRegistry.tracked.Clear();
        TIFleetRework.FleetRegistry.reserveOrders.Clear();
        TIFleetRework.FleetRegistry.throwOrder = true;
        TIFleetRework.FleetRegistry.reserve.Add(refitFleet);
        Assert(claimHeld(f.low, refit).held && failedParts.Contains("reserve refit check")
            && !failedParts.Contains("reserve check") && (bool)AccessTools.Method(compat, "InReserve").Invoke(null, new object[] { refitFleet }),
            "When the reserve order cannot be read, the refit is held back, and the rally picker's reserve check keeps working");
        TIFleetRework.FleetRegistry.throwOrder = false;
        TIFleetRework.FleetRegistry.reserve.Clear();
        failedParts.Clear();
        Assert(!claimHeld(f.low, built).held, "A new ship is not held back as a tracked refit");
        AccessTools.Field(rally, "notification").SetValue(null, null);
        AccessTools.Field(rally, "completingYard").SetValue(null, null);
        RallyCall("SetTarget", f.hab, 0);

        // Losing a yard: every open order on it moves to the survivor and stays open.
        var lostYard = AccessTools.Method(Mod("StationConstructionLostYard"), "Prefix");
        registered.Clear();
        f = Fixture();
        var fleetC = Bare<TISpaceFleetState>(); var fleetD = Bare<TISpaceFleetState>();
        var onA1 = commission(f.high, "Frigate", fleetC);
        var onA2 = commission(f.high, "Frigate", fleetD);
        var paid = Enqueue(f.faction, f.high, paid: true); paid.shipDesignTemplateName = "Frigate";
        TIFleetRework.MPBridge.pushes.Clear();
        lostYard.Invoke(null, new object[] { f.faction, f.high });
        f.faction.nShipyardQueues.Remove(f.high);
        Assert(onA1.shipyardID == f.low.ID && onA2.shipyardID == f.low.ID && !onA1.archived && !onA2.archived
            && TIFleetRework.MPBridge.pushes.Count == 1 && TIFleetRework.MPBridge.pushes[0].Length == 2,
            "Every open order on a lost yard moves to the survivor, stays open and is sent to clients");
        Assert(claim(f.low) == fleetC, "The moved orders take the station's next ships, oldest first");
        onA1.deliveredShipID = new GameStateID(999);
        Assert(claim(f.low) == fleetD, "The second moved order takes the ship after that");
        registered.Clear();
        f = Fixture();
        var kept = commission(f.high, "Frigate", fleetC);
        settings.pooledStationConstruction = false;
        lostYard.Invoke(null, new object[] { f.faction, f.high });
        Assert(kept.shipyardID == f.high.ID, "Without the shared queue a lost yard's orders are not touched");
        settings.pooledStationConstruction = true;
        f.faction.nShipyardQueues.Remove(f.low);
        lostYard.Invoke(null, new object[] { f.faction, f.high });
        Assert(kept.shipyardID == f.high.ID, "When the last yard is lost nothing changes, as with Fleet Manager alone");
        registered.Clear();
        f = Fixture();
        var unlisted = Bare<TIHabModuleState>();
        yards[unlisted] = new Yard { Hab = f.hab, Tier = 2, Active = true, Days = 10 };
        var onUnlisted = commission(unlisted, "Frigate", fleetC);
        lostYard.Invoke(null, new object[] { f.faction, unlisted });
        Assert(onUnlisted.shipyardID == unlisted.ID,
            "A station yard with no queue, still being built or decommissioned, is skipped as the game skips it");
        var broken = Bare<TIHabModuleState>(); // Not in the fixture's yards: reading its hab throws, as a broken yard's tier does.
        f.faction.nShipyardQueues[broken] = new List<ShipConstructionQueueItem>();
        var onBroken = commission(broken, "Frigate", fleetC);
        lostYard.Invoke(null, new object[] { f.faction, broken });
        Assert(onBroken.shipyardID == broken.ID, "A broken yard that save repair removes throws nothing into the game");
        f.faction.nShipyardQueues.Remove(broken);

        // Rally targets in Fleet Manager's reserve.
        Stub(stubs, AccessTools.Method(rally, "NotifyReserve"), nameof(Noticed));
        var fleetStates = new Dictionary<GameStateID, TIGameState>();
        states[typeof(TISpaceFleetState)] = fleetStates;
        var reserve = Bare<TISpaceFleetState>(); reserve.exists = true; fleetStates[reserve.ID] = reserve;
        var habA = Bare<TIHabState>(); var habB = Bare<TIHabState>(); var habC = Bare<TIHabState>();
        RallyCall("SetTarget", habA, (int)reserve.ID);
        RallyCall("SetTarget", habB, (int)reserve.ID);
        RallyCall("SetTarget", habC, (int)reserve.ID);
        Func<TIGameState, bool> drop = state => (bool)RallyCall("DropReserveTarget", state);
        Func<TIGameState, int> target = state => (int)RallyCall("TargetID", state);
        notices = 0;
        Assert(!drop(habA) && target(habA) == (int)reserve.ID && notices == 0, "A fleet outside the reserve stays a rally target");
        TIFleetRework.FleetRegistry.reserve.Add(reserve);
        settings.shipyardRally = false;
        Assert(!drop(habA) && target(habA) == (int)reserve.ID && notices == 0, "With the rally off, reserve checks change nothing and post nothing");
        settings.shipyardRally = true;
        Assert(drop(habA) && target(habA) == 0 && notices == 1, "A rally target that enters the reserve is cleared with a notice");
        Assert(drop(habB) && target(habB) == 0 && notices == 1, "One notice a day per reserve fleet");
        RallyCall("ResetNotices");
        Assert(drop(habC) && target(habC) == 0 && notices == 2, "A new campaign starts the notices afresh");
        TIFleetRework.MPBridge.remote = true;
        Assert((bool)AccessTools.Method(compat, "InReserve").Invoke(null, new object[] { reserve }),
            "A remote multiplayer client still sees reserve fleets, so its picker hides them");
        TIFleetRework.MPBridge.remote = false;
        TIFleetRework.FleetRegistry.reserve.Clear();

        // Rallied ships are sorted into the mother fleet's squadrons, as Auto-Organise does.
        var organize = AccessTools.Method(compat, "Organize");
        var detached = Bare<TISpaceFleetState>(); var mother = Bare<TISpaceFleetState>();
        var ship = Bare<TISpaceShipState>(); ship.fleet = detached;
        TIFleetRework.FleetHierarchyOperations.mothers[detached] = mother;
        TIFleetRework.FleetRegistry.withSquadrons.Add(mother);
        organize.Invoke(null, new object[] { detached, new List<TISpaceShipState> { ship } });
        Assert(TIFleetRework.FleetHierarchyOperations.sortedInto == mother && TIFleetRework.FleetBattlegroup.reassessed == detached
            && TIFleetRework.AutoOrganiseFleetAction.pushed.SequenceEqual(new[] { detached, mother }),
            "A detached fleet's new ships are sorted into its mother fleet's squadrons, reassessed and sent to clients");
        TIFleetRework.FleetHierarchyOperations.mothers.Clear();
        TIFleetRework.FleetHierarchyOperations.sortedInto = null;
        organize.Invoke(null, new object[] { detached, new List<TISpaceShipState> { ship } });
        Assert(TIFleetRework.FleetHierarchyOperations.sortedInto == null, "A fleet with no squadrons of its own or its mother's is left alone");
        TIFleetRework.FleetRegistry.withSquadrons.Clear();
        TIFleetRework.AutoOrganiseFleetAction.pushed.Clear();
        TIFleetRework.FleetBattlegroup.reassessed = null;

        // A waiting item that every yard refused today still counts, from the next day.
        f = Fixture();
        var refusedEverywhere = Enqueue(f.faction, f.high);
        foreach (var refusing in new[] { f.high, f.low })
            Call("Refuse", refusedEverywhere, refusing);
        var refusedFree = (IDictionary)Call("FreeTimes", f.faction, f.high);
        Assert((float)refusedFree[f.high] == 11f && (float)refusedFree[f.low] == 0f,
            "An item every yard refused today counts in its fastest yard, starting on the next day");
        f = Fixture();
        var busy = Enqueue(f.faction, f.low, paid: true); busy.daysToCompletion = 60f;
        var refusedByFree = Enqueue(f.faction, f.high);
        Call("Refuse", refusedByFree, f.high);
        refusedFree = (IDictionary)Call("FreeTimes", f.faction, f.high);
        Assert((float)refusedFree[f.high] == 11f && (float)refusedFree[f.low] == 60f,
            "A free yard that refused the item today is still used from the next day, before a yard busy for 60 days");

        // Fleet Manager's queue estimate follows the shared queue.
        f = Fixture();
        Enqueue(f.faction, f.high, paid: true);
        var stubDays = AccessTools.Method(typeof(TIFleetRework.FleetCommissionManager), "SumQueueDays");
        var free = (IDictionary)Call("FreeTimes", f.faction, f.high);
        Assert((float)stubDays.Invoke(null, new object[] { f.faction, f.low }) == (float)free[f.low],
            "With a shared queue Fleet Manager's queue days are the dispatcher's free time");
        settings.pooledStationConstruction = false;
        Assert((float)stubDays.Invoke(null, new object[] { f.faction, f.low }) == -1f, "Without the shared queue Fleet Manager's own sum stays");
        settings.pooledStationConstruction = true;
        Assert((float)stubDays.Invoke(null, new object[] { f.faction, Bare<TIHabModuleState>() }) == -1f && flagOf("bound") && failedParts.Count == 0,
            "An error in CataTweaks' estimate keeps Fleet Manager's sum, throws nothing into it and leaves the bridge on");
        stubs.UnpatchAll(stubs.Id);
        states.Remove(typeof(TIFleetRework.TIFleetCommissionOrder));
        states.Remove(typeof(TISpaceFleetState));
        reset();

        string dll = Path.Combine(gameDir, "..", "..", "workshop", "content", "1176470", "3731957572", "TIFleetRework.dll");
        if (!File.Exists(dll))
        {
            Console.WriteLine("Fleet Manager binding check skipped: " + Path.GetFullPath(dll) + " not found.");
            return;
        }
        var real = Assembly.LoadFrom(dll);
        var harmony = new Harmony("CataTweaks.ConstructionCheck.FleetManager");
        bind(harmony, real);
        Assert(flagOf("bound") && !flagOf("bindFailed"), "Every Fleet Manager member CataTweaks uses still resolves");
        f = Fixture();
        Enqueue(f.faction, f.high, paid: true);
        Enqueue(f.faction, f.high);
        var realDays = AccessTools.Method(real.GetType("TIFleetRework.FleetCommissionManager", true), "SumQueueDays");
        free = (IDictionary)Call("FreeTimes", f.faction, f.high);
        Assert((float)realDays.Invoke(null, new object[] { f.faction, f.high }) == (float)free[f.high]
            && (float)free[f.high] != f.faction.GetShipyardQueue(f.high).Sum(i => i.daysToCompletion),
            "The queue estimate postfix attaches to Fleet Manager's SumQueueDays and replaces its result");
        harmony.UnpatchAll(harmony.Id);
        reset();
    }

    private static object RallyCall(string method, params object[] args) =>
        AccessTools.Method(Mod("StationConstructionRally"), method).Invoke(null, args);

    // Serves Space Nations' type names from the stand-ins in this program, except one type that
    // can be replaced.
    private sealed class MappedAssembly : Assembly
    {
        private readonly string replaced;
        private readonly Type replacement;
        internal MappedAssembly(string replaced = null, Type replacement = null)
        {
            this.replaced = replaced;
            this.replacement = replacement;
        }
        public override Type GetType(string name, bool throwOnError) => name == replaced ? replacement
            : typeof(Program).Assembly.GetType("SpaceNationsStandIns." + name.Substring("TISpaceNations.".Length), throwOnError);
    }

    // The planetary union stand-in, except that ClaimAllColonyRegions no longer reads the claims.
    private static class StalePlanetaryUnionService
    {
        public static bool HandleUnification(TINationState enacting, TIFactionState faction, TINationState joining) => enacting.claims.Count > 0;
        public static void Absorb(TINationState union, TIFactionState faction, TINationState nation) => union.claims.Clear();
        public static void ClaimAllColonyRegions(TINationState union, string body) { }
    }

    private static bool seenOccupying;
    private static void DuringOccupation() =>
        seenOccupying = (bool)AccessTools.Field(Mod("InheritedCapitalClaims"), "occupying").GetValue(null);

    private static TINationState Nation(TIRegionState capital, bool extant, bool alien = false)
    {
        var nation = Bare<TINationState>();
        AccessTools.Property(typeof(TINationState), nameof(TINationState.originalCapital)).SetValue(nation, capital);
        AccessTools.Property(typeof(TINationState), nameof(TINationState.regions)).SetValue(nation,
            extant ? new List<TIRegionState> { Bare<TIRegionState>() } : new List<TIRegionState>());
        nation.alienNation = alien;
        return nation;
    }

    private static void CheckCapitalClaims()
    {
        var claims = Mod("InheritedCapitalClaims");
        var identity = Mod("DormantCapitalIdentity");
        var shanghai = Bare<TIRegionState>(); var yemen = Bare<TIRegionState>();
        var twn = Nation(shanghai, false); var wur = Nation(shanghai, false); var yem = Nation(yemen, true);
        var map = (Dictionary<TIRegionState, List<TINationState>>)AccessTools.Method(claims, "MapCapitals")
            .Invoke(null, new object[] { new[] { twn, null, wur, Nation(null, false), yem } });
        Assert(map.Count == 2 && map[shanghai].SequenceEqual(new[] { twn, wur }) && map[yemen].Single() == yem,
            "Nations that share an original capital are all kept for it, in nation order");

        // Which absorptions count as voluntary mergers. The campaign is not ready here, so
        // BeforeMerge stops after the classification, before it reads the absorber's claims.
        var beforeMerge = AccessTools.Method(claims, "BeforeMerge");
        var occupying = AccessTools.Field(claims, "occupying");
        Func<TINationState, TINationState, TINationState> classify = (absorber, joining) =>
        {
            var args = new object[] { absorber, joining, null };
            beforeMerge.Invoke(null, args);
            return args[2] == null ? null : (TINationState)AccessTools.Field(args[2].GetType(), "joiner").GetValue(args[2]);
        };
        var human = Nation(Bare<TIRegionState>(), true);
        var joiner = Nation(Bare<TIRegionState>(), true);
        Assert(classify(human, joiner) == joiner, "A human nation absorbing a living nation is a voluntary merger");
        Assert(classify(Nation(Bare<TIRegionState>(), true, alien: true), joiner) == null, "An alien absorber never makes a voluntary merger");
        Assert(classify(human, Nation(Bare<TIRegionState>(), false)) == null, "Absorbing a dormant nation after a conquest is not a merger");

        // The occupation mark, through the real prefix and finalizer: nested, and around a throw.
        var beforeCapture = AccessTools.Method(claims, "BeforeCapture");
        var afterCapture = AccessTools.Method(claims, "AfterCapture");
        Func<object> enter = () =>
        {
            var args = new object[] { Bare<TIRegionState>(), null, null };
            beforeCapture.Invoke(null, args);
            return args[2];
        };
        Action<object> leave = state => afterCapture.Invoke(null, new[] { state });
        var outer = enter();
        var inner = enter();
        Assert(classify(human, joiner) == null, "Any absorb inside an occupation is a conquest, even of a living nation");
        leave(inner);
        Assert((bool)occupying.GetValue(null) && classify(human, joiner) == null, "Leaving a nested occupation keeps the outer one");
        leave(outer);
        Assert(!(bool)occupying.GetValue(null) && classify(human, joiner) == joiner, "Leaving the outer occupation ends it");
        var occupation = AccessTools.Method(typeof(TIRegionState), "CompleteOccupationofRegion");
        var probe = new Harmony("CataTweaks.OccupationCheck");
        seenOccupying = false;
        bool threw = false;
        try
        {
            probe.Patch(occupation, prefix: new HarmonyMethod(beforeCapture) { priority = Priority.First },
                finalizer: new HarmonyMethod(afterCapture));
            probe.Patch(occupation, prefix: new HarmonyMethod(typeof(Program), nameof(DuringOccupation)) { priority = Priority.Last });
            occupation.Invoke(Bare<TIRegionState>(), new object[] { null });
        }
        catch (TargetInvocationException) { threw = true; }
        finally { probe.UnpatchAll(probe.Id); }
        Assert(threw && seenOccupying, "The game's occupation runs with the mark set");
        Assert(!(bool)occupying.GetValue(null), "An occupation that throws still clears the mark");

        var mergers = new Dictionary<int, HashSet<int>>();
        Action<int, int> add = (absorber, joining) => AccessTools.Method(identity, "AddMerger").Invoke(null, new object[] { mergers, absorber, joining });
        add(2, 3); add(1, 2); add(1, 1);
        Assert(mergers[1].SetEquals(new[] { 2, 3 }) && mergers[2].SetEquals(new[] { 3 }),
            "A merger takes over the joiner's own members, and a nation never merges into itself");

        // The daily sweep: a revived member leaves every list, and a dormant nation loses its own.
        var sweep = AccessTools.Method(identity, "Sweep");
        var absorberA = Nation(Bare<TIRegionState>(), true);
        var memberJ = Nation(Bare<TIRegionState>(), false);
        var memberK = Nation(Bare<TIRegionState>(), false);
        mergers.Clear();
        add((int)memberJ.ID, (int)memberK.ID);
        add((int)absorberA.ID, (int)memberJ.ID);
        sweep.Invoke(null, new object[] { mergers, new[] { absorberA, memberJ, memberK } });
        Assert(mergers.Count == 1 && mergers[(int)absorberA.ID].SetEquals(new[] { (int)memberJ.ID, (int)memberK.ID }),
            "A dormant member loses its own list, and the living absorber keeps every member");
        AccessTools.Property(typeof(TINationState), nameof(TINationState.regions)).SetValue(memberJ, new List<TIRegionState> { Bare<TIRegionState>() });
        sweep.Invoke(null, new object[] { mergers, new[] { absorberA, memberJ, memberK } });
        Assert(mergers[(int)absorberA.ID].SetEquals(new[] { (int)memberK.ID }) && !mergers.ContainsKey((int)memberJ.ID),
            "A revived member leaves the absorber's list and starts with no members of its own");

        var seat = Bare<TIRegionState>();
        var rump = Nation(Bare<TIRegionState>(), true);
        var member = Nation(seat, false); var other = Nation(seat, false); var hostile = Nation(seat, false);
        var living = Nation(seat, true); var alien = Nation(seat, false, alien: true); var unclaimed = Nation(seat, false);
        // The lower-ID peaceful claimant comes after a higher-ID one, so order cannot decide.
        var all = new[] { living, alien, other, hostile, unclaimed, member, rump };
        mergers.Clear();
        Func<TINationState> heir = () => (TINationState)AccessTools.Method(identity, "Heir").Invoke(null, new object[]
        {
            all, rump, seat, mergers, (Func<TINationState, bool>)(x => x != unclaimed), (Func<TINationState, bool>)(x => x == hostile)
        });
        Assert(heir() == member, "With no merger the heir is the dormant nation with the lowest ID that peacefully claims the seat");
        add((int)rump.ID, (int)member.ID);
        Assert(heir() == other, "A nation that merged into the rump is never its heir");
        add((int)rump.ID, (int)other.ID);
        Assert(heir() == hostile, "A nation with a hostile claim on the seat is the heir when no peaceful one is left");
        add((int)rump.ID, (int)hostile.ID);
        Assert(heir() == null, "No living, alien or unclaiming nation, and no member, can be the heir");

        // The heir of a rump takes over the rump's members.
        var heirState = Nation(Bare<TIRegionState>(), true);
        AccessTools.Method(identity, "Inherit").Invoke(null, new object[] { mergers, (int)rump.ID, (int)heirState.ID });
        Assert(!mergers.ContainsKey((int)rump.ID)
            && mergers[(int)heirState.ID].SetEquals(new[] { (int)member.ID, (int)other.ID, (int)hostile.ID }),
            "The heir takes over every member of the rump");

        AccessTools.Method(identity, "Reset").Invoke(null, null);
        var store = (Dictionary<int, HashSet<int>>)AccessTools.Field(identity, "merged").GetValue(null);
        AccessTools.Method(identity, "Merged").Invoke(null, new object[] { rump, member });
        var save = fsData.CreateDictionary();
        AccessTools.Method(identity, "WriteSave").Invoke(null, new object[] { rump, save });
        var empty = fsData.CreateDictionary();
        AccessTools.Method(identity, "WriteSave").Invoke(null, new object[] { member, empty });
        Assert(save.AsDictionary["CataTweaks_MergedNations"].AsList.Single().AsInt64 == (int)member.ID
            && !empty.AsDictionary.ContainsKey("CataTweaks_MergedNations"), "Only an absorber's record carries its mergers");
        AccessTools.Method(identity, "Reset").Invoke(null, null);
        AccessTools.Method(identity, "ReadSave").Invoke(null, new object[] { rump, save });
        Assert(store[(int)rump.ID].SetEquals(new[] { (int)member.ID }), "Mergers survive a save and load");
        AccessTools.Method(identity, "ReadSave").Invoke(null, new object[] { rump, fsData.CreateDictionary() });
        Assert(!store.ContainsKey((int)rump.ID), "A save made before the record existed holds no mergers");
        AccessTools.Method(identity, "Reset").Invoke(null, null);
    }

    private static void CheckSpaceNations()
    {
        var compat = Mod("SpaceNationsCompat");
        var bind = AccessTools.Method(compat, "Bind");
        var bound = AccessTools.Field(compat, "bound");
        var failed = AccessTools.Field(compat, "failed");
        string dll = Path.Combine(gameDir, "..", "..", "workshop", "content", "1176470", "3818604132", "TISpaceNations.dll");
        bind.Invoke(null, new object[] { null, new ThrowingAssembly() });
        Assert(!(bool)bound.GetValue(null) && (bool)failed.GetValue(null), "A Space Nations lookup that throws turns the bridge off instead of failing");
        failed.SetValue(null, false);
        if (AccessTools.TypeByName("TISpaceNations.UnitedNations.UnitedNationsService") == null)
        {
            bind.Invoke(null, new object[] { null, null });
            Assert(!(bool)bound.GetValue(null) && (bool)failed.GetValue(null), "Missing Space Nations types turn the bridge off instead of failing");
            failed.SetValue(null, false);
        }
        // Stand-ins with Space Nations' names, where every grant reads the claim getters, and the
        // same with a planetary union whose ClaimAllColonyRegions no longer does.
        var lookup = AccessTools.Method(compat, "Lookup");
        var stale = new MappedAssembly("TISpaceNations.UnitedNations.PlanetaryUnionService", typeof(StalePlanetaryUnionService));
        var found = (List<MethodBase>)lookup.Invoke(null, new object[] { new MappedAssembly() });
        Assert(found?.Count == 6, "The stand-ins that read the claim getters resolve to six grants");
        Assert(lookup.Invoke(null, new object[] { stale }) == null,
            "A grant that no longer reads the claim getters fails the lookup");
        bind.Invoke(null, new object[] { null, stale });
        Assert(!(bool)bound.GetValue(null) && (bool)failed.GetValue(null), "A grant that no longer reads the claim getters turns the bridge off");
        failed.SetValue(null, false);
        if (!File.Exists(dll))
        {
            Console.WriteLine("Space Nations binding check skipped: " + Path.GetFullPath(dll) + " not found.");
            return;
        }
        Assembly.LoadFrom(dll);
        var harmony = new Harmony("CataTweaks.SpaceNationsCheck");
        bind.Invoke(null, new object[] { harmony, null });
        Assert((bool)bound.GetValue(null) && !(bool)failed.GetValue(null), "Every Space Nations claim grant CataTweaks rewrites still resolves and patches");
        var patched = harmony.GetPatchedMethods().ToList();
        Assert(patched.Count == 6, "All six Space Nations claim grants are rewritten");
        var reads = AccessTools.Method(Mod("InheritedCapitalClaims")
            .GetNestedType("WritersUseOwnLists", BindingFlags.NonPublic), "Reads");
        foreach (MethodBase method in patched)
        {
            Assert(Harmony.GetPatchInfo(method).Transpilers.Count == 1, method.Name + " carries the own-list rewrite");
            Assert(!PatchProcessor.GetCurrentInstructions(method).Any(i => (bool)reads.Invoke(null, new object[] { i })),
                method.Name + " no longer calls the claim getters");
            // Two that return on null input before touching the game are run, which compiles the
            // rewrite: a backing field the patched code may not read would fail here.
            if (method.Name != "Absorb" && !method.Name.StartsWith("<MissingEarthClaims>")) continue;
            object instance = method.IsStatic ? null : Activator.CreateInstance(method.DeclaringType, true);
            object result = method.Invoke(instance, method.GetParameters().Select(_ => (object)null).ToArray());
            Assert(result == null || result is bool b && !b, method.Name + " runs after the rewrite");
        }
        harmony.UnpatchAll(harmony.Id);
        bound.SetValue(null, false);
    }
}

// Stand-ins with TI Fleet Manager's names and signatures, so the bridge runs without Fleet Manager.
namespace TIFleetRework
{
    public class TIFleetCommissionOrder : TIGameState
    {
        public GameStateID shipyardID;
        public string shipTemplateDataName;
        public GameStateID deliveredShipID;
        internal TISpaceFleetState target;
        internal TIFactionState faction;
        public override TIFactionState ref_faction => faction;
        public TISpaceFleetState GetTargetFleet() => target;
    }
    public static class FleetRegistry
    {
        internal static readonly HashSet<TISpaceFleetState> reserve = new HashSet<TISpaceFleetState>();
        internal static readonly HashSet<TISpaceFleetState> withSquadrons = new HashSet<TISpaceFleetState>();
        internal static bool throwReserve;
        public static bool IsInReserve(TISpaceFleetState fleet) =>
            throwReserve ? throw new InvalidOperationException("reserve") : reserve.Contains(fleet);
        internal static readonly HashSet<TISpaceShipState> tracked = new HashSet<TISpaceShipState>();
        internal static readonly HashSet<TISpaceFleetState> reserveOrders = new HashSet<TISpaceFleetState>();
        internal static bool throwOrder;
        public static object GetDivisionForShip(TISpaceShipState ship) => tracked.Contains(ship) ? new object() : null;
        public static object GetReserveOrder(TISpaceFleetState fleet) =>
            throwOrder ? throw new InvalidOperationException("reserve order") : reserveOrders.Contains(fleet) ? new object() : null;
        public static List<object> GetSquadronsForFleet(TISpaceFleetState fleet) =>
            withSquadrons.Contains(fleet) ? new List<object> { new object() } : new List<object>();
    }
    public static class FleetHierarchyOperations
    {
        internal static readonly Dictionary<TISpaceFleetState, TISpaceFleetState> mothers = new Dictionary<TISpaceFleetState, TISpaceFleetState>();
        internal static TISpaceFleetState sortedInto;
        public static void DistributeShipsByType(TISpaceFleetState ownerFleet, List<TISpaceShipState> ships) => sortedInto = ownerFleet;
        private static TISpaceFleetState ResolveMotherFleet(TISpaceFleetState fleet) => mothers.TryGetValue(fleet, out var m) ? m : null;
    }
    public static class FleetBattlegroup
    {
        internal static TISpaceFleetState reassessed;
        public static void Reassess(TISpaceFleetState fleet) => reassessed = fleet;
    }
    public class AutoOrganiseFleetAction
    {
        internal static readonly List<TISpaceFleetState> pushed = new List<TISpaceFleetState>();
        internal static void PushFleetHierarchy(string reason, TISpaceFleetState fleet) => pushed.Add(fleet);
    }
    public static class FleetCommissionManager
    {
        private static float SumQueueDays(TIFactionState faction, TIHabModuleState shipyard) => -1f;
    }
    public static class MPBridge
    {
        internal static bool remote;
        internal static readonly List<GameStateID[]> pushes = new List<GameStateID[]>();
        public static bool IsRemoteClient => remote;
        public static void PushChanged(string reason, params GameStateID[] ids) => pushes.Add(ids);
    }
}

// Stand-ins with Space Nations' claim grant names and signatures, served by MappedAssembly.
namespace SpaceNationsStandIns.UnitedNations
{
    public static class UnitedNationsService
    {
        public static IEnumerable<TIRegionState> MissingEarthClaims(TINationState unitedNations, IEnumerable<TIRegionState> regions) =>
            regions.Where(region => !unitedNations.claims.Contains(region));
    }
    public static class PlanetaryUnionService
    {
        public static bool HandleUnification(TINationState enacting, TIFactionState faction, TINationState joining) => enacting.claims.Count > 0;
        public static void Absorb(TINationState union, TIFactionState faction, TINationState nation) => union.claims.Clear();
        public static void ClaimAllColonyRegions(TINationState union, string body) => union.hostileClaims.Clear();
    }
}
namespace SpaceNationsStandIns.Colonies
{
    public static class ColonyNationFoundingService
    {
        public static void ClaimForExistingUnion(TISpaceBodyState body, TIRegionState region) => ((TINationState)null).claims.Remove(region);
    }
    public static class SettlementService
    {
        public static void ClaimForExistingUnion(TINationState nation, TIRegionState region) => nation.claims.Remove(region);
    }
}
