using System;
using Match3.Presentation;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Match3.App
{
    /// <summary>
    /// A button that hit-tests itself against the pointer.
    /// <para>
    /// Deliberately not uGUI's Button + EventSystem: the input handling in this project is set to
    /// the Input System package only, which means the classic input module does not work and the
    /// EventSystem needs its own setup. One rect test is less machinery and less to go wrong.
    /// </para>
    /// <para>
    /// <see cref="ActiveLayer"/> is what stops a popup's buttons and the screen behind it from both
    /// reacting to the same click.
    /// </para>
    /// </summary>
    public sealed class UiButton : MonoBehaviour
    {
        /// <summary>Only buttons on this layer respond to the pointer.</summary>
        public static int ActiveLayer;

        private RectTransform _rect;
        private Image _background;
        private Camera _camera;
        private Color _normalColor;
        private bool _pressedInside;

        public int Layer { get; set; }
        public bool Interactable { get; set; } = true;
        public event Action Clicked;

        public Text Label { get; private set; }
        public RectTransform Rect => _rect;

        public static UiButton Create(string name, Transform parent, Camera camera, Sprite background,
            Color color, string label, int fontSize)
        {
            RectTransform rect = UiKit.CreateRect(name, parent);

            var button = rect.gameObject.AddComponent<UiButton>();
            button._rect = rect;
            button._camera = camera;
            button._normalColor = color;

            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = background;
            image.type = Image.Type.Sliced;
            image.color = color;
            image.raycastTarget = false;
            button._background = image;

            // null means "no label, ever" (there's no such caller today, but the distinction matters);
            // "" means "a label that isn't known yet" -- PopupView creates its buttons with "" and
            // fills in the real text later from Show(), so it still needs a Text component to write
            // into. Skipping creation for "" left PopupView.Show writing into a null Label and
            // crashing the WebGL runtime the instant a level finished.
            if (label != null)
            {
                button.Label = UiKit.CreateText("label", rect, label, fontSize, TextAnchor.MiddleCenter,
                    SpriteLibrary.TextColor, FontStyle.Bold, display: true);
                button.Label.rectTransform.Fill();
            }

            return button;
        }

        public void SetColor(Color color)
        {
            _normalColor = color;
            if (_background != null && !_pressedInside)
                _background.color = color;
        }

        private void Update()
        {
            if (_background != null)
                _background.color = Interactable
                    ? (_pressedInside ? Darken(_normalColor) : _normalColor)
                    : Fade(_normalColor);

            if (!Interactable || Layer != ActiveLayer)
            {
                _pressedInside = false;
                return;
            }

            Pointer pointer = Pointer.current;
            if (pointer == null)
                return;

            Vector2 screen = pointer.position.ReadValue();

            if (pointer.press.wasPressedThisFrame)
                _pressedInside = Contains(screen);
            else if (pointer.press.wasReleasedThisFrame)
            {
                bool click = _pressedInside && Contains(screen);
                _pressedInside = false;
                if (click)
                    Clicked?.Invoke();
            }
        }

        /// <summary>True if the pointer is over any button on the active layer — used to keep board taps out of the UI.</summary>
        public bool Contains(Vector2 screenPoint) =>
            RectTransformUtility.RectangleContainsScreenPoint(_rect, screenPoint, _camera);

        private static Color Darken(Color color) =>
            new Color(color.r * 0.78f, color.g * 0.78f, color.b * 0.78f, color.a);

        private static Color Fade(Color color) =>
            new Color(color.r * 0.55f, color.g * 0.55f, color.b * 0.55f, color.a * 0.7f);
    }
}
