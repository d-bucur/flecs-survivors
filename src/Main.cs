using Flecs.NET.Core;
using Raylib_cs;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace flecs_test;

// This is one big file with all game logic that doesn't fit in the others
// Will break off logical groupings from here as they get big enough

record struct Shooter(List<IBulletPattern> Weapons, float Time = 0) {
    public Vector2? Target;
    public bool Enabled = true;
}
record struct Bullet;
record struct Scenery;

record struct DespawnTimed(float TimeToDespawn, float TimeSinceSpawn = 0);

record struct Powerup(ulong Value = 1);
record struct PowerCollector(float Range, ulong Accumulated = 0);

record struct Health(int MaxValue = 1, float OnLossInvulnTime = 300) {
    public int Value = MaxValue;
    public bool IsInvulnerable = false;
    public float TimeSinceInvuln = 0;
}

record struct FollowTarget(Entity Target, float FollowSpeed = 0.25f, float FollowAnticipation = 0);

record struct DeathEvent(Vector2 direction);

struct DebugConfig() {
    internal bool DebugColliders = false;
    internal bool DebugFlowFields = false;
    internal Entity? SystemIdColliders;
    internal Entity? SystemIdFlow;
}

class Main : IFlecsModule {
    public void InitModule(World world) {
        Level.InitLevel(ref world);
        world.Set(new DebugConfig());

        world.System<Shooter, GlobalTransform>()
            .Kind(Ecs.PreUpdate)
            .Iter(SetShooterTarget);

        world.System<Shooter, Transform, Heading>()
            .Kind(Ecs.PreUpdate)
            .Immediate()
            .Each(ProcessShooters);

        world.System<DespawnTimed>()
            .Kind(Ecs.PreUpdate)
            .Each(TickDespawnTimer);

        world.System<Health>()
            .Kind(Ecs.PreUpdate)
            .Each(UpdateInvulnerabilities);

        world.System<Tween>()
            .Kind(Ecs.OnUpdate)
            .Immediate()
            .Each(ProgressTweens);

        world.System<Powerup, Transform, PhysicsBody>()
            .Kind(Ecs.PreUpdate)
            .Iter(AttractPowerups);

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
            .TermAt(0).Singleton()
            .Kind(Ecs.PostLoad)
            .Each(InputTimeScale);
    }

    private void SetShooterTarget(Iter it, Field<Shooter> shooter, Field<GlobalTransform> transform) {
        // TODO cache query
        var qEnemies = it.World().QueryBuilder<GlobalTransform>().With<Enemy>().Build();
        foreach (var shooterId in it) {
            var myPos = transform[shooterId].Pos;
            double smallestDistance = double.MaxValue;
            Vector2? closestEnemy = null;

            qEnemies.Each((ref GlobalTransform enemy) => {
                double distanceSqr = (myPos - enemy.Pos).LengthSquared();
                if (distanceSqr < smallestDistance) {
                    smallestDistance = distanceSqr;
                    closestEnemy = enemy.Pos;
                }
            });
            shooter[shooterId].Target = closestEnemy is null ? null : myPos - closestEnemy;
        }
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

    static void AttractPowerups(Iter it, Field<Powerup> powerup, Field<Transform> transform, Field<PhysicsBody> body) {
        var collectorQ = it.World().QueryBuilder().With<PowerCollector>().Build();
        var collector = collectorQ.First().Get<Transform>();
        var rangeSq = MathF.Pow(collectorQ.First().Get<PowerCollector>().Range, 2);

        const float SPEED = 0.3f;
        foreach (var i in it) {
            Vector2 dist = collector.Pos - transform[i].Pos;
            if (dist.LengthSquared() >= rangeSq) continue;

            dist = Vector2.Normalize(dist);
            body[i].Vel = dist * SPEED;
        }
    }

    static void TickDespawnTimer(Iter it, int i, ref DespawnTimed despawn) {
        despawn.TimeSinceSpawn += it.DeltaTime();
        if (despawn.TimeSinceSpawn < despawn.TimeToDespawn) return;
        it.Entity(i).Destruct();
    }

    static void ProcessShooters(Iter it, int i, ref Shooter shooter, ref Transform transform, ref Heading heading) {
        if (!shooter.Enabled) return;

        shooter.Time += it.DeltaTime();
        foreach (var weapon in shooter.Weapons) {
            var playerDir = heading.Value == Vector2.Zero ? Vector2.UnitX : Vector2.Normalize(heading.Value);
            foreach (var bullet in weapon.Tick(shooter.Time, playerDir, shooter.Target)) {
                SpawnBullet(it.World(), transform.Pos + bullet.Pos, bullet.Vel);
            }
        }
    }

    static void SpawnBullet(World world, Vector2 pos, Vector2 dir) {
        Entity bullet = world.Entity()
            .Add<Bullet>()
            .Add<Trigger>()
            .Set(new Transform(pos, Vector2.One, 0))
            .Set(new PhysicsBody(dir, Vector2.Zero, DragCoeff: 1))
            .Set(new DespawnTimed(5000f))
            .Set(new Collider(17, CollisionFlags.BULLET, CollisionFlags.ALL & ~CollisionFlags.POWERUP & ~CollisionFlags.BULLET))
            .Set(new Health(2))
            .Observe<OnCollisionEnter>(HandleBulletHit)
            .Observe<DeathEvent>(SimpleDeath);
        var sprite = world.Entity()
            .Set(new Transform(new Vector2(0, 0), new Vector2(2, 2), 0))
            .Set(new Sprite("Content/sprites/packed2/characters.png", OriginAlign.CENTER, new Color(146, 252, 245)))
            .Set(new Animator("bullets", "bullet1", 75))
            .ChildOf(bullet);
        RotationTween(sprite).RegisterEcs();
    }

    public static Tween RotationTween(Entity e) {
        return new Tween(e).With(
            (ref Transform t, float rot) => t.Rot = rot,
            0,
            360,
            500,
            (t) => t,
            Repetitions: -1
        );
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

    private static void HandleBulletHit(Entity bulletEntity, ref OnCollisionEnter collision) {
        if (collision.Other.Has<Scenery>()) bulletEntity.Destruct();
        if (!collision.Other.Has<Enemy>()) return;
        DecreaseHealth(bulletEntity, collision.Penetration);
    }

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
            e.Read(((ref readonly Sprite sprite) => {
                var startColor = sprite.Tint;
                new Tween(e).With(
                    (ref Sprite s, Color t) => s.Tint = t,
                    HSV.Hsv(0, 0, 0, 0),
                    HSV.Hsv(0, 0, 0, 1),
                    300,
                    Ease.QuartOut,
                    Raylib.ColorLerp,
                    OnEnd: (ref Sprite s) => s.Tint = startColor,
                    AutoReverse: true
                ).RegisterEcs();
            }));
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
    }

    private void InputTimeScale(ref GameCtx ctx) {
        if (Raylib.IsKeyPressed(KeyboardKey.One)) {
            Console.WriteLine($"Timescale 1d");
            ctx.TimeScale = 0.2f;
        }
        if (Raylib.IsKeyPressed(KeyboardKey.Two)) {
            ctx.TimeScale = 0.5f;
        }
        if (Raylib.IsKeyPressed(KeyboardKey.Three)) {
            ctx.TimeScale = 1f;
        }
        if (Raylib.IsKeyPressed(KeyboardKey.Four)) {
            ctx.TimeScale = 2f;
        }
    }
}