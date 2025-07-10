using System;
using System.Numerics;
using Flecs.NET.Core;

namespace flecs_test;

record struct Powerup(ulong Value = 1);
record struct PowerCollector(float Range, float Exp = 2f) {
	public ulong AccumulatedCurrent = 0;
	public ulong AccumulatedTotal = 0;
	public ulong XpToNextLevel = 5;
	public ulong LevelCurrent = 1;
}

class PowerupModule : IFlecsModule {
	public void InitModule(World world) {
		world.System<Powerup, Transform, PhysicsBody>()
			.Kind(Ecs.PreUpdate)
			.Iter(AttractPowerups);

        // TODO handle powerups
        // world.Observer<PowerCollector, Shooter>()
        //     .Event(Ecs.OnSet)
        //     .Each(UpdateWeaponLevels);
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

    internal static void HandlePowerCollected(Entity e, ref OnCollisionEnter collision) {
        if (!collision.Other.Has<Powerup>() || !e.Has<PowerCollector>()) return;
        bool shouldDestructOther = false;
        collision.Other.Read((ref readonly Powerup powerup) => {
            var powerupValue = powerup.Value;
            e.Write((ref PowerCollector collector) => {
                // Increment XP and level up
                collector.AccumulatedCurrent += powerupValue;
                collector.AccumulatedTotal += powerupValue;
                long diff = (long)collector.AccumulatedCurrent - (long)collector.XpToNextLevel;
                if (diff >= 0) {
                    Console.WriteLine($"Level Up!");
                    // GameState.ChangeState(GameState.LevelUp);
                    collector.AccumulatedCurrent = (ulong)diff;
                    collector.LevelCurrent += 1;
                    collector.XpToNextLevel *= (ulong)collector.Exp;
                }
                shouldDestructOther = true;
            });
        });
        if (shouldDestructOther)
            collision.Other.Destruct();
    }

    static void UpdateWeaponLevels(ref PowerCollector collector, ref Shooter shooter) {
        // TODO update levelling logic
        var level = 1 + (uint)collector.AccumulatedCurrent / 5;
        foreach (var weapon in shooter.Weapons) {
            if (weapon.Level == level) continue;
            weapon.Level = level;
            Console.WriteLine($"Updated weapon level to {level}");
        }
    }
}