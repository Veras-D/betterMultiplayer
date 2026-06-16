using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using ItemSyncMod;
using UnityEngine;

namespace BetterMultiplayer
{
    // Built-in item sync, reimplemented on top of the label-based
    // ItemSyncMod.Connection API (which mirrors the original
    // fireb0rn/ItemSync API that DeathSync and other HKMP addons
    // hook into). Addons can subscribe to Connection.OnDataReceived
    // with their own label to piggyback on the same transport.
    //
    // Built-in labels (see ItemSyncMod.ItemSyncMod.Labels):
    //   "itemsync"    — PlayerData bool / int changes
    //   "persistbool" — PersistentBoolItem state (levers, walls, etc.)
    //   "persistint"  — PersistentIntItem state
    //   "listadd"     — additions to PlayerData lists (charms, etc.)
    //
    // Built-in data formats (all pipe-delimited):
    //   itemsync    : <type>|<key>|<val>           type = "bool" | "int"
    //   persistbool : <scene>|<id>|<activated>|<semi>
    //   persistint  : <scene>|<id>|<val>|<semi>
    //   listadd     : <listName>|<val>
    public static class ItemSync
    {
        public static bool isSyncing = false;
        private static readonly Dictionary<string, FieldInfo> FieldCache = new Dictionary<string, FieldInfo>();
        private static readonly Dictionary<string, bool> cachedBools = new Dictionary<string, bool>();
        private static readonly Dictionary<string, int> cachedInts = new Dictionary<string, int>();
        private static float lastPollTime = 0f;
        private const float PollInterval = 0.5f;

        private static readonly Dictionary<string, HashSet<string>> ListCaches = new Dictionary<string, HashSet<string>>()
        {
            { "royalCharmEquips", new HashSet<string>() },
            { "charmEquips", new HashSet<string>() },
        };

        // Whitelist of PlayerData fields to sync. Only bool/int
        // fields are supported. List fields are hardcoded above
        // (charmEquips, royalCharmEquips) because they need a
        // different polling strategy.
        private static readonly HashSet<string> Whitelist = new HashSet<string>()
        {
            "hasDash", "canDash",
            "hasWalljump", "canWallJump",
            "hasDoubleJump",
            "hasSuperDash", "canSuperDash",
            "hasAcidArmour",
            "hasDreamNail",
            "hasLantern",
            "hasCyclone",
            "hasUpwardSlash", "hasDashSlash",
            "fireballLevel", "quakeLevel", "screamLevel",
            "nailSmithUpgrades",
            "maxHealth", "maxHealthBase",
            "health", "healthBlue",
            "heartPieces",
            "vesselFragments", "MPReserveMax", "MPReserve",
            "simpleKeys",
            "ore",
            "ghostCoins",
            "travellersPassed",
            "sewersPassed",
            "openedTown", "openedCrossroads", "openedGreenpath",
            "openedFungalWastes", "openedRuins1", "openedRuins2",
            "openedRestingGrounds", "openedDeepnest", "openedHiddenStation",
            "openedStagNest", "openedGardensStagStation",
            "tollBenchCity", "tollBenchQueensGardens", "tollBenchAbyss",
            "hasLoveKey", "hasTramPass", "hasWhiteKey",
            "gotCharm_1", "gotCharm_2", "gotCharm_3", "gotCharm_4", "gotCharm_5",
            "gotCharm_6", "gotCharm_7", "gotCharm_8", "gotCharm_9", "gotCharm_10",
            "gotCharm_11", "gotCharm_22", "gotCharm_27", "gotCharm_28", "gotCharm_29",
            "gotCharm_30", "gotCharm_31", "gotCharm_32", "gotCharm_33", "gotCharm_38",
            "hasKingFragment", "hasQueenFragment", "hasVoidFragment",
            "maskBrokenHegemol", "maskBrokenLurien", "maskBrokenMonomon",
            "killedMageLord", "killedMageLordDream",
            "killedDungDefender", "killedBlackKnight",
            "killedInfectedKnight", "killedMimicSpider",
            "killedTraitorLord",
            "killedZote", "killedHornet",
            "killedMegaMossCharger", "killedMantisLords",
            "killedOblobbles", "killedFlukemarm",
            "killedBrokenVessel", "killedBroodingMawlek",
            "killedNosk", "killedWingedNosk",
            "killedCollector", "killedCrystalGuardian",
            "killedEnragedGuardian", "killedLobbedLancer",
            "killedMimicSpider",
            "gotMapCrossroads", "gotMapGreenpath", "gotMapFogCanyon",
            "gotMapFungalWastes", "gotMapCity", "gotMapDeepnest",
            "gotMapRoyalGardens", "gotMapRestingGrounds",
            "gotMapKingdomsEdge", "gotMapHowlingCliffs",
            "gotMapAbyss", "gotMapWhitePalace", "gotMapColosseum",
        };

        public static void Initialize()
        {
            // Subscribe to incoming ISC packets. We filter by label
            // inside the handler so addons can subscribe to their
            // own labels via the same Connection.
            ItemSyncMod.ItemSyncMod.Connection.OnDataReceived += OnIscDataReceived;

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

        // Incoming ISC packet dispatch. Label-based so addons can
        // also subscribe to Connection.OnDataReceived for their own
        // labels and we'll just ignore their labels here.
        private static void OnIscDataReceived(object sender, DataReceivedEvent e)
        {
            if (e.Handled) return;
            if (e.Label == ItemSyncMod.ItemSyncMod.Labels.ItemSync)
            {
                e.Handled = true;
                // data format: <type>|<key>|<val>
                var parts = e.Data.Split('|');
                if (parts.Length >= 3)
                {
                    ApplyNetworkChange(parts[0], parts[1], parts[2]);
                }
            }
            else if (e.Label == ItemSyncMod.ItemSyncMod.Labels.PersistBool)
            {
                e.Handled = true;
                // data format: <scene>|<id>|<activated>|<semi>
                var parts = e.Data.Split('|');
                if (parts.Length >= 4)
                {
                    bool activated = bool.Parse(parts[2]);
                    bool semi = bool.Parse(parts[3]);
                    ApplyPersistentBool(parts[0], parts[1], activated, semi);
                }
            }
            else if (e.Label == ItemSyncMod.ItemSyncMod.Labels.PersistInt)
            {
                e.Handled = true;
                // data format: <scene>|<id>|<val>|<semi>
                var parts = e.Data.Split('|');
                if (parts.Length >= 4)
                {
                    int val = int.Parse(parts[2]);
                    bool semi = bool.Parse(parts[3]);
                    ApplyPersistentInt(parts[0], parts[1], val, semi);
                }
            }
            else if (e.Label == ItemSyncMod.ItemSyncMod.Labels.ListAdd)
            {
                e.Handled = true;
                // data format: <listName>|<val>
                var parts = e.Data.Split('|');
                if (parts.Length >= 2)
                {
                    ApplyListAdd(parts[0], parts[1]);
                }
            }
        }

        public static void Update()
        {
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
                            if (val)
                            {
                                SendItemSyncBool(key, true);
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
                                SendItemSyncInt(key, val);
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

        // === SEND HELPERS (use the label-based Connection API) ===

        private static void SendItemSyncBool(string key, bool val)
        {
            ItemSyncMod.ItemSyncMod.Connection.SendDataToAll(
                ItemSyncMod.ItemSyncMod.Labels.ItemSync,
                "bool|" + key + "|" + val);
        }

        private static void SendItemSyncInt(string key, int val)
        {
            ItemSyncMod.ItemSyncMod.Connection.SendDataToAll(
                ItemSyncMod.ItemSyncMod.Labels.ItemSync,
                "int|" + key + "|" + val);
        }

        private static void SendPersistBool(string scene, string id, bool activated, bool semi)
        {
            ItemSyncMod.ItemSyncMod.Connection.SendDataToAll(
                ItemSyncMod.ItemSyncMod.Labels.PersistBool,
                scene + "|" + id + "|" + activated + "|" + semi);
        }

        private static void SendPersistInt(string scene, string id, int val, bool semi)
        {
            ItemSyncMod.ItemSyncMod.Connection.SendDataToAll(
                ItemSyncMod.ItemSyncMod.Labels.PersistInt,
                scene + "|" + id + "|" + val + "|" + semi);
        }

        private static void SendListAdd(string listName, string val)
        {
            ItemSyncMod.ItemSyncMod.Connection.SendDataToAll(
                ItemSyncMod.ItemSyncMod.Labels.ListAdd,
                listName + "|" + val);
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

                    if (parsedVal && !current)
                    {
                        BetterMultiplayer.Instance.Log($"[Network] Syncing bool {key} = {parsedVal}");
                        isSyncing = true;
                        field.SetValue(PlayerData.instance, parsedVal);
                        cachedBools[key] = parsedVal;
                        changed = true;

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

                        if ((key == "maxHealth" || key == "maxHealthBase") && parsedVal > current)
                        {
                            int diff = parsedVal - current;
                            int newHealth = PlayerData.instance.health + diff;
                            PlayerData.instance.health = newHealth;
                            BetterMultiplayer.Instance.Log($"[Network] Player max health increased, healing current health by +{diff} to {newHealth}");
                        }

                        field.SetValue(PlayerData.instance, parsedVal);
                        cachedInts[key] = parsedVal;
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
                SendItemSyncBool(name, val);
            }
        }

        public static void OnSetPlayerInt(string name, int val)
        {
            if (!isSyncing && Whitelist.Contains(name))
            {
                BetterMultiplayer.Instance.Log($"[Local] Player set int {name} = {val}");
                cachedInts[name] = val;
                SendItemSyncInt(name, val);
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
                            SendItemSyncBool(key, true);
                        }
                    }
                    else if (field.FieldType == typeof(int))
                    {
                        int val = (int)field.GetValue(PlayerData.instance);
                        cachedInts[key] = val;
                        if (val > 0)
                        {
                            SendItemSyncInt(key, val);
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

                PersistentBoolData data = SceneData.instance.persistentBoolItems.Find(x => x.id == id && x.sceneName == sceneName);
                if (data == null)
                {
                    data = new PersistentBoolData { id = id, sceneName = sceneName };
                    SceneData.instance.persistentBoolItems.Add(data);
                }
                data.activated = activated;
                data.semiPersistent = semiPersistent;

                BetterMultiplayer.Instance.Log($"[PersistentBool] Received activation for {id} in {sceneName}");

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
                                        SendListAdd(listName, item);
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
                BetterMultiplayer.Instance.LogError($"Error applying persistent int: " + ex.Message);
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
                // Use the label-based send helper
                ItemSyncMod.ItemSyncMod.Connection.SendDataToAll(
                    ItemSyncMod.ItemSyncMod.Labels.PersistBool,
                    sceneName + "|" + persistentBoolData.id + "|" + persistentBoolData.activated + "|" + persistentBoolData.semiPersistent);
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
                ItemSyncMod.ItemSyncMod.Connection.SendDataToAll(
                    ItemSyncMod.ItemSyncMod.Labels.PersistBool,
                    sceneName + "|" + id + "|" + activated + "|" + semi);
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
                ItemSyncMod.ItemSyncMod.Connection.SendDataToAll(
                    ItemSyncMod.ItemSyncMod.Labels.PersistInt,
                    sceneName + "|" + persistentIntData.id + "|" + persistentIntData.value + "|" + persistentIntData.semiPersistent);
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
                ItemSyncMod.ItemSyncMod.Connection.SendDataToAll(
                    ItemSyncMod.ItemSyncMod.Labels.PersistInt,
                    sceneName + "|" + id + "|" + value + "|" + semi);
            }
        }
    }
}
