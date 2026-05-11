namespace ModularExperiment.ObjectPooling
{
    /// <summary>
    /// Lifecycle hooks for objects that can be reused by a pool.
    /// </summary>
    public interface IPoolable
    {
        /// <summary>
        /// Called when the object is handed out by a pool.
        /// Use this to initialize runtime state before use.
        /// </summary>
        void OnSpawn();

        /// <summary>
        /// Called when the object is returned to a pool.
        /// Use this to reset state and release transient resources.
        /// </summary>
        void OnDespawn();
    }
}
