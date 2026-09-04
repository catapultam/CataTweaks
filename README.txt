CataTweaks — installation

This is a code mod. It needs Unity Mod Manager (one-time setup):

1. Download Unity Mod Manager 0.25.0+ from https://www.nexusmods.com/site/mods/21
   (free Nexus account required).
2. Unzip anywhere and run UnityModManager.exe.
3. Game: "Terra Invicta", set the game folder path, installation method
   "DoorstopProxy", click Install.

That's it. UMM loads mods straight from Terra Invicta's Mods\Enabled
folder, so subscribing on the Steam Workshop (or dropping this folder in
place) is a complete install. The in-game Mods list toggle works too
(takes effect on restart).

What it does:
- Hab naming workflow: applying a saved hab template names the hab
  "Slug-Body-N" (template name, celestial body, unique number per
  slug+body: "Mining-Luna-1", "Mining-Luna-2", "Mining-Ceres-1").
  Re-applying a template to an already-named hab keeps its number.
  A hab that already has the template's modules but not the name
  (built by hand, renamed, pre-mod save) can still Apply - it just
  gets renamed.
  Saving a hab as a template strips the "-Body-N" back off, so the
  template is named just "Mining" and overwrites the previous version
  (vanilla appends description and timestamp). Update a hab, save,
  re-apply elsewhere - no manual renaming.
- Saving a priority preset under an existing custom preset's name
  overwrites it (vanilla disables the save button). If it was your
  default preset, the new version becomes the default, and every control
  point that was using the old version is updated to the new one (so
  countries keep tracking the profile instead of flipping to 'Custom').
  Built-in presets are never overwritten.
- Countries also keep tracking their preset when their valid priorities
  change (gaining a spaceflight program, founding a military, unlocking
  navy/STO/space-defense/nuke options...): the preset is re-applied with
  its weights for the newly valid priorities, within a day of the change.

