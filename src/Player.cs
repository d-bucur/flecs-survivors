using Flecs.NET.Core;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework;

namespace flecs_test;

class PlayerModule : IFlecsModule
{
	public void InitModule(World world)
	{
		// better place for input?
		world.System<PhysicsBody>()
			.With<Player>()
			.Kind(Ecs.PreUpdate)
			.Each(PlayerInput);

		world.System<Transform, PhysicsBody>()
			.With<Enemy>()
			.Iter(FollowPlayer);
	}

	private void PlayerInput(Entity e, ref PhysicsBody b)
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
}