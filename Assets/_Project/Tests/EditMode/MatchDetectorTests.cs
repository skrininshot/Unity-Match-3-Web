using System.Collections.Generic;
using System.Linq;
using Match3.Core;
using NUnit.Framework;

namespace Match3.Tests
{
    public class MatchDetectorTests
    {
        private MatchDetector _detector;

        [SetUp]
        public void SetUp() => _detector = new MatchDetector();

        [Test]
        public void NoMatch_OnMixedBoard()
        {
            Board board = TestBoard.Parse(@"
                rgby
                gbyr
                byrg
                yrgb");

            Assert.IsEmpty(_detector.FindMatches(board));
            Assert.IsFalse(_detector.HasAnyMatch(board));
        }

        [Test]
        public void HorizontalThree_IsOneGroup_NoBooster()
        {
            Board board = TestBoard.Parse(@"
                gybg
                rrry
                gbgb");

            List<MatchGroup> groups = _detector.FindMatches(board);

            Assert.AreEqual(1, groups.Count);
            Assert.AreEqual(PieceColor.Red, groups[0].Color);
            Assert.AreEqual(3, groups[0].Size);
            Assert.AreEqual(3, groups[0].LongestLine);
            Assert.AreEqual(BoosterType.None, groups[0].AwardedBooster);
        }

        [Test]
        public void VerticalThree_IsDetected()
        {
            Board board = TestBoard.Parse(@"
                gry
                bry
                yrg");

            List<MatchGroup> groups = _detector.FindMatches(board);

            Assert.AreEqual(1, groups.Count);
            Assert.AreEqual(PieceColor.Red, groups[0].Color);
            Assert.AreEqual(3, groups[0].Size);
            Assert.AreEqual(LineOrientation.Vertical, groups[0].Shapes[0].Orientation);
        }

        [Test]
        public void FourInARow_AwardsLine_WithMatchingOrientation()
        {
            Board horizontal = TestBoard.Parse(@"
                gybgy
                rrrry
                gbgbg");

            MatchGroup group = _detector.FindMatches(horizontal).Single();
            Assert.AreEqual(4, group.LongestLine);
            Assert.AreEqual(BoosterType.Line, group.AwardedBooster);
            Assert.AreEqual(LineOrientation.Horizontal, group.AwardedLineOrientation);

            Board vertical = TestBoard.Parse(@"
                gry
                bry
                yrg
                grb");

            group = _detector.FindMatches(vertical).Single();
            Assert.AreEqual(4, group.LongestLine);
            Assert.AreEqual(BoosterType.Line, group.AwardedBooster);
            Assert.AreEqual(LineOrientation.Vertical, group.AwardedLineOrientation);
        }

        [Test]
        public void FiveInARow_AwardsRainbow()
        {
            Board board = TestBoard.Parse(@"
                gybgy
                rrrrr
                gbgbg");

            MatchGroup group = _detector.FindMatches(board).Single();

            Assert.AreEqual(5, group.LongestLine);
            Assert.AreEqual(5, group.Size);
            Assert.AreEqual(BoosterType.Rainbow, group.AwardedBooster);
        }

        [Test]
        public void LShape_MergesIntoOneGroup_AndAwardsBomb()
        {
            // Vertical run of three in the left column crossing a horizontal run of three.
            Board board = TestBoard.Parse(@"
                rgb
                rgb
                rrr");

            List<MatchGroup> groups = _detector.FindMatches(board);

            Assert.AreEqual(1, groups.Count, "L-shape must merge into a single group");
            MatchGroup group = groups[0];
            Assert.AreEqual(5, group.Size, "3 + 3 sharing the corner cell = 5 cells");
            Assert.IsTrue(group.HasCorner);
            Assert.AreEqual(BoosterType.Bomb, group.AwardedBooster);
        }

        [Test]
        public void TShape_AwardsBomb()
        {
            // Horizontal run along the top crossing a vertical run down the middle column.
            Board board = TestBoard.Parse(@"
                rrr
                grg
                grg");

            MatchGroup group = _detector.FindMatches(board).Single();

            Assert.IsTrue(group.HasCorner);
            Assert.AreEqual(5, group.Size, "3 + 3 sharing one cell = 5 cells");
            Assert.AreEqual(BoosterType.Bomb, group.AwardedBooster);
        }

        [Test]
        public void PlusShape_AwardsBomb()
        {
            Board board = TestBoard.Parse(@"
                grg
                rrr
                grg");

            MatchGroup group = _detector.FindMatches(board).Single();

            Assert.IsTrue(group.HasCorner);
            Assert.AreEqual(5, group.Size);
            Assert.AreEqual(BoosterType.Bomb, group.AwardedBooster);
        }

        [Test]
        public void Square2x2_AwardsPlane_AndCountsAsMatch()
        {
            Board board = TestBoard.Parse(@"
                gbgb
                rrgb
                rrbg
                bgbg");

            MatchGroup group = _detector.FindMatches(board).Single();

            Assert.IsTrue(group.HasSquare);
            Assert.AreEqual(4, group.Size);
            Assert.AreEqual(0, group.LongestLine, "a bare 2x2 contains no run of three");
            Assert.AreEqual(BoosterType.Plane, group.AwardedBooster);
            Assert.IsTrue(_detector.HasAnyMatch(board));
        }

        [Test]
        public void RainbowBooster_IsColourless_AndNeverMatches()
        {
            Board board = TestBoard.Parse(@"
                gbg
                rrr
                gbg");

            // Turn the middle piece into a Rainbow: the line must stop being a match.
            Piece middle = board.PieceAt(new GridPos(1, 1));
            middle.Booster = BoosterType.Rainbow;
            middle.Color = PieceColor.None;

            Assert.IsEmpty(_detector.FindMatches(board));
        }

        [Test]
        public void LineBooster_StillMatchesByColour()
        {
            Board board = TestBoard.Parse(@"
                gbg
                rrr
                gbg");

            Piece middle = board.PieceAt(new GridPos(1, 1));
            middle.Booster = BoosterType.Line;

            MatchGroup group = _detector.FindMatches(board).Single();
            Assert.AreEqual(3, group.Size, "a Line booster keeps its colour and can be matched");
        }

        [Test]
        public void Holes_BreakRuns()
        {
            Board board = TestBoard.Parse(@"
                gbg
                r#r
                gbg");

            Assert.IsEmpty(_detector.FindMatches(board));
        }

        [Test]
        public void Obstacles_BreakRuns()
        {
            Board board = TestBoard.Parse(@"
                gbg
                rXr
                gbg");

            Assert.IsEmpty(_detector.FindMatches(board));
        }

        [Test]
        public void WouldSwapMatch_DetectsMatch_AndLeavesBoardUnchanged()
        {
            Board board = TestBoard.Parse(@"
                gbyy
                rgrr
                ybyg");

            string before = TestBoard.Render(board);

            // Swapping (1,1)=g with (1,2)=b creates nothing.
            Assert.IsFalse(_detector.WouldSwapMatch(board, new GridPos(1, 1), new GridPos(1, 2)));

            // Swapping (1,1)=g with (0,1)=r would put r at (1,1) -> r r r across row 1.
            Assert.IsTrue(_detector.WouldSwapMatch(board, new GridPos(1, 1), new GridPos(0, 1)));

            Assert.AreEqual(before, TestBoard.Render(board), "swap probing must not mutate the board");
        }

        [Test]
        public void CreatesMatchAt_IgnoresEmptyAndObstacleCells()
        {
            Board board = TestBoard.Parse(@"
                rrr
                .X#");

            Assert.IsTrue(_detector.CreatesMatchAt(board, new GridPos(1, 1)));
            Assert.IsFalse(_detector.CreatesMatchAt(board, new GridPos(0, 0)));
            Assert.IsFalse(_detector.CreatesMatchAt(board, new GridPos(1, 0)));
            Assert.IsFalse(_detector.CreatesMatchAt(board, new GridPos(2, 0)));
        }

        [Test]
        public void TwoSeparateMatches_AreTwoGroups()
        {
            Board board = TestBoard.Parse(@"
                rrry
                gbgb
                yyyb");

            List<MatchGroup> groups = _detector.FindMatches(board);

            Assert.AreEqual(2, groups.Count);
            // Deterministic order: bottom-most group first.
            Assert.AreEqual(PieceColor.Yellow, groups[0].Color);
            Assert.AreEqual(PieceColor.Red, groups[1].Color);
        }
    }
}
