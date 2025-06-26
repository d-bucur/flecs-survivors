global using PackingData = System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<string, flecs_test.SpriteSheet.KeyframeRaw[]>>;

using System.Text.Json;
using Raylib_cs;

namespace flecs_test;

// TODO super ugly. Refactor. Load from file
struct SpriteSheet {
	public static PackingData? LoadSheet(string fileName) {
		if (fileName == "Content/sprites/Blue_witch/packed/blue_witch.png")
			return JsonSerializer.Deserialize<PackingData>(witchJson)!;
		else
			return null;
		// TODO Would be better to parse here directly into Rectangles and delete the implicit conversion
	}

	public record struct KeyframeRaw(int x, int y, int w, int h) {
		public static implicit operator Rectangle(KeyframeRaw k) {
			return new Rectangle(k.x, k.y, k.w, k.h);
		}
	}

	static string witchJson =
	@"
	{
  ""blue_witch"": {
    ""charge"": [
      {
        ""x"": 0,
        ""y"": 0,
        ""w"": 48,
        ""h"": 48
      },
      {
        ""x"": 0,
        ""y"": 48,
        ""w"": 48,
        ""h"": 48
      },
      {
        ""x"": 0,
        ""y"": 96,
        ""w"": 48,
        ""h"": 48
      },
      {
        ""x"": 0,
        ""y"": 144,
        ""w"": 48,
        ""h"": 48
      },
      {
        ""x"": 0,
        ""y"": 192,
        ""w"": 48,
        ""h"": 48
      }
    ],
    ""run"": [
      {
        ""x"": 0,
        ""y"": 240,
        ""w"": 32,
        ""h"": 48
      },
      {
        ""x"": 0,
        ""y"": 288,
        ""w"": 32,
        ""h"": 48
      },
      {
        ""x"": 0,
        ""y"": 336,
        ""w"": 32,
        ""h"": 48
      },
      {
        ""x"": 0,
        ""y"": 384,
        ""w"": 32,
        ""h"": 48
      },
      {
        ""x"": 0,
        ""y"": 432,
        ""w"": 32,
        ""h"": 48
      },
      {
        ""x"": 0,
        ""y"": 480,
        ""w"": 32,
        ""h"": 48
      },
      {
        ""x"": 0,
        ""y"": 528,
        ""w"": 32,
        ""h"": 48
      },
      {
        ""x"": 0,
        ""y"": 576,
        ""w"": 32,
        ""h"": 48
      }
    ]
  }
}
	";
}