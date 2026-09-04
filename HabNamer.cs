using System.Text.RegularExpressions;

namespace CataTweaks;

// Naming scheme: habs created from a template are "Custom Name (Template, Location)" where
// location is "Orbit|Site, Body" (just "Body" for single-orbit bodies like Lagrange points).
// Saving a hab as a template pulls the template name back out of the parentheses.
public static class HabNamer
{
    private static readonly Regex Suffix = new Regex(@"^(.*?)\s*\(([^(),]+),[^()]*\)$");

    public static string Name(string habName, string slug, string location)
    {
        return $"{BaseOf(habName)} ({slug}, {location})";
    }

    // The custom name: everything before a trailing "(Template, Location)".
    public static string BaseOf(string habName)
    {
        Match m = Suffix.Match(habName);
        return m.Success ? m.Groups[1].Value : habName;
    }

    public static string SlugOf(string habName, string body)
    {
        Match m = Suffix.Match(habName);
        if (m.Success)
        {
            return m.Groups[2].Value.Trim();
        }
        // Legacy formats from earlier mod versions / vanilla: "Name-Body-3", "Name 3", "Name-3".
        m = new Regex("^(.+)-" + Regex.Escape(body) + @"-\d+$").Match(habName);
        if (m.Success)
        {
            return m.Groups[1].Value;
        }
        m = new Regex(@"^(.+?)[ -]\d+$").Match(habName);
        return m.Success ? m.Groups[1].Value : habName;
    }
}
