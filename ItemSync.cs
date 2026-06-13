using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace BetterMultiplayer
{
    public static class ItemSync
    {
        public static bool isSyncing = false;
        private static readonly Dictionary<string, string> localStateCache = new Dictionary<string, string>();
        private static float lastPollTime = 0f;
        private const float PollInterval = 1.0f; // Check for changes every 1 second

        private static readonly HashSet<string> Whitelist = new HashSet<string>()
        {
            // Abilities
            "hasDash", "canDash", "hasWalljump", "canWallJump", "hasDoubleJump", "hasSuperDash", "canSuperDash", "hasAcidArmour", 
            "hasDreamNail", "dreamNailUpgraded", "hasShadowDash", "canShadowDash", "hasWaterSwim", "hasLantern",
            "hasCyclone", "hasGreatSlash", "hasDashSlash", "canOvercharm",
            // Spells
            "fireballLevel", "quakeLevel", "screechLevel",
            // Nail
            "nailSmithUpgrades",
            // HP/Soul
            "maxHealth", "maxHealthBase", "maxHealthCap", "heartPieces", "heartPieceMax",
            // Stags
            "openedCrossroads", "openedGreenpath", "openedFungalWastes", "openedRuins1", "openedRuins2",
            "openedRestingGrounds", "openedDeepnest", "openedHiddenStation", "openedStagNest", "openedGardensStagStation",
            "stationsOpened", "hasStagKey",
            // Benches
            "tollBenchCity", "tollBenchQueensGardens", "tollBenchAbyss",
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
            // Dream nail essence
            "dreamOrbs", "dreamOrbsSpent", "hasDreamGate",
            // Grimm troupe
            "grimmChildLevel", "gotGrimmNotch",
            // Shop Inventory (Sly & Salubra purchases)
            "slyShellFrag1", "slyShellFrag2", "slyShellFrag3", "slyShellFrag4",
            "slyVesselFrag1", "slyVesselFrag2", "slyVesselFrag3", "slyVesselFrag4",
            "slyNotch1", "slyNotch2", "slySimpleKey", "slyRancidEgg", "gotSlyCharm",
            "salubraNotch1", "salubraNotch2", "salubraNotch3", "salubraNotch4", "salubraBlessing"
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

        public static void Update()
        {
            // Self-heal any broken ability states locally
            if (PlayerData.instance != null)
            {
                if (PlayerData.instance.hasDash && !PlayerData.instance.canDash)
                {
                    PlayerData.instance.canDash = true;
                    BetterMultiplayer.Instance.Log("Self-healing: Fixed hasDash = true but canDash = false.");
                }
                if (PlayerData.instance.hasWalljump && !PlayerData.instance.canWallJump)
                {
                    PlayerData.instance.canWallJump = true;
                    BetterMultiplayer.Instance.Log("Self-healing: Fixed hasWalljump = true but canWallJump = false.");
                }
                if (PlayerData.instance.hasSuperDash && !PlayerData.instance.canSuperDash)
                {
                    PlayerData.instance.canSuperDash = true;
                    BetterMultiplayer.Instance.Log("Self-healing: Fixed hasSuperDash = true but canSuperDash = false.");
                }
                if (PlayerData.instance.hasShadowDash && !PlayerData.instance.canShadowDash)
                {
                    PlayerData.instance.canShadowDash = true;
                    BetterMultiplayer.Instance.Log("Self-healing: Fixed hasShadowDash = true but canShadowDash = false.");
                }
            }

            if (UnityEngine.Time.unscaledTime - lastPollTime >= PollInterval)
            {
                lastPollTime = UnityEngine.Time.unscaledTime;
                PollLocalChanges();
            }
        }

        private static void PollLocalChanges()
        {
            if (PlayerData.instance == null || isSyncing) return;

            foreach (var key in Whitelist)
            {
                try
                {
                    FieldInfo field = typeof(PlayerData).GetField(key, BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
                    if (field != null)
                    {
                        var fieldVal = field.GetValue(PlayerData.instance);
                        string currentVal = fieldVal != null ? fieldVal.ToString() : "";
                        
                        // If key is not in cache, initialize it
                        if (!localStateCache.TryGetValue(key, out string cachedVal))
                        {
                            localStateCache[key] = currentVal;
                            continue;
                        }

                        // If value changed locally, update cache
                        if (currentVal != cachedVal)
                        {
                            localStateCache[key] = currentVal;
                            BetterMultiplayer.Instance.Log($"[Local Cache] Detected change: {key} = {currentVal}");
                            
                            // Only send if it's a bool that became true, or an int that changed/increased
                            bool shouldSend = false;
                            if (field.FieldType == typeof(bool))
                            {
                                shouldSend = (currentVal == "True");
                            }
                            else if (field.FieldType == typeof(int))
                            {
                                int currentInt = (int)fieldVal;
                                int cachedInt = 0;
                                int.TryParse(cachedVal, out cachedInt);
                                
                                if (key == "heartPieces" || key == "vesselFragments" || key == "simpleKeys" || key == "ore")
                                {
                                    shouldSend = (currentInt != cachedInt);
                                }
                                else
                                {
                                    shouldSend = (currentInt > cachedInt);
                                }
                            }

                            if (shouldSend)
                            {
                                string type = (field.FieldType == typeof(bool)) ? "bool" : "int";
                                NetworkManager.SendPacket($"ITEM|{type}|{key}|{currentVal}");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    BetterMultiplayer.Instance.LogError($"Error polling field {key}: {ex.Message}");
                }
            }
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
                    // Only sync if the incoming value is true and we don't have it yet
                    if (parsedVal && !PlayerData.instance.GetBool(key))
                    {
                        BetterMultiplayer.Instance.Log($"[Network] Syncing bool {key} = {parsedVal}");
                        isSyncing = true;
                        PlayerData.instance.SetBool(key, parsedVal);
                        localStateCache[key] = val; // Update cache to prevent reflection detection of network write as local change
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
                    bool shouldSync = false;

                    if (key == "heartPieces" || key == "vesselFragments" || key == "simpleKeys" || key == "ore")
                    {
                        shouldSync = (parsedVal != current);
                    }
                    else
                    {
                        shouldSync = (parsedVal > current);
                    }

                    if (shouldSync)
                    {
                        BetterMultiplayer.Instance.Log($"[Network] Syncing int {key} = {parsedVal}");
                        isSyncing = true;

                        // If max health is increasing, heal the player's current health by the difference
                        if ((key == "maxHealth" || key == "maxHealthBase") && parsedVal > current)
                        {
                            int diff = parsedVal - current;
                            int newHealth = PlayerData.instance.GetInt("health") + diff;
                            PlayerData.instance.SetInt("health", newHealth);
                            BetterMultiplayer.Instance.Log($"[Network] Player max health increased, healing current health by +{diff} to {newHealth}");
                        }

                        PlayerData.instance.SetInt(key, parsedVal);
                        localStateCache[key] = val; // Update cache to prevent reflection detection of network write as local change
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
                        string valStr = "";
                        if (field.FieldType == typeof(bool))
                        {
                            bool val = (bool)field.GetValue(PlayerData.instance);
                            valStr = val.ToString();
                            if (val)
                            {
                                NetworkManager.SendPacket($"ITEM|bool|{key}|{val}");
                            }
                        }
                        else if (field.FieldType == typeof(int))
                        {
                            int val = (int)field.GetValue(PlayerData.instance);
                            valStr = val.ToString();
                            if (val > 0)
                            {
                                NetworkManager.SendPacket($"ITEM|int|{key}|{val}");
                            }
                        }
                        localStateCache[key] = valStr;
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
