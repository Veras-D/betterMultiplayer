using System;
using System.Collections.Generic;
using System.Globalization;
using HarmonyLib;
using UnityEngine;
using HutongGames.PlayMaker;

namespace BetterMultiplayer
{
    public static class EnemySync
    {
        public static bool isSyncingEnemy = false;

        // Associate live HealthManagers with their unique ID.
        // Cleared on every scene transition to prevent memory leaks.
        private static readonly Dictionary<HealthManager, string> enemyIds = new Dictionary<HealthManager, string>();

        // Remote Shade state variables
        public static string RemoteShadeScene = "None";
        public static float RemoteShadeX = 0f;
        public static float RemoteShadeY = 0f;
        public static int RemoteShadeGeo = 0;
        public static bool RemoteSoulLimited = false;
        public static GameObject RemoteShadeInstance = null;

        // Local Shade change detection cache
        private static string lastShadeScene = "None";
        private static float lastShadeX = 0f;
        private static float lastShadeY = 0f;
        private static int lastGeoPool = 0;
        private static bool lastSoulLimited = false;

        public static void Initialize()
        {
            // Listen for scene transitions to clear references using Unity's native SceneManager
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += (scene, mode) => {
                OnSceneChanged(scene.name);
            };
        }

        private static void OnSceneChanged(string sceneName)
        {
            try
            {
                enemyIds.Clear();
                RemoteShadeInstance = null;
                UpdateRemoteShadeSpawning();
            }
            catch (Exception ex)
            {
                BetterMultiplayer.Instance.LogError("Error clearing enemy IDs on scene change: " + ex);
            }
        }

        public static void SendLocalShadeState(bool force = false)
        {
            if (PlayerData.instance == null || !NetworkManager.IsClientConnected) return;

            try
            {
                string currentScene = PlayerData.instance.GetString("shadeScene");
                float currentX = PlayerData.instance.GetFloat("shadePositionX");
                float currentY = PlayerData.instance.GetFloat("shadePositionY");
                int currentGeo = PlayerData.instance.GetInt("geoPool");
                bool currentLimited = PlayerData.instance.GetBool("soulLimited");

                if (force || 
                    currentScene != lastShadeScene || 
                    Mathf.Abs(currentX - lastShadeX) > 0.01f || 
                    Mathf.Abs(currentY - lastShadeY) > 0.01f || 
                    currentGeo != lastGeoPool || 
                    currentLimited != lastSoulLimited)
                {
                    lastShadeScene = currentScene;
                    lastShadeX = currentX;
                    lastShadeY = currentY;
                    lastGeoPool = currentGeo;
                    lastSoulLimited = currentLimited;

                    NetworkManager.SendPacket($"SHADE_STATE|{currentScene}|{currentX.ToString("F3", CultureInfo.InvariantCulture)}|{currentY.ToString("F3", CultureInfo.InvariantCulture)}|{currentGeo}|{currentLimited}");
                }
            }
            catch (Exception ex)
            {
                BetterMultiplayer.Instance.LogError("Error in SendLocalShadeState: " + ex);
            }
        }

        public static void HandleRemoteShadeState(string scene, float x, float y, int geo, bool limited)
        {
            RemoteShadeScene = scene;
            RemoteShadeX = x;
            RemoteShadeY = y;
            RemoteShadeGeo = geo;
            RemoteSoulLimited = limited;

            BetterMultiplayer.Instance.Log($"[ShadeSync] Received remote shade state: scene={scene}, x={x}, y={y}, geo={geo}, limited={limited}");

            UpdateRemoteShadeSpawning();
        }

        public static void UpdateRemoteShadeSpawning()
        {
            try
            {
                if (PlayerData.instance == null) return;

                string activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
                bool shouldBeSpawned = (RemoteShadeScene != "None" && !string.IsNullOrEmpty(RemoteShadeScene) && RemoteShadeScene == activeScene && RemoteSoulLimited);

                if (shouldBeSpawned)
                {
                    if (RemoteShadeInstance == null)
                    {
                        SpawnRemoteShade();
                    }
                }
                else
                {
                    if (RemoteShadeInstance != null)
                    {
                        BetterMultiplayer.Instance.Log("[ShadeSync] Destroying remote shade instance.");
                        UnityEngine.Object.Destroy(RemoteShadeInstance);
                        RemoteShadeInstance = null;
                    }
                }
            }
            catch (Exception ex)
            {
                BetterMultiplayer.Instance.LogError("Error in UpdateRemoteShadeSpawning: " + ex);
            }
        }

        private static void SpawnRemoteShade()
        {
            try
            {
                BetterMultiplayer.Instance.Log($"[ShadeSync] Spawning remote shade at ({RemoteShadeX}, {RemoteShadeY})");

                var sm = UnityEngine.Object.FindObjectOfType<SceneManager>();
                if (sm == null)
                {
                    BetterMultiplayer.Instance.LogError("Cannot spawn remote shade: SceneManager not found in scene.");
                    return;
                }

                GameObject prefab = sm.hollowShadeObject;
                if (prefab == null)
                {
                    BetterMultiplayer.Instance.LogError("Cannot spawn remote shade: hollowShadeObject prefab in SceneManager is null.");
                    return;
                }

                RemoteShadeInstance = UnityEngine.Object.Instantiate(prefab, new Vector3(RemoteShadeX, RemoteShadeY, 0f), Quaternion.identity);
                RemoteShadeInstance.name = "Remote_Hollow Shade";

                foreach (var fsm in RemoteShadeInstance.GetComponents<PlayMakerFSM>())
                {
                    foreach (var state in fsm.FsmStates)
                    {
                        var actionList = new List<FsmStateAction>(state.Actions);
                        for (int i = actionList.Count - 1; i >= 0; i--)
                        {
                            var action = actionList[i];
                            if (action != null)
                            {
                                string typeName = action.GetType().Name;
                                if (typeName.Contains("PlayerData"))
                                {
                                    actionList.RemoveAt(i);
                                }
                            }
                        }
                        state.Actions = actionList.ToArray();
                    }
                }

                HealthManager hm = RemoteShadeInstance.GetComponent<HealthManager>();
                if (hm != null)
                {
                    string id = $"{RemoteShadeScene}@Remote_Hollow Shade@{RemoteShadeX.ToString("F1", CultureInfo.InvariantCulture)}@{RemoteShadeY.ToString("F1", CultureInfo.InvariantCulture)}";
                    enemyIds[hm] = id;
                    BetterMultiplayer.Instance.Log($"Registered remote shade HealthManager with ID: {id}");
                }
                else
                {
                    BetterMultiplayer.Instance.LogError("Spawned remote shade but it has no HealthManager component.");
                }
            }
            catch (Exception ex)
            {
                BetterMultiplayer.Instance.LogError("Error spawning remote shade: " + ex);
            }
        }

        public static void RecoverLocalShade()
        {
            try
            {
                if (PlayerData.instance == null) return;

                int geoToRestore = PlayerData.instance.GetInt("geoPool");
                BetterMultiplayer.Instance.Log($"[ShadeSync] Recovering local shade. Restoring {geoToRestore} Geo.");

                if (geoToRestore > 0)
                {
                    int currentGeo = PlayerData.instance.GetInt("geo");
                    PlayerData.instance.SetInt("geo", currentGeo + geoToRestore);
                    PlayerData.instance.SetInt("geoPool", 0);
                }

                PlayerData.instance.SetBool("soulLimited", false);
                PlayerData.instance.SetString("shadeScene", "None");

                if (HeroController.instance != null)
                {
                    HeroController.instance.CharmUpdate();
                    HeroController.instance.MaxHealthKeepBlue();
                }

                PlayMakerFSM.BroadcastEvent("CHARM UPDATE");
                PlayMakerFSM.BroadcastEvent("MAX HEALTH UPDATE");
            }
            catch (Exception ex)
            {
                BetterMultiplayer.Instance.LogError("Error in RecoverLocalShade: " + ex);
            }
        }

        public static void OnHealthManagerStart(HealthManager self)
        {
            try
            {
                // Assign a unique ID based on active scene name, object name, and initial position
                string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
                string id = $"{sceneName}@{self.gameObject.name}@{self.transform.position.x.ToString("F1", CultureInfo.InvariantCulture)}@{self.transform.position.y.ToString("F1", CultureInfo.InvariantCulture)}";
                
                enemyIds[self] = id;
            }
            catch (Exception ex)
            {
                BetterMultiplayer.Instance.LogError("Error in OnHealthManagerStart: " + ex);
            }
        }

        public static void OnHealthManagerHit(HealthManager self, HitInstance hitInstance)
        {
            if (isSyncingEnemy) return;

            try
            {
                string id;
                if (enemyIds.TryGetValue(self, out id))
                {
                    string attackTypeVal = ((int)hitInstance.AttackType).ToString();
                    string damageVal = hitInstance.DamageDealt.ToString();
                    string directionVal = hitInstance.Direction.ToString("F3", CultureInfo.InvariantCulture);

                    NetworkManager.SendPacket($"HIT|{id}|{damageVal}|{attackTypeVal}|{directionVal}");
                }
            }
            catch (Exception ex)
            {
                BetterMultiplayer.Instance.LogError("Error in OnHealthManagerHit: " + ex);
            }
        }

        public static void OnHealthManagerDie(HealthManager self, float? attackDirection, AttackTypes attackType, bool ignoreEvasion)
        {
            if (isSyncingEnemy) return;

            try
            {
                string id;
                if (enemyIds.TryGetValue(self, out id))
                {
                    string dirVal = attackDirection.HasValue ? attackDirection.Value.ToString("F3", CultureInfo.InvariantCulture) : "null";
                    string attackTypeVal = ((int)attackType).ToString();
                    string ignoreEvasionVal = ignoreEvasion.ToString();

                    NetworkManager.SendPacket($"DIE|{id}|{dirVal}|{attackTypeVal}|{ignoreEvasionVal}");
                }
            }
            catch (Exception ex)
            {
                BetterMultiplayer.Instance.LogError("Error in OnHealthManagerDie: " + ex);
            }
        }

        public static void ApplyNetworkHit(string enemyId, int damage, AttackTypes attackType, float direction)
        {
            try
            {
                HealthManager enemy = FindEnemy(enemyId);
                if (enemy != null && enemy.hp > 0)
                {
                    isSyncingEnemy = true;
                    
                    HitInstance hit = new HitInstance
                    {
                        DamageDealt = damage,
                        AttackType = attackType,
                        Direction = direction,
                        Source = HeroController.instance != null ? HeroController.instance.gameObject : null
                    };

                    enemy.Hit(hit);
                }
            }
            catch (Exception ex)
            {
                BetterMultiplayer.Instance.LogError("Error in ApplyNetworkHit: " + ex);
            }
            finally
            {
                isSyncingEnemy = false;
            }
        }

        public static void ApplyNetworkDie(string enemyId, float? attackDirection, AttackTypes attackType, bool ignoreEvasion)
        {
            try
            {
                HealthManager enemy = FindEnemy(enemyId);
                if (enemy != null && enemy.hp > 0)
                {
                    isSyncingEnemy = true;
                    enemy.Die(attackDirection, attackType, ignoreEvasion);
                }

                // If it is the Hollow Shade that died, ensure local player recovers their Shade state
                if (enemyId.Contains("Hollow Shade"))
                {
                    RecoverLocalShade();
                }
            }
            catch (Exception ex)
            {
                BetterMultiplayer.Instance.LogError("Error in ApplyNetworkDie: " + ex);
            }
            finally
            {
                isSyncingEnemy = false;
            }
        }

        public static void SendAllEnemyHp()
        {
            try
            {
                var list = new List<KeyValuePair<HealthManager, string>>(enemyIds);
                foreach (var entry in list)
                {
                    HealthManager enemy = entry.Key;
                    string id = entry.Value;
                    
                    if (enemy == null)
                    {
                        // Enemy has been destroyed (meaning it is dead)
                        NetworkManager.SendPacket($"SYNCHP|{id}|0");
                    }
                    else
                    {
                        NetworkManager.SendPacket($"SYNCHP|{id}|{enemy.hp}");
                    }
                }
            }
            catch (Exception ex)
            {
                BetterMultiplayer.Instance.LogError("Error in SendAllEnemyHp: " + ex);
            }
        }

        public static void ApplyNetworkHpSync(string enemyId, int hp)
        {
            try
            {
                HealthManager enemy = FindEnemy(enemyId);
                if (enemy != null)
                {
                    if (hp <= 0)
                    {
                        if (enemy.hp > 0)
                        {
                            isSyncingEnemy = true;
                            BetterMultiplayer.Instance.Log($"[EnemySync] Syncing death for {enemyId} (HP is 0)");
                            enemy.Die(null, AttackTypes.Nail, true);
                        }
                    }
                    else
                    {
                        if (enemy.hp != hp)
                        {
                            BetterMultiplayer.Instance.Log($"[EnemySync] Syncing HP for {enemyId} to {hp}");
                            enemy.hp = hp;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                BetterMultiplayer.Instance.LogError("Error in ApplyNetworkHpSync: " + ex);
            }
            finally
            {
                isSyncingEnemy = false;
            }
        }

        private static HealthManager FindEnemy(string enemyId)
        {
            // First, try exact cache match
            foreach (var entry in enemyIds)
            {
                if (entry.Value == enemyId)
                {
                    return entry.Key;
                }
            }

            // Parse ID parts
            // Format: sceneName@enemyName@x@y
            string[] parts = enemyId.Split('@');
            if (parts.Length < 4) return null;

            string targetScene = parts[0];
            string targetXStr = parts[parts.Length - 2];
            string targetYStr = parts[parts.Length - 1];

            // Join midparts in case name contains @
            string targetName = string.Join("@", parts, 1, parts.Length - 3);

            float targetX = float.Parse(targetXStr, CultureInfo.InvariantCulture);
            float targetY = float.Parse(targetYStr, CultureInfo.InvariantCulture);

            var activeSceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (activeSceneName != targetScene) return null;

            HealthManager closestEnemy = null;
            float minDistance = float.MaxValue;

            // Search in scene for the closest enemy matching the name within range
            foreach (var hm in UnityEngine.Object.FindObjectsOfType<HealthManager>())
            {
                if (hm == null || hm.gameObject == null) continue;

                string hmName = hm.gameObject.name.Replace("(Clone)", "").Trim();
                string cleanTargetName = targetName.Replace("(Clone)", "").Trim();

                if (hmName == cleanTargetName || hm.gameObject.name.Contains(cleanTargetName) || cleanTargetName.Contains(hmName))
                {
                    float dist = Vector2.Distance(new Vector2(hm.transform.position.x, hm.transform.position.y), new Vector2(targetX, targetY));
                    if (dist < minDistance && dist < 5.0f) // Within 5.0 units range
                    {
                        minDistance = dist;
                        closestEnemy = hm;
                    }
                }
            }

            if (closestEnemy != null)
            {
                enemyIds[closestEnemy] = enemyId;
                return closestEnemy;
            }

            return null;
        }
    }

    [HarmonyPatch(typeof(HealthManager), "Start")]
    public static class HealthManager_Start_Patch
    {
        public static void Postfix(HealthManager __instance)
        {
            EnemySync.OnHealthManagerStart(__instance);
        }
    }

    [HarmonyPatch(typeof(HealthManager), nameof(HealthManager.Hit))]
    public static class HealthManager_Hit_Patch
    {
        public static void Postfix(HealthManager __instance, HitInstance hitInstance)
        {
            EnemySync.OnHealthManagerHit(__instance, hitInstance);
        }
    }

    [HarmonyPatch(typeof(HealthManager), nameof(HealthManager.Die))]
    public static class HealthManager_Die_Patch
    {
        public static void Postfix(HealthManager __instance, float? attackDirection, AttackTypes attackType, bool ignoreEvasion)
        {
            EnemySync.OnHealthManagerDie(__instance, attackDirection, attackType, ignoreEvasion);
        }
    }
}
