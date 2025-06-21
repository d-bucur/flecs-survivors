using System;
using System.Collections.Generic;
using Flecs.NET.Core;
using Microsoft.Xna.Framework;
using static System.Linq.Enumerable;
using MonoGame.Extended;

namespace flecs_test;

record struct Enemy;
record struct EnemySpawner(uint Level = 1);

class EnemiesModule : IFlecsModule {
	#region init
	public void InitModule(World world) {
		world.Entity("EnemySpawner").Set(new EnemySpawner(1));

		world.Set(new FlowField(50, 15));

		world.System<FlowField, GlobalTransform>()
			.TermAt(0).Singleton()
			.With<Scenery>()
			.Kind(Ecs.PreUpdate)
			.Run(FlowFieldECS.BlockScenery);

		// Could skip updating flow field every few frames
		// or introduce a delay and have a pipeline of fields
		world.System<FlowField, GlobalTransform>()
			.TermAt(0).Singleton()
			.With<Player>()
			.Kind(Ecs.PreUpdate)
			// .MultiThreaded()
			.Each(FlowFieldECS.GenerateFlowField);

		world.System<FlowField>()
			.Kind<RenderPhase>()
			.Each(FlowFieldECS.DebugFlowField);

		world.System<Transform, PhysicsBody, FlowField>()
			.With<Enemy>()
			.TermAt(2).Singleton()
			.Kind(Ecs.PreUpdate)
			// .Kind(Ecs.Disabled)
			.Iter(FollowPlayer);

		world.System<EnemySpawner>()
			.TickSource(world.Timer().Interval(5000f))
			.Kind(Ecs.PreUpdate)
			.Each(IncrementLevel);

		world.System<EnemySpawner>()
			.TickSource(world.Timer().Interval(300f))
			.Kind(Ecs.PreUpdate)
			.Kind(Ecs.Disabled)
			.Immediate()
			.Iter(SpawnEnemies);

		world.Observer<GlobalTransform>()
			.With<Enemy>()
			.Event(Ecs.OnRemove)
			.Each(HandleDeath);

	}
	#endregion init

	#region systems
	static void IncrementLevel(ref EnemySpawner spawner) {
		if (spawner.Level >= 5) return;
		spawner.Level += 1;
		Console.WriteLine($"Enemy levels: {spawner.Level}");
	}

	static void FollowPlayer(Iter it, Field<Transform> transform, Field<PhysicsBody> body, Field<FlowField> flow) {
		// Get Transform of Player and update all Enemy bodies to follow
		const float SPEED = 1;
		var query = it.World().QueryBuilder<Transform>().With<Player>().Build();
		ref readonly var player = ref query.First().Get<Transform>();
		var field = flow[0];

		// TODO when close enough should go directly towards player
		// TODO 
		foreach (var i in it) {
			var key = field.ToFieldPos(transform[i].Pos);
			if (key is not null) {
				var force = field.Flow[field.ToKey(key.Value)];
				body[i].Vel = force * SPEED;
			}
			else { // outside of field. Use direct force towards player
				var dir = player.Pos - transform[i].Pos;
				if (dir != Vector2.Zero) dir.Normalize();
				body[i].Vel = dir * SPEED;
			}
		}
	}

	static void SpawnEnemies(Iter it, Field<EnemySpawner> spawner) {
		var world = it.World();
		var playerQ = world.QueryBuilder().With<Player>().Build();
		var playerTransform = playerQ.First().Get<Transform>();
		const float RADIUS = 500;

		foreach (var i in it) {
			var angle = Random.Shared.NextSingle() * MathF.PI * 2;
			var pos = Vector2.Rotate(Vector2.UnitX, angle) * RADIUS + playerTransform.Pos;
			SpawnEnemy(ref world, pos, spawner[i].Level);
		}
	}

	static void SpawnEnemy(ref World world, Vector2 pos, uint level) {
		var enemy = world.Entity()
			.Add<Enemy>()
			.Set(new Transform(pos, Vector2.One, 0))
			.Set(new PhysicsBody(Vector2.Zero, Vector2.Zero))
			.Set(new Collider(17, Layers.ENEMY, Layers.ALL & ~Layers.POWERUP))
			.Set(new Health((int)level))
			.Observe<OnCollisionEnter>(HandleEnemyHit);
		var sprite = level switch {
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

	static void HandleDeath(Iter it, int i, ref GlobalTransform t) {
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

	static void HandleEnemyHit(Entity entity, ref OnCollisionEnter collision) {
		if (!collision.Other.Has<Projectile>()) return;
		// Console.WriteLine($"Hit by projectile: {entity} - {collision.Other}");
		Main.DecreaseHealth(entity);
	}
	#endregion
}