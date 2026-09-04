using System.Diagnostics;
using CataTweaks;

Trace.Listeners.Add(new ConsoleTraceListener());
string[] habs = ["Random Station", "Mining-Luna-1", "Mining-Luna-3", "Mining-Ceres-1", "Miningx-Luna-9"];

Debug.Assert(HabNamer.NextName("Mining", "Luna", habs) == "Mining-Luna-4", "max+1 across gaps per body");
Debug.Assert(HabNamer.NextName("Mining", "Mars", habs) == "Mining-Mars-1", "numbering is per body");
Debug.Assert(HabNamer.NextName("Shipyard", "Luna", habs) == "Shipyard-Luna-1", "first instance starts at 1");
Debug.Assert(HabNamer.NextName("Min", "Luna", habs) == "Min-Luna-1", "prefix of longer slug must not match");
Debug.Assert(HabNamer.SlugBodyPattern("Mining", "Luna").IsMatch("Mining-Luna-3"), "reapply keep-guard matches");
Debug.Assert(!HabNamer.SlugBodyPattern("Mining", "Luna").IsMatch("Mining-Ceres-3"), "other body is not a match");
Debug.Assert(HabNamer.SlugOf("Mining-Luna-3", "Luna") == "Mining", "slug round-trips from Slug-Body-N");
Debug.Assert(HabNamer.SlugOf("Mining-Earth-Luna L4-2", "Earth-Luna L4") == "Mining", "body names with dashes parse");
Debug.Assert(HabNamer.SlugOf("Mining Base 3", "Luna") == "Mining Base", "legacy 'Name N' strips number");
Debug.Assert(HabNamer.SlugOf("Freeport", "Luna") == "Freeport", "custom names pass through whole");
System.Console.WriteLine("all checks passed");
