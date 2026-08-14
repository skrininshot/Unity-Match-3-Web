using System.Collections.Generic;
using System.Linq;
using Match3.Core;
using NUnit.Framework;

namespace Match3.Tests
{
    public class TurnResolverTests
    {
        // Swapping (2,1) with (2,2) completes r-r-r along row 1.
        private const string SimpleMatchBoard = @"
            gyb
            rrg
            byr";

        [Test]
        public void ValidSwap_IsAccepted_AndClearsBeforeFalling()
        {
            TestHarness game = TestGame.FromArt(SimpleMatchBoard);

            TurnResult result = game.Swap(2, 1, 2, 0);

            Assert.IsTrue(result.Accepted, result.RejectionReason);

            List<PhaseKind> kinds = result.Phases.Select(p => p.Kind).ToList();
            Assert.AreEqual(PhaseKind.Swap, kinds[0]);
            Assert.AreEqual(PhaseKind.Clear, kinds[1]);
            Assert.AreEqual(PhaseKind.Fall, kinds[2]);
            Assert.AreEqual(PhaseKind.Outcome, kinds[kinds.Count - 1]);
        }

        [Test]
        public void ClearingAndFallingNeverShareAPhase()
        {
            // This is the structural guarantee behind "pieces must not start falling until the
            // removal effect has finished".
            TestHarness game = TestGame.FromArt(SimpleMatchBoard);

            TurnResult result = game.Swap(2, 1, 2, 0);

            foreach (TurnPhase phase in result.Phases)
            {
                bool hasClears = phase.Events.Any(e => e is EntityClearedEvent);
                bool hasFalls = phase.Events.Any(e => e is EntityMovedEvent m && m.Reason == MoveReason.Fall);

                Assert.IsFalse(hasClears && hasFalls,
                    $"phase {phase.Kind} mixes removals and falls");
            }
        }

        [Test]
        public void MatchedPiecesAreRemovedFromTheBoard()
        {
            TestHarness game = TestGame.FromArt(SimpleMatchBoard);

            TurnResult result = game.Swap(2, 1, 2, 0);

            List<GridPos> cleared = TestGame.ClearedCells(result);
            CollectionAssert.AreEquivalent(
                new[] { new GridPos(0, 1), new GridPos(1, 1), new GridPos(2, 1) },
                cleared);
        }

        [Test]
        public void InvalidSwap_IsRejected_WithABounce_AndLeavesBoardUnchanged()
        {
            TestHarness game = TestGame.FromArt(SimpleMatchBoard);
            string before = game.Render();

            TurnResult result = game.Swap(0, 0, 1, 0);

            Assert.IsFalse(result.Accepted);
            Assert.AreEqual(2, result.Phases.Count);
            Assert.AreEqual(PhaseKind.Swap, result.Phases[0].Kind);
            Assert.AreEqual(PhaseKind.SwapRevert, result.Phases[1].Kind);
            Assert.AreEqual(before, game.Render(), "a rejected swap must not touch the board");
            Assert.AreEqual(50, game.Level.MovesLeft, "a rejected swap must not cost a move");
        }

        [Test]
        public void NonAdjacentSwap_IsRejectedWithoutAnimation()
        {
            TestHarness game = TestGame.FromArt(SimpleMatchBoard);

            TurnResult result = game.Swap(0, 0, 2, 2);

            Assert.IsFalse(result.Accepted);
            Assert.IsEmpty(result.Phases);
        }

        [Test]
        public void SwapWithAnObstacle_IsRejected()
        {
            TestHarness game = TestGame.FromArt(@"
                gyb
                rXg
                byr");

            TurnResult result = game.Swap(0, 1, 1, 1);

            Assert.IsFalse(result.Accepted);
            Assert.IsEmpty(result.Phases);
        }

        [Test]
        public void CascadeResolvesFollowUpMatches()
        {
            // Clearing r-r-r on row 3 drops column 0 so that three blues line up underneath.
            TestHarness game = TestGame.FromArt(@"
                byg
                bgr
                rrg
                bgy
                gyb
                ygb");

            TurnResult result = game.Swap(2, 3, 2, 4);

            Assert.IsTrue(result.Accepted, result.RejectionReason);
            Assert.AreEqual(2, result.CountPhases(PhaseKind.Clear),
                "one clear for the swap match and one for the cascade");
            Assert.GreaterOrEqual(result.CountPhases(PhaseKind.Fall), 1);

            var clearedColors = result.EventsOf<EntityClearedEvent>()
                .Select(e => e.Color)
                .ToList();
            Assert.AreEqual(3, clearedColors.Count(c => c == PieceColor.Red));
            Assert.AreEqual(3, clearedColors.Count(c => c == PieceColor.Blue),
                "the cascade cleared the blue column");

            // The order matters: the cascade clear must come after the first fall.
            List<PhaseKind> kinds = result.Phases.Select(p => p.Kind).ToList();
            int firstClear = kinds.IndexOf(PhaseKind.Clear);
            int firstFall = kinds.IndexOf(PhaseKind.Fall);
            int lastClear = kinds.LastIndexOf(PhaseKind.Clear);
            Assert.Less(firstClear, firstFall);
            Assert.Less(firstFall, lastClear);
        }

        [Test]
        public void MoveIsConsumed_AndReported()
        {
            TestHarness game = TestGame.FromArt(SimpleMatchBoard, moveLimit: 5);

            TurnResult result = game.Swap(2, 1, 2, 0);

            Assert.AreEqual(4, result.MovesLeft);
            Assert.AreEqual(4, game.Level.MovesLeft);
            Assert.AreEqual(4, result.EventsOf<MovesLeftChangedEvent>().Single().MovesLeft);
        }

        [Test]
        public void GoalProgressIsCredited()
        {
            TestHarness game = TestGame.FromArt(SimpleMatchBoard, goalColor: PieceColor.Red, goalCount: 10);

            TurnResult result = game.Swap(2, 1, 2, 0);

            GoalState goal = game.Level.Goals.Goals.Single();
            Assert.AreEqual(3, goal.Collected);
            Assert.AreEqual(3, result.EventsOf<GoalProgressEvent>().Last().Collected);
        }

        [Test]
        public void CompletingTheGoalWinsTheLevel()
        {
            TestHarness game = TestGame.FromArt(SimpleMatchBoard, goalColor: PieceColor.Red, goalCount: 3);

            TurnResult result = game.Swap(2, 1, 2, 0);

            Assert.AreEqual(LevelOutcome.Won, result.Outcome);
            Assert.AreEqual(LevelOutcome.Won, game.Level.Outcome);
            Assert.AreEqual(LevelOutcome.Won, result.EventsOf<OutcomeEvent>().Last().Outcome);
        }

        [Test]
        public void RunningOutOfMovesLosesTheLevel()
        {
            TestHarness game = TestGame.FromArt(SimpleMatchBoard, moveLimit: 1, goalCount: 9999);

            TurnResult result = game.Swap(2, 1, 2, 0);

            Assert.AreEqual(LevelOutcome.Lost, result.Outcome);
            Assert.AreEqual(0, result.MovesLeft);
        }

        [Test]
        public void GoalWinsOverRunningOutOfMovesOnTheSameTurn()
        {
            TestHarness game = TestGame.FromArt(SimpleMatchBoard, moveLimit: 1,
                goalColor: PieceColor.Red, goalCount: 3);

            TurnResult result = game.Swap(2, 1, 2, 0);

            Assert.AreEqual(LevelOutcome.Won, result.Outcome,
                "finishing the goal on the last move is a win, not a loss");
        }

        [Test]
        public void ActionsAfterTheLevelEnded_AreRejected()
        {
            TestHarness game = TestGame.FromArt(SimpleMatchBoard, goalColor: PieceColor.Red, goalCount: 3);
            game.Swap(2, 1, 2, 0);

            TurnResult second = game.Swap(0, 0, 0, 1);

            Assert.IsFalse(second.Accepted);
            Assert.IsEmpty(second.Phases);
        }

        [Test]
        public void MatchOfFour_CreatesALineBoosterWhereThePlayerActed()
        {
            // Bringing the fourth red into row 1 from above.
            TestHarness game = TestGame.FromArt(@"
                gyry
                rrgr
                bygb");

            TurnResult result = game.Swap(2, 1, 2, 2);

            Assert.IsTrue(result.Accepted, result.RejectionReason);

            BoosterCreatedEvent created = result.EventsOf<BoosterCreatedEvent>().Single();
            Assert.AreEqual(BoosterType.Line, created.Entity.Booster);
            Assert.AreEqual(LineOrientation.Horizontal, created.Entity.Orientation);
            Assert.AreEqual(new GridPos(2, 1), created.Entity.Anchor,
                "the booster appears in the cell the player moved the piece into");

            Assert.AreEqual(3, result.EventsOf<EntityClearedEvent>().Count(),
                "four matched cells minus the one promoted to a booster");
        }

        [Test]
        public void MatchOfFive_CreatesARainbow_WhichIsColourless()
        {
            TestHarness game = TestGame.FromArt(@"
                gyrgy
                rrgrr
                gbgbg");

            TurnResult result = game.Swap(2, 1, 2, 2);

            Assert.IsTrue(result.Accepted, result.RejectionReason);
            BoosterCreatedEvent created = result.EventsOf<BoosterCreatedEvent>().Single();
            Assert.AreEqual(BoosterType.Rainbow, created.Entity.Booster);
            Assert.AreEqual(PieceColor.None, created.Entity.Color);
        }

        [Test]
        public void CornerMatch_CreatesABomb()
        {
            // The red dropped into (1,2) completes row 2 and column 1 at once, forming a T.
            // Note it has to arrive from *outside* both lines — every neighbour of a plus-shape's
            // centre is already part of the cross, so that shape can never be made by a swap.
            TestHarness game = TestGame.FromArt(@"
                brb
                rgr
                gry
                yrb");

            TurnResult result = game.Swap(1, 3, 1, 2);

            Assert.IsTrue(result.Accepted, result.RejectionReason);
            BoosterCreatedEvent created = result.EventsOf<BoosterCreatedEvent>().Single();
            Assert.AreEqual(BoosterType.Bomb, created.Entity.Booster);
        }

        [Test]
        public void SquareMatch_CreatesAPlane()
        {
            // Swapping brings the fourth red into the 2x2 block at the bottom left. The group has
            // no run of three at all, which is exactly why it awards a Plane.
            TestHarness game = TestGame.FromArt(@"
                yrby
                rgby
                rrgb");

            TurnResult result = game.Swap(1, 1, 1, 2);

            Assert.IsTrue(result.Accepted, result.RejectionReason);
            BoosterCreatedEvent created = result.EventsOf<BoosterCreatedEvent>().Single();
            Assert.AreEqual(BoosterType.Plane, created.Entity.Booster);
        }
    }
}
