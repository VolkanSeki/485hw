namespace ModularExperiment.ObjectPooling
{
    /// <summary>
    /// Non-generic contract used by a central manager
    /// to store and operate on multiple pools.
    /// </summary>
    public interface IObjectPool
    {
        /// <summary>
        /// Number of currently inactive items in this pool.
        /// </summary>
        int InactiveCount { get; }

        /// <summary>
        /// Number of items instantiated by this pool.
        /// </summary>
        int TotalAllocations { get; }

        /// <summary>
        /// Number of requests served from inactive pooled items.
        /// </summary>
        int TotalReuses { get; }

        /// <summary>
        /// Adds more inactive items to the pool.
        /// </summary>
        /// <param name="count">Number of items to create.</param>
        void PreWarm(int count);

        /// <summary>
        /// Removes all currently inactive items from the pool.
        /// </summary>
        void ClearInactive();
    }
}
