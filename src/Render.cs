using Microsoft.Xna.Framework;
using Flecs.NET.Core;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Content;
using System;

namespace flecs_test;

enum RenderPhase;

public record struct RenderCtx(GraphicsDeviceManager Graphics, SpriteBatch SpriteBatch);
public record struct GameCtx(ContentManager Content);
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
		world.System<Transform, Sprite>()
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