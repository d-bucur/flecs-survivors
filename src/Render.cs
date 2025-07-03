using System;
using System.Collections.Generic;
using System.Numerics;
using Flecs.NET.Core;
using Raylib_cs;

namespace flecs_test;

enum RenderPhase;
enum PostRenderPhase;

record struct Camera(Camera2D Value, int ScreenWidth, int ScreenHeight);
record struct RenderCtx(Vec2I WinSize, Shader SpriteShader);

struct Sprite(string Path, OriginAlign align = OriginAlign.BOTTOM_CENTER, Color? Tint = null) {
    public string Path = Path; // TODO don't need path after loading
    public Texture2D? Texture = null;
    public PackingData? Packing = null; // TODO should only keep my own key
    public Color Tint = Tint.GetValueOrDefault(Color.White);
    public OriginAlign Align = align;
    public Vector2? Origin = null;
    public Rectangle DrawSource;
    public bool Flipped = false;

    internal void SetDrawSource(Rectangle source) {
        DrawSource = source;
        if (Align == OriginAlign.BOTTOM_CENTER) {
            Origin = new Vector2(source.Width / 2, source.Height);
        }
        else {
            Origin = new Vector2(source.Width / 2, source.Height / 2);
        }
    }
}
enum OriginAlign {
    CENTER,
    BOTTOM_CENTER,
}
internal enum AnimationPlayMode {
    REPEAT,
    ONCE,
}

record struct Animator(
    string ObjectName, // TODO object name in spritesheet not needed after holding only my own key
    string CurrentAnimation = "default",
    float Speed = 1000f / 60f,
    AnimationPlayMode PlayMode = AnimationPlayMode.REPEAT
) {
    public int AnimationFrame = -1;
    public float TimeSinceUpdate = Speed;
    string DefaultAnimation = CurrentAnimation;

    internal void Progress(float deltaTime, SpriteSheet.KeyframeRaw[] animationRects) {
        TimeSinceUpdate += deltaTime;
        while (TimeSinceUpdate > Speed) {
            TimeSinceUpdate -= Speed;
            if (PlayMode == AnimationPlayMode.REPEAT || AnimationFrame + 1 < animationRects.Length)
                AnimationFrame = (AnimationFrame + 1) % animationRects.Length;
            else {
                AnimationFrame = 0;
                CurrentAnimation = DefaultAnimation;
                PlayMode = AnimationPlayMode.REPEAT;
            }
        }
    }

    // Check if animation exists needs to be done outside
    internal void PlayAnimation(string name, AnimationPlayMode playMode = AnimationPlayMode.ONCE) {
        if (CurrentAnimation == name)
            return;
        AnimationFrame = 0;
        TimeSinceUpdate = 0;
        CurrentAnimation = name;
        PlayMode = playMode;
    }
}

internal record struct TextureData(Texture2D Texture, PackingData? Packed);
record struct ContentManager {
    Dictionary<string, TextureData> TexturesCache;

    public ContentManager() {
        TexturesCache = new();
    }

    public TextureData Load(string path) {
        if (TexturesCache.TryGetValue(path, out var ret)) {
            return ret;
        }
        var texture = Raylib.LoadTexture(path);
        var data = SpriteSheet.LoadSheet(path);

        TexturesCache[path] = new TextureData(texture, data);
        return TexturesCache[path];
    }
}

public struct Render : IFlecsModule {
    private Query<Camera> cameraQuery;
    private Query<Health> playerHealthQuery;

    public unsafe void InitModule(World world) {
        cameraQuery = world.Query<Camera>();
        playerHealthQuery = world.QueryBuilder<Health>().With<Player>().Build();

        world.Set(new ContentManager());

        world.Observer<Sprite, ContentManager>()
            .Event(Ecs.OnSet)
            .TermAt(1).Singleton()
            .Each(LoadSprite);

        world.System<Camera, GlobalTransform>()
            .Kind(Ecs.PreUpdate)
            .Each(UpdateCameraTransform);

        world.System<Sprite, PhysicsBody>()
            .TermAt(1).Up()
            .Kind<RenderPhase>()
            .Each(FlipSprites);

        world.System<Animator, Sprite, ContentManager>()
            .TermAt(2).Singleton()
            .Kind<RenderPhase>()
            .Each(UpdateAnimator);
        world.System<GlobalTransform, Sprite>()
            .Kind<RenderPhase>()
            // TODO full sorting on each frame is expensive. Maybe some way to cache a more stable list using change detection?
            .OrderBy<GlobalTransform>(OrderSprites)
            .Run(RenderSprites);

        world.System()
            .Kind<RenderPhase>()
            .Run(DrawUI);

        world.System()
            .With<Player>()
            .Kind(Ecs.OnStart)
            .Iter(InitCamera);
    }

    private void FlipSprites(ref Sprite s, ref PhysicsBody b) {
        s.Flipped = b.Accel.X < 0;
    }

    private unsafe int OrderSprites(ulong e1, void* t1, ulong e2, void* t2) {
        var p1 = ((GlobalTransform*)t1)->Pos.Y;
        var p2 = ((GlobalTransform*)t2)->Pos.Y;
        return (int)(p1 - p2);
    }

    static void InitCamera(Iter it) {
        var world = it.World();
        var renderCtx = world.Get<RenderCtx>();
        var playerEntity = world.QueryBuilder<Transform>().With<Player>().Build().First();
        var cameraOffset = renderCtx.WinSize / 2;
        world.Entity("Camera")
            .Set(new Camera(new Camera2D(cameraOffset.ToVector2(), Vector2.Zero, 0, 1), 800, 480))
            .Set(new Transform(Vector2.Zero, Vector2.One))
            .Set(new FollowTarget(playerEntity));
    }

    static void UpdateCameraTransform(ref Camera camera, ref GlobalTransform global) {
        var cam = camera.Value;
        cam.Target = global.Pos;
        camera.Value = cam;
    }

    static void LoadSprite(ref Sprite sprite, ref ContentManager content) {
        TextureData textureData = content.Load(sprite.Path);
        sprite.Texture ??= textureData.Texture;
        sprite.Packing ??= textureData.Packed;
        if (textureData.Packed is null)
            sprite.SetDrawSource(new Rectangle(0, 0, textureData.Texture.Width, textureData.Texture.Height));
    }

    private void UpdateAnimator(Entity e, ref Animator animator, ref Sprite sprite, ref ContentManager content) {
        // TODO packed sprite without animator should default to the correct frame. It currently doesn't
        if (sprite.Packing is null)
            return; // static sprite

        var animationRects = sprite.Packing[animator.ObjectName][animator.CurrentAnimation];
        animator.Progress(e.CsWorld().DeltaTime(), animationRects);
        sprite.SetDrawSource(animationRects[animator.AnimationFrame]);
    }

    void RenderSprites(Iter it) {
        var camera = cameraQuery.First().Get<Camera>();
        var ctx = it.World().Get<RenderCtx>();
        var cutoffDistance = MathF.Pow(camera.ScreenWidth, 2);
        Raylib.BeginMode2D(camera.Value);
        Raylib.BeginShaderMode(ctx.SpriteShader);

        while (it.Next()) {
            var transform = it.Field<GlobalTransform>(0);
            var spriteField = it.Field<Sprite>(1);
            foreach (int i in it) {
                var t = transform[i];
                // skip if too far away from camera
                if ((t.Pos - camera.Value.Target).LengthSquared() > cutoffDistance) continue;

                Sprite sprite = spriteField[i];
                Vector2 origin = sprite.Origin!.Value * transform[i].Scale;
                Rectangle source = sprite.DrawSource;
                var dest = new Rectangle(t.Pos, source.Width * t.Scale.X, source.Height * t.Scale.Y);
                if (sprite.Flipped) {
                    source.Width = -source.Width;
                }

                Raylib.DrawTexturePro(
                    sprite.Texture!.Value,
                    source,
                    dest,
                    origin,
                    t.Rot,
                    sprite.Tint
                );
            }
        }
        Raylib.EndShaderMode();
        Raylib.EndMode2D();
    }

    private void DrawUI(Iter it) {
        // var camera = cameraQuery.First().Get<Camera>();
        var ctx = it.World().Get<RenderCtx>();
        Raylib.DrawFPS(10, ctx.WinSize.Y - 30);
        playerHealthQuery.Each((ref Health h) => {
            string healthText = $"Health: {h.Value}/{h.MaxValue}";
            DrawText(healthText, 10, 10, 20);
        });
        // TODO
        DrawText("Progress:", 10, 40, 20);
        DrawText("Entities:", 10, 70, 20);
    }

    private static void DrawText(string text, int x, int y, int fontSize) {
        Raylib.DrawText(text, x + 2, y + 2, fontSize, Color.Black);
        Raylib.DrawText(text, x, y, fontSize, Color.White);
    }
}