using System;
using System.Collections.Generic;
using Match3.Core;
using UnityEngine;

namespace Match3.Presentation
{
    /// <summary>
    /// Generates every sprite the game needs at startup, straight into textures.
    /// <para>
    /// No imported art at all: the whole look is code, which keeps the repository free of binary
    /// assets and licence questions and lets the palette be retuned in one place.
    /// </para>
    /// <para>
    /// Each colour also gets its own <b>shape</b>, not just its own hue. That is what makes the board
    /// readable at a glance — and readable to a colour-blind player, who would otherwise be guessing.
    /// </para>
    /// </summary>
    public sealed class SpriteLibrary
    {
        public const int PieceSize = 128;
        public const int PanelSize = 64;

        private readonly Dictionary<PieceColor, Sprite> _pieces = new Dictionary<PieceColor, Sprite>();
        private readonly Dictionary<string, Sprite> _misc = new Dictionary<string, Sprite>();
        private readonly List<Texture2D> _textures = new List<Texture2D>();

        // ------------------------------------------------------------------ palette

        private static readonly Dictionary<PieceColor, Color> Palette = new Dictionary<PieceColor, Color>
        {
            { PieceColor.Red, new Color(0.91f, 0.27f, 0.24f) },
            { PieceColor.Blue, new Color(0.18f, 0.50f, 0.93f) },
            { PieceColor.Green, new Color(0.20f, 0.78f, 0.35f) },
            { PieceColor.Yellow, new Color(1.00f, 0.77f, 0.05f) },
            { PieceColor.Purple, new Color(0.63f, 0.33f, 0.89f) },
            { PieceColor.Orange, new Color(1.00f, 0.48f, 0.20f) },
        };

        public static Color ColorOf(PieceColor color) =>
            Palette.TryGetValue(color, out Color value) ? value : new Color(0.75f, 0.75f, 0.78f);

        // Warm plum rather than the cold slate-blue this started as: the pieces read as candy, so
        // the chrome around them should feel like a candy shop, not a spreadsheet. Every UI file
        // pulls its greys and near-blacks from here rather than rolling its own, so the whole game
        // reads as one palette instead of six slightly different ones.
        public static readonly Color BoardBackground = new Color(0.24f, 0.15f, 0.22f);
        public static readonly Color CellLight = new Color(1f, 1f, 1f, 0.10f);
        public static readonly Color CellDark = new Color(0f, 0f, 0f, 0.14f);
        public static readonly Color PanelColor = new Color(0.20f, 0.12f, 0.18f, 0.96f);
        public static readonly Color ScreenBackground = new Color(0.15f, 0.09f, 0.13f, 1f);
        public static readonly Color AccentColor = new Color(1f, 0.80f, 0.28f);
        public static readonly Color TextColor = new Color(0.99f, 0.95f, 0.91f);
        public static readonly Color TextMuted = new Color(0.78f, 0.67f, 0.73f);
        public static readonly Color ButtonNeutral = new Color(0.34f, 0.21f, 0.29f, 0.96f);
        public static readonly Color ButtonAccent = new Color(0.86f, 0.40f, 0.32f, 1f);

        // ------------------------------------------------------------------ pieces

        public Sprite Piece(PieceColor color)
        {
            if (_pieces.TryGetValue(color, out Sprite cached))
                return cached;

            Sprite sprite = BuildPiece(color);
            _pieces[color] = sprite;
            return sprite;
        }

        public Sprite Rainbow() => Cached("rainbow", BuildRainbow);

        public Sprite BoosterOverlay(BoosterType type, LineOrientation orientation)
        {
            string key = $"booster.{type}.{orientation}";
            return Cached(key, () => BuildBoosterOverlay(type, orientation));
        }

        public Sprite Crate(string obstacleId, PieceColor requiredColor)
        {
            string key = $"crate.{obstacleId}.{requiredColor}";
            return Cached(key, () => BuildCrate(obstacleId, requiredColor));
        }

        public Sprite Cell(bool dark) =>
            Cached(dark ? "cell.dark" : "cell.light", () => BuildCell(dark));

        public Sprite Glow() => Cached("glow", BuildGlow);

        public Sprite Spark() => Cached("spark", BuildSpark);

        public Sprite Ring() => Cached("ring", BuildRing);

        public Sprite Panel() => Cached("panel", () => BuildPanel(28f));

        public Sprite Pill() => Cached("pill", () => BuildPanel(PanelSize * 0.5f - 1f));

        public Sprite White() => Cached("white", BuildWhite);

        private Sprite Cached(string key, Func<Sprite> factory)
        {
            if (_misc.TryGetValue(key, out Sprite cached))
                return cached;

            Sprite sprite = factory();
            _misc[key] = sprite;
            return sprite;
        }

        /// <summary>Frees every generated texture. Called when the game shuts down.</summary>
        public void Dispose()
        {
            foreach (Texture2D texture in _textures)
            {
                if (texture == null)
                    continue;

                if (Application.isPlaying)
                    UnityEngine.Object.Destroy(texture);
                else
                    UnityEngine.Object.DestroyImmediate(texture);
            }

            _textures.Clear();
            _pieces.Clear();
            _misc.Clear();
        }

        // ------------------------------------------------------------------ drawing

        private delegate Color PixelShader(Vector2 uv, float pixel);

        private Sprite Render(int size, PixelShader shader, float pixelsPerUnit, Vector4 border = default)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };

            var pixels = new Color[size * size];
            float pixel = 2f / size;

            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                // uv spans -1..1 across the sprite, with the centre at the origin.
                var uv = new Vector2((x + 0.5f) / size * 2f - 1f, (y + 0.5f) / size * 2f - 1f);
                pixels[y * size + x] = shader(uv, pixel);
            }

            texture.SetPixels(pixels);
            texture.Apply(false, false);
            _textures.Add(texture);

            var sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f),
                pixelsPerUnit, 0, SpriteMeshType.FullRect, border);
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        private static Func<Vector2, float> ShapeOf(PieceColor color)
        {
            const float r = 0.74f;
            switch (color)
            {
                case PieceColor.Red: return p => ShapeMath.Circle(p, r);
                // Diamond's "radius" is an axis-aligned (L1) reach, not a circumradius like the other
                // shapes here, so it needs a smaller multiplier to end up the same size: at 1.28 the
                // tip lands at uv ~1.14, past the sprite's -1..1 bounds, clipping the corners.
                case PieceColor.Blue: return p => ShapeMath.Diamond(p, r * 1.0f, 0.08f);
                case PieceColor.Green: return p => ShapeMath.Hexagon(p, r * 0.92f, 0.07f);
                case PieceColor.Yellow: return p => ShapeMath.RoundedBox(p, new Vector2(r * 0.90f, r * 0.90f), 0.22f);
                case PieceColor.Purple: return p => ShapeMath.Star(p, r * 1.06f, 0.52f, 0.05f);
                case PieceColor.Orange: return p => ShapeMath.Triangle(p, r * 0.98f, 0.10f);
                default: return p => ShapeMath.Circle(p, r);
            }
        }

        private Sprite BuildPiece(PieceColor color)
        {
            Func<Vector2, float> shape = ShapeOf(color);
            Color baseColor = ColorOf(color);
            Color top = Color.Lerp(baseColor, Color.white, 0.30f);
            Color bottom = Color.Lerp(baseColor, Color.black, 0.22f);
            Color rim = Color.Lerp(baseColor, Color.black, 0.50f);

            return Render(PieceSize, (uv, pixel) =>
            {
                float d = shape(uv);
                float outer = ShapeMath.Coverage(d - 0.055f, pixel);
                if (outer <= 0f)
                    return Color.clear;

                float inner = ShapeMath.Coverage(d, pixel);

                // Vertical gradient so the pieces read as rounded rather than flat.
                Color body = Color.Lerp(bottom, top, Mathf.InverseLerp(-0.85f, 0.85f, uv.y));

                // Soft specular blob up and to the left.
                float highlight = ShapeMath.Coverage(
                    ShapeMath.Circle((uv - new Vector2(-0.24f, 0.34f)) * new Vector2(1f, 1.35f), 0.26f),
                    0.55f);
                body = Color.Lerp(body, Color.white, highlight * 0.45f);

                return ShapeMath.Over(ShapeMath.WithAlpha(body, inner),
                    ShapeMath.WithAlpha(rim, outer));
            }, PieceSize);
        }

        private Sprite BuildRainbow()
        {
            return Render(PieceSize, (uv, pixel) =>
            {
                float d = ShapeMath.Circle(uv, 0.74f);
                float outer = ShapeMath.Coverage(d - 0.055f, pixel);
                if (outer <= 0f)
                    return Color.clear;

                float inner = ShapeMath.Coverage(d, pixel);

                // Hue swept around the orb, so it reads as "any colour".
                float angle = Mathf.Atan2(uv.y, uv.x) / (Mathf.PI * 2f) + 0.5f;
                Color body = Color.HSVToRGB(Mathf.Repeat(angle, 1f), 0.72f, 1f);

                float radial = Mathf.InverseLerp(0.74f, 0.1f, uv.magnitude);
                body = Color.Lerp(body, Color.white, radial * 0.65f);

                float highlight = ShapeMath.Coverage(
                    ShapeMath.Circle(uv - new Vector2(-0.22f, 0.32f), 0.20f), 0.5f);
                body = Color.Lerp(body, Color.white, highlight * 0.5f);

                return ShapeMath.Over(ShapeMath.WithAlpha(body, inner),
                    ShapeMath.WithAlpha(new Color(0.25f, 0.22f, 0.35f), outer));
            }, PieceSize);
        }

        private Sprite BuildBoosterOverlay(BoosterType type, LineOrientation orientation)
        {
            return Render(PieceSize, (uv, pixel) =>
            {
                float d = GlyphDistance(type, orientation, uv);
                float outline = ShapeMath.Coverage(d - 0.055f, pixel);
                if (outline <= 0f)
                    return Color.clear;

                float fill = ShapeMath.Coverage(d, pixel);
                return ShapeMath.Over(ShapeMath.WithAlpha(Color.white, fill),
                    ShapeMath.WithAlpha(new Color(0.10f, 0.10f, 0.16f, 0.85f), outline));
            }, PieceSize);
        }

        private static float GlyphDistance(BoosterType type, LineOrientation orientation, Vector2 uv)
        {
            switch (type)
            {
                case BoosterType.Line:
                {
                    // A double-headed arrow along the direction the booster clears.
                    Vector2 p = orientation == LineOrientation.Horizontal ? uv : new Vector2(uv.y, uv.x);
                    float shaft = ShapeMath.Segment(p, new Vector2(-0.40f, 0f), new Vector2(0.40f, 0f), 0.10f);

                    // The triangle SDF points up, so each head is rotated a quarter turn — and the
                    // two heads rotate opposite ways, or they end up pointing back at each other.
                    Vector2 right = p - new Vector2(0.42f, 0f);
                    Vector2 left = p + new Vector2(0.42f, 0f);
                    const float scale = 2.4f;
                    float headRight = ShapeMath.Triangle(new Vector2(-right.y, right.x) * scale, 0.62f, 0.04f) / scale;
                    float headLeft = ShapeMath.Triangle(new Vector2(left.y, -left.x) * scale, 0.62f, 0.04f) / scale;

                    return Mathf.Min(shaft, Mathf.Min(headRight, headLeft));
                }

                case BoosterType.Bomb:
                {
                    // A starburst: a disc with four spikes.
                    float core = ShapeMath.Circle(uv, 0.30f);
                    float spikeA = ShapeMath.Segment(uv, new Vector2(-0.52f, 0f), new Vector2(0.52f, 0f), 0.055f);
                    float spikeB = ShapeMath.Segment(uv, new Vector2(0f, -0.52f), new Vector2(0f, 0.52f), 0.055f);
                    Vector2 rot = new Vector2(uv.x + uv.y, uv.y - uv.x) * 0.70710678f;
                    float spikeC = ShapeMath.Segment(rot, new Vector2(-0.44f, 0f), new Vector2(0.44f, 0f), 0.045f);
                    float spikeD = ShapeMath.Segment(rot, new Vector2(0f, -0.44f), new Vector2(0f, 0.44f), 0.045f);
                    return Mathf.Min(Mathf.Min(core, spikeA), Mathf.Min(Mathf.Min(spikeB, spikeC), spikeD));
                }

                case BoosterType.Plane:
                {
                    // A paper dart: a triangle with a notch cut out of its tail, which is what
                    // separates it from the plain triangle gem at a glance. Its nose ends up pointing
                    // at ~135 degrees (up-and-left) -- EffectsLayer.AnimateFlight has to know that to
                    // aim it at the flight direction.
                    const float cos = 0.70710678f;
                    Vector2 p = new Vector2(uv.x * cos + uv.y * cos, -uv.x * cos + uv.y * cos);

                    // Slimmer than the triangle gem, so the two never get confused.
                    float body = ShapeMath.Triangle(new Vector2(p.x * 1.30f, p.y) * 1.30f, 0.88f, 0.04f) / 1.30f;

                    // The notch has to reach up past the body's base line to actually cut anything.
                    float notch = ShapeMath.Triangle(new Vector2(p.x * 1.30f, p.y + 0.45f) * 1.7f, 0.72f, 0.02f) / 1.7f;
                    return Mathf.Max(body, -notch);
                }

                default:
                    return 1f;
            }
        }

        private Sprite BuildCrate(string obstacleId, PieceColor requiredColor)
        {
            bool blocker = obstacleId == ObstacleCatalog.Blocker;
            bool cycling = obstacleId == ObstacleCatalog.CyclingBox;
            bool coloured = obstacleId == ObstacleCatalog.ColoredBox || cycling;

            // Blocker stays a cool neutral grey deliberately: against the board's warm plum it reads
            // as "stone, not a piece" at a glance, and it's lighter than the old tone so it doesn't
            // sink into the background the way a similarly-dark blue-grey used to.
            Color wood = blocker
                ? new Color(0.56f, 0.55f, 0.59f)
                : new Color(0.62f, 0.42f, 0.24f);

            Color tint = coloured ? ColorOf(requiredColor) : wood;

            return Render(PieceSize, (uv, pixel) =>
            {
                float d = ShapeMath.RoundedBox(uv, new Vector2(0.80f, 0.80f), blocker ? 0.10f : 0.16f);
                float outer = ShapeMath.Coverage(d - 0.05f, pixel);
                if (outer <= 0f)
                    return Color.clear;

                float inner = ShapeMath.Coverage(d, pixel);

                Color body = Color.Lerp(Color.Lerp(wood, Color.black, 0.18f),
                    Color.Lerp(wood, Color.white, 0.16f),
                    Mathf.InverseLerp(-0.8f, 0.8f, uv.y));

                if (blocker)
                {
                    // Rivets, to read as solid metal rather than an empty box.
                    float rivets = 1f;
                    for (int sx = -1; sx <= 1; sx += 2)
                    for (int sy = -1; sy <= 1; sy += 2)
                        rivets = Mathf.Min(rivets,
                            ShapeMath.Circle(uv - new Vector2(0.5f * sx, 0.5f * sy), 0.10f));

                    body = Color.Lerp(body, Color.Lerp(wood, Color.black, 0.45f),
                        ShapeMath.Coverage(rivets, pixel));
                }
                else
                {
                    // Two plank grooves.
                    float groove = Mathf.Min(
                        Mathf.Abs(uv.y - 0.26f) - 0.035f,
                        Mathf.Abs(uv.y + 0.26f) - 0.035f);
                    body = Color.Lerp(body, Color.Lerp(wood, Color.black, 0.35f),
                        ShapeMath.Coverage(groove, pixel));
                }

                if (coloured)
                {
                    // A gem of the required colour in the middle, so the rule is visible on the board.
                    float gem = ShapeMath.Circle(uv, 0.30f);
                    float gemOuter = ShapeMath.Coverage(gem - 0.05f, pixel);
                    float gemInner = ShapeMath.Coverage(gem, pixel);
                    Color gemColor = Color.Lerp(tint, Color.white, 0.15f);
                    body = Color.Lerp(body, Color.Lerp(tint, Color.black, 0.5f), gemOuter);
                    body = Color.Lerp(body, gemColor, gemInner);

                    if (cycling)
                    {
                        // A ring around the gem hints that its colour will change.
                        float ring = Mathf.Abs(ShapeMath.Circle(uv, 0.44f)) - 0.035f;
                        body = Color.Lerp(body, Color.white, ShapeMath.Coverage(ring, pixel) * 0.85f);
                    }
                }

                return ShapeMath.Over(ShapeMath.WithAlpha(body, inner),
                    ShapeMath.WithAlpha(Color.Lerp(wood, Color.black, 0.6f), outer));
            }, PieceSize);
        }

        private Sprite BuildCell(bool dark)
        {
            Color color = dark ? CellDark : CellLight;

            return Render(PieceSize, (uv, pixel) =>
            {
                float d = ShapeMath.RoundedBox(uv, new Vector2(0.95f, 0.95f), 0.18f);
                return ShapeMath.WithAlpha(color, ShapeMath.Coverage(d, pixel) * color.a);
            }, PieceSize);
        }

        private Sprite BuildGlow()
        {
            return Render(PieceSize, (uv, pixel) =>
            {
                float t = Mathf.Clamp01(1f - uv.magnitude);
                float alpha = t * t * t;
                return new Color(1f, 1f, 1f, alpha);
            }, PieceSize);
        }

        private Sprite BuildSpark()
        {
            return Render(32, (uv, pixel) =>
            {
                float d = ShapeMath.Circle(uv, 0.7f);
                float cov = ShapeMath.Coverage(d, pixel);
                float t = Mathf.Clamp01(1f - uv.magnitude / 0.7f);
                return new Color(1f, 1f, 1f, cov * Mathf.Lerp(0.35f, 1f, t));
            }, 32);
        }

        private Sprite BuildRing()
        {
            return Render(PieceSize, (uv, pixel) =>
            {
                float d = ShapeMath.Ring(uv, 0.78f, 0.14f);
                float cov = ShapeMath.Coverage(d, pixel);
                return new Color(1f, 1f, 1f, cov);
            }, PieceSize);
        }

        /// <summary>Rounded panel for UI, generated with 9-slice borders so it scales cleanly.</summary>
        private Sprite BuildPanel(float cornerPixels)
        {
            float radius = cornerPixels / (PanelSize * 0.5f);

            Sprite sprite = Render(PanelSize, (uv, pixel) =>
            {
                float d = ShapeMath.RoundedBox(uv, Vector2.one, radius);
                return new Color(1f, 1f, 1f, ShapeMath.Coverage(d, pixel));
            }, PanelSize, new Vector4(cornerPixels, cornerPixels, cornerPixels, cornerPixels));

            return sprite;
        }

        private Sprite BuildWhite() => Render(4, (uv, pixel) => Color.white, 4);
    }
}
