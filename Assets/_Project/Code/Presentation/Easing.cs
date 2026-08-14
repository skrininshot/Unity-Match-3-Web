using UnityEngine;

namespace Match3.Presentation
{
    /// <summary>
    /// Easing curves for the hand-rolled tweens.
    /// A tweening package would be one more dependency to justify for a handful of curves, and
    /// coroutines cover everything this game animates.
    /// </summary>
    public static class Easing
    {
        public delegate float Curve(float t);

        public static float Linear(float t) => t;

        public static float QuadOut(float t) => 1f - (1f - t) * (1f - t);

        public static float CubicOut(float t)
        {
            float inv = 1f - t;
            return 1f - inv * inv * inv;
        }

        public static float CubicIn(float t) => t * t * t;

        public static float QuadInOut(float t) =>
            t < 0.5f ? 2f * t * t : 1f - 2f * (1f - t) * (1f - t);

        /// <summary>Overshoots slightly and settles back — used for swaps and pop-ins.</summary>
        public static float BackOut(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float inv = t - 1f;
            return 1f + c3 * inv * inv * inv + c1 * inv * inv;
        }

        /// <summary>A short bounce at the end, which is what makes falling pieces feel weighty.</summary>
        public static float BounceOut(float t)
        {
            const float n1 = 7.5625f;
            const float d1 = 2.75f;

            if (t < 1f / d1)
                return n1 * t * t;

            if (t < 2f / d1)
            {
                t -= 1.5f / d1;
                return n1 * t * t + 0.75f;
            }

            if (t < 2.5f / d1)
            {
                t -= 2.25f / d1;
                return n1 * t * t + 0.9375f;
            }

            t -= 2.625f / d1;
            return n1 * t * t + 0.984375f;
        }

        /// <summary>Falling: accelerate in, then a small bounce on landing.</summary>
        public static float FallCurve(float t)
        {
            const float impact = 0.78f;
            if (t < impact)
            {
                float k = t / impact;
                return k * k;
            }

            float rest = (t - impact) / (1f - impact);
            // A single small overshoot and settle.
            return 1f + 0.06f * Mathf.Sin(rest * Mathf.PI) * (1f - rest);
        }
    }
}
