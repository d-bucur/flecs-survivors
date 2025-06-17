using Microsoft.Xna.Framework;
using Flecs.NET.Core;
using Microsoft.Xna.Framework.Graphics;
using System;
using MonoGame.Extended;
using MonoGame.Extended.ViewportAdapters;

namespace flecs_test;

enum RenderPhase;

record struct Camera(OrthographicCamera Value);
record struct RenderCtx(GraphicsDeviceManager Graphics, SpriteBatch SpriteBatch, GraphicsDevice GraphicsDevice, GameWindow Window);
struct Sprite(string Path)
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
		world.System<Camera, GlobalTransform, Transform>()
			.Kind(Ecs.PreUpdate)
			.Each(UpdateCameraTransform);
		world.System<GlobalTransform, Sprite>()
			.Kind<RenderPhase>()
			.Iter(RenderSprites);

		world.System()
			.With<Player>()
			.Kind(Ecs.OnStart)
			.Iter(InitCamera);
	}

	static void InitCamera(Iter it)
	{
		var world = it.World();
		var renderCtx = world.Get<RenderCtx>();
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
		OrthographicCamera camera = it.World().Query<Camera>().First().Get<Camera>().Value;
		var centerTranslation = Matrix.CreateTranslation(camera.BoundingRectangle.Width / 2, camera.BoundingRectangle.Height / 2, 0);
		var batch = it.World().Get<RenderCtx>().SpriteBatch;
		batch.Begin(transformMatrix: camera.GetViewMatrix() * centerTranslation);

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