using System.Collections.Generic;

namespace Match3.Core
{
    /// <summary>Turns a declarative <see cref="EntitySpec"/> into a live board entity.</summary>
    public static class EntityFactory
    {
        /// <summary>
        /// Creates the entity described by <paramref name="spec"/> without placing it.
        /// Returns null for <see cref="EntitySpecKind.Empty"/>.
        /// </summary>
        public static BoardEntity Create(Board board, EntitySpec spec, ObstacleCatalog catalog,
            IReadOnlyList<PieceColor> palette, Rng rng)
        {
            if (spec == null)
                return null;

            switch (spec.Kind)
            {
                case EntitySpecKind.Empty:
                    return null;

                case EntitySpecKind.RandomPiece:
                    return board.CreatePiece(rng.Pick(palette));

                case EntitySpecKind.ColoredPiece:
                {
                    PieceColor color = spec.Color != PieceColor.None ? spec.Color : rng.Pick(palette);
                    return board.CreatePiece(color, spec.Booster, spec.Orientation);
                }

                case EntitySpecKind.Obstacle:
                {
                    ObstacleConfig config = catalog.Get(spec.ObstacleId);
                    var obstacle = new Obstacle(
                        board.NewEntityId(),
                        config,
                        spec.HpOverride,
                        spec.Color,
                        spec.Contains,
                        spec.WidthOverride,
                        spec.HeightOverride);

                    obstacle.ColorPalette = palette;

                    // A colour-based crate with no colour given picks one from the palette, so
                    // level data can stay terse.
                    if (config.UsesColor && obstacle.RequiredColor == PieceColor.None)
                        obstacle.RequiredColor = rng.Pick(palette);

                    return obstacle;
                }

                default:
                    return null;
            }
        }
    }
}
