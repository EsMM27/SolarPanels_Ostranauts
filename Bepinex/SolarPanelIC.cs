using System;
using UnityEngine;

namespace SolarPanels
{
    public abstract class SolarPanelIC : MonoBehaviour
    {
        private CondOwner co;
        private SolarPanelOperationalBlink operationalBlink;
        private double fTimeNextRun = 0.0;
        private double fTimeNextLog = 0.0;
        private const double UPDATE_INTERVAL = 1.0;
        private const double LOG_INTERVAL = 10.0;
        private const double GasHeatCapacity = 20.7;
        public double CurrentProducedKWhThisTick { get; private set; }
        public double CurrentProducedWattsThisTick { get; private set; }

        protected abstract double RatedWatts { get; }
        protected virtual double MinBufferKWh => 0.000001;
        protected virtual double PanelEfficiency => 0.28;
        protected virtual double HeatWattsPerElectricWatt => (1.0 - PanelEfficiency) / PanelEfficiency;
        protected virtual double HullHeatCoupling => 0.05;

        private void Awake()
        {
            co = GetComponent<CondOwner>();
            operationalBlink = GetComponent<SolarPanelOperationalBlink>();
            if (operationalBlink == null)
            {
                operationalBlink = gameObject.AddComponent<SolarPanelOperationalBlink>();
            }
        }

        private void Update()
        {
            if (co == null || co.bDestroyed)
            {
                SetOperationalBlink(false);
                return;
            }

            if (StarSystem.fEpoch >= fTimeNextRun)
            {
                fTimeNextRun = StarSystem.fEpoch + UPDATE_INTERVAL;
                Run();
            }
        }

        private void Run()
        {
            if (TrySyncInstalledState())
            {
                return;
            }

            bool forcedOff = co.HasCond("IsOff") || co.HasCond("IsOverrideOff");
            bool signalSuppressed = !co.HasCond("IsOverrideOn") && co.HasCond("IsSignalOff");

            // Only generate power while the panel is installed on a ship and not switched off.
            if (!co.HasCond("IsInstalled") || forcedOff || signalSuppressed)
            {
                CurrentProducedKWhThisTick = 0.0;
                CurrentProducedWattsThisTick = 0.0;
                SetPoweredState(false);
                SetOperationalBlink(false);
                co.ZeroCondAmount("IsReadyRecharge");
                co.ZeroCondAmount("StatPower");
                LogDebug(signalSuppressed
                    ? "Skipping run because the panel is signalled off."
                    : forcedOff
                    ? "Skipping run because the panel is switched off."
                    : "Skipping run because the panel is not installed.");
                return;
            }

            CurrentProducedKWhThisTick = CalculateProducedKWhThisTick();
            CurrentProducedWattsThisTick = CurrentProducedKWhThisTick * 1000.0 * 3600.0 / UPDATE_INTERVAL;
            SetPoweredState(CurrentProducedWattsThisTick > 0.0);
            SetOperationalBlink(CurrentProducedWattsThisTick > 0.0);
            TransferWasteHeatToInteriorGas();

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

        private void SetPoweredState(bool powered)
        {
            if (co == null)
            {
                return;
            }

            if (powered)
            {
                co.SetCondAmount("IsPowered", 1.0, 0.0);
                return;
            }

            co.ZeroCondAmount("IsPowered");
        }

        private void SetOperationalBlink(bool enabled)
        {
            operationalBlink?.SetOperational(enabled);
        }

        private bool TrySyncInstalledState()
        {
            if (co == null || co.ship == null || !co.HasCond("IsInstalled"))
            {
                return false;
            }

            string onState = GetInstalledStateName(false);
            string offState = GetInstalledStateName(true);
            if (string.IsNullOrEmpty(onState) || string.IsNullOrEmpty(offState))
            {
                return false;
            }

            int knobState = GetKnobState();
            bool manualOff = knobState == 0 || co.HasCond("IsOverrideOff");
            bool manualOn = knobState == 2 || co.HasCond("IsOverrideOn");
            bool signalSuppressed = co.HasCond("IsSignalOff");

            if (manualOff)
            {
                if (co.strName != offState)
                {
                    return ModeSwitchTo(offState, "control panel set to Off", 0);
                }

                SetPanelKnobState(0);
                return false;
            }

            if (manualOn)
            {
                if (co.strName != onState)
                {
                    return ModeSwitchTo(onState, "control panel set to On", 2);
                }

                SetPanelKnobState(2);
                return false;
            }

            if (signalSuppressed)
            {
                if (co.strName != offState)
                {
                    return ModeSwitchTo(offState, "signal box set to Off", 1);
                }

                SetPanelKnobState(1);
                return false;
            }

            if (co.strName != onState)
            {
                return ModeSwitchTo(onState, "Auto mode restored panel to On", 1);
            }

            SetPanelKnobState(1);
            return false;
        }

        private int GetKnobState()
        {
            int knobState = co.HasCond("IsOff") ? 0 : 1;
            string value = co.GetGPMInfo("Panel A", "nKnobBus");
            if (!string.IsNullOrEmpty(value))
            {
                int.TryParse(value, out knobState);
            }

            return knobState;
        }

        private string GetInstalledStateName(bool offState)
        {
            if (co.HasCond("IsSolarPanelSmall"))
            {
                return offState ? "ItmSolarPanelSmallOff" : "ItmSolarPanelSmallOn";
            }

            if (co.HasCond("IsSolarPanelMedium"))
            {
                return offState ? "ItmSolarPanelMediumOff" : "ItmSolarPanelMediumOn";
            }

            if (co.HasCond("IsSolarPanelLarge"))
            {
                return offState ? "ItmSolarPanelLargeOff" : "ItmSolarPanelLargeOn";
            }

            return null;
        }

        private bool ModeSwitchTo(string targetState, string reason, int knobState)
        {
            CondOwner coNew = DataHandler.GetCondOwner(targetState, null, null, true, null, null, co.strID, null);
            if (coNew == null)
            {
                LogDebug($"Failed to mode switch to '{targetState}'.");
                return false;
            }

            co.ModeSwitch(coNew, co.tf.position);
            co = coNew;
            if (targetState.EndsWith("On"))
            {
                co.ZeroCondAmount("IsOff");
            }
            else if (targetState.EndsWith("Off"))
            {
                co.SetCondAmount("IsOff", 1.0, 0.0);
            }

            SetPanelKnobState(knobState);
            LogDebug($"Mode switched to '{targetState}' because {reason}.");
            return true;
        }

        private void SetPanelKnobState(int knobState)
        {
            if (co == null || co.mapGUIPropMaps == null)
            {
                return;
            }

            if (!co.mapGUIPropMaps.TryGetValue("Panel A", out var panelMap) || panelMap == null)
            {
                return;
            }

            panelMap["nKnobBus"] = knobState.ToString();
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

        private void TransferWasteHeatToInteriorGas()
        {
            if (CurrentProducedWattsThisTick <= 0.0 || co == null || co.ship == null)
            {
                LogDebug("No heat transfer because current produced watts is zero.");
                return;
            }

            Room room = GetNearestInteriorRoomWithGas();
            if (room == null || room.CO == null || room.CO.GasContainer == null)
            {
                LogDebug("No interior room with gas was found on this ship.");
                return;
            }

            double gasMoles;
            if (!room.CO.GasContainer.mapGasMols1.TryGetValue("StatGasMolTotal", out gasMoles) || gasMoles <= 0.0)
            {
                LogDebug($"Room '{room.CO.strName}' has no gas moles available for heat transfer.");
                return;
            }

            double wasteHeatWatts = CurrentProducedWattsThisTick * HeatWattsPerElectricWatt;
            double heatToShipWatts = wasteHeatWatts * HullHeatCoupling;
            double deltaTemp = heatToShipWatts * UPDATE_INTERVAL / GasHeatCapacity / gasMoles;
            room.CO.GasContainer.fDGasTemp += deltaTemp;
            LogDebug(
                $"Heat applied to room '{room.CO.strName}': produced={CurrentProducedWattsThisTick:F2}W, " +
                $"waste={wasteHeatWatts:F2}W, coupled={heatToShipWatts:F2}W, gasMoles={gasMoles:F2}, dGasTemp={deltaTemp:F5}K.");
        }

        private Room GetNearestInteriorRoomWithGas()
        {
            if (co == null || co.ship == null || co.ship.aRooms == null)
            {
                return null;
            }

            Vector2 panelPos = co.tf.position;
            Room bestRoom = null;
            float bestDistanceSq = float.MaxValue;

            foreach (Room room in co.ship.aRooms)
            {
                if (room == null || room.Void || room.CO == null || room.CO.GasContainer == null)
                {
                    continue;
                }

                double gasMoles;
                if (!room.CO.GasContainer.mapGasMols1.TryGetValue("StatGasMolTotal", out gasMoles) || gasMoles <= 0.0)
                {
                    continue;
                }

                float roomDistanceSq = GetClosestRoomDistanceSq(room, panelPos);
                if (roomDistanceSq < bestDistanceSq)
                {
                    bestDistanceSq = roomDistanceSq;
                    bestRoom = room;
                }
            }

            return bestRoom;
        }

        private static float GetClosestRoomDistanceSq(Room room, Vector2 panelPos)
        {
            if (room.aTiles != null && room.aTiles.Count > 0)
            {
                float bestDistanceSq = float.MaxValue;
                for (int i = 0; i < room.aTiles.Count; i++)
                {
                    Tile tile = room.aTiles[i];
                    if (tile == null || tile.tf == null)
                    {
                        continue;
                    }

                    float distanceSq = ((Vector2)tile.tf.position - panelPos).sqrMagnitude;
                    if (distanceSq < bestDistanceSq)
                    {
                        bestDistanceSq = distanceSq;
                    }
                }

                if (bestDistanceSq < float.MaxValue)
                {
                    return bestDistanceSq;
                }
            }

            if (room.CO != null && room.CO.tf != null)
            {
                return ((Vector2)room.CO.tf.position - panelPos).sqrMagnitude;
            }

            return float.MaxValue;
        }

        private void LogDebug(string message)
        {
            if (SolarPanelPlugin.Log == null || StarSystem.fEpoch < fTimeNextLog)
            {
                return;
            }

            fTimeNextLog = StarSystem.fEpoch + LOG_INTERVAL;
            string coName = co == null ? GetType().Name : $"{co.strName}/{co.strID}";
            SolarPanelPlugin.Log.LogInfo($"[SolarHeat] {coName}: {message}");
        }
    }
}
