using Microsoft.Xna.Framework;
using Flecs.NET.Core;

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
	}

	void MovementSys(Entity e, ref Transform t, ref PhysicsBody b)
	{
		t.Pos += b.Vel;
	}
}
