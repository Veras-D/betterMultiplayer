using System;
using System.Collections.Generic;
using System.Globalization;
using HarmonyLib;
using UnityEngine;

namespace BetterMultiplayer
{
    public static class EnemySync
    {
        public static bool isSyncingEnemy = false;

        // Associate live HealthManagers with their unique ID.
        // Cleared on every scene transition to prevent memory leaks.
        private static readonly Dictionary<HealthManager, string> enemyIds = new Dictionary<HealthManager, string>();

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
            }
            catch (Exception ex)
            {
                BetterMultiplayer.Instance.LogError("Error clearing enemy IDs on scene change: " + ex);
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
