using Microsoft.Xna.Framework;
using Flecs.NET.Core;
using static System.Linq.Enumerable;
using System;

namespace flecs_test;

public record struct Player;
public record struct Enemy;
public record struct EnemySpawner(bool Placeholder);

class Main : IFlecsModule
{
	public void InitModule(World world)
	{
		Entity player = world.Entity()
			.Add<Player>()
			.Set(new Shooter())
			.Set(new Transform(new Vector2(10, 20), Vector2.One, 0))
			.Set(new PhysicsBody(new Vector2(1, 1), Vector2.Zero, 0.2f))
			.Set(new Collider(17));
		world.Entity()
			.Set(new Transform(new Vector2(0, 15), new Vector2(0.5f, 0.5f), 0))
			.Set(new Sprite("sprites/alienGreen_walk1"))
			.ChildOf(player);
		Console.WriteLine($"Player: {player}");

		foreach (int i in Range(1, 5))
		{
			SpawnEnemy(ref world, new Vector2(100 * i, 20));
		}
		world.Entity().Set(new EnemySpawner());

		var tickSource = world.Timer().Interval(500f);
		world.System<EnemySpawner>()
			.TickSource(tickSource)
			.Kind(Ecs.PreUpdate)
			.Immediate()
			.Iter(SpawnEnemies);
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

	public static void HandleEnemyHit(Entity entity, ref CollisionEvent collision)
	{
		if (!collision.Other.Has<Projectile>()) return;
		// Console.WriteLine($"Hit by projectile: {entity} - {collision.Other}");
		collision.Other.Destruct();
		entity.Destruct();
	}
}