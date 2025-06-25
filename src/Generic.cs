using System;
using System.Numerics;
using Flecs.NET.Core;
using Raylib_cs;

record struct Vec2I(int X, int Y) {
    public static implicit operator Vec2I((int, int) o) => new Vec2I(o.Item1, o.Item2);

    public static readonly Vec2I Zero = new(0, 0);

    public Vector2 ToVector2() {
        return new Vector2(X, Y);
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


public class HSV {
    public static Color Hsv(float h, float s, float v, float a = 1f) {
        return Raylib.ColorAlpha(Raylib.ColorFromHSV(h, s, v), a);
    }
}

static class Helpers {
    public static void PrintSysName(Iter it) {
        Console.WriteLine($"{it.System().Name()}");
    }

    public static Vector2 Truncated(this Vector2 v, float length) {
        var l = v.Length();
        return l > length ? v / l * length : v;
    }

    public static Vector2 Normalized(this Vector2 v) {
        return v / v.Length();
    }

    public static Vector2 Rotate(this Vector2 value, float radians) {
        float cos = MathF.Cos(radians);
        float sin = MathF.Sin(radians);
        return new Vector2(value.X * cos - value.Y * sin, value.X * sin + value.Y * cos);
    }
}