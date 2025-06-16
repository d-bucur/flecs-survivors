using System;
using Flecs.NET.Core;
using Microsoft.Xna.Framework;
using static System.Linq.Enumerable;

namespace flecs_test;

record struct Enemy;
record struct EnemySpawner(bool Placeholder);

class EnemiesModule : IFlecsModule
{
	public void InitModule(World world)
	{
		foreach (int i in Range(1, 5))
		{
			SpawnEnemy(ref world, new Vector2(100 * i, 20));
		}
		world.Entity().Set(new EnemySpawner());

		world.System<Transform, PhysicsBody>()
			.With<Enemy>()
			.Kind(Ecs.PreUpdate)
			.Iter(FollowPlayer);

		var tickSource = world.Timer().Interval(500f);
		world.System<EnemySpawner>()
			.TickSource(tickSource)
			.Kind(Ecs.PreUpdate)
			.Immediate()
			.Iter(SpawnEnemies);
	}

	static void FollowPlayer(Iter it, Field<Transform> transform, Field<PhysicsBody> body)
	{
		// Get Transform of Player and update all Enemy bodies to follow
		const float SPEED = 1;
		var query = it.World().QueryBuilder<Transform>().With<Player>().Build();
		ref readonly var player = ref query.First().Get<Transform>();

		foreach (var i in it)
		{
			var dir = player.Pos - transform[i].Pos;
			if (dir != Vector2.Zero) dir.Normalize();
			body[i].Vel = dir * SPEED;
		}
	}

	static void SpawnEnemies(Iter it, Field<EnemySpawner> t0)
	{
		var world = it.World();
		var playerQ = world.QueryBuilder().With<Player>().Build();
		var playerTransform = playerQ.First().Get<Transform>();
		const float RADIUS = 500;

		foreach (var i in it)
		{
			var angle = Random.Shared.NextSingle() * MathF.PI * 2;
			var pos = Vector2.Rotate(Vector2.UnitX, angle) * RADIUS + playerTransform.Pos;
			SpawnEnemy(ref world, pos);
		}
	}

	static void SpawnEnemy(ref World world, Vector2 pos)
	{
		var enemy = world.Entity()
			.Add<Enemy>()
			.Set(new Transform(pos, Vector2.One, 0))
			.Set(new PhysicsBody(new Vector2(1, 1), Vector2.Zero))
			.Set(new Collider(17))
			.Observe<CollisionEvent>(HandleEnemyHit);
		world.Entity()
			.Set(new Transform(new Vector2(0, 15), new Vector2(0.5f, 0.5f), 0))
			.Set(new Sprite("sprites/alienPink_walk1"))
			.ChildOf(enemy);
	}

	static void HandleEnemyHit(Entity entity, ref CollisionEvent collision)
	{
		if (!collision.Other.Has<Projectile>()) return;
		// Console.WriteLine($"Hit by projectile: {entity} - {collision.Other}");
		collision.Other.Destruct();
		entity.Destruct();
	}
}