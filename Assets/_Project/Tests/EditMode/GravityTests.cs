using System.Collections.Generic;
using System.Linq;
using Match3.Core;
using NUnit.Framework;

namespace Match3.Tests
{
    public class GravityTests
    {
        private static readonly PieceColor[] NoSpawning = new PieceColor[0];

        private GravityResolver _gravity;
        private Rng _rng;

        [SetUp]
        public void SetUp()
        {
            _gravity = new GravityResolver();
            _rng = new Rng(1234);
        }

        private List<BoardEvent> Settle(Board board, IReadOnlyList<PieceColor> palette = null)
        {
            var events = new List<BoardEvent>();
            bool stable = _gravity.Settle(board, palette ?? NoSpawning, _rng, events);
            Assert.IsTrue(stable, "gravity must reach a stable board within its budget");
            return events;
        }

        [Test]
        public void PieceFallsIntoGapBelow()
        {
            Board board = TestBoard.Parse(@"
                r
                .
                .");

            Settle(board);

            Assert.AreEqual(".\n.\nr", TestBoard.Render(board));
        }

        [Test]
        public void ColumnCollapses_AndEachPieceReportsOneCollapsedMove()
        {
            Board board = TestBoard.Parse(@"
                r
                g
                .
                .");

            List<BoardEvent> events = Settle(board);

            Assert.AreEqual(".\n.\nr\ng", TestBoard.Render(board));

            var moves = events.OfType<EntityMovedEvent>().ToList();
            Assert.AreEqual(2, moves.Count, "one collapsed move event per entity, not one per cell");
            foreach (EntityMovedEvent move in moves)
            {
                Assert.AreEqual(MoveReason.Fall, move.Reason);
                Assert.AreEqual(2, move.From.Y - move.To.Y, "each piece fell exactly two cells");
            }
        }

        [Test]
        public void BlockerDoesNotFall()
        {
            Board board = TestBoard.Parse(@"
                X
                .
                .");

            Settle(board);

            Assert.AreEqual("X\n.\n.", TestBoard.Render(board));
        }

        /// <summary>A crate configured to be gravity-affected, to prove the capability exists.</summary>
        private static readonly ObstacleConfig FallingCrate =
            new ObstacleConfig("falling_crate", new AnyMatchDamageRule(), falls: true);

        [Test]
        public void CratesStayWhereTheLevelPutThem()
        {
            Board board = TestBoard.Parse(@"
                =
                .
                .");

            Settle(board);

            Assert.AreEqual("=\n.\n.", TestBoard.Render(board),
                "the catalog's crates are fixed, so a designed layout keeps its shape");
        }

        [Test]
        public void ACrateConfiguredToFall_DoesFall()
        {
            var board = new Board(1, 3);
            board.SpawnObstacle(new GridPos(0, 2), FallingCrate);
            board.RecomputeSpawners();

            Settle(board);

            Assert.AreEqual(new GridPos(0, 0), board.ObstacleAt(new GridPos(0, 0)).Anchor);
        }

        [Test]
        public void GapUnderABlockerIsFilledDiagonally_WhileThePieceOnTopStaysPut()
        {
            Board board = TestBoard.Parse(@"
                rrr
                .X.
                ...");

            Settle(board);

            // The middle piece rests on the blocker and does not slide off, but the cell beneath the
            // blocker is still reachable — it pulls a piece in from the column beside it.
            Assert.AreEqual(".r.\n.X.\n.rr", TestBoard.Render(board));
        }

        [Test]
        public void GapUnderAHoleIsFilledDiagonally()
        {
            Board board = TestBoard.Parse(@"
                rrr
                .#.
                ...");

            Settle(board);

            Assert.AreEqual(".r.\n.#.\n.rr", TestBoard.Render(board));
        }

        [Test]
        public void ADeepPocketUnderABlockerFillsCompletely()
        {
            // This is the shape that first broke the stress simulation: three cells stacked under a
            // blocker, reachable only from the sides.
            Board board = TestBoard.Parse(@"
                ...
                .X.
                ...
                ...
                ...");

            Settle(board, new[] { PieceColor.Red, PieceColor.Blue, PieceColor.Green });

            foreach (GridPos pos in board.Positions)
                if (board.IsPlayable(pos))
                    Assert.IsTrue(board.IsOccupied(pos), $"cell {pos} was never reached");
        }

        [Test]
        public void PieceWaitsForFallingPieceBelow_RatherThanSlidingAside()
        {
            // Nothing permanent is in the way: both pieces should simply drop straight down.
            Board board = TestBoard.Parse(@"
                .r.
                .g.
                ...
                ...");

            Settle(board);

            Assert.AreEqual("...\n...\n.r.\n.g.", TestBoard.Render(board));
        }

        [Test]
        public void SpawningFillsEveryPlayableCell()
        {
            Board board = TestBoard.Parse(@"
                ...
                .X.
                ..#");

            var palette = new[] { PieceColor.Red, PieceColor.Blue, PieceColor.Green };
            Settle(board, palette);

            foreach (GridPos pos in board.Positions)
            {
                if (!board.IsPlayable(pos))
                    continue;

                Assert.IsTrue(board.IsOccupied(pos), $"cell {pos} should have been filled");
            }
        }

        [Test]
        public void SpawnedPiecesAreReportedOnceAtTheirFinalCell()
        {
            Board board = TestBoard.Parse(@"
                .
                .
                .");

            var palette = new[] { PieceColor.Red };
            List<BoardEvent> events = Settle(board, palette);

            var spawns = events.OfType<EntitySpawnedEvent>().ToList();
            Assert.AreEqual(3, spawns.Count);
            Assert.IsTrue(spawns.All(s => s.FromOutside), "refills enter from outside the board");

            var finalCells = spawns.Select(s => s.Entity.Anchor.Y).OrderBy(y => y).ToList();
            CollectionAssert.AreEqual(new[] { 0, 1, 2 }, finalCells,
                "each spawn is reported at the cell it came to rest in");

            // A spawned piece must not additionally report a fall; its flight is implied.
            var spawnedIds = spawns.Select(s => s.Entity.Id).ToHashSet();
            Assert.IsFalse(events.OfType<EntityMovedEvent>().Any(m => spawnedIds.Contains(m.Id)),
                "spawned pieces are animated by the spawn event alone");
        }

        [Test]
        public void MultiCellCrateFallsOnlyWhenItsWholeFootprintCan()
        {
            var board = new Board(4, 4);
            board.SpawnObstacle(new GridPos(0, 2), FallingCrate, width: 2, height: 2);
            board.RecomputeSpawners();

            Settle(board);

            Obstacle crate = board.ObstacleAt(new GridPos(0, 0));
            Assert.IsNotNull(crate, "the 2x2 crate should have landed on the floor");
            Assert.AreEqual(new GridPos(0, 0), crate.Anchor);
            Assert.AreEqual(2, crate.Width);
            Assert.AreEqual(2, crate.Height);

            // All four cells report the same entity.
            Assert.AreSame(crate, board.EntityAt(new GridPos(1, 1)));
        }

        [Test]
        public void MultiCellCrateStopsOnAnObstruction()
        {
            var board = new Board(4, 4);
            board.SpawnObstacle(new GridPos(0, 0), TestBoard.Catalog.Get(ObstacleCatalog.Blocker));
            board.SpawnObstacle(new GridPos(0, 2), FallingCrate, width: 2, height: 2);
            board.RecomputeSpawners();

            Settle(board);

            Obstacle crate = board.ObstacleAt(new GridPos(0, 1));
            Assert.IsNotNull(crate);
            Assert.AreEqual(new GridPos(0, 1), crate.Anchor,
                "the crate cannot descend past the blocker under one of its cells");
        }

        [Test]
        public void PiecesPileOnTopOfABlocker()
        {
            Board board = TestBoard.Parse(@"
                r
                g
                X");

            Settle(board);

            Assert.AreEqual("r\ng\nX", TestBoard.Render(board));
        }

        [Test]
        public void NothingToDo_ProducesNoEvents()
        {
            Board board = TestBoard.Parse(@"
                rg
                by");

            List<BoardEvent> events = Settle(board);

            Assert.IsEmpty(events);
        }
    }
}
