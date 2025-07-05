using System;
using System.Numerics;
using Flecs.NET.Core;

namespace flecs_test;

class Level {
    public static void InitLevel(ref World world) {
        // RandomGrid(ref world);
        // FixedWalls(ref world);
        // FieldTestGrid(ref world);

        var map = TiledMapLoader.LoadMap(ref world.GetMut<ContentManager>());
        map.TileHeight *= 2;
        map.TileWidth *= 2;
        world.Set(map);
    }

    static void RandomGrid(ref World world) {
        for (var i = -5; i < 6; i++)
            for (var j = -5; j < 6; j++) {
                SpawnObstacle(ref world, new Vector2(
                    i * 300 + (Random.Shared.NextSingle() - 0.5f) * 300,
                    j * 300 + (Random.Shared.NextSingle() - 0.5f) * 300
                ));
            }
    }

    static void FieldTestGrid(ref World world) {
        const float WIDTH = 2;
        const float DIST = 110;
        var offset = new Vector2(25, 25);
        for (var i = -WIDTH; i <= WIDTH; i++)
            for (var j = -WIDTH; j <= WIDTH; j++)
                SpawnObstacle(ref world, new Vector2(i * DIST, j * DIST) + offset);
    }

    static void FixedWalls(ref World world) {
        for (var i = 0; i < 5; i++) {
            SpawnObstacle(ref world, new Vector2(120, i * 50));
            SpawnObstacle(ref world, new Vector2(-300, i * 50));
            SpawnObstacle(ref world, new Vector2(i * 50 - 300, 300));
            SpawnObstacle(ref world, new Vector2(i * 50 - 280, -300));
            SpawnObstacle(ref world, new Vector2(i * 50 + 80, -300));
        }
    }

    static void SpawnObstacle(ref World world, Vector2 position) {
        Entity e = world.Entity()
            .Add<Scenery>()
            .Set(new Transform(position, Vector2.One, 0))
            .Set(new PhysicsBody(Vector2.Zero, Vector2.Zero, 0))
            .Set(new Collider(new SphereCollider(25), CollisionFlags.SCENERY, CollisionFlags.ALL & ~CollisionFlags.POWERUP & ~CollisionFlags.SCENERY));
        world.Entity()
            .Set(new Transform(new Vector2(0, 30), new Vector2(1f, 1f), 0))
            .Set(new Sprite("Content/sprites/grassBlock.png"))
            .ChildOf(e);
    }
}