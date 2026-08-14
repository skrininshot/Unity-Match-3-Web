using System.Collections.Generic;
using System.Linq;
using Match3.Core;
using NUnit.Framework;

namespace Match3.Tests
{
    /// <summary>
    /// Each booster's blast area, resolved in isolation. Keeping these separate from the resolver
    /// means an area bug cannot hide behind cascade noise.
    /// </summary>
    public class BoosterEffectTests
    {
        // 5x5 alternating board: no runs of three and no 2x2 blocks anywhere.
        private const string Checkerboard = @"
            bgbgb
            gbgbg
            bgbgb
            gbgbg
            bgbgb";

        [Test]
        public void Line_Horizontal_CoversTheWholeRow()
        {
            TestHarness game = TestGame.FromArt(Checkerboard);

            BoosterContext context = game.ResolveEffect(
                ActivationRequest.Line(new GridPos(2, 3), LineOrientation.Horizontal));

            CollectionAssert.AreEquivalent(
                Enumerable.Range(0, 5).Select(x => new GridPos(x, 3)).ToArray(),
                context.Affected);
        }

        [Test]
        public void Line_Vertical_CoversTheWholeColumn()
        {
            TestHarness game = TestGame.FromArt(Checkerboard);

            BoosterContext context = game.ResolveEffect(
                ActivationRequest.Line(new GridPos(1, 2), LineOrientation.Vertical));

            CollectionAssert.AreEquivalent(
                Enumerable.Range(0, 5).Select(y => new GridPos(1, y)).ToArray(),
                context.Affected);
        }

        [Test]
        public void Line_Thickness3_CoversThreeRows()
        {
            TestHarness game = TestGame.FromArt(Checkerboard);

            BoosterContext context = game.ResolveEffect(
                ActivationRequest.Line(new GridPos(2, 2), LineOrientation.Horizontal, thickness: 3));

            Assert.AreEqual(15, context.Affected.Count);
            CollectionAssert.AreEquivalent(new[] { 1, 2, 3 },
                context.Affected.Select(p => p.Y).Distinct().ToArray());
        }

        [Test]
        public void Bomb_DefaultRadiusCovers5x5()
        {
            TestHarness game = TestGame.FromArt(Checkerboard);

            BoosterContext context = game.ResolveEffect(ActivationRequest.Bomb(new GridPos(2, 2)));

            Assert.AreEqual(25, context.Affected.Count, "5x5 fits exactly on a 5x5 board");
        }

        [Test]
        public void Bomb_IsClippedToTheBoard()
        {
            TestHarness game = TestGame.FromArt(Checkerboard);

            BoosterContext context = game.ResolveEffect(ActivationRequest.Bomb(new GridPos(0, 0)));

            Assert.AreEqual(9, context.Affected.Count, "a corner blast only covers the 3x3 that exists");
            Assert.IsTrue(context.Affected.All(p => p.X <= 2 && p.Y <= 2));
        }

        [Test]
        public void Bomb_SkipsHoles()
        {
            TestHarness game = TestGame.FromArt(@"
                bgb
                g#g
                bgb");

            BoosterContext context = game.ResolveEffect(ActivationRequest.Bomb(new GridPos(1, 1)));

            Assert.AreEqual(8, context.Affected.Count);
            CollectionAssert.DoesNotContain(context.Affected, new GridPos(1, 1));
        }

        [Test]
        public void Rainbow_ClearsOnlyTheChosenColour()
        {
            TestHarness game = TestGame.FromArt(Checkerboard);

            BoosterContext context = game.ResolveEffect(
                ActivationRequest.Rainbow(new GridPos(0, 0), PieceColor.Blue));

            Assert.AreEqual(PieceColor.Blue, context.ChosenColor);
            Assert.IsTrue(context.Affected.All(p => game.PieceAt(p.X, p.Y).Color == PieceColor.Blue));
            Assert.AreEqual(13, context.Affected.Count, "blue occupies 13 of the 25 checkerboard cells");
        }

        [Test]
        public void Rainbow_EntireBoard_CoversEveryPlayableCell()
        {
            TestHarness game = TestGame.FromArt(@"
                bgb
                g#g
                bgb");

            BoosterContext context = game.ResolveEffect(
                ActivationRequest.Rainbow(new GridPos(0, 0), PieceColor.None, entireBoard: true));

            Assert.AreEqual(8, context.Affected.Count);
            CollectionAssert.DoesNotContain(context.Affected, new GridPos(1, 1));
        }

        [Test]
        public void Rainbow_WithoutAColour_PrefersTheGoalColour()
        {
            // Green is the majority colour, but red is what the level asks for.
            TestHarness game = TestGame.FromArt(@"
                gggg
                gggg
                grgr", goalColor: PieceColor.Red, goalCount: 5);

            BoosterContext context = game.ResolveEffect(
                ActivationRequest.Rainbow(new GridPos(0, 0), PieceColor.None));

            Assert.AreEqual(PieceColor.Red, context.ChosenColor);
            Assert.AreEqual(2, context.Affected.Count);
        }

        [Test]
        public void Rainbow_WithoutAColour_FallsBackToTheMostCommonColour()
        {
            TestHarness game = TestGame.FromArt(@"
                gggg
                gggg
                gbgb", goalColor: PieceColor.Red, goalCount: 5);

            BoosterContext context = game.ResolveEffect(
                ActivationRequest.Rainbow(new GridPos(0, 0), PieceColor.None));

            Assert.AreEqual(PieceColor.Green, context.ChosenColor,
                "no red on the board, so it picks the colour it can hit most of");
        }

        [Test]
        public void Plane_TargetsAGoalColouredPiece()
        {
            TestHarness game = TestGame.FromArt(@"
                gbgb
                bgbg
                gbrb", goalColor: PieceColor.Red, goalCount: 5);

            BoosterContext context = game.ResolveEffect(ActivationRequest.Plane(new GridPos(0, 2)));

            Assert.AreEqual(new GridPos(2, 0), context.FlyTarget,
                "the only red piece is the most useful thing to destroy");
            CollectionAssert.AreEqual(new[] { new GridPos(2, 0) }, context.Affected);
        }

        [Test]
        public void Plane_PrefersABoosterOverAPlainPiece()
        {
            TestHarness game = TestGame.FromArt(@"
                gbgb
                bgbg
                gbgb", goalColor: PieceColor.Red, goalCount: 5);
            game.MakeBooster(3, 1, BoosterType.Bomb);

            BoosterContext context = game.ResolveEffect(ActivationRequest.Plane(new GridPos(0, 2)));

            Assert.AreEqual(new GridPos(3, 1), context.FlyTarget,
                "no goal colour on the board, so chaining into a booster is the best move");
        }

        [Test]
        public void Plane_PrefersACrateOverAPlainPiece()
        {
            TestHarness game = TestGame.FromArt(@"
                gbgb
                bg=g
                gbgb", goalColor: PieceColor.Red, goalCount: 5);

            BoosterContext context = game.ResolveEffect(ActivationRequest.Plane(new GridPos(0, 2)));

            Assert.AreEqual(new GridPos(2, 1), context.FlyTarget);
        }

        [Test]
        public void Plane_IgnoresIndestructibleBlockers()
        {
            TestHarness game = TestGame.FromArt(@"
                gbgb
                bgXg
                gbgb", goalColor: PieceColor.Red, goalCount: 5);

            BoosterContext context = game.ResolveEffect(ActivationRequest.Plane(new GridPos(0, 2)));

            Assert.AreNotEqual(new GridPos(2, 1), context.FlyTarget,
                "there is no point flying into something that cannot be destroyed");
        }

        [Test]
        public void Plane_WithPayload_QueuesTheCarriedBoosterAtItsTarget()
        {
            TestHarness game = TestGame.FromArt(@"
                gbgb
                bgbg
                gbrb", goalColor: PieceColor.Red, goalCount: 5);

            BoosterContext context = game.ResolveEffect(
                ActivationRequest.Plane(new GridPos(0, 2), BoosterType.Bomb));

            Assert.AreEqual(new GridPos(2, 0), context.FlyTarget);
            ActivationRequest followUp = context.FollowUps.Single();
            Assert.AreEqual(BoosterType.Bomb, followUp.Type);
            Assert.AreEqual(new GridPos(2, 0), followUp.At);
        }

        [Test]
        public void Plane_RespectsReservedTargets_SoTwoPlanesPickTwoCells()
        {
            TestHarness game = TestGame.FromArt(@"
                gbgb
                bgbg
                grbr", goalColor: PieceColor.Red, goalCount: 5);

            var reserved = new HashSet<GridPos>();
            game.Boosters.TryGet(BoosterType.Plane, out IBoosterEffect effect);

            var first = new BoosterContext(game.Board, game.Level, game.Rng, reserved);
            effect.Resolve(ActivationRequest.Plane(new GridPos(0, 2)), first);

            var second = new BoosterContext(game.Board, game.Level, game.Rng, reserved);
            effect.Resolve(ActivationRequest.Plane(new GridPos(0, 2)), second);

            Assert.IsNotNull(first.FlyTarget);
            Assert.IsNotNull(second.FlyTarget);
            Assert.AreNotEqual(first.FlyTarget, second.FlyTarget);
        }
    }
}
