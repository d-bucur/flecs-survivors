using System;
using System.Numerics;
using Flecs.NET.Core;
using Raylib_cs;

namespace flecs_test;

class GameCtx() {
    internal float TimeScale = 1.0f;
}

public class Program {
    public static void Main() {
        var winSize = new Vec2I(800, 480);
        Raylib.InitWindow(winSize.X, winSize.Y, "Hello World");
        Raylib.SetWindowState(ConfigFlags.ResizableWindow);
        Raylib.SetWindowMinSize(winSize.X, winSize.Y);

        Raylib.SetTargetFPS(60);

        var game = new Game(winSize);

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
    Vec2I _winSize;
    GameCtx gameCtx;

    Rectangle source;
    RenderTexture2D frameBuffer;

    internal Game(Vec2I winSize) {
        _winSize = winSize;
        source = new Rectangle(0, 0, winSize.X, -winSize.Y);
        frameBuffer = Raylib.LoadRenderTexture(_winSize.X, _winSize.Y);
        Raylib.SetTextureFilter(frameBuffer.Texture, TextureFilter.Bilinear);
    }

    private enum RenderPhaseTop; // Not a great name. Equivalent of Ecs.Phase for rendering

    internal void InitEcs(Vec2I winSize) {
        _world = World.Create();

        // custom phases in between PreUpdate and OnUpdate
        _world.Entity<PrePhysics>().Add(Ecs.Phase).DependsOn(Ecs.PreUpdate);
        _world.Entity<OnPhysics>().Add(Ecs.Phase).DependsOn<PrePhysics>();
        _world.Entity<PostPhysics>().Add(Ecs.Phase).DependsOn<OnPhysics>();
        _world.Entity(Ecs.OnUpdate).DependsOn<PostPhysics>();

        // separate pipeline for rendering
        _world.Entity<RenderPhase>().Add<RenderPhaseTop>();
        _world.Entity<PostRenderPhase>().Add<RenderPhaseTop>().DependsOn<RenderPhase>();
        // query from https://www.flecs.dev/flecs/md_docs_2Systems.html#builtin-pipeline-query
        _renderPipeline = _world.Pipeline()
            .With(Ecs.System)
            .With<RenderPhaseTop>().Cascade(Ecs.DependsOn)
            .Without(Ecs.Disabled).Up(Ecs.DependsOn)
            .Without(Ecs.Disabled).Up(Ecs.ChildOf)
            .Build();

        // custom shader that support damage white flash
        Shader spriteShader = Raylib.LoadShader("", "Content/shaders/frag.fs");
        _world.Set(new RenderCtx(winSize, spriteShader));
        gameCtx = new();
        _world.Set(gameCtx);

        // _world.SetThreads(Environment.ProcessorCount);
        // _world.SetTaskThreads(Environment.ProcessorCount);

        _world.Import<GameStateModule>();
        _world.Import<CachedQueries>();
        _world.Import<Render>();
        _world.Import<PhysicsModule>();
        _world.Import<TransformsModule>();
        _world.Import<Main>();
        _world.Import<PlayerModule>();
        _world.Import<EnemiesModule>();
        _world.Import<ShootingModule>();
        _world.Import<PowerupModule>();
        // Ecs.Log.SetLevel(1);
    }

    internal void Update() {
        float deltaTime = Raylib.GetFrameTime() * 1000 * gameCtx.TimeScale;
        _world.Progress(deltaTime);
    }

    internal void Draw() {
        Raylib.BeginTextureMode(frameBuffer);
        Raylib.ClearBackground(Color.DarkBlue);
        _world.RunPipeline(_renderPipeline, Raylib.GetFrameTime() * 1000 * gameCtx.TimeScale);
        Raylib.EndTextureMode();

        // TODO some issues. Resizing window doesn't update screen width (at least on linux)
        // need to do virtual mouse too
        // https://www.raylib.com/examples/core/loader.html?name=core_window_letterbox
        var scale = MathF.Min(Raylib.GetScreenWidth() / _winSize.X, Raylib.GetScreenHeight() / _winSize.Y);
        var dest = new Rectangle(
            // Not centered
            0, 0,
            _winSize.X * scale,
            _winSize.Y * scale
        );

        Raylib.BeginDrawing();
        Raylib.ClearBackground(Color.Black);
        Raylib.DrawTexturePro(frameBuffer.Texture, source, dest, Vector2.Zero, 0, Color.White);
        Raylib.EndDrawing();
    }
}
