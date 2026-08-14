using Match3.App;
using Match3.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace Match3.Tests
{
    /// <summary>
    /// UI-construction smoke tests. These live in EditMode (not PlayMode) deliberately: building and
    /// driving uGUI components works fine without a render loop, and EditMode is the tier that
    /// actually runs reliably everywhere PlayMode's GPU requirement doesn't.
    /// <para>
    /// The regression this guards against: PopupView's buttons don't know their label text until
    /// Show() is called, so Build() creates them with "" — and UiButton.Create used to read that as
    /// "no label wanted" and skip creating the Text component entirely, so Show() then wrote into a
    /// null Label and crashed. Reachable only through the App layer, never through Core, so nothing
    /// else in this suite could have caught it.
    /// </para>
    /// </summary>
    public class PopupViewTests
    {
        [Test]
        public void Show_DoesNotThrow_WhenBuiltWithPlaceholderButtonLabels()
        {
            var cameraGo = new GameObject("camera");
            var canvasGo = new GameObject("canvas", typeof(RectTransform));
            var sprites = new SpriteLibrary();

            try
            {
                Camera camera = cameraGo.AddComponent<Camera>();

                var popup = new PopupView();
                popup.Build(canvasGo.transform, camera, sprites);

                Assert.DoesNotThrow(() => popup.Show(
                    "Level complete", "Cleared with moves to spare.", Color.white,
                    "Next level", () => { },
                    "Level map", () => { }));

                Assert.IsTrue(popup.IsVisible);
            }
            finally
            {
                sprites.Dispose();
                Object.DestroyImmediate(cameraGo);
                Object.DestroyImmediate(canvasGo);
            }
        }
    }
}
