using System;
using System.Collections.Generic;
using System.Linq;
using Flecs.NET.Core;
using Microsoft.Xna.Framework;
using MonoGame.Extended;

namespace flecs_test;

record struct FlowField(float CellSize, uint FullWidth) {
	internal Vector2 Origin; // Center of player

	internal readonly uint FullWidth = FullWidth;
	internal readonly uint SideWidth = (FullWidth - 1) / 2;
	internal readonly Vector2 CellCenterOffset = Vector2.One * CellSize / 2;

	internal uint[] Costs = new uint[FullWidth * FullWidth];
	internal uint[] Integration = new uint[FullWidth * FullWidth];
	// Could be direction enum for optimization
	internal Vector2[] Flow = new Vector2[FullWidth * FullWidth];

	// TODO use Flags in physics layers
	internal CellFlags[] Flags = new CellFlags[FullWidth * FullWidth];
	[Flags]
	public enum CellFlags {
		None = 0,
		VisitedFlow = 1,
	}

	public readonly Vec2I? ToFieldPos(Vector2 pos) {
		var fieldPos = (pos - Origin + CellCenterOffset) / CellSize;
		var hash = new Vec2I((int)float.Floor(fieldPos.X), (int)float.Floor(fieldPos.Y));
		if (IsOutsideBounds(hash))
			return null;
		return hash;
	}

	private readonly bool IsOutsideBounds(Vec2I pos) {
		return Math.Abs(pos.X) > SideWidth || Math.Abs(pos.Y) > SideWidth;
	}

	public readonly uint ToKey(Vec2I pos) {
		return (uint)((pos.Y + (int)SideWidth) * FullWidth + pos.X + (int)SideWidth);
	}

	public readonly uint? ToKeySafe(Vec2I pos) {
		return IsOutsideBounds(pos) ? null : ToKey(pos);
	}
}

class FlowFieldECS {
	internal static void BlockScenery(Iter it) {
		ref readonly var field = ref it.World().Get<FlowField>();
		// Array.Fill<uint>(field.Costs, 0); // span should be faster than Array.Fill
		new Span<uint>(field.Costs).Clear();

		while (it.Next()) {
			var transform = it.Field<GlobalTransform>(1);
			foreach (int i in it) {
				var pos = field.ToFieldPos(transform[i].Pos);
				if (pos is null) continue;
				field.Costs[field.ToKey(pos.Value)] = uint.MaxValue;
				// TODO obstacles should also block neighboring cells if big enough
			}
		}
	}

	internal static void PathAroundEnemies(ref FlowField field, ref GlobalTransform transform) {
		var pos = field.ToFieldPos(transform.Pos);
		if (pos is null) return;
		field.Costs[field.ToKey(pos.Value)] += 1;
	}

	static readonly Vec2I[] neighbors = [(0, -1), (0, 1), (-1, 0), (1, 0)];
	static readonly Vec2I[] neighborsDiag = [
		(-1, -1), (0, -1), (1, -1),
		(-1, 0), (1, 0),
		(-1, 1), (0, 1), (1, 1),
	];

	internal static void GenerateFlowField(ref FlowField field, ref GlobalTransform player) {
		// TODO add line of sight
		// TODO dont recalc on not moving
		field.Origin = player.Pos;
		Integration(ref field);
		Flow(ref field);
	}

	private static Queue<Vec2I> ToVisit = new(20);
	private static void Integration(ref FlowField field) {
		new Span<uint>(field.Integration).Fill(uint.MaxValue);
		new Span<FlowField.CellFlags>(field.Flags).Clear();

		ToVisit.Enqueue((0, 0));
		while (ToVisit.Count > 0) {
			var pos = ToVisit.Dequeue();
			var myCost = field.Integration[field.ToKey(pos)];

			foreach (var n in neighbors) {
				var next = pos + n;
				var nextKey = field.ToKeySafe(next);
				if (nextKey is null || field.Costs[nextKey.Value] == uint.MaxValue)
					continue;
				var newCost = myCost + 1 + field.Costs[nextKey.Value];
				if (newCost < field.Integration[nextKey.Value]) {
					ToVisit.Enqueue(next);
					field.Integration[nextKey.Value] = newCost;
				}
			}
		}
	}

	private static void Flow(ref FlowField field) {
		new Span<Vector2>(field.Flow).Fill(Vector2.Zero);
		ToVisit.Enqueue((0, 0));
		while (ToVisit.Count > 0) {
			var pos = ToVisit.Dequeue();
			uint key = field.ToKey(pos);
			Vec2I? cheapestPos = null;
			uint cheapestCost = uint.MaxValue;
			foreach (var neighbor in neighborsDiag) {
				var next = pos + neighbor;
				var nextKey = field.ToKeySafe(next);
				if (nextKey is null)
					continue;
				uint nextInteg = field.Integration[nextKey.Value];
				if (nextInteg < cheapestCost) {
					cheapestCost = nextInteg;
					cheapestPos = neighbor;
				}
				if (!field.Flags[nextKey.Value].HasFlag(FlowField.CellFlags.VisitedFlow)) {
					ToVisit.Enqueue(next);
					field.Flags[nextKey.Value] |= FlowField.CellFlags.VisitedFlow;
				}
			}
			field.Flow[key] = cheapestPos.GetValueOrDefault().ToVector2().NormalizedCopy();
		}
	}

	internal static void DebugFlowField(Entity e, ref FlowField field) {
		var camera = e.CsWorld().Query<Camera>().First().Get<Camera>();
		var batch = e.CsWorld().Get<RenderCtx>().SpriteBatch;
		batch.Begin(transformMatrix: camera.GetTransformMatrix());

		for (var i = -field.SideWidth; i <= field.SideWidth; i++)
			for (var j = -field.SideWidth; j <= field.SideWidth; j++) {
				var cellCenter = new Vector2(i, j) * field.CellSize + field.Origin;
				var cellCorner = cellCenter - field.CellCenterOffset;

				// Draw the grid line
				Color gridColor = HSL.Hsl(120, 0.5f, 0.5f, 1f);
				batch.DrawLine(cellCorner, cellCorner + Vector2.UnitX * field.CellSize, gridColor);
				batch.DrawLine(cellCorner, cellCorner + Vector2.UnitY * field.CellSize, gridColor);

				var pos = new Vec2I((int)i, (int)j);
				if (field.Costs[field.ToKey(pos)] == uint.MaxValue) {
					// Draw obstacle
					batch.DrawLine(cellCorner, cellCorner + new Vector2(field.CellSize), HSL.Hsl(40, 0.5f, 0.75f, 1f), 2);
					continue;
				}
				// Draw the force
				var dir = field.Flow[field.ToKey(pos)];
				batch.DrawLine(cellCenter, cellCenter + dir * 20, HSL.Hsl(0, 0.5f, 0.5f, 1f));
			}
		batch.End();
	}
}