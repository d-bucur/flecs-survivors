using System;
using System.Numerics;
using flecs_test;

namespace flecs_test;

interface IColliderShape {
	public PenetrationData? GetPenetrationVec(IColliderShape other, ref Transform myTransf, ref Transform otherTransform);
}
record struct PenetrationData(
	Vector2 penetration,
	Vector2 distance,
	float penetrationLen
);
record struct SphereCollider(float Radius) : IColliderShape {
	public PenetrationData? GetPenetrationVec(IColliderShape other, ref Transform myTransf, ref Transform otherTransform) {
		return other switch {
			SphereCollider s => Shapes.PenetrationSpheres(this, s, ref myTransf, ref otherTransform),
			AABBCollider b => Shapes.PenetrationSphereAABB(this, b, ref myTransf, ref otherTransform),
		};
	}
}
record struct AABBCollider(Vector2 Min, Vector2 Max) : IColliderShape {
	public PenetrationData? GetPenetrationVec(IColliderShape other, ref Transform myTransf, ref Transform otherTransform) {
		return other switch {
			SphereCollider s => Shapes.PenetrationSphereAABB(s, this, ref otherTransform, ref myTransf),
			AABBCollider b => Shapes.PenetrationAABBs(this, b, ref myTransf, ref otherTransform),
		};
	}
}

file class Shapes {
	internal static PenetrationData? PenetrationSphereAABB(SphereCollider sphere, AABBCollider aabb, ref Transform t1, ref Transform t2) {
		throw new NotImplementedException();
	}

	internal static PenetrationData? PenetrationSpheres(SphereCollider s1, SphereCollider s2, ref Transform t1, ref Transform t2) {
		var distance = t1.Pos - t2.Pos;
		float distanceLen = distance.Length();
		var separation = s1.Radius + s2.Radius;
		var penetration = separation - distanceLen;
		if (penetration <= 0)
			return null;

		var penetrationVec = distance / distanceLen * penetration;
		// TODO contact point not precise, should consider displacement below
		return new PenetrationData {
			penetrationLen = penetration,
			distance = distance,
			penetration = penetrationVec,
		};
	}

	internal static PenetrationData? PenetrationAABBs(AABBCollider b1, AABBCollider b2, ref Transform t1, ref Transform t2) {
		throw new NotImplementedException();
	}
}