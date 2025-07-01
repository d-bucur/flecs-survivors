## priority 1
- bullet sprites
- tilesets. Use Ldtk maybe
- revisit death animation
- pushback should be in same direction as bullet velocity
- particle effects

## priority 2
- juice:
  - dmg indicators?, particles
  - camera shake when hit
- add AABB colliders for walls and maybe rotated rectangles?

## priority 3
- ui & menus
- sound
- web build
- more weapon types
- roguelite powerups

## priority 4
- profiling
- better GlobalTransform using change detection on Transform https://www.flecs.dev/flecs/md_docs_2Queries.html#change-detection
- add sprite offset to sheet?
- refactor: don't expose structs as fields: https://docs.flatredball.com/flatredball/contributing/general-programming-flatredball-programming-style-guide#structs-as-fields

## bugs
- bug: spread rotation is not centered
- flow field LoS and jankiness
- bug: program probably crashes if enemy spawn exactly on top of obstacle. Not 100% sure why
- bug: wonky movement ever since added multithreading
- bug sprite sorting not working correctly for large sprites. should add origin to sprite and consider it when sorting