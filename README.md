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
the last campaign you set up. They are saved when you leave the screen, not only when you launch
a campaign. Custom faction names are saved for each faction and return when that faction is
selected.

**Hab template tweaks.** Applying a saved hab template names the hab
`Name (Template, Orbit, Body)`, as in `Freeport (Mining, Low Earth Orbit 2, Earth)`, and applies
the template's map icon. Bodies with one orbit, such as Lagrange points, drop the orbit.
Applying a template again replaces only the text in brackets.

Saving a hab as a template names the template after the text in brackets and overwrites the
existing template of that name. The Apply button stays enabled when the hab already has the
modules of the template, so a template that changes nothing can still be applied.

**Priority presets overwrite in place.** Saving a preset under an existing custom name replaces
it. It stays the default preset if the old one was, and control points using the old version move
to the new one. Built-in presets are not overwritten. Selecting a custom preset from the dropdown
carries its name into the name field, and the field accepts a name that already exists.

**Nations keep their preset when priorities change.** When a nation gains or loses a priority,
such as a spaceflight program, a military, or navy, STO, space defense and nuclear options, the
preset is applied again for the new set of priorities, within a day.

**Dividers between stations in the ship construction UI.** A line is drawn between the shipyards
of one station and the next. It follows the vanilla location filter.

**Fleet detections name the orbit or body.** Detection notices include the hab's orbit, and the
body for a landed fleet.

**Nation picker on the nation panel.** The nation's name becomes a dropdown of the nations you
hold a control point in, with a previous and a next arrow either side. The list is in name order
and the arrows wrap at either end. A nation you hold nothing in is shown as its own entry at the
top of the list, and the arrows lead from it into the list.

## Gameplay

**Solar mirrors boost orbital stations.** The solar modules of an orbital station receive the
solar mirror bonus. A mirror lights targets that orbit inward of it, so a mirror in high Mars
orbit boosts medium and low orbit, one in medium orbit boosts low orbit, and a mirror level with
its target does nothing for it. Mirrors at Lagrange points light what they light in the vanilla
game. Surface bases are unchanged. Power management is refreshed for affected stations when
mirror numbers change. Off by default.

**Solar mirror orbital boost cap.** The maximum output of a mirror-lit station's solar modules,
as a multiple of their unlit output, from 2 to 8. The vanilla ceiling is 8. Stations only.

**Project review favors the expensive project.** Review Failed Projects weights each candidate by
availability chance multiplied by research cost, rather than divided by it.

**Demand Claim ignores the target's other wars.** Demand Claim is blocked only by a war between
the two nations involved, rather than by the target being at war with anyone. Every other
condition is unchanged.

**Holding a capital borrows that nation's claims.** While you hold a dormant nation's original
capital, and your claim on that capital is not hostile, you may use that nation's claims. Losing
the capital, or that claim turning hostile, removes them. A claim the other nation held as
hostile stays hostile for you. Nations that still hold territory are excluded. Save files are
unchanged.

**Repeatable management project scaling.** Each repeat of Management Research, Audience Research,
Commercial Research and Operations Research pays its base reward plus this percentage for every
repeat already completed. The slider runs from 0% to 20%, and 0% is the vanilla flat reward.
Alien factions are not affected.

The capacity bonus is calculated from the number of repeats, so it covers repeats completed
before installing the mod. Resources already granted are not topped up. Project descriptions show
the scaled value and the next completion.

**Control points can be traded.** Control points join orgs, habs and projects on the diplomacy
table, under a tab of their own, grouped by nation. The AI values them: it asks a price for its
own and pays for yours, and it offers seats in nations it has walked away from. Alien factions,
and control points in alien nations, are excluded. On by default.

**Control point base value.** How heavily the AI weighs a control point against everything else
on the table, from 0.5x to 5x. The value under it is six months of the seat's income valued the
way that faction values income, plus its investment points and the armies behind it, doubled for
an executive. A seat in a nation its holder has abandoned is worth a tenth of that to them, so
abandoned seats change hands cheaply while the faction across the table still counts them in full.

**Suppressed control points can be traded.** Seats under a crackdown can be put on the table,
including seats in a nation their holder has abandoned, which are suppressed for the same reason.
The crackdown goes with the seat: the new owner serves out exactly what was left of it, to the
day, so trading a seat away and back is not a way to clear one. Off by default.

**Purging a friendly control point does not break the pact.** Purging a suppressed control point
held by a faction you have a non-aggression pact or a truce with does not hand them the hate that
would end it. Purging anything else angers them as usual. Off by default.

**Warn before a mission breaks a pact.** A mission aimed at a faction you have a non-aggression
pact or a truce with marks the chosen target and asks for confirmation before the councilor is
assigned. The vanilla game marks such a target while the list is open and then drops the mark from
the line it writes for the target you picked, which is the moment it matters. On by default.

## Campaign options

Both of these are lightly tested. The code does what this section describes, and the projects and
claim rows load without errors, but neither has been played through a campaign.

**Spy slots become councilor slots.** The eight slots on the council screen become one pool
shared between your councilors and councilors you have turned in other factions. No spies seats
eight councilors, one spy seats seven, and two spies seats six, as in the vanilla game. Turning
is blocked when the pool is full.

Seats seven and eight are researched. **Deep Cover Handlers** (2,400) follows Covert Operations
and Applied Artificial Intelligence. **Shadow Cabinet** (4,800) follows Deep Cover Handlers and
Administration Algorithms. Neither is guaranteed to be offered. With this option off, both
projects are hidden.

The AI obeys both caps and does not keep a slot free for a spy, so AI factions fill their
councils and stop spying.

**Restored Empires claims.** Claim chains and four projects for old powers. This was a separate
mod and is now part of CataTweaks.

- **The Sun Never Sets** (United Kingdom, 25,000) claims the Empire and Commonwealth beyond the
  settler dominions. The vanilla project **Commonwealth Restored** also gains Delhi, Calcutta and
  Ireland.
- **New Liberia** (Liberia, 20,000) claims Sierra Leone, the Ivory Coast, Accra, Togo-Benin and
  Libreville. The **Dominion of America** claims Liberia and Jamaica behind the vanilla project
  Greater Dominion, which gives it a route to that coast by releasing Liberia, letting it grow,
  and absorbing it again.
- **Mare Nostrum** (Rome, 10,000) and **Imperium Sine Fine** (Rome, 25,000) claim the
  Mediterranean world, the eastern roads, Aksum and both American seaboards. Both appear in
  Broken Earth only, which is the one scenario the Roman Republic exists in.

Claims on territories that fought their way out are hostile. Claims on small dependencies that
left by agreement are peaceful. A hostile claim needs conquest, then a government push until the
claim becomes peaceful, then release, regrowth and reabsorption.

Broken Earth has a dense claim web, so these chains reach much further there than in the intact
starts. The scripts in `tools/` recompute the reach for each scenario from the game's own
templates.

## Notes

A save made with this mod still loads without it.

Settings are written to `CataTweaks.xml` in `Documents/My Games/TerraInvicta`, beside the folder
the game keeps its own saves and options in. They are not kept in the mod folder: Terra Invicta
resyncs a Workshop mod folder against the subscribed copy on every launch and deletes anything
the Workshop item does not contain, which took the settings file with it. A `Settings.xml` left
in the mod folder by an older version is carried over on the next start, and a `Settings.txt`
from the version before that is read once and then renamed to `Settings.txt.migrated`.

If you still have the separate Restored Empires mod installed, remove it, or its claim rows will
be duplicated.

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
