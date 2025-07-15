using System;
using System.Collections.Generic;
using Flecs.NET.Core;
using flecs_survivors;

// Common tag for the states in GameState
enum GameStateComp;
enum InitGameEvent;
record struct OnStateChange(Entity OldState);

// Would be nice to declare a top level state where an entity exists instead of this
/// <summary>
/// All tagged with this are destroyed when exiting game
/// </summary>
enum InGameEntity;

// To make systems only run when in a certain state, add the following to their definition:
// .Entity.DependsOn(GameState.Running)
struct GameState {
	// can probably use hierarchical states here by adding DependsOn relationship
	// would be better to use world singleton rather than static, but use sites would be more complicated
	public static Entity CurrentState;

	/// <summary>
	/// Main menu screen
	/// </summary>
	public static Entity MainMenu;
	/// <summary>
	/// Temporary state in which we initialize the level and move into Running when done
	/// </summary>
	public static Entity InitGame;
	/// <summary>
	/// Game running inside the level normally
	/// </summary>
	public static Entity Running;
	/// <summary>
	/// Game is paused for level up selection screen
	/// </summary>
	public static Entity LevelUp;
	/// <summary>
	/// Game is paused for game over screen
	/// </summary>
	public static Entity GameOver;
	
	private static List<Entity> AllStates = [];

	/// <summary>
	/// Weak typing for newState. Plz send one of the types above so it doesn't bork
	/// </summary>
	public static void ChangeState(Entity newState) {
		Console.WriteLine($"Changing state to {newState}");
		var oldState = CurrentState;
		CurrentState.Disable();
		newState.Enable();
		CurrentState = newState;

		if (newState == LevelUp || newState == MainMenu || newState == GameOver) {
			Timers.runningTimer.Stop();
			Timers.intervalTimer.Stop();
		}
		else if (newState == Running) {
			Timers.runningTimer.Start();
			Timers.intervalTimer.Start();
		}
		CurrentState.Enqueue(new OnStateChange(oldState));
	}

	internal static void StartGame() {
		CurrentState = InitGame;
		AllStates = [MainMenu, InitGame, Running, LevelUp, GameOver];
		AllStates.ForEach((s) => { if (s != CurrentState) s.Disable(); });
		ChangeState(InitGame);
	}
}

struct Timers {
	/// <summary>
	/// Use for game logic that updates continously, like physics
	/// </summary>
	public static TimerEntity runningTimer;
	/// <summary>
	/// Use for rate and intervals in game logic, like spawning enemies. Ticks every 100 ms
	/// </summary>
	public static TimerEntity intervalTimer;
	/// <summary>
	/// Use for things in menus like animations, that can run indipendent of the game sim
	/// </summary>
	public static TimerEntity menuTimer;
}

class GameStateModule : IFlecsModule {
	public void InitModule(World world) {
		InitGameState(world);
		InitTimers(world);
	}

	private static void InitTimers(World world) {
		Timers.runningTimer = world.Timer();
		Timers.runningTimer.Start();
		Timers.menuTimer = world.Timer();
		Timers.menuTimer.Start();
		Timers.intervalTimer = world.Timer().Interval(100f);
		Timers.intervalTimer.Start();
	}

	private static void InitGameState(World world) {
		GameState.MainMenu = world.Component("MainMenuState").Add<GameStateComp>();
		GameState.InitGame = world.Component("InitGameState").Add<GameStateComp>()
			.Entity.Observe((Entity _, ref OnStateChange e) => {
				ClearGameEntities();
				GameState.InitGame.Enqueue(new InitGameEvent());
				GameState.ChangeState(GameState.Running);
			});
		GameState.Running = world.Component("RunningState").Add<GameStateComp>();
		GameState.LevelUp = world.Component("LevelUpState").Add<GameStateComp>();
		GameState.GameOver = world.Component("GameOverState").Add<GameStateComp>();
	}

	// An alternative would be to parent all level entities to a single root entity and then destroy that.
	// Would be more similar to hierarchies in engines. Not sure which is better
	internal static void ClearGameEntities() => CachedQueries.inGameEntities.Each((Entity e, ref InGameEntity _) => e.Destruct());
}

// Old code for states. Remove?
// class GameState {
//     public interface IGamestate;
//     public struct Running : IGamestate;
//     public struct Paused : IGamestate;
// }