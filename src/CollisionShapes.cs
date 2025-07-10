using System;
using System.Numerics;

namespace flecs_survivors;

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
			SphereCollider s => Shapes.PenetrationSphereAABB(s, this, ref otherTransform, ref myTransf, -1),
			AABBCollider b => Shapes.PenetrationAABBs(this, b, ref myTransf, ref otherTransform),
		};
	}
}

record struct PenetrationData(
	Vector2 penetration, // TODO not needed?
	Vector2 Normal,
	float penetrationLen
);

file class Shapes {
	internal static PenetrationData? PenetrationSphereAABB(SphereCollider s, AABBCollider b, ref Transform ts, ref Transform tb, float sign = 1f) {
		var minPos = tb.Pos - b.Size;
		var maxPos = tb.Pos + b.Size;
		var closestPoint = Vector2.Clamp(ts.Pos, minPos, maxPos);
		var distance = ts.Pos - closestPoint;
		// TODO optimize: avoid sqrt for early exit
		var distanceLen = distance.Length();
		if (distanceLen == 0) {
			distance = ts.Pos - tb.Pos;
			distanceLen = distance.Length();
		}
		if (distanceLen == 0) {
			// TODO could still be div0 here if very unlucky
			Console.WriteLine($"div0 Sphere-AABB");
		}
		if (distanceLen < s.Radius) {
			Vector2 normal = distance / distanceLen * sign;
			float penLen = s.Radius - distanceLen;
			return new PenetrationData {
				penetration = normal * penLen,
				Normal = normal,
				penetrationLen = penLen,
			};
		}
		return null;
	}

	internal static PenetrationData? PenetrationSpheres(SphereCollider s1, SphereCollider s2, ref Transform t1, ref Transform t2) {
		var distance = t1.Pos - t2.Pos;
		// TODO optimize: avoid sqrt for early exit
		float distanceLen = distance.Length();
		if (distanceLen == 0) {
			Console.WriteLine($"div0 Spheres");
		}
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