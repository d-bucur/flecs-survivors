using Microsoft.Xna.Framework;
using Flecs.NET.Core;
using Microsoft.Xna.Framework.Graphics;
using System;
using MonoGame.Extended;
using MonoGame.Extended.ViewportAdapters;

namespace flecs_test;

enum RenderPhase;

record struct Camera(OrthographicCamera Value)
{
	Matrix? centerTranslation;

	public Matrix GetTransformMatrix()
	{
		centerTranslation ??= Matrix.CreateTranslation(new Vector3(Value.Origin, 0));
		return Value.GetViewMatrix() * centerTranslation.Value;
	}
}
record struct RenderCtx(GraphicsDeviceManager Graphics, SpriteBatch SpriteBatch, GraphicsDevice GraphicsDevice, GameWindow Window);
struct Sprite(string Path)
{
	public string Path = Path;
	public Texture2D? Texture = null;
}

public struct Render : IFlecsModule
{
	public unsafe void InitModule(World world)
	{
		world.Observer<Sprite>()
			.Event(Ecs.OnSet)
			.Iter(LoadSprite);
		world.System<Camera, GlobalTransform, Transform>()
			.Kind(Ecs.PreUpdate)
			.Each(UpdateCameraTransform);
		world.System<GlobalTransform, Sprite>()
			.Kind<RenderPhase>()
			// monogame depth sorting is very finicky so do it here instead
			.OrderBy<GlobalTransform>(OrderSprites) 
			// flecs recommends rendering here. Not sure how to do that using monogame since Draw is separate
			// .Kind(Ecs.OnStore) 
			.Iter(RenderSprites);

		world.System()
			.With<Player>()
			.Kind(Ecs.OnStart)
			.Iter(InitCamera);
	}

	private unsafe int OrderSprites(ulong e1, void* t1, ulong e2, void* t2)
	{
		var p1 = ((GlobalTransform*)t1)->Pos.Y;
		var p2 = ((GlobalTransform*)t2)->Pos.Y;
		return (int)((p1 - p2) * 10);
	}

	static void InitCamera(Iter it)
	{
		var world = it.World();
		var renderCtx = world.Get<RenderCtx>();
		// TODO Use this when enabling window scaling
		var viewportAdapter = new BoxingViewportAdapter(renderCtx.Window, renderCtx.GraphicsDevice, 800, 480);
		var playerEntity = world.QueryBuilder<Transform>().With<Player>().Build().First();
		world.Entity("Camera")
			.Set(new Camera(new OrthographicCamera(viewportAdapter)))
			.Set(new Transform(Vector2.Zero, Vector2.One))
			.Set(new FollowTarget(playerEntity));
	}

	static void UpdateCameraTransform(ref Camera camera, ref GlobalTransform global, ref Transform t2)
	{
		camera.Value.Position = global.Pos;
	}

	static void LoadSprite(Iter it, Field<Sprite> sprite)
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
		var camera = it.World().Query<Camera>().First().Get<Camera>();
		var batch = it.World().Get<RenderCtx>().SpriteBatch;
		batch.Begin(transformMatrix: camera.GetTransformMatrix());

		foreach (int i in it)
		{
			var t = transform[i];
			// pivot to bottom center of texture
			// Texture is always set here. Ignore null
			var offset = new Vector2(-sprite[i].Texture!.Width / 2, -sprite[i].Texture!.Height) * transform[i].Scale;
			batch.Draw(sprite[i].Texture, t.Pos + offset, null, Color.White, t.Rot, Vector2.Zero, t.Scale, SpriteEffects.None, 0);
		}
		batch.End();
	}
}