using System;
using System.Collections.Generic;
using System.Linq;
using Match3.Core;
using NUnit.Framework;

namespace Match3.Tests
{
    public class BoardBuilderTests
    {
        private MatchDetector _detector;
        private ObstacleCatalog _catalog;

        [SetUp]
        public void SetUp()
        {
            _detector = new MatchDetector();
            _catalog = ObstacleCatalog.CreateDefault();
        }

        private Board Build(LevelConfig config, int seed) =>
            BoardBuilder.Build(config, _catalog, new Rng(seed));

        [Test]
        public void GeneratedBoards_ContainNoAutomaticMatches()
        {
            // The spec's hard requirement: a random start board must never already be matched.
            var shapes = new[] { (8, 8, 4), (6, 9, 5), (5, 5, 3), (9, 9, 6), (4, 4, 3) };

            foreach ((int width, int height, int colors) in shapes)
            for (int seed = 1; seed <= 60; seed++)
            {
                LevelConfig config = TestGame.Config(width, height,
                    palette: PieceColors.All.Take(colors), seed: seed);
                Board board = Build(config, seed);

                Assert.IsFalse(_detector.HasAnyMatch(board),
                    $"{width}x{height} with {colors} colours, seed {seed}:\n{TestBoard.Render(board)}");
            }
        }

        [Test]
        public void GeneratedBoards_ContainNo2x2Squares()
        {
            // 2x2 is a match shape in this game because it awards the Plane, so the start board
            // must be free of those too — an easy thing to forget.
            for (int seed = 1; seed <= 60; seed++)
            {
                LevelConfig config = TestGame.Config(8, 8, palette: PieceColors.All.Take(4), seed: seed);
                Board board = Build(config, seed);

                foreach (MatchShape shape in _detector.FindMatches(board).SelectMany(g => g.Shapes))
                    Assert.AreNotEqual(MatchShapeKind.Square, shape.Kind);
            }
        }

        [Test]
        public void GeneratedBoards_AlwaysOfferAtLeastOneMove()
        {
            for (int seed = 1; seed <= 60; seed++)
            {
                LevelConfig config = TestGame.Config(7, 7, palette: PieceColors.All.Take(4), seed: seed);
                Board board = Build(config, seed);

                Assert.IsTrue(MoveFinder.HasAny(board, _detector),
                    $"seed {seed} produced a dead board:\n{TestBoard.Render(board)}");
            }
        }

        [Test]
        public void LayoutThatCanNeverBeBuiltWithoutAMatch_ThrowsInsteadOfSilentlyShippingOne()
        {
            // Three cells fixed to the same explicit colour in a row: PlaceFixedEntities places
            // them unconditionally, so this match exists before FillRandomCells even runs and is
            // identical on every one of the 40 attempts -- no roll of the dice avoids it, and none
            // of the three cells is a free cell RepairMatches is allowed to touch. Build() must
            // say so loudly rather than returning a board that starts with an automatic match.
            var layout = new[]
            {
                "........",
                "........",
                "........",
                "rrr.....",
                "........",
                "........",
                "........",
                "........",
            };
            LevelConfig config = TestGame.Config(8, 8, palette: PieceColors.All.Take(3), layout: layout, seed: 1);

            var exception = Assert.Throws<InvalidOperationException>(() => Build(config, 1));
            StringAssert.Contains(config.Id, exception.Message);
        }

        [Test]
        public void EveryPlayableCellIsFilled()
        {
            LevelConfig config = TestGame.Config(8, 8, palette: PieceColors.All.Take(4), seed: 3);
            Board board = Build(config, 3);

            foreach (GridPos pos in board.Positions)
                if (board.IsPlayable(pos))
                    Assert.IsTrue(board.IsOccupied(pos), $"{pos} was left empty");
        }

        [Test]
        public void OnlyPaletteColoursAreUsed()
        {
            var palette = new[] { PieceColor.Red, PieceColor.Blue, PieceColor.Green };
            LevelConfig config = TestGame.Config(8, 8, palette: palette, seed: 11);
            Board board = Build(config, 11);

            foreach (GridPos pos in board.Positions)
            {
                Piece piece = board.PieceAt(pos);
                if (piece != null)
                    CollectionAssert.Contains(palette, piece.Color);
            }
        }

        [Test]
        public void ColourCountIsConfigurable()
        {
            foreach (int colors in new[] { 3, 4, 5, 6 })
            {
                LevelConfig config = TestGame.Config(8, 8,
                    palette: PieceColors.All.Take(colors), seed: 5);
                Board board = Build(config, 5);

                int distinct = board.Positions
                    .Select(p => board.PieceAt(p))
                    .Where(p => p != null)
                    .Select(p => p.Color)
                    .Distinct()
                    .Count();

                Assert.LessOrEqual(distinct, colors);
            }
        }

        [Test]
        public void LayoutHolesBecomeNonPlayableCells()
        {
            var layout = new[]
            {
                "##..##",
                "#....#",
                "......",
                "......",
                "#....#",
                "##..##",
            };
            LevelConfig config = TestGame.Config(6, 6, palette: PieceColors.All.Take(4),
                layout: layout, seed: 2);

            Board board = Build(config, 2);

            Assert.IsFalse(board.IsPlayable(new GridPos(0, 5)), "top-left corner is a hole");
            Assert.IsFalse(board.IsPlayable(new GridPos(0, 0)), "bottom-left corner is a hole");
            Assert.IsTrue(board.IsPlayable(new GridPos(2, 5)));
            Assert.AreEqual(6 * 6 - 12, board.PlayableCellCount);
        }

        [Test]
        public void LayoutPlacesEachKindOfBoardElement()
        {
            var layout = new[]
            {
                "......",
                ".=.X..",
                "..R.*.",
                "......",
                "......",
                "......",
            };
            LevelConfig config = TestGame.Config(6, 6, palette: PieceColors.All.Take(4),
                layout: layout, seed: 4);

            Board board = Build(config, 4);

            List<Obstacle> obstacles = board.AllEntities().OfType<Obstacle>().ToList();
            CollectionAssert.AreEquivalent(
                new[]
                {
                    ObstacleCatalog.Box, ObstacleCatalog.Blocker,
                    ObstacleCatalog.ColoredBox, ObstacleCatalog.CyclingBox,
                },
                obstacles.Select(o => o.Config.Id).ToArray());

            Obstacle colored = obstacles.Single(o => o.Config.Id == ObstacleCatalog.ColoredBox);
            Assert.AreEqual(PieceColor.Red, colored.RequiredColor, "'R' means a red-requiring crate");

            // The blocker does not fall; the crates do, so they end up on the floor.
            Obstacle blocker = obstacles.Single(o => o.Config.Id == ObstacleCatalog.Blocker);
            Assert.AreEqual(new GridPos(3, 4), blocker.Anchor);
        }

        [Test]
        public void SpecificAndRandomPiecesCanBothBePlacedFromTheLayout()
        {
            var layout = new[]
            {
                "......",
                "......",
                "......",
                "......",
                "..gy..",
                "..rb..",
            };
            LevelConfig config = TestGame.Config(6, 6, palette: PieceColors.All.Take(4),
                layout: layout, seed: 9);

            Board board = Build(config, 9);

            Assert.AreEqual(PieceColor.Red, board.PieceAt(new GridPos(2, 0)).Color);
            Assert.AreEqual(PieceColor.Blue, board.PieceAt(new GridPos(3, 0)).Color);
            Assert.AreEqual(PieceColor.Green, board.PieceAt(new GridPos(2, 1)).Color);
            Assert.AreEqual(PieceColor.Yellow, board.PieceAt(new GridPos(3, 1)).Color);
        }

        [Test]
        public void OverridesCanPlaceAMultiCellCrateHoldingSomething()
        {
            LevelConfig config = TestGame.Config(8, 8, palette: PieceColors.All.Take(4), seed: 6);
            config.Overrides.Add(new CellOverride(2, 0, EntitySpec.Obstacle(
                ObstacleCatalog.Box,
                hp: 3,
                contains: EntitySpec.Obstacle(ObstacleCatalog.Blocker),
                width: 2,
                height: 2)));

            Board board = Build(config, 6);

            Obstacle crate = board.ObstacleAt(new GridPos(2, 0));
            Assert.IsNotNull(crate);
            Assert.AreEqual(3, crate.Hp);
            Assert.AreEqual(2, crate.Width);
            Assert.AreEqual(2, crate.Height);
            Assert.AreSame(crate, board.ObstacleAt(new GridPos(3, 1)),
                "all four cells resolve to the same entity");
            Assert.IsNotNull(crate.Contains);
        }

        [Test]
        public void ObstacleCellsAreNotAlsoFilledWithPieces()
        {
            var layout = new[]
            {
                "......",
                "......",
                "......",
                "......",
                "..XX..",
                "..XX..",
            };
            LevelConfig config = TestGame.Config(6, 6, palette: PieceColors.All.Take(4),
                layout: layout, seed: 8);

            Board board = Build(config, 8);

            foreach (GridPos pos in new[]
                     {
                         new GridPos(2, 0), new GridPos(3, 0),
                         new GridPos(2, 1), new GridPos(3, 1),
                     })
                Assert.IsInstanceOf<Obstacle>(board.EntityAt(pos), $"{pos} should still hold the blocker");
        }

        [Test]
        public void SeedProducesTheSameBoardEveryTime()
        {
            LevelConfig config = TestGame.Config(8, 8, palette: PieceColors.All.Take(4), seed: 42);

            string first = TestBoard.Render(Build(config, 42));
            string second = TestBoard.Render(Build(config, 42));

            Assert.AreEqual(first, second, "board generation must be reproducible from its seed");
        }

        [Test]
        public void DifferentSeedsProduceDifferentBoards()
        {
            LevelConfig config = TestGame.Config(8, 8, palette: PieceColors.All.Take(4), seed: 1);

            string first = TestBoard.Render(Build(config, 1));
            string second = TestBoard.Render(Build(config, 2));

            Assert.AreNotEqual(first, second);
        }
    }
}
