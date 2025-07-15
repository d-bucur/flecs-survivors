using System;
using System.Numerics;
using Flecs.NET.Core;
using Tiled;

namespace flecs_survivors;

// TODO make into proper flecs module?
class LevelLoader : IFlecsModule {
	public void InitModule(World world) {
        var map = TiledMapLoader.LoadMap(ref world.GetMut<ContentManager>());
        map.TileHeight *= 2;
        map.TileWidth *= 2;
        world.Set(map);
        
        GameState.InitGame.Observe<OnStateEntered>(() => InitLevelColliders(ref world, ref map));
	}

    private static void InitLevelColliders(ref World world, ref TiledMap map) {
        foreach (var layer in map.Layers) {
            if (!layer.Colliders || !layer.Visible)
                continue;
            for (int i = 0; i < layer.Tiles.Length; i++) {
                int tile = layer.Tiles[i];
                if (tile == 0) continue;
                var y = Math.DivRem(i, layer.Width, out var x);
                var pos = new Vector2((x + 0.5f) * map.TileWidth, (y + 0.5f) * map.TileHeight);
                int size = map.TileWidth / 2;
                world.Entity()
                    .Add<Scenery>()
                    .Add<InGameEntity>()
                    .Set(new Transform(pos, Vector2.One, 0))
                    .Set(new PhysicsBody(Vector2.Zero, Vector2.Zero, 0))
                    .Set(new Collider(new AABBCollider(new Vector2(size)), CollisionFlags.SCENERY, CollisionFlags.ALL & ~CollisionFlags.POWERUP & ~CollisionFlags.SCENERY));
            }
        }
    }
}