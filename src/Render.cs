using System;
using System.Collections.Generic;
using System.Numerics;
using Flecs.NET.Core;
using Raylib_cs;

namespace flecs_test;

enum RenderPhase;
enum PostRenderPhase;

record struct Camera(Camera2D Value, int ScreenWidth, int ScreenHeight);
record struct RenderCtx(Vec2I WinSize);
struct Sprite(string Path) {
    public string Path = Path;
    public Texture2D? Texture = null;
}
record struct Content {
    Dictionary<string, Texture2D> Textures;
    public Content() {
        Textures = new();
    }

    public Texture2D Load(string path) {
        if (Textures.TryGetValue(path, out var ret)) {
            return ret;
        }
        ret = Raylib.LoadTexture(path);
        Textures[path] = ret;
        return ret;
    }
}

public struct Render : IFlecsModule {

    Query<Camera> cameraQuery;

    public unsafe void InitModule(World world) {
        cameraQuery = world.Query<Camera>();
        world.Set(new Content());

        world.Observer<Sprite, Content>()
            .Event(Ecs.OnSet)
            .TermAt(1).Singleton()
            .Each(LoadSprite);
        world.System<Camera, GlobalTransform>()
            .Kind(Ecs.PreUpdate)
            .Each(UpdateCameraTransform);
        world.System<GlobalTransform, Sprite>()
            .Kind<RenderPhase>()
            // monogame depth sorting is very finicky so do it here instead
            // TODO full sorting on each frame is expensive. Maybe some way to cache a more stable list using change detection?
            .OrderBy<GlobalTransform>(OrderSprites)
            // flecs recommends rendering here. Not sure how to do that using monogame since Draw is separate
            // .Kind(Ecs.OnStore) 
            .Iter(RenderSprites);

        world.System()
            .With<Player>()
            .Kind(Ecs.OnStart)
            .Iter(InitCamera);
    }

    private unsafe int OrderSprites(ulong e1, void* t1, ulong e2, void* t2) {
        var p1 = ((GlobalTransform*)t1)->Pos.Y;
        var p2 = ((GlobalTransform*)t2)->Pos.Y;
        return (int)((p1 - p2));
    }

    static void InitCamera(Iter it) {
        var world = it.World();
        var renderCtx = world.Get<RenderCtx>();
        var playerEntity = world.QueryBuilder<Transform>().With<Player>().Build().First();
        var cameraOffset = renderCtx.WinSize / 2;
        world.Entity("Camera")
            .Set(new Camera(new Camera2D(cameraOffset.ToNumerics(), Vector2.Zero, 0, 1), 800, 480))
            .Set(new Transform(Vector2.Zero, Vector2.One))
            .Set(new FollowTarget(playerEntity));
    }

    static void UpdateCameraTransform(ref Camera camera, ref GlobalTransform global) {
        var cam = camera.Value;
        cam.Target = global.Pos;
        camera.Value = cam;
    }

    static void LoadSprite(ref Sprite sprite, ref Content content) {
        sprite.Texture ??= content.Load(sprite.Path);
    }

    void RenderSprites(Iter it, Field<GlobalTransform> transform, Field<Sprite> sprite) {
        var camera = cameraQuery.First().Get<Camera>();
        Raylib.BeginMode2D(camera.Value);
        var cutoffDistance = MathF.Pow(camera.ScreenWidth, 2);

        foreach (int i in it) {
            var t = transform[i];
            // skip if too far away from camera
            if ((t.Pos - camera.Value.Target).LengthSquared() > cutoffDistance) continue;
            // pivot to bottom center of texture
            // TODO preload textures
            var offset = new Vector2(-sprite[i].Texture!.Value.Width / 2, -sprite[i].Texture!.Value.Height) * transform[i].Scale;
            Raylib.DrawTextureEx(sprite[i].Texture!.Value, t.Pos + offset, t.Rot, t.Scale.X, Color.White);
        }
        Raylib.EndMode2D();
    }
}