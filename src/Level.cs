using System;
using System.Numerics;
using Flecs.NET.Core;
using Tiled;

namespace flecs_test;

// TODO make into proper flecs module?
class LevelLoader {
    public static void InitLevel(ref World world) {
        // RandomGrid(ref world);
        // FixedWalls(ref world);
        // FieldTestGrid(ref world);

        var map = TiledMapLoader.LoadMap(ref world.GetMut<ContentManager>());
        map.TileHeight *= 2;
        map.TileWidth *= 2;
        world.Set(map);

        world.System<TiledMap>()
            .Kind(Ecs.OnStart)
            .Each(InitLevelColliders);
    }

    private static void InitLevelColliders(Entity e, ref TiledMap map) {
        foreach (var layer in map.Layers) {
            if (!layer.Colliders || !layer.Visible)
                continue;
            for (int i = 0; i < layer.Tiles.Length; i++) {
                int tile = layer.Tiles[i];
                if (tile == 0) continue;
                var y = Math.DivRem(i, layer.Width, out var x);
                var pos = new Vector2((x + 0.5f) * map.TileWidth, (y + 0.5f) * map.TileHeight);
                int size = map.TileWidth / 2;
                e.CsWorld().Entity()
                    .Add<Scenery>()
                    .Set(new Transform(pos, Vector2.One, 0))
                    .Set(new PhysicsBody(Vector2.Zero, Vector2.Zero, 0))
                    .Set(new Collider(new AABBCollider(new Vector2(size)), CollisionFlags.SCENERY, CollisionFlags.ALL & ~CollisionFlags.POWERUP & ~CollisionFlags.SCENERY));
            }
        }
    }
}