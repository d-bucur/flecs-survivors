using Microsoft.Xna.Framework;
using Flecs.NET.Core;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;
using System;

namespace flecs_test;

// TODO separate transform related code from physics
public record struct Transform(Vector2 Pos, Vector2 Scale, float Rot);
public record struct GlobalTransform(Vector2 Pos, Vector2 Scale, float Rot)
{
	public static GlobalTransform from(Transform t)
	{
		return new GlobalTransform(t.Pos, t.Scale, t.Rot);
	}

	internal GlobalTransform Apply(Transform other)
	{
		return new GlobalTransform(other.Pos + this.Pos, other.Scale * this.Scale, other.Rot + this.Rot);
	}
}
public record struct PhysicsBody(Vector2 Vel, Vector2 Accel);
public record struct Collider(float Radius);

class Movement : IFlecsModule
{
	public void InitModule(World world)
	{
		// better place for this?
		world.System<Transform, PhysicsBody>()
			.With<Player>()
			.Each(PlayerInput);

		world.System<Transform, PhysicsBody>()
			.With<Enemy>()
			.Iter(FollowPlayer);

		world.System<Transform, PhysicsBody>()
			.Kind(Ecs.OnUpdate)
			.Each(MovementSys);

		world.System<Transform, PhysicsBody, Collider>()
			.Kind(Ecs.OnUpdate)
			.Run(HandleCollisions);

		world.System<Transform, PhysicsBody, Collider>()
			.Kind<RenderPhase>()
			.Iter(DebugColliders);
	}

	private void PlayerInput(Entity e, ref Transform t, ref PhysicsBody b)
	{
		const float SPEED = 5;
		var state = Keyboard.GetState();
		Vector2 dir = Vector2.Zero;
		if (state.IsKeyDown(Keys.D)) dir += new Vector2(1, 0);
		if (state.IsKeyDown(Keys.A)) dir += new Vector2(-1, 0);
		if (state.IsKeyDown(Keys.S)) dir += new Vector2(0, 1);
		if (state.IsKeyDown(Keys.W)) dir += new Vector2(0, -1);
		if (dir != Vector2.Zero) dir.Normalize();
		b.Vel = dir * SPEED;
	}

	private void HandleCollisions(Iter it)
	{
		var q = it.World().Query<Transform, PhysicsBody, Collider>();
		q.Each((Entity e1, ref Transform t1, ref PhysicsBody b1, ref Collider c1) =>
		{
			// Can't capture refs inside the second lambda, so using a workaround
			var t1Pos = t1.Pos;
			var c1Radius = c1.Radius;
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
				// Console.WriteLine($"Collision between {it.Entity(i)} and {it.Entity(j)}");
				// Handle Collision
				distance.Normalize();
				float displacement = penetration / 2;
				t1PosDisplacement += distance * displacement;
				t2.Pos -= distance * displacement;
			});
			// Not ideal to apply it here, but it works
			t1.Pos += t1PosDisplacement;
		});
	}

	private void FollowPlayer(Iter it, Field<Transform> transform, Field<PhysicsBody> body)
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

	private void MovementSys(Entity e, ref Transform transform, ref PhysicsBody body)
	{
		transform.Pos += body.Vel;
	}

	private void DebugColliders(Iter it, Field<Transform> transform, Field<PhysicsBody> body, Field<Collider> collider)
	{
		var batch = it.World().Get<RenderCtx>().SpriteBatch;
		batch.Begin();
		foreach (int i in it)
		{
			batch.DrawCircle(transform[i].Pos, collider[i].Radius, 10, Color.Red);
		}
		batch.End();
	}
}
