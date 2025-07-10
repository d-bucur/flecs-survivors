using Flecs.NET.Core;

namespace flecs_survivors;

public struct CachedQueries : IFlecsModule {
	internal static Query<Camera> camera;
	internal static Query<Health> playerHealth;
	internal static Query<PowerCollector> playerPower;

	public unsafe void InitModule(World world) {
		camera = world.QueryBuilder<Camera>().Cached().Build();
		playerHealth = world.QueryBuilder<Health>().With<Player>().Cached().Build();
		playerPower = world.QueryBuilder<PowerCollector>().With<Player>().Cached().Build();
	}
}