using Microsoft.Xna.Framework;
using Flecs.NET.Core;
using MonoGame.Extended;
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading;
using MonoGame.Extended.Collections;

namespace flecs_test;

enum Trigger;
record struct PhysicsBody(Vector2 Vel, Vector2 Accel, float BounceCoeff = 1, float DragCoeff = 0.9f);
record struct Collider(float Radius, CollisionFlags MyLayers = CollisionFlags.DEFAULT, CollisionFlags MaskLayers = CollisionFlags.ALL) {
    public HashSet<Id> collisionsLastFrame = [];
    public HashSet<Id> collisionsCurrentFrame = [];
}
record struct SpatialMap(float CellSize) {
    // Could try to profile HybridDictionary
    public ConcurrentDictionary<(int, int), List<ulong>> Map = new(-1, 10);
    public Pool<List<ulong>> pool = new(
        () => new List<ulong>(3),
        (l) => l.Clear(),
        50
    );
}

// TODO add collision data like directions
record struct OnCollisionEnter(Entity Other);
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
    public void InitModule(World world) {
        world.Set(new SpatialMap(30));

        // TODO update to GlobalTransform
        world.System<Transform, PhysicsBody>()
            .Kind(Ecs.OnUpdate)
            .MultiThreaded()
            .Each(IntegratePosition);

        world.System<SpatialMap>()
            .Kind(Ecs.OnUpdate)
            .Each((ref SpatialMap s) => {
                foreach (var (k, l) in s.Map)
                    s.pool.Free(l);
                s.Map.Clear();
            });

        world.System<Transform, SpatialMap>()
            .TermAt(1).Singleton()
            .With<PhysicsBody>()
            .With<Collider>()
            .Write<SpatialMap>()
            .Kind(Ecs.OnUpdate)
            .MultiThreaded()
            .Each(BuildSpatialHash);

        world.System()
            .Kind(Ecs.OnUpdate)
            .Read<SpatialMap>()
            .MultiThreaded()
            .Immediate()
            .Run(HandleCollisions);

        world.System<Collider>()
            .Kind(Ecs.OnUpdate)
            .MultiThreaded()
            .Each(EmitCollisionEvents);

        world.System<Heading, PhysicsBody>()
            .Kind(Ecs.OnUpdate)
            .MultiThreaded()
            .Each(UpdateHeading);

        world.System<GlobalTransform, PhysicsBody, Collider>()
            .Kind<RenderPhase>()
            // .Kind(Ecs.Disabled)
            .Iter(DebugColliders);
    }

    private void EmitCollisionEvents(Entity e, ref Collider collider) {
        foreach (var current in collider.collisionsCurrentFrame) {
            if (collider.collisionsLastFrame.Contains(current)) {
                e.Emit(new OnCollisionStay(e.CsWorld().GetAlive(current)));
                continue;
            }
            // TODO need check for null id here?
            e.Emit(new OnCollisionEnter(e.CsWorld().GetAlive(current)));
        }
        foreach (var last in collider.collisionsLastFrame) {
            // OnCollisionStay already emitted above
            if (collider.collisionsCurrentFrame.Contains(last))
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
        var x = (int)(transform.Pos.X / map.CellSize);
        var y = (int)(transform.Pos.Y / map.CellSize);
        var key = (x, y);
        map.Map.TryGetValue(key, out List<ulong>? val);
        val ??= map.pool.Obtain();
        val.Add(e.Id.Value);
        map.Map[key] = val;
    }

    // Only check down and right. Avoids deadlocking on mutex since corners will always release
    readonly (int, int)[] _neighbors = [
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
                            var separation = c1.Radius + c2.Radius;
                            var penetration = separation - distance.Length();
                            if (penetration <= 0)
                                continue;

                            // Register collision
                            if (canE1Collide) c1.collisionsCurrentFrame.Add(e2.Id);
                            if (canE2Collide) c2.collisionsCurrentFrame.Add(e1.Id);
                            // Console.WriteLine($"Collision between {e1.Id} and {e2.Id}");

                            // Displace only if no triggers
                            if (e1.Has<Trigger>() || e2.Has<Trigger>())
                                continue;

                            distance.Normalize();
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
        // Could use e.CsWorld().DeltaTime() to support variable time scale
        body.Vel += body.Accel;
        transform.Pos += body.Vel;
        // should probably do drag in a different step
        body.Vel *= body.DragCoeff;
    }

    private void DebugColliders(Iter it, Field<GlobalTransform> transform, Field<PhysicsBody> body, Field<Collider> collider) {
        var camera = it.World().Query<Camera>().First().Get<Camera>();
        var batch = it.World().Get<RenderCtx>().SpriteBatch;
        batch.Begin(transformMatrix: camera.GetTransformMatrix());
        foreach (int i in it) {
            var hue = 0f;
            if (it.Entity(i).Has<Trigger>()) hue = 200f;
            Color color = new(new HslColor(hue, 0.8f, 0.5f).ToRgb(), 0.5f);
            batch.DrawCircle(transform[i].Pos, collider[i].Radius, 10, color);
            batch.DrawLine(transform[i].Pos, transform[i].Pos + body[i].Vel * 10, Color.Green);
        }
        batch.End();
    }
}
