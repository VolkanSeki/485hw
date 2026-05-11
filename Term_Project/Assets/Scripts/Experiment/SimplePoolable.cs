using UnityEngine;

namespace ModularExperiment.Experiment
{
    /// <summary>
    /// Lightweight pooled object example.
    /// Resets transform state on spawn.
    /// </summary>
    public class SimplePoolable : BasePoolable
    {
        private Vector3 initialLocalPosition;
        private Quaternion initialLocalRotation;
        private Vector3 initialLocalScale;

        protected virtual void Awake()
        {
            initialLocalPosition = transform.localPosition;
            initialLocalRotation = transform.localRotation;
            initialLocalScale = transform.localScale;
        }

        public override void OnSpawn()
        {
            base.OnSpawn();
            transform.localPosition = initialLocalPosition;
            transform.localRotation = initialLocalRotation;
            transform.localScale = initialLocalScale;
        }
    }
}
