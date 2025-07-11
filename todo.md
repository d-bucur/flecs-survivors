## priority 1
- menus & scenes (states and sub states)

## priority 2
- more weapon types
  - bullet damage
  - different bullet speeds
  - aoe
  - split bullets (SpawnOnDeath component)
  - bounce bullets
  - circle around player
  - global damage

## priority 3
- juice: dmg indicators?, custom particles?
- sound
- web build

## priority 4
- add sprite offset to sheet?
- profiling
- better GlobalTransform using change detection on Transform https://www.flecs.dev/flecs/md_docs_2Queries.html#change-detection
- refactor: don't expose structs as fields: https://docs.flatredball.com/flatredball/contributing/general-programming-flatredball-programming-style-guide#structs-as-fields
- cache queries: https://www.flecs.dev/flecs/md_docs_2Queries.html#performance-and-caching
- rotated rectangles in physics engine?
- too many entities? starting at ~500. Need to debug

## optional
- split screen multiplayer
- networking with rollback??

## bugs
- bug sprite sorting not working correctly for large sprites. should add origin to sprite and consider it when sorting. Also tilemap sorting would be nice
- flow field LoS and jankiness (enemies don't go directly towards player)
- bug: window resizing and mouse movement not working properly
- bug: wonky displacement ever since added multithreading. issue with hash sizes?
- bug: spread rotation is not centered
- bug: program probably crashes if enemy spawn exactly on top of obstacle. Not 100% sure why. Very rare
- bug: if stopping the main timer or disabling integration, getting a lot of div0s (distances). overlapping entities that are triggering collisions?
- bug: sometimes entities disappear. Maybe same as div0 errors above. Happening more often since added level up screen. Something weird happening around origin (0,0). Seems like an empty object is stuck there and colliding