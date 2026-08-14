using System;
using System.Collections.Generic;
using Match3.Core;
using Match3.Data;
using Match3.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace Match3.App
{
    /// <summary>
    /// Level map: every level as a node, locked until the one before it is beaten.
    /// A grid rather than a winding path, because a grid stays readable at any board count and
    /// needs no scrolling for a set this size.
    /// </summary>
    public sealed class MapScreen
    {
        public const int Layer = 1;
        private const int Columns = 6;

        private readonly List<UiButton> _nodes = new List<UiButton>();
        private readonly List<Text> _names = new List<Text>();

        private RectTransform _root;
        private RectTransform _grid;
        private Camera _camera;
        private SpriteLibrary _sprites;

        public event Action<int> LevelChosen;

        public bool IsVisible => _root != null && _root.gameObject.activeSelf;

        public void Build(Transform parent, Camera camera, SpriteLibrary sprites)
        {
            _camera = camera;
            _sprites = sprites;

            _root = UiKit.CreateRect("map", parent);
            _root.Fill();

            Image backdrop = UiKit.CreateImage("backdrop", _root, sprites.White(),
                SpriteLibrary.ScreenBackground);
            backdrop.rectTransform.Fill();

            UiKit.CreateText("title", _root, "Match Three", 54, TextAnchor.MiddleCenter,
                    SpriteLibrary.AccentColor, FontStyle.Bold, display: true)
                .rectTransform.Place(UiKit.TopCentre, UiKit.TopCentre,
                    new Vector2(0f, -42f), new Vector2(700f, 70f));

            UiKit.CreateText("subtitle", _root, "Pick a level", 24, TextAnchor.MiddleCenter,
                    SpriteLibrary.TextMuted)
                .rectTransform.Place(UiKit.TopCentre, UiKit.TopCentre,
                    new Vector2(0f, -106f), new Vector2(700f, 40f));

            _grid = UiKit.CreateRect("grid", _root);
            _grid.Place(UiKit.Centre, UiKit.Centre, new Vector2(0f, -30f), new Vector2(1100f, 420f));
        }

        public void SetVisible(bool visible)
        {
            _root.gameObject.SetActive(visible);
            if (visible)
                UiButton.ActiveLayer = Layer;
        }

        public void Refresh(LevelLibrary library, ProgressStore progress)
        {
            EnsureNodes(library.Count);

            const float cellWidth = 168f;
            const float cellHeight = 132f;
            int rows = Mathf.CeilToInt(library.Count / (float)Columns);

            for (int i = 0; i < _nodes.Count; i++)
            {
                bool used = i < library.Count;
                _nodes[i].gameObject.SetActive(used);
                if (!used)
                    continue;

                int column = i % Columns;
                int row = i / Columns;
                int inThisRow = Mathf.Min(Columns, library.Count - row * Columns);

                float x = (column - (inThisRow - 1) * 0.5f) * cellWidth;
                float y = -(row - (rows - 1) * 0.5f) * cellHeight;

                _nodes[i].Rect.Place(UiKit.Centre, UiKit.Centre, new Vector2(x, y), new Vector2(132f, 96f));

                bool unlocked = progress.IsUnlocked(i);
                _nodes[i].Interactable = unlocked;
                // The number always shows: the built-in font has no padlock glyph, and a dimmed
                // number plus the "Locked" caption says the same thing without a missing character.
                _nodes[i].Label.text = (i + 1).ToString("00");
                _nodes[i].SetColor(unlocked ? SpriteLibrary.ButtonAccent : SpriteLibrary.ButtonNeutral);

                _names[i].text = unlocked ? library[i].Name : "Locked";
                _names[i].color = unlocked ? SpriteLibrary.TextColor : SpriteLibrary.TextMuted;
                _names[i].rectTransform.Place(UiKit.Centre, UiKit.Centre,
                    new Vector2(x, y - 58f), new Vector2(cellWidth - 12f, 26f));
            }
        }

        private void EnsureNodes(int count)
        {
            while (_nodes.Count < count)
            {
                int index = _nodes.Count;

                UiButton node = UiButton.Create($"level-{index:00}", _grid, _camera, _sprites.Panel(),
                    SpriteLibrary.ButtonAccent, (index + 1).ToString("00"), 38);
                node.Layer = Layer;
                node.Clicked += () => LevelChosen?.Invoke(index);
                _nodes.Add(node);

                Text name = UiKit.CreateText($"name-{index:00}", _grid, "", 17,
                    TextAnchor.MiddleCenter, SpriteLibrary.TextColor, display: true);
                _names.Add(name);
            }
        }
    }
}
