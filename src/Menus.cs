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
	private enum MainMenu;

	public void InitModule(World world) {
		GameState.MainMenu.Observe<OnStateEntered>(() => EnterMenu(ref world));
		GameState.MainMenu.Observe<OnStateLeft>(() => LeaveMenu(ref world));

		world.System<MenuSelectable, InputManager>()
			.TermAt(1).Singleton()
			.Kind(Ecs.PostLoad)
			.Each(HandleMenuSelection);

		world.System<MenuSelectable, InputManager>()
			.TermAt(1).Singleton()
			.Kind(Ecs.PostLoad)
			.Each(HandleMenuProgression);

		world.System<RenderCtx, MenuSelectable>()
			.With<MainMenu>()
			.TermAt(0).Singleton()
			.Kind<PostRenderPhase>()
			.Each(DrawMenu)
			.Entity.DependsOn(GameState.MainMenu);
	}

	private void HandleMenuSelection(ref MenuSelectable selectable, ref InputManager actions) {
		if (actions.IsPressed(InputActions.UP)) selectable.CurrentValue--;
		if (actions.IsPressed(InputActions.DOWN)) selectable.CurrentValue++;
	}

	private void HandleMenuProgression(ref MenuSelectable selectable, ref InputManager actions) {
		if (actions.IsPressed(InputActions.CONFIRM)) {
			if (selectable.CurrentValue == 0) {
				GameState.ChangeState(GameState.InitGame);
			}
		}
	}

	const int COUNT_CHOICES_MAIN_MENU = 3;
	private static void EnterMenu(ref World world) {
		world.Entity()
			.Add<MainMenu>()
			.Set(new MenuSelectable { TotalValues = COUNT_CHOICES_MAIN_MENU });
	}

	private static void LeaveMenu(ref World world) {
		world.QueryBuilder().With<MainMenu>().Build().Each(e => e.Destruct());
	}

	private Color?[] _colors = new Color?[COUNT_CHOICES_MAIN_MENU];
	private void DrawMenu(ref RenderCtx ctx, ref MenuSelectable m) {
		int width = ctx.WinSize.X / 3;
		int height = 50;
		var offset = new Vector2(width, 100);
		const float space = 70;
		for (int i = 0; i < _colors.Length; i++) {
			_colors[i] = m.CurrentValue == i ? Color.Red : null;
		}
		DrawMenuButton("Play", new Rectangle(offset.X, offset.Y, width, height), _colors[0]);
		DrawMenuButton("Credits", new Rectangle(offset.X, offset.Y + space, width, height), _colors[1]);
		DrawMenuButton("Exit", new Rectangle(offset.X, offset.Y + space * 2, width, height), _colors[2]);
	}

	private static void DrawMenuButton(string text, Rectangle rect, Color? color = null) {
		float borderThick = 3;
		Raylib.DrawRectangleRec(rect, Raylib.Fade(color ?? Color.DarkGray, 0.7f));
		Raylib.DrawRectangleLinesEx(rect, borderThick, Raylib.Fade(Color.Black, 0.3f));
		Render.DrawTextShadowed(text, (int)(rect.X + 30), (int)(rect.Y + (rect.Height - 18) / 2));
	}
}