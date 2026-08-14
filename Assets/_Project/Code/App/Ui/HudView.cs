using System;
using System.Collections.Generic;
using Match3.Core;
using Match3.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace Match3.App
{
    /// <summary>
    /// The in-game bar: which level this is, how many moves are left, and how each goal is going.
    /// </summary>
    public sealed class HudView
    {
        private sealed class GoalChip
        {
            public PieceColor Color;
            public Image Icon;
            public Text Counter;
            public Image Tick;
        }

        private readonly List<GoalChip> _chips = new List<GoalChip>();

        private SpriteLibrary _sprites;
        private RectTransform _root;
        private RectTransform _goalRow;
        private Text _levelLabel;
        private Text _movesLabel;

        public UiButton MapButton { get; private set; }
        public UiButton RestartButton { get; private set; }

        public void Build(Transform parent, Camera camera, SpriteLibrary sprites)
        {
            _sprites = sprites;
            _root = UiKit.CreateRect("hud", parent);
            _root.Fill();

            Image bar = UiKit.CreateImage("bar", _root, sprites.Panel(), SpriteLibrary.PanelColor, true);
            bar.rectTransform.anchorMin = new Vector2(0f, 1f);
            bar.rectTransform.anchorMax = new Vector2(1f, 1f);
            bar.rectTransform.pivot = new Vector2(0.5f, 1f);
            bar.rectTransform.sizeDelta = new Vector2(-24f, 96f);
            bar.rectTransform.anchoredPosition = new Vector2(0f, -12f);

            _levelLabel = UiKit.CreateText("level", bar.rectTransform, "Level", 26,
                TextAnchor.MiddleLeft, SpriteLibrary.TextColor, FontStyle.Bold, display: true);
            _levelLabel.rectTransform.Place(UiKit.TopLeft, UiKit.TopLeft,
                new Vector2(24f, -22f), new Vector2(360f, 52f));

            // Moves counter, on the right where the eye goes for a number.
            Image movesPill = UiKit.CreateImage("moves-pill", bar.rectTransform, sprites.Pill(),
                new Color(0.11f, 0.06f, 0.09f, 0.95f), true);
            movesPill.rectTransform.Place(UiKit.TopRight, UiKit.TopRight,
                new Vector2(-20f, -14f), new Vector2(150f, 68f));

            UiKit.CreateText("moves-caption", movesPill.rectTransform, "MOVES", 15,
                    TextAnchor.UpperCenter, SpriteLibrary.TextMuted)
                .rectTransform.Place(UiKit.TopCentre, UiKit.TopCentre, new Vector2(0f, -6f),
                    new Vector2(140f, 20f));

            _movesLabel = UiKit.CreateText("moves", movesPill.rectTransform, "0", 30,
                TextAnchor.LowerCenter, SpriteLibrary.AccentColor, FontStyle.Bold);
            _movesLabel.rectTransform.Place(UiKit.BottomCentre, UiKit.BottomCentre,
                new Vector2(0f, 4f), new Vector2(140f, 38f));

            _goalRow = UiKit.CreateRect("goals", bar.rectTransform);
            _goalRow.Place(UiKit.TopCentre, UiKit.TopCentre, new Vector2(0f, -14f), new Vector2(520f, 68f));

            MapButton = UiButton.Create("map-button", _root, camera, sprites.Pill(),
                SpriteLibrary.ButtonNeutral, "Map", 22);
            MapButton.Rect.Place(new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(24f, 24f), new Vector2(150f, 56f));

            RestartButton = UiButton.Create("restart-button", _root, camera, sprites.Pill(),
                SpriteLibrary.ButtonNeutral, "Restart", 22);
            RestartButton.Rect.Place(new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-24f, 24f), new Vector2(170f, 56f));
        }

        public void SetVisible(bool visible) => _root.gameObject.SetActive(visible);

        public void SetLevel(int number, LevelConfig config)
        {
            _levelLabel.text = $"Level {number}  ·  {config.Name}";
        }

        public void SetMoves(int moves)
        {
            _movesLabel.text = moves.ToString();
            _movesLabel.color = moves <= 3 ? new Color(1f, 0.45f, 0.4f) : SpriteLibrary.AccentColor;
        }

        public void SetGoals(IReadOnlyList<GoalState> goals)
        {
            EnsureChips(goals.Count);

            float spacing = 128f;
            float offset = (goals.Count - 1) * spacing * 0.5f;

            for (int i = 0; i < _chips.Count; i++)
            {
                GoalChip chip = _chips[i];
                bool used = i < goals.Count;
                chip.Icon.transform.parent.gameObject.SetActive(used);
                if (!used)
                    continue;

                GoalState goal = goals[i];
                chip.Color = goal.Color;
                chip.Icon.sprite = _sprites.Piece(goal.Color);

                var holder = (RectTransform)chip.Icon.transform.parent;
                holder.anchoredPosition = new Vector2(i * spacing - offset, -6f);

                UpdateChip(chip, goal.Collected, goal.Required);
            }
        }

        public void UpdateGoal(PieceColor color, int collected, int required)
        {
            foreach (GoalChip chip in _chips)
                if (chip.Color == color)
                    UpdateChip(chip, collected, required);
        }

        private static void UpdateChip(GoalChip chip, int collected, int required)
        {
            bool done = collected >= required;
            chip.Counter.text = done ? "done" : $"{collected}/{required}";
            chip.Counter.color = done ? new Color(0.45f, 0.92f, 0.55f) : SpriteLibrary.TextColor;
            chip.Tick.enabled = done;
        }

        private void EnsureChips(int count)
        {
            while (_chips.Count < count)
            {
                RectTransform holder = UiKit.CreateRect($"goal-{_chips.Count}", _goalRow);
                holder.Place(UiKit.Centre, UiKit.Centre, Vector2.zero, new Vector2(120f, 64f));

                Image icon = UiKit.CreateImage("icon", holder, _sprites.Piece(PieceColor.Red), Color.white);
                icon.rectTransform.Place(new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                    new Vector2(4f, 0f), new Vector2(52f, 52f));

                Image tick = UiKit.CreateImage("tick", holder, _sprites.Glow(),
                    new Color(0.45f, 0.92f, 0.55f, 0.55f));
                tick.rectTransform.Place(new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                    new Vector2(4f, 0f), new Vector2(58f, 58f));
                tick.enabled = false;

                Text counter = UiKit.CreateText("count", holder, "0/0", 24,
                    TextAnchor.MiddleLeft, SpriteLibrary.TextColor, FontStyle.Bold);
                counter.rectTransform.Place(new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                    new Vector2(62f, 0f), new Vector2(70f, 40f));

                _chips.Add(new GoalChip { Icon = icon, Counter = counter, Tick = tick });
            }
        }
    }
}
