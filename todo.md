## priority 1
- bug: walls are pushed to infinity by spawning enemies. better guarantee for static colliders, or check when spawning enemies
- pushback should be in same direction as bullet velocity

## priority 2
- ui
- menus & scenes (states and sub states)
- more weapon types
- roguelite powerups

## priority 3
- juice: dmg indicators?, custom particles?
- sound
- web build
- rotated rectangles in physics engine?

## priority 4
- profiling
- better GlobalTransform using change detection on Transform https://www.flecs.dev/flecs/md_docs_2Queries.html#change-detection
- add sprite offset to sheet?
- refactor: don't expose structs as fields: https://docs.flatredball.com/flatredball/contributing/general-programming-flatredball-programming-style-guide#structs-as-fields
- cache queries: https://www.flecs.dev/flecs/md_docs_2Queries.html#performance-and-caching

## bugs
- bug sprite sorting not working correctly for large sprites. should add origin to sprite and consider it when sorting
- flow field LoS and jankiness (enemies don't go directly towards player)
- bug: program probably crashes if enemy spawn exactly on top of obstacle. Not 100% sure why
- bug: wonky displacement ever since added multithreading
- bug: spread rotation is not centered
- bug: multiple flashes on same sprite change initial color