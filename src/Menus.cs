using System;
using System.Numerics;
using Flecs.NET.Core;
using Raylib_cs;

namespace flecs_survivors;

// TODO use in powerup selections as well
struct MenuSelectable {
	private int _current;
	public int CurrentValue {
		readonly get => _current; set {
			_current = Helpers.Modulo(value, TotalValues);
		}
	}
	required public int TotalValues;
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
class MenuBase<MenuTag>(
	Entity State,
	string[] Choices
) where MenuTag : Enum {
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

	protected virtual void HandleMenuTransition(ref MenuSelectable selectable, ref InputManager actions) {
	}

	protected virtual void EnterMenu(ref World world) {
		Console.WriteLine($"Creating Menu {typeof(MenuTag)}");
		world.Entity()
			.Add<MenuTag>()
			.Set(new MenuSelectable { TotalValues = Choices.Length });
	}

	protected virtual void LeaveMenu(ref World world) {
		Console.WriteLine($"Destoying Menu {typeof(MenuTag)}");
		world.QueryBuilder().With<MenuTag>().Build().Each(e => e.Destruct());
	}

	protected virtual void DrawMenu(ref RenderCtx ctx, ref MenuSelectable m) {
		int width = ctx.WinSize.X / 3;
		int height = 50;
		var offset = new Vector2(width, 100);
		const float space = 70;
		for (int i = 0; i < Choices.Length; i++) {
			Color? color = m.CurrentValue == i ? Color.Red : null;
			MenusModule.DrawMenuButton(Choices[i], new Rectangle(offset.X, offset.Y + space * i, width, height), color);
		}
	}
}

enum MainMenuTag;
file class MainMenu : MenuBase<MainMenuTag> {
	public MainMenu() : base(GameState.MainMenu, ["Play", "Credits", "Exit"]) {
	}

	override protected void HandleMenuTransition(ref MenuSelectable selectable, ref InputManager actions) {
		if (actions.IsPressed(InputActions.CONFIRM)) {
			if (selectable.CurrentValue == 0) {
				GameState.ChangeState(GameState.InitGame);
			}
		}
	}
}

enum GameOverTag;
file class GameOverMenu : MenuBase<GameOverTag> {
	public GameOverMenu() : base(GameState.GameOver, ["Restart", "Menu", "Exit"]) {
	}

	override protected void HandleMenuTransition(ref MenuSelectable selectable, ref InputManager actions) {
		if (actions.IsPressed(InputActions.CONFIRM)) {
			if (selectable.CurrentValue == 0) {
				GameState.ChangeState(GameState.InitGame);
			}
			if (selectable.CurrentValue == 1) {
				GameState.ChangeState(GameState.MainMenu);
			}
		}
	}
}