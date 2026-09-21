# CataTweaks

CataTweaks adds quality of life, interface and balance changes to
[Terra Invicta](https://store.steampowered.com/app/1176470/Terra_Invicta/). You set it up on
the game's own Settings screen. You do not edit a text file.

The mod contains two parts. Harmony patches run as code, and Unity Mod Manager loads them.
Projects and claim rows are templates, and the game's own template system loads them.

On the Steam Workshop:
[CataTweaks](https://steamcommunity.com/sharedfiles/filedetails/?id=3805519545).

---

## The settings screen

CataTweaks adds its own tab to the Settings screen. The tab works in the main menu and in a
loaded campaign. Each setting has one row. Each row has a tooltip.

The mod clones the tab from the Gameplay pane. It must clone it, because the Gameplay screen
is hand-wired. Each vanilla setting is a separate serialized `Toggle` or `Slider` field with
its own handler. There is no list to add a row to.

`TabbedPaneManager` finds its tabs with `GetComponentsInChildren<TabbedPaneController>`. A
clone beside the other panes therefore registers itself. `TabbedPaneController.Start` also
wires its own tab button with a closure over itself.

The mod builds the clone under a disabled object. This stops `Awake` from running too early.
The clone inherits a serialized `tab` field that points at the original button. The mod
corrects that field before it enables the pane.

The mod builds each row from the `[Draw]` attribute of the setting. The label and the tooltip
have one source. They cannot disagree.

```csharp
[Draw("Solar mirror orbital boost cap", DrawType.Slider, Min = 2f, Max = 8f,
    Tooltip = "The most a mirror-lit station's solar modules may produce ...")]
public float solarMirrorOutputCap = 8f;
```

Every setting applies immediately. No patch uses Harmony's `Prepare()`. Harmony calls
`Prepare()` once, at patch time. A setting behind `Prepare()` cannot change without a game
restart. Each patch therefore reads its setting when the patch runs.

Two patches hold state of their own. They release that state when you clear their setting.
One patch returns borrowed claims. The other patch clears the solar mirror cache.

The mod writes the settings to `Settings.xml` in the mod folder. If an older `Settings.txt`
exists, the mod reads it once and copies the values.

---

## Quality of life and UI

### Remember campaign options between sessions

The game stores your campaign options in `TIPlayerProfileManager.storedCampaignOptions`. The
Previous Campaign Settings button reads them back. Two things are missing.

First, only `OnLaunchCustomCampaignClicked` calls `StoreCampaignOptions`. The game therefore
stores nothing unless you start the campaign. Second, nothing applies the stored options
automatically.

This setting stores the options when the Customize Campaign panel closes. It stores them for
every exit: cancel, escape or launch. It then applies them where the screen resets to default
values.

The vanilla store has no field for custom faction names. The mod keeps those names in
`Settings.xml`. It stores one entry for each faction. It restores the names from
`LoadFactionTextFields`. That method is the only place where the game fills those four boxes
for a selected faction. A name for one faction cannot appear on a different faction.

### Hab template tweaks

When you apply a saved hab template, the mod names the hab `Name (Template, Orbit, Body)`. It
keeps the existing name. It then adds the template and the location. An example is
`Freeport (Mining, Low Earth Orbit 2, Earth)`. Some bodies have only one orbit, such as
Lagrange points. For those bodies, the mod removes the orbit from the name.

When you apply the template again, the mod replaces only the text in the brackets. Your custom
name stays.

The mod stores the map icon of the hab in the template. It applies that icon with the modules.
A Mining template therefore gives its icon to each hab that uses it.

When you save a hab as a template, the mod names the template after the text in the brackets.
It overwrites the previous template with that name. The game adds a description and a
timestamp instead.

The Apply button stays enabled when the hab already has the modules of the template. You can
therefore apply a template that changes nothing. Use this to rename a hab that you built by hand. It also
works for a hab from a save that is older than the mod.

### Priority presets overwrite in place

The game disables the save button when a preset name already exists. With this setting, the
mod replaces the existing custom preset.

The new preset stays the default preset if the old one was the default. The mod also moves
each control point from the old preset to the new one. Countries therefore continue to track
the preset by name. They do not change to Custom. The mod never overwrites a built-in preset.

### Nations keep their preset when priorities change

A nation can gain or lose a priority. Examples are a spaceflight program, a new military, or
navy, STO, space defense and nuclear options. The game changes the nation to Custom.

With this setting, the mod applies the preset again. It uses the weights of the preset for the
new set of priorities. The mod does this within one day of the change.

### Dividers between stations in the ship construction UI

One station can hold more than one shipyard. The list already groups these shipyards. The mod
draws a full-width line between the shipyards of one station and the next. The line obeys the
vanilla location filter.

### Fleet detections name the orbit or body

A vanilla message reads: "Surveillance telescopes have detected a new fleet docked at Montezuma
Base". This message helps you only if you know the location of Montezuma Base.

The game can write a longer form. The longer form adds the orbit after the hab. For a landed
fleet, it adds the body. The game uses this longer form in other places. The notification asked
for the short form. The mod asks for the long form.

---

## Gameplay

### Solar mirrors boost orbital stations

The game contains the complete solar mirror mechanism. It supports station-mounted mirrors and
per-body accumulation. It scales mirror mass with the square of the distance from the Sun. It
also applies an output ceiling.

The game uses this mechanism for surface bases only. `SolarPowerOutput` adds the mirror bonus
for a space body, a hab site, or a hab where `location.ref_hab.IsBase` is true. A station is
none of these.

With this setting, the solar modules of a station also receive the bonus. A direction rule
applies. A mirror lights targets that orbit inward of it only. A mirror in high Mars orbit
therefore boosts medium orbit and low orbit. A mirror in medium orbit boosts low orbit. It
does not boost high orbit. A mirror at the same level as its target gives that target nothing.

Lagrange points are outside the orbit ladder. They light every target that they already light
in the vanilla game. A Sun-Earth L1 mirror covers each Earth orbit and each Luna orbit. An
Earth-Luna L1 mirror covers Luna only.

The mod caches mirror values for each frame. The game caches its own hab queries in the same
way. The cache is necessary because power management, the hab screen and the AI planner all
call `SolarPowerOutput`.

This setting is off by default.

### Solar mirror orbital boost cap

This value is the maximum output of the solar modules of a mirror-lit station. The mod
expresses it as a multiple of the unlit output. The range is 2 to 8.

The vanilla ceiling is 8. The default value therefore changes nothing. Use a lower value to
reduce the effect of mirrors without disabling them.

The cap applies to stations only. The patch never runs for a surface base. Surface bases
therefore keep the vanilla ceiling of 8.

### Project review favors the expensive project

Review Failed Projects gives each candidate a weight. The game divides the availability chance
by the research cost. A project of 200 research therefore beats a project of 5000 research by
25 to 1. The review is least useful when you need it most.

The mod multiplies instead of divides. A high cost therefore increases the chance of a project.
The availability chance still scales the result. Rare projects stay rare.

The target of this patch is a compiler-generated local function. The mod finds it by name at
run time. The mod therefore attaches this one patch by hand, not through `PatchAll`. If the
name changes in a future game version, this patch fails alone. It writes the failure to the
log. The other patches continue to work.

### Demand Claim ignores the target's other wars

The game asks one question: is the target nation at war? It does not ask if the target nation
is at war with the nation that makes the demand.

A nation at war can therefore never give a region to any nation. An alien war does not end. It
therefore stops every peaceful border change on the map for the rest of the campaign. This
includes a transfer between two nations that one faction already controls. The game approves
that transfer without a prompt in other conditions.

With this setting, only a war between those two nations stops the demand. Each other vanilla
condition stays. You still cannot demand a capital. You still cannot demand with a hostile
claim. Both sides still need consolidated executive control. The improve-relations cooldown
still applies.

### Holding a capital borrows that nation's claims

`AbsorbNation` moves the regions, the control points, the nuclear weapons, the space program
and half of the accumulated investment. It moves no claims.

The absorbed nation keeps its claim list while it is dormant. To reach a region that it claims,
you must release the nation, rebuild its government, let it expand, and absorb it again.
Unification therefore runs from the outside in. If you merge inward first, each claim past
that point becomes unreachable.

With this setting, you borrow the claims. You do not receive them. You can use the claims of a
dormant nation while two conditions are true. You must hold the original capital of that
nation. Your claim on that capital must not be hostile. If you lose the capital, you lose the
claims. If your claim on the capital becomes hostile, you also lose them.

Hostility does not change. A claim that the other nation held as hostile stays hostile for you.
The purpose is to remove the merge sequence. The purpose is not to convert a hostile claim into
a peaceful one.

Nations that still hold territory are excluded. Those nations can act for themselves.

The mod tests each claim with `TIRegionState.ClaimedBy`. It does not read the `claims` list
directly. A claim with an unresearched unlock project is already in that list. The game filters
it at query time.

Borrowed claims never reach a save file. The mod returns them before `SaveAllGameStates` runs.
It lends them again after the save. The restore step is a finalizer. A save that throws an
exception therefore still returns the claims.

### Repeatable management project scaling

The Nth completion of a repeatable project costs N times the base cost. Management Research
grants the same flat 5 capacity each time. The research cost for each point of capacity is
therefore 120N. That value has no limit.

Each other source of control point capacity has a hard limit. Maintenance cost scales with
national GDP and has no limit. The repeatable project is therefore the only source that can
match the demand, and the vanilla price puts it out of reach.

With this setting, each repeat pays base x (1 + rate x (N-1)). The mod rounds the result. The
cost for each point of capacity therefore converges. At the default rate of 3%, it settles near
4,000 research for each point. That value is about ten times the median one-off capacity
project. The repeatable project stays a poor last resort. It does not become a shortcut.

Audience Research, Commercial Research and Operations Research use the same scaling. It applies
to their Influence, Money and Operations grants.

The mod calculates the capacity bonus from the number of repeats. The bonus therefore includes
repeats that you completed before you installed the mod. The mod does not add resources that
the game already granted.

The project descriptions show the scaled value in place of the base value. They also predict
the next completion. A rate of 0% restores the vanilla behavior.

---

## Campaign options

You select these two options on the New Game / Customize Campaign screen. The mod stores them
in the save. A campaign therefore always plays with the options that you started it with.

The rows are at the bottom of that screen, below a CataTweaks header. The mod clones the header
from the "Your Faction Names" header. It clones the toggle grid from the vanilla toggle grid.
The rows therefore use the vanilla cell size.

The mod stores the options in `ScenarioCustomizations.customFactionText`. It uses keys that are
not the name of any faction. The game reads this dictionary by key only, in `TIFactionState`
and `TISpaceAssetState`. It never enumerates the dictionary. Extra entries are therefore
inert.

The similar dictionary `customFactionStartingNationGroup` is not safe. Two places iterate that
dictionary and compare the integer value. They then resolve the key as a faction. A foreign key
there causes a null reference when an integer value matches a group id.

These entries are plain data. A save that you write with this mod therefore still loads without
the mod. A subclass of `ScenarioCustomizations` would write a `$type` value that the
deserializer cannot resolve, and the save would not open.

### Spy slots become councilor slots

The council screen has eight slots. Six slots hold your councilors. Two slots hold councilors
that you turned in other factions. The two spy slots are unused in most campaigns. This is true
of the second slot especially, because even the AI wants a spy only when it has none. A
complete council stops at six.

With this option, the eight slots become one pool. Each spy slot that you do not use becomes a
councilor slot. No spies gives you eight councilors. One spy gives you seven. Two spies give
you six, as in the vanilla game. The mod also stops a Turn mission when the pool is full. The
trade therefore works in both directions.

You must research seats seven and eight. **Deep Cover Handlers** costs 2,400. **Shadow
Cabinet** costs 4,800. Both continue the chain after Covert Operations. Each one also needs a
deeper information science technology. These are Applied Artificial Intelligence and then
Administration Algorithms. The prerequisites of Administration Algorithms include Quantum
Encryption. The game does not offer either project with certainty. If you turn this option off,
the mod hides both projects.

The vanilla council grid uses a fixed `TICouncilorState[8]` array. It writes turned councilors
at index 6. A seventh councilor therefore overwrites a spy. With this option, councilors fill
the array from the front and spies fill it from the back. The mod also sizes the array from the
true counts. This is necessary because an assassination can give a faction a vengeful defector
without a check against the cap.

`maxCouncilSize` also calculates the limit from the `CouncilSize` effects. The vanilla code uses
`Mathf.Min(6, ...)`, which discards the effects of the two new projects.

The AI obeys both caps without new code. Each consumer reads the same property. The AI has no
model of the trade. It will not keep a slot free for a spy. AI factions therefore fill their
councils and stop spying. This result is intended.

### Restored Empires claims

This option adds claim chains and four projects. They give old powers a route back to the map.
These files were a separate mod. They are now part of CataTweaks.

- **The Sun Never Sets** (United Kingdom, 25,000) extends Britain past the settler dominions to
  the whole Empire and Commonwealth. The vanilla project **Commonwealth Restored** also gains
  Delhi, Calcutta and Ireland. The Raj therefore sits at the cheaper tier, where Commonwealth
  membership puts it.
- **New Liberia** (Liberia, 20,000) unites Monrovia, Freetown and Libreville. Settlers founded
  all three for freed slaves, within forty years of each other. The **Dominion of America**
  claims Monrovia and Jamaica behind the vanilla project Greater Dominion. The Dominion
  therefore reaches Africa. It claims Monrovia, releases Liberia, lets Liberia grow along that
  coast, and absorbs it again.
- **Mare Nostrum** (Rome, 10,000) and **Imperium Sine Fine** (Rome, 25,000) give the Republic
  the Mediterranean world, the eastern roads, Aksum and both American seaboards.

A claim on a territory that fought its way out is hostile. A claim on a small dependency that
left by agreement is peaceful. A hostile claim needs more work. Conquer the region first. Then
push government until the claim becomes peaceful. Then release the nation, let it grow, and
absorb it.

The result changes with the scenario. Britain reaches 37 regions in the vanilla game. It
reaches about 198 regions in the intact starts. It reaches all 363 regions in Broken Earth.

The two Rome projects appear in Broken Earth only. The Roman Republic exists in that scenario
only. Broken Earth is also the only scenario where these chains reach the whole map.

New Liberia opens a coast outside Broken Earth. It does not open a continent. The claim web of
an intact start is sparse. The chain from Gabon reaches one region in the 2026 start. It
reaches 346 regions in Broken Earth.

The mod writes each claim row once, in map-layer region names. It then emits one row for each
scenario. This is necessary because each scenario adds a prefix to its template names. The
prefixes are `2026_`, `2070_`, `2003_` and `1962_`. The modern start has no prefix. A row from
one namespace does nothing in another namespace. The generator and the claim graph analysis are
in the archived [RestoredEmpires](https://github.com/catapultam/RestoredEmpires) repository.

Most rows need no extra code to gate them. Nearly every row depends on one of the four
projects. `BilateralIsActive()` already returns `projectUnlock?.SomeoneHasDoneIt() ?? true`. A
hidden project therefore also hides its claims. The mod gates the few ungated rows through
`BilateralIsActive`. `ClaimedBy` uses that same method.

---

## Layout

```
Plugin.cs                    every patch, and the Settings class
HabNamer.cs                  hab and template naming
TIProjectTemplate.json       6 projects (2 council seats, 4 Restored Empires)
TIBilateralTemplate.json     391 claim rows, one set for each scenario
Localization/en/             project names, summaries and descriptions
ModInfo.json                 UMM manifest
README.txt                   ships with the mod, written for players
workshop-description.txt     Steam BBCode
tools/validate.py            checks that run without the game
test/                        standalone check for the hab namer
```

## Building

```
dotnet build -c Release
```

`CataTweaks.csproj` reads the game assemblies from the install directory through `$(GameDir)`.
It therefore builds against the installed version. Override `$(GameDir)` if your install is in
a different place.

Copy `bin/Release/net48/CataTweaks.dll` and the template and localization files to
`Terra Invicta/Mods/Enabled/CataTweaks/`.

CI cannot build this mod. The project reads `Assembly-CSharp.dll` and eleven Unity assemblies
from the game install. Those files are proprietary, and this repository does not contain them.
`tools/validate.py` runs the checks that do not need the game.

## Notes on patching this game's UI

These facts took time to establish. The comments in `Plugin.cs` give more detail.

- Rows use duplicate names in a nested structure, such as
  `DynamicEarthLights/DynamicEarthLights`. Only the outer object is a sibling of the other
  settings. Walk out to the outermost container that holds exactly one control.
- A clone keeps the state of its source at that moment. In a campaign, this includes a
  `CanvasGroup` that is not yet interactable. A `CanvasGroup` overrides
  `Selectable.interactable`. Measure `IsInteractable()` instead.
- A clone also keeps editor-wired persistent listeners. `RemoveAllListeners` does not remove
  them. Assign a new `UnityEvent` instead.
- `?.` on a `UnityEngine.Object` does not use the overloaded `==` operator of Unity. It reaches
  through a destroyed object and throws an exception from native code.
- Harmony matches injected parameters by name. If you rename `__instance`, Harmony looks for a
  parameter that does not exist. The exception stops every other patch in the mod.
- `requiresNation` on a project rejects a nation that exists and is not extant. It does not
  reject a nation that the scenario never contained.

## License

MIT. See [LICENSE](LICENSE).
