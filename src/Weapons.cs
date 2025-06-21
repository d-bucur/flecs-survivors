using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using static System.Linq.Enumerable;

namespace flecs_test;

record struct BulletData(Vector2 Vel, Vector2 Pos);

interface IBulletPattern {
    List<BulletData> Tick(double time, Vector2 direction, Vector2? target);
    uint Level { get; set; }
}

struct SimplePattern(float shootInterval) : IBulletPattern {
    List<BulletData> bulletData = new(10);
    double lastShotTime = 0;
    float shootInterval = shootInterval;
    const float SPEED = 7;

    public uint Level { get => 1; set => _ = 1; }

    public List<BulletData> Tick(double time, Vector2 _, Vector2? target) {
        bulletData.Clear();
        if (time - lastShotTime < shootInterval)
            return bulletData;
        lastShotTime = time;

        bulletData.Add(new BulletData(new Vector2(SPEED, 0), Vector2.Zero));
        bulletData.Add(new BulletData(new Vector2(-SPEED, 0), Vector2.Zero));
        bulletData.Add(new BulletData(new Vector2(0, SPEED), Vector2.Zero));
        bulletData.Add(new BulletData(new Vector2(0, -SPEED), Vector2.Zero));
        return bulletData;
    }
}

/// <summary>
/// Flexible way of describing multiple patterns
/// </summary>
record struct UniformRotatingPattern(
    float shootInterval,
    float bulletsPerShot = 1,
    float rotRadiansPerSec = 0,
    float scatterAngle = MathF.PI * 2,
    float bulletSpeed = 8,
    bool targetOrDirectionRotated = false,
    float bulletsPerLevel = 1,
    float intervalFactorPerLevel = 0.95f
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
            var dir = Vector2.Rotate(Vector2.UnitX, rotationCentered + deltaAngle * i);
            bulletData.Add(new BulletData(dir * bulletSpeed, Vector2.Zero));
        }

        return bulletData;
    }
}

class Weapons {
    // Spiral shouldn't use any extra target
    public static readonly UniformRotatingPattern PresetSpiral = new(200, 1, MathF.PI * 2, intervalFactorPerLevel: 0.8f, bulletsPerLevel: 0f);
    public static readonly UniformRotatingPattern PresetShotgun = new(1000, 2, 0, 1);
    public static readonly UniformRotatingPattern PresetClosestSMG = new(500, targetOrDirectionRotated: true, bulletsPerLevel: 0, intervalFactorPerLevel: 0.9f);
    public static readonly UniformRotatingPattern PresetWeak = new(700, targetOrDirectionRotated: true, bulletsPerLevel: 0);
    public static readonly UniformRotatingPattern PresetSpread = new(300, 8, MathF.PI);
    public static readonly UniformRotatingPattern PresetDOOM = new(100, 2, MathF.PI * 2, intervalFactorPerLevel: 0.4f, bulletsPerLevel: 1f);
}