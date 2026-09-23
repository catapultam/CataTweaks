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
  with the options from your last campaign. The options are saved when
  you leave the screen, and not only when you start a campaign. A
  custom faction name is saved for each faction, and appears again
  when you select that faction.

- Hab template naming. When you apply a hab template, the hab is
  renamed to Name (Template, Orbit, Body), for example Freeport
  (Mining, Low Earth Orbit 2, Earth), and takes the map icon of the
  template. A body with one orbit, such as a Lagrange point, does not
  show the orbit. When you apply a template again, only the text in
  brackets changes.

  When you save a hab as a template, the template takes the name in
  brackets and replaces the template that has that name. The Apply
  button stays enabled when the hab already has the modules of the
  template, so you can apply a template that changes nothing and
  rename the hab.

- Priority presets overwrite in place. When you save a preset with the
  name of an existing custom preset, it replaces that preset. The new
  preset stays the default preset if the old one was the default.
  Control points that use the old preset move to the new one. Built-in
  presets are not replaced. When you select a custom preset from the
  dropdown, its name goes into the name field, and the field accepts a
  name that already exists.

- Nations keep their preset when priorities change. When a nation
  gains or loses a priority, its preset is applied again within one
  day. Examples are a spaceflight program, a military, a navy, and the
  STO, space defense and nuclear options. The nation does not change
  to Custom.

- Dividers between stations in the ship construction screen. The list
  shows a line between the shipyards of one station and the shipyards
  of the next station. The line follows the standard location filter.

- Fleet detections name the orbit or body. A fleet detection notice
  gives the orbit and the body of the hab. For a landed fleet it gives
  the body.

- Nation picker on the nation panel. The nation name becomes a
  dropdown. The dropdown lists the nations where you hold a control
  point, in name order. A previous arrow and a next arrow are on each
  side, and they continue from the last nation to the first. A nation
  where you hold no control point is at the top of the list, and the
  arrows lead from it into the list.


Gameplay

- Solar mirrors boost orbital stations. The solar modules of an
  orbital station receive the solar mirror bonus. A mirror lights the
  stations that orbit inward of it. A mirror in high Mars orbit lights
  medium orbit and low orbit, and a mirror in medium orbit lights low
  orbit. A mirror at the same level as a station does not light it. A
  mirror at a Lagrange point lights the same stations as in the
  standard game. Surface bases do not change. Power management is
  refreshed for the affected stations when the number of mirrors
  changes. Off by default.

- Solar mirror orbital boost cap. The maximum output of the solar
  modules of a mirror-lit station, as a multiple of their unlit
  output, from 2 to 8. This applies to orbital stations only. The
  standard limit is 8.

- Project review favors the expensive project. Review Failed Projects
  weights each project by its availability chance multiplied by its
  research cost. An expensive project is more likely to be selected.

- Boost priority prefers better launch sites. A completed Boost
  priority can build its launch facilities in the best available
  region of the nation. The best region is the eligible region nearest
  the equator. A region under occupation is not eligible. This applies
  to the Boost priority and to the first launch facilities of a new
  spaceflight program. The Boost priority tooltip shows the range of
  the eligible regions, and one figure when the result is certain.

  The AI also reads a southern latitude as a distance from the
  equator. This applies to its score for a nation, to its test for a
  useful launch site, and to the event that grants a spaceflight
  program.

- Launch site focus. How often the Boost priority builds in the best
  available region, from 0% to 100%. At 0% the game selects the region
  as the standard game does, which almost always adds to a region that
  already has launch facilities. At 100% the nation always builds in
  the best region. 50% by default.

- Any faction can go dormant, and can come back. The campaign option
  Allow AI Factions To Be Disabled spares five factions. Two of them
  are the aliens and your own faction, and they still cannot go
  dormant. The other three are spared for their part in the story, and
  they can now go dormant on the same terms as the others: no fleets,
  habs, councilors or control points on one day, from campaign year
  15. In a game with all factions the three are the Servants, the
  Protectorate and Humanity First. Humanity First is spared only while
  you play a faction that is not Humanity First or the Resistance.

  A dormant faction also comes back when it holds two control points
  again. The aliens continue to give control points to the Servants
  and the Protectorate with the Enthrall Elites and Terrorize
  missions, and you can trade a control point to a dormant faction. A
  faction that comes back has no money, no orgs and no income from its
  founding or from events, and runs on what its control points give
  it. The game shows no notification when a faction comes back.

  This setting does nothing unless the campaign option is on. Off by
  default.

- Demand Claim ignores the target's other wars. A war blocks Demand
  Claim only if it is a war between the two nations in the claim. The
  other conditions do not change.

- Holding a capital borrows that nation's claims. While you hold the
  original capital of a dormant nation, you can use the claims of that
  nation. Your claim on the capital must not be hostile. You lose the
  borrowed claims when you lose the capital, or when your claim on it
  becomes hostile. A claim that the other nation held as hostile stays
  hostile for you. A nation that still holds territory is excluded.
  Save files do not change.

- Repeatable management project scaling. Each repeat of Management
  Research, Audience Research, Commercial Research and Operations
  Research pays its base reward plus this percentage, for each repeat
  you have already completed. The slider is from 0% to 20%. At 0% each
  repeat pays the standard flat reward. Alien factions are not
  affected.

  The capacity bonus uses the number of repeats, so it includes
  repeats you completed before you installed the mod. Resources you
  have already received are not increased. Project descriptions show
  the scaled value and the next completion.

- Control points can be traded. Control points are on the diplomacy
  table with orgs, habs and projects, on a separate tab and grouped by
  nation. The AI gives them a value. It asks a price for its own
  control points, pays for yours, and offers control points in nations
  it has abandoned. Alien factions and control points in alien nations
  are excluded. On by default.

- Control point base value. How much the AI weights a control point
  against the other items on the table, from 0.5x to 5x. The value is
  six months of the income of the control point, as that faction
  values income, plus its investment points and the armies behind it.
  The value is doubled for an executive control point. A control point
  in a nation that its holder has abandoned is worth one tenth of that
  value to the holder. The faction on the other side of the table
  counts it in full.

- Suppressed control points can be traded. You can put a control point
  that is under a crackdown on the diplomacy table. This includes a
  control point in a nation that its holder has abandoned. The
  crackdown moves with the control point, and the new owner completes
  the remaining time to the day. You cannot clear a crackdown with a
  trade. Off by default.

- Purging a friendly control point does not break the pact. When you
  purge a suppressed control point of a faction that has a non-
  aggression pact or a truce with you, that faction does not become
  angry and the pact continues. A purge of any other control point
  makes the faction angry. Off by default.

- Show Triggered Projects keeps achievements. The Show Triggered
  Projects campaign option does not mark a campaign as custom
  difficulty. The Normal, Veteran and Brutal victory achievements stay
  available in that campaign. Every other campaign option still marks
  a campaign as custom difficulty, and a campaign that is already
  marked does not change. On by default.

- Warn before a mission breaks a pact. A mission against a faction
  that has a non-aggression pact or a truce with you marks the
  selected target. The game asks you to confirm before it assigns the
  councilor. On by default.


Campaign options

Both of these options are lightly tested. The code does what this
section describes, and the projects and claim rows load without
errors. Neither option has been played through a full campaign.

- Spy slots become councilor slots. The eight slots on the council
  screen are one pool. The pool holds your councilors and the
  councilors you have turned in other factions. With no spies you have
  eight councilors, with one spy you have seven, and with two spies
  you have six, as in the standard game. You cannot turn a councilor
  while the pool is full.

  Seats seven and eight are researched. Deep Cover Handlers (2,400)
  follows Covert Operations and Applied Artificial Intelligence.
  Shadow Cabinet (4,800) follows Deep Cover Handlers and
  Administration Algorithms. The game does not always offer them. With
  this option off, both projects are hidden.

  The AI obeys both caps. It does not keep a slot free for a spy, so
  AI factions fill their councils and stop spying.

- Restored Empires claims. Claim chains and four projects for old
  powers. This was a separate mod and is now part of CataTweaks.

  * The Sun Never Sets (United Kingdom, 25,000) claims the Empire and
    the Commonwealth beyond the settler dominions. The standard
    project Commonwealth Restored also gains Delhi, Calcutta and
    Ireland.

  * New Liberia (Liberia, 20,000) claims Sierra Leone, the Ivory
    Coast, Accra, Togo-Benin and Libreville. The Dominion of America
    claims Liberia and Jamaica behind the standard project Greater
    Dominion. It reaches that coast when it releases Liberia, lets it
    grow, and absorbs it again.

  * Mare Nostrum (Rome, 10,000) and Imperium Sine Fine (Rome, 25,000)
    claim the Mediterranean world, the eastern roads, Aksum and both
    American seaboards. Both appear in Broken Earth only, which is the
    one scenario that has the Roman Republic.

  A claim on a territory that fought for independence is hostile. A
  claim on a small dependency that left by agreement is peaceful. For
  a hostile claim you must conquer the territory, then use a
  government push until the claim becomes peaceful, then release the
  nation, let it grow, and absorb it again.

  Broken Earth has a dense claim web, so these chains reach much
  further there than in the intact starts. The scripts in tools/
  recompute the reach for each scenario from the templates of the
  game.


Notes

A save made with this mod still loads without it. Campaign options are
stored as ordinary data that the base game understands, and borrowed
capital claims are never written to a save. When you turn the council
slot pool off, a faction stays above the standard cap of six until it
dismisses councilors, which the game accepts.

Settings are stored in CataTweaks.xml in Documents/My
Games/TerraInvicta, beside the saves and options of the game. A
Settings.xml left in the mod folder by an older version is copied over
on the next start. A Settings.txt from the version before that is read
once and then renamed to Settings.txt.migrated.

If you still have the separate Restored Empires mod installed, remove
it. If you do not, its claim rows are duplicated.
