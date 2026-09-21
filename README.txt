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
  with the options from the last campaign you set up. They are saved
  when you leave the screen, not only when you launch a campaign. Custom
  faction names are saved for each faction and return when that faction
  is selected.

- Hab template tweaks. Applying a saved hab template names the hab Name
  (Template, Orbit, Body), as in Freeport (Mining, Low Earth Orbit 2,
  Earth), and applies the template's map icon. Bodies with one orbit,
  such as Lagrange points, drop the orbit. Applying a template again
  replaces only the text in brackets.

  Saving a hab as a template names the template after the text in
  brackets and overwrites the existing template of that name. The Apply
  button stays enabled when the hab already has the modules of the
  template, so a template that changes nothing can still be applied.

- Priority presets overwrite in place. Saving a preset under an existing
  custom name replaces it. It stays the default preset if the old one
  was, and control points using the old version move to the new one.
  Built-in presets are not overwritten. Selecting a custom preset from
  the dropdown carries its name into the name field, and the field
  accepts a name that already exists.

- Nations keep their preset when priorities change. When a nation gains
  or loses a priority, such as a spaceflight program, a military, or
  navy, STO, space defense and nuclear options, the preset is applied
  again for the new set of priorities, within a day.

- Dividers between stations in the ship construction UI. A line is drawn
  between the shipyards of one station and the next. It follows the
  vanilla location filter.

- Fleet detections name the orbit or body. Detection notices include the
  hab's orbit, and the body for a landed fleet.


Gameplay


- Solar mirrors boost orbital stations. The solar modules of an orbital
  station receive the solar mirror bonus. A mirror lights targets that
  orbit inward of it, so a mirror in high Mars orbit boosts medium and
  low orbit, one in medium orbit boosts low orbit, and a mirror level
  with its target does nothing for it. Mirrors at Lagrange points light
  what they light in the vanilla game. Surface bases are unchanged.
  Power management is refreshed for affected stations when mirror
  numbers change. Off by default.

- Solar mirror orbital boost cap. The maximum output of a mirror-lit
  station's solar modules, as a multiple of their unlit output, from 2
  to 8. The vanilla ceiling is 8. Stations only.

- Project review favors the expensive project. Review Failed Projects
  weights each candidate by availability chance multiplied by research
  cost, rather than divided by it.

- Demand Claim ignores the target's other wars. Demand Claim is blocked
  only by a war between the two nations involved, rather than by the
  target being at war with anyone. Every other condition is unchanged.

- Holding a capital borrows that nation's claims. While you hold a
  dormant nation's original capital, and your claim on that capital is
  not hostile, you may use that nation's claims. Losing the capital, or
  that claim turning hostile, removes them. A claim the other nation
  held as hostile stays hostile for you. Nations that still hold
  territory are excluded. Save files are unchanged.

- Repeatable management project scaling. Each repeat of Management
  Research, Audience Research, Commercial Research and Operations
  Research pays its base reward plus this percentage for every repeat
  already completed. The slider runs from 0% to 20%, and 0% is the
  vanilla flat reward. Alien factions are not affected.

  The capacity bonus is calculated from the number of repeats, so it
  covers repeats completed before installing the mod. Resources already
  granted are not topped up. Project descriptions show the scaled value
  and the next completion.


Campaign options (New Game / Customize Campaign)


  Both of these are lightly tested. The code does what this section
  describes, and the projects and claim rows load without errors, but
  neither has been played through a campaign.

- Spy slots become councilor slots. The eight slots on the council
  screen become one pool shared between your councilors and councilors
  you have turned in other factions. No spies seats eight councilors,
  one spy seats seven, and two spies seats six, as in the vanilla game.
  Turning is blocked when the pool is full.

  Seats seven and eight are researched. Deep Cover Handlers (2,400)
  follows Covert Operations and Applied Artificial Intelligence. Shadow
  Cabinet (4,800) follows Deep Cover Handlers and Administration
  Algorithms. Neither is guaranteed to be offered. With this option off,
  both projects are hidden.

  The AI obeys both caps and does not keep a slot free for a spy, so AI
  factions fill their councils and stop spying.

- Restored Empires claims. Claim chains and four projects for old
  powers. This was a separate mod and is now part of CataTweaks.

  * The Sun Never Sets (United Kingdom, 25,000) claims the Empire and
    Commonwealth beyond the settler dominions. The vanilla project
    Commonwealth Restored also gains Delhi, Calcutta and Ireland.

  * New Liberia (Liberia, 20,000) claims Sierra Leone, the Ivory Coast,
    Accra, Togo-Benin and Libreville. The Dominion of America claims
    Liberia and Jamaica behind the vanilla project Greater Dominion,
    which gives it a route to that coast by releasing Liberia, letting
    it grow, and absorbing it again.

  * Mare Nostrum (Rome, 10,000) and Imperium Sine Fine (Rome, 25,000)
    claim the Mediterranean world, the eastern roads, Aksum and both
    American seaboards. Both appear in Broken Earth only, which is the
    one scenario the Roman Republic exists in.

  Claims on territories that fought their way out are hostile. Claims on
  small dependencies that left by agreement are peaceful. A hostile
  claim needs conquest, then a government push until the claim becomes
  peaceful, then release, regrowth and reabsorption.

  Broken Earth has a dense claim web, so these chains reach much further
  there than in the intact starts. The scripts in tools/ recompute the
  reach for each scenario from the game's own templates.


Notes

Settings are written to Settings.xml in the mod folder. A Settings.txt
from an older version is read once and carried over, then left behind as
Settings.txt.migrated.

A save made with this mod still loads without it. Campaign options are
stored as ordinary data the base game already understands, and borrowed
capital claims are never written to a save at all. Turning the council
slot pool off will leave a faction over the vanilla cap of six until it
dismisses councilors, which the game tolerates.
