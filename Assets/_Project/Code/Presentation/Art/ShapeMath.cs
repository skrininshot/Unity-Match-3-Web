using UnityEngine;

namespace Match3.Presentation
{
    /// <summary>
    /// Signed distance functions used to draw the game's sprites.
    /// <para>
    /// Distances are in the same units as the sample point (roughly -1..1 across a sprite), and
    /// negative means inside. Drawing from distances rather than plotting pixels is what gives the
    /// shapes clean antialiased edges at any resolution, and makes outlines a one-line change.
    /// </para>
    /// </summary>
    public static class ShapeMath
    {
        public static float Circle(Vector2 p, float radius) => p.magnitude - radius;

        /// <summary>A circle outline of the given thickness — the shape behind expanding shockwaves.</summary>
        public static float Ring(Vector2 p, float radius, float thickness) =>
            Mathf.Abs(Circle(p, radius)) - thickness;

        public static float RoundedBox(Vector2 p, Vector2 halfSize, float radius)
        {
            Vector2 d = new Vector2(Mathf.Abs(p.x), Mathf.Abs(p.y)) - halfSize + Vector2.one * radius;
            float outside = new Vector2(Mathf.Max(d.x, 0f), Mathf.Max(d.y, 0f)).magnitude;
            float inside = Mathf.Min(Mathf.Max(d.x, d.y), 0f);
            return outside + inside - radius;
        }

        /// <summary>Diamond: a square rotated by 45 degrees, rounded by <paramref name="round"/>.</summary>
        public static float Diamond(Vector2 p, float radius, float round)
        {
            float d = (Mathf.Abs(p.x) + Mathf.Abs(p.y) - radius) * 0.70710678f;
            return d - round;
        }

        public static float Hexagon(Vector2 p, float radius, float round)
        {
            Vector2 k = new Vector2(-0.866025f, 0.5f);
            p = new Vector2(Mathf.Abs(p.x), Mathf.Abs(p.y));
            float dot = Mathf.Min(k.x * p.x + k.y * p.y, 0f);
            p -= 2f * dot * k;
            p -= new Vector2(Mathf.Clamp(p.x, -0.577350f * radius, 0.577350f * radius), radius);
            return p.magnitude * Mathf.Sign(p.y) - round;
        }

        public static float Triangle(Vector2 p, float radius, float round)
        {
            const float k = 1.7320508f; // sqrt(3)
            p = new Vector2(Mathf.Abs(p.x) - radius, p.y + radius / k);
            if (p.x + k * p.y > 0f)
                p = new Vector2(p.x - k * p.y, -k * p.x - p.y) / 2f;
            p.x -= Mathf.Clamp(p.x, -2f * radius, 0f);
            return -p.magnitude * Mathf.Sign(p.y) - round;
        }

        /// <summary>Five-pointed star, drawn as a polar radius test with rounded tips.</summary>
        public static float Star(Vector2 p, float radius, float innerRatio, float round)
        {
            float angle = Mathf.Atan2(p.y, p.x);
            float sector = Mathf.PI * 2f / 5f;

            // Fold the plane into one star point and measure against a straight edge.
            float a = Mathf.Repeat(angle + Mathf.PI / 2f, sector) - sector * 0.5f;
            float r = p.magnitude;
            float outer = radius;
            float inner = radius * innerRatio;

            float edge = Mathf.Lerp(inner, outer, 1f - Mathf.Abs(a) / (sector * 0.5f));
            return (r - edge) * 0.6f - round;
        }

        /// <summary>Axis-aligned capsule, used for the arrow shafts on the Line booster.</summary>
        public static float Segment(Vector2 p, Vector2 a, Vector2 b, float radius)
        {
            Vector2 pa = p - a;
            Vector2 ba = b - a;
            float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / Vector2.Dot(ba, ba));
            return (pa - ba * h).magnitude - radius;
        }

        /// <summary>Coverage of a shape at a given distance, antialiased over one pixel.</summary>
        public static float Coverage(float distance, float pixel) =>
            Mathf.Clamp01(0.5f - distance / Mathf.Max(pixel, 1e-6f));

        public static Color Over(Color top, Color bottom)
        {
            float a = top.a + bottom.a * (1f - top.a);
            if (a <= 0f)
                return Color.clear;

            Vector3 rgb = (new Vector3(top.r, top.g, top.b) * top.a
                           + new Vector3(bottom.r, bottom.g, bottom.b) * bottom.a * (1f - top.a)) / a;
            return new Color(rgb.x, rgb.y, rgb.z, a);
        }

        public static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }
    }
}
