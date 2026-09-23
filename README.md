# CataTweaks

A collection of UI, quality of life and gameplay tweaks for
[Terra Invicta](https://store.steampowered.com/app/1176470/Terra_Invicta/), built to my own
tastes. Every feature has its own switch.

On the Steam Workshop:
[CataTweaks](https://steamcommunity.com/sharedfiles/filedetails/?id=3805519545).

AI was used to create this mod. If that bothers you, don't feel compelled to use it. I made it
for myself, and was asked to post it.

## Install

CataTweaks needs Unity Mod Manager. Download it from
[nexusmods.com/site/mods/21](https://www.nexusmods.com/site/mods/21), run it, select Terra
Invicta, set the game folder, choose the DoorstopProxy method, and click Install.

Then subscribe on the Workshop, or copy this folder into `Terra Invicta/Mods/Enabled/`.

## Settings

CataTweaks adds its own tab to the game's Settings screen. The tab works in the main menu and in
a loaded campaign. Every setting has a tooltip, and changes apply the moment you click them.
There is no restart and no reload.

Two settings belong to a campaign rather than to you. They also appear at the bottom of New Game
/ Customize Campaign, and the value you choose there is stored in the save. A campaign therefore
always plays with the settings it was started with. The rows in the Settings tab set the default
that the Customize Campaign screen starts from.

## Quality of life and UI

**Remember campaign options between sessions.** Customize Campaign opens with the options from
your last campaign. The options are saved when you leave the screen, and not only when you start a
campaign. A custom faction name is saved for each faction, and appears again when you select that
faction.

**Hab template naming.** When you apply a hab template, the hab is renamed to Name (Template,
Orbit, Body), for example Freeport (Mining, Low Earth Orbit 2, Earth), and takes the map icon of
the template. A body with one orbit, such as a Lagrange point, does not show the orbit. When you
apply a template again, only the text in brackets changes.

When you save a hab as a template, the template takes the name in brackets and replaces the
template that has that name. The Apply button stays enabled when the hab already has the modules
of the template, so you can apply a template that changes nothing and rename the hab.

**Priority presets overwrite in place.** When you save a preset with the name of an existing
custom preset, it replaces that preset. The new preset stays the default preset if the old one was
the default. Control points that use the old preset move to the new one. Built-in presets are not
replaced. When you select a custom preset from the dropdown, its name goes into the name field,
and the field accepts a name that already exists.

**Nations keep their preset when priorities change.** When a nation gains or loses a priority, its
preset is applied again within one day. Examples are a spaceflight program, a military, a navy,
and the STO, space defense and nuclear options. The nation does not change to Custom.

**Dividers between stations in the ship construction screen.** The list shows a line between the
shipyards of one station and the shipyards of the next station. The line follows the standard
location filter.

**Fleet detections name the orbit or body.** A fleet detection notice gives the orbit and the body
of the hab. For a landed fleet it gives the body.

**Nation picker on the nation panel.** The nation name becomes a dropdown. The dropdown lists the
nations where you hold a control point, in name order. A previous arrow and a next arrow are on
each side, and they continue from the last nation to the first. A nation where you hold no control
point is at the top of the list, and the arrows lead from it into the list.

## Gameplay

**Solar mirrors boost orbital stations.** The solar modules of an orbital station receive the
solar mirror bonus. A mirror lights the stations that orbit inward of it. A mirror in high Mars
orbit lights medium orbit and low orbit, and a mirror in medium orbit lights low orbit. A mirror
at the same level as a station does not light it. A mirror at a Lagrange point lights the same
stations as in the standard game. Surface bases do not change. Power management is refreshed for
the affected stations when the number of mirrors changes. Off by default.

**Solar mirror orbital boost cap.** The maximum output of the solar modules of a mirror-lit
station, as a multiple of their unlit output, from 2 to 8. This applies to orbital stations only.
The standard limit is 8.

**Project review favors the expensive project.** Review Failed Projects weights each project by
its availability chance multiplied by its research cost. An expensive project is more likely to be
selected.

**Boost priority prefers better launch sites.** A completed Boost priority can build its launch
facilities in the best available region of the nation. The best region is the eligible region
nearest the equator. A region under occupation is not eligible. This applies to the Boost priority
and to the first launch facilities of a new spaceflight program. The Boost priority tooltip shows
the range of the eligible regions, and one figure when the result is certain.

The AI also reads a southern latitude as a distance from the equator. This applies to its score
for a nation, to its test for a useful launch site, and to the event that grants a spaceflight
program.

**Launch site focus.** How often the Boost priority builds in the best available region, from 0%
to 100%. At 0% the game selects the region as the standard game does, which almost always adds to
a region that already has launch facilities. At 100% the nation always builds in the best region.
50% by default.

**Any faction can go dormant, and can come back.** The campaign option Allow AI Factions To Be
Disabled spares five factions. Two of them are the aliens and your own faction, and they still
cannot go dormant. The other three are spared for their part in the story, and they can now go
dormant on the same terms as the others: no fleets, habs, councilors or control points on one day,
from campaign year 15. In a game with all factions the three are the Servants, the Protectorate
and Humanity First. Humanity First is spared only while you play a faction that is not Humanity
First or the Resistance.

A dormant faction also comes back when it holds two control points again. The aliens continue to
give control points to the Servants and the Protectorate with the Enthrall Elites and Terrorize
missions, and you can trade a control point to a dormant faction. A faction that comes back has no
money, no orgs and no income from its founding or from events, and runs on what its control points
give it. The game shows no notification when a faction comes back.

This setting does nothing unless the campaign option is on. Off by default.

**Demand Claim ignores the target's other wars.** A war blocks Demand Claim only if it is a war
between the two nations in the claim. The other conditions do not change.

**Holding a capital borrows that nation's claims.** While you hold the original capital of a
dormant nation, you can use the claims of that nation. Your claim on the capital must not be
hostile. You lose the borrowed claims when you lose the capital, or when your claim on it becomes
hostile. A claim that the other nation held as hostile stays hostile for you. A nation that still
holds territory is excluded. Save files do not change.

**Repeatable management project scaling.** Each repeat of Management Research, Audience Research,
Commercial Research and Operations Research pays its base reward plus this percentage, for each
repeat you have already completed. The slider is from 0% to 20%. At 0% each repeat pays the
standard flat reward. Alien factions are not affected.

The capacity bonus uses the number of repeats, so it includes repeats you completed before you
installed the mod. Resources you have already received are not increased. Project descriptions
show the scaled value and the next completion.

**Control points can be traded.** Control points are on the diplomacy table with orgs, habs and
projects, on a separate tab and grouped by nation. The AI gives them a value. It asks a price for
its own control points, pays for yours, and offers control points in nations it has abandoned.
Alien factions and control points in alien nations are excluded. On by default.

**Control point base value.** How much the AI weights a control point against the other items on
the table, from 0.5x to 5x. The value is six months of the income of the control point, as that
faction values income, plus its investment points and the armies behind it. The value is doubled
for an executive control point. A control point in a nation that its holder has abandoned is worth
one tenth of that value to the holder. The faction on the other side of the table counts it in
full.

**Suppressed control points can be traded.** You can put a control point that is under a crackdown
on the diplomacy table. This includes a control point in a nation that its holder has abandoned.
The crackdown moves with the control point, and the new owner completes the remaining time to the
day. You cannot clear a crackdown with a trade. Off by default.

**Purging a friendly control point does not break the pact.** When you purge a suppressed control
point of a faction that has a non-aggression pact or a truce with you, that faction does not
become angry and the pact continues. A purge of any other control point makes the faction angry.
Off by default.

**Warn before a mission breaks a pact.** A mission against a faction that has a non-aggression
pact or a truce with you marks the selected target. The game asks you to confirm before it assigns
the councilor. On by default.

## Campaign options

Both of these options are lightly tested. The code does what this section describes, and the
projects and claim rows load without errors. Neither option has been played through a full
campaign.

**Spy slots become councilor slots.** The eight slots on the council screen are one pool. The pool
holds your councilors and the councilors you have turned in other factions. With no spies you have
eight councilors, with one spy you have seven, and with two spies you have six, as in the standard
game. You cannot turn a councilor while the pool is full.

Seats seven and eight are researched. Deep Cover Handlers (2,400) follows Covert Operations and
Applied Artificial Intelligence. Shadow Cabinet (4,800) follows Deep Cover Handlers and
Administration Algorithms. The game does not always offer them. With this option off, both
projects are hidden.

The AI obeys both caps. It does not keep a slot free for a spy, so AI factions fill their councils
and stop spying.

**Restored Empires claims.** Claim chains and four projects for old powers. This was a separate
mod and is now part of CataTweaks.

- The Sun Never Sets (United Kingdom, 25,000) claims the Empire and the Commonwealth beyond the
  settler dominions. The standard project Commonwealth Restored also gains Delhi, Calcutta and
  Ireland.

- New Liberia (Liberia, 20,000) claims Sierra Leone, the Ivory Coast, Accra, Togo-Benin and
  Libreville. The Dominion of America claims Liberia and Jamaica behind the standard project
  Greater Dominion. It reaches that coast when it releases Liberia, lets it grow, and absorbs it
  again.

- Mare Nostrum (Rome, 10,000) and Imperium Sine Fine (Rome, 25,000) claim the Mediterranean world,
  the eastern roads, Aksum and both American seaboards. Both appear in Broken Earth only, which is
  the one scenario that has the Roman Republic.

A claim on a territory that fought for independence is hostile. A claim on a small dependency that
left by agreement is peaceful. For a hostile claim you must conquer the territory, then use a
government push until the claim becomes peaceful, then release the nation, let it grow, and absorb
it again.

Broken Earth has a dense claim web, so these chains reach much further there than in the intact
starts. The scripts in tools/ recompute the reach for each scenario from the templates of the
game.

## Notes

A save made with this mod still loads without it.

Settings are stored in `CataTweaks.xml` in `Documents/My Games/TerraInvicta`, beside the folder
that holds the saves and options of the game. A `Settings.xml` left in the mod folder by an older
version is copied over on the next start. A `Settings.txt` from the version before that is read
once and then renamed to `Settings.txt.migrated`.

If you still have the separate Restored Empires mod installed, remove it. If you do not, its claim
rows are duplicated.

---

# Technical

## Layout

```
Plugin.cs                    every patch, and the Settings class
HabNamer.cs                  hab and template naming
CataTweaks.csproj            builds the DLL against the game assemblies
TIProjectTemplate.json       6 projects (2 council seats, 4 Restored Empires)
TIBilateralTemplate.json     391 claim rows
Localization/<lang>/         project names, summaries and descriptions
ModInfo.json                 UMM manifest
README.txt                   ships with the mod
workshop-description.txt     Steam BBCode
LICENSE                      MIT, ships with the mod
tools/                       claim generator, analysis, and the CI checks
test/                        standalone check for the hab namer
.github/workflows/ci.yml     runs tools/validate.py and tools/SyntaxCheck
```

The claim rows are not spread evenly across scenarios: 61 rows each for the untagged, `2026_`,
`2070_` and `2003_` namespaces, and 147 for `1962_`, because the 85 Rome rows exist in Broken
Earth only.

## Building

```
dotnet build -c Release
```

`CataTweaks.csproj` reads the game assemblies from the install directory through `$(GameDir)`,
which is overridable. Copy `bin/Release/net48/CataTweaks.dll` with the template, localization and
documentation files into `Terra Invicta/Mods/Enabled/CataTweaks/`.

CI cannot build the mod. The project references `Assembly-CSharp.dll`, nine Unity assemblies,
`UnityModManager.dll` and `0Harmony.dll` from the game install, and those are proprietary. CI
runs the two checks that need no game instead: `tools/validate.py` for the templates,
localization, version consistency and house style, and `tools/SyntaxCheck` to parse the sources
with Roslyn.

## How the settings screen is built

The options screen is hand-wired. Each vanilla setting is a separate serialized `Toggle` or
`Slider` field with its own handler, so there is no list to add a row to and rows must be cloned.

The tab is a clone of the Gameplay pane. `TabbedPaneManager` finds its tabs with
`GetComponentsInChildren<TabbedPaneController>`, so a clone beside the other panes registers
itself, and `TabbedPaneController.Start` wires its own tab button with a closure over itself. The
clone is built under a disabled object so `Awake` does not run before its serialized `tab` field
is corrected to point at the new button.

Rows are generated from the `[Draw]` attribute of each setting, so a label and its tooltip have
one source. Section headers, the percent and multiplier formatting of slider readouts, and the
graying of a row whose master setting is off are driven from small tables beside that code. The
viewport gets a `RectMask2D`, because the stencil `Mask` the game put there is disabled and never
had to clip anything until this tab made the list longer than the panel.

Every setting applies immediately, which is only possible because no patch uses Harmony's
`Prepare()`. Harmony evaluates that once at patch time, so a setting behind it could not change
without a restart. Four patches hold state of their own: the solar mirror cache, the borrowed
claim ledger, the preset validity cache and the repeatable capacity cache. The first two are
released from `Settings.OnChange` when their setting is cleared.

## How the campaign options are stored

They are entries in `ScenarioCustomizations.customFactionText` under keys that are not the name
of any faction. `TIFactionState`, `TISpaceAssetState` and `TISpaceShipTemplate` read that
dictionary by key and never enumerate it, so extra entries are inert.

The similar dictionary `customFactionStartingNationGroup` is not safe for this. Two places
iterate it, compare the integer value against `nation.template.group`, and then resolve the key
as a faction, so a foreign key there causes a null reference as soon as an integer collides with
a real group id.

Plain dictionary entries are also why a save written with this mod still loads without it. A
subclass of `ScenarioCustomizations` would serialize a `$type` that the deserializer could not
resolve.

## How the claim gating works

Claims are not filtered at query time. A project-gated claim is added to a nation only when the
project completes, by a loop in `TIFactionState` over every `TIBilateralTemplate` whose
`projectUnlockName` matches.

That loop tests `BilateralIsInScenario()`, not `BilateralIsActive()`. Hiding a project is
therefore not enough on its own: 25 of the shipped rows hang off the vanilla projects Commonwealth
Restored and Greater Dominion, and one is ungated. The mod patches both methods and suppresses
every row in its own `TIBilateralTemplate.json` when the campaign option is off. It reads that
file at run time to learn which rows are its own, so adding a claim needs no code change.

`TIRegionState.ClaimedBy` resolves a claim's gate by building the name
`"Claim" + nation.templateName + region.templateName`. Every vanilla row follows that convention.
The rows shipped here do not, so that lookup finds nothing for them and treats them as ungated.
This is why the gate has to be applied at the template rather than at the region.

`BilateralIsActive()` returns false unless `BilateralIsInScenario()` passes first, and then
returns `projectUnlock?.SomeoneHasDoneIt() ?? true`.

## Borrowed capital claims

`Recompute` reads `source.claims` and filters each entry through `TIRegionState.ClaimedBy`,
because a claim whose unlock project is unresearched can be present without being usable. It
recomputes when a nation is absorbed, when regions change hands, when a hostile claim turns
peaceful, when any project completes, on load, and monthly as a backstop.

Borrowed claims never reach a save file. `SaveAllGameStates` is bracketed so they are handed back
before serialization and lent again afterwards, with the restore in a finalizer so a save that
throws still returns them.

## Repeatable scaling arithmetic

The Nth repeat costs N times the base while granting the same flat amount, so research per point
of capacity is 300N in the base game (cost 1500, grant 5), 120N in Broken Earth (600) and 60N in
Adamantine Sky (300). With scaling `r` each repeat pays `base x (1 + r(N-1))`, so the cost per
point converges on `base / (grant x r)`. At the default 3% that is 10,000 per point in the base
game and 4,000 in Broken Earth.

## Notes on patching this game's UI

These facts took time to establish. The comments in `Plugin.cs` give more detail.

- Rows use duplicate names in a nested structure, such as
  `DynamicEarthLights/DynamicEarthLights`. Only the outer object is a sibling of the other
  settings.
- A clone keeps the state of its source at that moment. In a campaign that includes a
  `CanvasGroup` that is not yet interactable, and a `CanvasGroup` overrides
  `Selectable.interactable`. Measure `IsInteractable()` instead. The pane nests, so both of its
  groups need unlocking, not only the outer one.
- A clone also keeps editor-wired persistent listeners, and `RemoveAllListeners` does not remove
  them. Assign a new `UnityEvent` instead. The same applies to a `TooltipTrigger`, which arrives
  with `valueOnDemand` set and a delegate that cannot survive `Instantiate`.
- `?.` on a `UnityEngine.Object` does not use Unity's overloaded `==`. It reaches through a
  destroyed object and throws from native code.
- Harmony matches injected parameters by name. Renaming `__instance` makes it look for a
  parameter that does not exist, and that exception stops every other patch in the mod.
- `requiresNation` on a project rejects a nation that exists and is not extant. It does not
  reject a nation the scenario never contained, so a project naming one appears everywhere.
- `LocalizationManager.Find` returns the key itself when a string is missing. There is no English
  fallback, so every language needs every key.

## License

MIT. See [LICENSE](LICENSE).
