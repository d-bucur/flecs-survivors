global using PackingData = System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<string, flecs_survivors.SpriteSheet.KeyframeRaw[]>>;
using System.IO;
using System.Text.Json;
using Raylib_cs;

namespace flecs_survivors;

// TODO super ugly. Refactor
struct SpriteSheet {
	public static PackingData? LoadSheet(string fileName) {
		JsonSerializerOptions options = new();
		options.UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow;
        if (fileName == Textures.MEGA_SHEET)
            return JsonSerializer.Deserialize<PackingData>(File.ReadAllText("Content/sprites/packed2/characters_out.json"), options)!;
        else
            return null;
		// TODO Would be better to parse here directly into Rectangles and delete the implicit conversion
	}

	public record struct KeyframeRaw(int x, int y, int w, int h) {
		public static implicit operator Rectangle(KeyframeRaw k) {
			return new Rectangle(k.x, k.y, k.w, k.h);
		}
	}
}