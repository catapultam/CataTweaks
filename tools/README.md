# tools

`validate.py` runs the checks that do not need the game. CI calls it.

`localize.py --check` validates the fourteen project translation files against the current
project catalogue. Running it without `--check` orders their keys while preserving every
translated value. `python test/localize_check.py` verifies preservation and rejects missing
translations before any files are written. Checked-in translations are the source of truth.

`SyntaxCheck/` parses the C# sources with Roslyn. CI calls it too, because the mod itself
cannot be compiled without the game assemblies.

`dotnet run --project tools/RefCheck -c Release -- <CataTweaks.dll> <Managed> [<Managed> ...]`
resolves the built DLL against the game assemblies of each version given: every type, method
and field it references, and each member it finds by name, as Harmony finds it. A `[HarmonyPatch]`
target must be declared on its type, an `AccessTools` lookup may be on a base type, each kind
(method, field, property getter or setter) is found only as that kind, and a method named without
parameter types must have no overload. A name in `TargetMethods`, also in a loop or a `Select`, is
paired with that method's one `AccessTools` call. A member found by shape at run time, as
`GameVersions` does, is not seen. It needs the game assemblies, so CI cannot run it.

`dotnet run --project tools/ConstructionCheck -c Release` checks the shared station queue against
the installed game's types. It validates patch targets and exercises the real dispatcher with
game pricing, eligibility, and payment stubbed at their boundaries. Checks cover parallel slots,
retained progress, pending queue migration, incompatible hulls, inactive yards, resource shortages,
priority recovery, starts and enqueues refused by other mods, held jobs in finish estimates,
loss of a yard, parallel refits, disabling the tweak, daily update/batch edit
guards, exclusion of bases and AI factions, mutually exclusive shipyard modes, campaign option
persistence, capacity scaling cache invalidation and collection, and slot progress
calculations, dynamic section space allocation, and horizontal content/full-height card sizing.
Finish estimates count a waiting item that a yard refused today as starting there on the next
day, and place it in the yard where it finishes first, even before a busy yard that did not refuse it. The
patch-target check also confirms the Harmony priorities that order CataTweaks against Fleet
Manager and Crew Extended. Rally checks cover save metadata round trips, earliest affordable trajectory selection, fuel
failure classification, notification handling, cancellation, moving-target trajectory adoption,
and the rally option's shared-queue dependency. TI Fleet Manager checks run the bridge against
stand-in types with Fleet Manager's names and signatures. They cover which commission order a
ship claims and the fleet it returns, that orders are read by yard and design only (any or no
faction) and orders whose fleet is gone, that orders are not touched without shared queues or for
refits, that a remote multiplayer client follows Fleet Manager's own rule (the oldest order on the
building yard, not the station) and moves nothing, the commission line on the completion notice
and an error in it, the rally hold by Fleet Manager's own per-yard rule when the bind failed, the
order work is off, the claim threw or the orders cannot be read, the refit hold (only a refit
from a reserve fleet, as Fleet Manager's Patch_ReserveRefit; held when that cannot be read or the
bind failed; never without Fleet Manager; a failed refit read leaves the picker's reserve check
on), lost yards (every open order moves
to the survivor and stays open; nothing changes with shared queues off, for the last yard, for a
station yard with no queue, or for a broken yard), the multiplayer push of moved
orders, separate failure of the reserve check, a lookup that throws and a patch that fails
during binding, clearing reserve rally targets only while the rally is on and notices after a
new campaign, sorting into a detached fleet's mother fleet, the queue estimate postfix, and
that the bridge stays off without Fleet Manager. If the
Workshop copy of `TIFleetRework.dll` is installed beside the game, the check also confirms that
every Fleet Manager member CataTweaks reads still resolves and attaches the queue estimate
postfix to its `SumQueueDays`; if not, it prints a line and skips.
Capital claim checks cover the capital map with shared seats; which absorptions count as voluntary
mergers (alien absorber, dormant joiner, living joiner, and an absorb during an occupation,
through the real `BeforeCapture` and `AfterCapture`, nested and after a throw); the merger record,
its daily `Sweep`, its transfer to an heir by `Inherit`, and its save round trip; and the heir
choice of the dormant capital rule. If the Workshop copy of
`TISpaceNations.dll` is installed, the check also confirms that the six Space Nations claim grants
still resolve, take the own-list rewrite and no longer call the claim getters, and it runs two of
them after the rewrite (`Absorb` and the lambda in `MissingEarthClaims`); if not, it prints a line
and skips. Space Nations checks also cover a lookup that throws, missing Space Nations types, and
a claim grant that no longer reads the claim getters: each turns the bridge off. It needs
the game installed. Desktop CLR cannot execute Unity's engine ECalls, so prefab layout, real
resource/refit calculations, full campaign save round trips, actual rendezvous/merging, and full patch attachment still require an in-game
smoke test. On Linux, build the net48 harness and run its executable with Mono, passing the game
installation path as its argument and adding the game's Managed and Managed/UnityModManager
directories to `MONO_PATH`.

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
`names.py`, `make_mod.py`, `scen.py` and `mod_summary.py` accept the game installation path through
the `TERRA_INVICTA_DIR` environment variable; otherwise they use the standard Windows Steam path.
