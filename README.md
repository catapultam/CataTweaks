# CataTweaks

Quality of life, interface and balance tweaks for [Terra Invicta](https://store.steampowered.com/app/1176470/Terra_Invicta/),
with settings on the game's own Settings screen rather than in a text file.

Both a code mod (Harmony patches, loaded by Unity Mod Manager) and a template mod
(projects and claim rows loaded by the game's own template system).

---

## The settings screen

CataTweaks adds a tab of its own to Terra Invicta's Settings screen, in the main menu and
in a loaded campaign alike, with a row per setting and a tooltip on each.

The tab is cloned from the Gameplay pane, because that screen is hand-wired: one
serialized `Toggle` or `Slider` field per setting, each bound to its own handler in the
editor, with no list to append to. `TabbedPaneManager` finds its tabs with
`GetComponentsInChildren<TabbedPaneController>`, so a clone parented beside the others
registers itself, and `TabbedPaneController.Start` wires its own button with a closure
over itself. The pane is built parented to a disabled object so that `Awake` does not run
before its serialized `tab` field is corrected to point at the new button instead of the
original's.

Rows are generated from the same `[Draw]` attributes that describe each setting, so a
label and its tooltip have exactly one home and the two cannot drift apart:

```csharp
[Draw("Solar mirror orbital boost cap", DrawType.Slider, Min = 2f, Max = 8f,
    Tooltip = "The most a mirror-lit station's solar modules may produce ...")]
public float solarMirrorOutputCap = 8f;
```

Every toggle applies the moment it is clicked. That is only possible because no patch here
uses Harmony's `Prepare()`, which is evaluated once at patch time: a setting gated there
could never take effect without a restart. Each patch reads its setting when it runs
instead, and the two that hold state of their own (borrowed claims, the solar mirror
cache) unwind it when their setting is cleared.

Settings are stored in `Settings.xml` in the mod folder. A `Settings.txt` from an older
version is read once and carried over.

---

## Quality of life and UI

### Remember campaign options between sessions

Vanilla stores your campaign options in `TIPlayerProfileManager.storedCampaignOptions`,
and the Previous Campaign Settings button reads them back. Two things are missing:
`StoreCampaignOptions` has exactly one caller, `OnLaunchCustomCampaignClicked`, so nothing
is remembered unless you go through with launching; and nothing applies them unasked.

This stores them as the Customize Campaign panel closes, whichever way it closes, and
applies them where the screen would otherwise reset to stock values.

Custom faction names are not part of vanilla's store at all, so they are kept in
`Settings.xml` keyed by faction and restored from `LoadFactionTextFields`, the one place
the game fills those four boxes for a chosen faction. A name written for the Resistance
never follows you to the Servants.

### Hab template tweaks

Applying a saved hab template names the hab `Name (Template, Orbit, Body)`: the hab's
existing name, then the template and where it is, as in
`Freeport (Mining, Low Earth Orbit 2, Earth)`. Bodies with a single orbit, such as
Lagrange points, drop the orbit part. Re-applying only replaces the text in brackets, so a
custom name survives.

The template's map icon is stored with it and applied along with the modules, so a "Mining"
template carries its icon to every hab it is applied to. Saving a hab as a template names
the template after the text in brackets and overwrites the previous version in place,
rather than appending a description and timestamp.

The Apply button also stays live when the hab already has the template's modules, so a
template that would change nothing can still be applied. That is the only way to rename a
hab that was built by hand or predates the mod.

### Priority presets overwrite in place

Vanilla disables the save button when a preset name is already taken. Saving over an
existing custom preset now replaces it: if it was your default it stays the default, and
every control point that was using the old version is re-pointed at the new one, so
countries keep tracking the profile by name instead of falling back to Custom. Built-in
presets are never overwritten.

### Nations keep their preset when priorities change

When a nation's set of valid priorities changes, by gaining a spaceflight program,
founding a military, or unlocking navy, STO, space defense or nuclear options, vanilla
drops it to Custom. The preset is re-applied with its weights for the newly valid
priorities, within a day of the change.

### Dividers between stations in the ship construction UI

One station can host several shipyards and the list already sorts them together. A
full-width rule marks where one station's yards end and the next begin, following the
vanilla location filter.

### Fleet detections name the orbit or body

"Surveillance telescopes have detected a new fleet docked at Montezuma Base" only helps if
you already know where Montezuma Base is. The game can write the longer form, with the
orbit after the hab and the body for a landed fleet, and uses it elsewhere. The
notification simply asked for the short one.

---

## Gameplay

### Solar mirrors boost orbital stations

Vanilla builds the entire mechanism: station-mounted mirrors, per-body accumulation,
mirror mass scaling with distance from the Sun squared, and an output ceiling. It then
spends it only on surface bases, because `SolarPowerOutput` adds the mirror bonus for a
space body, a hab site or `location.ref_hab.IsBase`, and a station is none of those.

Now a station's solar modules benefit too, with a direction rule: a mirror only lights
targets inward of itself, so a mirror in high Mars orbit boosts medium and low, one in
medium boosts low but never high, and a mirror level with its target does nothing for it.
Lagrange points sit outside the orbit ladder, so they light everything they already credit
in vanilla: a Sun-Earth L1 mirror covers every Earth and Luna orbit, an Earth-Luna L1
mirror covers Luna only.

Mirror lookups are cached per frame, matching how the game caches its own hot per-hab
queries, because `SolarPowerOutput` is called from power management, the hab screen and
the AI planner. This setting ships off.

### Solar mirror orbital boost cap

The most a mirror-lit station's solar modules may produce, as a multiple of their unlit
output, from 2 to 8. Vanilla's own ceiling is 8, so the default changes nothing; lower it
to blunt mirrors without switching them off. It applies to stations only, since the patch
never runs for a surface base, so bases keep the vanilla 8x whatever this is set to.

### Project review favors the expensive project

Review Failed Projects weights each candidate by availability chance divided by research
cost, so a 200-research throwaway outdraws a 5000-research drive by 25 to 1, and the review
is least useful exactly when it matters most. That divide becomes a multiply, so cost
raises a project's odds instead of sinking them. Availability chance still scales it, so
genuinely rare projects stay rare.

The target is a compiler-generated local function found by name at runtime, so this one
patch is attached by hand rather than through `PatchAll`. If the name ever changes it
fails alone and says so in the log, instead of abandoning every patch registered after it.

### Demand Claim ignores the target's other wars

Vanilla asks whether the target nation is at war at all, not whether it is at war with the
nation asking. Any nation in any war can therefore never cede a region to anybody, so an
alien war that never ends freezes every peaceful border change on the map for the rest of
the campaign, including transfers between two nations the same faction already controls,
which the game would otherwise approve without prompting.

Now only a war between those two nations blocks it. Every other vanilla condition is
untouched: no capitals, no hostile claims, both sides still need consolidated executive
control, and the improve-relations cooldown still applies.

### Holding a capital borrows that nation's claims

`AbsorbNation` moves regions, control points, nuclear weapons, the space program and half
the accumulated investment, and not one claim. The absorbed nation keeps its claim list
while it sits dormant, so the only route to anything it claimed is to release it, rebuild
its government, let it expand and take it back. That is what forces unification to run
strictly outside in: merge inward first and every claim past that point is stranded.

So the claims are borrowed, not granted. While you hold a dormant nation's original
capital and your claim on that capital is not hostile, its claims are yours; lose the
capital, or have that claim turn hostile, and they go again. Hostility carries across
unchanged, because the point is to skip the merge dance rather than to launder a grievance
into a peaceful merger. Nations that still hold territory of their own are excluded, since
they can speak for themselves.

Claims are checked through `TIRegionState.ClaimedBy` rather than the raw `claims` list,
because a claim whose unlock project is unresearched sits in that list already and is only
filtered at query time. Borrowed claims never reach a save file: `SaveAllGameStates` is
bracketed so they are handed back before serialization and lent again afterwards, with the
restore in a finalizer so a save that throws still gives them back.

### Repeatable management project scaling

A repeatable's Nth completion costs N times the base, but Management Research grants the
same flat +5 capacity every time, so research per point of capacity is 120N and grows
without limit. Every other source of capacity is hard-bounded while maintenance cost scales
with national GDP forever, which makes the repeatable the only source that can keep up, and
vanilla prices it out of reach.

Each repeat now pays base x (1 + rate x (N-1)), rounded, so cost per point converges. At
the default 3% it settles around 4,000 research per point, roughly ten times the median
one-off capacity project, so the grind stays a deliberately poor last resort rather than a
shortcut. Audience, Commercial and Operations Research get the same scaling on their
Influence, Money and Operations grants.

The capacity bonus is derived from the repeat count, so it covers repeats finished before
the mod was installed; resources already granted are not topped up. Project descriptions
show the scaled figure in place of the base one and predict the next completion. 0%
restores vanilla exactly.

---

## Campaign options

These two are chosen on New Game / Customize Campaign and stored in the save, so a campaign
always plays the way it was started. The rows sit at the bottom of that screen under a
CataTweaks header, with the header cloned from "Your Faction Names" so it matches, and the
toggles in a clone of the game's own toggle grid so they keep its cell sizing.

They are stored in `ScenarioCustomizations.customFactionText` under keys no faction is
named. That dictionary is only ever reached by key, in `TIFactionState` and
`TISpaceAssetState`, and never enumerated, so extra entries are inert. Its sibling
`customFactionStartingNationGroup` would not do: two sites iterate that one and match on
the int value, then resolve the key as a faction, so a foreign key there is a null
dereference waiting for a group id to collide.

Plain dictionary entries rather than fields of our own mean **a save written with this mod
still loads without it**. A subclass of `ScenarioCustomizations` would serialize a `$type`
that cannot resolve, and the save would not open at all.

### Spy slots become councilor slots

The council screen has eight slots: six for your councilors, two for councilors you have
turned in other factions. The spy slots sit idle in most campaigns, the second especially,
since even the AI only values a spy when it has none, while a fully developed council is
stuck at six.

The eight become one pool. Every spy slot you are not using is a councilor slot instead:
no spies seats eight, one spy seats seven, two spies seats six exactly as vanilla. Turning
is blocked when the pool is full, so the trade runs both ways.

Seats seven and eight still have to be researched. **Deep Cover Handlers** (2,400) and then
**Shadow Cabinet** (4,800) continue the chain after Covert Operations, each also waiting on
a deeper information science tech: Applied Artificial Intelligence, then Administration
Algorithms, whose own prerequisites include Quantum Encryption. Neither is guaranteed to be
offered. With the option off, both are hidden.

Two details worth knowing. Vanilla packs the council grid into a fixed
`TICouncilorState[8]` with turned councilors written at index 6, so a seventh councilor
lands on top of a spy; councilors now fill from the front and spies from the back, and the
array is sized off the actual counts as well, because an assassination can hand a faction a
vengeful defector without consulting the cap. And `maxCouncilSize` recomputes from
`CouncilSize` effects rather than vanilla's `Mathf.Min(6, ...)`, which was discarding the
two new projects.

The AI follows both caps without being taught, since every consumer reads the same
property, but it has no model of the trade and will not hold a slot open for a spy. Expect
AI factions to fill up on councilors and stop spying. That is the intended direction.

### Restored Empires claims

Claim chains and four projects that give old powers a route back to the map. Previously a
separate mod, now folded in.

- **The Sun Never Sets** (United Kingdom, 25,000) extends Britain past the settler
  dominions to the whole Empire and Commonwealth. Vanilla **Commonwealth Restored** also
  gains Delhi, Calcutta and Ireland, so the Raj sits at that cheaper tier where
  Commonwealth membership puts it.
- **New Liberia** (Liberia, 20,000) unites Monrovia, Freetown and Libreville, all founded
  within forty years of each other as settlements for freed slaves. The **Dominion of
  America** claims Monrovia and Jamaica behind vanilla Greater Dominion, so it reaches
  Africa by releasing Liberia, letting it grow along that coast, and taking it back.
- **Mare Nostrum** (Rome, 10,000) and **Imperium Sine Fine** (Rome, 25,000) give the
  Republic the Mediterranean world, the eastern roads, Aksum and both American seaboards.

Claims on territories that fought their way out are hostile; small dependencies that left
by agreement are peaceful. Hostile claims need conquest, a government push until the claim
turns peaceful, release, regrowth and reabsorption.

Reach varies sharply by scenario. Britain goes from 37 reachable regions to about 198 in
the intact starts and to all 363 in Broken Earth. Rome's two projects appear in Broken
Earth only, which is the one scenario the Roman Republic exists in, and the only one where
these chains close the map. New Liberia opens a coast rather than a continent outside
Broken Earth, because the intact-start claim web is sparse: Gabon's chain reaches one
region in 2026 against 346 in Broken Earth.

Claim rows are authored once in map-layer names and emitted per scenario, because every
scenario prefixes its template names (`2026_`, `2070_`, `2003_`, `1962_`, with the modern
start untagged) and **rows from one namespace are inert in another**. The generator that
does this, and the claim-graph analysis behind the routes, lives in the archived
[RestoredEmpires](https://github.com/catapultam/RestoredEmpires) repo.

Gating is mostly free: almost every row hangs off one of the four projects, and
`BilateralIsActive()` is already `projectUnlock?.SomeoneHasDoneIt() ?? true`, so hiding a
project takes its claims with it. The few ungated rows are gated directly through
`BilateralIsActive`, the same funnel `ClaimedBy` checks.

---

## Layout

```
Plugin.cs                    every patch, and the Settings class
HabNamer.cs                  hab and template naming
TIProjectTemplate.json       6 projects (2 council seats, 4 Restored Empires)
TIBilateralTemplate.json     391 claim rows, emitted per scenario
Localization/en/             project names, summaries and descriptions
ModInfo.json                 UMM manifest
README.txt                   ships with the mod, written for players
workshop-description.txt     Steam BBCode
test/                        standalone check for the hab namer
```

## Building

```
dotnet build -c Release
```

`CataTweaks.csproj` references the game's assemblies out of the install directory via
`$(GameDir)`, so it builds against whatever version is installed. Copy
`bin/Release/net48/CataTweaks.dll` and the template and localization files into
`Terra Invicta/Mods/Enabled/CataTweaks/`.

## Notes on patching this game's UI

Things that cost real time to establish, and that the comments in `Plugin.cs` go into
properly:

- Rows nest under duplicate names (`DynamicEarthLights/DynamicEarthLights`), and only the
  outer one is a sibling of the other settings. Walk out to the outermost container that
  still holds exactly one control.
- A clone carries whatever state its source held at that instant, which in a campaign
  means a `CanvasGroup` that is not yet interactable. A `CanvasGroup` overrides
  `Selectable.interactable`, so `IsInteractable()` is the only useful thing to measure.
- A clone also carries editor-wired persistent listeners. `RemoveAllListeners` does not
  clear those; assigning a fresh `UnityEvent` does.
- `?.` on a `UnityEngine.Object` does not consult Unity's overloaded `==`, so it happily
  reaches through a destroyed object and throws from native code.
- Harmony matches injected parameters by name. Renaming `__instance` turns a patch into a
  lookup for a parameter that does not exist, and the exception takes down every other
  patch with it.
- `requiresNation` on a project only rejects a nation that exists and is gone. One naming
  a nation the scenario never had passes.

## License

MIT. See [LICENSE](LICENSE).
