using System;
using System.Collections.Generic;
using System.Numerics;
using Flecs.NET.Core;
using Raylib_cs;
using static System.Linq.Enumerable;

namespace flecs_survivors;

record struct BulletData(Vector2 Vel, Vector2 Pos, Color Color, string SpriteName, float Pushback = 0f);

interface IBulletPattern {
    List<BulletData> Tick(double time, Vector2 direction, Vector2? target);
    uint Level { get; set; }
    List<UpgradeChoice> Upgrades { get; }
    string Name { get; }
}

/// <summary>
/// Flexible way of describing multiple patterns
/// </summary>
// TODO kind of a mess. Make everything a public field
class UniformRotatingPattern(
    float shootInterval,
    Color Color,
    string name,
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
    public string Name { get { return name; } }
    public float bulletsPerShot = bulletsPerShot;
    public float shootInterval = shootInterval;

    List<BulletData> bulletData = new((int)bulletsPerShot);
    double lastShotTime = 0;
    float rotation = 0;
    public List<UpgradeChoice> Upgrades { get; internal set; } = [];


    uint _level = 1;
    public uint Level {
        get => _level; set {
            // TODO not using levels anymore. Remove
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

    public UniformRotatingPattern WithUpgrades(List<UpgradeChoice> upgrades) {
        Upgrades = upgrades;
        return this;
    }
}

class Weapons {
    static Action<Entity> MakeUpgrade(string weaponName, Action<UniformRotatingPattern> upgradeApply) {
        return (entity) => {
            var w = (UniformRotatingPattern)entity.Get<Shooter>().GetWeapon(weaponName);
            upgradeApply(w);
        };
    }
    public static readonly UniformRotatingPattern PresetSpiral =
        new UniformRotatingPattern(400, Color.Green, "Spiral", 1, MathF.PI * 2, intervalFactorPerLevel: 0.8f, bulletsPerLevel: 0f, SpriteName: "bullet4", Pushback: 0.1f)
        .WithUpgrades([
            new("Spiral\nextra bullet", MakeUpgrade("Spiral", w => w.bulletsPerShot++)),
            new("Spiral\nshooting time", MakeUpgrade("Spiral", w => w.shootInterval *= 0.9f)),
        ]);
    public static readonly UniformRotatingPattern PresetShotgun =
        new UniformRotatingPattern(1500, Color.Red, "Shotgun", 3, 0, 1, SpriteName: "bullet5", Pushback: 0.2f)
        .WithUpgrades([
            new("Shotgun\nextra bullet", MakeUpgrade("Shotgun", w => w.bulletsPerShot++)),
            new("Shotgun\nshooting time", MakeUpgrade("Shotgun", w => w.shootInterval *= 0.9f)),
        ]);
    public static readonly UniformRotatingPattern PresetClosestSMG =
        new UniformRotatingPattern(1000, Color.Blue, "SMG", targetOrDirectionRotated: true, bulletsPerLevel: 0, intervalFactorPerLevel: 0.9f, SpriteName: "bullet1", Pushback: 0.3f)
        .WithUpgrades([
            new("SMG\nextra bullet", MakeUpgrade("SMG", w => w.bulletsPerShot++)),
            new("SMG\nshooting time", MakeUpgrade("SMG", w => w.shootInterval *= 0.9f)),
        ]);
    public static readonly UniformRotatingPattern PresetSpread =
        new UniformRotatingPattern(2000, Color.Purple, "Spread", 4, MathF.PI, SpriteName: "bullet7", Pushback: 0f)
        .WithUpgrades([
            new("Spread\nextra bullet", MakeUpgrade("Spread", w => w.bulletsPerShot++)),
            new("Spread\nshooting time", MakeUpgrade("Spread", w => w.shootInterval *= 0.9f)),
        ]);
}

// class WeaponsDebug {
//     public static readonly UniformRotatingPattern PresetSpiral =
//         new(200, Color.Blue, 1, MathF.PI * 2, intervalFactorPerLevel: 0.8f, bulletsPerLevel: 0f);
//     public static readonly UniformRotatingPattern PresetShotgun =
//         new(1000, Color.Blue, 2, 0, 1);
//     public static readonly UniformRotatingPattern PresetClosestSMG =
//         new(500, Color.Blue, targetOrDirectionRotated: true, bulletsPerLevel: 0, intervalFactorPerLevel: 0.9f);
//     public static readonly UniformRotatingPattern PresetWeak =
//         new(700, Color.Blue, targetOrDirectionRotated: true, bulletsPerLevel: 0);
//     public static readonly UniformRotatingPattern PresetSpread =
//         new(300, Color.Blue, 8, MathF.PI);
//     public static readonly UniformRotatingPattern PresetDOOM =
//         new(100, Color.Blue, 2, MathF.PI * 2, intervalFactorPerLevel: 0.4f, bulletsPerLevel: 1f);
// }

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