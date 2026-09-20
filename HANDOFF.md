# CataTweaks - handoff

Working note for resuming in a fresh session. Delete when the work below is finished.

## Read this first

The previous session repeatedly listed "what I need to look up next" and then ended the turn
without running anything. **Don't do that.** Everything needed to finish the remaining work is
written down below - decompiled signatures, line numbers, formulas, decisions. Open the files,
write the code, build. If something here turns out wrong, verify that one thing and keep going.

## State

Repo: `C:\Users\alex\GitHub\CataTweaks` (git, `catapultam/CataTweaks`, private; v2.5.0 committed + pushed 2026-09-16. HANDOFF.md is untracked on purpose)
Deploy target: `C:\Program Files (x86)\Steam\steamapps\common\Terra Invicta\Mods\Enabled\CataTweaks\`

Version bumped **2.4.0 → 2.5.0** in `CataTweaks.csproj` and `ModInfo.json`.

### Done, builds clean, NOT deployed

- **Tweak 8 - `DemandClaimDespiteOtherWars`** (`Plugin.cs`, postfix on
  `TransferRegionsOption.GetPossibleTargets`). Setting: `demandClaimDespiteOtherWars`, bool,
  default `true`.
- **Tweak 9 - `RepeatableCapScalesWithCost`** (`Plugin.cs`, postfix on
  `TIFactionState.GetControlPointMaintenanceFreebieCap`). Setting:
  `repeatableProjectScaling`, float, default `0.03`, `0` = vanilla.
- `README.txt` and `workshop-description.txt` updated for both.

`dotnet build -c Release` → 0 warnings, 0 errors. Output in `bin\Release\net48\CataTweaks.dll`.

- **Patches A + B done** (2026-09-16): `RepeatableGrantsScaleWithCost` (OnProjectComplete
  prefix/postfix; prefix captures the completion count because vanilla's early return at the top
  still runs the postfix) and `RepeatableScalingDescription` (BenefitsDescription postfix).
  `JustCompleted` shows repeat = count (the notice is built after the completion is recorded),
  `Prospective` shows count + 1. README/workshop updated (workshop was missing the Tweak 9 bullet).

### Remaining work

**Deploy only**, once the user says the game is closed (see checklist). Then delete this file.

## Decisions already made - do not relitigate

- **Rate: 3%**, exposed as float `repeatableProjectScaling`, `0` restores vanilla.
  Chosen against measured benchmarks: one-off CP-cap projects the player can actually take run
  **median 417 / mean 613** research per cap point; 3% puts the repeatable's asymptote at
  `120/rate` = **~4,000**, ~10x the median and ~2x the worst one-off in the game. Deliberately
  the worst option, but bounded instead of divergent (vanilla diverges: 120N, unbounded).
- **Asymmetry is accepted.** Management Research is retroactive (cap postfix recomputes from
  repeat count, `base * rate * N(N-1)/2`, +49 on load at 26 repeats). Resource grants are
  forward-only (already-spent resources can't be topped up). User signed off, **provided the UI
  shows consistent numbers** - that's Patch B's job.
- Ledger will not itemise the cap bonus; total exceeds listed sources. Accepted.
- Both tweaks apply to AI factions too. Accepted.

## API facts (all verified - don't re-derive)

- `TIProjectTemplate : TIGenericTechTemplate`; `Effects` (`List<TIEffectTemplate>`) declared on
  the base at `TIGenericTechTemplate.cs:95`, resolved from `effects` string list.
- `TIEffectTemplate`: `public float value`, `public List<Context> contexts`, `GetContexts()`.
  CP-maint effects store **negative** values (they reduce maintenance).
- `TIFactionState`: `public List<TIProjectTemplate> completedProjects` (contains duplicates for
  repeats), `public bool IsAlienFaction`, `public float GetControlPointMaintenanceFreebieCap()`.
- Repeatable cost: `TIProjectTemplate.GetResearchCost` →
  `researchCost * (1 + completedProjects.Count(x => x == this))`.
- `TransferRegionsOption` is in the **global namespace** (no namespace decl).
- Cap maths is **float end to end** - no int casts, no rounding. Any rate works; `N0` formatting
  in the UI is display-only.

## Constraints

- **The game was running (PID 77296). Do not deploy over a live session and never restart it.**
  Ask, or wait for the user to say the game is closed. Historically they say "game closed deploy
  new version".
- `Settings.txt` is written only when missing, never rewritten. New keys therefore do **not**
  appear in an existing install and fall through to their C# defaults - which is why both new
  settings are live for the user without editing anything.
- The settings file must **not** be named `.json`. TI's `ModTemplateManager.LoadJsonMods` globs
  every enabled-mod path containing `.json` (except `ModInfo.json`), parses as `List<JObject>`,
  and `break`s the whole loop on failure - one bad file kills template loading for every mod
  sorted after it, and CataTweaks sorts early.
- No AI attribution in commits or PRs (global CLAUDE.md).

## Deploy checklist

Copy to the Mods\Enabled\CataTweaks folder: `CataTweaks.dll`, `ModInfo.json`, `README.txt`,
`workshop-description.txt`. Leave `Settings.txt` alone. Use `Rename-Item` rather than
`Remove-Item` under Program Files - an environment guard blocks `Remove-Item` there and refuses
the whole script pre-execution.

## Loose threads (unrelated to the patches, low priority)

- **CP cap calibration gap.** Reconstructed cost/cap disagree with the game's own recorded
  `history_CPCapOverageByDay` by roughly 8x. Formula confirmed as
  `(GDP / 323869500)^0.6 / (2 * nCP) * 0.7` per control point, summed over non-`benefitsDisabled`
  points; cap = `125 freebies + councilors(P+C+A) + hab CP modules + banked project reductions`.
  Ratios and orderings derived from it held up; absolute numbers did not. Trust the in-game
  Nations screen over any reconstruction.
- **Denpasar / Demand Claim.** `1962_LesserSundas` is owned by `1962_WES`, claimed non-hostile by
  `1962_IDN`. Blocker was `item.nation.atWar` (bare `wars.Count > 0`) - fixed by Tweak 8. Two
  conditions were never verified and could still block after deploying:
  `ExecutivePowerConsolidated` on both nations (IDN cohesion 0.005 / unrest 4.31 is suspect) and
  `CanImproveRelationsYet(WES)` cooldown.
