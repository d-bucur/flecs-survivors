
using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using MonoGame.Extended;
using static System.Linq.Enumerable;

namespace flecs_test;

record struct BulletData(Vector2 Vel, Vector2 Pos);

interface IBulletPattern
{
	List<BulletData> Tick(double time, Vector2 direction);
	uint Level { get; set; }
}

struct SimplePattern(float shootInterval) : IBulletPattern
{
	List<BulletData> bulletData = new(10);
	double lastShotTime = 0;
	float shootInterval = shootInterval;
	const float SPEED = 7;

	public uint Level { get => 1; set => _ = 1; }

	public List<BulletData> Tick(double time, Vector2 _)
	{
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
struct UniformRotatingPattern(float shootInterval, int bulletsPerShot = 1, float rotRadiansPerSec = 0, float scatterAngle = MathF.PI * 2, float bulletSpeed = 8) : IBulletPattern
{
	float shootInterval = shootInterval;
	int bulletsPerShot = bulletsPerShot;
	float rotRadiansPerSec = rotRadiansPerSec;
	float scatterAngle = scatterAngle;

	List<BulletData> bulletData = new(bulletsPerShot);
	float bulletSpeed = bulletSpeed;
	double lastShotTime = 0;
	float rotation = 0;

	uint _level = 1;
	public uint Level
	{
		get => _level; set
		{
			// TODO make more generic
			var diff = value - _level;
			bulletsPerShot += (int)diff;
			shootInterval *= MathF.Pow(0.95f, diff); // can div0
			_level = value;
		}
	}

	public List<BulletData> Tick(double time, Vector2 direction)
	{
		bulletData.Clear();
		if (time - lastShotTime < shootInterval)
			return bulletData;
		lastShotTime = time;
		rotation = (float)(time / 1000 * rotRadiansPerSec);

		var rotationCentered = rotation - scatterAngle / 2 + MathF.Atan2(direction.Y, direction.X);
		var deltaAngle = scatterAngle / bulletsPerShot;
		foreach (var i in Range(0, bulletsPerShot))
		{
			var dir = Vector2.Rotate(Vector2.UnitX, rotationCentered + deltaAngle * i);
			bulletData.Add(new BulletData(dir * bulletSpeed, Vector2.Zero));
		}

		return bulletData;
	}
}

class Weapons
{
	public static readonly UniformRotatingPattern PresetSpiral = new(50, 1, MathF.PI * 2, MathF.PI * 2, 10);
	public static readonly UniformRotatingPattern PresetShotgun = new(1000, 2, 0, 1);
	// public static readonly UniformRotatingPattern PresetShotgun = new(500, 8, 0, 1);
	public static readonly UniformRotatingPattern PresetSpread = new(300, 8, MathF.PI);
}