using System;
using Match3.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace Match3.App
{
    /// <summary>Modal panel for level results. Its own button layer, so clicks cannot leak through.</summary>
    public sealed class PopupView
    {
        public const int Layer = 2;

        private RectTransform _root;
        private Text _title;
        private Text _message;
        private UiButton _primary;
        private UiButton _secondary;

        private Action _onPrimary;
        private Action _onSecondary;

        public bool IsVisible => _root != null && _root.gameObject.activeSelf;

        public void Build(Transform parent, Camera camera, SpriteLibrary sprites)
        {
            _root = UiKit.CreateRect("popup", parent);
            _root.Fill();

            Image scrim = UiKit.CreateImage("scrim", _root, sprites.White(), new Color(0f, 0f, 0f, 0.62f));
            scrim.rectTransform.Fill();

            Image panel = UiKit.CreateImage("panel", _root, sprites.Panel(),
                SpriteLibrary.PanelColor, true);
            panel.rectTransform.Place(UiKit.Centre, UiKit.Centre, Vector2.zero, new Vector2(520f, 340f));

            _title = UiKit.CreateText("title", panel.rectTransform, "", 42,
                TextAnchor.MiddleCenter, SpriteLibrary.AccentColor, FontStyle.Bold, display: true);
            _title.rectTransform.Place(UiKit.TopCentre, UiKit.TopCentre,
                new Vector2(0f, -34f), new Vector2(480f, 56f));

            _message = UiKit.CreateText("message", panel.rectTransform, "", 24,
                TextAnchor.MiddleCenter, SpriteLibrary.TextColor);
            _message.rectTransform.Place(UiKit.TopCentre, UiKit.TopCentre,
                new Vector2(0f, -104f), new Vector2(460f, 80f));

            _primary = UiButton.Create("primary", panel.rectTransform, camera, sprites.Pill(),
                new Color(0.24f, 0.62f, 0.36f, 1f), "", 26);
            _primary.Layer = Layer;
            _primary.Rect.Place(UiKit.BottomCentre, UiKit.BottomCentre,
                new Vector2(0f, 106f), new Vector2(300f, 64f));
            _primary.Clicked += () => _onPrimary?.Invoke();

            _secondary = UiButton.Create("secondary", panel.rectTransform, camera, sprites.Pill(),
                SpriteLibrary.ButtonNeutral, "", 24);
            _secondary.Layer = Layer;
            _secondary.Rect.Place(UiKit.BottomCentre, UiKit.BottomCentre,
                new Vector2(0f, 30f), new Vector2(300f, 58f));
            _secondary.Clicked += () => _onSecondary?.Invoke();

            Hide();
        }

        public void Show(string title, string message, Color titleColor,
            string primaryLabel, Action onPrimary,
            string secondaryLabel, Action onSecondary)
        {
            _title.text = title;
            _title.color = titleColor;
            _message.text = message;

            _primary.Label.text = primaryLabel;
            _secondary.Label.text = secondaryLabel;
            _onPrimary = onPrimary;
            _onSecondary = onSecondary;

            _root.gameObject.SetActive(true);
            UiButton.ActiveLayer = Layer;
        }

        public void Hide()
        {
            _root.gameObject.SetActive(false);
            _onPrimary = null;
            _onSecondary = null;
        }
    }
}
