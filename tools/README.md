# tools

`validate.py` runs the checks that do not need the game. CI calls it.

`SyntaxCheck/` parses the C# sources with Roslyn. CI calls it too, because the mod itself
cannot be compiled without the game assemblies.

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
