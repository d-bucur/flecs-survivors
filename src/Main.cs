using Microsoft.Xna.Framework;
using Flecs.NET.Core;
using System;

namespace flecs_test;

record struct Shooter(int Placeholder);
record struct Projectile;
record struct DespawnTimed(float TimeToDespawn, float TimeSinceSpawn = 0);

record struct Powerup(int Placeholder);
record struct PowerCollector(float Range);

class Main : IFlecsModule
{
	public void InitModule(World world)
	{
		var tickSource = world.Timer().Interval(1000f);
		world.System<Shooter, Transform>()
			.TickSource(tickSource)
			.Kind(Ecs.PreUpdate)
			.Each(ProcessShooters);

		world.System<DespawnTimed>()
			.Kind(Ecs.PreUpdate)
			.Each(TickDespawnTimer);

		world.System<Powerup, Transform, PhysicsBody>()
			.Kind(Ecs.PreUpdate)
			.Iter(AttractPowerups);
	}

	private void AttractPowerups(Iter it, Field<Powerup> powerup, Field<Transform> transform, Field<PhysicsBody> body)
	{
		var collectorQ = it.World().QueryBuilder().With<PowerCollector>().Build();
		var collector = collectorQ.First().Get<Transform>();
		var rangeSq = MathF.Pow(collectorQ.First().Get<PowerCollector>().Range, 2);

		const float SPEED = 10;
		foreach (var i in it)
		{
			Vector2 dist = collector.Pos - transform[i].Pos;
			if (dist.LengthSquared() >= rangeSq) continue;

			dist.Normalize();
			body[i].Vel = dist * SPEED;
		}
	}

	static void TickDespawnTimer(Iter it, int i, ref DespawnTimed despawn)
	{
		despawn.TimeSinceSpawn += it.DeltaTime();
		if (despawn.TimeSinceSpawn < despawn.TimeToDespawn) return;
		it.Entity(i).Destruct();
	}

	static void ProcessShooters(Iter it, int i, ref Shooter shooter, ref Transform transform)
	{
		const float BULLET_SPPED = 7;
		SpawnBullet(it.World(), transform.Pos, new Vector2(1, 0) * BULLET_SPPED);
		SpawnBullet(it.World(), transform.Pos, new Vector2(-1, 0) * BULLET_SPPED);
		SpawnBullet(it.World(), transform.Pos, new Vector2(0, -1) * BULLET_SPPED);
		SpawnBullet(it.World(), transform.Pos, new Vector2(0, 1) * BULLET_SPPED);
	}

	static void SpawnBullet(World world, Vector2 pos, Vector2 dir)
	{
		Entity bullet = world.Entity()
			.Add<Projectile>()
			.Add<Trigger>()
			.Set(new Transform(pos, Vector2.One, 0))
			.Set(new PhysicsBody(dir, Vector2.Zero))
			.Set(new DespawnTimed(5000f))
			.Set(new Collider(17));
		world.Entity()
			.Set(new Transform(new Vector2(0, 15), new Vector2(0.5f, 0.5f), 0))
			.Set(new Sprite("sprites/bee"))
			.ChildOf(bullet);
	}

	internal static void HandlePowerupHit(Entity e, ref CollisionEvent collision)
	{
		if (!collision.Other.Has<Player>()) return;
		Console.WriteLine("Powerup hit");
		e.Destruct();
	}
}