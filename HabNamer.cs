using System;
using System.Text.RegularExpressions;

namespace CataTweaks;

// Naming scheme: habs created from a template are "Custom Name (Template, Location)" where
// location is "Orbit|Site, Body" (just "Body" for single-orbit bodies like Lagrange points).
// With no location (Systematic Hab Names supplies it), the name is "Custom Name (Template)".
// Saving a hab as a template pulls the template name back out of the parentheses.
public static class HabNamer
{
    private static readonly Regex Suffix = new Regex(@"^(.*?)\s*\(([^(),]+),[^()]*\)$");
    // "(Template)" is a tag only when isTemplate accepts the text, so "Station (Alpha)" stays a
    // custom name. This form can contain a comma, for a template name that has one.
    private static readonly Regex ShortSuffix = new Regex(@"^(.*?)\s*\(([^()]+)\)$");

    public static string Name(string habName, string slug, string location, Func<string, bool> isTemplate)
    {
        string baseName = BaseOf(habName, isTemplate);
        string tag = string.IsNullOrEmpty(location) ? $"({slug})" : $"({slug}, {location})";
        return string.IsNullOrWhiteSpace(baseName) ? tag : $"{baseName} {tag}";
    }

    // Splits a trailing "(Template)" or "(Template, Location)" from the custom name.
    public static bool TrySplit(string habName, Func<string, bool> isTemplate, out string baseName, out string slug)
    {
        habName = habName ?? "";
        Match m = ShortSuffix.Match(habName);
        if (m.Success && isTemplate(m.Groups[2].Value.Trim()))
        {
            baseName = m.Groups[1].Value;
            slug = m.Groups[2].Value.Trim();
            return true;
        }
        m = Suffix.Match(habName);
        if (m.Success)
        {
            baseName = m.Groups[1].Value;
            slug = m.Groups[2].Value.Trim();
            return true;
        }
        baseName = habName;
        slug = null;
        return false;
    }

    // The custom name: everything before a trailing tag.
    public static string BaseOf(string habName, Func<string, bool> isTemplate)
    {
        TrySplit(habName, isTemplate, out string baseName, out _);
        return baseName;
    }

    // The template name in the tag. With no tag and legacy false, returns null.
    public static string SlugOf(string habName, string body, Func<string, bool> isTemplate, bool legacy = true)
    {
        habName = habName ?? "";
        if (TrySplit(habName, isTemplate, out _, out string slug))
        {
            return slug;
        }
        if (!legacy)
        {
            return null;
        }
        // Legacy formats from earlier mod versions / vanilla: "Name-Body-3", "Name 3", "Name-3".
        Match m = new Regex("^(.+)-" + Regex.Escape(body) + @"-\d+$").Match(habName);
        if (m.Success)
        {
            return m.Groups[1].Value;
        }
        m = new Regex(@"^(.+?)[ -]\d+$").Match(habName);
        return m.Success ? m.Groups[1].Value : habName;
    }
}
