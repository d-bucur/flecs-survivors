using Microsoft.Xna.Framework;
using Flecs.NET.Core;
using System;
using System.Collections.Generic;
using MonoGame.Extended;
using static System.Linq.Enumerable;

namespace flecs_test;

// This is one big file with all game logic that doesn't fit in the others
// Will break off logical groupings from here as they get big enough

record struct Shooter(List<IBulletPattern> Weapons, float Time = 0) {
    public Vector2? Target;
}
record struct Projectile;
record struct Scenery;

record struct DespawnTimed(float TimeToDespawn, float TimeSinceSpawn = 0);

record struct Powerup(ulong Value = 1);
record struct PowerCollector(float Range, ulong Accumulated = 0);

record struct Health(int MaxValue = 1) {
    public int Value = MaxValue;
}

record struct FollowTarget(Entity Target, float FollowSpeed = 0.25f, float FollowAnticipation = 0);

class Main : IFlecsModule {
    public void InitModule(World world) {
        Level.InitLevel(ref world);

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

        world.System<Powerup, Transform, PhysicsBody>()
            .Kind(Ecs.PreUpdate)
            .Iter(AttractPowerups);

        world.System<FollowTarget, Transform>()
            .Kind(Ecs.PostUpdate)
            .Each(MoveFollowTargets);

        world.System()
            .Kind(Ecs.PostLoad)
            .Kind(Ecs.Disabled)
            .Run((it) => Console.WriteLine($"Entities: {it.World().Count(Ecs.Any)}"));
    }

    private void SetShooterTarget(Iter it, Field<Shooter> shooter, Field<GlobalTransform> transform) {
        // can cache query
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

        const float SPEED = 8;
        foreach (var i in it) {
            Vector2 dist = collector.Pos - transform[i].Pos;
            if (dist.LengthSquared() >= rangeSq) continue;

            dist.Normalize();
            body[i].Vel = dist * SPEED;
        }
    }

    static void TickDespawnTimer(Iter it, int i, ref DespawnTimed despawn) {
        despawn.TimeSinceSpawn += it.DeltaTime();
        if (despawn.TimeSinceSpawn < despawn.TimeToDespawn) return;
        it.Entity(i).Destruct();
    }

    static void ProcessShooters(Iter it, int i, ref Shooter shooter, ref Transform transform, ref Heading heading) {
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
            .Add<Projectile>()
            .Add<Trigger>()
            .Set(new Transform(pos, Vector2.One, 0))
            .Set(new PhysicsBody(dir, Vector2.Zero))
            .Set(new DespawnTimed(5000f))
            .Set(new Collider(17, Layers.PROJECTILE, Layers.ALL & ~Layers.POWERUP & ~Layers.PROJECTILE))
            .Set(new Health(2))
            .Observe<OnCollisionEnter>(HandleBulletHit);
        world.Entity()
            .Set(new Transform(new Vector2(0, 15), new Vector2(0.5f, 0.5f), 0))
            .Set(new Sprite("sprites/bee"))
            .ChildOf(bullet);
    }

    private static void HandleBulletHit(Entity e, ref OnCollisionEnter collision) {
        if (collision.Other.Has<Scenery>()) e.Destruct();
        if (!collision.Other.Has<Enemy>()) return;
        DecreaseHealth(e);
    }

    public static void DecreaseHealth(Entity e) {
        ref var health = ref e.GetMut<Health>();
        health.Value -= 1;
        e.Modified<Health>();
        if (health.Value <= 0) e.Destruct();
    }
}