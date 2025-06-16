
using System.Collections.Generic;
using System.Numerics;

namespace flecs_test;

record struct BulletData(Vector2 Vel, Vector2 Pos);

interface IBulletPattern
{
	List<BulletData> Tick(float delta);
}

struct SimplePattern(float shootInterval) : IBulletPattern
{
	List<BulletData> bulletData = new(10);
	float lastShotTime = 0;
	float shootInterval = shootInterval;
	const float SPEED = 7;

	public List<BulletData> Tick(float time)
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
