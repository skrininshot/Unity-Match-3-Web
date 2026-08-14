using System.Collections.Generic;
using System.IO;
using Match3.Core;
using Match3.Presentation;
using UnityEngine;

namespace Match3.EditorTools
{
    /// <summary>
    /// Renders every generated sprite into one contact sheet PNG.
    /// The art is produced by code, so this is the only way to actually look at it without opening
    /// the editor — and looking at it is the point.
    /// </summary>
    public static class ArtPreviewTool
    {
        private const int Cell = 128;
        private const int Padding = 8;
        private const int Columns = 8;

        public static void Capture()
        {
            var library = new SpriteLibrary();
            var entries = new List<Sprite>();

            foreach (PieceColor color in PieceColors.All)
                entries.Add(library.Piece(color));

            entries.Add(library.Rainbow());
            entries.Add(library.BoosterOverlay(BoosterType.Line, LineOrientation.Horizontal));
            entries.Add(library.BoosterOverlay(BoosterType.Line, LineOrientation.Vertical));
            entries.Add(library.BoosterOverlay(BoosterType.Bomb, LineOrientation.Horizontal));
            entries.Add(library.BoosterOverlay(BoosterType.Plane, LineOrientation.Horizontal));

            entries.Add(library.Crate(ObstacleCatalog.Box, PieceColor.None));
            entries.Add(library.Crate(ObstacleCatalog.Blocker, PieceColor.None));
            entries.Add(library.Crate(ObstacleCatalog.ColoredBox, PieceColor.Red));
            entries.Add(library.Crate(ObstacleCatalog.ColoredBox, PieceColor.Blue));
            entries.Add(library.Crate(ObstacleCatalog.CyclingBox, PieceColor.Green));

            entries.Add(library.Cell(false));
            entries.Add(library.Cell(true));
            entries.Add(library.Glow());
            entries.Add(library.Spark());
            entries.Add(library.Ring());
            entries.Add(library.Panel());

            string path = ArtifactPaths.Screenshot("sprites.png");
            WriteSheet(entries, path);
            Debug.Log($"[TOOL] art preview written to {path}");

            library.Dispose();
        }

        private static void WriteSheet(List<Sprite> sprites, string path)
        {
            int rows = Mathf.CeilToInt(sprites.Count / (float)Columns);
            int width = Columns * (Cell + Padding) + Padding;
            int height = rows * (Cell + Padding) + Padding;

            var sheet = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var background = new Color[width * height];
            Color checkerA = new Color(0.20f, 0.23f, 0.32f);
            Color checkerB = new Color(0.25f, 0.28f, 0.38f);

            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                background[y * width + x] = ((x / 16 + y / 16) % 2 == 0) ? checkerA : checkerB;

            sheet.SetPixels(background);

            for (int i = 0; i < sprites.Count; i++)
            {
                Sprite sprite = sprites[i];
                var source = (Texture2D)sprite.texture;
                Color[] pixels = source.GetPixels();

                int column = i % Columns;
                int row = i / Columns;
                int originX = Padding + column * (Cell + Padding);
                int originY = height - Padding - (row + 1) * Cell - row * Padding;

                for (int y = 0; y < source.height; y++)
                for (int x = 0; x < source.width; x++)
                {
                    // Scale small sprites up so every tile in the sheet is the same size.
                    int scale = Cell / source.width;
                    Color pixel = pixels[y * source.width + x];

                    for (int sy = 0; sy < scale; sy++)
                    for (int sx = 0; sx < scale; sx++)
                    {
                        int px = originX + x * scale + sx;
                        int py = originY + y * scale + sy;
                        if (px < 0 || px >= width || py < 0 || py >= height)
                            continue;

                        Color existing = sheet.GetPixel(px, py);
                        sheet.SetPixel(px, py, ShapeMath.Over(pixel, existing));
                    }
                }
            }

            sheet.Apply();
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, sheet.EncodeToPNG());
            Object.DestroyImmediate(sheet);
        }
    }
}
