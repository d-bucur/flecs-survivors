using System;
using System.Collections.Generic;
using Flecs.NET.Core;
using flecs_survivors;

record struct OnStateEntered(Entity OldState);
record struct OnStateLeft(Entity NewState);

// Common tag for states in GameState. Not really used right now
enum GameStateTag;
// Would be nice to declare a top level state where an entity exists instead of this
/// <summary>
/// All tagged with this are destroyed when exiting game
/// </summary>
enum InGameEntity;

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

// To make systems only run when in a certain state, add the following to their definition:
// .Entity.DependsOn(GameState.Running)
class GameState : IFlecsModule {
	// Could refactor State machine logic into something more generic so it's reusable
	// can probably use hierarchical states here by adding DependsOn relationship
	// would be better to use world singleton rather than static, but use sites would be more complicated
	public static Entity CurrentState;
	/// <summary>
	/// Main menu screen
	/// </summary>
	public static Entity MainMenu;
	/// <summary>
	/// Temporary state in which we destroy the previous level and move into InitGame when done
	/// </summary>
	public static Entity PreInitGame;
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

	public void InitModule(World world) {
		InitGameState(world);
		InitTimers(world);
	}

	/// <summary>
	/// Weak typing for newState. Plz send one of the types above so it doesn't bork
	/// </summary>
	public static void ChangeState(Entity newState) {
		if (!newState.Has<GameStateTag>())
			throw new ArgumentException($"Invalid state: {newState}. Should be an entity marked with GameStateTag");
		Console.WriteLine($"Changing state to {newState}");
		var oldState = CurrentState;
		CurrentState = newState;

		if (newState == Running) {
			Timers.runningTimer.Start();
			Timers.intervalTimer.Start();
		}
		else {
			Timers.runningTimer.Stop();
			Timers.intervalTimer.Stop();
		}

		oldState.Enqueue(new OnStateLeft(newState));
		oldState.Disable();
		newState.Enable();
		newState.Enqueue(new OnStateEntered(oldState));
	}

	internal static void StartGame() {
		CurrentState = GameOver; // any state other than the first one, just for the init
		AllStates = [MainMenu, PreInitGame, InitGame, Running, LevelUp, GameOver];
		AllStates.ForEach(s => s.Disable());
		ChangeState(MainMenu);
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
		MainMenu = world.Component("MainMenuState").Add<GameStateTag>();
		PreInitGame = world.Component("PreInitGameState").Add<GameStateTag>()
			.Entity.Observe((Entity _, ref OnStateEntered e) => {
				ClearGameEntities();
				ChangeState(InitGame);
			});
		InitGame = world.Component("InitGameState").Add<GameStateTag>()
			.Entity.Observe((Entity _, ref OnStateEntered e) => {
				ChangeState(Running);
			});
		Running = world.Component("RunningState").Add<GameStateTag>();
		LevelUp = world.Component("LevelUpState").Add<GameStateTag>();
		GameOver = world.Component("GameOverState").Add<GameStateTag>();
	}

	// An alternative would be to parent all level entities to a single root entity and then destroy that.
	// Would be more similar to hierarchies in engines. Not sure which is better
	internal static void ClearGameEntities() => CachedQueries.inGameEntities.Each((Entity e, ref InGameEntity _) => e.Destruct());
}