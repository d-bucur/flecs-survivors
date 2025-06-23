using System;
using Flecs.NET.Core;
using Microsoft.Xna.Framework;

namespace flecs_test;

record struct Transform(Vector2 Pos, Vector2 Scale, float Rot = 0);
record struct GlobalTransform(Vector2 Pos, Vector2 Scale, float Rot) {
    public static GlobalTransform from(Transform t) {
        return new GlobalTransform(t.Pos, t.Scale, t.Rot);
    }

    internal GlobalTransform Apply(Transform other) {
        return new GlobalTransform(other.Pos + this.Pos, other.Scale * this.Scale, other.Rot + this.Rot);
    }
}

class TransformsModule : IFlecsModule {
    public void InitModule(World world) {
        world.System<Transform>()
            .Without(Ecs.ChildOf)
            .Kind<PostPhysics>() // TODO check this phase
            .Write<GlobalTransform>() // apply commands here
            .Each(CreateRootGlobals);
        // Can have some lag in updating GlobalTransform
        // Would be better to update reactively on Transforms
        world.System<Transform, GlobalTransform>()
            .TermAt(1).Parent().Cascade()
            .Kind<PostPhysics>()
            .Write<GlobalTransform>() // redundant?
            .Each(PropagateTransforms);
    }

    private void PropagateTransforms(Entity e, ref Transform transform, ref GlobalTransform parent) {
        GlobalTransform global = parent.Apply(transform);
        e.Set(global);
        // Console.WriteLine($"Set GlobalTransform for {e}: {global}");
    }

    private void CreateRootGlobals(Entity e, ref Transform t0) {
        e.Set(GlobalTransform.from(t0));
        // Console.WriteLine($"Added Global to {e}");
    }
}