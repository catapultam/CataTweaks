CataTweaks - installation

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


Where the settings are

Every feature has its own switch on the game's own Settings screen,
under a CataTweaks tab, in the main menu and in a loaded campaign alike.
Changes take effect the moment you click them: no restart, no reload.
Each row has a tooltip describing what it does.

Two settings belong to a campaign rather than to you, so they live at
the bottom of New Game / Customize Campaign instead, under a CataTweaks
header. They are fixed for the life of that campaign and stored in its
save, so a campaign always plays the way it was started.


Quality of life and UI

- Remember campaign options between sessions. Customize Campaign opens
  with the options from the last campaign you set up instead of
  resetting to defaults, and they are saved when you leave the screen
  rather than only when you launch a campaign. Custom faction names are
  remembered per faction and reappear only when that faction is
  selected.

- Hab template tweaks. Applying a saved hab template names the hab
  "Name (Template, Orbit, Body)": the hab's existing name, then the
  template and where it is, as in "Freeport (Mining, Low Earth Orbit 2,
  Earth)". Bodies with a single orbit, like Lagrange points, drop the
  orbit part. Re-applying only replaces the part in brackets, so your
  custom name is kept, and the template's map icon is applied with it.
  Saving a hab as a template names the template after the text in
  brackets and overwrites the previous version in place, instead of
  appending a description and timestamp. A template that would change
  nothing can still be applied, so a hab can be renamed on its own.

- Priority presets overwrite in place. Saving a preset under an existing
  custom name replaces it, instead of the save button going dead. If it
  was your default it stays the default, and every control point using
  the old version is moved to the new one, so countries keep tracking
  the profile instead of flipping to Custom. Built-in presets are never
  overwritten.

- Nations keep their preset when priorities change. Gaining a
  spaceflight program, founding a military, unlocking navy, STO, space
  defense or nuclear options: the preset is re-applied with its weights
  for the newly valid priorities, within a day of the change.

- Dividers between stations in the ship construction UI. One station can
  host several shipyards and the list already groups them; this marks
  where one station's yards end and the next begin. It follows the
  vanilla location filter.

- Fleet detections name the orbit or body. "Surveillance telescopes have
  detected a new fleet docked at Montezuma Base" only helps if you
  already know where Montezuma Base is. The game can write the longer
  form and uses it elsewhere; the notification just asked for the short
  one.


Gameplay

- Solar mirrors boost orbital stations. Vanilla builds the whole
  mechanism, including station-mounted mirrors, per-body accumulation,
  mirror mass scaling with distance from the Sun squared and an output
  ceiling, but only ever spends it on surface bases. Now a station's
  solar modules benefit too, with a direction rule: a mirror only lights
  targets inward of itself, so a mirror in high Mars orbit boosts medium
  and low, and one in medium boosts low but never high. A mirror level
  with its target does nothing for it. Lagrange points sit outside the
  orbit ladder, so they light everything they already credit in vanilla.
  Surface bases are unchanged. This one ships off.

- Solar mirror orbital boost cap. The most a mirror-lit station's solar
  modules may produce, as a multiple of their unlit output. 8 is
  vanilla's own ceiling; lower it to blunt mirrors without switching
  them off. Applies to stations only, so surface bases keep the vanilla
  8x whatever this is set to.

- Project review favors the expensive project. Vanilla weights each
  candidate by availability chance divided by research cost, so a
  200-research throwaway outdraws a 5000-research drive by 25 to 1, and
  the review is least useful exactly when it matters most. That divide
  becomes a multiply, so cost raises a project's odds instead of sinking
  them. Availability chance still scales it, so genuinely rare projects
  stay rare.

- Demand Claim ignores the target's other wars. Vanilla checks "is this
  nation at war at all", not "at war with the nation asking", so any
  nation in any war can never cede a region to anyone. An alien war that
  never ends therefore freezes every peaceful border change on the map
  permanently, including transfers between two nations the same faction
  already controls. Now only a war between those two nations blocks it.
  Every other vanilla condition is unchanged: still no capitals, no
  hostile claims, both sides need consolidated executive control, and
  the improve-relations cooldown still applies.

- Holding a capital borrows that nation's claims. Absorbing a nation
  moves its regions, control points, nuclear weapons, space program and
  half its accumulated investment, and none of its claims, which is what
  forces unification to be worked strictly from the outside in. While
  you hold a dormant nation's original capital and your claim on that
  capital is not hostile, its claims are yours to use. Lose the capital,
  or have that claim turn hostile, and they go again. Claims the other
  nation held hostilely stay hostile for you, and nations that still
  hold territory of their own are excluded. Applies to AI nations as
  much as yours.

- Repeatable management project scaling. Management Research, Audience
  Research, Commercial Research and Operations Research cost N times the
  base on their Nth completion but pay the same flat amount every time,
  so research per point of capacity grows without limit. Each repeat now
  pays base x (1 + rate x (N-1)), rounded, so cost per point converges
  instead of diverging. At the default 3% it settles around 4,000
  research per point of control point capacity, roughly ten times the
  median one-off capacity project, so the grind stays a deliberately
  poor last resort rather than a shortcut. The capacity bonus is derived
  from the number of repeats, so it covers repeats finished before the
  mod was installed; the other three top up from the next completion on.
  Project descriptions show the scaled figure and predict the next
  completion. 0% restores vanilla.


Campaign options (New Game / Customize Campaign)

- Spy slots become councilor slots. The council screen has eight slots:
  six for your councilors and two for councilors you have turned in
  other factions. This makes the eight one pool, so every spy slot you
  are not using is a councilor slot instead. No spies seats eight; two
  spies seats six, exactly as vanilla. Turning is blocked when the pool
  is full, so the trade runs both ways.

  Seats seven and eight still have to be researched. Deep Cover Handlers
  (2,400) and then Shadow Cabinet (4,800) continue the chain after
  Covert Operations, and each also waits on a deeper information science
  tech. Neither is guaranteed to be offered. With this option off, both
  projects are hidden.

  The AI follows both caps without being taught, but it will not hold a
  slot open for a spy, so expect AI factions to fill up on councilors
  and stop spying. That is the intended trade.

- Restored Empires claims. Claim chains and four projects that give old
  powers a route back to the map.

  The Sun Never Sets extends the United Kingdom past the settler
  dominions, and vanilla Commonwealth Restored gains Delhi, Calcutta and
  Ireland. New Liberia lets the Dominion of America reach the West
  African coast: claim Monrovia, release Liberia, let it grow along the
  freedmen's coast, and take it back. The Dominion also claims Jamaica
  behind Greater Dominion. These apply in every scenario, though New
  Liberia opens only a few regions outside Broken Earth.

  Mare Nostrum and Imperium Sine Fine give Rome the Mediterranean world,
  the eastern roads, Aksum and both American seaboards. They appear in
  Broken Earth only, which is the one scenario the Roman Republic exists
  in, and the only one where these chains reach the whole map.

  Claims on territories that fought their way out are hostile; small
  dependencies are peaceful. Hostile claims need conquest, a government
  push until the claim turns peaceful, release, regrowth and
  reabsorption; peaceful ones can be merged diplomatically.

  This was previously the separate Restored Empires mod. If you have
  that installed, remove it, or its claim rows will be duplicated.


Notes

Settings are written to Settings.xml in the mod folder. A Settings.txt
from an older version is read once and carried over, then left behind as
Settings.txt.migrated.

A save made with this mod still loads without it. Campaign options are
stored as ordinary data the base game already understands, and borrowed
capital claims are never written to a save at all. Turning the council
slot pool off will leave a faction over the vanilla cap of six until it
dismisses councilors, which the game tolerates.
