using System;
using System.Collections.Generic;
using System.Numerics;
using Raylib_cs;
using static System.Linq.Enumerable;

namespace flecs_test;

record struct BulletData(Vector2 Vel, Vector2 Pos, Color Color, string SpriteName, float Pushback = 0f);

interface IBulletPattern {
    List<BulletData> Tick(double time, Vector2 direction, Vector2? target);
    uint Level { get; set; }
}

// struct SimplePattern(float shootInterval) : IBulletPattern {
//     List<BulletData> bulletData = new(10);
//     double lastShotTime = 0;
//     float shootInterval = shootInterval;
//     const float SPEED = 7f;

//     public uint Level { get => 1; set => _ = 1; }

//     public List<BulletData> Tick(double time, Vector2 _, Vector2? target) {
//         bulletData.Clear();
//         if (time - lastShotTime < shootInterval)
//             return bulletData;
//         lastShotTime = time;

//         bulletData.Add(new BulletData(new Vector2(SPEED, 0), Vector2.Zero));
//         bulletData.Add(new BulletData(new Vector2(-SPEED, 0), Vector2.Zero));
//         bulletData.Add(new BulletData(new Vector2(0, SPEED), Vector2.Zero));
//         bulletData.Add(new BulletData(new Vector2(0, -SPEED), Vector2.Zero));
//         return bulletData;
//     }
// }

/// <summary>
/// Flexible way of describing multiple patterns
/// </summary>
record struct UniformRotatingPattern(
    float shootInterval,
    Color Color,
    float bulletsPerShot = 1,
    float rotRadiansPerSec = 0,
    float scatterAngle = MathF.PI * 2,
    float bulletSpeed = 0.5f,
    bool targetOrDirectionRotated = false,
    float bulletsPerLevel = 1,
    float intervalFactorPerLevel = 0.95f,
    float Pushback = 0f,
    string SpriteName = "bullet1"
) : IBulletPattern {
    List<BulletData> bulletData = new((int)bulletsPerShot);
    float bulletSpeed = bulletSpeed;
    double lastShotTime = 0;
    float rotation = 0;


    uint _level = 1;
    public uint Level {
        get => _level; set {
            // TODO make more generic, using scaling functions
            var diff = value - _level;
            bulletsPerShot += (int)diff * bulletsPerLevel;
            shootInterval *= MathF.Pow(intervalFactorPerLevel, diff); // can div0
            _level = value;
        }
    }

    public List<BulletData> Tick(double time, Vector2 heading, Vector2? target) {
        bulletData.Clear();
        if (time - lastShotTime < shootInterval)
            return bulletData;
        lastShotTime = time;
        rotation = (float)(time / 1000 * rotRadiansPerSec);

        var targetDirection = targetOrDirectionRotated ? (target is null ? Vector2.Zero : target.Value) : heading;
        var rotationCentered = rotation - scatterAngle / 2 + MathF.Atan2(targetDirection.Y, targetDirection.X);
        var deltaAngle = scatterAngle / bulletsPerShot;
        foreach (var i in Range(0, (int)bulletsPerShot)) {
            var dir = Vector2.UnitX.Rotate(rotationCentered + deltaAngle * i);
            bulletData.Add(new BulletData(dir * bulletSpeed, Vector2.Zero, Color, SpriteName, Pushback));
        }

        return bulletData;
    }
}

class Weapons {
    // Spiral shouldn't use any extra target
    public static readonly UniformRotatingPattern PresetSpiral =
        new(600, Color.Green, 1, MathF.PI * 2, intervalFactorPerLevel: 0.8f, bulletsPerLevel: 0f, SpriteName: "bullet4", Pushback: 0.1f);
    public static readonly UniformRotatingPattern PresetShotgun =
        new(1500, Color.Red, 3, 0, 1, SpriteName: "bullet5", Pushback: 0.2f);
    public static readonly UniformRotatingPattern PresetClosestSMG =
        new(1000, Color.Blue, targetOrDirectionRotated: true, bulletsPerLevel: 0, intervalFactorPerLevel: 0.9f, SpriteName: "bullet1", Pushback: 0.3f);
    public static readonly UniformRotatingPattern PresetSpread =
        new(2000, Color.Purple, 4, MathF.PI, SpriteName: "bullet7", Pushback: 0f);
}

class WeaponsTest {
    public static readonly UniformRotatingPattern PresetSpiral =
        new(200, Color.Blue, 1, MathF.PI * 2, intervalFactorPerLevel: 0.8f, bulletsPerLevel: 0f);
    public static readonly UniformRotatingPattern PresetShotgun =
        new(1000, Color.Blue, 2, 0, 1);
    public static readonly UniformRotatingPattern PresetClosestSMG =
        new(500, Color.Blue, targetOrDirectionRotated: true, bulletsPerLevel: 0, intervalFactorPerLevel: 0.9f);
    public static readonly UniformRotatingPattern PresetWeak =
        new(700, Color.Blue, targetOrDirectionRotated: true, bulletsPerLevel: 0);
    public static readonly UniformRotatingPattern PresetSpread =
        new(300, Color.Blue, 8, MathF.PI);
    public static readonly UniformRotatingPattern PresetDOOM =
        new(100, Color.Blue, 2, MathF.PI * 2, intervalFactorPerLevel: 0.4f, bulletsPerLevel: 1f);
}