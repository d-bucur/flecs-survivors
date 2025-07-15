using System;
using System.Numerics;
using Flecs.NET.Core;

namespace flecs_survivors;

record struct Enemy;
record struct EnemySpawner(uint Level = 1, uint MaxLevel = 0, float Radius = 1000);

class EnemiesModule : IFlecsModule {
	#region init
	public void InitModule(World world) {
		var renderCtx = world.Get<RenderCtx>();
		world.Entity("EnemySpawner").Set(new EnemySpawner(1, 10, (renderCtx.WinSize.ToVector2() / 2).Length() * 1.05f));

		world.Set(new FlowField(64, 15));

		world.System<FlowField, GlobalTransform>()
			.TermAt(0).Singleton()
			.With<Scenery>()
			.Kind(Ecs.PreUpdate)
			.Run(FlowFieldECS.AddSceneryCost);

		world.System<FlowField, GlobalTransform>()
			.TermAt(0).Singleton()
			.With<Enemy>()
			.Kind(Ecs.PreUpdate)
			.Each(FlowFieldECS.AddEnemyCost);

		// Could skip updating flow field every few frames
		// or introduce a delay and have a pipeline of fields
		world.System<FlowField, GlobalTransform>()
			.TermAt(0).Singleton()
			.With<Player>()
			.Kind(Ecs.PreUpdate)
			// .MultiThreaded()
			.Each(FlowFieldECS.GenerateFlowField);

		var debugId = world.System<FlowField>()
			.Kind<RenderPhase>()
			.Each(FlowFieldECS.DebugFlowField);
		world.System<DebugConfig>()
			.Kind(Ecs.OnStart)
			.Each((ref DebugConfig conf) => { conf.SystemIdFlow = debugId; debugId.Disable(); });

		world.System<GlobalTransform, PhysicsBody, SpatialQuery, SpatialMap>()
			.TermAt(2).Singleton()
			.TermAt(3).Singleton()
			.With<Enemy>()
			.Kind<OnPhysics>()
			.Each(AddFlockingForces);

		world.System<Transform, PhysicsBody, FlowField>()
			.With<Enemy>()
			.TermAt(2).Singleton()
			.Kind(Ecs.PreUpdate)
			.Iter(UpdateEnemyAccel);

		world.System<EnemySpawner>()
			.TickSource(world.Timer().Rate(150, Timers.intervalTimer))
			.Kind(Ecs.PreUpdate)
			.Each(IncrementLevel);

		world.System<EnemySpawner>()
			.TickSource(world.Timer().Rate(6, Timers.intervalTimer))
			.Kind(Ecs.PreUpdate)
			// .Kind(Ecs.Disabled)
			.Immediate()
			.Iter(SpawnEnemies);
	}
	#endregion init

	#region systems
	static void IncrementLevel(ref EnemySpawner spawner) {
		if (spawner.Level >= spawner.MaxLevel && spawner.MaxLevel != 0) return;
		spawner.Level += 1;
		Console.WriteLine($"Enemy levels: {spawner.Level}");
	}

	static void SpawnEnemies(Iter it, Field<EnemySpawner> spawner) {
		var world = it.World();
		var playerQ = world.QueryBuilder().With<Player>().Build();
		var playerTransform = playerQ.First().Get<Transform>();

		foreach (var i in it) {
			var angle = Random.Shared.NextSingle() * MathF.PI * 2;
			var pos = Vector2.UnitX.Rotate(angle) * spawner[i].Radius + playerTransform.Pos;
			SpawnEnemy(ref world, pos, spawner[i].Level);
		}
	}

	static void SpawnEnemy(ref World world, Vector2 pos, uint level) {
		var enemy = world.Entity()
			.Add<Enemy>()
			.Add<InGameEntity>()
			.Set(new Transform(pos, Vector2.One, 0))
			.Set(new PhysicsBody(Vector2.Zero, Vector2.Zero))
			.Set(new Collider(new SphereCollider(17), CollisionFlags.ENEMY, CollisionFlags.ALL & ~CollisionFlags.POWERUP))
			.Set(new Health((int)level))
			.Set(new Level(level))
			.Observe<OnCollisionEnter>(HandleEnemyCollision)
			.Observe<DeathEvent>(HandleEnemyDeath)
			.Observe<DeathEvent>(PowerupOnDeath);
		var sprite = world.Entity("Sprite")
			.Set(new Sprite(Textures.MEGA_SHEET))
			.ChildOf(enemy);
		PrepEnemyLevel(level, ref enemy, ref sprite);
	}

	#region enemy types
	private static void PrepEnemyLevel(uint level, ref Entity enemy, ref Entity sprite) {
		if (level == 1) {
			sprite
				.Set(new Transform(new Vector2(0, 15), new Vector2(3f, 3f), 0))
				.Set(new Animator("thief", "run", 75));
		}
		if (level == 2) {
			sprite
				.Set(new Transform(new Vector2(0, 15), new Vector2(3f, 3f), 0))
				.Set(new Animator("anomaly", "run", 75));
		}
		if (level == 3) {
			sprite
				.Set(new Transform(new Vector2(0, 20), new Vector2(3f, 3f), 0))
				.Set(new Animator("knight0", "run", 75));
		}
		if (level == 4) {
			sprite
				.Set(new Transform(new Vector2(0, 70), new Vector2(3f, 3f), 0))
				.Set(new Animator("knight1", "run", 75));
		}
		if (level == 5) {
			sprite
				.Set(new Transform(new Vector2(0, 70), new Vector2(3f, 3f), 0))
				.Set(new Animator("knight2", "run", 75));
		}
		if (level == 6) {
			sprite
				.Set(new Transform(new Vector2(0, 70), new Vector2(3f, 3f), 0))
				.Set(new Animator("knight3", "run", 75));
		}
		if (level == 7) {
			sprite
				.Set(new Transform(new Vector2(0, 70), new Vector2(3f, 3f), 0))
				.Set(new Animator("knight4", "run", 75));
		}
		if (level == 8) {
			sprite
				.Set(new Transform(new Vector2(0, 45), new Vector2(2f, 2f), 0))
				.Set(new Animator("sword", "walk", 75));
		}
		if (level == 9) {
			sprite
				.Set(new Transform(new Vector2(0, 40), new Vector2(1f, 1f), 0))
				.Set(new Animator("wetland_boss", "walk", 75));
		}
		if (level == 10) {
			sprite
				.Set(new Transform(new Vector2(0, 60), new Vector2(2f, 2f), 0))
				.Set(new Animator("dead", "idle", 75));
		}
		// Not happy with how there are working for now
		// if (level == 8) {
		// 	sprite
		// 		.Set(new Transform(new Vector2(0, 45), new Vector2(2f, 2f), 0))
		// 		.Set(new Animator("frost_curse", "run", 75));
		// }
		// if (level == 11) {
		// 	sprite
		// 		.Set(new Transform(new Vector2(0, 120), new Vector2(2f, 2f), 0))
		// 		.Set(new Animator("dusk_druid", "walk", 75));
		// }
		// if (level == 12) {
		// 	sprite
		// 		.Set(new Transform(new Vector2(0, 120), new Vector2(2f, 2f), 0))
		// 		.Set(new Animator("forest_mage", "walk", 75));
		// }
		// if (level == 13) {
		// 	sprite
		// 		.Set(new Transform(new Vector2(0, 110), new Vector2(2f, 2f), 0))
		// 		.Set(new Animator("evil_wizard", "walk", 75));
		// }
		// if (level == 15) {
		// 	sprite
		// 		.Set(new Transform(new Vector2(0, 40), new Vector2(2f, 2f), 0))
		// 		.Set(new Animator("necro", "walk", 75));
		// }
	}
	#endregion

	static void PowerupOnDeath(Entity e, ref DeathEvent t) {
		// Spawn power pickup
		e.Read((ref readonly Level level, ref readonly GlobalTransform transf) => {
			Entity power = e.CsWorld().Entity()
				.Add<Trigger>()
				.Add<InGameEntity>()
				.Set(new Powerup(level.Value))
				.Set(new Transform(transf.Pos, Vector2.One, 0))
				.Set(new PhysicsBody(Vector2.Zero, Vector2.Zero))
				.Set(new DespawnTimed(30000f))
				.Set(new Collider(new SphereCollider(15), CollisionFlags.POWERUP, CollisionFlags.PLAYER));
			e.CsWorld().Entity()
				.Set(new Transform(new Vector2(0, 15), new Vector2(0.5f, 0.5f), 0))
				.Set(new Sprite("Content/sprites/slime.png"))
				.ChildOf(power);
		});
	}

	static void HandleEnemyCollision(Entity enemy, ref OnCollisionEnter collision) {
		if (collision.Other.Has<Player>()) {
			HandleCollisionWithPlayer(enemy, ref collision);
			return;
		}
		if (!collision.Other.Has<Bullet>()) return;
		var bullet = collision.Other.Get<Bullet>();

		ref var body = ref enemy.GetMut<PhysicsBody>();
		body.Vel = collision.Data.Penetration.Normalized() * bullet.Pushback;

		if (Main.DecreaseHealth(enemy, collision.Data.Penetration))
			Main.FlashDamage(enemy);
	}

	private static void HandleCollisionWithPlayer(Entity enemy, ref OnCollisionEnter collision) {
		var child = enemy.Lookup("Sprite");
		ref var animator = ref child.GetMut<Animator>();
		var sprite = child.Get<Sprite>();
		var hasAttackAnimation = sprite.Packing?[animator.ObjectName].ContainsKey("attack");
		if (hasAttackAnimation.GetValueOrDefault(false))
			animator.PlayAnimation("attack");
	}

	static void HandleEnemyDeath(Entity e, ref DeathEvent death) {
		// Remove child with sprite from hierarchy and animate it out
		var sprite = e.Lookup("Sprite");
		sprite.SetName(null!);
		Vector2 currentPos = new();
		Vector2 prevScale = new();
		sprite.Write((ref Transform t, ref GlobalTransform g) => {
			t.Pos = g.Pos;
			currentPos = g.Pos;
			prevScale = g.Scale;
		});
		sprite.Remove(Ecs.ChildOf, e);
		e.Destruct();

		// Tween animation
		const int ANIM_TIME = 500;
		const int PUSHBACK = 50;
		new Tween(sprite).With(
			(ref Transform p, float v) => p.Rot = v,
			0, 360, 1000, Ease.Linear,
			(ref Transform p) => sprite.Destruct()
		).With(
			(ref Transform p, float s) => p.Scale = new Vector2(s),
			prevScale.X, 0, ANIM_TIME, Ease.Linear
		).With(
			(ref Transform t, Vector2 p) => t.Pos = p,
			currentPos, currentPos + death.direction.Normalized() * PUSHBACK, ANIM_TIME, Ease.QuartOut,
			Vector2.Lerp
		).RegisterEcs();
	}

	static void AddFlockingForces(Entity e, ref GlobalTransform enemy, ref PhysicsBody body, ref SpatialQuery query, ref SpatialMap map) {
		const float SEPARATION_COEFF = 50f;
		const float ALIGNMENT_COEFF = 0.1f;
		const float MAX_ACCEL = 0.07f / 1000;

		query.Prep(enemy.Pos, 10);
		query.Execute(map);
		Vector2 separation = Vector2.Zero;
		Vector2 alignment = Vector2.Zero;

		foreach (var neighId in query.Results) {
			if (neighId == e.Id) continue;
			var neigh = e.CsWorld().GetAlive(neighId);
			if (!neigh.Has<Enemy>()) continue;

			var neighPos = neigh.Get<Transform>().Pos;
			var dir = enemy.Pos - neighPos;
			separation += dir / dir.LengthSquared();
			var neighVel = neigh.Get<PhysicsBody>().Vel;
			alignment += neighVel;
		}
		var flockingTotal = separation * SEPARATION_COEFF + alignment * ALIGNMENT_COEFF;
		flockingTotal = flockingTotal.Truncated(MAX_ACCEL);

		body.Accel += flockingTotal;
	}

	static void UpdateEnemyAccel(Iter it, Field<Transform> transform, Field<PhysicsBody> body, Field<FlowField> flow) {
		// Get Transform of Player and update all Enemy bodies to follow
		const float ACCEL = 0.4f / 1000;
		const float SMOOTH_ACCEL_COEFF = 0.8f;

		var query = it.World().QueryBuilder<Transform>().With<Player>().Build();
		ref readonly var player = ref query.First().Get<Transform>();
		var field = flow[0];

		// TODO when close enough should go directly towards player
		foreach (var i in it) {
			var key = field.ToFieldPos(transform[i].Pos);
			Vector2 force;
			if (key is not null) {
				force = field.Flow[field.ToKey(key.Value)];
			}
			else { // outside of field. Use direct force towards player
				force = player.Pos - transform[i].Pos;
				if (force != Vector2.Zero) force = Vector2.Normalize(force);
			}
			var smoothAccel = body[i].Accel * SMOOTH_ACCEL_COEFF + force * ACCEL * (1 - SMOOTH_ACCEL_COEFF);
			body[i].Accel = smoothAccel;
		}
	}
	#endregion
}