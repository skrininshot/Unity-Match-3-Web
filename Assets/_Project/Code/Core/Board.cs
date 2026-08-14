using System;
using System.Collections.Generic;

namespace Match3.Core
{
    /// <summary>
    /// The grid. Owns cells and entities and nothing else — it knows how to place, move and
    /// remove things, but has no opinion about matching, gravity or goals. Those live in
    /// dedicated services so each can be tested in isolation.
    /// <para>
    /// A multi-cell entity is stored by reference in every cell of its footprint, so a lookup
    /// at any covered position returns it.
    /// </para>
    /// </summary>
    public sealed class Board
    {
        private readonly bool[] _playable;
        private readonly bool[] _spawner;
        private readonly BoardEntity[] _occupants;
        private readonly Dictionary<long, BoardEntity> _byId = new Dictionary<long, BoardEntity>();

        private long _nextId = 1;

        public Board(int width, int height)
        {
            if (width <= 0 || height <= 0)
                throw new ArgumentException($"Board size must be positive, got {width}x{height}.");

            Width = width;
            Height = height;
            int count = width * height;
            _playable = new bool[count];
            _spawner = new bool[count];
            _occupants = new BoardEntity[count];

            for (int i = 0; i < count; i++)
                _playable[i] = true;

            RecomputeSpawners();
        }

        public int Width { get; }
        public int Height { get; }

        /// <summary>Next unused entity id. Ids are never reused, which keeps view bookkeeping simple.</summary>
        public long NewEntityId() => _nextId++;

        public int Index(GridPos pos) => pos.Y * Width + pos.X;

        public bool InBounds(GridPos pos) =>
            pos.X >= 0 && pos.X < Width && pos.Y >= 0 && pos.Y < Height;

        // ---------------------------------------------------------------- cells

        public bool IsPlayable(GridPos pos) => InBounds(pos) && _playable[Index(pos)];

        public void SetPlayable(GridPos pos, bool playable)
        {
            if (!InBounds(pos))
                throw new ArgumentOutOfRangeException(nameof(pos), $"{pos} is outside {Width}x{Height}.");

            if (!playable && _occupants[Index(pos)] != null)
                throw new InvalidOperationException($"Cannot make occupied cell {pos} non-playable.");

            _playable[Index(pos)] = playable;
        }

        /// <summary>
        /// Cells where new pieces enter the board: the topmost playable cell of each column.
        /// Derived from the layout, so level data never has to declare spawners explicitly.
        /// </summary>
        public bool IsSpawner(GridPos pos) => InBounds(pos) && _spawner[Index(pos)];

        public void RecomputeSpawners()
        {
            Array.Clear(_spawner, 0, _spawner.Length);
            for (int x = 0; x < Width; x++)
            {
                for (int y = Height - 1; y >= 0; y--)
                {
                    var pos = new GridPos(x, y);
                    if (!_playable[Index(pos)])
                        continue;
                    _spawner[Index(pos)] = true;
                    break;
                }
            }
        }

        public int PlayableCellCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _playable.Length; i++)
                    if (_playable[i]) n++;
                return n;
            }
        }

        /// <summary>All positions, bottom row first, left to right. Deterministic iteration order.</summary>
        public IEnumerable<GridPos> Positions
        {
            get
            {
                for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                    yield return new GridPos(x, y);
            }
        }

        // ------------------------------------------------------------- occupants

        public BoardEntity EntityAt(GridPos pos) => IsPlayable(pos) ? _occupants[Index(pos)] : null;

        public Piece PieceAt(GridPos pos) => EntityAt(pos) as Piece;

        public Obstacle ObstacleAt(GridPos pos) => EntityAt(pos) as Obstacle;

        public bool IsEmpty(GridPos pos) => IsPlayable(pos) && _occupants[Index(pos)] == null;

        public bool IsOccupied(GridPos pos) => EntityAt(pos) != null;

        public BoardEntity FindById(long id) => _byId.TryGetValue(id, out BoardEntity e) ? e : null;

        public int EntityCount => _byId.Count;

        /// <summary>Snapshot of all entities — safe to iterate while mutating the board.</summary>
        public List<BoardEntity> AllEntities() => new List<BoardEntity>(_byId.Values);

        /// <summary>Entities ordered by anchor Y ascending, then X ascending. Used by gravity.</summary>
        public List<BoardEntity> EntitiesBottomUp()
        {
            var list = AllEntities();
            list.Sort(CompareBottomUp);
            return list;
        }

        private static int CompareBottomUp(BoardEntity a, BoardEntity b)
        {
            int cmp = a.Anchor.Y.CompareTo(b.Anchor.Y);
            if (cmp != 0) return cmp;
            cmp = a.Anchor.X.CompareTo(b.Anchor.X);
            if (cmp != 0) return cmp;
            return a.Id.CompareTo(b.Id);
        }

        /// <summary>
        /// True if <paramref name="entity"/> can occupy <paramref name="anchor"/>: every footprint
        /// cell is playable and either empty or already held by this same entity.
        /// </summary>
        public bool CanPlace(BoardEntity entity, GridPos anchor)
        {
            foreach (GridPos cell in entity.CellsAt(anchor))
            {
                if (!IsPlayable(cell))
                    return false;

                BoardEntity occupant = _occupants[Index(cell)];
                if (occupant != null && !ReferenceEquals(occupant, entity))
                    return false;
            }

            return true;
        }

        public void Place(BoardEntity entity, GridPos anchor)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            if (_byId.ContainsKey(entity.Id))
                throw new InvalidOperationException($"Entity {entity} is already on the board.");
            if (!CanPlace(entity, anchor))
                throw new InvalidOperationException($"Cannot place {entity} at {anchor}: cells unavailable.");

            entity.Anchor = anchor;
            _byId[entity.Id] = entity;
            foreach (GridPos cell in entity.CellsAt(anchor))
                _occupants[Index(cell)] = entity;
        }

        public void Remove(BoardEntity entity)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            if (!_byId.Remove(entity.Id))
                throw new InvalidOperationException($"Entity {entity} is not on the board.");

            foreach (GridPos cell in entity.Cells)
            {
                int i = Index(cell);
                if (ReferenceEquals(_occupants[i], entity))
                    _occupants[i] = null;
            }
        }

        public void MoveTo(BoardEntity entity, GridPos anchor)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            if (!_byId.ContainsKey(entity.Id))
                throw new InvalidOperationException($"Entity {entity} is not on the board.");
            if (!CanPlace(entity, anchor))
                throw new InvalidOperationException($"Cannot move {entity} to {anchor}: cells unavailable.");

            foreach (GridPos cell in entity.Cells)
            {
                int i = Index(cell);
                if (ReferenceEquals(_occupants[i], entity))
                    _occupants[i] = null;
            }

            entity.Anchor = anchor;
            foreach (GridPos cell in entity.CellsAt(anchor))
                _occupants[Index(cell)] = entity;
        }

        /// <summary>Swaps two single-cell entities. Both cells must hold a 1x1 entity.</summary>
        public void SwapCells(GridPos a, GridPos b)
        {
            BoardEntity ea = EntityAt(a);
            BoardEntity eb = EntityAt(b);
            if (ea == null || eb == null)
                throw new InvalidOperationException($"Cannot swap {a} and {b}: one of them is empty.");
            if (ea.IsMultiCell || eb.IsMultiCell)
                throw new InvalidOperationException($"Cannot swap multi-cell entities ({ea}, {eb}).");

            _occupants[Index(a)] = eb;
            _occupants[Index(b)] = ea;
            ea.Anchor = b;
            eb.Anchor = a;
        }

        // ------------------------------------------------------------- factories

        public Piece CreatePiece(PieceColor color, BoosterType booster = BoosterType.None,
            LineOrientation orientation = LineOrientation.Horizontal)
        {
            return new Piece(NewEntityId(), color, booster, orientation);
        }

        public Piece SpawnPiece(GridPos pos, PieceColor color, BoosterType booster = BoosterType.None,
            LineOrientation orientation = LineOrientation.Horizontal)
        {
            Piece piece = CreatePiece(color, booster, orientation);
            Place(piece, pos);
            return piece;
        }

        public Obstacle SpawnObstacle(GridPos pos, ObstacleConfig config, int hp = 0,
            PieceColor requiredColor = PieceColor.None, EntitySpec contains = null,
            int width = 0, int height = 0)
        {
            var obstacle = new Obstacle(NewEntityId(), config, hp, requiredColor, contains, width, height);
            Place(obstacle, pos);
            return obstacle;
        }
    }
}
