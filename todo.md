## priority 1
- enemies use flow field. needs LoS and accelerations, and some flocking behaviors
- bug: program probably crashes if enemy spawn exactly on top of obstacle. Not 100% sure
- bug: wonky movement ever since added multithreading

## priority 2
- bug spread rotation is not centered
- player health and death
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