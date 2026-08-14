using System.Collections;
using System.Collections.Generic;
using System.IO;
using Match3.App;
using Match3.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Match3.Tests.PlayMode
{
    /// <summary>
    /// Drives the real game and writes PNGs of each screen.
    /// <para>
    /// This is the only way to actually look at the result without a human opening the editor, and
    /// it doubles as a smoke test: if the game cannot boot, load a level, play a turn and finish it,
    /// these fail.
    /// </para>
    /// </summary>
    public class ScreenshotTests
    {
        private GameBootstrap _game;

        private static string OutputDirectory
        {
            get
            {
                string root = Directory.GetParent(Application.dataPath).FullName;
                string path = Path.Combine(root, "Artifacts", "screenshots");
                Directory.CreateDirectory(path);
                return path;
            }
        }

        [SetUp]
        public void SetUp()
        {
            var go = new GameObject("Game");
            _game = go.AddComponent<GameBootstrap>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_game != null)
                Object.Destroy(_game.gameObject);
        }

        [UnityTest]
        public IEnumerator CaptureEveryScreen()
        {
            yield return null;
            Assert.Greater(_game.Levels.Count, 0, "no levels were loaded");

            // 1. Level map.
            yield return Capture("01-map.png");

            // 2. A freshly built board.
            _game.StartLevel(0);
            yield return null;
            yield return Capture("02-level-01.png");

            // 3. Mid-cascade: caught while pieces are being removed.
            yield return PlayAnyMove();
            yield return new WaitForSeconds(0.22f);
            yield return Capture("03-clearing.png");
            yield return WaitUntilIdle();

            // 4. A later level, for holes, crates, blockers and colour-locked crates.
            _game.StartLevel(_game.Levels.Count - 1);
            yield return null;
            yield return Capture("04-board-elements.png");

            // 5. A booster combination going off.
            yield return SetUpBoosterCombo();
            yield return new WaitForSeconds(0.18f);
            yield return Capture("05-booster-combo.png");
            yield return WaitUntilIdle();

            // 6. Defeat popup.
            _game.StartLevel(0);
            yield return null;
            _game.Game.Level.MovesLeft = 1;
            yield return PlayAnyMove();
            yield return WaitUntilIdle();
            yield return new WaitForSeconds(0.1f);
            yield return Capture("06-defeat.png");
            Assert.AreEqual(LevelOutcome.Lost, _game.Game.Level.Outcome);

            // 7. Victory popup.
            _game.StartLevel(0);
            yield return null;
            yield return ForceVictory();
            yield return WaitUntilIdle();
            yield return new WaitForSeconds(0.1f);
            yield return Capture("07-victory.png");
            Assert.AreEqual(LevelOutcome.Won, _game.Game.Level.Outcome);
        }

        // ------------------------------------------------------------------ helpers

        private IEnumerator Capture(string fileName)
        {
            Camera camera = _game.Camera;
            Assert.IsNotNull(camera, "the game did not create a camera");

            int width = Mathf.Max(640, Screen.width);
            int height = Mathf.Max(360, Screen.height);

            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            camera.targetTexture = target;

            // Let the camera render into the texture during a normal frame; URP does not support
            // calling Camera.Render() directly.
            yield return null;
            yield return new WaitForEndOfFrame();

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;

            var image = new Texture2D(width, height, TextureFormat.RGBA32, false);
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            image.Apply();

            RenderTexture.active = previous;
            camera.targetTexture = null;

            string path = Path.Combine(OutputDirectory, fileName);
            File.WriteAllBytes(path, image.EncodeToPNG());
            Debug.Log($"[TOOL] screenshot {path}");

            Object.Destroy(image);
            target.Release();
            Object.Destroy(target);
        }

        private IEnumerator WaitUntilIdle()
        {
            float deadline = Time.realtimeSinceStartup + 20f;
            while (_game.IsBusy && Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.IsFalse(_game.IsBusy, "a turn never finished animating");
        }

        private IEnumerator PlayAnyMove()
        {
            List<BoardMove> moves = _game.Game.AvailableMoves();
            Assert.IsNotEmpty(moves, "the board offered no legal move");

            BoardMove move = moves[moves.Count / 2];
            _game.TrySwap(move.A, move.B);
            yield return null;
        }

        /// <summary>Puts a Line and a Bomb next to each other and swaps them.</summary>
        private IEnumerator SetUpBoosterCombo()
        {
            Board board = _game.Game.Board;

            GridPos? found = null;
            foreach (GridPos pos in board.Positions)
            {
                var right = new GridPos(pos.X + 1, pos.Y);
                if (board.PieceAt(pos) != null && board.PieceAt(right) != null)
                {
                    found = pos;
                    break;
                }
            }

            Assert.IsTrue(found.HasValue, "no adjacent pair of pieces to promote");

            GridPos a = found.Value;
            var b = new GridPos(a.X + 1, a.Y);

            Piece first = board.PieceAt(a);
            Piece second = board.PieceAt(b);
            first.Booster = BoosterType.Line;
            first.Orientation = LineOrientation.Horizontal;
            second.Booster = BoosterType.Bomb;

            // Rebuild so the view picks up the promotion.
            _game.BoardView.Build(board, _game.Game.SnapshotEntities());
            yield return null;

            _game.TrySwap(a, b);
            yield return null;
        }

        /// <summary>Fills the goal to one short, then clears the rest with a Rainbow.</summary>
        private IEnumerator ForceVictory()
        {
            LevelRuntime level = _game.Game.Level;
            GoalState goal = level.Goals.Goals[0];

            for (int i = goal.Collected; i < goal.Required - 1; i++)
                level.Goals.Register(goal.Color);

            _game.SetGoalDisplay();

            Board board = _game.Game.Board;

            // Force a Rainbow next to a piece of the goal colour, which always clears at least one.
            GridPos? spot = null;
            foreach (GridPos pos in board.Positions)
            {
                var right = new GridPos(pos.X + 1, pos.Y);
                if (board.PieceAt(pos) != null && board.PieceAt(right) != null)
                {
                    spot = pos;
                    break;
                }
            }

            Assert.IsTrue(spot.HasValue);
            GridPos a = spot.Value;
            var b = new GridPos(a.X + 1, a.Y);

            Piece rainbow = board.PieceAt(a);
            rainbow.Booster = BoosterType.Rainbow;
            rainbow.Color = PieceColor.None;
            board.PieceAt(b).Color = goal.Color;

            _game.BoardView.Build(board, _game.Game.SnapshotEntities());
            yield return null;

            _game.TrySwap(a, b);
            yield return null;
        }
    }
}
