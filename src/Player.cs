using Flecs.NET.Core;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using MonoGame.Extended.Input;

namespace flecs_test;

record struct Player;
record struct Controls() {
    public bool MouseMovementEnabled = false;
}

class PlayerModule : IFlecsModule {
    const float PLAYER_ACCEL = 0.6f;

    public void InitModule(World world) {
        Entity player = world.Entity("Player")
            .Add<Player>()
            .Set(new Transform(new Vector2(10, 20), Vector2.One, 0))
            .Set(new PhysicsBody(new Vector2(1, 1), Vector2.Zero, 0.2f, 0.85f))
            .Set(new Collider(17, CollisionFlags.PLAYER, CollisionFlags.ALL & ~CollisionFlags.PROJECTILE))
            .Set(new Heading())
            .Set(new Shooter(new List<IBulletPattern>([Weapons.PresetWeak])))
            .Set(new PowerCollector(200))
            .Observe<OnCollisionEnter>(HandlePowerCollected);
        world.Entity()
            .Set(new Transform(new Vector2(0, 15), new Vector2(0.5f, 0.5f), 0))
            .Set(new Sprite("sprites/alienGreen_walk1"))
            .ChildOf(player);
        Console.WriteLine($"Player: {player.Id.Value}");

        world.Set(new Controls());

        world.System<PhysicsBody>()
            .With<Player>()
            .Kind(Ecs.PostLoad)
            .Each(PlayerKeyInput);

        world.System<PhysicsBody, GlobalTransform>()
            .With<Player>()
            .Kind(Ecs.PostLoad)
            .Each(PlayerMouseInput);

        world.Observer<PowerCollector, Shooter>()
            .Event(Ecs.OnSet)
            .Each(UpdateWeaponLevels);
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

    static void PlayerKeyInput(Entity e, ref PhysicsBody b) {
        var state = Keyboard.GetState();
        Vector2 dir = Vector2.Zero;
        if (state.IsKeyDown(Keys.D)) dir += new Vector2(1, 0);
        if (state.IsKeyDown(Keys.A)) dir += new Vector2(-1, 0);
        if (state.IsKeyDown(Keys.S)) dir += new Vector2(0, 1);
        if (state.IsKeyDown(Keys.W)) dir += new Vector2(0, -1);
        if (dir != Vector2.Zero) dir.Normalize();
        b.Accel = dir * PLAYER_ACCEL;
    }

    static void PlayerMouseInput(Entity e, ref PhysicsBody b, ref GlobalTransform transform) {
        var mouseState = MouseExtended.GetState();
        ref bool mouseMovementEnabled = ref e.CsWorld().GetMut<Controls>().MouseMovementEnabled;
        if (mouseState.WasButtonPressed(MouseButton.Left)) mouseMovementEnabled = !mouseMovementEnabled;
        if (!mouseMovementEnabled) return;

        var camera = e.CsWorld().Query<Camera>().First().Get<Camera>();
        var mousePosWorld = camera.Value.ScreenToWorld(new Vector2(mouseState.X, mouseState.Y) - camera.Value.Origin);
        const int DIST_TO_MAX_SPEED = 150;
        Vector2 dir = (mousePosWorld - transform.Pos) / DIST_TO_MAX_SPEED;
        if (dir.Length() > 1) dir = Vector2.Normalize(dir);
        b.Accel = dir * PLAYER_ACCEL;
    }
}