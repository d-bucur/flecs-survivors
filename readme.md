# Dev
## Profiling
Install [dotnet-trace](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/dotnet-trace#install). 
```
# cd to build directory
dotnet-trace collect --format Speedscope -- flecs-test
```
Creates trace that can be loaded in [speedscope](https://www.speedscope.app/)

Or use [ultra](https://github.com/xoofx/ultra) for higher frequency sampling. dotnet-trace is limited to fixed 1ms which is not great