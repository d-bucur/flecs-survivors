using System;
using System.Collections.Generic;
using Flecs.NET.Core;
using Microsoft.Xna.Framework;
using MonoGame.Extended;

namespace flecs_test;

record struct FlowField(float CellSize, int FieldWidth = 10) {
	internal Vector2 Origin; // Center of player
	internal Vector2[] Field = new Vector2[(FieldWidth * 2 + 1) * (FieldWidth * 2 + 1)];
	internal HashSet<Vec2I> Obstacles = new(20);
	public Vector2 CellCenterOffset {
		get {
			// could cache
			return Vector2.One * CellSize / 2;
		}
	}

	public Vec2I? HashAt(Vector2 pos) {
		var fieldPos = (pos - Origin + CellCenterOffset) / CellSize;
		var hash = new Vec2I((int)float.Floor(fieldPos.X), (int)float.Floor(fieldPos.Y));
		if (Math.Abs(hash.X) > FieldWidth || MathF.Abs(hash.Y) > FieldWidth)
			return null;
		return hash;
	}

	// change to uint
	public int ToKey(Vec2I pos) {
		return (pos.Y + FieldWidth) * (FieldWidth * 2 + 1) + pos.X + FieldWidth;
	}
}

class FlowFieldECS {
	internal static void BlockScenery(ref FlowField field, ref GlobalTransform transform) {
		// TODO obstacles should also block neighboring cells if big enough
		var pos = field.HashAt(transform.Pos);
		if (pos is null) return;
		field.Obstacles.Add(pos.Value);
		// Console.WriteLine($"Blocking at: {pos}");
	}

	private record struct VisitEntry(Vec2I Pos, Vec2I Origin, Vector2 OriginDir);
	// readonly Vec2I[] neighbors = [
	// 	(-1, -1), (0, -1), (1, -1),
	// 	(-1, 0), (1, 0),
	// 	(-1, 1), (0, 1), (1, 1),
	// ];
	// Works without diagonals as well since it sums the previous force
	static readonly Vec2I[] neighbors = [
		(0, -1),
		(-1, 0), (1, 0),
		(0, 1),
	];

	internal static void GenerateFlowField(ref FlowField field, ref GlobalTransform player) {
		// TODO add line of sight
		// TODO dont recalc on not moving
		field.Origin = player.Pos;
		var visited = new HashSet<Vec2I>();
		var toVisit = new Queue<VisitEntry>([new VisitEntry((0, 0), (0, 0), Vector2.Zero)]);
		while (toVisit.Count > 0) {
			var (current, origin, originDir) = toVisit.Dequeue();
			visited.Add(current);
			int key = field.ToKey(current);
			Vector2 simpleDir = (origin - current).ToVector2();
			Vector2 dir = simpleDir;
			field.Field[key] = dir == Vector2.Zero ? dir : Vector2.Normalize(dir);
			foreach (var n in neighbors) {
				var pos = current + n;
				if (Math.Abs(pos.X) > field.FieldWidth || Math.Abs(pos.Y) > field.FieldWidth)
					continue;
				if (visited.Contains(pos) || field.Obstacles.Contains(pos))
					continue;
				toVisit.Enqueue(new VisitEntry(pos, current, dir));
			}
		}
	}

	internal static void DebugFlowField(Entity e, ref FlowField field) {
		var camera = e.CsWorld().Query<Camera>().First().Get<Camera>();
		var batch = e.CsWorld().Get<RenderCtx>().SpriteBatch;
		batch.Begin(transformMatrix: camera.GetTransformMatrix());

		for (var i = -field.FieldWidth; i <= field.FieldWidth; i++)
			for (var j = -field.FieldWidth; j <= field.FieldWidth; j++) {
				var cellCenter = new Vector2(i, j) * field.CellSize + field.Origin;
				var cellCorner = cellCenter - field.CellCenterOffset;

				// Draw the grid line
				Color gridColor = HSL.Hsl(120, 0.5f, 0.5f, 1f);
				batch.DrawLine(cellCorner, cellCorner + Vector2.UnitX * field.CellSize, gridColor);
				batch.DrawLine(cellCorner, cellCorner + Vector2.UnitY * field.CellSize, gridColor);

				var vecKey = new Vec2I(i, j);
				if (field.Obstacles.Contains(vecKey)) {
					// Draw obstacle
					batch.DrawLine(cellCorner, cellCorner + new Vector2(field.CellSize), HSL.Hsl(40, 0.5f, 0.75f, 1f), 2);
					continue;
				}
				// Draw the force
				var dir = field.Field[field.ToKey(vecKey)];
				batch.DrawLine(cellCenter, cellCenter + dir * 20, HSL.Hsl(0, 0.5f, 0.5f, 1f));
			}
		batch.End();
	}
}