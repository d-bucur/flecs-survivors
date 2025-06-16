using Microsoft.Xna.Framework;
using Flecs.NET.Core;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace flecs_test;

enum RenderPhase;

public record struct RenderCtx(GraphicsDeviceManager Graphics, SpriteBatch SpriteBatch, GraphicsDevice GraphicsDevice);
public struct Sprite(string Path)
{
	public string Path = Path;
	public Texture2D Texture = null;
}

public struct Render : IFlecsModule
{
	public void InitModule(World world)
	{
		world.Observer<Sprite>()
			.Event(Ecs.OnSet)
			.Iter(LoadSprite);
		world.System<GlobalTransform, Sprite>()
			.Kind<RenderPhase>()
			.Iter(RenderSprites);
	}

	private void LoadSprite(Iter it, Field<Sprite> sprite)
	{
		foreach (int i in it)
		{
			if (sprite[i].Texture is null)
			{
				sprite[i].Texture = it.World().Get<GameCtx>().Content.Load<Texture2D>(sprite[i].Path);
			}
		}
	}

	void RenderSprites(Iter it, Field<GlobalTransform> transform, Field<Sprite> sprite)
	{
		var batch = it.World().Get<RenderCtx>().SpriteBatch;
		batch.Begin();
		foreach (int i in it)
		{
			var t = transform[i];
			// pivot to bottom center of texture
			var offset = new Vector2(-sprite[i].Texture.Width / 2, -sprite[i].Texture.Height) * transform[i].Scale;
			// TODO depth not working
			var layerDepth = t.Pos.Y / 1000; // find better way to make this between (0..1)
			batch.Draw(sprite[i].Texture, t.Pos + offset, null, Color.White, t.Rot, Vector2.Zero, t.Scale, SpriteEffects.None, layerDepth);
		}
		batch.End();
	}
}