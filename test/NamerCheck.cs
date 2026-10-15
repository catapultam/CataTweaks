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
foreach (string slug in new[] { "Mining, Science", "Mining (Mars)", "Mining \"Science\"", @"Mining\Science", "Mining, (Mars) \"Science\"", "採掘, 科学 (火星)" })
{
    string name = HabNamer.Name("Freeport", slug, "Low Earth Orbit 2, Earth");
    if (HabNamer.SlugOf(name, "Earth") != slug
        || HabNamer.BaseOf(name) != "Freeport"
        || HabNamer.Name(name, slug, "Low Earth Orbit 2, Earth") != name
        || HabNamer.Name(name, "Shipyard", "Low Earth Orbit 2, Earth")
            != "Freeport (Shipyard, Low Earth Orbit 2, Earth)")
        throw new System.Exception("Template name failed to round-trip: " + slug);
}
foreach (string slug in new[] { "Mining \"Science\"", "\"Mining\"", "\"Mining", "Mining\"", "採掘 \"科学\"" })
{
    foreach (string location in new[] { "Luna", "Low Earth Orbit 2, Earth" })
    {
        string legacy = "Freeport (" + slug + ", " + location + ")";
        string migrated = HabNamer.Name(legacy, slug, location);
        if (HabNamer.SlugOf(legacy, "Luna") != slug
            || HabNamer.BaseOf(legacy) != "Freeport"
            || migrated != HabNamer.Name("Freeport", slug, location)
            || HabNamer.SlugOf(migrated, "Luna") != slug
            || HabNamer.Name(migrated, slug, location) != migrated
            || HabNamer.Name(legacy, "Shipyard", location) != "Freeport (Shipyard, " + location + ")")
            throw new System.Exception("Legacy quoted template failed to migrate: " + legacy);
    }
}
foreach (string location in new[] { "(4) Vesta", "(2) Pallas", "Low Orbit, (4) Vesta", "Site (North (Inner)), (4) Vesta" })
{
    foreach (string custom in new[] { "Freeport", "Freeport (Custom, name) Base", "Freeport (Alpha)" })
    {
        foreach (string slug in new[] { "Mining", "Mining (Mars)", "Mining, Science", "Mining \"Science\"" })
        {
            string name = HabNamer.Name(custom, slug, location);
            if (HabNamer.BaseOf(name) != custom || HabNamer.SlugOf(name, location) != slug
                || HabNamer.Name(name, slug, location) != name)
                throw new System.Exception("Parenthesized location failed to round-trip: " + name);
        }
    }
}
System.Console.WriteLine("all checks passed");
