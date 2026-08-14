using System;
using System.Collections.Generic;

namespace Match3.Core
{
    /// <summary>
    /// Seeded random source. The whole core takes its randomness from here so that any
    /// board, cascade or stress run can be reproduced exactly from a seed — which is what
    /// makes failing tests debuggable instead of "sometimes broken".
    /// </summary>
    public sealed class Rng
    {
        private readonly Random _random;

        public Rng(int seed)
        {
            Seed = seed;
            _random = new Random(seed);
        }

        public int Seed { get; }

        /// <summary>Random integer in [0, exclusiveMax).</summary>
        public int Next(int exclusiveMax) => _random.Next(exclusiveMax);

        /// <summary>Random integer in [min, exclusiveMax).</summary>
        public int Range(int min, int exclusiveMax) => _random.Next(min, exclusiveMax);

        public double NextDouble() => _random.NextDouble();

        public bool Chance(double probability) => _random.NextDouble() < probability;

        public T Pick<T>(IReadOnlyList<T> items)
        {
            if (items == null || items.Count == 0)
                throw new ArgumentException("Cannot pick from an empty collection.", nameof(items));
            return items[_random.Next(items.Count)];
        }

        /// <summary>In-place Fisher-Yates shuffle.</summary>
        public void Shuffle<T>(IList<T> items)
        {
            for (int i = items.Count - 1; i > 0; i--)
            {
                int j = _random.Next(i + 1);
                (items[i], items[j]) = (items[j], items[i]);
            }
        }
    }
}
