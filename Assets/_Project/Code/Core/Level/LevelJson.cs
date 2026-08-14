using System.Collections.Generic;
using Match3.Core.Json;

namespace Match3.Core
{
    /// <summary>
    /// Maps <see cref="LevelConfig"/> to and from JSON, field by field.
    /// Explicit mapping rather than reflection: it survives IL2CPP stripping, it round-trips the
    /// recursive <see cref="EntitySpec"/>, and a malformed level file produces a readable error
    /// instead of silently loading as defaults.
    /// </summary>
    public static class LevelJson
    {
        public static string Write(LevelConfig config, bool pretty = true) =>
            ToJson(config).ToJson(pretty);

        public static JsonValue ToJson(LevelConfig config)
        {
            JsonValue root = JsonValue.Object()
                .Set("id", config.Id)
                .Set("name", config.Name)
                .Set("width", config.Width)
                .Set("height", config.Height)
                .Set("moveLimit", config.MoveLimit)
                .Set("seed", config.Seed);

            JsonValue palette = JsonValue.Array();
            foreach (PieceColor color in config.Palette)
                palette.Add(JsonValue.Of(PieceColors.ToCode(color).ToString()));
            root.Set("palette", palette);

            JsonValue goals = JsonValue.Array();
            foreach (LevelGoal goal in config.Goals)
                goals.Add(JsonValue.Object()
                    .Set("color", PieceColors.ToCode(goal.Color).ToString())
                    .Set("count", goal.Count));
            root.Set("goals", goals);

            if (config.Layout.Count > 0)
            {
                JsonValue layout = JsonValue.Array();
                foreach (string row in config.Layout)
                    layout.Add(JsonValue.Of(row));
                root.Set("layout", layout);
            }

            if (config.Overrides.Count > 0)
            {
                JsonValue overrides = JsonValue.Array();
                foreach (CellOverride cell in config.Overrides)
                    overrides.Add(JsonValue.Object()
                        .Set("x", cell.X)
                        .Set("y", cell.Y)
                        .Set("spec", SpecToJson(cell.Spec)));
                root.Set("overrides", overrides);
            }

            return root;
        }

        public static LevelConfig Parse(string json) => FromJson(JsonValue.Parse(json));

        public static LevelConfig FromJson(JsonValue root)
        {
            var config = new LevelConfig
            {
                Id = root["id"].AsString(string.Empty),
                Name = root["name"].AsString(string.Empty),
                Width = root["width"].AsInt(8),
                Height = root["height"].AsInt(8),
                MoveLimit = root["moveLimit"].AsInt(20),
                Seed = root["seed"].AsInt(),
            };

            foreach (JsonValue item in root["palette"].AsArray())
                config.Palette.Add(ParseColor(item, "palette entry"));

            foreach (JsonValue item in root["goals"].AsArray())
                config.Goals.Add(new LevelGoal(
                    ParseColor(item["color"], "goal colour"),
                    item["count"].AsInt(1)));

            foreach (JsonValue item in root["layout"].AsArray())
                config.Layout.Add(item.AsString(string.Empty));

            foreach (JsonValue item in root["overrides"].AsArray())
                config.Overrides.Add(new CellOverride(
                    item["x"].AsInt(),
                    item["y"].AsInt(),
                    SpecFromJson(item["spec"])));

            return config;
        }

        // ------------------------------------------------------------------ entity specs

        private static JsonValue SpecToJson(EntitySpec spec)
        {
            if (spec == null)
                return JsonValue.Null();

            JsonValue node = JsonValue.Object();

            switch (spec.Kind)
            {
                case EntitySpecKind.Empty:
                    node.Set("kind", "empty");
                    break;

                case EntitySpecKind.RandomPiece:
                    node.Set("kind", "random");
                    break;

                case EntitySpecKind.ColoredPiece:
                    node.Set("kind", "piece");
                    if (spec.Color != PieceColor.None)
                        node.Set("color", PieceColors.ToCode(spec.Color).ToString());
                    if (spec.Booster != BoosterType.None)
                    {
                        node.Set("booster", BoosterCode(spec.Booster));
                        node.Set("orientation",
                            spec.Orientation == LineOrientation.Horizontal ? "h" : "v");
                    }

                    break;

                case EntitySpecKind.Obstacle:
                    node.Set("kind", "obstacle");
                    node.Set("id", spec.ObstacleId);
                    if (spec.HpOverride > 0) node.Set("hp", spec.HpOverride);
                    if (spec.WidthOverride > 0) node.Set("width", spec.WidthOverride);
                    if (spec.HeightOverride > 0) node.Set("height", spec.HeightOverride);
                    if (spec.Color != PieceColor.None)
                        node.Set("color", PieceColors.ToCode(spec.Color).ToString());
                    if (spec.Contains != null)
                        node.Set("contains", SpecToJson(spec.Contains));
                    break;
            }

            return node;
        }

        private static EntitySpec SpecFromJson(JsonValue node)
        {
            if (node == null || node.IsNull)
                return null;

            string kind = node["kind"].AsString("random");

            switch (kind)
            {
                case "empty":
                    return EntitySpec.Empty();

                case "random":
                    return EntitySpec.RandomPiece();

                case "piece":
                {
                    PieceColor color = node.Has("color")
                        ? ParseColor(node["color"], "piece colour")
                        : PieceColor.None;

                    if (!node.Has("booster"))
                        return EntitySpec.ColoredPiece(color);

                    LineOrientation orientation = node["orientation"].AsString("h") == "v"
                        ? LineOrientation.Vertical
                        : LineOrientation.Horizontal;

                    return EntitySpec.BoosterPiece(ParseBooster(node["booster"]), color, orientation);
                }

                case "obstacle":
                {
                    string id = node["id"].AsString();
                    if (string.IsNullOrEmpty(id))
                        throw new JsonException("An obstacle spec needs an \"id\".");

                    return EntitySpec.Obstacle(
                        id,
                        node["hp"].AsInt(),
                        node.Has("color") ? ParseColor(node["color"], "obstacle colour") : PieceColor.None,
                        SpecFromJson(node.Has("contains") ? node["contains"] : null),
                        node["width"].AsInt(),
                        node["height"].AsInt());
                }

                default:
                    throw new JsonException($"Unknown entity spec kind '{kind}'.");
            }
        }

        // ------------------------------------------------------------------ scalars

        private static PieceColor ParseColor(JsonValue value, string what)
        {
            string text = value.AsString();
            if (string.IsNullOrEmpty(text))
                throw new JsonException($"Missing {what}.");

            if (!PieceColors.TryFromCode(char.ToLowerInvariant(text[0]), out PieceColor color))
                throw new JsonException($"'{text}' is not a known colour code for {what} " +
                                        "(expected one of r, g, b, y, p, o).");

            return color;
        }

        private static string BoosterCode(BoosterType booster)
        {
            switch (booster)
            {
                case BoosterType.Line: return "line";
                case BoosterType.Bomb: return "bomb";
                case BoosterType.Rainbow: return "rainbow";
                case BoosterType.Plane: return "plane";
                default: return "none";
            }
        }

        private static BoosterType ParseBooster(JsonValue value)
        {
            switch (value.AsString(string.Empty))
            {
                case "line": return BoosterType.Line;
                case "bomb": return BoosterType.Bomb;
                case "rainbow": return BoosterType.Rainbow;
                case "plane": return BoosterType.Plane;
                case "none": return BoosterType.None;
                default: throw new JsonException($"Unknown booster '{value.AsString()}'.");
            }
        }
    }
}
