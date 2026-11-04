# CataTweaks 3.4.4

- Added compatibility with Systematic Hab Names. While that mod is loaded and enabled, applying
  a hab template renames the hab to Name (Template). For example: M-MAR-1 Tempe Terra (Mining).
  The short form has no location, because the name from that mod already gives it. Without that
  mod, the name stays Name (Template, Orbit, Body). If you switch off that mod in the Unity Mod
  Manager window, the long form stays. The long form also stays if the assembly of that mod
  loads but its own load step fails. In these two cases, a line in the Unity Mod Manager log
  tells you. If the assembly does not load, no line appears.
- Text in brackets at the end of a hab name is a template name in two cases. First, all of the
  text can be the name of one of your saved templates. Second, the text can have a comma. Then
  the template name is the text before the first comma, as before. Other text in brackets stays
  part of the custom name, so Station (Alpha) does not change. When you apply a template again,
  CataTweaks replaces the old template name in both forms. The names do not stack, also after
  you add or remove Systematic Hab Names. If you delete or rename a template, its old name in
  the short form becomes part of the custom name. CataTweaks cannot find a template name that
  contains brackets, so the next template name goes after it.
- Rename all my habs now in Systematic Hab Names keeps the name of the saved template of each
  hab. Apply and Rename all my habs now always read the short form (Template). Save reads it
  only while Systematic Hab Names is enabled.
- With Systematic Hab Names, you can save a hab that has no template name in brackets. Then the
  template keeps the name that the game gives it. CataTweaks does not shorten S-MAR-MED-1 to
  S-MAR-MED. Without that mod, saving a hab as a template works as in 3.4.3.
- A hab name that is empty or only spaces before you apply a template no longer starts with a
  space.

# CataTweaks 3.4.3

- Fixed missing fleet detection and ship construction/refit completion notifications.
  Harmony's generated method names were being used as notification template keys; the
  patched methods now select their native templates, including with mod options disabled.
- Added regression checks reproducing the failure and verifying the fixed lookups with
  options on and off. Incoming fleet and assault carrier template lookups pass as controls.

Validation: Release build, template/localization validation, C# syntax checks, and
notification and construction/rally regression checks passed. Actual alert presentation
still requires an in-game smoke test.

# CataTweaks 3.4.2

- Added one construction queue per station, with parallel construction and refit slots,
  retained progress, and waiting work preserved when a yard is lost. Surface yards keep
  individual queues. Unified queues and shipyard dividers are mutually exclusive.
- Reworked station cards into a horizontal scrolling row at stock width and full available
  height. Currently Building sizes dynamically and shows a progress bar per slot, with native
  hover/selection effects and a separate bordered well from the waiting queue. Yard labels use
  the module type without numbering; time is aligned left and ship class right with truncation.
- Added current construction/refit details to shipyard tooltips in hab management.
- Added **experimental automatic shipyard rally** as a separate option requiring unified
  station queues. Station cards offer a saved fleet selection, defaulting to None. New ships
  and completed refits choose the earliest affordable native rendezvous and merge on arrival.
- Successful rallies replace completion notices with detailed space events without a default
  popup. Insufficient delta-v leaves the ship at the yard and prepends an explanation to the
  normal completion notice. Disabling rally or unified queues stops new rallies and retains
  saved destinations; existing rally orders finish normally.
- Fixed the collapsed rally dropdown caption so it shows Rally: None or the selected fleet.
  Rally UI, notifications and settings text are localized in all fourteen game languages.

Rally remains experimental. Actual transfers, arrival merging, and full campaign save/load
behavior still need in-game verification. Later target course changes use the game's normal
change-trajectory prompt or cannot-chase handling; replacement transfers are not automatically
approved. Manual cancellation or redirecting to another destination cancels automatic merging.

Validation: Release build, localization/template validation, C# syntax checks, and 119 shipyard
and rally regression checks passed. The regression suite runs against installed game types;
Unity UI and complete gameplay transfers require an in-game smoke test.
