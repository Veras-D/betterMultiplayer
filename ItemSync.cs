using System;
using System.Collections.Generic;
using HarmonyLib;

namespace BetterMultiplayer
{
    public static class ItemSync
    {
        public static bool isSyncing = false;

        private static readonly HashSet<string> Whitelist = new HashSet<string>()
        {
            // Abilities
            "hasDash", "hasWallJump", "hasDoubleJump", "hasSuperDash", "hasAcidArmour", 
            "hasDreamNail", "dreamNailUpgraded", "hasShadowDash", "hasWaterSwim", "hasLantern",
            "hasCyclone", "hasGreatSlash", "hasDashSlash",
            // Spells
            "fireballLevel", "quakeLevel", "screechLevel",
            // Nail
            "nailLimit",
            // HP/Soul
            "maxHealth", "maxHealthBase", "maxHealthCap", "heartPieces", "heartPieceMax",
            "maxMP", "MPReserveMax", "vesselFragments", "vesselFragmentMax",
            // Keys & Maps
            "simpleKeys", "hasLoveKey", "hasSpaKey", "hasSlykey", "gaveSlykey", "hasTramPass", 
            "hasWhiteKey", "hasGodfinder", "hasKingBrand", "kingsBrand", "hasMap", "mapQuill",
            "hasCityKey", "gotLurkerKey", "hasMenderKey",
            // Dreamers & fragile / unbreakable upgrades & flower quest
            "maskBrokenLurien", "maskBrokenHegemol", "maskBrokenMonomon",
            "gaveFragileHeart", "gaveFragileGreed", "gaveFragileStrength",
            "fragileHealth_unbreakable", "fragileGreed_unbreakable", "fragileStrength_unbreakable",
            "brokenCharm_23", "brokenCharm_24", "brokenCharm_25",
            "hasXunFlower", "xunFlowerBroken", "xunFlowerGiven",
            "givenGodseekerFlower", "givenOroFlower", "givenWhiteLadyFlower", "givenEmilitiaFlower",
            // Maps
            "mapDirtmouth", "mapCrossroads", "mapGreenpath", "mapFogCanyon", "mapRoyalGardens", 
            "mapFungalWastes", "mapCity", "mapWaterways", "mapMines", "mapDeepnest", "mapCliffs", 
            "mapOutskirts", "mapRestingGrounds", "mapAbyss",
            // Pins
            "hasPin", "hasPinBench", "hasPinCocoon", "hasPinDreamPlant", "hasPinGuardian", 
            "hasPinBlackEgg", "hasPinShop", "hasPinSpa", "hasPinStag", "hasPinTram", 
            "hasPinGhost", "hasPinGrub",
            // Markers
            "hasMarker", "hasMarker_r", "hasMarker_b", "hasMarker_y", "hasMarker_w", 
            "spareMarkers_r", "spareMarkers_b", "spareMarkers_y", "spareMarkers_w",
            // Grubs & Ores
            "grubsCollected", "ore",
            // Trinkets
            "trinket1", "trinket2", "trinket3", "trinket4",
            // Stags
            "openedStagCrossroads", "openedStagGreenpath", "openedStagFungalWastes", 
            "openedStagCity", "openedStagRestingGrounds", "openedStagDeepnest", 
            "openedStagHiddenStation", "openedStagStagNest", "openedStagWaterways", 
            "openedStagAncientBasin",
            // Dream nail essence
            "dreamOrbs", "dreamOrbsSpent", "hasDreamGate",
            // Grimm troupe
            "grimmChildLevel", "gotGrimmNotch"
        };

        static ItemSync()
        {
            // Add charm entries gotCharm_1 to gotCharm_40 dynamically
            for (int i = 1; i <= 40; i++)
            {
                Whitelist.Add("gotCharm_" + i);
            }
            Whitelist.Add("charmSlots");
        }

        public static void Initialize()
        {
            // Handled via Harmony auto-patching
        }

        public static void ApplyNetworkChange(string type, string key, string val)
        {
            try
            {
                if (PlayerData.instance == null) return;

                bool changed = false;

                if (type == "bool")
                {
                    bool parsedVal = bool.Parse(val);
                    if (PlayerData.instance.GetBool(key) != parsedVal)
                    {
                        BetterMultiplayer.Instance.Log($"[Network] Syncing bool {key} = {parsedVal}");
                        isSyncing = true;
                        PlayerData.instance.SetBool(key, parsedVal);
                        changed = true;

                        // Reset respawning enemies when partner sits at a bench
                        if (key == "atBench" && parsedVal && GameManager.instance != null)
                        {
                            BetterMultiplayer.Instance.Log("[Network] Partner sat at bench, resetting semi-persistent items/enemies.");
                            GameManager.instance.ResetSemiPersistentItems();
                        }
                    }
                }
                else if (type == "int")
                {
                    int parsedVal = int.Parse(val);
                    int current = PlayerData.instance.GetInt(key);
                    if (parsedVal > current)
                    {
                        BetterMultiplayer.Instance.Log($"[Network] Syncing int {key} = {parsedVal}");
                        isSyncing = true;
                        PlayerData.instance.SetInt(key, parsedVal);
                        changed = true;
                    }
                }

                if (changed)
                {
                    if (HeroController.instance != null)
                    {
                        HeroController.instance.CharmUpdate();
                        HeroController.instance.MaxHealthKeepBlue();
                    }
                    try
                    {
                        PlayMakerFSM.BroadcastEvent("CHARM UPDATE");
                        PlayMakerFSM.BroadcastEvent("MAX HEALTH UPDATE");
                    }
                    catch {}
                }
            }
            catch (Exception ex)
            {
                BetterMultiplayer.Instance.LogError("Error in ApplyNetworkChange: " + ex);
            }
            finally
            {
                isSyncing = false;
            }
        }

        public static void OnSetPlayerBool(string name, bool val)
        {
            if (!isSyncing && Whitelist.Contains(name))
            {
                BetterMultiplayer.Instance.Log($"[Local] Player set bool {name} = {val}");
                NetworkManager.SendPacket($"ITEM|bool|{name}|{val}");
            }
        }

        public static void OnSetPlayerInt(string name, int val)
        {
            if (!isSyncing && Whitelist.Contains(name))
            {
                BetterMultiplayer.Instance.Log($"[Local] Player set int {name} = {val}");
                NetworkManager.SendPacket($"ITEM|int|{name}|{val}");
            }
        }

        public static void SendAllItems()
        {
            if (PlayerData.instance == null)
            {
                BetterMultiplayer.Instance.Log("Cannot sync items: PlayerData.instance is null.");
                return;
            }

            BetterMultiplayer.Instance.Log("Syncing all whitelisted items to peer...");
            foreach (var key in Whitelist)
            {
                try
                {
                    var field = typeof(PlayerData).GetField(key, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                    if (field != null)
                    {
                        if (field.FieldType == typeof(bool))
                        {
                            bool val = (bool)field.GetValue(PlayerData.instance);
                            if (val)
                            {
                                NetworkManager.SendPacket($"ITEM|bool|{key}|{val}");
                            }
                        }
                        else if (field.FieldType == typeof(int))
                        {
                            int val = (int)field.GetValue(PlayerData.instance);
                            if (val > 0)
                            {
                                NetworkManager.SendPacket($"ITEM|int|{key}|{val}");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    BetterMultiplayer.Instance.LogError($"Error syncing item {key}: {ex.Message}");
                }
            }
            BetterMultiplayer.Instance.Log("Item sync completed.");
        }
    }

    [HarmonyPatch(typeof(PlayerData), nameof(PlayerData.SetBool))]
    public static class PlayerData_SetBool_Patch
    {
        public static void Postfix(PlayerData __instance, string boolName, bool value)
        {
            ItemSync.OnSetPlayerBool(boolName, value);
        }
    }

    [HarmonyPatch(typeof(PlayerData), nameof(PlayerData.SetInt))]
    public static class PlayerData_SetInt_Patch
    {
        public static void Postfix(PlayerData __instance, string intName, int value)
        {
            ItemSync.OnSetPlayerInt(intName, value);
        }
    }
}
