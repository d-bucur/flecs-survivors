using Microsoft.Xna.Framework;
using Flecs.NET.Core;
using MonoGame.Extended;
using System;

namespace flecs_test;

public record struct PhysicsBody(Vector2 Vel, Vector2 Accel, float BounceCoeff = 1);
public record struct Collider(float Radius);
enum Trigger;
public record struct CollisionEvent(Entity Other);

class PhysicsModule : IFlecsModule
{
	public void InitModule(World world)
	{
		world.System<Transform, PhysicsBody>()
			.Kind(Ecs.OnUpdate)
			.MultiThreaded()
			.Each(MovementSys);

		world.System<Transform, PhysicsBody, Collider>()
			.Kind(Ecs.OnUpdate)
			.Run(HandleCollisions);

		world.System<Transform, PhysicsBody, Collider>()
			.Kind<RenderPhase>()
			.Iter(DebugColliders);
	}

	// TODO update to GlobalTransforms. Need to propagate back to Transform
	private void HandleCollisions(Iter it)
	{
		var q = it.World().Query<Transform, PhysicsBody, Collider>();
		q.Each((Entity e1, ref Transform t1, ref PhysicsBody b1, ref Collider c1) =>
		{
			// Can't capture refs inside the second lambda, so using a workaround
			var t1Pos = t1.Pos;
			var c1Radius = c1.Radius;
			var b1Bounce = b1.BounceCoeff;
			var t1PosDisplacement = Vector2.Zero;

			q.Each((Entity e2, ref Transform t2, ref PhysicsBody b2, ref Collider c2) =>
			{
				// Is there any case where the < can backfire?
				if (e1 >= e2) return;
				// Console.WriteLine($"Checking collisions: {e1} and {e2}");
				var distance = t1Pos - t2.Pos;
				var separation = c1Radius + c2.Radius;
				var penetration = separation - distance.Length();

				if (penetration <= 0) return;

				e2.Emit(new CollisionEvent(e1));
				e1.Emit(new CollisionEvent(e2));
				if (e1.Has<Trigger>() || e2.Has<Trigger>()) return;
				// Console.WriteLine($"Collision between {e1} and {e2}");

				// Handle Collision
				distance.Normalize();
				var totalBounce = b2.BounceCoeff + b1Bounce;
				float b1Displacement = b1Bounce / totalBounce * penetration;
				t1PosDisplacement += distance * b1Displacement;
				t2.Pos -= distance * (penetration - b1Displacement);
			});
			// Not ideal to apply it here, but it works
			t1.Pos += t1PosDisplacement;
		});
	}

	private void MovementSys(Entity e, ref Transform transform, ref PhysicsBody body)
	{
		transform.Pos += body.Vel;
	}

	private void DebugColliders(Iter it, Field<Transform> transform, Field<PhysicsBody> body, Field<Collider> collider)
	{
		var batch = it.World().Get<RenderCtx>().SpriteBatch;
		Color color = new(new HslColor(0, 0.8f, 0.5f).ToRgb(), 0.5f);
		batch.Begin();
		foreach (int i in it)
		{
			batch.DrawCircle(transform[i].Pos, collider[i].Radius, 10, color);
		}
		batch.End();
	}
}
