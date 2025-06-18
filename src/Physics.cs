using Microsoft.Xna.Framework;
using Flecs.NET.Core;
using MonoGame.Extended;
using System;
using System.Collections.Generic;

namespace flecs_test;

enum Trigger;
record struct PhysicsBody(Vector2 Vel, Vector2 Accel, float BounceCoeff = 1);
record struct Collider(float Radius)
{
	public HashSet<Id> collisionsLastFrame = new();
	public HashSet<Id> collisionsCurrentFrame = new();
}

record struct OnCollisionEnter(Entity Other);
record struct OnCollisionExit(Entity Other);
record struct OnCollisionStay(Entity Other);

record struct Heading(Vector2 Value);

class PhysicsModule : IFlecsModule
{
	public void InitModule(World world)
	{
		world.System<Transform, PhysicsBody>()
			.Kind(Ecs.OnUpdate)
			.MultiThreaded()
			.Each(IntegratePosition);

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
			var c1Current = c1.collisionsCurrentFrame;

			q.Each((Entity e2, ref Transform t2, ref PhysicsBody b2, ref Collider c2) =>
			{
				// Is there any case where the < can backfire?
				if (e1 >= e2) return;
				// TODO better avoidance system using bitmask
				bool isTriggerE1 = e1.Has<Trigger>();
				bool isTriggerE2 = e2.Has<Trigger>();
				if (isTriggerE1 && isTriggerE2) return;

				// Console.WriteLine($"Checking collisions: {e1} and {e2}");
				var distance = t1Pos - t2.Pos;
				var separation = c1Radius + c2.Radius;
				var penetration = separation - distance.Length();

				if (penetration <= 0) return;

				c2.collisionsCurrentFrame.Add(e1.Id);
				c1Current.Add(e2.Id);
				// Console.WriteLine($"Collision between {e1.Id} and {e2.Id}");

				if (isTriggerE1 || isTriggerE2) return;

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
