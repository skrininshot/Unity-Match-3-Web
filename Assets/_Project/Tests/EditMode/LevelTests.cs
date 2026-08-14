using System.Collections.Generic;
using System.Linq;
using System.Text;
using Match3.Core;
using Match3.Data;
using NUnit.Framework;
using UnityEngine;

namespace Match3.Tests
{
    public class LevelTests
    {
        private const int SeedsPerLevel = 20;

        private static LevelLibrary LoadLibrary()
        {
            LevelLibrary library = LevelResourceLoader.Load();
            Assert.Greater(library.Count, 0, "no level JSON found under Resources/Levels");
            return library;
        }

        [Test]
        public void TheGameShipsAtLeastTenLevels()
        {
            Assert.GreaterOrEqual(LoadLibrary().Count, 10);
        }

        [Test]
        public void EveryShippedLevelIsValid()
        {
            List<string> problems = LoadLibrary().Validate();
            Assert.IsEmpty(problems, string.Join("\n", problems));
        }

        [Test]
        public void LevelsAreOrderedAndUniquelyIdentified()
        {
            LevelLibrary library = LoadLibrary();
            var ids = library.Levels.Select(l => l.Id).ToList();

            CollectionAssert.AllItemsAreUnique(ids);
            CollectionAssert.IsOrdered(ids, System.StringComparer.OrdinalIgnoreCase);
        }

        [Test]
        public void EveryLevelRoundTripsThroughJson()
        {
            foreach (LevelConfig level in LoadLibrary().Levels)
            {
                string json = LevelJson.Write(level);
                LevelConfig reparsed = LevelJson.Parse(json);

                Assert.AreEqual(level.Id, reparsed.Id);
                Assert.AreEqual(level.Width, reparsed.Width);
                Assert.AreEqual(level.Height, reparsed.Height);
                Assert.AreEqual(level.MoveLimit, reparsed.MoveLimit);
                CollectionAssert.AreEqual(level.Palette, reparsed.Palette, level.Id);
                CollectionAssert.AreEqual(level.Layout, reparsed.Layout, level.Id);
                Assert.AreEqual(level.Goals.Count, reparsed.Goals.Count, level.Id);
                Assert.AreEqual(level.Overrides.Count, reparsed.Overrides.Count, level.Id);

                // A second trip must be byte-identical, which catches asymmetric mapping.
                Assert.AreEqual(json, LevelJson.Write(reparsed), level.Id);
            }
        }

        [Test]
        public void NoLevelLayoutStrandsACell()
        {
            // Three non-falling crates in a row would seal the cell beneath the middle one forever.
            // That reads as a broken board rather than a hard one, so no shipped level may do it.
            ObstacleCatalog catalog = ObstacleCatalog.CreateDefault();
            var failures = new List<string>();

            foreach (LevelConfig level in LoadLibrary().Levels)
            {
                for (int seed = 1; seed <= 5; seed++)
                {
                    Board board = BoardBuilder.Build(level, catalog, new Rng(seed));
                    List<GridPos> stranded = BoardReachability.FindStrandedCells(board);

                    if (stranded.Count > 0)
                        failures.Add($"{level.Id} seed {seed}: unreachable cells "
                                     + string.Join(", ", stranded) + "\n" + TestBoard.Render(board));
                }
            }

            Assert.IsEmpty(failures, string.Join("\n", failures));
        }

        [Test]
        public void ValidateCatchesALayoutThatCanNeverBeBuiltWithoutAMatch()
        {
            // Three cells fixed to the same explicit colour in a row: no repair pass can help,
            // since none of them is a free cell BoardBuilder is allowed to recolour. Previously
            // this would pass Validate() untouched and only surface as a silently matched board
            // at load time.
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
            LevelConfig config = TestGame.Config(8, 8, palette: PieceColors.All.Take(3), layout: layout);

            List<string> problems = config.Validate();

            Assert.IsNotEmpty(problems);
            Assert.IsTrue(problems.Any(p => p.Contains("automatic match")), string.Join("\n", problems));
        }

        [Test]
        public void ValidateCatchesALayoutWithNoMovablePiece()
        {
            // A board made entirely of blockers: BoardBuilder happily builds it -- there is
            // nothing to conflict with -- but MoveFinder.HasAny is always false and
            // BoardShuffler gives up below two loose pieces, so the player would be stuck
            // forever with no win, no loss and no message. This is what "dozagruzka urovney v
            // runtime" (LoadFromText) needed a real gate for, not just the shipped-level tests.
            string[] layout = Enumerable.Repeat("XXXXXXXX", 8).ToArray();
            LevelConfig config = TestGame.Config(8, 8, palette: PieceColors.All.Take(3), layout: layout);

            List<string> problems = config.Validate();

            Assert.IsNotEmpty(problems);
            Assert.IsTrue(problems.Any(p => p.Contains("legal move")), string.Join("\n", problems));
        }

        [Test]
        public void EveryLevelBuildsAPlayableBoard()
        {
            ObstacleCatalog catalog = ObstacleCatalog.CreateDefault();
            var detector = new MatchDetector();

            foreach (LevelConfig level in LoadLibrary().Levels)
            for (int seed = 1; seed <= 5; seed++)
            {
                Board board = BoardBuilder.Build(level, catalog, new Rng(seed));

                Assert.IsFalse(detector.HasAnyMatch(board),
                    $"{level.Id} seed {seed} starts with a match:\n{TestBoard.Render(board)}");
                Assert.IsTrue(MoveFinder.HasAny(board, detector),
                    $"{level.Id} seed {seed} starts with no legal move:\n{TestBoard.Render(board)}");
            }
        }

        [Test]
        public void EveryLevelIsWinnableByACompetentPlayer()
        {
            LevelLibrary library = LoadLibrary();
            var report = new StringBuilder("level tuning (greedy player, "
                                           + SeedsPerLevel + " attempts each)\n");
            var failures = new List<string>();

            foreach (LevelConfig level in library.Levels)
            {
                int wins = 0;
                float goalFraction = 0f;
                int turnsOnWin = 0;

                for (int seed = 1; seed <= SeedsPerLevel; seed++)
                {
                    var game = new Match3Game();
                    game.Load(level, seed * 977);

                    PlaythroughResult result = GreedyPlayer.Play(game, new Rng(seed * 31 + 7),
                        level.MoveLimit + 2);

                    if (result.Won)
                    {
                        wins++;
                        turnsOnWin += result.TurnsUsed;
                    }

                    goalFraction += result.GoalFraction;
                }

                float winRate = (float)wins / SeedsPerLevel;
                report.AppendLine($"  {level.Id} '{level.Name}': "
                                  + $"win {wins}/{SeedsPerLevel} ({winRate:P0}), "
                                  + $"avg goal {goalFraction / SeedsPerLevel:P0}, "
                                  + $"avg winning turns {(wins > 0 ? turnsOnWin / (float)wins : 0):0.0}"
                                  + $" of {level.MoveLimit}");

                // The greedy player evaluates every possible swap, so it is stronger than a casual
                // human. Levels are tuned so it spends roughly half the move limit: comfortably
                // winnable, but not over before it started.
                float movesUsedRatio = wins > 0 ? turnsOnWin / (float)wins / level.MoveLimit : 1f;

                if (winRate < 0.55f)
                    failures.Add($"{level.Id} is too hard: only {wins}/{SeedsPerLevel} wins");
                else if (winRate >= 1f && movesUsedRatio < 0.3f)
                    failures.Add($"{level.Id} is trivial: always won using {movesUsedRatio:P0} "
                                 + "of the move limit");
            }

            Debug.Log(report.ToString());
            Assert.IsEmpty(failures, string.Join("\n", failures));
        }

        [Test]
        public void ExtraLevelsCanBeAddedAtRuntime()
        {
            LevelLibrary library = LoadLibrary();
            int before = library.Count;

            LevelConfig added = LevelResourceLoader.LoadFromText(library, @"{
                ""id"": ""zz_side_loaded"",
                ""name"": ""Side loaded"",
                ""width"": 6, ""height"": 6, ""moveLimit"": 15,
                ""palette"": [""r"", ""g"", ""b""],
                ""goals"": [{ ""color"": ""r"", ""count"": 10 }]
            }");

            Assert.AreEqual(before + 1, library.Count);
            Assert.AreSame(added, library.ById("zz_side_loaded"));
        }

        [Test]
        public void AnExistingLevelCanBeReplacedInPlace()
        {
            LevelLibrary library = LoadLibrary();
            int before = library.Count;
            string firstId = library[0].Id;
            int firstIndex = library.IndexOf(firstId);

            LevelConfig replacement = LevelJson.Parse(@"{
                ""id"": """ + firstId + @""",
                ""name"": ""Replaced"",
                ""width"": 6, ""height"": 6, ""moveLimit"": 11,
                ""palette"": [""r"", ""g"", ""b""],
                ""goals"": [{ ""color"": ""r"", ""count"": 9 }]
            }");
            library.AddOrReplace(replacement);

            Assert.AreEqual(before, library.Count, "replacing must not grow the list");
            Assert.AreEqual(firstIndex, library.IndexOf(firstId), "and must keep its position");
            Assert.AreEqual("Replaced", library[firstIndex].Name);
            Assert.AreEqual(11, library[firstIndex].MoveLimit);
        }

        [Test]
        public void LibraryKnowsWhatComesNext()
        {
            LevelLibrary library = LoadLibrary();

            Assert.AreEqual(library[1].Id, library.After(library[0].Id).Id);
            Assert.IsNull(library.After(library[library.Count - 1].Id),
                "the last level has no successor");
        }

        [Test]
        public void InvalidLevelJsonIsRejectedWithAReadableMessage()
        {
            var library = new LevelLibrary();

            var exception = Assert.Throws<Core.Json.JsonException>(() =>
                LevelResourceLoader.LoadFromText(library, @"{
                    ""id"": ""broken"", ""width"": 6, ""height"": 6, ""moveLimit"": 0,
                    ""palette"": [""r""],
                    ""goals"": []
                }"));

            StringAssert.Contains("broken", exception.Message);
            Assert.AreEqual(0, library.Count);
        }

        [Test]
        public void MalformedJsonPointsAtTheProblem()
        {
            var exception = Assert.Throws<Core.Json.JsonException>(() =>
                LevelJson.Parse(@"{ ""id"": ""x"", ""width"": }"));

            Assert.IsNotEmpty(exception.Message);
        }
    }
}
