using Microsoft.Xna.Framework;
using Flecs.NET.Core;
using System;

namespace flecs_test;

public record struct Player;
public record struct Enemy;
public record struct EnemySpawner(bool Placeholder);

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
}