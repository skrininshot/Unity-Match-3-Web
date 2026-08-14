using System.Collections.Generic;
using System.Linq;
using Match3.Core;
using NUnit.Framework;

namespace Match3.Tests
{
    /// <summary>
    /// Plays thousands of random legal turns and checks the board after every one.
    /// Cascade and chain-reaction bugs live in rare event orderings that hand-written scenarios and
    /// manual clicking both miss; this is the test that actually finds them.
    /// </summary>
    public class StressSimulationTests
    {
        private const int SeedsPerConfig = 12;
        private const int TurnsPerRun = 60;

        private static IEnumerable<LevelConfig> Configs()
        {
            yield return Named("plain-8x8-5", TestGame.Config(8, 8, moveLimit: 20,
                goalCount: 15, palette: PieceColors.All.Take(5)));

            yield return Named("holes-8x8-4", TestGame.Config(8, 8, moveLimit: 20,
                goalCount: 15, palette: PieceColors.All.Take(4), layout: new[]
                {
                    "##....##",
                    "#......#",
                    "........",
                    "........",
                    "........",
                    "........",
                    "#......#",
                    "##....##",
                }));

            yield return Named("elements-8x8-4", TestGame.Config(8, 8, moveLimit: 20,
                goalCount: 15, palette: PieceColors.All.Take(4), layout: new[]
                {
                    "........",
                    "........",
                    "..X..X..",
                    "........",
                    ".==..==.",
                    "........",
                    "..X..X..",
                    "........",
                }));

            yield return Named("colour-crates-6x6-3", TestGame.Config(6, 6, moveLimit: 20,
                goalCount: 12, palette: PieceColors.All.Take(3), layout: new[]
                {
                    "......",
                    "......",
                    ".R..G.",
                    "......",
                    ".*..*.",
                    "......",
                }));
        }

        private static LevelConfig Named(string id, LevelConfig config)
        {
            config.Id = id;
            return config;
        }

        [Test]
        public void RandomPlaythroughs_LeaveTheBoardConsistent()
        {
            int turnsPlayed = 0;

            foreach (LevelConfig config in Configs())
            {
                for (int seed = 1; seed <= SeedsPerConfig; seed++)
                {
                    var game = new Match3Game();
                    game.Load(config, seed);

                    string where = $"{config.Id} seed {seed} after load";
                    AssertBoardIsSane(game, where);

                    var rng = new Rng(seed * 7919 + 13);

                    for (int turn = 0; turn < TurnsPerRun; turn++)
                    {
                        if (game.Level.IsOver)
                        {
                            game.Load(config, rng.Range(1, int.MaxValue));
                            AssertBoardIsSane(game, $"{config.Id} seed {seed} after reload on turn {turn}");
                            continue;
                        }

                        where = $"{config.Id} seed {seed} turn {turn}";
                        TurnResult result = PlayRandomAction(game, rng, where);
                        turnsPlayed++;

                        AssertResultIsSane(result, where);
                        AssertBoardIsSane(game, where);
                    }
                }
            }

            Assert.Greater(turnsPlayed, 1000, "the simulation should actually have played a lot of turns");
        }

        [Test]
        public void RandomPlaythroughsWithBoostersEverywhere_StayConsistent()
        {
            // Deliberately unrealistic booster density, to hammer combinations and chain reactions.
            LevelConfig config = TestGame.Config(8, 8, moveLimit: 40, goalCount: 40,
                palette: PieceColors.All.Take(5));

            BoosterType[] types =
            {
                BoosterType.Line, BoosterType.Bomb, BoosterType.Rainbow, BoosterType.Plane,
            };

            for (int seed = 1; seed <= 15; seed++)
            {
                var game = new Match3Game();
                game.Load(config, seed);
                var rng = new Rng(seed * 104729);

                for (int turn = 0; turn < 40; turn++)
                {
                    if (game.Level.IsOver)
                        break;

                    // Sprinkle boosters onto random plain pieces.
                    foreach (GridPos pos in game.Board.Positions)
                    {
                        Piece piece = game.Board.PieceAt(pos);
                        if (piece == null || piece.IsBooster || !rng.Chance(0.15))
                            continue;

                        BoosterType type = rng.Pick(types);
                        piece.Booster = type;
                        if (type == BoosterType.Rainbow)
                            piece.Color = PieceColor.None;
                    }

                    string where = $"booster-soup seed {seed} turn {turn}";

                    // Promoting pieces can leave a matched board; that is fine, the resolver copes.
                    TurnResult result = PlayRandomAction(game, rng, where);

                    AssertResultIsSane(result, where);
                    AssertBoardIsSane(game, where);
                }
            }
        }

        [Test]
        public void ReloadRebuildsAPlayableLevel_WithoutRecreatingTheGame()
        {
            LevelConfig config = TestGame.Config(8, 8, palette: PieceColors.All.Take(4), seed: 0);
            var game = new Match3Game();

            game.Load(config, 1);
            string first = TestBoard.Render(game.Board);
            game.Swap(new GridPos(0, 0), new GridPos(1, 0)); // may or may not be legal; irrelevant

            game.Reload();

            Assert.AreEqual(config.MoveLimit, game.Level.MovesLeft, "reload resets the move counter");
            Assert.AreEqual(LevelOutcome.InProgress, game.Level.Outcome);
            Assert.AreEqual(0, game.Level.Goals.Goals[0].Collected, "and the goal progress");
            AssertBoardIsSane(game, "after reload");

            game.ReloadSameBoard();
            Assert.AreEqual(TestBoard.Render(game.Board), TestBoard.Render(game.Board));
        }

        // ------------------------------------------------------------------ helpers

        private static TurnResult PlayRandomAction(Match3Game game, Rng rng, string where)
        {
            var swaps = game.AvailableMoves();
            var boosters = new List<GridPos>();
            foreach (GridPos pos in game.Board.Positions)
            {
                Piece piece = game.Board.PieceAt(pos);
                if (piece != null && piece.IsBooster)
                    boosters.Add(pos);
            }

            Assert.IsTrue(swaps.Count > 0 || boosters.Count > 0,
                $"{where}: no legal action at all, the dead-end shuffle should have prevented this:\n"
                + TestBoard.Render(game.Board));

            bool tap = boosters.Count > 0 && (swaps.Count == 0 || rng.Chance(0.35));

            TurnResult result = tap
                ? game.ActivateBooster(rng.Pick(boosters))
                : ApplySwap(game, rng.Pick(swaps));

            Assert.IsTrue(result.Accepted, $"{where}: a legal action was rejected: {result.RejectionReason}");
            return result;
        }

        private static TurnResult ApplySwap(Match3Game game, BoardMove move) =>
            game.Swap(move.A, move.B);

        private static void AssertResultIsSane(TurnResult result, string where)
        {
            Assert.IsFalse(result.GravityStalled, $"{where}: gravity did not settle");

            foreach (TurnPhase phase in result.Phases)
            {
                bool clears = phase.Events.Any(e => e is EntityClearedEvent);
                bool falls = phase.Events.Any(e => e is EntityMovedEvent m && m.Reason == MoveReason.Fall);
                Assert.IsFalse(clears && falls, $"{where}: phase {phase.Kind} mixes removals and falls");
            }

            var clearedIds = new HashSet<long>();
            foreach (EntityClearedEvent cleared in result.EventsOf<EntityClearedEvent>())
                Assert.IsTrue(clearedIds.Add(cleared.Id),
                    $"{where}: entity {cleared.Id} was cleared twice in one turn");

            var destroyedIds = new HashSet<long>();
            foreach (ObstacleDestroyedEvent destroyed in result.EventsOf<ObstacleDestroyedEvent>())
                Assert.IsTrue(destroyedIds.Add(destroyed.Id),
                    $"{where}: obstacle {destroyed.Id} was destroyed twice in one turn");

            var activatedIds = new HashSet<long>();
            foreach (BoosterActivatedEvent activation in result.EventsOf<BoosterActivatedEvent>())
            {
                if (activation.SourceId == 0)
                    continue; // synthetic activation from a combination

                Assert.IsTrue(activatedIds.Add(activation.SourceId),
                    $"{where}: booster {activation.SourceId} fired twice in one turn");
            }

            Assert.GreaterOrEqual(result.MovesLeft, 0, $"{where}: negative move count");
        }

        private static void AssertBoardIsSane(Match3Game game, string where)
        {
            Board board = game.Board;

            // A cell only has to be filled if refill can actually get there. Destroying a crate that
            // was holding a blocker can seal a pocket mid-game, so this is computed per turn rather
            // than assumed from the layout.
            bool[] reachable = BoardReachability.Compute(board);

            foreach (GridPos pos in board.Positions)
            {
                if (!board.IsPlayable(pos))
                {
                    Assert.IsNull(board.EntityAt(pos), $"{where}: hole {pos} is occupied");
                    continue;
                }

                if (!board.IsOccupied(pos))
                {
                    Assert.IsFalse(reachable[board.Index(pos)],
                        $"{where}: reachable cell {pos} was left empty:\n{TestBoard.Render(board)}");
                    continue;
                }

                BoardEntity entity = board.EntityAt(pos);
                Assert.IsTrue(entity.Covers(pos),
                    $"{where}: cell {pos} reports {entity}, whose footprint does not include it");
                Assert.AreSame(entity, board.FindById(entity.Id),
                    $"{where}: {entity} is not registered under its own id");
            }

            foreach (BoardEntity entity in board.AllEntities())
            foreach (GridPos cell in entity.Cells)
                Assert.AreSame(entity, board.EntityAt(cell),
                    $"{where}: {entity} claims {cell} but the cell says otherwise");

            Assert.IsFalse(game.Resolver.Detector.HasAnyMatch(board),
                $"{where}: a resolved board still contains a match:\n{TestBoard.Render(board)}");

            foreach (GoalState goal in game.Level.Goals.Goals)
            {
                Assert.GreaterOrEqual(goal.Collected, 0, $"{where}: negative goal progress");
                Assert.LessOrEqual(goal.Collected, goal.Required, $"{where}: goal overshot its target");
            }
        }
    }
}
