using Flecs.NET.Core;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

namespace flecs_test;

record struct Player;

class PlayerModule : IFlecsModule
{
	public void InitModule(World world)
	{
		Entity player = world.Entity("Player")
			.Add<Player>()
			.Set(new Transform(new Vector2(10, 20), Vector2.One, 0))
			.Set(new PhysicsBody(new Vector2(1, 1), Vector2.Zero, 0.2f))
			.Set(new Collider(17))
			.Set(new Heading())
			.Set(new Shooter(new List<IBulletPattern>([Weapons.PresetShotgun])))
			.Set(new PowerCollector(200))
			.Observe<CollisionEvent>(HandlePowerCollected);
		world.Entity()
			.Set(new Transform(new Vector2(0, 15), new Vector2(0.5f, 0.5f), 0))
			.Set(new Sprite("sprites/alienGreen_walk1"))
			.ChildOf(player);
		Console.WriteLine($"Player: {player}");

		world.System<PhysicsBody>()
			.With<Player>()
			.Kind(Ecs.PreUpdate)
			.Each(PlayerInput);

		world.Observer<PowerCollector, Shooter>()
			.Event(Ecs.OnSet)
			.Each(UpdateWeaponLevels);
	}

	static void UpdateWeaponLevels(ref PowerCollector collector, ref Shooter shooter)
	{
		var level = 1 + (uint)collector.Accumulated / 5;
		foreach (var weapon in shooter.Weapons)
		{
			if (weapon.Level == level) continue;
			weapon.Level = level;
			Console.WriteLine($"Updated weapon level to {level}");
		}
	}

	static void HandlePowerCollected(Entity e, ref CollisionEvent collision)
	{
		if (!collision.Other.Has<Powerup>() || !e.Has<PowerCollector>()) return;
		ref PowerCollector collector = ref e.GetMut<PowerCollector>();
		collector.Accumulated += collision.Other.Get<Powerup>().Value;
		e.Modified<PowerCollector>();
		collision.Other.Destruct();
		// Console.WriteLine($"Power: {collector.Accumulated}");
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