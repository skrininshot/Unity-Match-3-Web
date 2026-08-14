using Match3.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace Match3.App
{
    /// <summary>
    /// Builds uGUI elements from code.
    /// The whole interface is constructed at runtime, so the scene stays a single bootstrap object
    /// and the layout lives in reviewable code rather than in a serialized hierarchy.
    /// </summary>
    public static class UiKit
    {
        public static readonly Vector2 ReferenceResolution = new Vector2(1280f, 720f);

        private static Font _font;
        private static Font _display;
        private static Font _body;
        private static Font _bodyBold;

        /// <summary>
        /// The built-in font, used only as a fallback if a custom one below failed to load. Text is
        /// deliberately built on legacy UI Text rather than TextMeshPro: TMP needs its essential
        /// resources imported into the project, and a fresh project does not have them, which would
        /// break the build with no way to fix it from the command line.
        /// </summary>
        public static Font DefaultFont
        {
            get
            {
                if (_font != null)
                    return _font;

                _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                        ?? Resources.GetBuiltinResource<Font>("Arial.ttf");

                if (_font == null)
                    Debug.LogError("No built-in font available; UI text will not render.");

                return _font;
            }
        }

        /// <summary>Rounded and bold (Fredoka) -- titles, level names, every button label.</summary>
        public static Font DisplayFont => _display ??= Load("Fonts/Fredoka-Bold");

        /// <summary>Softer and plainer (Nunito) -- counters, captions, body copy.</summary>
        public static Font BodyFont => _body ??= Load("Fonts/Nunito-Regular");

        public static Font BodyFontBold => _bodyBold ??= Load("Fonts/Nunito-Bold");

        private static Font Load(string resourcePath) => Resources.Load<Font>(resourcePath) ?? DefaultFont;

        public static Canvas CreateCanvas(string name, Transform parent, Camera camera, int sortingOrder)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1f;
            canvas.sortingOrder = sortingOrder;

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            return canvas;
        }

        public static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static Image CreateImage(string name, Transform parent, Sprite sprite, Color color,
            bool sliced = false)
        {
            RectTransform rect = CreateRect(name, parent);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>
        /// <paramref name="display"/> picks Fredoka (titles, names, anything meant to feel loud) over
        /// Nunito (counters, captions, body copy). <paramref name="style"/> still selects bold within
        /// whichever family that resolves to -- Bold means the dedicated bold weight file, not a
        /// synthetic bold applied on top of it, which is what legacy Text would otherwise do to an
        /// already-heavy display face.
        /// </summary>
        public static Text CreateText(string name, Transform parent, string content, int fontSize,
            TextAnchor anchor, Color color, FontStyle style = FontStyle.Normal, bool display = false)
        {
            RectTransform rect = CreateRect(name, parent);
            var text = rect.gameObject.AddComponent<Text>();
            text.font = display ? DisplayFont : (style == FontStyle.Bold ? BodyFontBold : BodyFont);
            text.text = content;
            text.fontSize = fontSize;
            text.alignment = anchor;
            text.color = color;
            text.fontStyle = FontStyle.Normal;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        /// <summary>Anchors a rect to a point and gives it a fixed size.</summary>
        public static RectTransform Place(this RectTransform rect, Vector2 anchor, Vector2 pivot,
            Vector2 position, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        /// <summary>Stretches a rect to fill its parent.</summary>
        public static RectTransform Fill(this RectTransform rect, float padding = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(padding, padding);
            rect.offsetMax = new Vector2(-padding, -padding);
            return rect;
        }

        public static readonly Vector2 TopCentre = new Vector2(0.5f, 1f);
        public static readonly Vector2 TopLeft = new Vector2(0f, 1f);
        public static readonly Vector2 TopRight = new Vector2(1f, 1f);
        public static readonly Vector2 Centre = new Vector2(0.5f, 0.5f);
        public static readonly Vector2 BottomCentre = new Vector2(0.5f, 0f);
    }
}
