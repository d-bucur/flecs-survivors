using System;
using System.Numerics;
using Flecs.NET.Core;
using Raylib_cs;

namespace flecs_survivors;

struct MenuSelectable() {
	private int _current;
	public int CurrentValue {
		readonly get => _current; set {
			_current = Helpers.Modulo(value, TotalValues);
		}
	}
	required public int TotalValues;
	required public Rectangle[] ChoicePositions;
	public bool Enabled = true;
}

class MenusModule : IFlecsModule {
	public void InitModule(World world) {
		// Init every sub menu
		new MainMenu().Init(world);
		new GameOverMenu().Init(world);

		world.System<MenuSelectable, InputManager>()
			.TermAt(1).Singleton()
			.Kind(Ecs.PostLoad)
			.Each(HandleMenuSelection);
	}

	private void HandleMenuSelection(ref MenuSelectable selectable, ref InputManager actions) {
		if (!selectable.Enabled) return;
		if (actions.IsPressed(InputActions.UP)) selectable.CurrentValue--;
		if (actions.IsPressed(InputActions.DOWN)) selectable.CurrentValue++;
	}

	public static void DrawMenuButton(string text, Rectangle rect, Color? color = null) {
		float borderThick = 3;
		Raylib.DrawRectangleRec(rect, Raylib.Fade(color ?? Color.DarkGray, 0.7f));
		Raylib.DrawRectangleLinesEx(rect, borderThick, Raylib.Fade(Color.Black, 0.3f));
		Render.DrawTextShadowed(text, (int)(rect.X + 30), (int)(rect.Y + (rect.Height - 18) / 2));
	}
}

// Every menu needs its separate MenuTag (an enum) so it's registered on the ECS
class MenuBase<MenuTag> where MenuTag : Enum {
	protected Entity State;
	protected string[] Choices;
	protected int ButtonHeight = 50;

	public MenuBase(Entity state, string[] choices) {
		State = state;
		Choices = choices;
	}

	public void Init(World world) {
		State.Observe<OnStateEntered>(() => EnterMenu(ref world));
		State.Observe<OnStateLeft>(() => LeaveMenu(ref world));

		world.System<MenuSelectable, InputManager>()
			.With<MenuTag>()
			.TermAt(1).Singleton()
			.Kind(Ecs.PostLoad)
			.Each(HandleMenuTransition);

		world.System<RenderCtx, MenuSelectable>()
			.With<MenuTag>()
			.TermAt(0).Singleton()
			.Kind<PostRenderPhase>()
			.Each(DrawMenu)
			.Entity.DependsOn(State);
	}

	protected virtual void HandleMenuTransition(Entity e, ref MenuSelectable selectable, ref InputManager actions) {
	}

	protected virtual void EnterMenu(ref World world) {
		// Console.WriteLine($"Creating Menu {typeof(MenuTag)}");
		var selectable = world.Entity()
			.Add<MenuTag>()
			.Set(new MenuSelectable {
				TotalValues = Choices.Length,
				ChoicePositions = new Rectangle[Choices.Length],
				Enabled = false,
			});

		new Tween(selectable).With(
			(ref MenuSelectable s, float v) => {
				for (int i = 0; i < s.ChoicePositions.Length; i++)
					s.ChoicePositions[i].X = v * ((i % 2 == 0) ? -1 : 1);
			}, 1000, 0,
			1000f, Ease.SineOut,
			(ref MenuSelectable c) => { c.Enabled = true; }
		).RegisterEcs();
	}

	protected virtual void LeaveMenu(ref World world) {
		Console.WriteLine($"Destoying Menu {typeof(MenuTag)}");
		world.QueryBuilder().With<MenuTag>().Build().Each(e => e.Destruct());
	}

	protected virtual void DrawMenu(ref RenderCtx ctx, ref MenuSelectable m) {
		int width = ctx.WinSize.X / 3;
		var offset = new Vector2(width, 100);
		float space = ButtonHeight + 20;
		for (int i = 0; i < Choices.Length; i++) {
			Color? color = m.CurrentValue == i ? Color.Red : null;
			MenusModule.DrawMenuButton(Choices[i], new Rectangle(
				offset.X + m.ChoicePositions[i].X,
				offset.Y + space * i + m.ChoicePositions[i].Y,
				width + m.ChoicePositions[i].Width,
				ButtonHeight + m.ChoicePositions[i].Height),
				color
			);
		}
	}
}

enum MainMenuTag;
file class MainMenu : MenuBase<MainMenuTag> {
	public MainMenu() : base(GameState.MainMenu, ["Play", "Credits", "Exit"]) {
	}

	override protected void HandleMenuTransition(Entity _, ref MenuSelectable selectable, ref InputManager actions) {
		if (actions.IsPressed(InputActions.CONFIRM)) {
			if (selectable.CurrentValue == 0) {
				GameState.ChangeState(GameState.PreInitGame);
			}
		}
	}
}

enum GameOverTag;
file class GameOverMenu : MenuBase<GameOverTag> {
	public GameOverMenu() : base(GameState.GameOver, ["Restart", "Menu", "Exit"]) { }

	override protected void HandleMenuTransition(Entity _, ref MenuSelectable selectable, ref InputManager actions) {
		if (actions.IsPressed(InputActions.CONFIRM)) {
			if (selectable.CurrentValue == 0) {
				GameState.ChangeState(GameState.PreInitGame);
			}
			else if (selectable.CurrentValue == 1) {
				GameState.ChangeState(GameState.MainMenu);
			}
		}
	}
}