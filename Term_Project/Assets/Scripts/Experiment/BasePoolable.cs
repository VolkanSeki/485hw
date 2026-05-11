using System.Collections;
using ModularExperiment.ObjectPooling;
using UnityEngine;

namespace ModularExperiment.Experiment
{
    /// <summary>
    /// Base behaviour for pool-managed GameObjects.
    /// Handles lifecycle toggling and delayed auto-return.
    /// </summary>
    public class BasePoolable : MonoBehaviour, IPoolable
    {
        [SerializeField]
        private string poolKey;

        [SerializeField]
        [Min(0f)]
        private float defaultLifetimeSeconds = 2f;

        private Coroutine autoReturnRoutine;

        /// <summary>
        /// The pool key this instance returns itself to.
        /// </summary>
        public string PoolKey => poolKey;

        /// <summary>
        /// Updates the pool key at runtime (typically assigned by a factory).
        /// </summary>
        public void AssignPoolKey(string key)
        {
            poolKey = key;
        }

        /// <summary>
        /// Schedules this object to return itself to its pool.
        /// Any existing schedule is replaced.
        /// </summary>
        public void ReturnToPoolAfter(float delay)
        {
            CancelScheduledReturn();

            if (!isActiveAndEnabled)
            {
                return;
            }

            if (delay <= 0f)
            {
                ReturnToPool();
                return;
            }

            autoReturnRoutine = StartCoroutine(ReturnAfterDelayRoutine(delay));
        }

        /// <summary>
        /// Returns this object to its configured pool immediately.
        /// </summary>
        public void ReturnToPool()
        {
            if (string.IsNullOrWhiteSpace(poolKey))
            {
                Debug.LogWarning($"[{nameof(BasePoolable)}] Missing poolKey on '{name}'. Cannot return to pool.", this);
                return;
            }

            PoolManager.Return<BasePoolable>(poolKey, this);
        }

        public virtual void OnSpawn()
        {
            // Ensure stale lifetime timers from previous uses cannot fire.
            CancelScheduledReturn();
            gameObject.SetActive(true);

            if (defaultLifetimeSeconds > 0f)
            {
                ReturnToPoolAfter(defaultLifetimeSeconds);
            }
        }

        public virtual void OnDespawn()
        {
            // Ensure return timer cannot fire while inactive.
            CancelScheduledReturn();
            gameObject.SetActive(false);
        }

        protected virtual void OnDisable()
        {
            // Safety: avoid coroutine leaks if object gets disabled externally.
            CancelScheduledReturn();
        }

        private IEnumerator ReturnAfterDelayRoutine(float delay)
        {
            yield return new WaitForSeconds(delay);
            autoReturnRoutine = null;
            ReturnToPool();
        }

        private void CancelScheduledReturn()
        {
            if (autoReturnRoutine == null)
            {
                return;
            }

            StopCoroutine(autoReturnRoutine);
            autoReturnRoutine = null;
        }
    }
}
