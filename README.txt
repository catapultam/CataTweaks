CataTweaks — installation

This is a code mod. It needs Unity Mod Manager (one-time setup):

1. Download Unity Mod Manager 0.25.0+ from https://www.nexusmods.com/site/mods/21
   (free Nexus account required).
2. Unzip anywhere and run UnityModManager.exe.
3. Game: "Terra Invicta", set the game folder path, installation method
   "DoorstopProxy", click Install.

That's it. UMM loads mods straight from Terra Invicta's Mods\Enabled
folder, so subscribing on the Steam Workshop (or dropping this folder in
place) is a complete install. The in-game Mods list toggle works too
(takes effect on restart).

What it does:
- Hab naming workflow: applying a saved hab template names the hab
  "Name (Template, Orbit, Body)" - the hab's existing name, then the
  template and where it is: "Freeport (Mining, Low Earth Orbit 2,
  Earth)", "Peary Crater Base (Mining, Peary Crater, Luna)". Bodies
  with a single orbit, like Lagrange points, drop the orbit part:
  "Outpost (Mining, Earth-Luna L1)". Re-applying a template only
  replaces the part in parentheses; your custom name is kept.
  A hab that already has the template's modules but not the suffix
  (built by hand, renamed, pre-mod save) can still Apply - it just
  gets renamed.
  Saving a hab as a template names the template after the one in the
  parentheses, so "Freeport (Mining, ...)" saves as "Mining" and
  overwrites the previous version (vanilla appends description and
  timestamp). Update a hab, save, re-apply elsewhere - no manual
  renaming.
  The template also remembers the hab's custom map icon, and applying
  it sets that icon on the target hab - so a "Mining" template carries
  its mining icon to every hab you apply it to, no re-picking from the
  dropdown. Templates saved before this version have no icon stored
  and leave the target hab's icon alone; re-save one to record it.
- Saving a priority preset under an existing custom preset's name
  overwrites it (vanilla disables the save button). If it was your
  default preset, the new version becomes the default, and every control
  point that was using the old version is updated to the new one (so
  countries keep tracking the profile instead of flipping to 'Custom').
  Built-in presets are never overwritten.
- Countries also keep tracking their preset when their valid priorities
  change (gaining a spaceflight program, founding a military, unlocking
  navy/STO/space-defense/nuke options...): the preset is re-applied with
  its weights for the newly valid priorities, within a day of the change.
- Ship construction screen: a dividing rule between stations in the
  shipyard list. One station can host several shipyards, and the list
  already sorts them together, so the rule just marks where one
  station's yards end and the next begin. It follows the vanilla
  location filter, dividing whichever yards are currently shown.

These two change game balance rather than just the interface:

- Solar mirrors boost orbital stations, not only surface bases. Vanilla
  builds the whole mechanism - station-mounted mirrors, per-body
  accumulation, mirror mass scaling with distance from the Sun squared,
  an 8x output ceiling - but only ever spends it on bases. Now a
  station's solar modules also benefit, with a direction rule: a mirror
  only lights targets inward of itself, so a mirror in high Mars orbit
  boosts medium and low, and one in medium boosts low but never high. A
  mirror level with its target does nothing for it. Lagrange points sit
  outside the orbit ladder, so they light everything they already credit
  in vanilla - a Sun-Earth L1 mirror covers every Earth and Luna orbit,
  while an Earth-Luna L1 mirror covers Luna only. Surface bases are
  unchanged.

- Review Failed Projects favours the expensive missed project. Vanilla
  weights each candidate by availability chance divided by research
  cost, so a 200-research throwaway outdraws a 5000-research drive by
  25 to 1 and the review is least useful exactly when it matters most.
  That divide becomes a multiply, so cost raises a project's odds
  instead of sinking them. Availability chance still scales it, so
  genuinely rare projects stay rare.

- Demand Claim works while the target is fighting someone else.
  Vanilla checks "is this nation at war at all", not "at war with the
  nation asking", so any nation in any war can never cede a region to
  anyone. An alien war that never ends therefore freezes every
  peaceful border change on the map permanently - including transfers
  between two nations the same faction already controls, which the
  game would otherwise approve without even asking. Now only a war
  between those two nations blocks it. Every other vanilla condition
  is unchanged: still no capitals, no hostile claims, both sides need
  consolidated executive control, and the improve-relations cooldown
  still applies.

- Repeatable income projects (Management, Audience, Commercial and
  Operations Research) scale their payoff with their cost. A repeatable's Nth completion already costs N times the
  base, but Management Research grants the same flat +5 every time, so
  research per point of capacity is 120N and grows without limit - by
  the 27th repeat it is over 3,000 and the cumulative cost of N points
  is quadratic. Every other source of cap (global freebies, councilor
  attributes, one administration module per station, a fixed list of
  one-off projects) is hard-bounded, while maintenance cost scales with
  national GDP forever, so the repeatable is the only source that can
  keep up and vanilla prices it out of reach. Now the Nth repeat grants
  the base effect times (1 + rate x (N-1)), rounded to whole units, so
  the cost per point converges instead of diverging. At the default 0.03
  it settles around 4,000 research per point: roughly ten times the
  median one-off capacity project and twice the worst one in the game,
  so the grind stays a deliberately poor last resort rather than a
  shortcut. The capacity bonus is derived from the number of repeats,
  so it applies to ones already completed. Audience, Commercial and
  Operations Research get the same scaling on their Influence, Money
  and Operations grants, from the next completion on (resources
  already granted are not topped up). The project descriptions show
  the scaled figure in place of the base one, and the "This is a
  repeatable project" note also says what the next attempt will grant.

- Fleet detection notices say where the hab is. "Surveillance
  telescopes have detected a new Protectorate fleet docked at Montezuma
  Base" only helps if you already know where Montezuma Base is. The
  game can already write the longer form - the same line with ", Low
  Mars Orbit" after the hab, and the body for a landed fleet - and uses
  it elsewhere; the notification just asked for the short one. Now it
  asks for the long one, so a detection tells you where to look.

- Unifying a nation inherits its peaceful claims. Absorbing a nation
  moves its regions, control points, nuclear weapons, space program and
  half its accumulated investment - and none of its claims. The absorbed
  nation keeps them while it sits dormant, so the only way to reach what
  it claimed is to release it, let it expand and take it back. That is
  what forces unification to be worked strictly from the outside in:
  merge the far end of a chain first, because merging inward first
  strands every claim beyond it. Now the absorbing nation takes over the
  peaceful half of the claims. Claims the other nation held hostilely
  are left behind, since those are a grievance that was never yours, and
  inherited claims arrive non-hostile. Applies to annexation as well,
  and to AI nations as much as yours.

Configuration:

Every feature above can be switched on or off on its own. The mod writes
Settings.txt into its own folder the first time it runs. The contents are
JSON; the file is named .txt because Terra Invicta's own template loader
tries to parse every .json file in an enabled mod folder as a template
array, and gives up on the whole batch when one doesn't fit:

    {
        "habTemplateNaming": true,
        "priorityPresetOverwrite": true,
        "presetTrackingOnNationChanges": true,
        "stationDividers": true,
        "solarMirrorsBoostStations": false,
        "expensiveFirstProjectReview": true,
        "demandClaimDespiteOtherWars": true,
        "fleetDetectionLocation": true,
        "inheritClaimsOnMerge": true,
        "repeatableProjectScaling": 0.03
    }

repeatableProjectScaling is a number, not a switch: it is the
fraction of the base effect that each repeat adds. 0 turns the patch
off and restores stock behaviour; 0.03 is the default; larger values
make the grind cheaper. See the last entry above for what it does.

The solar mirror change is the one that ships off, since it alters power
output across every station you own; the rest are on. Edit the file and
restart the game. A setting that is off is not merely inert - the patch
is never applied at all, so that part of the game runs exactly as stock.

Your own edits are never overwritten: the file is only written when it
is missing. If it cannot be parsed the mod logs the error, falls back to
the defaults for that run, and leaves your file alone to be fixed.

