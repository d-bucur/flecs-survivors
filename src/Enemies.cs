using System;
using System.Collections.Generic;
using Flecs.NET.Core;
using Microsoft.Xna.Framework;
using static System.Linq.Enumerable;
using MonoGame.Extended;


namespace flecs_test;

record struct Enemy;
record struct EnemySpawner(uint Level = 1);
record struct FlowField(float CellSize, int FieldWidth = 10) {
	internal Vector2 Origin; // Center of player
	internal Vector2[] Field = new Vector2[(FieldWidth * 2 + 1) * (FieldWidth * 2 + 1)];
	internal HashSet<Vec2I> Obstacles = new(20);
	public Vector2 CellCenterOffset {
		get {
			// could cache
			return Vector2.One * CellSize / 2;
		}
	}

	public Vec2I? HashAt(Vector2 pos) {
		var fieldPos = (pos - Origin + CellCenterOffset) / CellSize;
		var hash = new Vec2I((int)float.Floor(fieldPos.X), (int)float.Floor(fieldPos.Y));
		if (Math.Abs(hash.X) > FieldWidth || MathF.Abs(hash.Y) > FieldWidth)
			return null;
		return hash;
	}

	// change to uint
	public int ToKey(Vec2I pos) {
		return (pos.Y + FieldWidth) * (FieldWidth * 2 + 1) + pos.X + FieldWidth;
	}
}

class EnemiesModule : IFlecsModule {
	public void InitModule(World world) {
		world.Entity("EnemySpawner").Set(new EnemySpawner(1));

		world.Set(new FlowField(50, 7));

		world.System<FlowField>()
			.Kind(Ecs.PreUpdate)
			.Each((ref FlowField field) => field.Obstacles.Clear());

		world.System<FlowField, GlobalTransform>()
			.TermAt(0).Singleton()
			.With<Scenery>()
			.Kind(Ecs.PreUpdate)
			// .MultiThreaded()
			.Each(BlockScenery);

		// TODO can skip updating flow field every few frames
		world.System<FlowField, GlobalTransform>()
			.TermAt(0).Singleton()
			.With<Player>()
			.Kind(Ecs.PreUpdate)
			// .MultiThreaded()
			.Each(GenerateFlowField);

		world.System<FlowField>()
			.Kind<RenderPhase>()
			.Each(DebugFlowField);

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
			// .Kind(Ecs.Disabled)
			.Immediate()
			.Iter(SpawnEnemies);

		world.Observer<GlobalTransform>()
			.With<Enemy>()
			.Event(Ecs.OnRemove)
			.Each(HandleDeath);

	}

	private void BlockScenery(ref FlowField field, ref GlobalTransform transform) {
		// TODO obstacles should also block neighboring cells if big enough
		var pos = field.HashAt(transform.Pos);
		if (pos is null) return;
		field.Obstacles.Add(pos.Value);
		// Console.WriteLine($"Blocking at: {pos}");
	}

	private record struct VisitEntry(Vec2I Pos, Vec2I Origin, Vector2 OriginDir);
	// readonly Vec2I[] neighbors = [
	// 	(-1, -1), (0, -1), (1, -1),
	// 	(-1, 0), (1, 0),
	// 	(-1, 1), (0, 1), (1, 1),
	// ];
	// Works without diagonals as well since it sums the previous force
	readonly Vec2I[] neighbors = [
		(0, -1),
		(-1, 0), (1, 0),
		(0, 1),
	];

	private void GenerateFlowField(ref FlowField field, ref GlobalTransform player) {
		// TODO add line of sight
		// TODO dont recalc on not moving
		field.Origin = player.Pos;
		var visited = new HashSet<Vec2I>();
		var toVisit = new Queue<VisitEntry>([new VisitEntry((0, 0), (0, 0), Vector2.Zero)]);
		while (toVisit.Count > 0) {
			var (current, origin, originDir) = toVisit.Dequeue();
			visited.Add(current);
			int key = field.ToKey(current);
			Vector2 simpleDir = (origin - current).ToVector2();
			Vector2 dir = simpleDir + originDir;
			field.Field[key] = dir == Vector2.Zero ? dir : Vector2.Normalize(dir);
			foreach (var n in neighbors) {
				var pos = current + n;
				if (Math.Abs(pos.X) > field.FieldWidth || Math.Abs(pos.Y) > field.FieldWidth)
					continue;
				if (visited.Contains(pos) || field.Obstacles.Contains(pos))
					continue;
				toVisit.Enqueue(new VisitEntry(pos, current, dir));
			}
		}
	}

	private void DebugFlowField(Entity e, ref FlowField field) {
		var camera = e.CsWorld().Query<Camera>().First().Get<Camera>();
		var batch = e.CsWorld().Get<RenderCtx>().SpriteBatch;
		batch.Begin(transformMatrix: camera.GetTransformMatrix());

		for (var i = -field.FieldWidth; i <= field.FieldWidth; i++)
			for (var j = -field.FieldWidth; j <= field.FieldWidth; j++) {
				var cellCenter = new Vector2(i, j) * field.CellSize + field.Origin;
				var cellCorner = cellCenter - field.CellCenterOffset;

				// Draw the grid line
				Color gridColor = HSL.Hsl(120, 0.5f, 0.5f, 1f);
				batch.DrawLine(cellCorner, cellCorner + Vector2.UnitX * field.CellSize, gridColor);
				batch.DrawLine(cellCorner, cellCorner + Vector2.UnitY * field.CellSize, gridColor);

				var vecKey = new Vec2I(i, j);
				if (field.Obstacles.Contains(vecKey)) {
					// Draw obstacle
					batch.DrawLine(cellCorner, cellCorner + new Vector2(field.CellSize), HSL.Hsl(40, 0.5f, 0.75f, 1f), 2);
					continue;
				}
				// Draw the force
				var dir = field.Field[field.ToKey(vecKey)];
				batch.DrawLine(cellCenter, cellCenter + dir * 20, HSL.Hsl(0, 0.5f, 0.5f, 1f));
			}
		batch.End();
	}

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
			var key = field.HashAt(transform[i].Pos);
			if (key is not null) {
				var force = field.Field[field.ToKey(key.Value)];
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
}