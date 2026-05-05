using System;
using UnityEngine;

namespace SolarPanels
{
    public abstract class SolarPanelIC : MonoBehaviour
    {
        private CondOwner co;
        private double fTimeNextRun = 0.0;
        private const double UPDATE_INTERVAL = 1.0;
        public double CurrentProducedKWhThisTick { get; private set; }

        protected abstract double RatedWatts { get; }
        protected virtual double MinBufferKWh => 0.000001;

        private void Awake()
        {
            co = GetComponent<CondOwner>();
        }

        private void Update()
        {
            if (co == null || co.ship == null || co.bDestroyed) return;

            if (StarSystem.fEpoch >= fTimeNextRun)
            {
                fTimeNextRun = StarSystem.fEpoch + UPDATE_INTERVAL;
                Run();
            }
        }

        private void Run()
        {
            // Only generate power while the panel is installed on a ship.
            if (!co.HasCond("IsInstalled"))
            {
                CurrentProducedKWhThisTick = 0.0;
                co.ZeroCondAmount("IsReadyRecharge");
                co.ZeroCondAmount("StatPower");
                return;
            }

            CurrentProducedKWhThisTick = CalculateProducedKWhThisTick();

            // Keep the panel's internal buffer equal to the current produced
            // energy so vanilla recharge logic cannot snap it up to a larger
            // rated-output bank before the next transfer pass.
            double activeBufferKWh = Math.Max(CurrentProducedKWhThisTick, MinBufferKWh);
            co.SetCondAmount("IsReadyRecharge", 1.0, 0.0);
            co.SetCondAmount("StatPowerMax", activeBufferKWh, 0.0);

            // Publish only the currently generated energy, capped by the
            // current active buffer so nearby-sun output cannot turn into stored bulk charge.
            co.SetCondAmount("StatPower", Math.Min(CurrentProducedKWhThisTick, activeBufferKWh), 0.0);
        }

        protected virtual double CalculateProducedKWhThisTick()
        {
            double distAU = AUUtils.DistanceShipToSolAU(co.ship);
            if (double.IsNaN(distAU) || distAU <= 0.0)
            {
                return 0.0;
            }

            double multiplier = 1.0 / (distAU * distAU);
            double producedWatts = Math.Min(RatedWatts * multiplier, RatedWatts);
            return (producedWatts / 1000.0) * (UPDATE_INTERVAL / 3600.0);
        }
    }
}
