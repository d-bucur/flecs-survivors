using System.Collections.Generic;
using System.Numerics;
using Flecs.NET.Core;
using Raylib_cs;

namespace flecs_test;


record struct Shooter(List<IBulletPattern> Weapons, float Time = 0) {
    public Vector2? Target;
    public bool Enabled = true;
}
record struct Bullet;

class ShootingModule : IFlecsModule {
	public void InitModule(World world) {

		world.System<Shooter, GlobalTransform>()
			.Kind(Ecs.PreUpdate)
			.Iter(SetShooterTarget);

		world.System<Shooter, Transform, Heading>()
			.Kind(Ecs.PreUpdate)
			.Immediate()
			.Each(ProcessShooters);
	}

	private void SetShooterTarget(Iter it, Field<Shooter> shooter, Field<GlobalTransform> transform) {
		// TODO cache query
		var qEnemies = it.World().QueryBuilder<GlobalTransform>().With<Enemy>().Build();
		foreach (var shooterId in it) {
			var myPos = transform[shooterId].Pos;
			double smallestDistance = double.MaxValue;
			Vector2? closestEnemy = null;

			qEnemies.Each((ref GlobalTransform enemy) => {
				double distanceSqr = (myPos - enemy.Pos).LengthSquared();
				if (distanceSqr < smallestDistance) {
					smallestDistance = distanceSqr;
					closestEnemy = enemy.Pos;
				}
			});
			shooter[shooterId].Target = closestEnemy is null ? null : myPos - closestEnemy;
		}
	}

	static void ProcessShooters(Iter it, int i, ref Shooter shooter, ref Transform transform, ref Heading heading) {
		if (!shooter.Enabled) return;

		shooter.Time += it.DeltaTime();
		foreach (var weapon in shooter.Weapons) {
			var playerDir = heading.Value == Vector2.Zero ? Vector2.UnitX : Vector2.Normalize(heading.Value);
			foreach (var bullet in weapon.Tick(shooter.Time, playerDir, shooter.Target)) {
				SpawnBullet(it.World(), transform.Pos + bullet.Pos, bullet.Vel);
			}
		}
	}

	static void SpawnBullet(World world, Vector2 pos, Vector2 dir) {
		Entity bullet = world.Entity()
			.Add<Bullet>()
			.Add<Trigger>()
			.Set(new Transform(pos, Vector2.One, 0))
			.Set(new PhysicsBody(dir, Vector2.Zero, DragCoeff: 1))
			.Set(new DespawnTimed(5000f))
			.Set(new Collider(17, CollisionFlags.BULLET, CollisionFlags.ALL & ~CollisionFlags.POWERUP & ~CollisionFlags.BULLET))
			.Set(new Health(2))
			.Observe<OnCollisionEnter>(HandleBulletHit)
			.Observe<DeathEvent>(Main.SimpleDeath);
		var sprite = world.Entity()
			.Set(new Transform(new Vector2(0, 0), new Vector2(2, 2), 0))
			.Set(new Sprite("Content/sprites/packed2/characters.png", OriginAlign.CENTER, new Color(146, 252, 245)))
			.Set(new Animator("bullets", "bullet1", 75))
			.ChildOf(bullet);
		// RotationTween(sprite).RegisterEcs();
	}

	private static void HandleBulletHit(Entity bulletEntity, ref OnCollisionEnter collision) {
		if (collision.Other.Has<Scenery>()) bulletEntity.Destruct();
		if (!collision.Other.Has<Enemy>()) return;
		Main.DecreaseHealth(bulletEntity, collision.Penetration);
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