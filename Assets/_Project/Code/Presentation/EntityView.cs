using System.Collections;
using System.Collections.Generic;
using Match3.Core;
using UnityEngine;

namespace Match3.Presentation
{
    /// <summary>
    /// The visual for one board entity. Owned by <see cref="BoardView"/> and addressed by the
    /// entity id from the core, which is why a piece keeps its identity through swaps, falls,
    /// cascades and being promoted to a booster.
    /// </summary>
    public sealed class EntityView : MonoBehaviour
    {
        private const int BaseSortingOrder = 0;

        private SpriteRenderer _main;
        private SpriteRenderer _overlay;
        private readonly List<SpriteRenderer> _pips = new List<SpriteRenderer>();

        private SpriteLibrary _sprites;

        public long Id { get; private set; }
        public EntitySnapshot Snapshot { get; private set; }

        public static EntityView Create(Transform parent, SpriteLibrary sprites, EntitySnapshot snapshot)
        {
            var go = new GameObject($"entity-{snapshot.Id}");
            go.transform.SetParent(parent, false);

            var view = go.AddComponent<EntityView>();
            view._sprites = sprites;
            view.Id = snapshot.Id;

            view._main = go.AddComponent<SpriteRenderer>();
            view._main.sortingOrder = BaseSortingOrder;

            var overlayGo = new GameObject("glyph");
            overlayGo.transform.SetParent(go.transform, false);
            view._overlay = overlayGo.AddComponent<SpriteRenderer>();
            view._overlay.sortingOrder = BaseSortingOrder + 1;
            view._overlay.enabled = false;

            view.Apply(snapshot);
            return view;
        }

        /// <summary>Rebuilds the visuals from a snapshot. Used on creation and on promotion.</summary>
        public void Apply(EntitySnapshot snapshot)
        {
            Snapshot = snapshot;

            if (snapshot.IsPiece)
                ApplyPiece(snapshot);
            else
                ApplyObstacle(snapshot);

            // Multi-cell entities cover several cells, so their sprite is scaled to match.
            transform.localScale = new Vector3(snapshot.Width, snapshot.Height, 1f);
        }

        private void ApplyPiece(EntitySnapshot snapshot)
        {
            _main.sprite = snapshot.Booster == BoosterType.Rainbow
                ? _sprites.Rainbow()
                : _sprites.Piece(snapshot.Color);
            _main.color = Color.white;

            bool hasGlyph = snapshot.Booster != BoosterType.None
                            && snapshot.Booster != BoosterType.Rainbow;

            _overlay.enabled = hasGlyph;
            if (hasGlyph)
            {
                _overlay.sprite = _sprites.BoosterOverlay(snapshot.Booster, snapshot.Orientation);
                _overlay.color = Color.white;
                _overlay.transform.localScale = Vector3.one * 0.62f;
            }

            SetPips(0, 0);
        }

        private void ApplyObstacle(EntitySnapshot snapshot)
        {
            _main.sprite = _sprites.Crate(snapshot.ObstacleId, snapshot.RequiredColor);
            _main.color = Color.white;
            _overlay.enabled = false;

            SetPips(snapshot.Hp, snapshot.MaxHp);
        }

        /// <summary>
        /// Remaining lives, drawn as pips under the crate. Without this the player cannot tell a
        /// three-hit crate from a one-hit crate until they have wasted moves finding out.
        /// </summary>
        public void SetPips(int hp, int maxHp)
        {
            int wanted = maxHp > 1 ? maxHp : 0;

            while (_pips.Count < wanted)
            {
                var go = new GameObject($"pip-{_pips.Count}");
                go.transform.SetParent(transform, false);
                var renderer = go.AddComponent<SpriteRenderer>();
                renderer.sprite = _sprites.Spark();
                renderer.sortingOrder = BaseSortingOrder + 2;
                _pips.Add(renderer);
            }

            for (int i = 0; i < _pips.Count; i++)
            {
                bool visible = i < wanted;
                _pips[i].enabled = visible;
                if (!visible)
                    continue;

                float spacing = 0.16f;
                float offset = (wanted - 1) * spacing * 0.5f;
                _pips[i].transform.localPosition = new Vector3(i * spacing - offset, -0.34f, 0f);
                _pips[i].transform.localScale = Vector3.one * 0.16f;
                _pips[i].color = i < hp
                    ? new Color(1f, 1f, 1f, 0.95f)
                    : new Color(0f, 0f, 0f, 0.45f);
            }
        }

        public void SetPosition(Vector3 localPosition) => transform.localPosition = localPosition;

        public void SetSortingOrder(int order)
        {
            _main.sortingOrder = order;
            _overlay.sortingOrder = order + 1;
            foreach (SpriteRenderer pip in _pips)
                pip.sortingOrder = order + 2;
        }

        public IEnumerator MoveTo(Vector3 target, float duration, Easing.Curve curve)
        {
            Vector3 start = transform.localPosition;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                transform.localPosition = Vector3.LerpUnclamped(start, target, curve(t));
                yield return null;
            }

            transform.localPosition = target;
        }

        /// <summary>Shrink and fade — the removal effect that must finish before anything falls.</summary>
        public IEnumerator Vanish(float duration)
        {
            Vector3 startScale = transform.localScale;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                // A tiny swell before collapsing reads as a pop rather than a fade-out.
                float scale = t < 0.25f
                    ? Mathf.Lerp(1f, 1.18f, t / 0.25f)
                    : Mathf.Lerp(1.18f, 0f, (t - 0.25f) / 0.75f);

                transform.localScale = startScale * scale;
                SetAlpha(1f - Easing.CubicIn(t));
                yield return null;
            }

            transform.localScale = Vector3.zero;
            SetAlpha(0f);
        }

        public IEnumerator PopIn(float duration)
        {
            Vector3 target = new Vector3(Snapshot.Width, Snapshot.Height, 1f);
            float elapsed = 0f;
            transform.localScale = Vector3.zero;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                transform.localScale = target * Easing.BackOut(t);
                yield return null;
            }

            transform.localScale = target;
        }

        /// <summary>Quick shake, for a crate that took a hit but survived.</summary>
        public IEnumerator Shake(float duration, float amplitude)
        {
            Vector3 origin = transform.localPosition;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float damping = 1f - t;
                float offset = Mathf.Sin(t * Mathf.PI * 8f) * amplitude * damping;
                transform.localPosition = origin + new Vector3(offset, 0f, 0f);
                yield return null;
            }

            transform.localPosition = origin;
        }

        /// <summary>Scale punch used when a match promotes a piece into a booster.</summary>
        public IEnumerator Promote(EntitySnapshot snapshot, float duration)
        {
            Apply(snapshot);

            Vector3 target = new Vector3(snapshot.Width, snapshot.Height, 1f);
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float punch = 1f + 0.45f * Mathf.Sin(t * Mathf.PI);
                transform.localScale = target * punch;
                yield return null;
            }

            transform.localScale = target;
        }

        public void SetAlpha(float alpha)
        {
            SetRendererAlpha(_main, alpha);
            SetRendererAlpha(_overlay, alpha);
            foreach (SpriteRenderer pip in _pips)
                SetRendererAlpha(pip, alpha);
        }

        private static void SetRendererAlpha(SpriteRenderer renderer, float alpha)
        {
            if (renderer == null)
                return;

            Color color = renderer.color;
            renderer.color = new Color(color.r, color.g, color.b, alpha);
        }
    }
}
