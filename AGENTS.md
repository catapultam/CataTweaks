# CataTweaks

A Terra Invicta mod: one `Plugin.cs` of Harmony patches, plus template JSON and localization
that ship beside the DLL.

## Player-facing text is always localized

Terra Invicta has **no English fallback**. `LocalizationManager.Find` returns the key itself when
a string is missing, so a key with thirteen translations prints `UI.CataTweaks.Whatever` at
everyone else.

Any string the player reads in the game UI therefore gets a key in `Localization/<lang>/` for all
fourteen languages, never a hardcoded English literal and never an English-only key. Do this as
part of the change that adds the string - do not ask whether it is wanted, and do not ship the
English version first and translate later.

- Reuse a key the game already ships where one says what you mean. `Loc.T` takes `{0}` arguments.
- Otherwise add your own to `Localization/<lang>/UICataTweaks.<lang>`, which the game loads by
  extension from anywhere under the mod folder. Use the game's own vocabulary for terms it already
  translates - lift them out of `TerraInvicta_Data/StreamingAssets/Localization/<lang>/`.
- `tools/validate.py` fails if a key in the English file is missing from any of the other thirteen.
- `//` in a value is treated as a comment and truncates the line. `<br/>`, `<h>`, `<sp>` and
  `<rcol>` are markup the loader rewrites.

The `[Draw]` tooltips in `Settings` are the exception: those are the mod manager's own window,
which is English throughout.

## Checks

    dotnet build -c Release          # needs the game installed; CI cannot run it
    python tools/validate.py         # templates, claim rows, localization
    dotnet run --project tools/SyntaxCheck -c Release

`python tools/package.py [destination ...]` builds, validates, writes `dist/`, and fills each
destination given - the game's `Mods/Enabled/CataTweaks` is one.

## Commits

No AI attribution: no `Co-Authored-By` trailers, no "Generated with" lines.
