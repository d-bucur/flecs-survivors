# About
WIP survivors game built using flecs, Raylib, C# and custom solutions for everything else. More of a prototype for the tech than an actual game.

Features:
- [Flecs](https://www.flecs.dev/flecs/) ECS architecture
- [Raylib](https://www.raylib.com/index.html) as low level graphics API
- Multithreaded collision detection
- Collision broad phase using grid hashing
- Pathfinding with flow fields and steering behaviors
- Property tweening
- Sprite animations using one big packed atlas
- Tilemap rendering of level using [Tiled](https://www.mapeditor.org/) format
- Menus and state changes between scenes

Note: not all art assets are commited yet

# Dev
## Hot reloading
Start with hot reloading
```
dotnet watch
```
## Errors
Error handling inside systems [doesn't work](https://github.com/BeanCheeseBurrito/Flecs.NET/issues/92) with .NET 9.0. Run with following to use old system that reports errors correctly 
```
DOTNET_LegacyExceptionHandling=1 dotnet run
```

If the native library crashes, it won't normally show you the stack trace for the managed app. To get the C# stacktrace at the time of the crash you can run with coredumps enabled on crash
```
DOTNET_DbgEnableMiniDump=1 dotnet run 
```
Then open the coredump for more details
```
dotnet-dump analyze /path/to/coredump
clrstack
```
## Profiling
Install [dotnet-trace](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/dotnet-trace#install). 
```
# cd to build directory
dotnet-trace collect --format Speedscope -- flecs-survivors
```
Creates trace that can be loaded in [speedscope](https://www.speedscope.app/)

Or use [ultra](https://github.com/xoofx/ultra) for higher frequency sampling. dotnet-trace is limited to fixed 1ms which is not great

## Sprites
Packed sprite atlas is generated using [Free Texture Packer](https://github.com/odrick/free-tex-packer) and then processed into a runtime friendly format using a [python script](Content/sprites/sheet_json_format.py). Original split sprites are not includeed in this repo.

[Tilemaps](Content/tileset/map.tmj) are generated using Tiled

## Phases
TODO better description of phases

- OnLoad
- PostLoad: player input
- PreUpdate: process inputs before physics
- PrePhysics: spatial queries
- PhysicsPhase
- PostPhysics: update GlobalTransform
- OnUpdate: collision response, etc
- OnValidate: 
- PostUpdate: 
- PreStore: 
- OnStore: Rendering. Actually done in separate pipeline with RenderPhase

Some inspiration from [unity phases](https://docs.unity3d.com/6000.0/Documentation/Manual/execution-order.html)
And Bevy: https://docs.rs/bevy/latest/bevy/prelude/struct.Main.html

# Credits
## Art
TODO