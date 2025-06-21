using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Flecs.NET.Core;
using Microsoft.Xna.Framework.Content;
using MonoGame.Extended.Input;
using System.Threading;
using System;

namespace flecs_test;

record struct GameCtx(ContentManager Content);

public class Game1 : Game {
    GraphicsDeviceManager _graphics;
    World _world;
    Pipeline _renderPipeline;
    SpriteBatch? _spriteBatch;

    public Game1() {
        // TODO resizing not working properly
        Window.AllowUserResizing = true;
        _graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
        IsMouseVisible = true;

        _world = World.Create();

        _renderPipeline = _world.Pipeline()
            .With(Ecs.System)
            .With<RenderPhase>()
            .Build();
    }

    protected override void Initialize() {
        base.Initialize();

        _spriteBatch = new(GraphicsDevice);
        _world.Set(new RenderCtx(_graphics, _spriteBatch, GraphicsDevice, Window));
        _world.Set(new GameCtx(Content));
        // _world.SetThreads(Environment.ProcessorCount);
        // _world.SetTaskThreads(Environment.ProcessorCount);

        _world.Import<TransformsModule>();
        _world.Import<Render>();
        _world.Import<Main>();
        _world.Import<PhysicsModule>();
        _world.Import<PlayerModule>();
        _world.Import<EnemiesModule>();
        // Ecs.Log.SetLevel(1);
    }

    protected override void LoadContent() {
    }

    protected override void Update(GameTime gameTime) {
        if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || Keyboard.GetState().IsKeyDown(Keys.Escape))
            Exit();
        KeyboardExtended.Update();
        MouseExtended.Update();

        _world.Progress((float)gameTime.ElapsedGameTime.TotalMilliseconds);

        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime) {
        GraphicsDevice.Clear(Color.CornflowerBlue);

        _world.RunPipeline(_renderPipeline, (float)gameTime.ElapsedGameTime.TotalMilliseconds);

        base.Draw(gameTime);
    }
}
