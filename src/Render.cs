using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using Flecs.NET.Core;
using Raylib_cs;

namespace flecs_survivors;

#region Components
enum RenderPhase;
enum PostRenderPhase;

record struct Camera(Camera2D Value, int ScreenWidth, int ScreenHeight) {
    public Camera2D Value = Value;
}
record struct RenderCtx(Vec2I WinSize, Shader SpriteShader);

struct Sprite(string Path, OriginAlign align = OriginAlign.BOTTOM_CENTER, Color? Tint = null) {
    public string Path = Path; // TODO don't need path after loading
    public Texture2D? Texture = null;
    public PackingData? Packing = null; // TODO should only keep my own key
    public Color Tint = Tint.GetValueOrDefault(Color.White);
    public OriginAlign Align = align;
    public Rectangle DrawSource;
    public Vector2 Origin = Vector2.Zero;
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
    AnimationPlayMode PlayMode = AnimationPlayMode.REPEAT,
    Action? OnFinishCb = null
) {
    public int AnimationFrame = -1;
    public float TimeSinceUpdate = Speed;
    string DefaultAnimation = CurrentAnimation;

    internal void Progress(float deltaTime, SpriteSheet.KeyframeRaw[] animationRects) {
        TimeSinceUpdate += deltaTime;
        while (TimeSinceUpdate > Speed) {
            TimeSinceUpdate -= Speed;
            bool isFrameBelowMax = AnimationFrame + 1 < animationRects.Length;
            if (PlayMode == AnimationPlayMode.REPEAT || isFrameBelowMax)
                AnimationFrame = (AnimationFrame + 1) % animationRects.Length;
            else {
                AnimationFrame = 0;
                CurrentAnimation = DefaultAnimation;
                PlayMode = AnimationPlayMode.REPEAT;
            }
            if (!isFrameBelowMax) {
                OnFinishCb?.Invoke();
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
readonly struct Textures {
    public static readonly string MEGA_SHEET = "Content/sprites/packed2/characters.png";
};
#endregion

public struct Render : IFlecsModule {
    #region Init
    public unsafe void InitModule(World world) {
        world.Set(new ContentManager());
        InitCamera(ref world);

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
            .TickSource(Timers.runningTimer)
            .Each(UpdateAnimator);

        world.System<Tiled.TiledMap>()
            .Kind<RenderPhase>()
            .Each(RenderTiledMap);

        world.System<GlobalTransform, Sprite>()
            .Kind<RenderPhase>()
            // TODO full sorting on each frame is expensive. Maybe some way to cache a more stable list using change detection?
            .OrderBy<GlobalTransform>(OrderSprites)
            .Run(RenderSprites);

        world.System()
            .Kind<PostRenderPhase>()
            .Run(DrawUI);
    }
    #endregion

    #region Systems
    private void FlipSprites(ref Sprite s, ref PhysicsBody b) {
        s.Flipped = b.Accel.X < 0;
    }

    private unsafe int OrderSprites(ulong e1, void* t1, ulong e2, void* t2) {
        var p1 = ((GlobalTransform*)t1)->Pos.Y;
        var p2 = ((GlobalTransform*)t2)->Pos.Y;
        return (int)(p1 - p2);
    }

    static void InitCamera(ref World world) {
        var renderCtx = world.Get<RenderCtx>();
        var cameraOffset = renderCtx.WinSize / 2;
        world.Entity("Camera")
            .Set(new Camera(new Camera2D(cameraOffset.ToVector2(), Vector2.Zero, 0, 1), 800, 480));
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
        var camera = CachedQueries.camera.First().Get<Camera>();
        var ctx = it.World().Get<RenderCtx>();
        var cutoffDistance = GetCutoffDistance(camera);
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
                Vector2 origin = sprite.Origin * transform[i].Scale;
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

    private static void RenderTiledMap(Entity e, ref Tiled.TiledMap map) {
        var camera = CachedQueries.camera.First().Get<Camera>();
        var ctx = e.CsWorld().Get<RenderCtx>();
        var cutoffDistance = GetCutoffDistance(camera);
        Raylib.BeginMode2D(camera.Value);
        Raylib.BeginShaderMode(ctx.SpriteShader);

        foreach (var layer in map.Layers) {
            if (!layer.Visible) continue;
            for (int i = 0; i < layer.Tiles.Length; i++) {
                int tile = layer.Tiles[i];
                if (tile == 0) continue;

                var y = Math.DivRem(i, layer.Width, out var x);
                var pos = new Vector2(x * map.TileWidth, y * map.TileHeight);
                // skip if too far away from camera
                if ((pos - camera.Value.Target).LengthSquared() > cutoffDistance) continue;

                var (texture, source) = map.GetTileData(tile);
                var dest = new Rectangle(pos, map.TileWidth, map.TileHeight);
                Raylib.DrawTexturePro(
                    texture,
                    source,
                    dest,
                    Vector2.Zero,
                    0,
                    Color.White
                );
            }
        }
        Raylib.EndShaderMode();
        Raylib.EndMode2D();
    }

    private static float GetCutoffDistance(Camera camera) {
        return (camera.ScreenWidth * camera.ScreenWidth + camera.ScreenHeight * camera.ScreenHeight) * 0.6f;
    }

    private void DrawUI(Iter it) {
        var ctx = it.World().Get<RenderCtx>();

        const int margin = 10;
        float width = ctx.WinSize.X - margin * 2;

        CachedQueries.playerHealth.Each((ref Health h) => {
            string healthText = $"{h.Value}/{h.MaxValue}";
            var perc = (float)h.Value / h.MaxValue;
            DrawBar(perc, margin, width, 1, Color.Red, healthText);
        });
        CachedQueries.playerPower.Each((ref PowerCollector power) => {
            DrawBar((float)power.AccumulatedCurrent / power.XpToNextLevel, margin, width, 0, Color.Purple, $"{power.AccumulatedCurrent}/{power.XpToNextLevel} Lvl {power.LevelCurrent}");
        });

        DrawTextShadowed($"Entities: {it.World().Count<InGameEntity>()}",
            ctx.WinSize.X - 200, ctx.WinSize.Y - 30, 20);
        Raylib.DrawFPS(10, ctx.WinSize.Y - 30);

        var sb = new StringBuilder();
        var conf = it.World().Get<DebugConfig>();
        if (conf.DebugColliders)
            sb.Append(" Colliders");
        if (conf.DebugFlowFields)
            sb.Append(" Flow fields");
        if (conf.DebugSpatialMap)
            sb.Append(" Spatial map");
        if (sb.Length > 0) {
            DrawTextShadowed($"Debug:{sb}", 200, ctx.WinSize.Y - 30, 18);
        }
    }

    private static void DrawBar(float perc, int margin, float width, int pos, Color color, string text, float height = 25f) {
        const int lineThick = 3;
        float y = margin + pos * height + pos * 5;

        Rectangle rect = new Rectangle(margin, y, width, height);
        Raylib.DrawRectangleRec(rect, Raylib.Fade(Color.DarkGray, 0.7f));
        Raylib.DrawRectangleRec(new Rectangle(margin, y, width * perc, height), Raylib.Fade(color, 0.7f));
        Raylib.DrawRectangleLinesEx(rect, lineThick, Raylib.Fade(Color.Black, 0.3f));

        const int fontSize = 18;
        DrawTextShadowed(text, (int)width / 2, (int)(y + (height - fontSize) / 2f), fontSize);
    }

    public static void DrawTextShadowed(string text, int x, int y, int fontSize = 18) {
        Raylib.DrawText(text, x + 2, y + 2, fontSize, Color.Black);
        Raylib.DrawText(text, x, y, fontSize, Color.White);
    }
    #endregion
}