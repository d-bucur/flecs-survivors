using System;
using System.Numerics;
using Flecs.NET.Core;
using Raylib_cs;

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

		world.System<FlowField>()
			.Kind<RenderPhase>()
			.Kind(Ecs.Disabled)
			.Each(FlowFieldECS.DebugFlowField);

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
			// .Kind(Ecs.Disabled)
			.Iter(UpdateEnemyAccel);

		world.System<EnemySpawner>()
			.TickSource(world.Timer().Interval(5000f))
			.Kind(Ecs.PreUpdate)
			.Each(IncrementLevel);

		world.System<EnemySpawner>()
			.TickSource(world.Timer().Interval(300f))
			.Kind(Ecs.PreUpdate)
			// .Kind(Ecs.Disabled)
			.Immediate()
			.Iter(SpawnEnemies);

		world.Observer<GlobalTransform>()
			.With<Enemy>()
			.Event(Ecs.OnRemove)
			.Each(PowerupOnDeath);

	}
	#endregion init

	#region systems
	static void IncrementLevel(ref EnemySpawner spawner) {
		if (spawner.Level >= 5) return;
		spawner.Level += 1;
		Console.WriteLine($"Enemy levels: {spawner.Level}");
	}

	static void UpdateEnemyAccel(Iter it, Field<Transform> transform, Field<PhysicsBody> body, Field<FlowField> flow) {
		// Get Transform of Player and update all Enemy bodies to follow
		const float ACCEL = 0.1f;
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

	static void SpawnEnemies(Iter it, Field<EnemySpawner> spawner) {
		var world = it.World();
		var playerQ = world.QueryBuilder().With<Player>().Build();
		var playerTransform = playerQ.First().Get<Transform>();
		const float RADIUS = 500;

		foreach (var i in it) {
			var angle = Random.Shared.NextSingle() * MathF.PI * 2;
			var pos = Vector2.UnitX.Rotate(angle) * RADIUS + playerTransform.Pos;
			SpawnEnemy(ref world, pos, spawner[i].Level);
		}
	}

	static void SpawnEnemy(ref World world, Vector2 pos, uint level) {
		var enemy = world.Entity()
			.Add<Enemy>()
			.Set(new Transform(pos, Vector2.One, 0))
			.Set(new PhysicsBody(Vector2.Zero, Vector2.Zero))
			.Set(new Collider(17, CollisionFlags.ENEMY, CollisionFlags.ALL & ~CollisionFlags.POWERUP))
			.Set(new Health((int)level))
			.Observe<OnCollisionEnter>(HandleEnemyHit)
			.Observe<DeathEvent>(HandleDeath);
		var sprite = level switch {
			1 => "Content/sprites/alienBeige_walk1.png",
			2 => "Content/sprites/alienYellow_walk1.png",
			3 => "Content/sprites/alienBlue_walk1.png",
			_ => "Content/sprites/alienPink_walk1.png",
		};
		world.Entity("Sprite")
			.Set(new Transform(new Vector2(0, 15), new Vector2(0.5f, 0.5f), 0))
			.Set(new Sprite(sprite))
			.ChildOf(enemy);
	}

	static void PowerupOnDeath(Iter it, int i, ref GlobalTransform t) {
		// Spawn power pickup
		Entity power = it.World().Entity()
			.Add<Trigger>()
			.Set(new Powerup(1))
			.Set(new Transform(t.Pos, Vector2.One, 0))
			.Set(new PhysicsBody(Vector2.Zero, Vector2.Zero))
			.Set(new DespawnTimed(30000f))
			.Set(new Collider(15, CollisionFlags.POWERUP, CollisionFlags.PLAYER));
		it.World().Entity()
			.Set(new Transform(new Vector2(0, 15), new Vector2(0.5f, 0.5f), 0))
			.Set(new Sprite("Content/sprites/slime.png"))
			.ChildOf(power);
	}

	static void HandleEnemyHit(Entity enemy, ref OnCollisionEnter collision) {
		const float PUSHBACK = 10f;
		if (!collision.Other.Has<Projectile>()) return;

		ref var body = ref enemy.GetMut<PhysicsBody>();
		body.Vel = collision.Penetration.Normalized() * PUSHBACK;

		// Flash effect
		enemy.Children((e) => {
			if (e.Has<Sprite>()) {
				new Tween(e).With(
					(ref Sprite s, Color t) => s.Tint = t,
					Color.White,
					Color.Black,
					300,
					Ease.QuartOut,
					Raylib.ColorLerp,
					AutoReverse: true
				).RegisterEcs();
			}
		});

		// Console.WriteLine($"Hit by projectile: {entity} - {collision.Other}");
		Main.DecreaseHealth(enemy, collision.Penetration);
	}

	static void HandleDeath(Entity e, ref DeathEvent death) {
		// Remove child with sprite from hierarchy and animate it out
		var sprite = e.Lookup("Sprite");
		sprite.SetName(null!);
		Vector2 currentPos = new();
		sprite.Write((ref Transform t, ref GlobalTransform g) => {
			t.Pos = g.Pos;
			currentPos = g.Pos;
		});
		sprite.Remove(Ecs.ChildOf, e);
		e.Destruct();

		const int ANIM_TIME = 500;
		const int PUSHBACK = 50;
		new Tween(sprite).With(
			(ref Transform p, float v) => p.Rot = v,
				0, 360, 1000, Ease.Linear,
				(ref Transform p) => sprite.Destruct()
			).With(
			(ref Transform p, float s) => p.Scale = new Vector2(s),
			0.5f, 0, ANIM_TIME, Ease.Linear
		).With(
			(ref Transform t, Vector2 p) => t.Pos = p,
			currentPos, currentPos + death.direction.Normalized() * PUSHBACK, ANIM_TIME, Ease.QuartOut,
			Vector2.Lerp
		).RegisterEcs();
	}

	static void AddFlockingForces(Entity e, ref GlobalTransform enemy, ref PhysicsBody body, ref SpatialQuery query, ref SpatialMap map) {
		const float SEPARATION_COEFF = 50f;
		const float ALIGNMENT_COEFF = 0.1f;
		const float MAX_ACCEL = 0.01f;

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
	#endregion
}