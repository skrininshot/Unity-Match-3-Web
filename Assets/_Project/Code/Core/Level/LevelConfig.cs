using System;
using System.Collections.Generic;

namespace Match3.Core
{
    public sealed class LevelGoal
    {
        public PieceColor Color;
        public int Count;

        public LevelGoal()
        {
        }

        public LevelGoal(PieceColor color, int count)
        {
            Color = color;
            Count = count;
        }

        public override string ToString() => $"{Count}x{Color}";
    }

    /// <summary>Per-cell escape hatch for things the single-character layout cannot express.</summary>
    public sealed class CellOverride
    {
        public int X;
        public int Y;
        public EntitySpec Spec;

        public CellOverride()
        {
        }

        public CellOverride(int x, int y, EntitySpec spec)
        {
            X = x;
            Y = y;
            Spec = spec;
        }

        public GridPos Pos => new GridPos(X, Y);

        public override string ToString() => $"{Pos} = {Spec}";
    }

    /// <summary>
    /// Everything that defines a level. Plain data with no engine types, loaded from JSON, so
    /// board size, colour count, goals and move limit are all tunable without touching gameplay code.
    /// </summary>
    public sealed class LevelConfig
    {
        public string Id = string.Empty;
        public string Name = string.Empty;

        public int Width = 8;
        public int Height = 8;
        public int MoveLimit = 20;

        /// <summary>Seed for board generation and refills. 0 means "pick one at runtime".</summary>
        public int Seed;

        /// <summary>Colours in play. Its length is the level's colour count.</summary>
        public List<PieceColor> Palette = new List<PieceColor>();

        public List<LevelGoal> Goals = new List<LevelGoal>();

        /// <summary>
        /// Board layout, first string = TOP row. Empty means "all cells playable, filled randomly".
        /// See <see cref="LayoutCodes"/> for the character set.
        /// </summary>
        public List<string> Layout = new List<string>();

        public List<CellOverride> Overrides = new List<CellOverride>();

        public LevelConfig Clone()
        {
            var clone = new LevelConfig
            {
                Id = Id,
                Name = Name,
                Width = Width,
                Height = Height,
                MoveLimit = MoveLimit,
                Seed = Seed,
                Palette = new List<PieceColor>(Palette),
                Layout = new List<string>(Layout),
                Goals = new List<LevelGoal>(),
                Overrides = new List<CellOverride>(),
            };

            foreach (LevelGoal goal in Goals)
                clone.Goals.Add(new LevelGoal(goal.Color, goal.Count));
            foreach (CellOverride cell in Overrides)
                clone.Overrides.Add(new CellOverride(cell.X, cell.Y, cell.Spec));

            return clone;
        }

        /// <summary>
        /// Human-readable problems with this level. Empty list means the level is loadable.
        /// Every shipped level is checked against this in the tests.
        /// <para>
        /// Accepts an optional obstacle catalog only so tests and tooling can supply a non-default
        /// one; every real call site (<see cref="Match3.Data.LevelResourceLoader"/>) uses the
        /// default by leaving it out.
        /// </para>
        /// </summary>
        public List<string> Validate(ObstacleCatalog catalog = null)
        {
            var problems = new List<string>();

            if (string.IsNullOrWhiteSpace(Id))
                problems.Add("id is empty");
            if (Width <= 0 || Height <= 0)
                problems.Add($"board size must be positive, got {Width}x{Height}");
            if (MoveLimit <= 0)
                problems.Add($"move limit must be positive, got {MoveLimit}");

            if (Palette.Count < 3)
                problems.Add($"palette needs at least 3 colours to avoid forced matches, got {Palette.Count}");

            var seenColors = new HashSet<PieceColor>();
            foreach (PieceColor color in Palette)
            {
                if (color == PieceColor.None)
                    problems.Add("palette contains PieceColor.None");
                else if (!seenColors.Add(color))
                    problems.Add($"palette contains {color} twice");
            }

            if (Goals.Count == 0)
                problems.Add("level has no goals");

            foreach (LevelGoal goal in Goals)
            {
                if (goal.Count <= 0)
                    problems.Add($"goal {goal} must require a positive count");
                if (!seenColors.Contains(goal.Color))
                    problems.Add($"goal colour {goal.Color} is not in the palette");
            }

            if (Layout.Count > 0)
            {
                if (Layout.Count != Height)
                    problems.Add($"layout has {Layout.Count} rows but height is {Height}");

                for (int i = 0; i < Layout.Count; i++)
                {
                    string row = Layout[i];
                    if (row.Length != Width)
                    {
                        problems.Add($"layout row {i} has {row.Length} cells but width is {Width}");
                        continue;
                    }

                    foreach (char code in row)
                        if (!LayoutCodes.IsKnown(code))
                            problems.Add($"layout row {i} contains unknown code '{code}'");
                }
            }

            foreach (CellOverride cell in Overrides)
            {
                if (cell.X < 0 || cell.X >= Width || cell.Y < 0 || cell.Y >= Height)
                    problems.Add($"override at {cell.Pos} is outside the board");
                if (cell.Spec == null)
                    problems.Add($"override at {cell.Pos} has no spec");
            }

            // A layout that leaves no movable piece at all -- built entirely from non-falling
            // obstacles, say -- would pass every check above, load "successfully", and then sit at
            // a permanent dead end: BoardShuffler refuses to run below two loose pieces, and
            // nothing else in the turn loop ever unsticks it. Only attempt this once the level is
            // otherwise well-formed; a bad size or layout should be reported as that specific
            // problem rather than as an opaque board-build failure.
            if (problems.Count == 0)
            {
                try
                {
                    Board board = BoardBuilder.Build(this, catalog ?? ObstacleCatalog.CreateDefault(), new Rng(1));
                    if (!MoveFinder.HasAny(board, new MatchDetector()))
                        problems.Add("board has no legal move once built -- layout may leave no movable piece");
                }
                catch (InvalidOperationException exception)
                {
                    // BoardBuilder itself only throws when a layout can never be built without an
                    // automatic match (see its own doc comment) -- also a validation failure, not a
                    // crash for this method's caller to handle.
                    problems.Add(exception.Message);
                }
            }

            return problems;
        }

        public override string ToString() => $"{Id} '{Name}' {Width}x{Height} moves={MoveLimit}";
    }
}
