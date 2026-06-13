using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace BetterMultiplayer
{
    public static class ItemSync
    {
        public static bool isSyncing = false;
        private static readonly Dictionary<string, FieldInfo> FieldCache = new Dictionary<string, FieldInfo>();
        private static readonly Dictionary<string, bool> cachedBools = new Dictionary<string, bool>();
        private static readonly Dictionary<string, int> cachedInts = new Dictionary<string, int>();
        private static float lastPollTime = 0f;
        private const float PollInterval = 1.0f; // Check for changes every 1 second

        private static readonly Dictionary<string, HashSet<string>> ListCaches = new Dictionary<string, HashSet<string>>()
        {
            { "scenesMapped", new HashSet<string>() },
            { "scenesEncounteredBench", new HashSet<string>() },
            { "scenesGrubRescued", new HashSet<string>() },
            { "scenesFlameCollected", new HashSet<string>() },
            { "scenesEncounteredCocoon", new HashSet<string>() },
            { "scenesEncounteredDreamPlant", new HashSet<string>() },
            { "scenesEncounteredDreamPlantC", new HashSet<string>() }
        };

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
            foreach (var key in Whitelist)
            {
                try
                {
                    FieldInfo field = typeof(PlayerData).GetField(key, BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
                    if (field != null)
                    {
                        FieldCache[key] = field;
                    }
                }
                catch (Exception ex)
                {
                    BetterMultiplayer.Instance.LogError($"Error caching field {key}: {ex.Message}");
                }
            }
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
                PollListChanges();
                EnemySync.SendLocalShadeState();
            }
        }

        private static void PollLocalChanges()
        {
            if (PlayerData.instance == null || isSyncing) return;

            foreach (var entry in FieldCache)
            {
                string key = entry.Key;
                FieldInfo field = entry.Value;
                try
                {
                    if (field.FieldType == typeof(bool))
                    {
                        bool val = (bool)field.GetValue(PlayerData.instance);
                        if (!cachedBools.TryGetValue(key, out bool cachedVal))
                        {
                            cachedBools[key] = val;
                        }
                        else if (val != cachedVal)
                        {
                            cachedBools[key] = val;
                            BetterMultiplayer.Instance.Log($"[Local Cache] Detected change: {key} = {val}");
                            if (val) // only sync if it became true
                            {
                                NetworkManager.SendPacket($"ITEM|bool|{key}|True");
                            }
                        }
                    }
                    else if (field.FieldType == typeof(int))
                    {
                        int val = (int)field.GetValue(PlayerData.instance);
                        if (!cachedInts.TryGetValue(key, out int cachedVal))
                        {
                            cachedInts[key] = val;
                        }
                        else if (val != cachedVal)
                        {
                            cachedInts[key] = val;
                            BetterMultiplayer.Instance.Log($"[Local Cache] Detected change: {key} = {val}");
                            bool shouldSend = false;
                            if (key == "heartPieces" || key == "vesselFragments" || key == "simpleKeys" || key == "ore")
                            {
                                shouldSend = true;
                            }
                            else
                            {
                                shouldSend = (val > cachedVal);
                            }

                            if (shouldSend)
                            {
                                NetworkManager.SendPacket($"ITEM|int|{key}|{val}");
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
                if (!FieldCache.TryGetValue(key, out FieldInfo field)) return;

                bool changed = false;

                if (type == "bool")
                {
                    bool parsedVal = bool.Parse(val);
                    bool current = (bool)field.GetValue(PlayerData.instance);
                    
                    // Only sync if the incoming value is true and we don't have it yet
                    if (parsedVal && !current)
                    {
                        BetterMultiplayer.Instance.Log($"[Network] Syncing bool {key} = {parsedVal}");
                        isSyncing = true;
                        field.SetValue(PlayerData.instance, parsedVal);
                        cachedBools[key] = parsedVal; // Update cache
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
                    int current = (int)field.GetValue(PlayerData.instance);
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
                            int newHealth = PlayerData.instance.health + diff;
                            PlayerData.instance.health = newHealth;
                            BetterMultiplayer.Instance.Log($"[Network] Player max health increased, healing current health by +{diff} to {newHealth}");
                        }

                        field.SetValue(PlayerData.instance, parsedVal);
                        cachedInts[key] = parsedVal; // Update cache
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
                cachedBools[name] = val;
                NetworkManager.SendPacket($"ITEM|bool|{name}|{val}");
            }
        }

        public static void OnSetPlayerInt(string name, int val)
        {
            if (!isSyncing && Whitelist.Contains(name))
            {
                BetterMultiplayer.Instance.Log($"[Local] Player set int {name} = {val}");
                cachedInts[name] = val;
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
            foreach (var entry in FieldCache)
            {
                string key = entry.Key;
                FieldInfo field = entry.Value;
                try
                {
                    if (field.FieldType == typeof(bool))
                    {
                        bool val = (bool)field.GetValue(PlayerData.instance);
                        cachedBools[key] = val;
                        if (val)
                        {
                            NetworkManager.SendPacket($"ITEM|bool|{key}|True");
                        }
                    }
                    else if (field.FieldType == typeof(int))
                    {
                        int val = (int)field.GetValue(PlayerData.instance);
                        cachedInts[key] = val;
                        if (val > 0)
                        {
                            NetworkManager.SendPacket($"ITEM|int|{key}|{val}");
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

        public static void ApplyPersistentBool(string sceneName, string id, bool activated, bool semiPersistent)
        {
            try
            {
                if (SceneData.instance == null) return;

                isSyncing = true;
                
                // Find or create the persistent state in global storage
                PersistentBoolData data = SceneData.instance.persistentBoolItems.Find(x => x.id == id && x.sceneName == sceneName);
                if (data == null)
                {
                    data = new PersistentBoolData { id = id, sceneName = sceneName };
                    SceneData.instance.persistentBoolItems.Add(data);
                }
                data.activated = activated;
                data.semiPersistent = semiPersistent;

                BetterMultiplayer.Instance.Log($"[PersistentBool] Received activation for {id} in {sceneName}");

                // If players are in the same scene, instantly update the visual object (pull lever, break wall, open chest)
                if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == sceneName)
                {
                    foreach (var item in UnityEngine.Object.FindObjectsOfType<PersistentBoolItem>())
                    {
                        if (item != null && item.GetId() == id)
                        {
                            if (item.persistentBoolData != null)
                            {
                                item.persistentBoolData.activated = activated;
                            }
                            item.SaveState();
                            item.PreSetup();
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                BetterMultiplayer.Instance.LogError($"Error applying persistent bool: " + ex);
            }
            finally
            {
                isSyncing = false;
            }
        }

        private static void PollListChanges()
        {
            if (PlayerData.instance == null || isSyncing) return;

            foreach (var entry in ListCaches)
            {
                string listName = entry.Key;
                HashSet<string> cache = entry.Value;

                try
                {
                    FieldInfo field = typeof(PlayerData).GetField(listName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
                    if (field != null)
                    {
                        var list = field.GetValue(PlayerData.instance) as List<string>;
                        if (list != null)
                        {
                            if (cache.Count == 0 && list.Count > 0)
                            {
                                foreach (var item in list)
                                {
                                    cache.Add(item);
                                }
                            }
                            else
                            {
                                foreach (var item in list)
                                {
                                    if (!string.IsNullOrEmpty(item) && !cache.Contains(item))
                                    {
                                        cache.Add(item);
                                        BetterMultiplayer.Instance.Log($"[Local Cache] Detected list add in {listName}: {item}");
                                        NetworkManager.SendPacket($"LIST_ADD|{listName}|{item}");
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    BetterMultiplayer.Instance.LogError($"Error polling list field {listName}: {ex.Message}");
                }
            }
        }

        public static void ApplyListAdd(string listName, string val)
        {
            try
            {
                if (PlayerData.instance == null) return;
                
                FieldInfo field = typeof(PlayerData).GetField(listName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
                if (field != null)
                {
                    var list = field.GetValue(PlayerData.instance) as List<string>;
                    if (list != null)
                    {
                        isSyncing = true;
                        if (!list.Contains(val))
                        {
                            BetterMultiplayer.Instance.Log($"[Network] Adding to list {listName}: {val}");
                            list.Add(val);
                            if (ListCaches.TryGetValue(listName, out var cache))
                            {
                                cache.Add(val);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                BetterMultiplayer.Instance.LogError($"Error applying list add for {listName}: {ex.Message}");
            }
            finally
            {
                isSyncing = false;
            }
        }

        public static void ApplyPersistentInt(string sceneName, string id, int value, bool semiPersistent)
        {
            try
            {
                if (SceneData.instance == null) return;

                isSyncing = true;

                PersistentIntData data = SceneData.instance.persistentIntItems.Find(x => x.id == id && x.sceneName == sceneName);
                if (data == null)
                {
                    data = new PersistentIntData { id = id, sceneName = sceneName };
                    SceneData.instance.persistentIntItems.Add(data);
                }
                data.value = value;
                data.semiPersistent = semiPersistent;

                BetterMultiplayer.Instance.Log($"[PersistentInt] Received value update for {id} in {sceneName} = {value}");

                if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == sceneName)
                {
                    foreach (var item in UnityEngine.Object.FindObjectsOfType<PersistentIntItem>())
                    {
                        if (item != null && item.GetId() == id)
                        {
                            if (item.persistentIntData != null)
                            {
                                item.persistentIntData.value = value;
                            }
                            var setValMethod = typeof(PersistentIntItem).GetMethod("SetValueOnFSM", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                            if (setValMethod != null)
                            {
                                setValMethod.Invoke(item, new object[] { value });
                            }

                            var saveStateMethod = typeof(PersistentIntItem).GetMethod("SaveState", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                            if (saveStateMethod != null)
                            {
                                saveStateMethod.Invoke(item, null);
                            }
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                BetterMultiplayer.Instance.LogError($"Error applying persistent int: " + ex);
            }
            finally
            {
                isSyncing = false;
            }
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

    [HarmonyPatch(typeof(SceneData), "SaveMyState", new Type[] { typeof(PersistentBoolData) })]
    public static class SceneData_SaveMyState_Patch
    {
        public static void Postfix(PersistentBoolData persistentBoolData)
        {
            if (persistentBoolData != null && !ItemSync.isSyncing)
            {
                string sceneName = string.IsNullOrEmpty(persistentBoolData.sceneName) ? UnityEngine.SceneManagement.SceneManager.GetActiveScene().name : persistentBoolData.sceneName;
                BetterMultiplayer.Instance.Log($"[PersistentBool] Broadcasting activation for {persistentBoolData.id} in {sceneName}: {persistentBoolData.activated}");
                NetworkManager.SendPacket($"PERSIST_BOOL|{sceneName}|{persistentBoolData.id}|{persistentBoolData.activated}|{persistentBoolData.semiPersistent}");
            }
        }
    }

    [HarmonyPatch(typeof(PersistentBoolItem), nameof(PersistentBoolItem.SaveState))]
    public static class PersistentBoolItem_SaveState_Patch
    {
        public static void Postfix(PersistentBoolItem __instance)
        {
            if (__instance != null && __instance.persistentBoolData != null && !ItemSync.isSyncing)
            {
                string sceneName = (__instance.persistentBoolData != null && !string.IsNullOrEmpty(__instance.persistentBoolData.sceneName)) ? __instance.persistentBoolData.sceneName : UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
                string id = __instance.GetId();
                bool activated = __instance.persistentBoolData.activated;
                bool semi = __instance.persistentBoolData.semiPersistent;
                BetterMultiplayer.Instance.Log($"[PersistentBool] Broadcasting activation for {id} in {sceneName} via SaveState: {activated}");
                NetworkManager.SendPacket($"PERSIST_BOOL|{sceneName}|{id}|{activated}|{semi}");
            }
        }
    }

    [HarmonyPatch(typeof(SceneData), "SaveMyState", new Type[] { typeof(PersistentIntData) })]
    public static class SceneData_SaveMyState_Int_Patch
    {
        public static void Postfix(PersistentIntData persistentIntData)
        {
            if (persistentIntData != null && !ItemSync.isSyncing)
            {
                string sceneName = string.IsNullOrEmpty(persistentIntData.sceneName) ? UnityEngine.SceneManagement.SceneManager.GetActiveScene().name : persistentIntData.sceneName;
                BetterMultiplayer.Instance.Log($"[PersistentInt] Broadcasting value for {persistentIntData.id} in {sceneName}: {persistentIntData.value}");
                NetworkManager.SendPacket($"PERSIST_INT|{sceneName}|{persistentIntData.id}|{persistentIntData.value}|{persistentIntData.semiPersistent}");
            }
        }
    }

    [HarmonyPatch(typeof(PersistentIntItem), "SaveState")]
    public static class PersistentIntItem_SaveState_Patch
    {
        public static void Postfix(PersistentIntItem __instance)
        {
            if (__instance != null && __instance.persistentIntData != null && !ItemSync.isSyncing)
            {
                string sceneName = (__instance.persistentIntData != null && !string.IsNullOrEmpty(__instance.persistentIntData.sceneName)) ? __instance.persistentIntData.sceneName : UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
                string id = __instance.GetId();
                int value = __instance.persistentIntData.value;
                bool semi = __instance.persistentIntData.semiPersistent;
                BetterMultiplayer.Instance.Log($"[PersistentInt] Broadcasting value for {id} in {sceneName} via SaveState: {value}");
                NetworkManager.SendPacket($"PERSIST_INT|{sceneName}|{id}|{value}|{semi}");
            }
        }
    }
}
