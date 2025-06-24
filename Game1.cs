using Flecs.NET.Core;
using Raylib_cs;

namespace flecs_test;

record struct GameCtx();

public class Program {
    public static void Main() {
        var winSize = new Vec2I(800, 480);
        Raylib.InitWindow(winSize.X, winSize.Y, "Hello World");

        Raylib.SetTargetFPS(60);

        var game = new Game();

        game.InitEcs(winSize);

        while (!Raylib.WindowShouldClose()) {
            game.Update();
            game.Draw();
        }

        Raylib.CloseWindow();
    }
}

public class Game {
    World _world;
    Pipeline _renderPipeline;

    internal void InitEcs(Vec2I winSize) {
        _world = World.Create();

        // custom phases in between PreUpdate and OnUpdate
        _world.Entity<PrePhysics>().Add(Ecs.Phase).DependsOn(Ecs.PreUpdate);
        _world.Entity<OnPhysics>().Add(Ecs.Phase).DependsOn<PrePhysics>();
        _world.Entity<PostPhysics>().Add(Ecs.Phase).DependsOn<OnPhysics>();
        _world.Entity(Ecs.OnUpdate).DependsOn<PostPhysics>();

        _world.Entity<PostRenderPhase>().Add(Ecs.Phase).DependsOn<RenderPhase>();
        _renderPipeline = _world.Pipeline()
            .With(Ecs.System)
            .With<RenderPhase>()
            .Build();

        _world.Set(new RenderCtx(winSize));
        // _world.SetThreads(Environment.ProcessorCount);
        // _world.SetTaskThreads(Environment.ProcessorCount);

        _world.Import<Render>();
        _world.Import<PhysicsModule>();
        _world.Import<TransformsModule>();
        _world.Import<Main>();
        _world.Import<PlayerModule>();
        _world.Import<EnemiesModule>();
        // Ecs.Log.SetLevel(1);
    }

    internal void Update() {
        _world.Progress(Raylib.GetFrameTime() * 1000);
    }

    internal void Draw() {
        Raylib.BeginDrawing();
        Raylib.ClearBackground(Color.DarkBlue);
        _world.RunPipeline(_renderPipeline, Raylib.GetFrameTime() * 1000);
        Raylib.EndDrawing();
    }
}
