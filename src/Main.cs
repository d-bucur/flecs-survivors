using Microsoft.Xna.Framework;
using Flecs.NET.Core;
using Microsoft.Xna.Framework.Graphics;

namespace flecs_test;

public record struct Player;
public record struct Enemy;

class Main : IFlecsModule
{
	public void InitModule(World world)
	{
		Entity entity = world.Entity()
			.Set(new Transform(new Vector2(10, 20), new Vector2(0.5f, 0.5f), 0))
			.Set(new PhysicsBody(new Vector2(1, 1), Vector2.Zero))
			// TODO set sprite automatically
			.Set(new Sprite(world.Get<GameCtx>().Content.Load<Texture2D>("sprites/investor2")));
	}
}