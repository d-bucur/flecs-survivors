using System;
using System.Collections.Generic;
using System.Numerics;
using Flecs.NET.Core;
using Raylib_cs;

namespace flecs_survivors;

record struct Shooter(List<IBulletPattern> Weapons, float Time = 0) {
	public Vector2? Target;
	public bool Enabled = true;
	// Can make more efficient by using map
	public IBulletPattern GetWeapon(string name) {
		var v = Weapons.FindIndex((p) => p.Name == name);
		return Weapons[v];
	}
}
record struct Bullet(float Pushback = 0f);

class ShootingModule : IFlecsModule {
	public void InitModule(World world) {

		// Don't need to do this every frame
		world.System<Shooter, GlobalTransform>()
			.Kind(Ecs.PreUpdate)
			.Iter(SetShooterTarget);

		world.System<Shooter, Transform, Heading>()
			.Kind(Ecs.PreUpdate)
			.Immediate()
			.TickSource(Timers.runningTimer)
			.Each(ProcessShooters);
	}

	private void SetShooterTarget(Iter it, Field<Shooter> shooter, Field<GlobalTransform> transform) {
		// TODO cache query
		var qEnemies = it.World().QueryBuilder<GlobalTransform>().With<Enemy>().Build();
		foreach (var shooterId in it) {
			var myPos = transform[shooterId].Pos;
			double smallestDistance = double.MaxValue;
			Vector2? closestEnemy = null;

			// TODO should do spatial query around
			qEnemies.Each((ref GlobalTransform enemy) => {
				double distanceSqr = (myPos - enemy.Pos).LengthSquared();
				if (distanceSqr < smallestDistance) {
					smallestDistance = distanceSqr;
					closestEnemy = enemy.Pos;
				}
			});
			const int MAX_TARGET_DISTANCE = 400 * 400;
			if (smallestDistance > MAX_TARGET_DISTANCE) return;
			shooter[shooterId].Target = closestEnemy is null ? null : myPos - closestEnemy;
		}
	}

	static void ProcessShooters(Iter it, int i, ref Shooter shooter, ref Transform transform, ref Heading heading) {
		if (!shooter.Enabled) return;

		shooter.Time += it.DeltaTime();
		foreach (var weapon in shooter.Weapons) {
			var playerDir = heading.Value == Vector2.Zero ? Vector2.UnitX : Vector2.Normalize(heading.Value);
			foreach (var bulletData in weapon.Tick(shooter.Time, playerDir, shooter.Target)) {
				SpawnBullet(it.World(), transform.Pos + bulletData.Pos, bulletData);
			}
		}
	}

	static void SpawnBullet(World world, Vector2 pos, BulletData bulletData) {
		Entity bullet = world.Entity()
			.Add<Trigger>()
            .Add<InGameEntity>()
			.Set(new Bullet(bulletData.Pushback))
			.Set(new Transform(pos, Vector2.One, 0))
			.Set(new PhysicsBody(bulletData.Vel, Vector2.Zero, DragCoeff: 1))
			.Set(new DespawnTimed(5000f))
			.Set(new Collider(new SphereCollider(17), CollisionFlags.BULLET, CollisionFlags.ALL & ~CollisionFlags.POWERUP & ~CollisionFlags.BULLET))
			.Set(new Health(2))
			.Observe<OnCollisionEnter>(HandleBulletHit)
			.Observe<DeathEvent>(HandleBulletDeath);
		var sprite = world.Entity()
			.Set(new Transform(new Vector2(0, 0), new Vector2(2, 2), 0))
			.Set(new Sprite(Textures.MEGA_SHEET, OriginAlign.CENTER, bulletData.Color))
			.Set(new Animator("bullets", bulletData.SpriteName, 75))
			.ChildOf(bullet);
		// RotationTween(sprite).RegisterEcs();
	}

	private static void HandleBulletHit(Entity bulletEntity, ref OnCollisionEnter collision) {
		bool needFx = false;
		// TODO bullet invuln and penetration not working correcly. Maybe should check collision in a single place for more control
		if (collision.Other.Has<Enemy>()) {
			needFx = Main.DecreaseHealth(bulletEntity, collision.Data.Penetration);
		}
		if (collision.Other.Has<Scenery>()) {
			bulletEntity.Destruct();
			needFx = true;
		}
		if (needFx) {
			SpawnBulletFx(bulletEntity.CsWorld(), collision.Data.ContactPoint);
		}
	}

	private static void HandleBulletDeath(Entity e, ref DeathEvent death) {
		e.Destruct();
	}

	private static void SpawnBulletFx(World world, Vector2 position) {
		var fx = world.Entity()
            .Add<InGameEntity>()
			.Set(new Transform(position, Vector2.One, 0));
		var sprite = world.Entity()
			.Set(new Transform(new Vector2(0, 0), new Vector2(1, 1), 0))
			.Set(new Sprite(Textures.MEGA_SHEET, OriginAlign.CENTER, new Color(146, 252, 245)))
			.Set(new Animator("fx", "fx1", 34, OnFinishCb: () => fx.Destruct()))
			.ChildOf(fx);
	}

	public static Tween RotationTween(Entity e) {
		return new Tween(e).With(
			(ref Transform t, float rot) => t.Rot = rot,
			0,
			360,
			500,
			(t) => t,
			Repetitions: -1
		);
	}
}