using System.Collections;
using System.Collections.Generic;
using Match3.Core;
using UnityEngine;

namespace Match3.Presentation
{
    /// <summary>
    /// Draws the board and replays a <see cref="TurnResult"/> phase by phase.
    /// <para>
    /// It never reads live core state while animating: everything it shows comes from the events in
    /// the result. That is what keeps the picture consistent with what the player is being told,
    /// even though the logic finished the whole cascade before the first piece moved.
    /// </para>
    /// </summary>
    public sealed class BoardView : MonoBehaviour
    {
        // Timings. Kept together because they are the feel of the game.
        public const float SwapDuration = 0.16f;
        public const float RevertDuration = 0.15f;
        public const float ClearDuration = 0.24f;
        public const float PromoteDuration = 0.26f;
        public const float ShuffleDuration = 0.40f;
        public const float FallBaseDuration = 0.13f;
        public const float FallPerCellDuration = 0.045f;
        public const float FallMaxDuration = 0.52f;

        private readonly Dictionary<long, EntityView> _views = new Dictionary<long, EntityView>();
        private readonly List<EntityView> _fading = new List<EntityView>();

        private SpriteLibrary _sprites;
        private Transform _cellRoot;
        private Transform _entityRoot;
        private EffectsLayer _effects;

        public int Width { get; private set; }
        public int Height { get; private set; }

        public static BoardView Create(Transform parent, SpriteLibrary sprites)
        {
            var go = new GameObject("board");
            go.transform.SetParent(parent, false);

            var view = go.AddComponent<BoardView>();
            view._sprites = sprites;

            view._cellRoot = new GameObject("cells").transform;
            view._cellRoot.SetParent(go.transform, false);

            view._entityRoot = new GameObject("entities").transform;
            view._entityRoot.SetParent(go.transform, false);

            view._effects = EffectsLayer.Create(go.transform, sprites, view.CellToLocal);
            return view;
        }

        /// <summary>Local position of a cell centre, with the board centred on its own origin.</summary>
        public Vector3 CellToLocal(GridPos pos) =>
            new Vector3(pos.X - (Width - 1) * 0.5f, pos.Y - (Height - 1) * 0.5f, 0f);

        /// <summary>Anchor position of a possibly multi-cell entity, which draws from its centre.</summary>
        private Vector3 AnchorToLocal(GridPos anchor, int width, int height) =>
            CellToLocal(anchor) + new Vector3((width - 1) * 0.5f, (height - 1) * 0.5f, 0f);

        public bool WorldToCell(Vector3 world, out GridPos cell)
        {
            Vector3 local = transform.InverseTransformPoint(world);
            int x = Mathf.RoundToInt(local.x + (Width - 1) * 0.5f);
            int y = Mathf.RoundToInt(local.y + (Height - 1) * 0.5f);

            cell = new GridPos(x, y);
            return x >= 0 && x < Width && y >= 0 && y < Height;
        }

        // ------------------------------------------------------------------ building

        public void Build(Board board, IReadOnlyList<EntitySnapshot> snapshots)
        {
            ClearAll();

            Width = board.Width;
            Height = board.Height;

            BuildCells(board);

            foreach (EntitySnapshot snapshot in snapshots)
                CreateView(snapshot);
        }

        private void BuildCells(Board board)
        {
            foreach (GridPos pos in board.Positions)
            {
                if (!board.IsPlayable(pos))
                    continue;

                var go = new GameObject($"cell-{pos.X}-{pos.Y}");
                go.transform.SetParent(_cellRoot, false);
                go.transform.localPosition = CellToLocal(pos);

                var renderer = go.AddComponent<SpriteRenderer>();
                renderer.sprite = _sprites.Cell((pos.X + pos.Y) % 2 == 0);
                renderer.sortingOrder = -10;
            }
        }

        public void ClearAll()
        {
            foreach (EntityView view in _views.Values)
                if (view != null)
                    Destroy(view.gameObject);
            _views.Clear();

            foreach (EntityView view in _fading)
                if (view != null)
                    Destroy(view.gameObject);
            _fading.Clear();

            for (int i = _cellRoot.childCount - 1; i >= 0; i--)
                Destroy(_cellRoot.GetChild(i).gameObject);
        }

        private EntityView CreateView(EntitySnapshot snapshot)
        {
            EntityView view = EntityView.Create(_entityRoot, _sprites, snapshot);
            view.SetPosition(AnchorToLocal(snapshot.Anchor, snapshot.Width, snapshot.Height));
            _views[snapshot.Id] = view;
            return view;
        }

        private EntityView Find(long id) => _views.TryGetValue(id, out EntityView view) ? view : null;

        // ------------------------------------------------------------------ playback

        public IEnumerator PlayTurn(TurnResult result)
        {
            foreach (TurnPhase phase in result.Phases)
                yield return PlayPhase(phase);
        }

        public IEnumerator PlayPhase(TurnPhase phase)
        {
            switch (phase.Kind)
            {
                case PhaseKind.Swap:
                    yield return PlayMoves(phase, SwapDuration, Easing.QuadInOut, raiseMoved: true);
                    break;

                case PhaseKind.SwapRevert:
                    yield return PlayMoves(phase, RevertDuration, Easing.QuadInOut, raiseMoved: true);
                    break;

                case PhaseKind.Shuffle:
                    yield return PlayMoves(phase, ShuffleDuration, Easing.QuadInOut, raiseMoved: false);
                    break;

                case PhaseKind.Clear:
                    yield return PlayClear(phase);
                    break;

                case PhaseKind.Fall:
                    yield return PlayFall(phase);
                    break;

                case PhaseKind.Outcome:
                    // Purely bookkeeping; the HUD listens to these separately.
                    break;
            }
        }

        private IEnumerator PlayMoves(TurnPhase phase, float duration, Easing.Curve curve, bool raiseMoved)
        {
            bool any = false;

            foreach (BoardEvent evt in phase.Events)
            {
                if (!(evt is EntityMovedEvent moved))
                    continue;

                EntityView view = Find(moved.Id);
                if (view == null)
                    continue;

                // Lift the swapped pieces above their neighbours so they cross cleanly.
                if (raiseMoved)
                    view.SetSortingOrder(5);

                StartCoroutine(view.MoveTo(CellToLocal(moved.To), duration, curve));
                any = true;
            }

            if (!any)
                yield break;

            yield return new WaitForSeconds(duration);

            if (raiseMoved)
                foreach (BoardEvent evt in phase.Events)
                    if (evt is EntityMovedEvent moved)
                        Find(moved.Id)?.SetSortingOrder(0);
        }

        private IEnumerator PlayClear(TurnPhase phase)
        {
            var timing = EffectsLayer.EffectTiming.None;

            // Booster effects lead, so the blast is visible -- and, for the plane, actually
            // arrived -- before its victims disappear.
            foreach (BoardEvent evt in phase.Events)
                if (evt is BoosterActivatedEvent activation)
                {
                    EffectsLayer.EffectTiming t = _effects.PlayActivation(activation);
                    timing = new EffectsLayer.EffectTiming(
                        Mathf.Max(timing.ImpactDelay, t.ImpactDelay),
                        Mathf.Max(timing.TotalDuration, t.TotalDuration));
                }

            if (timing.ImpactDelay > 0f)
                yield return new WaitForSeconds(timing.ImpactDelay);

            foreach (BoardEvent evt in phase.Events)
            {
                switch (evt)
                {
                    case EntityClearedEvent cleared:
                        StartVanish(cleared.Id, cleared.At, cleared.Color);
                        break;

                    case ObstacleDestroyedEvent destroyed:
                        StartVanish(destroyed.Id, destroyed.Anchor, PieceColor.None);
                        break;

                    case ObstacleDamagedEvent damaged:
                    {
                        EntityView view = Find(damaged.Id);
                        if (view != null)
                        {
                            view.SetPips(damaged.Hp, damaged.MaxHp);
                            StartCoroutine(view.Shake(0.2f, 0.08f));
                        }

                        break;
                    }

                    case BoosterCreatedEvent created:
                    {
                        EntityView view = Find(created.Entity.Id);
                        if (view != null)
                            StartCoroutine(view.Promote(created.Entity, PromoteDuration));
                        break;
                    }

                    case EntitySpawnedEvent spawned when !spawned.FromOutside:
                    {
                        // Something revealed inside a destroyed crate.
                        EntityView view = CreateView(spawned.Entity);
                        StartCoroutine(view.PopIn(PromoteDuration));
                        break;
                    }

                    case ObstacleColorChangedEvent recolored:
                    {
                        EntityView view = Find(recolored.Id);
                        view?.Apply(view.Snapshot.WithRequiredColor(recolored.Color));
                        break;
                    }
                }
            }

            yield return new WaitForSeconds(Mathf.Max(ClearDuration, timing.TotalDuration - timing.ImpactDelay));
        }

        private void StartVanish(long id, GridPos at, PieceColor color)
        {
            EntityView view = Find(id);
            if (view == null)
                return;

            _views.Remove(id);
            _fading.Add(view);

            _effects.PlayBurst(at, color == PieceColor.None
                ? new Color(0.85f, 0.85f, 0.9f)
                : SpriteLibrary.ColorOf(color));

            StartCoroutine(VanishAndDestroy(view));
        }

        private IEnumerator VanishAndDestroy(EntityView view)
        {
            yield return view.Vanish(ClearDuration);
            _fading.Remove(view);
            if (view != null)
                Destroy(view.gameObject);
        }

        private IEnumerator PlayFall(TurnPhase phase)
        {
            float longest = 0f;

            foreach (BoardEvent evt in phase.Events)
            {
                switch (evt)
                {
                    case EntitySpawnedEvent spawned when spawned.FromOutside:
                    {
                        EntityView view = CreateView(spawned.Entity);

                        // Start just above the board and fly into place.
                        Vector3 target = CellToLocal(spawned.Entity.Anchor);
                        var entry = new GridPos(spawned.Entity.Anchor.X, Height);
                        view.SetPosition(CellToLocal(entry));

                        float distance = Mathf.Abs(CellToLocal(entry).y - target.y);
                        float duration = DurationForFall(distance);
                        longest = Mathf.Max(longest, duration);

                        StartCoroutine(view.MoveTo(target, duration, Easing.FallCurve));
                        break;
                    }

                    case EntityMovedEvent moved when moved.Reason == MoveReason.Fall:
                    {
                        EntityView view = Find(moved.Id);
                        if (view == null)
                            break;

                        Vector3 target = AnchorToLocal(moved.To, view.Snapshot.Width, view.Snapshot.Height);
                        float distance = Vector3.Distance(view.transform.localPosition, target);
                        float duration = DurationForFall(distance);
                        longest = Mathf.Max(longest, duration);

                        StartCoroutine(view.MoveTo(target, duration, Easing.FallCurve));
                        break;
                    }
                }
            }

            if (longest > 0f)
                yield return new WaitForSeconds(longest);
        }

        private static float DurationForFall(float distanceInCells) =>
            Mathf.Min(FallBaseDuration + distanceInCells * FallPerCellDuration, FallMaxDuration);

        /// <summary>Highlights a cell, used to show the piece the player has picked up.</summary>
        public void SetSelection(GridPos? cell)
        {
            if (_selection == null)
            {
                var go = new GameObject("selection");
                go.transform.SetParent(transform, false);
                _selection = go.AddComponent<SpriteRenderer>();
                _selection.sprite = _sprites.Cell(false);
                _selection.color = new Color(1f, 1f, 1f, 0.45f);
                _selection.sortingOrder = -5;
            }

            _selection.enabled = cell.HasValue;
            if (cell.HasValue)
                _selection.transform.localPosition = CellToLocal(cell.Value);
        }

        private SpriteRenderer _selection;
    }
}
