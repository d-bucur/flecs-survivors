global using CollisionMask = uint;

using Microsoft.Xna.Framework;
using Flecs.NET.Core;
using MonoGame.Extended;
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;

namespace flecs_test;

enum Trigger;
record struct PhysicsBody(Vector2 Vel, Vector2 Accel, float BounceCoeff = 1);
record struct Collider(float Radius, CollisionMask MyLayers = Layers.DEFAULT, CollisionMask MaskLayers = Layers.ALL)
{
	// TODO Race condition when threaded
	public HashSet<Id> collisionsLastFrame = [];
	public HashSet<Id> collisionsCurrentFrame = [];
}
record struct SpatialMap(float CellSize)
{
	// Could try to profile HybridDictionary
	public ConcurrentDictionary<(int, int), List<ulong>> Map = new(-1, 10);
}

record struct OnCollisionEnter(Entity Other);
record struct OnCollisionExit(Entity Other);
record struct OnCollisionStay(Entity Other);

record struct Heading(Vector2 Value);

class Layers
{
	public const CollisionMask ALL = ~0u;
	public const CollisionMask DEFAULT = 1 << 0;
	public const CollisionMask PLAYER = 1 << 1;
	public const CollisionMask ENEMY = 1 << 2;
	public const CollisionMask PROJECTILE = 1 << 3;
	public const CollisionMask POWERUP = 1 << 4;
	public const CollisionMask SCENERY = 1 << 5;
}

class PhysicsModule : IFlecsModule
{
	public void InitModule(World world)
	{
		world.Set(new SpatialMap(30));

		// TODO update to GlobalTransform
		world.System<Transform, PhysicsBody>()
			.Kind(Ecs.OnUpdate)
			.MultiThreaded()
			.Each(IntegratePosition);

		world.System<SpatialMap>()
			.Kind(Ecs.OnUpdate)
			.Each((ref SpatialMap s) => s.Map.Clear());

		world.System<Transform, SpatialMap>()
			.TermAt(1).Singleton()
			.With<PhysicsBody>()
			.With<Collider>()
			.Write<SpatialMap>()
			.Kind(Ecs.OnUpdate)
			.MultiThreaded()
			.Each(BuildSpatialHash);

		world.System<Transform, PhysicsBody, Collider>()
			.Kind(Ecs.OnUpdate)
			.Run(HandleCollisions);

		world.System<Collider>()
			.Kind(Ecs.OnUpdate)
			.Each(EmitCollisionEvents);

		world.System<Heading, PhysicsBody>()
			.Kind(Ecs.OnUpdate)
			.MultiThreaded()
			.Each(UpdateHeading);

		world.System<GlobalTransform, PhysicsBody, Collider>()
			.Kind<RenderPhase>()
			.Kind(Ecs.Disabled)
			.Iter(DebugColliders);
	}

	private void EmitCollisionEvents(Entity e, ref Collider collider)
	{
		foreach (var current in collider.collisionsCurrentFrame)
		{
			if (collider.collisionsLastFrame.Contains(current))
			{
				e.Emit(new OnCollisionStay(e.CsWorld().GetAlive(current)));
				continue;
			}
			// TODO need check for null id here?
			e.Emit(new OnCollisionEnter(e.CsWorld().GetAlive(current)));
		}
		foreach (var last in collider.collisionsLastFrame)
		{
			// OnCollisionStay already emitted above
			if (collider.collisionsCurrentFrame.Contains(last))
				continue;
			e.Emit(new OnCollisionExit(e.CsWorld().GetAlive(last)));
		}
		// Swap buffers
		var t = collider.collisionsLastFrame;
		t.Clear();
		collider.collisionsLastFrame = collider.collisionsCurrentFrame;
		collider.collisionsCurrentFrame = t;
	}

	private void UpdateHeading(ref Heading h, ref PhysicsBody b)
	{
		h.Value = b.Vel != Vector2.Zero ? b.Vel : h.Value;
		if (h.Value == Vector2.Zero) h.Value = Vector2.UnitX;
	}

	private void BuildSpatialHash(Entity e, ref Transform transform, ref SpatialMap map)
	{
		var x = (int)(transform.Pos.X / map.CellSize);
		var y = (int)(transform.Pos.Y / map.CellSize);
		var key = (x, y);
		map.Map.TryGetValue(key, out List<ulong>? values);
		values ??= new(2);
		values.Add(e.Id.Value);
		map.Map[key] = values;
	}

	readonly (int, int)[] _neighbors = [
		(-1, -1),(0, -1),(1, -1),
		(-1, 0),(0, 0),(1, 0),
		(-1, 1),(0, 1),(1, 1),
	];

	// TODO update to GlobalTransforms. Need to propagate back to Transform
	private void HandleCollisions(Iter it)
	{
		World world = it.World();
		var map = world.Get<SpatialMap>();

		foreach (var ((a, b), startCellEntities) in map.Map)
			foreach (var e1Id in startCellEntities)
			{
				var e1 = world.GetAlive(e1Id);
				// TODO use fields instead? Need to translate ids to array indx
				ref var t1 = ref e1.GetMut<Transform>();
				ref var b1 = ref e1.GetMut<PhysicsBody>();
				ref var c1 = ref e1.GetMut<Collider>();

				foreach (var (x, y) in _neighbors)
				{
					// Console.WriteLine($"Checking collisions {startCellKey}: ({x}, {y})");
					map.Map.TryGetValue((x + a, y + b), out var nearCellEntities);
					foreach (var e2Id in nearCellEntities is null ? [] : nearCellEntities)
					{
						// Skip double checks
						if (e1Id >= e2Id)
							continue;
						var e2 = world.GetAlive(e2Id);

						// check layer masks if can collide
						ref var c2 = ref e2.GetMut<Collider>();
						bool canE1Collide = (c1.MaskLayers & c2.MyLayers) != 0;
						bool canE2Collide = (c2.MaskLayers & c1.MyLayers) != 0;
						if (!canE1Collide && !canE2Collide)
							continue;

						// Calculate overlap vector
						ref var t2 = ref e2.GetMut<Transform>();
						var distance = t1.Pos - t2.Pos;
						var separation = c1.Radius + c2.Radius;
						var penetration = separation - distance.Length();
						if (penetration <= 0)
							continue;

						// Register collision
						if (canE1Collide) c1.collisionsCurrentFrame.Add(e2.Id);
						if (canE2Collide) c2.collisionsCurrentFrame.Add(e1.Id);
						// Console.WriteLine($"Collision between {e1.Id} and {e2.Id}");

						// Displace only if no triggers
						if (e1.Has<Trigger>() || e2.Has<Trigger>())
							continue;
							
						distance.Normalize();
						ref var b2 = ref e2.GetMut<PhysicsBody>();
						var totalBounce = b2.BounceCoeff + b1.BounceCoeff;
						float b1Displacement = b1.BounceCoeff / totalBounce * penetration;
						t1.Pos += distance * b1Displacement;
						t2.Pos -= distance * (penetration - b1Displacement);
					}
				}
			}

	}

	private void IntegratePosition(Entity e, ref Transform transform, ref PhysicsBody body)
	{
		transform.Pos += body.Vel;
	}

	private void DebugColliders(Iter it, Field<GlobalTransform> transform, Field<PhysicsBody> body, Field<Collider> collider)
	{
		var camera = it.World().Query<Camera>().First().Get<Camera>();
		var batch = it.World().Get<RenderCtx>().SpriteBatch;
		batch.Begin(transformMatrix: camera.GetTransformMatrix());
		foreach (int i in it)
		{
			var hue = 0f;
			if (it.Entity(i).Has<Trigger>()) hue = 200f;
			Color color = new(new HslColor(hue, 0.8f, 0.5f).ToRgb(), 0.5f);
			batch.DrawCircle(transform[i].Pos, collider[i].Radius, 10, color);
			batch.DrawLine(transform[i].Pos, transform[i].Pos + body[i].Vel * 10, Color.Green);
		}
		batch.End();
	}
}
