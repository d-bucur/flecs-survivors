using Flecs.NET.Core;
using System;
using System.Collections.Generic;
using Raylib_cs;
using System.Numerics;

namespace flecs_test;

record struct Player;
record struct Controls() {
    public bool MouseMovementEnabled = false;
}

class PlayerModule : IFlecsModule {
    const float PLAYER_ACCEL = 2f / 1000;

    public void InitModule(World world) {
		InitPlayer(world);

		world.Set(new Controls());

		world.System<PhysicsBody, Shooter>()
			.With<Player>()
			.Kind(Ecs.PostLoad)
			.Each(PlayerKeyInput);

		world.System<PhysicsBody, GlobalTransform>()
			.With<Player>()
			.Kind(Ecs.PostLoad)
			.Each(PlayerMouseInput); ;

		world.Observer<PowerCollector, Shooter>()
			.Event(Ecs.OnSet)
			.Each(UpdateWeaponLevels);
	}

	private void InitPlayer(World world) {
		Entity player = world.Entity("Player")
            .Add<Player>()
            .Set(new Transform(new Vector2(10, 20), Vector2.One, 0))
            .Set(new PhysicsBody(Vector2.Zero, Vector2.Zero, 0.2f, 0.85f))
            .Set(new Collider(17, CollisionFlags.PLAYER, CollisionFlags.ALL & ~CollisionFlags.PROJECTILE))
            .Set(new Heading())
            .Set(new Shooter(new List<IBulletPattern>([Weapons.PresetWeak])))
            .Set(new PowerCollector(200))
            .Set(new Health(10, 500))
            .Observe<OnCollisionEnter>(HandlePowerCollected)
            .Observe<OnCollisionEnter>(HandleCollisionWithEnemy);
		world.Entity()
			.Set(new Transform(new Vector2(0, 15), new Vector2(2f, 2f), 0))
			.Set(new Sprite("Content/sprites/packed2/characters.png"))
			.Set(new Animator("Blue_witch", "charge", 75))
			.ChildOf(player);
		Console.WriteLine($"Player: {player.Id.Value}");
	}

	static void UpdateWeaponLevels(ref PowerCollector collector, ref Shooter shooter) {
        var level = 1 + (uint)collector.Accumulated / 5;
        foreach (var weapon in shooter.Weapons) {
            if (weapon.Level == level) continue;
            weapon.Level = level;
            Console.WriteLine($"Updated weapon level to {level}");
        }
    }

    static void HandlePowerCollected(Entity e, ref OnCollisionEnter collision) {
        if (!collision.Other.Has<Powerup>() || !e.Has<PowerCollector>()) return;
        ref PowerCollector collector = ref e.GetMut<PowerCollector>();
        collector.Accumulated += collision.Other.Get<Powerup>().Value;
        e.Modified<PowerCollector>();
        collision.Other.Destruct();
        // Console.WriteLine($"Power: {collector.Accumulated}");
    }

    static void PlayerKeyInput(Entity e, ref PhysicsBody b, ref Shooter shooter) {
        Vector2 dir = Vector2.Zero;
        if (Raylib.IsKeyDown(KeyboardKey.D)) dir += new Vector2(1, 0);
        if (Raylib.IsKeyDown(KeyboardKey.A)) dir += new Vector2(-1, 0);
        if (Raylib.IsKeyDown(KeyboardKey.S)) dir += new Vector2(0, 1);
        if (Raylib.IsKeyDown(KeyboardKey.W)) dir += new Vector2(0, -1);
        if (dir != Vector2.Zero) Vector2.Normalize(dir);
        b.Accel = dir * PLAYER_ACCEL;

        if (Raylib.IsKeyPressed(KeyboardKey.Space)) {
            shooter.Enabled = !shooter.Enabled;
        }
    }

    static void PlayerMouseInput(Entity e, ref PhysicsBody b, ref GlobalTransform transform) {
        ref bool mouseMovementEnabled = ref e.CsWorld().GetMut<Controls>().MouseMovementEnabled;
        if (Raylib.IsMouseButtonPressed(MouseButton.Left)) mouseMovementEnabled = !mouseMovementEnabled;
        if (!mouseMovementEnabled) return;

        var camera = e.CsWorld().Query<Camera>().First().Get<Camera>();
        var screenPos = Raylib.GetMousePosition();
        var mousePosWorld = Raylib.GetScreenToWorld2D(screenPos, camera.Value);

        const int DIST_TO_MAX_SPEED = 150;
        Vector2 dir = (mousePosWorld - transform.Pos) / DIST_TO_MAX_SPEED;
        if (dir.Length() > 1) dir = Vector2.Normalize(dir);
        b.Accel = dir * PLAYER_ACCEL;
    }

    private void HandleCollisionWithEnemy(Entity player, ref OnCollisionEnter collision) {
        if (!collision.Other.Has<Enemy>()) return;
        if (Main.DecreaseHealth(player, collision.Penetration)) {
            if (player.Get<Health>().Value <= 0) {
                // TODO restart level
                Console.WriteLine($"Game Over");
            }
            Main.FlashDamage(player);
        }
	}
}