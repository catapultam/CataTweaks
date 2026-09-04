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
  "Name (Template, Orbit, Body)" - the hab's existing name, then the
  template and where it is: "Freeport (Mining, Low Earth Orbit 2,
  Earth)", "Peary Crater Base (Mining, Peary Crater, Luna)". Bodies
  with a single orbit, like Lagrange points, drop the orbit part:
  "Outpost (Mining, Earth-Luna L1)". Re-applying a template only
  replaces the part in parentheses; your custom name is kept.
  A hab that already has the template's modules but not the suffix
  (built by hand, renamed, pre-mod save) can still Apply - it just
  gets renamed.
  Saving a hab as a template names the template after the one in the
  parentheses, so "Freeport (Mining, ...)" saves as "Mining" and
  overwrites the previous version (vanilla appends description and
  timestamp). Update a hab, save, re-apply elsewhere - no manual
  renaming.
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

