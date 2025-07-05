using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Xml;
using flecs_test;
using Raylib_cs;


namespace Tiled {
	struct Tileset {
		internal required int FirstGid;
		internal required int LastGid;
		internal required Texture2D Texture;
		internal required int Columns;
		internal required int TileHeight;
		internal required int TileWidth;
	}

	class Layer {
		internal required int Width;
		internal required int Height;
		internal required int[] Tiles;
		internal required bool Visible;
	};

	struct TiledMap {
		internal required int Height;
		internal required int Width;
		internal required int TileWidth;
		internal required int TileHeight;
		internal required Tileset[] Tilesets;
		internal required Layer[] Layers;

		public (Texture2D texture, Rectangle source) GetCellData(int tile) {
			int tilesetIdx = -1;
			int testIdx = 0;
			do {
				if (tile >= Tilesets[testIdx].FirstGid && tile <= Tilesets[testIdx].LastGid) {
					tilesetIdx = testIdx;
					break;
				}
				testIdx++;
				// unchecked. Should always exit if data is correct
			} while (tilesetIdx == -1);

			var tileset = Tilesets[tilesetIdx];
			tile -= tileset.FirstGid;
			var y = Math.DivRem(tile, tileset.Columns, out var x);
			var source = new Rectangle(
				x * tileset.TileWidth,
				y * tileset.TileHeight,
				tileset.TileWidth,
				tileset.TileHeight
			);
			return (tileset.Texture, source);
		}
	}
}

class TiledMapLoader {
	public static void LoadMapXml() {
		var doc = new XmlDocument();
		doc.Load("Content/tileset/map.tmx");
	}

	public static Tiled.TiledMap LoadMap(ref ContentManager contentManager) {
		var options = new JsonSerializerOptions {
			PropertyNameCaseInsensitive = true
		};
		var filePath = "Content/tileset/map.tmj";
		var ret = JsonSerializer.Deserialize<TiledIntermediate.TiledMap>(File.ReadAllText(filePath), options)!;
		Console.WriteLine($"Loaded: {ret}");
		return ConvertTiledMap(ret, contentManager);
	}

	private static Tiled.TiledMap ConvertTiledMap(TiledIntermediate.TiledMap m, ContentManager contentManager) {
		var tilesets = m.Tilesets.Select(t => new Tiled.Tileset {
			FirstGid = t.FirstGid,
			LastGid = t.FirstGid + t.TileCount,
			Columns = t.Columns,
			TileHeight = t.TileHeight,
			TileWidth = t.TileHeight,
			Texture = contentManager.Load($"Content/tileset/{t.Image}").Texture,
		}).ToArray();
		return new Tiled.TiledMap {
			Height = m.Height,
			Width = m.Width,
			TileWidth = m.TileWidth,
			TileHeight = m.TileHeight,
			Layers = m.Layers
				.Select(l => new Tiled.Layer {
					Width = l.Width,
					Height = l.Height,
					Tiles = l.Data,
					Visible = l.Visible,
				}).ToArray(),
			Tilesets = tilesets
		};
	}
}

namespace TiledIntermediate {
	record struct TiledMap(
		int Height,
		int Width,
		int TileHeight,
		int TileWidth,
		Tileset[] Tilesets,
		Layer[] Layers
	);

	record struct Tileset(
		int FirstGid,
		string Image,
		int Columns,
		int TileHeight,
		int TileWidth,
		int TileCount
	);

	record class Layer(
		int Id,
		int Width,
		int Height,
		bool Visible,
		int[] Data
	);
}