## priority 1
- add remaining enemies and anims
- bullet sprites
- tilesets. Use Ldtk maybe
- revisit death animation
- pushback should be in same direction and bullet velocity
- temp invulnerability after hit
- particle effects

## priority 2
- player health and death
- juice:
  - dmg indicators?, particles
  - camera shake when hit
- add sprite offset to sheet?

## priority 3
- add AABB colliders for walls and maybe rotated rectangles?
- ui & menus
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