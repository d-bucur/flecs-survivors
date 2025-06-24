using System;
using Flecs.NET.Core;
using Microsoft.Xna.Framework;
using MonoGame.Extended;

record struct Vec2I(int X, int Y) {
    public static implicit operator Vec2I((int, int) o) => new Vec2I(o.Item1, o.Item2);

    public static readonly Vec2I Zero = new(0, 0);

    public Vector2 ToVector2() {
        return new Vector2(X, Y);
    }

    public System.Numerics.Vector2 ToNumerics() {
        return new System.Numerics.Vector2(X, Y);
    }

    public static Vec2I operator +(Vec2I a, Vec2I b) {
        return new Vec2I(a.X + b.X, a.Y + b.Y);
    }

    public static Vec2I operator -(Vec2I a, Vec2I b) {
        return new Vec2I(a.X - b.X, a.Y - b.Y);
    }

    public static Vec2I operator /(Vec2I a, Vec2I b) {
        return new Vec2I(a.X / b.X, a.Y / b.Y);
    }

    public static Vec2I operator /(Vec2I a, int b) {
        return new Vec2I(a.X / b, a.Y / b);
    }
}


public class HSL {
    // Was this so hard monogame????
    public static Color Hsl(float h, float s, float l, float a = 1f) {
        return new Color(new HslColor(h, s, l).ToRgb(), a);
    }
}

class Helpers {
    public static void PrintSysName(Iter it) {
        Console.WriteLine($"{it.System().Name()}");
    }

    public System.Numerics.Vector2 To(Microsoft.Xna.Framework.Vector2 v) {
        return new System.Numerics.Vector2(v.X, v.Y);
    }
}