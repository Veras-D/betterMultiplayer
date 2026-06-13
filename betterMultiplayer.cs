using BepInEx;
using HarmonyLib;
using System;
using System.Globalization;
using UnityEngine;

namespace BetterMultiplayer
{
    [BepInPlugin("com.antigravity.bettermultiplayer", "betterMultiplayer", "1.0.0")]
    public class BetterMultiplayer : BaseUnityPlugin
    {
        public static BetterMultiplayer Instance { get; private set; }

        private static GameObject managerGo;

        void Awake()
        {
            Instance = this;
            Log("Initializing betterMultiplayer BepInEx plugin...");

            try
            {
                // Initialize sync scripts
                ItemSync.Initialize();
                EnemySync.Initialize();
                SkinManager.Initialize();

                // Apply Harmony patches
                var harmony = new Harmony("com.antigravity.bettermultiplayer");
                harmony.PatchAll();

                // Listen for scene transitions using Unity's native SceneManager
                UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;

                Log("betterMultiplayer BepInEx plugin initialized successfully!");
            }
            catch (Exception ex)
            {
                LogError("Error initializing betterMultiplayer: " + ex);
            }
        }

        private void OnDestroy()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            if (managerGo == null)
            {
                try
                {
                    Log("Spawning betterMultiplayerManager GameObject in scene: " + scene.name);
                    managerGo = new GameObject("betterMultiplayerManager");
                    managerGo.AddComponent<MainThreadDispatcher>();
                    managerGo.AddComponent<BetterMultiplayerMenu>();
                    UnityEngine.Object.DontDestroyOnLoad(managerGo);
                }
                catch (Exception ex)
                {
                    LogError("Error spawning manager GameObject: " + ex);
                }
            }
        }

        public void Log(string msg)
        {
            Logger.LogInfo(msg);
        }

        public void LogError(string msg)
        {
            Logger.LogError(msg);
        }
    }

    public class BetterMultiplayerMenu : MonoBehaviour
    {
        private string ipAddress = "192.168.0.101";
        private bool showMenu = true;
        private float lastSendTime = 0f;
        private const float SendInterval = 0.033f; // ~30 times a second

        // Controller toggle variables
        private float bumperPressTimer = 0f;
        private bool bumperToggled = false;

        // Skins menu variables
        private bool showSkinsMenu = false;
        private Vector2 skinsScrollPosition = Vector2.zero;
        private string skinWarning = "";

        private string highlightedSkin = "";
        private Texture2D highlightedPreview = null;
        private string highlightedAuthor = "";
        private string highlightedDesc = "";

        private tk2dSpriteAnimator localAnimatorCache;

        private void HighlightSkin(string skinName)
        {
            if (highlightedSkin == skinName) return;

            if (highlightedPreview != null)
            {
                UnityEngine.Object.Destroy(highlightedPreview);
                highlightedPreview = null;
            }

            highlightedSkin = skinName;
            SkinManager.LoadSkinDetails(skinName, out highlightedPreview, out highlightedAuthor, out highlightedDesc);
        }

        private void ClearSkinsMenu()
        {
            showSkinsMenu = false;
            highlightedSkin = "";
            if (highlightedPreview != null)
            {
                UnityEngine.Object.Destroy(highlightedPreview);
                highlightedPreview = null;
            }
        }

        void Start()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnMenuSceneLoaded;
        }

        void OnDestroy()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnMenuSceneLoaded;
        }

        private void OnMenuSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            localAnimatorCache = null;
            // Auto-show menu on Title screen so players can configure it easily
            if (scene.name == "Menu_Title")
            {
                showMenu = true;
            }
        }

        void Update()
        {
            // Keyboard toggle fallback
            if (Input.GetKeyDown(KeyCode.F10))
            {
                showMenu = !showMenu;
            }

            // Controller toggle: Hold LB + RB (joystick buttons 4 and 5) for 1 second
            bool bumpersPressed = Input.GetKey(KeyCode.JoystickButton4) && Input.GetKey(KeyCode.JoystickButton5);
            if (bumpersPressed)
            {
                bumperPressTimer += Time.unscaledDeltaTime;
                if (bumperPressTimer >= 1.0f && !bumperToggled)
                {
                    showMenu = !showMenu;
                    bumperToggled = true;
                }
            }
            else
            {
                bumperPressTimer = 0f;
                bumperToggled = false;
            }

            // Periodically send position if connected
            if (NetworkManager.IsClientConnected && HeroController.instance != null)
            {
                if (Time.time - lastSendTime >= SendInterval)
                {
                    lastSendTime = Time.time;
                    SendPosition();
                }
            }

            // Keep skins applied
            SkinManager.UpdateSkins();

            // Poll for inventory/ability state changes
            ItemSync.Update();
        }

        private void SendPosition()
        {
            try
            {
                Vector3 pos = HeroController.instance.transform.position;
                float scaleX = HeroController.instance.transform.localScale.x;
                string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
                
                string animName = "";
                if (localAnimatorCache == null && HeroController.instance != null)
                {
                    localAnimatorCache = HeroController.instance.GetComponentInChildren<tk2dSpriteAnimator>();
                }
                if (localAnimatorCache != null && localAnimatorCache.CurrentClip != null)
                {
                    animName = localAnimatorCache.CurrentClip.name;
                }

                float normX = 0f;
                float normY = 0f;
                if (GameManager.instance != null && GameManager.instance.sceneWidth > 0f && GameManager.instance.sceneHeight > 0f)
                {
                    normX = pos.x / GameManager.instance.sceneWidth;
                    normY = pos.y / GameManager.instance.sceneHeight;
                }

                string packet = $"POS|{sceneName}|{pos.x.ToString("F3", CultureInfo.InvariantCulture)}|{pos.y.ToString("F3", CultureInfo.InvariantCulture)}|{scaleX.ToString("F2", CultureInfo.InvariantCulture)}|{animName}|{normX.ToString("F4", CultureInfo.InvariantCulture)}|{normY.ToString("F4", CultureInfo.InvariantCulture)}";
                NetworkManager.SendPacket(packet);
            }
            catch (Exception ex)
            {
                BetterMultiplayer.Instance.LogError("Error in SendPosition: " + ex);
            }
        }

        void OnGUI()
        {
            if (!showMenu) return;

            int boxWidth = showSkinsMenu ? 500 : 260;
            int boxHeight = showSkinsMenu ? 340 : 220;
            GUI.Box(new Rect(10, 10, boxWidth, boxHeight), "betterMultiplayer (F10 / Hold LB+RB)");

            if (showSkinsMenu)
            {
                GUI.Label(new Rect(20, 40, 220, 25), "Select Active Skin:");
                
                int scrollY = 70;
                int scrollHeight = 210;
                if (!string.IsNullOrEmpty(skinWarning))
                {
                    GUI.color = Color.red;
                    GUI.Label(new Rect(20, 65, 220, 25), skinWarning);
                    GUI.color = Color.white;
                    scrollY = 90;
                    scrollHeight = 190;
                }
                
                var skins = SkinManager.GetAvailableSkins();
                skinsScrollPosition = GUI.BeginScrollView(
                    new Rect(20, scrollY, 220, scrollHeight), 
                    skinsScrollPosition, 
                    new Rect(0, 0, 200, skins.Count * 35)
                );
                
                for (int i = 0; i < skins.Count; i++)
                {
                    string skinName = skins[i];
                    bool isSelected = (SkinManager.SelectedSkin == skinName);
                    string displayLabel = isSelected ? $"> {skinName} <" : skinName;
                    
                    if (highlightedSkin == skinName)
                    {
                        GUI.backgroundColor = Color.cyan;
                    }
                    else
                    {
                        GUI.backgroundColor = Color.white;
                    }

                    if (GUI.Button(new Rect(0, i * 35, 200, 30), displayLabel))
                    {
                        HighlightSkin(skinName);
                        if (SkinManager.ApplyLocalSkin(skinName))
                        {
                            skinWarning = "";
                        }
                        else
                        {
                            skinWarning = $"Missing Knight.png in {skinName}!";
                        }
                    }
                }
                GUI.backgroundColor = Color.white;
                GUI.EndScrollView();

                // Draw Preview on the right side
                GUI.Box(new Rect(260, 40, 220, 240), "Skin Preview");
                if (highlightedPreview != null)
                {
                    GUI.DrawTexture(new Rect(270, 70, 200, 130), highlightedPreview, ScaleMode.ScaleToFit);
                }
                else
                {
                    GUI.Label(new Rect(270, 120, 200, 30), "No Preview Available");
                }

                // Draw Metadata
                GUI.Label(new Rect(270, 210, 200, 20), "Author: " + highlightedAuthor);
                GUI.Label(new Rect(270, 230, 200, 45), "Description: " + highlightedDesc);
            }
            else if (NetworkManager.IsClientConnected)
            {
                GUI.Label(new Rect(20, 40, 220, 25), "Status: " + (NetworkManager.IsServer ? "Hosting (Connected)" : "Connected to Host"));
                GUI.Label(new Rect(20, 65, 220, 25), "Partner Location: " + NetworkManager.RemoteSceneName);
                if (GUI.Button(new Rect(20, 95, 220, 30), "Sync My Items to Partner"))
                {
                    ItemSync.SendAllItems();
                }
                if (GUI.Button(new Rect(20, 135, 220, 30), "Disconnect"))
                {
                    NetworkManager.Stop();
                }
            }
            else if (NetworkManager.IsServer)
            {
                GUI.Label(new Rect(20, 40, 220, 30), "Status: Hosting (Waiting...)");
                if (GUI.Button(new Rect(20, 80, 220, 30), "Stop Server"))
                {
                    NetworkManager.Stop();
                }
            }
            else
            {
                if (GUI.Button(new Rect(20, 40, 105, 30), "Host Server"))
                {
                    NetworkManager.StartServer(10985);
                }

                GUI.Label(new Rect(20, 80, 60, 30), "Host IP:");
                ipAddress = GUI.TextField(new Rect(80, 80, 160, 30), ipAddress);

                if (GUI.Button(new Rect(20, 120, 220, 30), "Connect to Host"))
                {
                    NetworkManager.Connect(ipAddress, 10985);
                }
            }

            // Select Skin button at the bottom of the menu
            if (GUI.Button(new Rect(showSkinsMenu ? 140 : 20, boxHeight - 45, 220, 30), showSkinsMenu ? "Back to Main Menu" : "Select Skin..."))
            {
                if (showSkinsMenu)
                {
                    ClearSkinsMenu();
                }
                else
                {
                    showSkinsMenu = true;
                    SkinManager.RefreshAvailableSkins();
                    HighlightSkin(SkinManager.SelectedSkin);
                }
            }
        }
    }

    [HarmonyPatch(typeof(GameMap), "Update")]
    public static class GameMap_Update_Patch
    {
        private static GameObject partnerMarker;

        public static void Postfix(GameMap __instance)
        {
            try
            {
                if (!NetworkManager.IsClientConnected)
                {
                    if (partnerMarker != null)
                    {
                        UnityEngine.Object.Destroy(partnerMarker);
                        partnerMarker = null;
                    }
                    return;
                }

                string remoteScene = NetworkManager.RemoteSceneName;
                if (string.IsNullOrEmpty(remoteScene) || remoteScene == "Unknown")
                {
                    if (partnerMarker != null && partnerMarker.activeSelf)
                    {
                        partnerMarker.SetActive(false);
                    }
                    return;
                }

                GameObject areaGo;
                GameObject sceneGo = FindSceneGameObject(__instance, remoteScene, out areaGo);
                if (sceneGo == null || areaGo == null)
                {
                    if (partnerMarker != null && partnerMarker.activeSelf)
                    {
                        partnerMarker.SetActive(false);
                    }
                    return;
                }

                if (partnerMarker == null && __instance.compassIcon != null)
                {
                    partnerMarker = UnityEngine.Object.Instantiate(__instance.compassIcon, __instance.compassIcon.transform.parent);
                    
                    // Set partner color to Cyan (0.2, 0.8, 1.0)
                    var sr = partnerMarker.GetComponent<SpriteRenderer>();
                    if (sr != null)
                    {
                        sr.color = new Color(0.2f, 0.8f, 1f);
                    }
                    else
                    {
                        var tk2d = partnerMarker.GetComponent("tk2dBaseSprite");
                        if (tk2d != null)
                        {
                            var colorProp = tk2d.GetType().GetProperty("color");
                            if (colorProp != null)
                            {
                                colorProp.SetValue(tk2d, new Color(0.2f, 0.8f, 1f), null);
                            }
                        }
                    }
                }

                if (partnerMarker != null)
                {
                    partnerMarker.SetActive(__instance.compassIcon.activeSelf);

                    // Calculate position
                    Vector3 currentScenePos = sceneGo.transform.localPosition + areaGo.transform.localPosition;
                    
                    Vector2 boundsSize = Vector2.zero;
                    var sceneSr = sceneGo.GetComponent<SpriteRenderer>();
                    if (sceneSr != null && sceneSr.sprite != null)
                    {
                        boundsSize = sceneSr.sprite.bounds.size;
                    }

                    float normX = NetworkManager.RemoteNormX;
                    float normY = NetworkManager.RemoteNormY;

                    float localX = currentScenePos.x - boundsSize.x / 2f + normX * boundsSize.x;
                    float localY = currentScenePos.y - boundsSize.y / 2f + normY * boundsSize.y;

                    partnerMarker.transform.localPosition = new Vector3(localX, localY, -1.5f);
                }
            }
            catch (Exception ex)
            {
                BetterMultiplayer.Instance.LogError("Error in GameMap Update Patch: " + ex);
            }
        }

        private static GameObject FindSceneGameObject(GameMap map, string sceneName, out GameObject areaGo)
        {
            areaGo = null;
            if (string.IsNullOrEmpty(sceneName)) return null;

            GameObject[] areas = new GameObject[]
            {
                map.areaAncientBasin, map.areaCity, map.areaCliffs, map.areaCrossroads,
                map.areaCrystalPeak, map.areaDeepnest, map.areaFogCanyon, map.areaFungalWastes,
                map.areaGreenpath, map.areaKingdomsEdge, map.areaQueensGardens, map.areaRestingGrounds,
                map.areaDirtmouth, map.areaWaterways
            };

            foreach (var area in areas)
            {
                if (area == null) continue;
                Transform t = area.transform.Find(sceneName);
                if (t != null)
                {
                    areaGo = area;
                    return t.gameObject;
                }
            }
            return null;
        }

        public static void DestroyMarker()
        {
            if (partnerMarker != null)
            {
                UnityEngine.Object.Destroy(partnerMarker);
                partnerMarker = null;
            }
        }
    }

    [HarmonyPatch(typeof(GameMap), "OnDestroy")]
    public static class GameMap_OnDestroy_Patch
    {
        public static void Postfix()
        {
            GameMap_Update_Patch.DestroyMarker();
        }
    }
}
