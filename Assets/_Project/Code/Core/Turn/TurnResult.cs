using System.Collections.Generic;

namespace Match3.Core
{
    public enum PhaseKind
    {
        /// <summary>The player's swap is applied.</summary>
        Swap = 0,

        /// <summary>The swap produced nothing and is being undone.</summary>
        SwapRevert = 1,

        /// <summary>Matched and blasted entities are removed. Nothing moves during a clear phase.</summary>
        Clear = 2,

        /// <summary>Gravity and refill.</summary>
        Fall = 3,

        /// <summary>The board had no legal move left and was reshuffled.</summary>
        Shuffle = 4,

        /// <summary>Bookkeeping at the end of the turn: moves spent, goals, win/lose.</summary>
        Outcome = 5,
    }

    public sealed class TurnPhase
    {
        public TurnPhase(PhaseKind kind, IReadOnlyList<BoardEvent> events)
        {
            Kind = kind;
            Events = events;
        }

        public PhaseKind Kind { get; }
        public IReadOnlyList<BoardEvent> Events { get; }

        public override string ToString() => $"{Kind} ({Events.Count} events)";
    }

    public enum LevelOutcome
    {
        InProgress = 0,
        Won = 1,
        Lost = 2,
    }

    /// <summary>
    /// Everything that happened during one player action, as an ordered list of phases.
    /// <para>
    /// The core resolves a whole turn synchronously and hands this back; the view then plays the
    /// phases one after another. Because <see cref="PhaseKind.Clear"/> and
    /// <see cref="PhaseKind.Fall"/> are separate phases, "pieces must not start falling until the
    /// removal effect has completed" holds by construction rather than by animation timing.
    /// </para>
    /// </summary>
    public sealed class TurnResult
    {
        private static readonly BoardEvent[] NoEvents = new BoardEvent[0];

        public TurnResult(bool accepted, IReadOnlyList<TurnPhase> phases, LevelOutcome outcome,
            int movesLeft, string rejectionReason = null)
        {
            Accepted = accepted;
            Phases = phases;
            Outcome = outcome;
            MovesLeft = movesLeft;
            RejectionReason = rejectionReason;
        }

        /// <summary>False when the action was not a legal move; the board is unchanged.</summary>
        public bool Accepted { get; }

        /// <summary>Why the action was refused. Null when <see cref="Accepted"/> is true.</summary>
        public string RejectionReason { get; }

        public IReadOnlyList<TurnPhase> Phases { get; }

        public LevelOutcome Outcome { get; }
        public int MovesLeft { get; }

        /// <summary>
        /// Set when gravity failed to reach a stable board within its iteration budget.
        /// Always false in a correct build; asserted by the stress tests.
        /// </summary>
        public bool GravityStalled { get; internal set; }

        public static TurnResult Rejected(string reason, int movesLeft, LevelOutcome outcome) =>
            new TurnResult(false, new TurnPhase[0], outcome, movesLeft, reason);

        /// <summary>Rejected, but with a visible swap-and-bounce-back for the player.</summary>
        public static TurnResult RejectedWithBounce(string reason, GridPos a, GridPos b,
            long idA, long idB, int movesLeft, LevelOutcome outcome)
        {
            var phases = new[]
            {
                new TurnPhase(PhaseKind.Swap, new BoardEvent[]
                {
                    new EntityMovedEvent(idA, a, b, MoveReason.Swap),
                    new EntityMovedEvent(idB, b, a, MoveReason.Swap),
                }),
                new TurnPhase(PhaseKind.SwapRevert, new BoardEvent[]
                {
                    new EntityMovedEvent(idA, b, a, MoveReason.SwapRevert),
                    new EntityMovedEvent(idB, a, b, MoveReason.SwapRevert),
                }),
            };

            return new TurnResult(false, phases, outcome, movesLeft, reason);
        }

        public IEnumerable<BoardEvent> AllEvents()
        {
            foreach (TurnPhase phase in Phases)
            foreach (BoardEvent evt in phase.Events)
                yield return evt;
        }

        public IEnumerable<T> EventsOf<T>() where T : BoardEvent
        {
            foreach (BoardEvent evt in AllEvents())
                if (evt is T typed)
                    yield return typed;
        }

        public int CountPhases(PhaseKind kind)
        {
            int n = 0;
            foreach (TurnPhase phase in Phases)
                if (phase.Kind == kind)
                    n++;
            return n;
        }

        public override string ToString() =>
            $"{(Accepted ? "accepted" : "rejected: " + RejectionReason)}, " +
            $"{Phases.Count} phases, outcome={Outcome}, moves={MovesLeft}";
    }
}
