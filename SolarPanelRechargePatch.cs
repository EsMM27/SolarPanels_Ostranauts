using System.Collections.Generic;
using HarmonyLib;
using Ostranauts.Utils;
using UnityEngine;

namespace SolarPanels
{
    [HarmonyPatch(typeof(Powered), "Recharge")]
    internal static class SolarPanelRechargePatch
    {
        private static readonly System.Reflection.FieldInfo PowerLastField =
            AccessTools.Field(typeof(Powered), "fPowerLast");

        private static bool Prefix(Powered __instance)
        {
            CondOwner source = __instance.CO;
            SolarPanelIC solarPanelIC = (source == null) ? null : source.GetComponent<SolarPanelIC>();
            if (source == null || solarPanelIC == null)
            {
                return true;
            }

            if (source.ship == null || source.bDestroyed || !source.HasCond("IsInstalled"))
            {
                SetRemainingPower(__instance, 0.0);
                return false;
            }

            if (source.mapPoints == null || !source.mapPoints.ContainsKey("PowerOutput"))
            {
                return true;
            }

            double expectedThisTick = solarPanelIC.CurrentProducedKWhThisTick;
            double available = source.GetCondAmount("StatPower");
            if (expectedThisTick > 0.0)
            {
                available = System.Math.Min(available, expectedThisTick);
            }

            if (available <= 0.0)
            {
                SetRemainingPower(__instance, 0.0);
                return false;
            }

            List<Powered> batteries = FindRechargeBatteries(source);
            if (batteries.Count == 0)
            {
                SetRemainingPower(__instance, available);
                return false;
            }

            List<Powered> demandingBatteries = new List<Powered>(batteries.Count);
            List<double> demands = new List<double>(batteries.Count);
            double totalDemand = 0.0;
            foreach (Powered battery in batteries)
            {
                double demand = battery.PowerRechargeAmount;
                if (demand <= 0.0)
                {
                    continue;
                }

                demandingBatteries.Add(battery);
                demands.Add(demand);
                totalDemand += demand;
            }

            if (totalDemand <= 0.0)
            {
                SetRemainingPower(__instance, available);
                return false;
            }

            double remaining = available;
            double transferred = 0.0;

            for (int i = 0; i < demandingBatteries.Count; i++)
            {
                Powered battery = demandingBatteries[i];
                double demand = demands[i];
                double share = (i == demandingBatteries.Count - 1)
                    ? remaining
                    : System.Math.Min(remaining, available * (demand / totalDemand));

                share = System.Math.Min(share, demand);
                if (share <= 0.0)
                {
                    continue;
                }

                battery.CO.AddCondAmount("StatPower", share, 0.0, 0f);
                RecordPowerChange(battery, share);
                transferred += share;
                remaining -= share;

                if (remaining <= 0.0)
                {
                    remaining = 0.0;
                    break;
                }
            }

            if (transferred > 0.0)
            {
                RecordPowerChange(__instance, -transferred);
            }

            SetRemainingPower(__instance, remaining);
            return false;
        }

        private static List<Powered> FindRechargeBatteries(CondOwner source)
        {
            List<Powered> batteries = new List<Powered>();
            Vector2 outputPos = source.GetPos("PowerOutput", false);
            Tile powerTile = source.ship.GetTileAtWorldCoords1(outputPos.x, outputPos.y, true, true);
            if (powerTile == null || powerTile.aConnectedPowerCOs == null)
            {
                return batteries;
            }

            foreach (Powered connected in powerTile.aConnectedPowerCOs)
            {
                if (connected == null || connected.CO == null || connected.CO == source)
                {
                    continue;
                }

                if (!connected.CO.HasCond("IsPowerStorage"))
                {
                    continue;
                }

                batteries.Add(connected);
            }

            return batteries;
        }

        private static void RecordPowerChange(Powered powered, double amount)
        {
            if (powered.PowerUsageRecorder == null)
            {
                powered.PowerUsageRecorder = new PowerUsageRecorder();
            }

            powered.PowerUsageRecorder.RecordChange(amount);
        }

        private static void SetRemainingPower(Powered powered, double remaining)
        {
            PowerLastField?.SetValue(powered, remaining);
            powered.CO.SetCondAmount("StatPower", remaining, 0.0);
        }
    }
}
