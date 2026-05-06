using System;
using System.Collections.Generic;
using HarmonyLib;

namespace SolarPanels
{
    [HarmonyPatch(typeof(DataHandler), "PostModLoadMainThread")]
    internal static class SolarPanelLootInjectionPatch
    {
        private const string SolarDamagedPartsLoot = "ItmRandomDamagedPartsSolar=1.0x1-2";

        private static readonly string[] TargetKioskLoots =
        {
            "ItmOKLGSupplyKioskInv",
            "ItmFlotillaScrapKioskInv",
            "ItmVORBScrapKioskInv"
        };

        private static bool _applied;

        private static void Postfix()
        {
            if (_applied)
            {
                return;
            }

            bool appendedAny = false;
            foreach (string lootName in TargetKioskLoots)
            {
                if (TryAppendLootEntry(lootName, SolarDamagedPartsLoot))
                {
                    appendedAny = true;
                }
            }

            if (appendedAny)
            {
                SolarPanelPlugin.Log?.LogInfo("Injected solar panel loot into kiosk inventories.");
            }

            _applied = true;
        }

        private static bool TryAppendLootEntry(string lootName, string entry)
        {
            Loot loot = DataHandler.GetLoot(lootName);
            if (loot == null || loot.strName != lootName)
            {
                SolarPanelPlugin.Log?.LogWarning($"Unable to find kiosk loot '{lootName}' for solar injection.");
                return false;
            }

            string[] currentLoots = loot.aLoots ?? Array.Empty<string>();
            foreach (string current in currentLoots)
            {
                if (string.Equals(current, entry, StringComparison.Ordinal))
                {
                    return false;
                }
            }

            List<string> updatedLoots = new List<string>(currentLoots)
            {
                entry
            };
            loot.aLoots = updatedLoots.ToArray();
            return true;
        }
    }
}
