using System;
using System.Globalization;
using UnityEngine;

namespace BetterMultiplayer
{
    public static class PacketHandler
    {
        public static void HandlePacket(string packet)
        {
            try
            {
                string[] parts = packet.Split('|');
                if (parts.Length == 0) return;

                string header = parts[0];
                if (header == "POS" && parts.Length >= 6)
                {
                    string remoteScene = parts[1];
                    float x = float.Parse(parts[2], CultureInfo.InvariantCulture);
                    float y = float.Parse(parts[3], CultureInfo.InvariantCulture);
                    float scaleX = float.Parse(parts[4], CultureInfo.InvariantCulture);
                    string animName = parts[5];

                    float normX = 0f;
                    float normY = 0f;
                    if (parts.Length >= 8)
                    {
                        normX = float.Parse(parts[6], CultureInfo.InvariantCulture);
                        normY = float.Parse(parts[7], CultureInfo.InvariantCulture);
                    }

                    int remoteHealth = 5;
                    int remoteMaxHealth = 5;
                    int remoteHealthBlue = 0;
                    if (parts.Length >= 11)
                    {
                        remoteHealth = int.Parse(parts[8]);
                        remoteMaxHealth = int.Parse(parts[9]);
                        remoteHealthBlue = int.Parse(parts[10]);
                    }

                    string localScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
                    bool partnerJustEntered = (remoteScene == localScene) && (NetworkManager.RemoteSceneName != localScene);

                    NetworkManager.RemoteSceneName = remoteScene;
                    NetworkManager.RemoteNormX = normX;
                    NetworkManager.RemoteNormY = normY;
                    NetworkManager.RemoteHealth = remoteHealth;
                    NetworkManager.RemoteMaxHealth = remoteMaxHealth;
                    NetworkManager.RemoteHealthBlue = remoteHealthBlue;

                    if (partnerJustEntered)
                    {
                        BetterMultiplayer.Instance.Log($"Partner entered our scene ({localScene}). Sending enemy HP states...");
                        EnemySync.SendAllEnemyHp();
                    }

                    if (remoteScene == localScene)
                    {
                        if (NetworkManager.puppet == null && HeroController.instance != null)
                        {
                            NetworkManager.puppet = RemotePlayerPuppet.CreatePuppet(NetworkManager.IsServer ? "Peer" : "Host");
                        }

                        if (NetworkManager.puppet != null)
                        {
                            var puppetCtrl = NetworkManager.puppet.GetComponent<RemotePlayerPuppet>();
                            if (puppetCtrl != null)
                            {
                                puppetCtrl.UpdatePosition(x, y, scaleX, animName);
                            }
                        }
                    }
                    else
                    {
                        if (NetworkManager.puppet != null)
                        {
                            UnityEngine.Object.Destroy(NetworkManager.puppet);
                            NetworkManager.puppet = null;
                        }
                    }
                }
                else if (header == "ITEM" && parts.Length == 4)
                {
                    string type = parts[1];
                    string key = parts[2];
                    string val = parts[3];

                    ItemSync.ApplyNetworkChange(type, key, val);
                }
                else if (header == "SYNCHP" && parts.Length == 3)
                {
                    string enemyId = parts[1];
                    int hp = int.Parse(parts[2]);

                    EnemySync.ApplyNetworkHpSync(enemyId, hp);
                }
                else if (header == "HIT" && parts.Length == 5)
                {
                    string enemyId = parts[1];
                    int damage = int.Parse(parts[2]);
                    AttackTypes attackType = (AttackTypes)int.Parse(parts[3]);
                    float direction = float.Parse(parts[4], CultureInfo.InvariantCulture);

                    EnemySync.ApplyNetworkHit(enemyId, damage, attackType, direction);
                }
                else if (header == "DIE" && parts.Length == 5)
                {
                    string enemyId = parts[1];
                    float? attackDirection = parts[2] == "null" ? (float?)null : float.Parse(parts[2], CultureInfo.InvariantCulture);
                    AttackTypes attackType = (AttackTypes)int.Parse(parts[3]);
                    bool ignoreEvasion = bool.Parse(parts[4]);

                    EnemySync.ApplyNetworkDie(enemyId, attackDirection, attackType, ignoreEvasion);
                }
                else if (header == "SKIN" && parts.Length == 2)
                {
                    string skinName = parts[1];
                    SkinManager.ApplyRemoteSkin(skinName);
                }
            }
            catch (Exception ex)
            {
                BetterMultiplayer.Instance.LogError("Error handling packet: " + ex);
            }
        }
    }
}
