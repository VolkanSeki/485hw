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
        public enum VisibilityMode
        {
            SetActive = 0,
            RenderersAndColliders = 1
        }

        [SerializeField]
        private string poolKey;

        [SerializeField]
        [Min(0f)]
        private float defaultLifetimeSeconds = 2f;

        [Header("Despawn Strategy")]
        [SerializeField]
        private VisibilityMode visibilityMode = VisibilityMode.SetActive;

        [SerializeField]
        private Vector3 discardPosition = new Vector3(-9999f, -9999f, -9999f);

        private Coroutine autoReturnRoutine;
        private Coroutine destroyRoutine;
        private Vector3 initialLocalScale;
        private bool hasInitialLocalScale;
        private Renderer[] cachedRenderers;
        private Collider[] cachedColliders;

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
            CancelLifetimeSchedules();

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

        /// <summary>
        /// Schedules lifetime handling for pooled or non-pooled usage.
        /// - poolingEnabled: returns to pool
        /// - !poolingEnabled: destroys the instance
        /// </summary>
        public void ScheduleLifetime(float delay, bool poolingEnabled)
        {
            CancelLifetimeSchedules();

            var safeDelay = delay > 0f ? delay : 0.01f;
            if (poolingEnabled)
            {
                autoReturnRoutine = StartCoroutine(ReturnAfterDelayRoutine(safeDelay));
            }
            else
            {
                destroyRoutine = StartCoroutine(DestroyAfterDelayRoutine(safeDelay));
            }
        }

        public virtual void OnSpawn()
        {
            // Ensure stale lifetime timers from previous uses cannot fire.
            CancelLifetimeSchedules();
            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }

            if (visibilityMode == VisibilityMode.RenderersAndColliders)
            {
                SetVisibleComponents(enabled: true);
            }

            if (hasInitialLocalScale)
            {
                transform.localScale = initialLocalScale;
            }

            if (defaultLifetimeSeconds > 0f)
            {
                ReturnToPoolAfter(defaultLifetimeSeconds);
            }
        }

        public virtual void OnDespawn()
        {
            // Ensure return timer cannot fire while inactive.
            CancelLifetimeSchedules();
            if (visibilityMode == VisibilityMode.SetActive)
            {
                gameObject.SetActive(false);
                return;
            }

            transform.position = discardPosition;
            SetVisibleComponents(enabled: false);
        }

        protected virtual void OnDisable()
        {
            // Safety: avoid coroutine leaks if object gets disabled externally.
            CancelLifetimeSchedules();
        }

        private void Awake()
        {
            initialLocalScale = transform.localScale;
            hasInitialLocalScale = true;
            CacheVisibilityComponents();
        }

        private IEnumerator ReturnAfterDelayRoutine(float delay)
        {
            yield return new WaitForSeconds(delay);
            autoReturnRoutine = null;
            ReturnToPool();
        }

        private IEnumerator DestroyAfterDelayRoutine(float delay)
        {
            yield return new WaitForSeconds(delay);
            destroyRoutine = null;
            Destroy(gameObject);
        }

        private void CancelLifetimeSchedules()
        {
            if (autoReturnRoutine == null)
            {
                // Continue to non-pooled routine check.
            }
            else
            {
                StopCoroutine(autoReturnRoutine);
                autoReturnRoutine = null;
            }

            if (destroyRoutine != null)
            {
                StopCoroutine(destroyRoutine);
                destroyRoutine = null;
            }
        }

        private void CacheVisibilityComponents()
        {
            cachedRenderers = GetComponentsInChildren<Renderer>(true);
            cachedColliders = GetComponentsInChildren<Collider>(true);
        }

        private void SetVisibleComponents(bool enabled)
        {
            if (cachedRenderers == null || cachedColliders == null)
            {
                CacheVisibilityComponents();
            }

            if (cachedRenderers != null)
            {
                for (var i = 0; i < cachedRenderers.Length; i++)
                {
                    var rendererComponent = cachedRenderers[i];
                    if (rendererComponent != null)
                    {
                        rendererComponent.enabled = enabled;
                    }
                }
            }

            if (cachedColliders != null)
            {
                for (var i = 0; i < cachedColliders.Length; i++)
                {
                    var colliderComponent = cachedColliders[i];
                    if (colliderComponent != null)
                    {
                        colliderComponent.enabled = enabled;
                    }
                }
            }
        }
    }
}
