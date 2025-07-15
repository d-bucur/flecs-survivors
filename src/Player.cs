using Flecs.NET.Core;
using System;
using System.Collections.Generic;
using Raylib_cs;
using System.Numerics;

namespace flecs_survivors;

record struct Player;
record struct Controls() {
    public bool MouseMovementEnabled = false;
}

class PlayerModule : IFlecsModule {
    const float PLAYER_ACCEL = 2f / 1000;

    public void InitModule(World world) {
        world.Set(new Controls());
        GameState.InitGame.Observe<InitGameEvent>(() => InitPlayer(ref world));

        world.System<PhysicsBody, Shooter>()
            .With<Player>()
            .Kind(Ecs.PostLoad)
            .Each(PlayerKeyInput)
            .Entity.DependsOn(GameState.Running);

        world.System<PhysicsBody, GlobalTransform>()
            .With<Player>()
            .Kind(Ecs.PostLoad)
            .Each(PlayerMouseInput)
            .Entity.DependsOn(GameState.Running);
    }

    private void InitPlayer(ref World world) {
        var map = world.Get<Tiled.TiledMap>();
        var startPos = new Vector2(map.Width * map.TileWidth / 2, map.Height * map.TileHeight / 2);

        Entity player = world.Entity("Player")
            .Add<Player>()
            .Add<InGameEntity>()
            .Set(new Transform(startPos, Vector2.One, 0))
            .Set(new PhysicsBody(Vector2.Zero, Vector2.Zero, 0.2f, 0.85f))
            .Set(new Collider(new SphereCollider(17), CollisionFlags.PLAYER, CollisionFlags.ALL & ~CollisionFlags.BULLET))
            .Set(new Heading())
            .Set(new Shooter(new List<IBulletPattern>([
                Weapons.PresetClosestSMG,
                // Weapons.PresetWeak,
                // Weapons.PresetShotgun,
                // Weapons.PresetSpiral,
                // Weapons.PresetSpread,
            ])))
            .Set(new PowerCollector(250))
            .Set(new Health(3, 500))
            .Observe<OnCollisionEnter>(PowerupModule.HandlePowerCollected)
            .Observe<OnCollisionEnter>(HandleCollisionWithEnemy);
        world.Entity()
            .Set(new Transform(new Vector2(0, 15), new Vector2(2f, 2f), 0))
            .Set(new Sprite(Textures.MEGA_SHEET))
            .Set(new Animator("Blue_witch", "charge", 75))
            .ChildOf(player);

        CachedQueries.camera.First()
            .Set(new FollowTarget(player))
            .Set(new Transform(startPos, Vector2.One));
        Console.WriteLine($"Player: {player.Id.Value}");
    }

    static void PlayerKeyInput(Entity e, ref PhysicsBody b, ref Shooter shooter) {
        var actions = e.CsWorld().Get<InputManager>();
        Vector2 dir = Vector2.Zero;
        if (actions.IsDown(InputActions.RIGHT)) dir += new Vector2(1, 0);
        if (actions.IsDown(InputActions.LEFT)) dir += new Vector2(-1, 0);
        if (actions.IsDown(InputActions.DOWN)) dir += new Vector2(0, 1);
        if (actions.IsDown(InputActions.UP)) dir += new Vector2(0, -1);
        if (dir != Vector2.Zero) Vector2.Normalize(dir);
        b.Accel = dir * PLAYER_ACCEL;

        if (actions.IsPressed(InputActions.CONFIRM)) {
            shooter.Enabled = !shooter.Enabled;
        }
    }

    static void PlayerMouseInput(Entity e, ref PhysicsBody b, ref GlobalTransform transform) {
        ref bool mouseMovementEnabled = ref e.CsWorld().GetMut<Controls>().MouseMovementEnabled;
        if (Raylib.IsMouseButtonPressed(MouseButton.Left)) mouseMovementEnabled = !mouseMovementEnabled;
        if (!mouseMovementEnabled) return;

        var camera = CachedQueries.camera.First().Get<Camera>();
        var screenPos = Raylib.GetMousePosition();
        var mousePosWorld = Raylib.GetScreenToWorld2D(screenPos, camera.Value);

        const int DIST_TO_MAX_SPEED = 150;
        Vector2 dir = (mousePosWorld - transform.Pos) / DIST_TO_MAX_SPEED;
        if (dir.Length() > 1) dir = Vector2.Normalize(dir);
        b.Accel = dir * PLAYER_ACCEL;
    }

    private void HandleCollisionWithEnemy(Entity player, ref OnCollisionEnter collision) {
        if (!collision.Other.Has<Enemy>()) return;
        if (Main.DecreaseHealth(player, collision.Data.Penetration)) {
            if (player.Get<Health>().Value <= 0) {
                Console.WriteLine($"Game Over");
                GameState.ChangeState(GameState.InitGame);
            }
            Main.FlashDamage(player);
            Main.CameraShake(10);
        }
    }
}