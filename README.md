# CataTweaks

Quality of life, interface and balance tweaks for [Terra Invicta](https://store.steampowered.com/app/1176470/Terra_Invicta/),
with settings on the game's own Settings screen rather than in a text file.

Both a code mod (Harmony patches, loaded by Unity Mod Manager) and a template mod
(projects and claim rows loaded by the game's own template system).

## What it does

`README.txt` is the player-facing list, and every setting carries a tooltip in game.
In short:

| Area | Tweaks |
|---|---|
| Quality of life and UI | hab template naming and icons, priority presets that overwrite in place and keep countries tracking them, dividers between stations on the ship construction screen, fleet detections that name the orbit or body, Customize Campaign remembering its options between sessions |
| Gameplay | solar mirrors boosting orbital stations under a configurable cap, Review Failed Projects favoring the expensive missed project, Demand Claim blocking only on a war between the two nations involved, holding a dormant nation's original capital borrowing its claims, repeatable management projects scaling their payoff with their rising cost |
| Per campaign | unused turned-councilor slots becoming councilor slots, unlocked by two new projects; Restored Empires claim chains for the United Kingdom, Rome, the Dominion of America and Liberia |

The last two are chosen on New Game / Customize Campaign and stored in the save, so a
campaign always plays the way it was started. Everything else applies the moment it is
clicked.

## Layout

```
Plugin.cs                    every patch, and the Settings class
HabNamer.cs                  hab and template naming
TIProjectTemplate.json       6 projects (2 council seats, 4 Restored Empires)
TIBilateralTemplate.json     391 claim rows, emitted per scenario
Localization/en/             project names, summaries and descriptions
ModInfo.json                 UMM manifest
README.txt                   ships with the mod
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

## Notes for anyone reading the patches

A few things about this game's UI that cost time to find out, and that the comments in
`Plugin.cs` go into properly:

- The options screen is hand-wired, one serialized `Toggle` or `Slider` field per setting,
  so rows have to be cloned from ones that already exist. Objects nest under duplicate
  names (`DynamicEarthLights/DynamicEarthLights`), and only the outer one is a sibling of
  the other settings.
- A clone carries whatever state its source held at that instant, including a
  `CanvasGroup` that is not interactable while a campaign is still loading. A
  `CanvasGroup` overrides `Selectable.interactable`, so `IsInteractable()` is the only
  useful thing to measure.
- Harmony evaluates `Prepare()` at patch time, so a setting gated there can never change
  without a restart. Every patch here reads its setting when it runs.
- `requiresNation` on a project is not the gate it looks like: `PrereqsSatisfied` only
  rejects a project whose required nation exists and is gone, so one naming a nation the
  scenario never had passes.
- `ScenarioCustomizations.customFactionText` is only ever read by key, which makes it a
  safe place to keep per-campaign data. Its sibling `customFactionStartingNationGroup`
  is iterated and matched on the int value, which makes it an unsafe one.

## License

MIT. See [LICENSE](LICENSE).
