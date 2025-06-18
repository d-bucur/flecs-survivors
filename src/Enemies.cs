using System;
using Flecs.NET.Core;
using Microsoft.Xna.Framework;
using static System.Linq.Enumerable;

namespace flecs_test;

record struct Enemy;
record struct EnemySpawner(uint Level = 1);

class EnemiesModule : IFlecsModule
{
	public void InitModule(World world)
	{
		foreach (int i in Range(1, 5))
		{
			SpawnEnemy(ref world, new Vector2(100 * i, 20), 1);
		}
		world.Entity("EnemySpawner").Set(new EnemySpawner(1));

		world.System<Transform, PhysicsBody>()
			.With<Enemy>()
			.Kind(Ecs.PreUpdate)
			.Iter(FollowPlayer);

		world.System<EnemySpawner>()
			.TickSource(world.Timer().Interval(5000f))
			.Kind(Ecs.PreUpdate)
			.Each(IncrementLevel);

		world.System<EnemySpawner>()
			.TickSource(world.Timer().Interval(500f))
			.Kind(Ecs.PreUpdate)
			.Immediate()
			.Iter(SpawnEnemies);

		world.Observer<GlobalTransform>()
			.With<Enemy>()
			.Event(Ecs.OnRemove)
			.Each(HandleDeath);

	}
	static void IncrementLevel(ref EnemySpawner spawner)
	{
		if (spawner.Level >= 5) return;
		spawner.Level += 1;
		Console.WriteLine($"Enemy levels: {spawner.Level}");
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

	static void SpawnEnemies(Iter it, Field<EnemySpawner> spawner)
	{
		var world = it.World();
		var playerQ = world.QueryBuilder().With<Player>().Build();
		var playerTransform = playerQ.First().Get<Transform>();
		const float RADIUS = 500;

		foreach (var i in it)
		{
			var angle = Random.Shared.NextSingle() * MathF.PI * 2;
			var pos = Vector2.Rotate(Vector2.UnitX, angle) * RADIUS + playerTransform.Pos;
			SpawnEnemy(ref world, pos, spawner[i].Level);
		}
	}

	static void SpawnEnemy(ref World world, Vector2 pos, uint level)
	{
		var enemy = world.Entity()
			.Add<Enemy>()
			.Set(new Transform(pos, Vector2.One, 0))
			.Set(new PhysicsBody(new Vector2(1, 1), Vector2.Zero))
			.Set(new Collider(17, Layers.ENEMY, Layers.ALL & ~Layers.POWERUP))
			.Set(new Health((int)level))
			.Observe<OnCollisionEnter>(HandleEnemyHit);
		var sprite = level switch
		{
			1 => "sprites/alienBeige_walk1",
			2 => "sprites/alienYellow_walk1",
			3 => "sprites/alienBlue_walk1",
			_ => "sprites/alienPink_walk1",
		};
		world.Entity()
			.Set(new Transform(new Vector2(0, 15), new Vector2(0.5f, 0.5f), 0))
			.Set(new Sprite(sprite))
			.ChildOf(enemy);
	}

	static void HandleDeath(Iter it, int i, ref GlobalTransform t)
	{
		// Spawn power pickup
		Entity power = it.World().Entity()
			.Add<Trigger>()
			.Set(new Powerup(1))
			.Set(new Transform(t.Pos, Vector2.One, 0))
			.Set(new PhysicsBody(Vector2.Zero, Vector2.Zero))
			.Set(new DespawnTimed(30000f))
			.Set(new Collider(15, Layers.POWERUP, Layers.PLAYER));
		it.World().Entity()
			.Set(new Transform(new Vector2(0, 15), new Vector2(0.5f, 0.5f), 0))
			.Set(new Sprite("sprites/slime"))
			.ChildOf(power);
	}

	static void HandleEnemyHit(Entity entity, ref OnCollisionEnter collision)
	{
		if (!collision.Other.Has<Projectile>()) return;
		// Console.WriteLine($"Hit by projectile: {entity} - {collision.Other}");
		Main.DecreaseHealth(entity);
	}
}