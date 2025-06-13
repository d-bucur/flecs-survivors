using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Flecs.NET.Core;

namespace flecs_test;

public record struct Transform(Vector2 Pos, Vector2 Scale, float Rot);
public record struct PhysicsBody(Vector2 Vel, Vector2 Accel);

public class Game1 : Game
{
    private GraphicsDeviceManager _graphics;
    private SpriteBatch _spriteBatch;
    private World _world;
    private Texture2D _sprite;

    public Game1()
    {
        _graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
        IsMouseVisible = true;

        _world = World.Create();
    }

    protected override void Initialize()
    {
        base.Initialize();
        Entity entity = _world.Entity()
            .Set(new Transform(new Vector2(10, 20), new Vector2(0.5f, 0.5f), 0))
            .Set(new PhysicsBody(new Vector2(1, 1), Vector2.Zero));
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _sprite = Content.Load<Texture2D>("sprites/investor2");
    }

    protected override void Update(GameTime gameTime)
    {
        if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || Keyboard.GetState().IsKeyDown(Keys.Escape))
            Exit();

        _world.Each(static (ref Transform p, ref PhysicsBody v) =>
        {
            p.Pos += v.Vel;
        });

        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Color.CornflowerBlue);

        // Begin the sprite batch to prepare for rendering.
        _spriteBatch.Begin();

        _world.Each((ref Transform t) =>
        {
            _spriteBatch.Draw(_sprite, t.Pos, null, Color.White, t.Rot, Vector2.Zero, t.Scale, SpriteEffects.None, 1);
        });
        // Always end the sprite batch when finished.
        _spriteBatch.End();
        base.Draw(gameTime);
    }
}
