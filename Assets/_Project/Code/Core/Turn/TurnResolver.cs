using System.Collections.Generic;

namespace Match3.Core
{
    /// <summary>
    /// Plays one player action to completion and reports it as an ordered list of phases.
    /// <para>
    /// The whole cascade — matches, booster blasts, chain reactions, refills — is resolved
    /// synchronously here. The view replays the phases afterwards. Because clearing and falling are
    /// separate phases, "pieces must not start falling until the removal effect has completed"
    /// is guaranteed by the data, not by animation timings.
    /// </para>
    /// </summary>
    public sealed class TurnResolver
    {
        /// <summary>Cascade rounds allowed per turn. A real cascade is a handful; this is a bug fence.</summary>
        public const int MaxCascadeRounds = 200;

        /// <summary>Booster waves allowed per cascade round.</summary>
        public const int MaxWavesPerRound = 200;

        private readonly Board _board;
        private readonly LevelRuntime _level;
        private readonly ObstacleCatalog _catalog;
        private readonly MatchDetector _detector;
        private readonly GravityResolver _gravity;
        private readonly BoosterRegistry _boosters;
        private readonly BoosterCombinationRegistry _combinations;
        private readonly Rng _rng;

        /// <summary>
        /// Colours gravity refills from. Normally the level palette; an empty list produces a board
        /// that drains without refilling, which is how tests isolate a cascade from random new pieces.
        /// </summary>
        private readonly IReadOnlyList<PieceColor> _refillPalette;

        private readonly HashSet<long> _activated = new HashSet<long>();
        private readonly HashSet<GridPos> _reservedPlaneTargets = new HashSet<GridPos>();
        private readonly List<TurnPhase> _phases = new List<TurnPhase>();

        private GridPos? _playerCellA;
        private GridPos? _playerCellB;
        private bool _gravityStalled;

        public TurnResolver(
            Board board,
            LevelRuntime level,
            Rng rng,
            ObstacleCatalog catalog,
            MatchDetector detector = null,
            GravityResolver gravity = null,
            BoosterRegistry boosters = null,
            BoosterCombinationRegistry combinations = null,
            IReadOnlyList<PieceColor> refillPalette = null)
        {
            _board = board;
            _level = level;
            _rng = rng;
            _catalog = catalog;
            _refillPalette = refillPalette ?? level.Palette;
            _detector = detector ?? new MatchDetector();
            _gravity = gravity ?? new GravityResolver();
            _boosters = boosters ?? BoosterRegistry.CreateDefault();
            _combinations = combinations ?? BoosterCombinationRegistry.CreateDefault();
        }

        public Board Board => _board;
        public LevelRuntime Level => _level;
        public MatchDetector Detector => _detector;

        // ------------------------------------------------------------------ actions

        /// <summary>Swaps two adjacent cells. Rejected results leave the board untouched.</summary>
        public TurnResult Swap(GridPos a, GridPos b)
        {
            if (_level.IsOver)
                return TurnResult.Rejected("level is already finished", _level.MovesLeft, _level.Outcome);

            SwapKind kind = SwapRules.Classify(_board, _detector, a, b);
            Piece pieceA = _board.PieceAt(a);
            Piece pieceB = _board.PieceAt(b);

            if (kind == SwapKind.Invalid)
            {
                bool swappableAtAll = pieceA != null && pieceB != null
                                      && !pieceA.IsMultiCell && !pieceB.IsMultiCell
                                      && a.IsOrthogonalNeighbourOf(b);

                return swappableAtAll
                    ? TurnResult.RejectedWithBounce("swap creates no match", a, b,
                        pieceA.Id, pieceB.Id, _level.MovesLeft, _level.Outcome)
                    : TurnResult.Rejected("these cells cannot be swapped", _level.MovesLeft, _level.Outcome);
            }

            BeginTurn(a, b);

            _board.SwapCells(a, b);
            AddPhase(PhaseKind.Swap, new List<BoardEvent>
            {
                new EntityMovedEvent(pieceA.Id, a, b, MoveReason.Swap),
                new EntityMovedEvent(pieceB.Id, b, a, MoveReason.Swap),
            });

            var pending = new List<ActivationRequest>();

            switch (kind)
            {
                case SwapKind.BoosterCombo:
                {
                    // Both boosters are spent by the combination itself.
                    var consumed = new List<BoardEvent>();
                    Consume(pieceA, consumed);
                    Consume(pieceB, consumed);
                    AddPhase(PhaseKind.Clear, consumed);

                    if (!_combinations.TryResolve(pieceA, pieceB, b, _board, _level, _rng, pending))
                    {
                        // Unregistered pairing: firing both independently is a sane fallback.
                        pending.Add(ActivationRequest.FromPiece(pieceA));
                        pending.Add(ActivationRequest.FromPiece(pieceB));
                    }

                    break;
                }

                case SwapKind.RainbowColor:
                {
                    Piece rainbow = pieceA.Booster == BoosterType.Rainbow ? pieceA : pieceB;
                    Piece partner = ReferenceEquals(rainbow, pieceA) ? pieceB : pieceA;
                    PieceColor target = partner.Color;

                    var consumed = new List<BoardEvent>();
                    Consume(rainbow, consumed);
                    AddPhase(PhaseKind.Clear, consumed);

                    pending.Add(ActivationRequest.Rainbow(rainbow.Anchor, target, sourceId: rainbow.Id));
                    break;
                }

                case SwapKind.BoosterRelocate:
                {
                    // The ordinary piece just stays where it landed; the booster fires from its new
                    // cell, exactly like a tap, since Consume/FromPiece read the post-swap Anchor.
                    FireRelocatedBooster(pieceA.IsBooster ? pieceA : pieceB, pending);
                    break;
                }

                case SwapKind.Match:
                {
                    // WouldSwapMatch (which produced this classification) only asks "did the swap
                    // create a match anywhere" -- not "did the booster's own new cell join one". A
                    // lone booster relocated next to an unrelated pair can complete a match entirely
                    // on the far side of the swap (the plain piece landing in a run of its own)
                    // while the booster's own cell matches nothing at all. Left alone, that booster
                    // would just sit there relocated but never fired -- silently discarding the move
                    // the player aimed with it. So: if exactly one side is a lone booster and its own
                    // cell isn't itself part of a match, fire it explicitly, exactly like
                    // BoosterRelocate; the unrelated match elsewhere is still picked up normally by
                    // RunCascades below, same as always.
                    if (pieceA.IsBooster != pieceB.IsBooster)
                    {
                        Piece booster = pieceA.IsBooster ? pieceA : pieceB;
                        if (!_detector.CreatesMatchAt(_board, booster.Anchor))
                            FireRelocatedBooster(booster, pending);
                    }

                    break;
                }
            }

            RunCascades(pending);
            return FinishTurn();
        }

        /// <summary>Taps a booster on the board, firing it in place.</summary>
        public TurnResult ActivateBoosterAt(GridPos pos)
        {
            if (_level.IsOver)
                return TurnResult.Rejected("level is already finished", _level.MovesLeft, _level.Outcome);

            Piece piece = _board.PieceAt(pos);
            if (piece == null || !piece.IsBooster)
                return TurnResult.Rejected("no booster in that cell", _level.MovesLeft, _level.Outcome);

            BeginTurn(pos, null);

            ActivationRequest request = ActivationRequest.FromPiece(piece);

            var consumed = new List<BoardEvent>();
            Consume(piece, consumed);
            AddPhase(PhaseKind.Clear, consumed);

            RunCascades(new List<ActivationRequest> { request });
            return FinishTurn();
        }

        // ------------------------------------------------------------------ cascade

        private void RunCascades(List<ActivationRequest> pending)
        {
            for (int round = 0; round < MaxCascadeRounds; round++)
            {
                bool progressed = false;

                List<MatchGroup> groups = _detector.FindMatches(_board);
                if (groups.Count > 0)
                {
                    var events = new List<BoardEvent>();
                    ResolveMatchGroups(groups, events, pending);
                    if (events.Count > 0)
                    {
                        AddPhase(PhaseKind.Clear, events);
                        progressed = true;
                    }
                }

                // Each wave is its own phase so chain reactions read as a sequence of explosions
                // rather than one indistinguishable flash.
                int waves = 0;
                while (pending.Count > 0 && waves++ < MaxWavesPerRound)
                {
                    List<ActivationRequest> wave = pending;
                    pending = new List<ActivationRequest>();

                    var events = new List<BoardEvent>();
                    ResolveWave(wave, events, pending);
                    if (events.Count > 0)
                    {
                        AddPhase(PhaseKind.Clear, events);
                        progressed = true;
                    }
                }

                if (!progressed)
                    break;

                var fallEvents = new List<BoardEvent>();
                if (!_gravity.Settle(_board, _refillPalette, _rng, fallEvents))
                    _gravityStalled = true;

                if (fallEvents.Count > 0)
                    AddPhase(PhaseKind.Fall, fallEvents);
            }
        }

        private void ResolveMatchGroups(List<MatchGroup> groups, List<BoardEvent> events,
            List<ActivationRequest> pending)
        {
            foreach (MatchGroup group in groups)
            {
                BoosterType award = group.AwardedBooster;
                GridPos? boosterCell = award != BoosterType.None ? ChooseBoosterCell(group) : null;

                // Crates take their hit while the matched pieces are still on the board, so that
                // "adjacent to the match" means what the player saw.
                DamageObstaclesAround(group, events, pending);

                foreach (GridPos cell in group.Cells)
                {
                    if (boosterCell.HasValue && cell == boosterCell.Value)
                        continue;

                    ClearCell(cell, ClearReason.Match, DamageSource.FromMatch(group.Color), events, pending);
                }

                if (!boosterCell.HasValue)
                    continue;

                Piece promoted = _board.PieceAt(boosterCell.Value);
                if (promoted == null)
                    continue;

                promoted.Booster = award;
                promoted.Orientation = group.AwardedLineOrientation;

                // The Rainbow is colourless, which is also what keeps it out of future matches.
                if (award == BoosterType.Rainbow)
                    promoted.Color = PieceColor.None;

                events.Add(new BoosterCreatedEvent(EntitySnapshot.Of(promoted)));
            }
        }

        /// <summary>
        /// Picks the cell a new booster appears in: where the player acted if possible, otherwise
        /// the crossing of an L/T, otherwise the middle of the longest run.
        /// </summary>
        private GridPos? ChooseBoosterCell(MatchGroup group)
        {
            if (_playerCellB.HasValue && Promotable(_playerCellB.Value) && Contains(group, _playerCellB.Value))
                return _playerCellB.Value;
            if (_playerCellA.HasValue && Promotable(_playerCellA.Value) && Contains(group, _playerCellA.Value))
                return _playerCellA.Value;

            if (group.HasCorner)
            {
                foreach (MatchShape horizontal in group.Shapes)
                {
                    if (horizontal.Kind != MatchShapeKind.Line
                        || horizontal.Orientation != LineOrientation.Horizontal)
                        continue;

                    foreach (MatchShape vertical in group.Shapes)
                    {
                        if (vertical.Kind != MatchShapeKind.Line
                            || vertical.Orientation != LineOrientation.Vertical)
                            continue;

                        foreach (GridPos cell in horizontal.Cells)
                            if (ContainsCell(vertical, cell) && Promotable(cell))
                                return cell;
                    }
                }
            }

            MatchShape longest = null;
            foreach (MatchShape shape in group.Shapes)
                if (shape.Kind == MatchShapeKind.Line && (longest == null || shape.Length > longest.Length))
                    longest = shape;

            if (longest != null)
            {
                GridPos middle = longest.Cells[longest.Cells.Count / 2];
                if (Promotable(middle))
                    return middle;
            }

            foreach (GridPos cell in group.Cells)
                if (Promotable(cell))
                    return cell;

            return null;
        }

        private bool Promotable(GridPos cell)
        {
            Piece piece = _board.PieceAt(cell);
            return piece != null && !piece.IsBooster;
        }

        private static bool Contains(MatchGroup group, GridPos cell)
        {
            foreach (GridPos candidate in group.Cells)
                if (candidate == cell)
                    return true;
            return false;
        }

        private static bool ContainsCell(MatchShape shape, GridPos cell)
        {
            foreach (GridPos candidate in shape.Cells)
                if (candidate == cell)
                    return true;
            return false;
        }

        private void ResolveWave(List<ActivationRequest> wave, List<BoardEvent> events,
            List<ActivationRequest> next)
        {
            foreach (ActivationRequest request in wave)
            {
                if (!_boosters.TryGet(request.Type, out IBoosterEffect effect))
                    continue;

                var context = new BoosterContext(_board, _level, _rng, _reservedPlaneTargets);
                effect.Resolve(request, context);

                List<GridPos> affected = Deduplicate(context.Affected);

                events.Add(new BoosterActivatedEvent(
                    request.SourceId, request.At, request.Type, request.Orientation,
                    context.ChosenColor, context.FlyTarget, affected));

                foreach (GridPos cell in affected)
                    ClearCell(cell, ClearReason.BoosterBlast, DamageSource.FromBlast(), events, next);

                next.AddRange(context.FollowUps);
            }
        }

        private static List<GridPos> Deduplicate(List<GridPos> cells)
        {
            var seen = new HashSet<GridPos>();
            var result = new List<GridPos>(cells.Count);
            foreach (GridPos cell in cells)
                if (seen.Add(cell))
                    result.Add(cell);
            return result;
        }

        // ------------------------------------------------------------------ cell effects

        private void ClearCell(GridPos cell, ClearReason reason, DamageSource damage,
            List<BoardEvent> events, List<ActivationRequest> pending)
        {
            BoardEntity entity = _board.EntityAt(cell);
            if (entity == null)
                return;

            if (entity is Piece piece)
            {
                // A destroyed booster fires: this is where chain reactions come from.
                if (piece.IsBooster && _activated.Add(piece.Id))
                    pending.Add(ActivationRequest.FromPiece(piece));

                events.Add(new EntityClearedEvent(piece.Id, cell, piece.Color, reason));
                CreditGoal(piece.Color, events);
                _board.Remove(piece);
                return;
            }

            if (entity is Obstacle obstacle)
                DamageObstacle(obstacle, damage, events, pending);
        }

        private void DamageObstaclesAround(MatchGroup group, List<BoardEvent> events,
            List<ActivationRequest> pending)
        {
            var damaged = new HashSet<long>();
            var source = DamageSource.FromMatch(group.Color);

            foreach (GridPos cell in group.Cells)
            foreach (GridPos offset in GridPos.Orthogonal)
            {
                Obstacle obstacle = _board.ObstacleAt(cell + offset);
                if (obstacle == null || !damaged.Add(obstacle.Id))
                    continue;

                DamageObstacle(obstacle, source, events, pending);
            }
        }

        private void DamageObstacle(Obstacle obstacle, DamageSource source, List<BoardEvent> events,
            List<ActivationRequest> pending)
        {
            if (!obstacle.Accepts(source))
                return;

            obstacle.Hp--;

            if (obstacle.Hp > 0)
            {
                events.Add(new ObstacleDamagedEvent(obstacle.Id, obstacle.Anchor,
                    obstacle.Hp, obstacle.MaxHp, obstacle.RequiredColor));
                return;
            }

            GridPos anchor = obstacle.Anchor;
            EntitySpec contents = obstacle.Contains;

            _board.Remove(obstacle);
            events.Add(new ObstacleDestroyedEvent(obstacle.Id, anchor, obstacle.Config.Id));

            Reveal(contents, anchor, events);
        }

        /// <summary>Places whatever a destroyed crate was holding, which may be another crate.</summary>
        private void Reveal(EntitySpec spec, GridPos anchor, List<BoardEvent> events)
        {
            if (spec == null)
                return;

            BoardEntity created = EntityFactory.Create(_board, spec, _catalog, _level.Palette, _rng);
            if (created == null || !_board.CanPlace(created, anchor))
                return;

            _board.Place(created, anchor);
            events.Add(new EntitySpawnedEvent(EntitySnapshot.Of(created), fromOutside: false));
        }

        private void CreditGoal(PieceColor color, List<BoardEvent> events)
        {
            if (color == PieceColor.None)
                return;

            GoalState goal = _level.Goals.Register(color);
            if (goal != null)
                events.Add(new GoalProgressEvent(goal.Color, goal.Collected, goal.Required));
        }

        /// <summary>Removes a booster that is being spent by a tap or a combination.</summary>
        private void Consume(Piece piece, List<BoardEvent> events)
        {
            _activated.Add(piece.Id);
            events.Add(new EntityClearedEvent(piece.Id, piece.Anchor, piece.Color, ClearReason.BoosterBlast));
            CreditGoal(piece.Color, events);
            _board.Remove(piece);
        }

        /// <summary>Removes a relocated booster from the board and queues its activation, exactly
        /// as if it had been tapped in place -- used whenever a swap lands a lone booster somewhere
        /// without it joining a match of its own.</summary>
        private void FireRelocatedBooster(Piece booster, List<ActivationRequest> pending)
        {
            var consumed = new List<BoardEvent>();
            Consume(booster, consumed);
            AddPhase(PhaseKind.Clear, consumed);

            pending.Add(ActivationRequest.FromPiece(booster));
        }

        // ------------------------------------------------------------------ turn framing

        private void BeginTurn(GridPos? cellA, GridPos? cellB)
        {
            _phases.Clear();
            _activated.Clear();
            _reservedPlaneTargets.Clear();
            _gravityStalled = false;
            _playerCellA = cellA;
            _playerCellB = cellB;
        }

        private void AddPhase(PhaseKind kind, List<BoardEvent> events)
        {
            if (events.Count > 0)
                _phases.Add(new TurnPhase(kind, events));
        }

        private TurnResult FinishTurn()
        {
            var events = new List<BoardEvent>();

            _level.TurnNumber++;
            if (_level.MovesLeft > 0)
            {
                _level.MovesLeft--;
                events.Add(new MovesLeftChangedEvent(_level.MovesLeft));
            }

            // Per-turn element behaviour, e.g. the colour-changing crate rerolling.
            foreach (BoardEntity entity in _board.EntitiesBottomUp())
            {
                if (!(entity is Obstacle obstacle))
                    continue;

                if (obstacle.Config.Rule.OnTurnAdvanced(obstacle, _rng))
                    events.Add(new ObstacleColorChangedEvent(obstacle.Id, obstacle.RequiredColor));
            }

            // Goals win over running out of moves when both land on the same turn.
            if (_level.Goals.IsComplete)
                _level.Outcome = LevelOutcome.Won;
            else if (_level.MovesLeft <= 0)
                _level.Outcome = LevelOutcome.Lost;

            if (_level.Outcome == LevelOutcome.InProgress && !MoveFinder.HasAny(_board, _detector))
            {
                var shuffleEvents = new List<BoardEvent>();
                BoardShuffler.Shuffle(_board, _detector, _rng, shuffleEvents);
                AddPhase(PhaseKind.Shuffle, shuffleEvents);
            }

            events.Add(new OutcomeEvent(_level.Outcome));
            AddPhase(PhaseKind.Outcome, events);

            return new TurnResult(true, _phases.ToArray(), _level.Outcome, _level.MovesLeft)
            {
                GravityStalled = _gravityStalled,
            };
        }
    }
}
