using System;
using System.Collections.Generic;
using HarmonyLib;
using Ostranauts.Electrical;
using UnityEngine;

namespace SolarPanels
{
    [HarmonyPatch(typeof(Electrical), "AddSignal")]
    internal static class SolarPanelElectricalGuardPatch
    {
        private static readonly System.Reflection.FieldInfo CoSelfField =
            AccessTools.Field(typeof(Electrical), "coSelf");

        private static readonly System.Reflection.FieldInfo SignalQueueField =
            AccessTools.Field(typeof(Electrical), "signalQueue");

        private static readonly System.Reflection.FieldInfo NextSignalCheckField =
            AccessTools.Field(typeof(Electrical), "fTimeOfNextSignalCheck");

        private static readonly System.Reflection.FieldInfo NeedsCheckField =
            AccessTools.Field(typeof(Electrical), "bNeedsCheck");

        private static readonly System.Reflection.FieldInfo MapGpmField =
            AccessTools.Field(typeof(Electrical), "mapGPM");

        private static readonly System.Reflection.FieldInfo GpmKeyField =
            AccessTools.Field(typeof(Electrical), "strGPMKey");

        private static readonly System.Reflection.FieldInfo OverrideField =
            AccessTools.Field(typeof(Electrical), "bOverride");

        private static readonly System.Reflection.MethodInfo PropagateSignalMethod =
            AccessTools.Method(typeof(Electrical), "PropagateSignal");

        private static bool Prefix(Electrical __instance, ElectricalSignal signal)
        {
            if (__instance == null || __instance.GetComponent<SolarPanelIC>() == null)
            {
                return true;
            }

            CondOwner coSelf = (CondOwner)CoSelfField?.GetValue(__instance) ?? __instance.GetComponent<CondOwner>();
            string gpmKey = (string)GpmKeyField?.GetValue(__instance) ?? "Electrical";
            Dictionary<string, string> mapGpm = (Dictionary<string, string>)MapGpmField?.GetValue(__instance);

            bool hasSafeVanillaState = coSelf != null &&
                                       coSelf.mapGUIPropMaps != null &&
                                       mapGpm != null &&
                                       !string.IsNullOrEmpty(gpmKey);

            if (hasSafeVanillaState)
            {
                return true;
            }

            NeedsCheckField?.SetValue(__instance, true);

            List<ElectricalSignal> signalQueue = (List<ElectricalSignal>)SignalQueueField?.GetValue(__instance);
            if (signalQueue == null)
            {
                signalQueue = new List<ElectricalSignal>();
                SignalQueueField?.SetValue(__instance, signalQueue);
            }

            signalQueue.Add(signal);

            double nextSignalCheck = NextSignalCheckField != null
                ? (double)NextSignalCheckField.GetValue(__instance)
                : 0.0;
            if (nextSignalCheck < StarSystem.fEpoch)
            {
                NextSignalCheckField?.SetValue(__instance, StarSystem.fEpoch + 0.1);
            }

            if (coSelf == null)
            {
                return false;
            }

            if (CoSelfField != null)
            {
                CoSelfField.SetValue(__instance, coSelf);
            }

            if (string.IsNullOrEmpty(gpmKey))
            {
                gpmKey = "Electrical";
                GpmKeyField?.SetValue(__instance, gpmKey);
            }

            if (coSelf.mapGUIPropMaps == null)
            {
                return false;
            }

            if ((mapGpm == null || mapGpm.Count == 0) &&
                coSelf.mapGUIPropMaps.TryGetValue(gpmKey, out Dictionary<string, string> existingMap) &&
                existingMap != null)
            {
                mapGpm = existingMap;
            }

            if (mapGpm == null)
            {
                mapGpm = new Dictionary<string, string>();
            }

            MapGpmField?.SetValue(__instance, mapGpm);

            mapGpm["signalQueue"] = BuildSignalQueueString(signalQueue);
            coSelf.mapGUIPropMaps[gpmKey] = mapGpm;
            return false;
        }

        [HarmonyPatch(typeof(Electrical), "ToggleOverride")]
        [HarmonyPrefix]
        private static bool ToggleOverridePrefix(Electrical __instance)
        {
            if (__instance == null || __instance.GetComponent<SolarPanelIC>() == null)
            {
                return true;
            }

            CondOwner coSelf = (CondOwner)CoSelfField?.GetValue(__instance) ?? __instance.GetComponent<CondOwner>();
            string gpmKey = (string)GpmKeyField?.GetValue(__instance) ?? "Electrical";
            Dictionary<string, string> mapGpm = (Dictionary<string, string>)MapGpmField?.GetValue(__instance);

            bool hasSafeVanillaState = coSelf != null &&
                                       coSelf.mapGUIPropMaps != null &&
                                       mapGpm != null &&
                                       !string.IsNullOrEmpty(gpmKey);
            if (hasSafeVanillaState)
            {
                return true;
            }

            bool bOverride = OverrideField != null && (bool)OverrideField.GetValue(__instance);
            bOverride = !bOverride;
            OverrideField?.SetValue(__instance, bOverride);

            if (coSelf == null)
            {
                return false;
            }

            if (CoSelfField != null)
            {
                CoSelfField.SetValue(__instance, coSelf);
            }

            if (string.IsNullOrEmpty(gpmKey))
            {
                gpmKey = "Electrical";
                GpmKeyField?.SetValue(__instance, gpmKey);
            }

            if (coSelf.mapGUIPropMaps != null &&
                (mapGpm == null || mapGpm.Count == 0) &&
                coSelf.mapGUIPropMaps.TryGetValue(gpmKey, out Dictionary<string, string> existingMap) &&
                existingMap != null)
            {
                mapGpm = existingMap;
            }

            if (mapGpm == null)
            {
                mapGpm = new Dictionary<string, string>();
            }

            MapGpmField?.SetValue(__instance, mapGpm);
            mapGpm["override"] = bOverride.ToString().ToLower();
            if (coSelf.mapGUIPropMaps != null)
            {
                coSelf.mapGUIPropMaps[gpmKey] = mapGpm;
            }

            PropagateSignalMethod?.Invoke(__instance, new object[] { bOverride ? SignalType.On : SignalType.Off });
            __instance.ResolveSignalQueue();

            if (bOverride)
            {
                coSelf.ZeroCondAmount("IsOverrideOff");
            }
            else
            {
                coSelf.AddCondAmount("IsOverrideOff", 1.0, 0.0, 0f);
            }

            return false;
        }

        private static string BuildSignalQueueString(List<ElectricalSignal> signalQueue)
        {
            if (signalQueue == null || signalQueue.Count == 0)
            {
                return string.Empty;
            }

            return string.Join(",", signalQueue.ConvertAll(signal => signal.ToString()));
        }
    }
}
