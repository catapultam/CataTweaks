using System;
using System.Diagnostics;
using CataTweaks;

Trace.Listeners.Add(new ConsoleTraceListener());

// No saved templates, and saved templates "Mining", "Shipyard" and "Ore, Ice".
Func<string, bool> none = _ => false;
Func<string, bool> saved = name => name == "Mining" || name == "Shipyard" || name == "Ore, Ice";

// "(Template, Location)" form, as before.
Debug.Assert(HabNamer.Name("Freeport", "Mining", "Low Earth Orbit 2, Earth", none) == "Freeport (Mining, Low Earth Orbit 2, Earth)", "fresh name gets suffix");
Debug.Assert(HabNamer.Name("Freeport (Mining, Low Earth Orbit 2, Earth)", "Shipyard", "Low Earth Orbit 2, Earth", none) == "Freeport (Shipyard, Low Earth Orbit 2, Earth)", "re-apply replaces suffix, keeps custom name");
Debug.Assert(HabNamer.Name("Outpost (Mining, Earth-Luna L1)", "Mining", "Earth-Luna L1", none) == "Outpost (Mining, Earth-Luna L1)", "same template is idempotent");
Debug.Assert(HabNamer.BaseOf("Peary Crater Base (Mining, Peary Crater, Luna)", none) == "Peary Crater Base", "base strips suffix");
Debug.Assert(HabNamer.BaseOf("Peary Crater Base (Mining, Peary Crater, Luna)", saved) == "Peary Crater Base", "comma form still strips when templates exist");
Debug.Assert(HabNamer.BaseOf("Station (Alpha)", none) == "Station (Alpha)", "parens without a comma are part of the custom name");
Debug.Assert(HabNamer.BaseOf("Station (Alpha)", saved) == "Station (Alpha)", "(X) is a custom name when X is not a saved template");
Debug.Assert(HabNamer.SlugOf("Freeport (Mining, Low Earth Orbit 2, Earth)", "Earth", none) == "Mining", "slug round-trips from suffix");
Debug.Assert(HabNamer.SlugOf("Outpost (Mining, Earth-Luna L1)", "Earth-Luna L1", none) == "Mining", "slug round-trips from single-orbit suffix");
Debug.Assert(HabNamer.SlugOf("Mining-Luna-3", "Luna", none) == "Mining", "legacy Slug-Body-N still parses");
Debug.Assert(HabNamer.SlugOf("Mining Base 3", "Luna", none) == "Mining Base", "legacy 'Name N' strips number");
Debug.Assert(HabNamer.SlugOf("Freeport", "Luna", none) == "Freeport", "custom names pass through whole");

// "(Template)" form, for Systematic Hab Names bases.
Debug.Assert(HabNamer.Name("M-MAR-1 Tempe Terra", "Mining", null, saved) == "M-MAR-1 Tempe Terra (Mining)", "SHN base gets short tag");
Debug.Assert(HabNamer.Name("S-MAR-MED-1", "Shipyard", null, saved) == "S-MAR-MED-1 (Shipyard)", "SHN station gets short tag");
Debug.Assert(HabNamer.Name("LEO1-2", "Mining", "", saved) == "LEO1-2 (Mining)", "empty location gives short tag");
Debug.Assert(HabNamer.Name("S-MAR-MED-1 (Mining)", "Shipyard", null, saved) == "S-MAR-MED-1 (Shipyard)", "re-apply replaces short tag");
Debug.Assert(HabNamer.Name("S-MAR-MED-1 (Mining)", "Mining", null, saved) == "S-MAR-MED-1 (Mining)", "same template is idempotent in short form");
Debug.Assert(HabNamer.Name("Station (Alpha)", "Mining", null, saved) == "Station (Alpha) (Mining)", "unknown (X) stays in the custom name");
Debug.Assert(HabNamer.Name("ISS (Ore, Ice)", "Mining", null, saved) == "ISS (Mining)", "short tag can hold a comma");
Debug.Assert(HabNamer.BaseOf("M-MAR-1 Tempe Terra (Mining)", saved) == "M-MAR-1 Tempe Terra", "base strips known short tag");
Debug.Assert(HabNamer.BaseOf("M-MAR-1 Tempe Terra (Mining)", none) == "M-MAR-1 Tempe Terra (Mining)", "short tag needs a saved template");

// Switch between the forms replaces the tag and does not stack.
Debug.Assert(HabNamer.Name("M-MAR-1 Tempe Terra (Mining, Tempe Terra, Mars)", "Mining", null, saved) == "M-MAR-1 Tempe Terra (Mining)", "comma form to short form");
Debug.Assert(HabNamer.Name("M-MAR-1 Tempe Terra (Mining)", "Shipyard", "Tempe Terra, Mars", saved) == "M-MAR-1 Tempe Terra (Shipyard, Tempe Terra, Mars)", "short form to comma form");

// Template names from Systematic Hab Names habs.
Debug.Assert(HabNamer.SlugOf("S-MAR-MED-1 (Shipyard)", "Mars", saved, legacy: false) == "Shipyard", "slug from short tag");
Debug.Assert(HabNamer.SlugOf("M-MAR-1 Tempe Terra (Mining, Tempe Terra, Mars)", "Mars", saved, legacy: false) == "Mining", "slug from comma tag without legacy");
Debug.Assert(HabNamer.SlugOf("S-MAR-MED-1", "Mars", saved, legacy: false) == null, "no tag and no legacy gives null");
Debug.Assert(HabNamer.SlugOf("M-MAR-1 Tempe Terra", "Mars", saved, legacy: false) == null, "SHN base name is not a slug");
Debug.Assert(HabNamer.SlugOf("S-MAR-MED-1", "Mars", saved) == "S-MAR-MED", "legacy strip still applies when asked");
Debug.Assert(HabNamer.TrySplit("LEO1-2 (Mining)", saved, out string b, out string s) && b == "LEO1-2" && s == "Mining", "TrySplit gives base and slug");
Debug.Assert(!HabNamer.TrySplit("Station (Alpha)", saved, out _, out _), "TrySplit finds no tag in a custom name");

// Save without Systematic Hab Names passes a predicate that is always false. SlugOf does not read "(Template)", as in 3.4.3.
Debug.Assert(HabNamer.SlugOf("Freeport (Mining)", "Earth", none) == "Freeport (Mining)", "save without SHN keeps a short tag in the template name");
Debug.Assert(HabNamer.SlugOf("Freeport (Mining)", "Earth", saved, legacy: false) == "Mining", "save with SHN reads a short tag");
Debug.Assert(HabNamer.Name("Freeport (Mining)", "Shipyard", "Low Earth Orbit 2, Earth", saved) == "Freeport (Shipyard, Low Earth Orbit 2, Earth)", "apply without SHN replaces a short tag of a saved template");

// Empty and null names do not get a leading space.
Debug.Assert(HabNamer.Name("", "Mining", null, saved) == "(Mining)", "empty name gives tag only");
Debug.Assert(HabNamer.Name(null, "Mining", null, saved) == "(Mining)", "null name gives tag only");
Debug.Assert(HabNamer.Name("(Mining)", "Shipyard", null, saved) == "(Shipyard)", "tag-only name is replaced with no leading space");
Debug.Assert(HabNamer.Name(null, "Mining", "Earth-Luna L1", none) == "(Mining, Earth-Luna L1)", "null name gives comma tag only");
Debug.Assert(HabNamer.BaseOf(null, none) == "", "null base is empty");
Debug.Assert(HabNamer.SlugOf(null, "Earth", none, legacy: false) == null, "null name has no slug");
System.Console.WriteLine("all checks passed");
