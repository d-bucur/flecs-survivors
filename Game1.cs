using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Flecs.NET.Core;
using Microsoft.Xna.Framework.Content;

namespace flecs_test;

public record struct GameCtx(ContentManager Content);

public class Game1 : Game
{
    private GraphicsDeviceManager _graphics;
    private World _world;
    private Pipeline _renderPipeline;

    public Game1()
    {
        _graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
        IsMouseVisible = true;

        _world = World.Create();

        _renderPipeline = _world.Pipeline()
            .With(Ecs.System)
            .With<RenderPhase>()
            .Build();
    }

    protected override void Initialize()
    {
        base.Initialize();

        SpriteBatch _spriteBatch = new(GraphicsDevice);
        _world.Set(new RenderCtx(_graphics, _spriteBatch, GraphicsDevice));
        _world.Set(new GameCtx(Content));

        _world.Import<TransformsModule>();
        _world.Import<Render>();
        _world.Import<Main>();
        _world.Import<PhysicsModule>();
        _world.Import<PlayerModule>();
    }

    protected override void LoadContent()
    {
    }

    protected override void Update(GameTime gameTime)
    {
        if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || Keyboard.GetState().IsKeyDown(Keys.Escape))
            Exit();

        _world.Progress((float)gameTime.ElapsedGameTime.TotalMilliseconds);

        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Color.CornflowerBlue);

        _world.RunPipeline(_renderPipeline, (float)gameTime.ElapsedGameTime.TotalMilliseconds);

        base.Draw(gameTime);
    }
}
