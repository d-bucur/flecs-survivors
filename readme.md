## Phases
Too many physics phases? maybe physics at the end of Update

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

# Dev
## Hot reloading
Start with hot reloading
```
dotnet watch
```
## Errors
Error handling inside systems doesn't work with .NET 9.0. Run with following to use old system that reports errors correctly 
```
DOTNET_LegacyExceptionHandling=1 dotnet run
```
## Profiling
Install [dotnet-trace](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/dotnet-trace#install). 
```
# cd to build directory
dotnet-trace collect --format Speedscope -- flecs-test
```
Creates trace that can be loaded in [speedscope](https://www.speedscope.app/)

Or use [ultra](https://github.com/xoofx/ultra) for higher frequency sampling. dotnet-trace is limited to fixed 1ms which is not great