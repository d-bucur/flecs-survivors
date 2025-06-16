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

		world.System<Transform>()
			.Without(Ecs.ChildOf)
			.Each(CreateRootGlobals);
		world.System<Transform, GlobalTransform>()
			.TermAt(1).Parent().Cascade()
			.Each(PropagateTransforms);
	}

	private void PropagateTransforms(Entity e, ref Transform transform, ref GlobalTransform parent)
	{
		GlobalTransform global = parent.Apply(transform);
		e.Set(global);
		// Console.WriteLine($"Set GlobalTransform for {e}: {global}");
	}

	private void CreateRootGlobals(Entity e, ref Transform t0)
	{
		e.Set(GlobalTransform.from(t0));
		// Console.WriteLine($"Added Global to {e}");
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
			// depth not working
			var layerDepth = t.Pos.Y / 1000; // TODO find better way to make this between (0..1)
			batch.Draw(sprite[i].Texture, t.Pos, null, Color.White, t.Rot, Vector2.Zero, t.Scale, SpriteEffects.None, layerDepth);
		}
		batch.End();
	}
}