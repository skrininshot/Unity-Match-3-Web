namespace Match3.Core
{
    /// <summary>
    /// Decides what damages an obstacle. This is the single extension point for new
    /// board-element mechanics: a new element type is a new rule plus a config entry,
    /// never a change to the turn resolver.
    /// </summary>
    public interface IObstacleDamageRule
    {
        string Id { get; }

        /// <summary>True if nothing can ever destroy this obstacle (the Blocker).</summary>
        bool IsIndestructible { get; }

        bool Accepts(Obstacle obstacle, in DamageSource source);

        /// <summary>
        /// Called on every obstacle once per player turn, after the turn has been resolved.
        /// Returns true if the obstacle changed and the change should be reported to the view
        /// (used by the colour-changing box).
        /// </summary>
        bool OnTurnAdvanced(Obstacle obstacle, Rng rng);
    }
}
