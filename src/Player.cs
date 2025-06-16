using Flecs.NET.Core;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework;
using System;

namespace flecs_test;

record struct Player;

class PlayerModule : IFlecsModule
{
	public void InitModule(World world)
	{
		Entity player = world.Entity()
			.Add<Player>()
			.Set(new Shooter())
			.Set(new Transform(new Vector2(10, 20), Vector2.One, 0))
			.Set(new PhysicsBody(new Vector2(1, 1), Vector2.Zero, 0.2f))
			.Set(new Collider(17));
		world.Entity()
			.Set(new Transform(new Vector2(0, 15), new Vector2(0.5f, 0.5f), 0))
			.Set(new Sprite("sprites/alienGreen_walk1"))
			.ChildOf(player);
		Console.WriteLine($"Player: {player}");

		// Main game logic, divide up later
		world.System<PhysicsBody>()
			.With<Player>()
			.Kind(Ecs.PreUpdate)
			.Each(PlayerInput);
	}

	static void PlayerInput(Entity e, ref PhysicsBody b)
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
}