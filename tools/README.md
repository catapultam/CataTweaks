# tools

`validate.py` runs the checks that do not need the game. CI calls it.

`SyntaxCheck/` parses the C# sources with Roslyn. CI calls it too, because the mod itself
cannot be compiled without the game assemblies.

`dotnet run --project tools/ConstructionCheck -c Release` checks the shared station queue against
the installed game's types. It validates patch targets and exercises the real dispatcher with
game pricing, eligibility, and payment stubbed at their boundaries. Checks cover parallel slots,
retained progress, pending queue migration, incompatible hulls, inactive yards, resource shortages,
priority recovery, loss of a yard, parallel refits, disabling the tweak, daily update/batch edit
guards, exclusion of bases and AI factions, mutually exclusive shipyard modes, and slot progress
calculations, dynamic section space allocation, and horizontal content/full-height card sizing.
Rally checks cover save metadata round trips, earliest affordable trajectory selection, fuel
failure classification, notification handling, cancellation, moving-target trajectory adoption,
and the rally option's shared-queue dependency. It needs
the game installed. Desktop CLR cannot execute Unity's engine ECalls, so prefab layout, real
resource/refit calculations, full campaign save round trips, actual rendezvous/merging, and full patch attachment still require an in-game
smoke test.

`dotnet run --project tools/NotificationCheck -c Release` reproduces the generated Harmony
wrapper name breaking native notification template lookup, then verifies the fixed detection
and ship completion patches with their options on and off. Incoming fleet and assault carrier
warnings are controls. It needs the game installed and stops at notification initialization;
actual alert presentation still needs an in-game smoke test.

`package.py` builds, validates and writes the release zip. `workshop.py` stages the Workshop item
in `dist/workshop/` and uploads it with SteamCMD's `workshop_build_item`, setting the page's
description from `workshop-description.txt`. SteamCMD needs one interactive login first, for
Steam Guard: `C:\steamcmd\steamcmd.exe +login NAME +quit`.

The rest came from the Restored Empires mod when it merged into CataTweaks. They generate and
analyse the claim rows.

- `make_mod.py` writes `TIBilateralTemplate.json`. Claims are authored once in map-layer region
  names. The script then emits one row for each scenario, because each scenario prefixes its
  template names and a row from one namespace does nothing in another. The script asserts that
  every region resolves, so a typo cannot produce an inert row in silence.
- `names.py` resolves a map-layer name to the name that each scenario uses. Broken Earth renames
  31 regions.
- `scen.py` computes the claim graph and the reach for each scenario.
- `be.py` does the same for Broken Earth, from the DLC templates.
- `mod_summary.py` and `build_mod_page.py` build a summary page from the result.

These scripts read the game's own template files. They therefore need Terra Invicta installed.
