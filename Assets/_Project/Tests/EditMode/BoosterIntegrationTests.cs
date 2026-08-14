using System.Collections.Generic;
using System.Linq;
using Match3.Core;
using NUnit.Framework;

namespace Match3.Tests
{
    /// <summary>Boosters as the player meets them: taps, swaps, and chain reactions.</summary>
    public class BoosterIntegrationTests
    {
        private const string Checkerboard5 = @"
            bgbgb
            gbgbg
            bgbgb
            gbgbg
            bgbgb";

        [Test]
        public void TappingALineBooster_ClearsItsWholeRow()
        {
            TestHarness game = TestGame.FromArt(Checkerboard5);
            game.MakeBooster(2, 2, BoosterType.Line, LineOrientation.Horizontal);

            TurnResult result = game.Tap(2, 2);

            Assert.IsTrue(result.Accepted, result.RejectionReason);
            List<GridPos> cleared = TestGame.ClearedCells(result);
            for (int x = 0; x < 5; x++)
                CollectionAssert.Contains(cleared, new GridPos(x, 2));
        }

        [Test]
        public void TappingABooster_CostsAMove()
        {
            TestHarness game = TestGame.FromArt(Checkerboard5, moveLimit: 7);
            game.MakeBooster(2, 2, BoosterType.Bomb);

            TurnResult result = game.Tap(2, 2);

            Assert.AreEqual(6, result.MovesLeft);
        }

        [Test]
        public void TappingAPlainPiece_IsRejected()
        {
            TestHarness game = TestGame.FromArt(Checkerboard5);

            TurnResult result = game.Tap(2, 2);

            Assert.IsFalse(result.Accepted);
            Assert.IsEmpty(result.Phases);
        }

        [Test]
        public void ABoosterCaughtInAnotherBlast_FiresToo()
        {
            TestHarness game = TestGame.FromArt(Checkerboard5);
            game.MakeBooster(0, 2, BoosterType.Line, LineOrientation.Horizontal);
            game.MakeBooster(3, 2, BoosterType.Bomb);

            TurnResult result = game.Tap(0, 2);

            List<BoosterActivatedEvent> activations = TestGame.Activations(result);
            Assert.AreEqual(2, activations.Count, "the line's blast set off the bomb");
            Assert.AreEqual(BoosterType.Line, activations[0].Type);
            Assert.AreEqual(BoosterType.Bomb, activations[1].Type);
            Assert.AreNotEqual(activations[0].SourceId, activations[1].SourceId);
        }

        [Test]
        public void ChainReactionsHappenInSeparatePhases()
        {
            TestHarness game = TestGame.FromArt(Checkerboard5);
            game.MakeBooster(0, 2, BoosterType.Line, LineOrientation.Horizontal);
            game.MakeBooster(3, 2, BoosterType.Bomb);

            TurnResult result = game.Tap(0, 2);

            // The view relies on this: one wave per phase, so explosions read in sequence.
            var phasesWithActivations = result.Phases
                .Where(p => p.Events.Any(e => e is BoosterActivatedEvent))
                .ToList();
            Assert.AreEqual(2, phasesWithActivations.Count);
        }

        [Test]
        public void AMatchedBoosterFires()
        {
            TestHarness game = TestGame.FromArt(@"
                gyb
                rrg
                byr");
            game.MakeBooster(0, 1, BoosterType.Bomb);

            TurnResult result = game.Swap(2, 1, 2, 0);

            Assert.IsTrue(result.Accepted, result.RejectionReason);
            BoosterActivatedEvent activation = TestGame.Activations(result).Single();
            Assert.AreEqual(BoosterType.Bomb, activation.Type,
                "a booster destroyed by a match activates — this is the main source of chains");
        }

        [Test]
        public void SwappingTwoLineBoosters_ClearsACross()
        {
            TestHarness game = TestGame.FromArt(Checkerboard5);
            game.MakeBooster(1, 1, BoosterType.Line, LineOrientation.Horizontal);
            game.MakeBooster(2, 1, BoosterType.Line, LineOrientation.Vertical);

            TurnResult result = game.Swap(1, 1, 2, 1);

            Assert.IsTrue(result.Accepted, result.RejectionReason);

            List<GridPos> cleared = TestGame.ClearedCells(result);
            for (int x = 0; x < 5; x++)
                CollectionAssert.Contains(cleared, new GridPos(x, 1));
            for (int y = 0; y < 5; y++)
                CollectionAssert.Contains(cleared, new GridPos(2, y));
        }

        [Test]
        public void SwappingARainbowWithAGem_ErasesThatColour()
        {
            TestHarness game = TestGame.FromArt(Checkerboard5);
            game.MakeBooster(1, 1, BoosterType.Rainbow);

            // (2,1) is green on this board, so green is what should go.
            Assert.AreEqual(PieceColor.Green, game.PieceAt(2, 1).Color);

            TurnResult result = game.Swap(1, 1, 2, 1);

            Assert.IsTrue(result.Accepted, result.RejectionReason);
            int greensCleared = result.EventsOf<EntityClearedEvent>()
                .Count(e => e.Color == PieceColor.Green);
            Assert.AreEqual(12, greensCleared, "every green piece on the board");
        }

        [Test]
        public void SwappingARainbowWithAGem_IsLegalEvenWithoutAMatch()
        {
            TestHarness game = TestGame.FromArt(Checkerboard5);
            game.MakeBooster(1, 1, BoosterType.Rainbow);

            SwapKind kind = SwapRules.Classify(game.Board, game.Resolver.Detector,
                new GridPos(1, 1), new GridPos(2, 1));

            Assert.AreEqual(SwapKind.RainbowColor, kind);
        }

        [Test]
        public void SwappingAPlaneOntoAPlainPiece_IsLegalEvenWithoutAMatch_AndFiresIt()
        {
            TestHarness game = TestGame.FromArt(Checkerboard5);
            game.MakeBooster(1, 0, BoosterType.Plane);

            // On an interior checkerboard swap, breaking the alternation almost always creates a
            // match (the far side of one swapped cell shares the other's new colour). The bottom
            // row has no y=-1 neighbour, so (1,0) <-> (2,0) is one of the few swaps that truly makes
            // no match at all -- horizontal runs land at 2, and both affected columns only reach 2
            // going upward.
            SwapKind kind = SwapRules.Classify(game.Board, game.Resolver.Detector,
                new GridPos(1, 0), new GridPos(2, 0));
            Assert.AreEqual(SwapKind.BoosterRelocate, kind,
                "aiming a booster at a cell, not just tapping it in place, should always be legal");

            TurnResult result = game.Swap(1, 0, 2, 0);

            Assert.IsTrue(result.Accepted, result.RejectionReason);

            // The Plane's own target is picked by an RNG tie-break (nothing on this checkerboard
            // matches the level's goal colour, so every cell scores equally) and can itself cascade
            // into further matches -- that's legitimate emergent play, not something this test
            // should pin down. What must hold is that the relocated Plane fired at all.
            List<BoosterActivatedEvent> activations = TestGame.Activations(result);
            Assert.IsTrue(activations.Any(a => a.Type == BoosterType.Plane),
                "the relocated Plane should still fire");
        }

        [Test]
        public void SwappingABoosterIntoARunOfItsColour_MatchesInsteadOfJustRelocating()
        {
            // Bottom row (y=0) is two reds and a blue; top row (y=1) has a red at (2,1) about to
            // become a Plane. Swapping (2,0)<->(2,1) lines that Plane up as the third red in a row.
            TestHarness game = TestGame.FromArt(@"
                ggr
                rrb");
            game.MakeBooster(2, 1, BoosterType.Plane, color: PieceColor.Red);

            SwapKind kind = SwapRules.Classify(game.Board, game.Resolver.Detector,
                new GridPos(2, 0), new GridPos(2, 1));
            Assert.AreEqual(SwapKind.Match, kind,
                "a booster swapped into a matching run must complete the match, not just relocate");

            TurnResult result = game.Swap(2, 0, 2, 1);

            Assert.IsTrue(result.Accepted, result.RejectionReason);
            List<GridPos> cleared = TestGame.ClearedCells(result);
            CollectionAssert.Contains(cleared, new GridPos(0, 0));
            CollectionAssert.Contains(cleared, new GridPos(1, 0));
            CollectionAssert.Contains(cleared, new GridPos(2, 0));

            // The Plane was part of the matched run, so it still fires -- as a chained activation
            // from the match's own clear, not as the whole point of the swap.
            BoosterActivatedEvent activation = TestGame.Activations(result).Single();
            Assert.AreEqual(BoosterType.Plane, activation.Type);
        }

        [Test]
        public void SwappingABoosterNextToAnUnrelatedMatch_StillFiresTheBooster(
            [Values(BoosterType.Plane, BoosterType.Line, BoosterType.Bomb)] BoosterType type)
        {
            // Reported live: a booster on the second-from-bottom row, swiped down, silently did
            // nothing. Root cause had nothing to do with position -- it is just the shape of swap
            // most likely to produce this: the piece that moves UP completes a match of its own
            // (top row here) while the booster's own new cell (bottom row) matches nothing at all.
            // WouldSwapMatch only asks "did the swap make a match anywhere", so this classified as
            // Match -- and until now, TurnResolver.Swap did nothing special for Match, trusting
            // RunCascades' fresh FindMatches to pick up the booster too. That only works if the
            // booster's own cell is actually part of the match; here it never was, so the booster
            // just relocated and was silently discarded, costing the player a move for nothing.
            TestHarness game = TestGame.FromArt(@"
                rgr
                bry");
            game.MakeBooster(1, 1, type, color: PieceColor.Green);

            SwapKind kind = SwapRules.Classify(game.Board, game.Resolver.Detector,
                new GridPos(1, 1), new GridPos(1, 0));
            Assert.AreEqual(SwapKind.Match, kind,
                "the swap does make a match -- just not at the booster's own landing cell");

            TurnResult result = game.Swap(1, 1, 1, 0);

            Assert.IsTrue(result.Accepted, result.RejectionReason);

            // The unrelated match at the top row still resolves normally.
            List<GridPos> cleared = TestGame.ClearedCells(result);
            CollectionAssert.Contains(cleared, new GridPos(0, 1));
            CollectionAssert.Contains(cleared, new GridPos(1, 1));
            CollectionAssert.Contains(cleared, new GridPos(2, 1));

            // And the booster, having relocated to a cell that matched nothing, still fires --
            // exactly as if it had been tapped, instead of quietly sitting there unfired.
            BoosterActivatedEvent activation = TestGame.Activations(result).Single();
            Assert.AreEqual(type, activation.Type);
        }

        [Test]
        public void ABoardFullOfBombs_ChainsToCompletion_WithoutFiringAnythingTwice()
        {
            TestHarness game = TestGame.FromArt(@"
                bgbgbg
                gbgbgb
                bgbgbg
                gbgbgb
                bgbgbg
                gbgbgb");
            game.MakeEveryPieceABooster(BoosterType.Bomb);

            TurnResult result = game.Tap(0, 0);

            Assert.IsTrue(result.Accepted, result.RejectionReason);
            Assert.IsFalse(result.GravityStalled);

            List<BoosterActivatedEvent> activations = TestGame.Activations(result);
            Assert.AreEqual(36, activations.Count, "every bomb on the board went off");
            Assert.AreEqual(36, activations.Select(a => a.SourceId).Distinct().Count(),
                "and none of them went off twice");

            foreach (GridPos pos in game.Board.Positions)
                Assert.IsFalse(game.Board.IsOccupied(pos), $"cell {pos} should have been cleared");
        }

        [Test]
        public void LinePlusBombSwap_ClearsAThickCross()
        {
            TestHarness game = TestGame.FromArt(Checkerboard5);
            game.MakeBooster(1, 2, BoosterType.Line, LineOrientation.Horizontal);
            game.MakeBooster(2, 2, BoosterType.Bomb);

            TurnResult result = game.Swap(1, 2, 2, 2);

            Assert.IsTrue(result.Accepted, result.RejectionReason);

            // Centred on (2,2): rows 1..3 and columns 1..3 in full.
            List<GridPos> cleared = TestGame.ClearedCells(result);
            for (int x = 0; x < 5; x++)
            for (int y = 1; y <= 3; y++)
                CollectionAssert.Contains(cleared, new GridPos(x, y));
        }

        [Test]
        public void RainbowPlusRainbowSwap_ClearsTheEntireBoard()
        {
            TestHarness game = TestGame.FromArt(Checkerboard5);
            game.MakeBooster(1, 1, BoosterType.Rainbow);
            game.MakeBooster(2, 1, BoosterType.Rainbow);

            TurnResult result = game.Swap(1, 1, 2, 1);

            Assert.IsTrue(result.Accepted, result.RejectionReason);
            foreach (GridPos pos in game.Board.Positions)
                Assert.IsFalse(game.Board.IsOccupied(pos), $"cell {pos} should be empty");
        }

        [Test]
        public void PlanePlusPlaneSwap_HitsTwoDifferentCells()
        {
            TestHarness game = TestGame.FromArt(@"
                bgbgb
                gbgbg
                bgbgb
                gbgbg
                rbgbr", goalColor: PieceColor.Red, goalCount: 9);
            game.MakeBooster(1, 3, BoosterType.Plane);
            game.MakeBooster(2, 3, BoosterType.Plane);

            TurnResult result = game.Swap(1, 3, 2, 3);

            Assert.IsTrue(result.Accepted, result.RejectionReason);
            List<BoosterActivatedEvent> activations = TestGame.Activations(result);
            Assert.AreEqual(2, activations.Count);
            Assert.IsNotNull(activations[0].FlyTo);
            Assert.IsNotNull(activations[1].FlyTo);
            Assert.AreNotEqual(activations[0].FlyTo, activations[1].FlyTo,
                "two planes must not waste themselves on the same cell");
        }
    }
}
