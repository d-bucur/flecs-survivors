using Microsoft.Xna.Framework;
using Flecs.NET.Core;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Content;

namespace flecs_test;

enum RenderPhase;

public record struct RenderCtx(GraphicsDeviceManager Graphics, SpriteBatch SpriteBatch);
public record struct GameCtx(ContentManager Content);
public record struct Sprite(Texture2D Texture);

public struct Render : IFlecsModule
{
	public void InitModule(World world)
	{
		world.System<Transform, Sprite>()
			.Kind<RenderPhase>()
			.Iter(RenderSprites);
	}

	void RenderSprites(Iter it, Field<Transform> transform, Field<Sprite> sprite)
	{
		var batch = it.World().Get<RenderCtx>().SpriteBatch;
		batch.Begin();
		foreach (int i in it)
		{
			var t = transform[i];
			batch.Draw(sprite[i].Texture, t.Pos, null, Color.White, t.Rot, Vector2.Zero, t.Scale, SpriteEffects.None, 1);
		}
		batch.End();
	}
}