// Exercises real game notification methods and real mod patches without starting Unity.
// Stop at InitItem: only template selection is under test, not notification UI or campaign state.
using System;
using System.IO;
using System.Reflection;
using CataTweaks;
using HarmonyLib;
using PavonisInteractive.TerraInvicta;

internal static class Program
{
    private static int checks;
    private sealed class TemplateSelected : Exception
    {
        internal readonly string Name;
        internal TemplateSelected(string name) { Name = name; }
    }
    private static int Main(string[] args)
    {
        string managed = Path.Combine(args.Length > 0 ? args[0] : @"C:\Program Files (x86)\Steam\steamapps\common\Terra Invicta", "TerraInvicta_Data", "Managed");
        AppDomain.CurrentDomain.AssemblyResolve += (_, e) =>
        {
            foreach (string dir in new[] { managed, Path.Combine(managed, "UnityModManager") })
            {
                string path = Path.Combine(dir, new AssemblyName(e.Name).Name + ".dll");
                if (File.Exists(path)) return Assembly.LoadFrom(path);
            }
            return null;
        };
        try { Run(); Console.WriteLine(checks + " notification template checks passed."); return 0; }
        catch (Exception e) { Console.Error.WriteLine(e); return 1; }
    }
    private static void Stop(string templateName) => throw new TemplateSelected(templateName);
    private static string Probe(string name, params object[] args)
    {
        try { AccessTools.Method(typeof(TINotificationQueueState), name).Invoke(null, args); }
        catch (TargetInvocationException e) when (e.InnerException is TemplateSelected selected) { return selected.Name; }
        throw new Exception("Did not reach notification initialization: " + name);
    }
    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        checks++;
    }
    private static void Run()
    {
        var h = new Harmony("CataTweaks.NotificationCheck");
        h.Patch(AccessTools.Method(typeof(TINotificationQueueState), "InitItem"), prefix: new HarmonyMethod(typeof(Program), nameof(Stop)));
        var names = new[] { "LogFleetDetected", "LogShipComplete" };
        var args = new[] { new object[] { null, null }, new object[] { null, null, false } };
        var patches = new[] { "FleetDetectedLocation", "StationConstructionRallyNotification" };
        for (int i = 0; i < names.Length; i++)
        {
            Assert(Probe(names[i], args[i]) == names[i], "Native template name: " + names[i]);
            var target = AccessTools.Method(typeof(TINotificationQueueState), names[i]);
            var patch = typeof(Settings).Assembly.GetType("CataTweaks." + patches[i], true);
            // Reproduce the shipped prefix/finalizer patches, excluding the new transpiler.
            h.Patch(target, prefix: new HarmonyMethod(AccessTools.Method(patch, "Prefix")),
                finalizer: new HarmonyMethod(AccessTools.Method(patch, "Finalizer")));
            string broken = Probe(names[i], args[i]);
            Assert(broken != names[i] && broken.Contains("_Patch"), "Reproduce wrapper template lookup: " + names[i]);
            Console.WriteLine("Without fix: " + broken);
            h.Unpatch(target, HarmonyPatchType.All, h.Id);
            h.CreateClassProcessor(patch).Patch();
        }
        var settings = typeof(Settings).Assembly.GetType("CataTweaks.Main", true).GetField("settings", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        foreach (bool enabled in new[] { false, true })
        {
            foreach (string field in new[] { "fleetDetectionLocation", "pooledStationConstruction", "shipyardRally" })
                typeof(Settings).GetField(field).SetValue(settings, enabled);
            for (int i = 0; i < names.Length; i++)
                Assert(Probe(names[i], args[i]) == names[i], "Patched native template with features " + enabled + ": " + names[i]);
        }
        // Incoming fleet warnings have no mod patch; verify their native lookup as controls.
        Assert(Probe("LogFleetLaunchesTowardMyAsset", null, null, null, null) == "LogFleetLaunchesTowardMyAsset", "Incoming fleet template");
        Assert(Probe("LogAssaultCarrierLaunchesTowardEarth", null, null, null) == "LogAssaultCarrierLaunchesTowardEarth", "Incoming assault carrier template");
    }
}
