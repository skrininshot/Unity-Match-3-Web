using System.Collections;
using System.Collections.Generic;
using Match3.Core;
using UnityEngine;

namespace Match3.Presentation
{
    /// <summary>
    /// Transient visuals for booster activations and destructions.
    /// Every effect reports how long it lasts, so the board can wait exactly as long as it needs to
    /// rather than guessing a timing that drifts out of sync with the logic.
    /// </summary>
    public sealed class EffectsLayer : MonoBehaviour
    {
        private const int SortingOrder = 40;

        private SpriteLibrary _sprites;
        private System.Func<GridPos, Vector3> _cellToLocal;

        public static EffectsLayer Create(Transform parent, SpriteLibrary sprites,
            System.Func<GridPos, Vector3> cellToLocal)
        {
            var go = new GameObject("effects");
            go.transform.SetParent(parent, false);

            var layer = go.AddComponent<EffectsLayer>();
            layer._sprites = sprites;
            layer._cellToLocal = cellToLocal;
            return layer;
        }

        /// <summary>
        /// Splits a booster's timing into the two questions the board actually needs answered: when
        /// should the pieces it hits start disappearing (<see cref="ImpactDelay"/>), and when is it
        /// safe to move on (<see cref="TotalDuration"/>). They used to be one number derived from the
        /// other by a fixed percentage, which was fine for a quick flash but silently popped the
        /// plane's target before the plane had actually arrived.
        /// </summary>
        public readonly struct EffectTiming
        {
            public readonly float ImpactDelay;
            public readonly float TotalDuration;

            public EffectTiming(float impactDelay, float totalDuration)
            {
                ImpactDelay = impactDelay;
                TotalDuration = totalDuration;
            }

            public static readonly EffectTiming None = new EffectTiming(0f, 0f);
        }

        /// <summary>Plays the visual for one activation. Returns its timing (see <see cref="EffectTiming"/>).</summary>
        public EffectTiming PlayActivation(BoosterActivatedEvent activation)
        {
            switch (activation.Type)
            {
                case BoosterType.Line:
                    return PlayLine(activation);
                case BoosterType.Bomb:
                    return PlayBomb(activation);
                case BoosterType.Rainbow:
                    return PlayRainbow(activation);
                case BoosterType.Plane:
                    return PlayPlane(activation);
                default:
                    return EffectTiming.None;
            }
        }

        private EffectTiming PlayLine(BoosterActivatedEvent activation)
        {
            const float duration = 0.36f;
            const float impactDelay = 0.14f;

            // One stretched streak per covered row or column.
            var lines = new HashSet<int>();
            foreach (GridPos cell in activation.Affected)
                lines.Add(activation.Orientation == LineOrientation.Horizontal ? cell.Y : cell.X);

            foreach (int line in lines)
            {
                bool horizontal = activation.Orientation == LineOrientation.Horizontal;
                Vector3 centre = _cellToLocal(horizontal
                    ? new GridPos(activation.At.X, line)
                    : new GridPos(line, activation.At.Y));

                SpriteRenderer streak = Spawn(_sprites.Glow(), centre, SortingOrder);
                streak.color = new Color(1f, 0.96f, 0.78f, 0.95f);
                StartCoroutine(AnimateStreak(streak, horizontal, duration));
            }

            SpawnSparks(activation.At, 16, duration, new Color(1f, 0.93f, 0.7f));
            return new EffectTiming(impactDelay, duration);
        }

        private IEnumerator AnimateStreak(SpriteRenderer streak, bool horizontal, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                float length = Mathf.Lerp(2f, 40f, Easing.CubicOut(t));
                // A chunkier beam at launch reads as a solid bolt rather than a thin scratch.
                float thickness = Mathf.Lerp(1.5f, 0.4f, t);

                streak.transform.localScale = horizontal
                    ? new Vector3(length, thickness, 1f)
                    : new Vector3(thickness, length, 1f);

                SetAlpha(streak, 1f - Easing.QuadOut(t));
                yield return null;
            }

            Destroy(streak.gameObject);
        }

        private EffectTiming PlayBomb(BoosterActivatedEvent activation)
        {
            const float duration = 0.46f;
            const float impactDelay = 0.12f;

            float radius = 1f;
            foreach (GridPos cell in activation.Affected)
                radius = Mathf.Max(radius,
                    Mathf.Max(Mathf.Abs(cell.X - activation.At.X), Mathf.Abs(cell.Y - activation.At.Y)));

            Vector3 centre = _cellToLocal(activation.At);
            float span = radius * 2f + 1.8f;

            // A hot white core that flares and dies fast, under a slower warm bloom -- two flashes
            // stacked read as far punchier than either alone, which is the whole point of a bomb.
            SpriteRenderer core = Spawn(_sprites.Glow(), centre, SortingOrder + 2);
            core.color = new Color(1f, 0.97f, 0.88f, 1f);
            StartCoroutine(AnimateExpand(core, span * 0.6f, duration * 0.45f));

            SpriteRenderer flash = Spawn(_sprites.Glow(), centre, SortingOrder);
            flash.color = new Color(1f, 0.6f, 0.22f, 1f);
            StartCoroutine(AnimateExpand(flash, span, duration));

            SpriteRenderer ring = Spawn(_sprites.Ring(), centre, SortingOrder + 3);
            ring.color = new Color(1f, 0.86f, 0.6f, 0.95f);
            StartCoroutine(AnimateRing(ring, span * 1.2f, duration * 0.8f));

            SpawnSparks(activation.At, 28, duration, new Color(1f, 0.75f, 0.35f), Mathf.Max(1f, radius * 0.9f));
            return new EffectTiming(impactDelay, duration);
        }

        private IEnumerator AnimateExpand(SpriteRenderer glow, float finalSize, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float size = Mathf.Lerp(0.4f, finalSize, Easing.CubicOut(t));
                glow.transform.localScale = new Vector3(size, size, 1f);
                SetAlpha(glow, 1f - Easing.QuadInOut(t));
                yield return null;
            }

            Destroy(glow.gameObject);
        }

        private IEnumerator AnimateRing(SpriteRenderer ring, float finalSize, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float size = Mathf.Lerp(0.3f, finalSize, Easing.CubicOut(t));
                ring.transform.localScale = new Vector3(size, size, 1f);
                SetAlpha(ring, 1f - Easing.QuadInOut(t));
                yield return null;
            }

            Destroy(ring.gameObject);
        }

        private EffectTiming PlayRainbow(BoosterActivatedEvent activation)
        {
            const float duration = 0.46f;
            Color tint = activation.TargetColor == PieceColor.None
                ? Color.white
                : SpriteLibrary.ColorOf(activation.TargetColor);

            foreach (GridPos cell in activation.Affected)
            {
                // Stagger by distance so the wipe reads as a wave travelling outwards.
                float distance = Vector2.Distance(new Vector2(cell.X, cell.Y),
                    new Vector2(activation.At.X, activation.At.Y));
                float delay = Mathf.Min(distance * 0.028f, 0.22f);

                SpriteRenderer glow = Spawn(_sprites.Glow(), _cellToLocal(cell), SortingOrder);
                glow.color = tint;
                StartCoroutine(AnimateDelayedPulse(glow, delay, duration - delay));
            }

            float impactDelay = Mathf.Min(duration * 0.45f, 0.2f);
            return new EffectTiming(impactDelay, duration);
        }

        private IEnumerator AnimateDelayedPulse(SpriteRenderer glow, float delay, float duration)
        {
            SetAlpha(glow, 0f);
            if (delay > 0f)
                yield return new WaitForSeconds(delay);

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float size = Mathf.Lerp(0.5f, 2.1f, Easing.CubicOut(t));
                glow.transform.localScale = new Vector3(size, size, 1f);
                SetAlpha(glow, Mathf.Sin(t * Mathf.PI) * 0.95f);
                yield return null;
            }

            Destroy(glow.gameObject);
        }

        private EffectTiming PlayPlane(BoosterActivatedEvent activation)
        {
            if (activation.FlyTo == null)
                return EffectTiming.None;

            const float flightDuration = 0.52f;
            const float impactDuration = 0.32f;

            SpriteRenderer plane = Spawn(
                _sprites.BoosterOverlay(BoosterType.Plane, LineOrientation.Horizontal),
                _cellToLocal(activation.At), SortingOrder + 5);
            plane.color = Color.white;

            StartCoroutine(AnimateFlight(plane, _cellToLocal(activation.At),
                _cellToLocal(activation.FlyTo.Value), flightDuration, impactDuration));

            // The target doesn't blow up until the plane is actually there, and the board can't
            // move on until the explosion that follows has finished playing.
            return new EffectTiming(flightDuration, flightDuration + impactDuration);
        }

        // The dart's own nose points at ~135 degrees (up-and-left) in its unrotated art, not the 45
        // degrees a since-fixed comment on the shape code assumed. Verified empirically by scanning
        // the sprite's SDF for its farthest point from the pivot. Subtracting this out of the facing
        // angle is what makes it fly nose-first instead of sideways.
        private const float PlaneNoseOffsetDeg = -135f;

        private IEnumerator AnimateFlight(SpriteRenderer plane, Vector3 from, Vector3 to, float duration,
            float impactDuration)
        {
            plane.transform.localScale = Vector3.one * 0.8f;

            Vector3 delta = to - from;
            float distance = Mathf.Max(delta.magnitude, 0.01f);
            Vector3 forward = delta / distance;
            Vector3 side = new Vector3(-forward.y, forward.x, 0f);
            float hookSign = Random.value < 0.5f ? -1f : 1f;

            // A wide loop away from the target before diving in, sized off the flight distance so a
            // short hop still gets a visible wind-up without a long haul looking absurd. This is the
            // "bigger hook" a flat sine-arc couldn't give: a real banked loop, not a gentle bulge.
            float hookOut = Mathf.Clamp(distance * 0.6f, 1f, 2.6f);
            float hookUp = Mathf.Clamp(distance * 0.55f, 0.9f, 2.2f);
            Vector3 control1 = from + side * (hookOut * hookSign) + Vector3.up * hookUp;
            Vector3 control2 = to + Vector3.up * (hookUp * 0.3f) - forward * (distance * 0.18f);

            Vector3 PositionAt(float t)
            {
                float e = Easing.QuadInOut(t);
                float u = 1f - e;
                return u * u * u * from + 3f * u * u * e * control1 + 3f * u * e * e * control2 + e * e * e * to;
            }

            const float tangentStep = 0.02f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                plane.transform.localPosition = PositionAt(t);

                // Face where the curve is actually heading, sampled just behind and ahead of us, so
                // the nose visibly banks through the loop instead of holding a fixed launch angle.
                Vector3 tangent = PositionAt(Mathf.Min(1f, t + tangentStep))
                                   - PositionAt(Mathf.Max(0f, t - tangentStep));
                if (tangent.sqrMagnitude > 0.0001f)
                {
                    float angle = Mathf.Atan2(tangent.y, tangent.x) * Mathf.Rad2Deg + PlaneNoseOffsetDeg;
                    plane.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
                }

                yield return null;
            }

            plane.transform.localPosition = to;
            PlayImpactBurst(to);
            StartCoroutine(AnimateImpactPop(plane));
        }

        /// <summary>
        /// The plane's own body on landing: a fast squash-and-fade instead of the old slow fade
        /// spread across the last 15% of the flight, so it reads as hitting something rather than
        /// just quietly disappearing mid-air.
        /// </summary>
        private IEnumerator AnimateImpactPop(SpriteRenderer plane)
        {
            const float duration = 0.12f;
            Vector3 startScale = plane.transform.localScale;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                plane.transform.localScale = startScale * Mathf.Lerp(1f, 1.5f, Easing.CubicOut(t));
                SetAlpha(plane, 1f - Easing.CubicIn(t));
                yield return null;
            }

            Destroy(plane.gameObject);
        }

        /// <summary>The explosion where the plane lands — previously nothing, which is why the hit was unreadable.</summary>
        private void PlayImpactBurst(Vector3 localPosition)
        {
            const float duration = 0.3f;
            var tint = new Color(1f, 0.55f, 0.28f);

            SpriteRenderer flash = Spawn(_sprites.Glow(), localPosition, SortingOrder + 4);
            flash.color = new Color(1f, 0.92f, 0.78f, 1f);
            StartCoroutine(AnimateExpand(flash, 2.2f, duration * 0.75f));

            SpriteRenderer ring = Spawn(_sprites.Ring(), localPosition, SortingOrder + 3);
            ring.color = tint;
            StartCoroutine(AnimateRing(ring, 2.6f, duration));

            SpawnSparksAt(localPosition, 18, tint, duration + 0.1f, 1.5f);
        }

        /// <summary>Punchy little burst where something was destroyed — the everyday "pop" for a match or a blast.</summary>
        public void PlayBurst(GridPos cell, Color color, int count = 12)
        {
            Vector3 origin = _cellToLocal(cell);

            // A flash and a ring under the sparks are what turn "the piece faded out" into "the piece
            // popped" -- sparks alone read as confetti drifting off, not an actual pop.
            SpriteRenderer flash = Spawn(_sprites.Glow(), origin, SortingOrder);
            flash.color = Color.Lerp(color, Color.white, 0.6f);
            StartCoroutine(AnimateExpand(flash, 1.5f, 0.2f));

            SpriteRenderer ring = Spawn(_sprites.Ring(), origin, SortingOrder + 1);
            ring.color = color;
            StartCoroutine(AnimateRing(ring, 1.7f, 0.26f));

            SpawnSparksAt(origin, count, color, 0.42f, 1.1f);
        }

        private void SpawnSparks(GridPos cell, int count, float duration, Color? tint = null,
            float spreadScale = 1f) =>
            SpawnSparksAt(_cellToLocal(cell), count, tint ?? new Color(1f, 0.93f, 0.7f), duration, spreadScale);

        private void SpawnSparksAt(Vector3 origin, int count, Color tint, float duration, float spreadScale = 1f)
        {
            for (int i = 0; i < count; i++)
            {
                SpriteRenderer spark = Spawn(_sprites.Spark(), origin, SortingOrder + 1);
                spark.color = tint;

                // Evenly spaced with a little jitter reads as a deliberate burst; fully random angles
                // tend to clump and leave visible gaps.
                float angle = (i / (float)count) * Mathf.PI * 2f + Random.value * 0.5f;
                var direction = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
                StartCoroutine(AnimateSpark(spark, origin, direction, Random.Range(0.4f, 0.9f) * spreadScale,
                    duration));
            }
        }

        private IEnumerator AnimateSpark(SpriteRenderer spark, Vector3 origin, Vector3 direction,
            float distance, float duration)
        {
            float elapsed = 0f;
            float size = Random.Range(0.14f, 0.28f);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                spark.transform.localPosition = origin + direction * (distance * Easing.CubicOut(t))
                                                       + Vector3.down * (t * t * 0.5f);
                spark.transform.localScale = Vector3.one * (size * (1f - t * 0.7f));
                SetAlpha(spark, 1f - t);
                yield return null;
            }

            Destroy(spark.gameObject);
        }

        private SpriteRenderer Spawn(Sprite sprite, Vector3 localPosition, int sortingOrder)
        {
            var go = new GameObject("fx");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = localPosition;

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = sortingOrder;
            return renderer;
        }

        private static void SetAlpha(SpriteRenderer renderer, float alpha)
        {
            Color color = renderer.color;
            renderer.color = new Color(color.r, color.g, color.b, Mathf.Clamp01(alpha));
        }
    }
}
