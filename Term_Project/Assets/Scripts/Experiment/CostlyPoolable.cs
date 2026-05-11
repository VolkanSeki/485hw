using System;
using UnityEngine;

namespace ModularExperiment.Experiment
{
    /// <summary>
    /// Heavier pooled object example.
    /// Simulates expensive setup and optional hierarchy complexity.
    /// </summary>
    public class CostlyPoolable : BasePoolable
    {
        [Header("Cost Simulation")]
        [SerializeField]
        private int primeUpperBound = 12000;

        [SerializeField]
        private bool runCostOnEverySpawn;

        [Header("Hierarchy Complexity")]
        [SerializeField]
        private int extraChildCount = 4;

        [SerializeField]
        private bool attachParticleSystems = true;

        private int lastPrimeCount;
        private bool heavySetupCompleted;

        protected virtual void Awake()
        {
            CreateComplexChildren();
            SimulateHeavyOperation();
            heavySetupCompleted = true;
        }

        public override void OnSpawn()
        {
            base.OnSpawn();

            if (runCostOnEverySpawn)
            {
                SimulateHeavyOperation();
            }
            else if (!heavySetupCompleted)
            {
                // Safety for unusual object lifecycles where Awake did not run yet.
                SimulateHeavyOperation();
                heavySetupCompleted = true;
            }
        }

        private void SimulateHeavyOperation()
        {
            var max = Mathf.Max(50, primeUpperBound);
            var count = 0;

            for (var n = 2; n <= max; n++)
            {
                if (IsPrime(n))
                {
                    count++;
                }
            }

            lastPrimeCount = count;
        }

        private static bool IsPrime(int value)
        {
            if (value < 2)
            {
                return false;
            }

            var boundary = (int)Math.Sqrt(value);
            for (var divisor = 2; divisor <= boundary; divisor++)
            {
                if (value % divisor == 0)
                {
                    return false;
                }
            }

            return true;
        }

        private void CreateComplexChildren()
        {
            var childrenToCreate = Mathf.Max(0, extraChildCount);
            if (childrenToCreate == 0)
            {
                return;
            }

            for (var i = 0; i < childrenToCreate; i++)
            {
                var child = new GameObject($"Detail_{i + 1}");
                child.transform.SetParent(transform, false);
                child.transform.localPosition = new Vector3(0.25f * i, 0f, 0f);

                if (attachParticleSystems)
                {
                    var ps = child.AddComponent<ParticleSystem>();
                    var main = ps.main;
                    main.playOnAwake = false;
                    main.loop = false;
                    main.startLifetime = 0.4f;
                    main.startSpeed = 0.2f;
                }
            }
        }
    }
}
