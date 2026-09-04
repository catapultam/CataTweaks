using System.Diagnostics;
using CataTweaks;

Trace.Listeners.Add(new ConsoleTraceListener());

Debug.Assert(HabNamer.Name("Freeport", "Mining", "Low Earth Orbit 2, Earth") == "Freeport (Mining, Low Earth Orbit 2, Earth)", "fresh name gets suffix");
Debug.Assert(HabNamer.Name("Freeport (Mining, Low Earth Orbit 2, Earth)", "Shipyard", "Low Earth Orbit 2, Earth") == "Freeport (Shipyard, Low Earth Orbit 2, Earth)", "re-apply replaces suffix, keeps custom name");
Debug.Assert(HabNamer.Name("Outpost (Mining, Earth-Luna L1)", "Mining", "Earth-Luna L1") == "Outpost (Mining, Earth-Luna L1)", "same template is idempotent");
Debug.Assert(HabNamer.BaseOf("Peary Crater Base (Mining, Peary Crater, Luna)") == "Peary Crater Base", "base strips suffix");
Debug.Assert(HabNamer.BaseOf("Station (Alpha)") == "Station (Alpha)", "parens without a comma are part of the custom name");
Debug.Assert(HabNamer.SlugOf("Freeport (Mining, Low Earth Orbit 2, Earth)", "Earth") == "Mining", "slug round-trips from suffix");
Debug.Assert(HabNamer.SlugOf("Outpost (Mining, Earth-Luna L1)", "Earth-Luna L1") == "Mining", "slug round-trips from single-orbit suffix");
Debug.Assert(HabNamer.SlugOf("Mining-Luna-3", "Luna") == "Mining", "legacy Slug-Body-N still parses");
Debug.Assert(HabNamer.SlugOf("Mining Base 3", "Luna") == "Mining Base", "legacy 'Name N' strips number");
Debug.Assert(HabNamer.SlugOf("Freeport", "Luna") == "Freeport", "custom names pass through whole");
System.Console.WriteLine("all checks passed");
