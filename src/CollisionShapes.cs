using System;
using System.Numerics;
using flecs_test;

namespace flecs_test;

interface IColliderShape {
	public PenetrationData? GetPenetrationVec(IColliderShape other, ref Transform myTransf, ref Transform otherTransform);
}
record struct SphereCollider(float Radius) : IColliderShape {
	public PenetrationData? GetPenetrationVec(IColliderShape other, ref Transform myTransf, ref Transform otherTransform) {
		return other switch {
			SphereCollider s => Shapes.PenetrationSpheres(this, s, ref myTransf, ref otherTransform),
			AABBCollider b => Shapes.PenetrationSphereAABB(this, b, ref myTransf, ref otherTransform),
		};
	}
}
record struct AABBCollider(Vector2 Size) : IColliderShape {
	public PenetrationData? GetPenetrationVec(IColliderShape other, ref Transform myTransf, ref Transform otherTransform) {
		return other switch {
			SphereCollider s => Shapes.PenetrationSphereAABB(s, this, ref otherTransform, ref myTransf),
			AABBCollider b => Shapes.PenetrationAABBs(this, b, ref myTransf, ref otherTransform),
		};
	}
}

record struct PenetrationData(
	Vector2 penetration,
	Vector2 Normal,
	float penetrationLen
);

file class Shapes {
	internal static PenetrationData? PenetrationSphereAABB(SphereCollider sphere, AABBCollider aabb, ref Transform t1, ref Transform t2) {
		// throw new NotImplementedException();
		return null;
	}

	internal static PenetrationData? PenetrationSpheres(SphereCollider s1, SphereCollider s2, ref Transform t1, ref Transform t2) {
		var distance = t1.Pos - t2.Pos;
		float distanceLen = distance.Length();
		var penetration = s1.Radius + s2.Radius - distanceLen;
		if (penetration <= 0)
			return null;

		var penetrationVec = distance / distanceLen * penetration;
		// TODO contact point not precise, should consider displacement below
		return new PenetrationData {
			penetrationLen = penetration,
			Normal = distance / distanceLen,
			penetration = penetrationVec,
		};
	}

	internal static PenetrationData? PenetrationAABBs(AABBCollider b1, AABBCollider b2, ref Transform t1, ref Transform t2) {
		// TODO refactor naming to coincide with above
		var distance = t1.Pos - t2.Pos;
		var px = b1.Size.X + b2.Size.X - MathF.Abs(distance.X);
		var py = b1.Size.Y + b2.Size.Y - MathF.Abs(distance.Y);
		if (px > 0 && py > 0) {
			if (px < py) {
				var pv = new Vector2(MathF.Sign(distance.X) * px, 0);
				return new PenetrationData(pv, pv / px, px);
			}
			else {
				var pv = new Vector2(0, MathF.Sign(distance.Y) * py);
				return new PenetrationData(pv, pv / py, py);
			}
		}
		return null;
	}
}