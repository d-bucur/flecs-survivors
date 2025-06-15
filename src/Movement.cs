using Microsoft.Xna.Framework;
using Flecs.NET.Core;
using System;

namespace flecs_test;

public record struct Transform(Vector2 Pos, Vector2 Scale, float Rot);
public record struct PhysicsBody(Vector2 Vel, Vector2 Accel);

class Movement : IFlecsModule
{
	public void InitModule(World world)
	{
		world.System<Transform, PhysicsBody>()
			.Kind(Ecs.OnUpdate)
			.Each(MovementSys);

		world.System<Transform, PhysicsBody>()
			.With<Enemy>()
			.Iter(FollowPlayer);
	}

	private void FollowPlayer(Iter it, Field<Transform> transform, Field<PhysicsBody> body)
	{
		// Get Transform of Player and update all Enemy bodies to follow
		const float SPEED = 1;
		var query = it.World().QueryBuilder<Transform>().With<Player>().Build();
		var player = query.First().Get<Transform>();

		foreach (var i in it)
		{
			var dir = player.Pos - transform[i].Pos;
			dir.Normalize();
			body[i].Vel = dir * SPEED;
		}
	}

	private void MovementSys(Entity e, ref Transform t, ref PhysicsBody b)
	{
		t.Pos += b.Vel;
	}
}
