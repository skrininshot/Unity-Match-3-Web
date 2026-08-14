using System.Collections.Generic;

namespace Match3.Core
{
    public enum MoveReason
    {
        /// <summary>Player-initiated swap.</summary>
        Swap = 0,

        /// <summary>Swap being undone because it produced no match.</summary>
        SwapRevert = 1,

        /// <summary>Gravity.</summary>
        Fall = 2,

        /// <summary>Board reshuffle after a dead end.</summary>
        Shuffle = 3,
    }

    public enum ClearReason
    {
        Match = 0,
        BoosterBlast = 1,
    }

    /// <summary>Base type for everything the core reports about a turn.</summary>
    public abstract class BoardEvent
    {
    }

    public sealed class EntitySpawnedEvent : BoardEvent
    {
        public EntitySpawnedEvent(EntitySnapshot entity, bool fromOutside)
        {
            Entity = entity;
            FromOutside = fromOutside;
        }

        public EntitySnapshot Entity { get; }

        /// <summary>True when the entity enters from above the board and should fly in.</summary>
        public bool FromOutside { get; }
    }

    public sealed class EntityMovedEvent : BoardEvent
    {
        public EntityMovedEvent(long id, GridPos from, GridPos to, MoveReason reason)
        {
            Id = id;
            From = from;
            To = to;
            Reason = reason;
        }

        public long Id { get; }
        public GridPos From { get; }
        public GridPos To { get; }
        public MoveReason Reason { get; }
    }

    public sealed class EntityClearedEvent : BoardEvent
    {
        public EntityClearedEvent(long id, GridPos at, PieceColor color, ClearReason reason)
        {
            Id = id;
            At = at;
            Color = color;
            Reason = reason;
        }

        public long Id { get; }
        public GridPos At { get; }
        public PieceColor Color { get; }
        public ClearReason Reason { get; }
    }

    /// <summary>A matched piece was promoted to a booster instead of being cleared. The id is preserved.</summary>
    public sealed class BoosterCreatedEvent : BoardEvent
    {
        public BoosterCreatedEvent(EntitySnapshot entity)
        {
            Entity = entity;
        }

        public EntitySnapshot Entity { get; }
    }

    public sealed class BoosterActivatedEvent : BoardEvent
    {
        public BoosterActivatedEvent(long sourceId, GridPos at, BoosterType type,
            LineOrientation orientation, PieceColor targetColor, GridPos? flyTo,
            IReadOnlyList<GridPos> affected)
        {
            SourceId = sourceId;
            At = at;
            Type = type;
            Orientation = orientation;
            TargetColor = targetColor;
            FlyTo = flyTo;
            Affected = affected;
        }

        /// <summary>Id of the booster piece that fired, or 0 for a synthetic activation from a combination.</summary>
        public long SourceId { get; }

        public GridPos At { get; }
        public BoosterType Type { get; }
        public LineOrientation Orientation { get; }

        /// <summary>Colour chosen by a Rainbow activation.</summary>
        public PieceColor TargetColor { get; }

        /// <summary>Destination the Plane flew to.</summary>
        public GridPos? FlyTo { get; }

        /// <summary>Cells the blast covered — the view draws its effect over exactly these.</summary>
        public IReadOnlyList<GridPos> Affected { get; }
    }

    public sealed class ObstacleDamagedEvent : BoardEvent
    {
        public ObstacleDamagedEvent(long id, GridPos anchor, int hp, int maxHp, PieceColor requiredColor)
        {
            Id = id;
            Anchor = anchor;
            Hp = hp;
            MaxHp = maxHp;
            RequiredColor = requiredColor;
        }

        public long Id { get; }
        public GridPos Anchor { get; }
        public int Hp { get; }
        public int MaxHp { get; }
        public PieceColor RequiredColor { get; }
    }

    public sealed class ObstacleDestroyedEvent : BoardEvent
    {
        public ObstacleDestroyedEvent(long id, GridPos anchor, string obstacleId)
        {
            Id = id;
            Anchor = anchor;
            ObstacleId = obstacleId;
        }

        public long Id { get; }
        public GridPos Anchor { get; }
        public string ObstacleId { get; }
    }

    /// <summary>The colour-changing box picked a new required colour.</summary>
    public sealed class ObstacleColorChangedEvent : BoardEvent
    {
        public ObstacleColorChangedEvent(long id, PieceColor color)
        {
            Id = id;
            Color = color;
        }

        public long Id { get; }
        public PieceColor Color { get; }
    }

    public sealed class GoalProgressEvent : BoardEvent
    {
        public GoalProgressEvent(PieceColor color, int collected, int required)
        {
            Color = color;
            Collected = collected;
            Required = required;
        }

        public PieceColor Color { get; }
        public int Collected { get; }
        public int Required { get; }
    }

    public sealed class MovesLeftChangedEvent : BoardEvent
    {
        public MovesLeftChangedEvent(int movesLeft)
        {
            MovesLeft = movesLeft;
        }

        public int MovesLeft { get; }
    }

    public sealed class OutcomeEvent : BoardEvent
    {
        public OutcomeEvent(LevelOutcome outcome)
        {
            Outcome = outcome;
        }

        public LevelOutcome Outcome { get; }
    }
}
