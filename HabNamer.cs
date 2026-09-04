using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace CataTweaks;

// Naming scheme: habs created from a template are "Slug-Body-N" (slug = template name,
// body = celestial body, N unique per slug+body within a faction). Saving a hab as a
// template strips "-Body-N" so the template name round-trips to the bare slug.
public static class HabNamer
{
    public static Regex SlugBodyPattern(string slug, string body)
    {
        return new Regex("^" + Regex.Escape(slug) + "-" + Regex.Escape(body) + @"-(\d+)$");
    }

    public static string NextName(string slug, string body, IEnumerable<string> existingNames)
    {
        Regex rx = SlugBodyPattern(slug, body);
        int max = existingNames
            .Select(n => rx.Match(n))
            .Where(m => m.Success)
            .Select(m => int.Parse(m.Groups[1].Value))
            .DefaultIfEmpty(0)
            .Max();
        return $"{slug}-{body}-{max + 1}";
    }

    public static string SlugOf(string habName, string body)
    {
        Match m = new Regex("^(.+)-" + Regex.Escape(body) + @"-\d+$").Match(habName);
        if (m.Success)
        {
            return m.Groups[1].Value;
        }
        // Legacy formats from earlier mod versions / vanilla: "Name 3" or "Name-3".
        m = new Regex(@"^(.+?)[ -]\d+$").Match(habName);
        return m.Success ? m.Groups[1].Value : habName;
    }
}
