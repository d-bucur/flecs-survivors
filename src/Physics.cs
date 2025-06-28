using Flecs.NET.Core;
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading;
using MonoGame.Extended.Collections;
using System.Numerics;
using Raylib_cs;

namespace flecs_test;

enum PrePhysics;
enum OnPhysics;
enum PostPhysics;

enum Trigger;
record struct PhysicsBody(Vector2 Vel, Vector2 Accel, float BounceCoeff = 1, float DragCoeff = 0.9f);
record struct Collider(float Radius, CollisionFlags MyLayers = CollisionFlags.DEFAULT, CollisionFlags MaskLayers = CollisionFlags.ALL) {
    public Dictionary<Id, Vector2> collisionsLastFrame = [];
    public Dictionary<Id, Vector2> collisionsCurrentFrame = [];
}
record struct SpatialMap(float CellSize) {
    // Could try to profile HybridDictionary
    public ConcurrentDictionary<Vec2I, List<ulong>> Map = new(-1, 10);
    public Pool<List<ulong>> pool = new(
        () => new List<ulong>(3),
        (l) => l.Clear(),
        50
    );

    public readonly (int x, int y) PosToKey(Vector2 pos) {
        var x = (int)MathF.Floor(pos.X / CellSize);
        var y = (int)MathF.Floor(pos.Y / CellSize);
        return (x, y);
    }
}

// TODO API here is kinda weird
// Does a sphere cast query
record struct SpatialQuery() {
    float Radius = 0;
    Vector2 Center = Vector2.Zero;
    public List<ulong> Results = new(10);

    public void Prep(Vector2 Center, float Radius) {
        this.Radius = Radius;
        this.Center = Center;
        Results.Clear();
    }

    static readonly Vec2I[] queryNeighbors = [
        (-1, -1), (0, -1), (1, -1),
        (-1, 0), (0, 0), (1, 0),
        (-1, 1), (0, 1), (1, 1),
    ];
    public void Execute(SpatialMap map) {
        // TODO not actually considering sphere and radius
        var key = map.PosToKey(Center);

        foreach (var n in queryNeighbors) {
            var nk = key + n;
            map.Map.TryGetValue(nk, out var tempResults);
            if (tempResults is null) continue;
            Results.AddRange(tempResults);
        }
    }
}

// TODO add collision data like directions
record struct OnCollisionEnter(Entity Other, Vector2 Penetration);
record struct OnCollisionExit(Entity Other);
record struct OnCollisionStay(Entity Other);

record struct Heading(Vector2 Value);

[Flags]
public enum CollisionFlags {
    NONE = 0,
    DEFAULT = 1,
    PLAYER = 2,
    ENEMY = 4,
    PROJECTILE = 8,
    POWERUP = 16,
    SCENERY = 32,
    ALL = ~0,
}

class PhysicsModule : IFlecsModule {
    #region Init
    public void InitModule(World world) {
        world.Set(new SpatialMap(30));
        world.Set(new SpatialQuery());

        // TODO update to GlobalTransform
        world.System<Transform, PhysicsBody>("Integrate positions & velocities")
            .Kind<PrePhysics>()
            .MultiThreaded()
            .Each(IntegratePosition);

        world.System<SpatialMap>("Clear prev spatial map")
            .Kind<PrePhysics>()
            .Each((ref SpatialMap s) => {
                foreach (var (k, l) in s.Map)
                    s.pool.Free(l);
                s.Map.Clear();
            });

        world.System<Transform, SpatialMap>("Build new spatial map")
            .TermAt(1).Singleton()
            .With<PhysicsBody>()
            .With<Collider>()
            .Write<SpatialMap>()
            .Kind<PrePhysics>()
            .MultiThreaded()
            .Each(BuildSpatialHash);
        // Note: SpatialMap is only valid in between PrePhysics and PostPhysics where collion reponses
        // can destroy entities. Pretty weak implementation since spatial queries can return invalid entities
        // Maybe move it into Update so that entities are despawned only at this sync point
        // and spatial queries are valid during Update

        world.System("Handle collisions")
            .Kind<OnPhysics>()
            .Read<SpatialMap>()
            .MultiThreaded()
            .Immediate()
            .Run(HandleCollisions);

        world.System<Collider>("Emit collision events")
            .Kind<PostPhysics>()
            .MultiThreaded()
            .Each(EmitCollisionEvents);

        // Kind of useless? Just use velocity maybe?
        world.System<Heading, PhysicsBody>("Update headings")
            .Kind(Ecs.OnUpdate)
            .MultiThreaded()
            .Each(UpdateHeading);

        var debugId = world.System<GlobalTransform, PhysicsBody, Collider>("Debug colliders and physics")
            .Kind<RenderPhase>()
            .Iter(DebugColliders);
        world.System<DebugConfig>()
            .Kind(Ecs.OnStart)
            .Each((ref DebugConfig conf) => { conf.SystemIdColliders = debugId; debugId.Disable(); });
    }
    #endregion
    #region Systems

    private void EmitCollisionEvents(Entity e, ref Collider collider) {
        foreach (var current in collider.collisionsCurrentFrame.Keys) {
            if (collider.collisionsLastFrame.ContainsKey(current)) {
                e.Emit(new OnCollisionStay(e.CsWorld().GetAlive(current)));
                continue;
            }
            e.Emit(new OnCollisionEnter(e.CsWorld().GetAlive(current), collider.collisionsCurrentFrame[current]));
        }
        foreach (var last in collider.collisionsLastFrame.Keys) {
            // OnCollisionStay already emitted above
            if (collider.collisionsCurrentFrame.ContainsKey(last))
                continue;
            e.Emit(new OnCollisionExit(e.CsWorld().GetAlive(last)));
        }
        // Swap buffers
        var t = collider.collisionsLastFrame;
        t.Clear();
        collider.collisionsLastFrame = collider.collisionsCurrentFrame;
        collider.collisionsCurrentFrame = t;
    }

    private void UpdateHeading(ref Heading h, ref PhysicsBody b) {
        h.Value = b.Vel != Vector2.Zero ? b.Vel : h.Value;
        if (h.Value == Vector2.Zero) h.Value = Vector2.UnitX;
    }

    private void BuildSpatialHash(Entity e, ref Transform transform, ref SpatialMap map) {
        var key = map.PosToKey(transform.Pos);
        map.Map.TryGetValue(key, out List<ulong>? val);
        val ??= map.pool.Obtain();
        val.Add(e.Id.Value);
        map.Map[key] = val;
    }

    // Only check down and right. Avoids deadlocking on mutex since corners will always release
    static readonly Vec2I[] _neighbors = [
        (0, 0),(1, 0),
        (0, 1),(1, 1),
    ];

    // TODO update to GlobalTransforms. Need to propagate back to Transform
    private void HandleCollisions(Iter it) {
        // Does a multithreaded check for entities in each cell of the Spatial map and its neighbors.
        // Each cell is locked by a monitor when being iterated on to avoid race conditions
        // Unfortunately this means out of order iteration of entities, which does not improve that much
        // over the single threaded sequential iteration of all entities without spatial hashing
        // Maybe some way to iterate the entities in order?
        World world = it.World();
        var map = world.Get<SpatialMap>();

        var countdown = new CountdownEvent(map.Map.Count);

        foreach (var ((a, b), startCell) in map.Map) {
            ThreadPool.QueueUserWorkItem((cb) => {
                Monitor.Enter(startCell);
                foreach (var e1Id in startCell) {
                    var e1 = world.GetAlive(e1Id);
                    // out of order calls into flecs like this are slow
                    // a more efficient call to get multiple components is an open issue
                    // TODO use e1.Read();
                    ref var t1 = ref e1.GetMut<Transform>();
                    ref var b1 = ref e1.GetMut<PhysicsBody>();
                    ref var c1 = ref e1.GetMut<Collider>();

                    foreach (var (x, y) in _neighbors) {
                        // Console.WriteLine($"Checking collisions {startCellKey}: ({x}, {y})");
                        map.Map.TryGetValue((x + a, y + b), out List<ulong>? nearCell);
                        if (nearCell is null) continue;

                        bool areCellsDifferent = x != 0 || y != 0;
                        if (areCellsDifferent)
                            Monitor.Enter(nearCell);
                        foreach (var e2Id in nearCell) {
                            // Skip same entity check and symmetical ones in the same cell
                            if (e1Id == e2Id || (!areCellsDifferent && e1Id > e2Id))
                                continue;
                            var e2 = world.GetAlive(e2Id);

                            // check layer masks if can collide
                            ref var c2 = ref e2.GetMut<Collider>();
                            bool canE1Collide = (c1.MaskLayers & c2.MyLayers) != 0;
                            bool canE2Collide = (c2.MaskLayers & c1.MyLayers) != 0;
                            if (!canE1Collide && !canE2Collide)
                                continue;

                            // Calculate overlap vector
                            ref var t2 = ref e2.GetMut<Transform>();
                            var distance = t1.Pos - t2.Pos;
                            float distanceLen = distance.Length();
                            var separation = c1.Radius + c2.Radius;
                            var penetration = separation - distanceLen;
                            if (penetration <= 0)
                                continue;

                            // Register collision
                            var penetrationVec = distance / distanceLen * penetration;
                            if (canE1Collide) c1.collisionsCurrentFrame.Add(e2.Id, penetrationVec);
                            if (canE2Collide) c2.collisionsCurrentFrame.Add(e1.Id, -penetrationVec);
                            // Console.WriteLine($"Collision between {e1.Id} and {e2.Id}");

                            // Displace only if no triggers
                            if (e1.Has<Trigger>() || e2.Has<Trigger>())
                                continue;

                            distance = Vector2.Normalize(distance);
                            ref var b2 = ref e2.GetMut<PhysicsBody>();
                            // TODO if both 0 then div0
                            var totalBounce = b2.BounceCoeff + b1.BounceCoeff;
                            float b1Displacement = b1.BounceCoeff / totalBounce * penetration;
                            t1.Pos += distance * b1Displacement;
                            t2.Pos -= distance * (penetration - b1Displacement);
                        }
                        if (areCellsDifferent)
                            Monitor.Exit(nearCell);
                    }
                }
                Monitor.Exit(startCell);
                countdown.Signal();
            });
        }
        // Is WaitAll better? https://learn.microsoft.com/en-us/dotnet/api/system.threading.waithandle.waitall?view=net-9.0
        countdown.Wait();
    }

    private void IntegratePosition(Entity e, ref Transform transform, ref PhysicsBody body) {
        var dt = e.CsWorld().DeltaTime();
        body.Vel += body.Accel * dt;
        transform.Pos += body.Vel * dt;
        body.Vel *= body.DragCoeff;
    }

    private void DebugColliders(Iter it, Field<GlobalTransform> transform, Field<PhysicsBody> body, Field<Collider> collider) {
        var camera = it.World().Query<Camera>().First().Get<Camera>();
        Raylib.BeginMode2D(camera.Value);
        foreach (int i in it) {
            var hue = 0f;
            if (it.Entity(i).Has<Trigger>()) hue = 200f;
            Color color = HSV.Hsv(hue, 0.8f, 1f, 0.5f);
            Raylib.DrawCircleLinesV(transform[i].Pos, collider[i].Radius, color);
            Raylib.DrawLineV(transform[i].Pos, transform[i].Pos + body[i].Vel * 10, Color.Green);
        }
        Raylib.EndMode2D();
    }
    #endregion
}
