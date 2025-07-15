using System.Collections.Generic;
using Raylib_cs;

namespace flecs_survivors;

public enum InputActions {
	DOWN,
	UP,
	LEFT,
	RIGHT,
	CONFIRM,
	BACK,
}

// TODO replace all key reads with this
// TODO add axis like in Godot
public struct InputManager() {
	// Default key mappings
	public Dictionary<InputActions, KeyboardKey[]> ActionMappings = new() {
		{ InputActions.DOWN,
			[KeyboardKey.Down, KeyboardKey.S] },
		{ InputActions.UP,
			[KeyboardKey.Up, KeyboardKey.W] },
		{ InputActions.LEFT,
			[KeyboardKey.Left, KeyboardKey.A] },
		{ InputActions.RIGHT,
			[KeyboardKey.Right, KeyboardKey.D] },
		{ InputActions.CONFIRM,
			[KeyboardKey.Enter, KeyboardKey.Space] },
		{ InputActions.BACK,
			[KeyboardKey.Escape, KeyboardKey.Backspace] },
	};

	public bool IsPressed(InputActions action) {
		foreach (var key in ActionMappings[action]) {
			if (Raylib.IsKeyPressed(key)) return true;
		}
		return false;
	}

	public bool IsDown(InputActions action) {
		foreach (var key in ActionMappings[action]) {
			if (Raylib.IsKeyDown(key)) return true;
		}
		return false;
	}
}