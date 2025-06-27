## priority 1
- add remaining enemies
- bullet sprites
- add attack animations
- tilesets. Use Ldtk maybe
- revisit death animation

## priority 2
- multiply everything by deltaT or can't do timescale
- player health and death
- particle effects
- juice:
  - dmg indicators?, particles
  - camera shake when hit
- add sprite offset to sheet?

## priority 3
- ui & menus
- add AABB colliders
- sound
- web build
- better GlobalTransform using change detection on Transform https://www.flecs.dev/flecs/md_docs_2Queries.html#change-detection
- more weapon types
- roguelite powerups

## priority 4
- profiling
- refactor: don't expose structs as fields: https://docs.flatredball.com/flatredball/contributing/general-programming-flatredball-programming-style-guide#structs-as-fields

## bugs
- bug: spread rotation is not centered
- flow field LoS and jankiness
- bug: program probably crashes if enemy spawn exactly on top of obstacle. Not 100% sure why
- bug: wonky movement ever since added multithreading
- bug sprite sorting not working correctly for large sprites. should add origin to sprite and consider it when sorting