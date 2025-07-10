using Flecs.NET.Core;
using Raylib_cs;
using System;
using System.Numerics;

namespace flecs_survivors;

// This is one big file with all game logic that doesn't fit in the others
// Will break off logical groupings from here as they get big enough
record struct Scenery;

record struct DespawnTimed(float TimeToDespawn, float TimeSinceSpawn = 0);
record struct FollowTarget(Entity Target, float FollowSpeed = 0.25f, float FollowAnticipation = 0);

public record struct Level(uint Value = 1);

record struct Health(int MaxValue = 1, float OnLossInvulnTime = 300) {
    public int Value = MaxValue;
    public bool IsInvulnerable = false;
    public float TimeSinceInvuln = 0;
}
record struct DeathEvent(Vector2 direction);

struct DebugConfig() {
    internal bool DebugColliders = false;
    internal bool DebugFlowFields = false;
    internal bool DebugSpatialMap = false;
    internal Entity? SystemIdColliders;
    internal Entity? SystemIdFlow;
    internal Entity? SystemIdSpatial;
}

class Main : IFlecsModule {
    public void InitModule(World world) {
        LevelLoader.InitLevel(ref world);
        world.Set(new DebugConfig());

        world.System<DespawnTimed>()
            .Kind(Ecs.PreUpdate)
            .TickSource(Timers.runningTimer)
            .Each(TickDespawnTimer);

        world.System<Health>()
            .Kind(Ecs.PreUpdate)
            .TickSource(Timers.runningTimer)
            .Each(UpdateInvulnerabilities);

        world.System<Tween>()
            .Kind(Ecs.OnUpdate)
            .Immediate()
            // Ideally we would have tweens on different timers, but here it uses the default one
            // So tweens shouldn't do gameplay stuff, otherwise they would not be pausable in menus
            // .TickSource(Timers.menuTimer)
            .Each(ProgressTweens);

        world.System<FollowTarget, Transform>()
            .Kind(Ecs.PreStore)
            .Each(MoveFollowTargets);

        world.System()
            .Kind(Ecs.PostLoad)
            .Kind(Ecs.Disabled)
            .Run((it) => Console.WriteLine($"Entities: {it.World().Count(Ecs.Any)}"));

        world.System<DebugConfig>()
            .TermAt(0).Singleton()
            .Kind(Ecs.PostLoad)
            .Each(CheckDebugKeys);
        world.System<GameCtx>()
            .Kind(Ecs.PostLoad)
            .Each(InputTimeScale);
    }

	static void MoveFollowTargets(ref FollowTarget follow, ref Transform transform) {
        var targetPos = follow.Target.Get<Transform>().Pos; // should be GlobalTransform
        var currentPos = transform.Pos;
        // Anticipation not working great for camera. Disabling now
        // Creates jerky movement. Maybe need non linear interp
        // var body = follow.Target.GetRef<PhysicsBody>();
        // Not sure if this optional check works 
        // if (body) targetPos += body.TryGet().Vel * follow.FollowAnticipation;

        transform.Pos = Vector2.Lerp(currentPos, targetPos, follow.FollowSpeed); ;
    }

    static void TickDespawnTimer(Iter it, int i, ref DespawnTimed despawn) {
        despawn.TimeSinceSpawn += it.DeltaTime();
        if (despawn.TimeSinceSpawn < despawn.TimeToDespawn) return;
        it.Entity(i).Destruct();
    }

    public static void ProgressTweens(Entity e, ref Tween tween) {
        if (!tween.target.IsAlive()) {
            e.Destruct();
            return;
        }
        tween.Tick(e.CsWorld().DeltaTime());
        if (tween.IsFinished()) {
            tween.Cleanup();
            e.Destruct();
        }
    }

    /// <param name="direction">Direction of damage</param>
    /// <returns>true if decrease was succesful (if target was not invulnerable), false otherwise</returns>
    public static bool DecreaseHealth(Entity e, Vector2 direction) {
        ref var health = ref e.GetMut<Health>();
        if (health.IsInvulnerable) return false;
        health.Value -= 1;
        if (health.OnLossInvulnTime > 0) {
            health.IsInvulnerable = true;
            health.TimeSinceInvuln = 0;
        }
        e.Modified<Health>();
        if (health.Value <= 0)
            e.Emit(new DeathEvent(direction));
        return true;
    }

    public static void SimpleDeath(Entity e, ref DeathEvent death) {
        e.Destruct();
    }

    private void UpdateInvulnerabilities(Entity e, ref Health health) {
        if (!health.IsInvulnerable) return;
        health.TimeSinceInvuln += e.CsWorld().DeltaTime();
        if (health.TimeSinceInvuln > health.OnLossInvulnTime) {
            health.IsInvulnerable = false;
        }
    }

    public static void FlashDamage(Entity entity) {
        entity.Children((e) => {
            e.Read((ref readonly Sprite sprite) => {
                new Tween(e).With(
                    (ref Sprite s, Color t) => s.Tint = t,
                    HSV.Hsv(0, 0, 0, 0),
                    HSV.Hsv(0, 0, 0, 1),
                    150,
                    Ease.QuartOut,
                    Raylib.ColorLerp,
                    AutoReverse: true
                ).RegisterEcs();
            });
        });
    }

    private void CheckDebugKeys(ref DebugConfig conf) {
        if (Raylib.IsKeyPressed(KeyboardKey.I)) {
            conf.DebugColliders = !conf.DebugColliders;
            if (conf.DebugColliders)
                conf.SystemIdColliders!.Value.Enable();
            else
                conf.SystemIdColliders!.Value.Disable();
        }
        if (Raylib.IsKeyPressed(KeyboardKey.K)) {
            conf.DebugFlowFields = !conf.DebugFlowFields;
            if (conf.DebugFlowFields)
                conf.SystemIdFlow!.Value.Enable();
            else
                conf.SystemIdFlow!.Value.Disable();
        }
        if (Raylib.IsKeyPressed(KeyboardKey.O)) {
            conf.DebugSpatialMap = !conf.DebugSpatialMap;
            if (conf.DebugSpatialMap)
                conf.SystemIdSpatial!.Value.Enable();
            else
                conf.SystemIdSpatial!.Value.Disable();
        }
    }

    private void InputTimeScale(Entity e, ref GameCtx ctx) {
        float? target = null;
        if (Raylib.IsKeyPressed(KeyboardKey.One)) {
            target = 0.2f;
        }
        if (Raylib.IsKeyPressed(KeyboardKey.Two)) {
            target = 0.5f;
        }
        if (Raylib.IsKeyPressed(KeyboardKey.Three)) {
            target = 1f;
        }
        if (Raylib.IsKeyPressed(KeyboardKey.Four)) {
            target = 2f;
        }
        if (target.HasValue) {
            Console.WriteLine($"Spawning");
            new Tween(e).With(
                (ref GameCtx ctx, float ts) => ctx.TimeScale = ts,
                ctx.TimeScale, target.Value, 1000, Ease.Linear
            ).RegisterEcs();
        }
    }

    internal static void CameraShake(float intensity) {
        var cam = CachedQueries.camera.First();
        cam.Read((ref readonly Camera c) => {
            var startOffset = c.Value.Offset;
            new Tween(cam).With(
                (ref Camera c, float v) => c.Value.Offset = startOffset + new Vector2(intensity * MathF.Sin(v * MathF.PI * 2), 0),
                0, 1, 300, Ease.QuartOut
            ).RegisterEcs();
        });
    }
}