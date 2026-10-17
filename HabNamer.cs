using System.Text.RegularExpressions;

namespace CataTweaks;

// Naming scheme: habs created from a template are "Custom Name (Template, Location)" where
// location is "Orbit|Site, Body" (just "Body" for single-orbit bodies like Lagrange points).
// Saving a hab as a template pulls the template name back out of the parentheses.
public static class HabNamer
{
    // Quote names containing delimiters, escaping quotes and backslashes inside the quotes.
    // Keep reading the unquoted suffixes written by previous versions.
    private static readonly Regex Suffix = new Regex(
        @"^(.*?)\s*\((?:""((?:[^""\\]|\\[""\\])*)""|([^(),]+)),(?:[^()]|\((?<depth>)|\)(?<-depth>))*(?(depth)(?!))\)$");

    // A slug with one of these is quoted.
    private static readonly char[] Delimiters = { ',', '(', ')', '"', '\\' };

    public static string Name(string habName, string slug, string location)
    {
        string encoded = slug.IndexOfAny(Delimiters) >= 0
            ? "\"" + slug.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\""
            : slug;
        return $"{BaseOf(habName)} ({encoded}, {location})";
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
            if (!m.Groups[2].Success)
                return m.Groups[3].Value.Trim();
            string encoded = m.Groups[2].Value;
            // Name() never quotes a plain slug. A plain, fully quoted legacy slug therefore
            // includes those literal quotes, rather than using them as encoding delimiters.
            return encoded.IndexOfAny(Delimiters) >= 0
                ? Regex.Replace(encoded, @"\\([""\\])", "$1")
                : "\"" + encoded + "\"";
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
