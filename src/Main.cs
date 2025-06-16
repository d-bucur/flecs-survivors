using Microsoft.Xna.Framework;
using Flecs.NET.Core;
using static System.Linq.Enumerable;
using System;

namespace flecs_test;

public record struct Player;
public record struct Enemy;

class Main : IFlecsModule
{
	public void InitModule(World world)
	{
		Entity player = world.Entity()
			.Add<Player>()
			.Set(new Transform(new Vector2(10, 20), Vector2.One, 0))
			.Set(new PhysicsBody(new Vector2(1, 1), Vector2.Zero))
			.Set(new Collider(17));
		world.Entity()
			.Set(new Transform(new Vector2(0, 15), new Vector2(0.5f, 0.5f), 0))
			.Set(new Sprite("sprites/alienGreen_walk1"))
			.ChildOf(player);
		Console.WriteLine($"Player: {player}");

		foreach (int i in Range(1, 5))
		{
			var enemy = world.Entity()
				.Add<Enemy>()
				.Set(new Transform(new Vector2(100 * i, 20), Vector2.One, 0))
				.Set(new PhysicsBody(new Vector2(1, 1), Vector2.Zero))
				.Set(new Collider(17));
			world.Entity()
				.Set(new Transform(new Vector2(0, 15), new Vector2(0.5f, 0.5f), 0))
				.Set(new Sprite("sprites/alienPink_walk1"))
				.ChildOf(enemy);
		}
	}
}