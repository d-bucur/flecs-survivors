using Microsoft.Xna.Framework;
using Flecs.NET.Core;
using System;
using Microsoft.Xna.Framework.Input;

namespace flecs_test;

public record struct Transform(Vector2 Pos, Vector2 Scale, float Rot);
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
			.Iter(HandleCollisions);
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
		// Console.WriteLine($"{e}: {b.Vel}");
	}

	private void HandleCollisions(Iter it, Field<Transform> transform, Field<PhysicsBody> body, Field<Collider> collider)
	{
		// TODO avoid repetitions (a,b), (b,a)
		foreach (var i in it)
		{
			foreach (var j in it)
			{
				if (i == j) continue;
				var distance = transform[i].Pos - transform[j].Pos;
				var separation = collider[i].Radius + collider[j].Radius;
				var penetration = separation - distance.Length();
				if (penetration <= 0) continue;
				// Console.WriteLine($"Collision between {i} and {j}");
				// Collision
				distance.Normalize();
				float displacement = penetration / 2;
				transform[i].Pos += distance * displacement;
				transform[j].Pos -= distance * displacement;
			}
		}
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
}
