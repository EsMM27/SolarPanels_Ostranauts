using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

[BepInPlugin("edgar.ostranauts.SolarPanels", "SolarPanels", "0.1.1")]
public class SolarPanelPlugin : BaseUnityPlugin
{
    internal static ManualLogSource Log;
    private Harmony harmony;

    private void Awake()
    {
        Log = Logger;
        harmony = new Harmony("edgar.ostranauts.SolarPanels");
        harmony.PatchAll();
        Logger.LogInfo("SolarPanels plugin loaded.");
    }

    private void OnDestroy()
    {
        harmony?.UnpatchSelf();
    }
}
