using Flecs.NET.Core;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework;
using System;

namespace flecs_test;

public record struct Shooter(int Placeholder);
public record struct Projectile;

class PlayerModule : IFlecsModule
{
	public void InitModule(World world)
	{
		// Main game logic, divide up later
		world.System<PhysicsBody>()
			.With<Player>()
			.Kind(Ecs.PreUpdate)
			.Each(PlayerInput);

		world.System<Transform, PhysicsBody>()
			.With<Enemy>()
			.Kind(Ecs.PreUpdate)
			.Iter(FollowPlayer);

		var tickSource = world.Timer().Interval(1000f);
		world.System<Shooter, Transform>()
			.TickSource(tickSource)
			.Kind(Ecs.PreUpdate)
			.Each(ProcessShooters);
	}

	static void ProcessShooters(Iter it, int i, ref Shooter shooter, ref Transform transform)
	{
		const float BULLET_SPPED = 7;
		SpawnBullet(it.World(), transform.Pos, new Vector2(1, 0) * BULLET_SPPED);
		SpawnBullet(it.World(), transform.Pos, new Vector2(-1, 0) * BULLET_SPPED);
		SpawnBullet(it.World(), transform.Pos, new Vector2(0, -1) * BULLET_SPPED);
		SpawnBullet(it.World(), transform.Pos, new Vector2(0, 1) * BULLET_SPPED);
	}

	static void SpawnBullet(World world, Vector2 pos, Vector2 dir)
	{
		Entity bullet = world.Entity()
			.Add<Projectile>()
			.Add<Trigger>()
			.Set(new Transform(pos, Vector2.One, 0))
			.Set(new PhysicsBody(dir, Vector2.Zero))
			.Set(new Collider(17));
		world.Entity()
			.Set(new Transform(new Vector2(0, 15), new Vector2(0.5f, 0.5f), 0))
			.Set(new Sprite("sprites/bee"))
			.ChildOf(bullet);
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

	static void FollowPlayer(Iter it, Field<Transform> transform, Field<PhysicsBody> body)
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