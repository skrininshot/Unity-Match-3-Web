using System.Collections.Generic;

namespace Match3.Core
{
    /// <summary>
    /// Anything that occupies one or more board cells.
    /// <para>
    /// Every entity has a stable <see cref="Id"/>. The presentation layer keys its visuals off that id,
    /// which is what lets the view follow an entity through swaps, falls and cascades without
    /// re-reading the whole board.
    /// </para>
    /// </summary>
    public abstract class BoardEntity
    {
        protected BoardEntity(long id)
        {
            Id = id;
        }

        public long Id { get; }

        /// <summary>Bottom-left cell of the entity's footprint.</summary>
        public GridPos Anchor { get; internal set; }

        /// <summary>Footprint width in cells (1 for ordinary pieces).</summary>
        public virtual int Width => 1;

        /// <summary>Footprint height in cells (1 for ordinary pieces).</summary>
        public virtual int Height => 1;

        public bool IsMultiCell => Width > 1 || Height > 1;

        /// <summary>Does gravity pull this entity down?</summary>
        public abstract bool Falls { get; }

        /// <summary>
        /// Can other entities fall through the cells this one occupies?
        /// Obstacles normally block; nothing else does.
        /// </summary>
        public abstract bool BlocksFalling { get; }

        /// <summary>Cells this entity would occupy if anchored at <paramref name="anchor"/>.</summary>
        public IEnumerable<GridPos> CellsAt(GridPos anchor)
        {
            for (int dy = 0; dy < Height; dy++)
            for (int dx = 0; dx < Width; dx++)
                yield return new GridPos(anchor.X + dx, anchor.Y + dy);
        }

        /// <summary>Cells this entity currently occupies.</summary>
        public IEnumerable<GridPos> Cells => CellsAt(Anchor);

        public bool Covers(GridPos pos)
        {
            return pos.X >= Anchor.X && pos.X < Anchor.X + Width
                && pos.Y >= Anchor.Y && pos.Y < Anchor.Y + Height;
        }
    }
}
