## priority 1
- add keyboard shortcuts for debug systems
- multiply everything by deltaT or can't do timescale
- flow field LoS and jankiness
- bug: program probably crashes if enemy spawn exactly on top of obstacle. Not 100% sure why
- bug: wonky movement ever since added multithreading

## priority 2
- bug: spread rotation is not centered
- player health and death
- tilesets. Use Ldtk maybe
- particle effects
- juice:
  - dmg indicators?, particles
  - camera shake when hit

## priority 3
- ui
- sound
- better GlobalTransform using change detection on Transform https://www.flecs.dev/flecs/md_docs_2Queries.html#change-detection
- more weapon types
- roguelite powerups
- menus
- profiling
- refactor: don't expose structs as fields: https://docs.flatredball.com/flatredball/contributing/general-programming-flatredball-programming-style-guide#structs-as-fields