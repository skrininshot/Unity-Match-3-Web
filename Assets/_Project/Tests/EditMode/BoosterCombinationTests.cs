using System.Collections.Generic;
using System.Linq;
using Match3.Core;
using NUnit.Framework;

namespace Match3.Tests
{
    public class BoosterCombinationTests
    {
        private const string Checkerboard = @"
            bgbgb
            gbgbg
            bgbgb
            gbgbg
            bgbgb";

        private static readonly BoosterType[] AllBoosters =
        {
            BoosterType.Line, BoosterType.Bomb, BoosterType.Rainbow, BoosterType.Plane,
        };

        [Test]
        public void EveryPairOfBoostersHasACombination()
        {
            var registry = BoosterCombinationRegistry.CreateDefault();

            var missing = new List<string>();
            for (int i = 0; i < AllBoosters.Length; i++)
            for (int j = i; j < AllBoosters.Length; j++)
                if (!registry.IsRegistered(AllBoosters[i], AllBoosters[j]))
                    missing.Add($"{AllBoosters[i]}+{AllBoosters[j]}");

            Assert.IsEmpty(missing, "unhandled booster pairings: " + string.Join(", ", missing));
        }

        [Test]
        public void CombinationLookupIsOrderIndependent()
        {
            var registry = BoosterCombinationRegistry.CreateDefault();

            foreach (BoosterType a in AllBoosters)
            foreach (BoosterType b in AllBoosters)
                Assert.AreEqual(registry.IsRegistered(a, b), registry.IsRegistered(b, a),
                    $"{a}+{b} must resolve the same way in either order");
        }

        private List<ActivationRequest> Resolve(TestHarness game, BoosterType a, BoosterType b,
            PieceColor colorA = PieceColor.Red, PieceColor colorB = PieceColor.Red)
        {
            Piece pieceA = game.MakeBooster(1, 1, a, color: colorA);
            Piece pieceB = game.MakeBooster(2, 1, b, color: colorB);

            var output = new List<ActivationRequest>();
            bool resolved = game.Combinations.TryResolve(pieceA, pieceB, new GridPos(2, 1),
                game.Board, game.Level, game.Rng, output);

            Assert.IsTrue(resolved, $"{a}+{b} should have produced activations");
            return output;
        }

        [Test]
        public void LinePlusLine_IsAFullCross()
        {
            TestHarness game = TestGame.FromArt(Checkerboard);

            List<ActivationRequest> requests = Resolve(game, BoosterType.Line, BoosterType.Line);

            Assert.AreEqual(2, requests.Count);
            CollectionAssert.AreEquivalent(
                new[] { LineOrientation.Horizontal, LineOrientation.Vertical },
                requests.Select(r => r.Orientation).ToArray());
            Assert.IsTrue(requests.All(r => r.Type == BoosterType.Line && r.At == new GridPos(2, 1)));
            Assert.IsTrue(requests.All(r => r.Thickness == 0), "a plain cross is one cell thick");
        }

        [Test]
        public void LinePlusBomb_IsAThickCross()
        {
            TestHarness game = TestGame.FromArt(Checkerboard);

            List<ActivationRequest> requests = Resolve(game, BoosterType.Line, BoosterType.Bomb);

            Assert.AreEqual(2, requests.Count);
            Assert.IsTrue(requests.All(r => r.Type == BoosterType.Line && r.Thickness == 3));
            CollectionAssert.AreEquivalent(
                new[] { LineOrientation.Horizontal, LineOrientation.Vertical },
                requests.Select(r => r.Orientation).ToArray());
        }

        [Test]
        public void BombPlusBomb_IsOneLargerBlast()
        {
            TestHarness game = TestGame.FromArt(Checkerboard);

            ActivationRequest request = Resolve(game, BoosterType.Bomb, BoosterType.Bomb).Single();

            Assert.AreEqual(BoosterType.Bomb, request.Type);
            Assert.AreEqual(4, request.Radius);
        }

        [Test]
        public void RainbowPlusRainbow_ClearsEverything()
        {
            TestHarness game = TestGame.FromArt(Checkerboard);

            ActivationRequest request = Resolve(game, BoosterType.Rainbow, BoosterType.Rainbow,
                PieceColor.None, PieceColor.None).Single();

            Assert.AreEqual(BoosterType.Rainbow, request.Type);
            Assert.IsTrue(request.EntireBoard);
        }

        [Test]
        public void LinePlusRainbow_TurnsEveryPieceOfTheColourIntoALine()
        {
            TestHarness game = TestGame.FromArt(Checkerboard);
            // The Line partner is blue, so blue is the colour that erupts.
            List<ActivationRequest> requests = Resolve(game, BoosterType.Line, BoosterType.Rainbow,
                PieceColor.Blue, PieceColor.None);

            int blueCount = game.Board.Positions
                .Count(p => game.Board.PieceAt(p)?.Color == PieceColor.Blue);

            Assert.AreEqual(blueCount, requests.Count);
            Assert.IsTrue(requests.All(r => r.Type == BoosterType.Line));
            Assert.Greater(requests.Select(r => r.Orientation).Distinct().Count(), 1,
                "orientations alternate so the result reads as a burst");
        }

        [Test]
        public void BombPlusRainbow_TurnsEveryPieceOfTheColourIntoASmallBomb()
        {
            TestHarness game = TestGame.FromArt(Checkerboard);

            List<ActivationRequest> requests = Resolve(game, BoosterType.Bomb, BoosterType.Rainbow,
                PieceColor.Blue, PieceColor.None);

            int blueCount = game.Board.Positions
                .Count(p => game.Board.PieceAt(p)?.Color == PieceColor.Blue);

            Assert.AreEqual(blueCount, requests.Count);
            Assert.IsTrue(requests.All(r => r.Type == BoosterType.Bomb && r.Radius == 1),
                "deliberately smaller than a lone bomb, or this combination would always clear the board");
        }

        [Test]
        public void PlaneCarriesEachOtherBoosterToItsTarget()
        {
            foreach (BoosterType payload in new[] { BoosterType.Line, BoosterType.Bomb, BoosterType.Rainbow })
            {
                TestHarness game = TestGame.FromArt(Checkerboard);

                ActivationRequest request = Resolve(game, payload, BoosterType.Plane,
                    payload == BoosterType.Rainbow ? PieceColor.None : PieceColor.Red).Single();

                Assert.AreEqual(BoosterType.Plane, request.Type, $"{payload}+Plane");
                Assert.AreEqual(payload, request.Payload, $"{payload}+Plane payload");
            }
        }

        [Test]
        public void PlanePlusPlane_SendsTwoPlanes()
        {
            TestHarness game = TestGame.FromArt(Checkerboard);

            List<ActivationRequest> requests = Resolve(game, BoosterType.Plane, BoosterType.Plane);

            Assert.AreEqual(2, requests.Count);
            Assert.IsTrue(requests.All(r => r.Type == BoosterType.Plane));
        }
    }
}
