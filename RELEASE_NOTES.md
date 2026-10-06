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
