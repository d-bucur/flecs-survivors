## priority 1
- enemies use flow field. Not sure if workign correctly and some alignment issues.
  - Enemies don't seem to pathfind aroud obstacles correctly
- bug: program probably crashes if enemy spawn exactly on top of obstacle. Not 100% sure

## priority 2
- bug spread rotation is not centered
- player health and death
- add acceleration to movements
- tilesets. Use Ldtk maybe
- particle effects
- animations sprite changes
- animations tweens
- profile with Ecs.Log.SetLevel(1);

## priority 3
- ui
- sound
- better GlobalTransform using change detection on Transform https://www.flecs.dev/flecs/md_docs_2Queries.html#change-detection
- more weapon types
- roguelite powerups
- menus
- flocking behaviors
- profiling
- port to raylib?
  - https://github.com/raylib-cs/raylib-cs
  - https://github.com/MrScautHD/Raylib-CSharp
  - can probably be ported to web easier: https://github.com/Kiriller12/RaylibWasm
- juice