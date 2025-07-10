using Flecs.NET.Core;

// To make systems only run when in a certain state, add the following to their definition:
// .Entity.DependsOn(GameState.Running)
struct GameState {
	// can probably use hierarchical states here by adding DependsOn relationship
	// would be better to use world singleton rather than static, but use sites would be more complicated
	public static Entity CurrentState;

	public static Entity Running;
	public static Entity LevelUp;

	// Weak param typing for newState
	public static void ChangeState(Entity newState) {
		CurrentState.Disable();
		CurrentState = newState;
		if (newState == LevelUp) {
			Timers.runningTimer.Stop();
			Timers.intervalTimer.Stop();
		}
		else if (newState == Running) {
			Timers.runningTimer.Start();
			Timers.intervalTimer.Start();
		}
	}
}

struct Timers {
	public static TimerEntity runningTimer;
	public static TimerEntity menuTimer;
	/// <summary>
	/// Use this for rate and intervals in game logic. Ticks every 100 ms
	/// </summary>
	public static TimerEntity intervalTimer;
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
		GameState.Running = world.Component();
		GameState.LevelUp = world.Component();
		GameState.LevelUp.Disable();
		GameState.CurrentState = GameState.Running;
	}
}

// Old code for states. Remove?
// class GameState {
//     public interface IGamestate;
//     public struct Running : IGamestate;
//     public struct Paused : IGamestate;
// }