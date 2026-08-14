using System.Linq;
using Match3.Core;
using NUnit.Framework;

namespace Match3.Tests
{
    /// <summary>
    /// Board elements. The point of these tests is that every combination below is expressed as
    /// data on one <see cref="Obstacle"/> class — there is no subclass per behaviour.
    /// </summary>
    public class ObstacleTests
    {
        // Swapping (2,1) with (2,0) completes r-r-r on row 1, next to whatever sits at (0,0).
        private const string CrateBesideRedMatch = @"
            gyb
            rrg
            {0}yr";

        private static string BoardWith(char crateCode) =>
            CrateBesideRedMatch.Replace("{0}", crateCode.ToString());

        [Test]
        public void PlainCrate_IsDestroyedByAnyAdjacentMatch()
        {
            TestHarness game = TestGame.FromArt(BoardWith('='));

            TurnResult result = game.Swap(2, 1, 2, 0);

            Assert.IsTrue(result.Accepted, result.RejectionReason);
            Assert.AreEqual(1, result.EventsOf<ObstacleDestroyedEvent>().Count());
            Assert.IsNull(game.ObstacleAt(0, 0));
        }

        [Test]
        public void ColouredCrate_SurvivesAMatchOfTheWrongColour()
        {
            // Red crate, blue match.
            TestHarness game = TestGame.FromArt(@"
                gyb
                bbg
                Ryr");

            TurnResult result = game.Swap(2, 1, 2, 2);

            Assert.IsTrue(result.Accepted, result.RejectionReason);
            Assert.IsEmpty(result.EventsOf<ObstacleDestroyedEvent>());
            Assert.IsEmpty(result.EventsOf<ObstacleDamagedEvent>());

            Obstacle crate = game.ObstacleAt(0, 0);
            Assert.IsNotNull(crate);
            Assert.AreEqual(crate.MaxHp, crate.Hp, "a wrong-colour match must not even scratch it");
        }

        [Test]
        public void ColouredCrate_IsDestroyedByAMatchOfItsOwnColour()
        {
            TestHarness game = TestGame.FromArt(BoardWith('R'));

            TurnResult result = game.Swap(2, 1, 2, 0);

            Assert.IsTrue(result.Accepted, result.RejectionReason);
            Assert.AreEqual(1, result.EventsOf<ObstacleDestroyedEvent>().Count());
        }

        [Test]
        public void Blocker_IsNeverDamaged_ByMatchOrByBlast()
        {
            TestHarness game = TestGame.FromArt(BoardWith('X'));

            TurnResult matchResult = game.Swap(2, 1, 2, 0);
            Assert.IsEmpty(matchResult.EventsOf<ObstacleDamagedEvent>());
            Assert.IsEmpty(matchResult.EventsOf<ObstacleDestroyedEvent>());
            Assert.IsNotNull(game.ObstacleAt(0, 0));

            // Now hit it with a bomb, which ignores colour requirements but not indestructibility.
            TestHarness blastGame = TestGame.FromArt(@"
                bgb
                gbg
                Xgb");
            blastGame.MakeBooster(1, 1, BoosterType.Bomb);

            TurnResult blastResult = blastGame.Tap(1, 1);

            Assert.IsEmpty(blastResult.EventsOf<ObstacleDestroyedEvent>());
            Assert.IsNotNull(blastGame.ObstacleAt(0, 0));
        }

        [Test]
        public void ColourChangingCrate_RerollsItsColourEveryTurn()
        {
            // The crate starts red and the match is blue, so it survives the turn and we can observe
            // the reroll. Note the reroll happens at the end of the turn, after damage is applied.
            TestHarness game = TestGame.FromArt(@"
                gyb
                bbg
                *yr");
            Obstacle crate = game.ObstacleAt(0, 0);
            PieceColor before = crate.RequiredColor;
            Assert.AreEqual(PieceColor.Red, before);

            TurnResult result = game.Swap(2, 1, 2, 2);

            Assert.IsTrue(result.Accepted, result.RejectionReason);
            Assert.IsNotNull(game.ObstacleAt(0, 0), "a blue match must not break a red crate");

            var changes = result.EventsOf<ObstacleColorChangedEvent>().ToList();
            Assert.AreEqual(1, changes.Count, "the colour-changing crate reports its new colour");
            Assert.AreNotEqual(before, changes[0].Color, "and it is actually a different colour");
            Assert.AreEqual(crate.RequiredColor, changes[0].Color);
        }

        [Test]
        public void MultipleLives_RequireMultipleHits()
        {
            TestHarness game = TestGame.FromArt(@"
                bgb
                gbg
                .gb", refill: true);
            Obstacle crate = game.PutObstacle(0, 0, ObstacleCatalog.Box, hp: 3);

            Assert.AreEqual(3, crate.Hp);

            game.MakeBooster(1, 1, BoosterType.Bomb);
            TurnResult result = game.Tap(1, 1);

            Assert.AreEqual(1, result.EventsOf<ObstacleDamagedEvent>().Count(),
                "one hit reported, not a destruction");
            Assert.IsEmpty(result.EventsOf<ObstacleDestroyedEvent>());
            Assert.AreEqual(2, crate.Hp);
        }

        [Test]
        public void CrateWithContents_RevealsThemWhenDestroyed()
        {
            TestHarness game = TestGame.FromArt(@"
                gyb
                rrg
                .yr");
            game.PutObstacle(0, 0, ObstacleCatalog.Box,
                contains: EntitySpec.Obstacle(ObstacleCatalog.Blocker));

            TurnResult result = game.Swap(2, 1, 2, 0);

            Assert.IsTrue(result.Accepted, result.RejectionReason);
            Assert.AreEqual(1, result.EventsOf<ObstacleDestroyedEvent>().Count());

            EntitySpawnedEvent revealed = result.EventsOf<EntitySpawnedEvent>()
                .Single(e => !e.FromOutside);
            Assert.AreEqual(ObstacleCatalog.Blocker, revealed.Entity.ObstacleId);
            Assert.IsNotNull(game.ObstacleAt(0, 0));
            Assert.IsTrue(game.ObstacleAt(0, 0).IsIndestructible);
        }

        [Test]
        public void NestedCratesPeelOneLayerPerDestruction()
        {
            // A crate holding a crate holding a piece: three states, not three classes.
            TestHarness game = TestGame.FromArt(@"
                bgb
                gbg
                .gb", refill: true);
            game.PutObstacle(0, 0, ObstacleCatalog.Box,
                contains: EntitySpec.Obstacle(ObstacleCatalog.Box,
                    contains: EntitySpec.ColoredPiece(PieceColor.Red)));

            game.MakeBooster(1, 1, BoosterType.Bomb);
            game.Tap(1, 1);

            Obstacle inner = game.ObstacleAt(0, 0);
            Assert.IsNotNull(inner, "destroying the outer crate revealed the inner one");
            Assert.AreEqual(ObstacleCatalog.Box, inner.Config.Id);

            game.MakeBooster(1, 1, BoosterType.Bomb);
            game.Tap(1, 1);

            Assert.IsNull(game.ObstacleAt(0, 0));
            Piece revealed = game.PieceAt(0, 0);
            Assert.IsNotNull(revealed, "and destroying the inner one revealed the piece inside");
            Assert.AreEqual(PieceColor.Red, revealed.Color);
        }

        [Test]
        public void MultiCellCrate_IsDamagedByAMatchNextToAnyOfItsCells()
        {
            TestHarness game = TestGame.FromArt(@"
                gyrg
                rrgb
                ..yb
                ..gy");
            Obstacle crate = game.PutObstacle(0, 0, ObstacleCatalog.Box, hp: 2, width: 2, height: 2);

            Assert.AreSame(crate, game.ObstacleAt(1, 1));

            // The match on row 2 touches the crate's top-left cell (0,1).
            TurnResult result = game.Swap(2, 2, 2, 3);

            Assert.IsTrue(result.Accepted, result.RejectionReason);
            Assert.AreEqual(1, result.EventsOf<ObstacleDamagedEvent>().Count(),
                "a 2x2 crate takes one hit per match, not one per touched cell");
            Assert.AreEqual(1, crate.Hp);
        }

        [Test]
        public void BoosterBlast_IgnoresTheColourRequirement()
        {
            // A bomb should break a red crate even though the blast has no colour.
            TestHarness game = TestGame.FromArt(@"
                bgb
                gbg
                Rgb");
            game.MakeBooster(1, 1, BoosterType.Bomb);

            TurnResult result = game.Tap(1, 1);

            Assert.AreEqual(1, result.EventsOf<ObstacleDestroyedEvent>().Count(),
                "boosters would be useless against coloured crates otherwise");
        }

        [Test]
        public void ObstaclesAreNotMatchable_AndBreakRuns()
        {
            TestHarness game = TestGame.FromArt(@"
                bgb
                r=r
                bgb");

            Assert.IsFalse(game.Resolver.Detector.HasAnyMatch(game.Board));
        }

        [Test]
        public void AllCatalogEntriesShareOneImplementation()
        {
            ObstacleCatalog catalog = ObstacleCatalog.CreateDefault();

            foreach (ObstacleConfig config in catalog.All)
            {
                var obstacle = new Obstacle(1, config);
                Assert.IsInstanceOf<Obstacle>(obstacle);
                Assert.AreEqual(config.Id, obstacle.Config.Id);
            }

            // Four distinct behaviours, four rules, one entity class.
            Assert.AreEqual(4, catalog.All.Select(c => c.Rule.Id).Distinct().Count());
        }
    }
}
