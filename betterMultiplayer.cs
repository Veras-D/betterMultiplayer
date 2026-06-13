using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.Globalization;
using UnityEngine;
using InControl;


namespace BetterMultiplayer
{
    [BepInPlugin("com.antigravity.bettermultiplayer", "betterMultiplayer", "1.0.0")]
    public class BetterMultiplayer : BaseUnityPlugin
    {
        public static BetterMultiplayer Instance { get; private set; }
        public static ConfigEntry<string> ActiveSkinSetting;
        public static ConfigEntry<KeyCode> MenuToggleKeySetting;

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

                ActiveSkinSetting = Config.Bind(
                    "Skins",
                    "ActiveSkin",
                    "Default",
                    "The custom skin to load automatically on startup."
                );

                MenuToggleKeySetting = Config.Bind(
                    "Settings",
                    "MenuToggleKey",
                    KeyCode.F10,
                    "The key used to toggle the mod menu visibility."
                );

                if (ActiveSkinSetting.Value != "Default")
                {
                    SkinManager.ApplyLocalSkin(ActiveSkinSetting.Value);
                }

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
                    managerGo.AddComponent<PartnerHealthDisplay>();
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
        private bool wasControlRelinquished = false;

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
            wasMenuShownLastFrame = false;
        }

        void OnDestroy()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnMenuSceneLoaded;
            if (wasControlRelinquished && HeroController.instance != null)
            {
                try
                {
                    HeroController.instance.RegainControl();
                }
                catch {}
            }
        }

        private void OnMenuSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            BetterMultiplayer.Instance.Log($"OnMenuSceneLoaded triggered: Scene name is '{scene.name}'");
            localAnimatorCache = null;
            wasMenuShownLastFrame = false;
            // Auto-show menu on Title screen so players can configure it easily
            if (scene.name == "Menu_Title")
            {
                showMenu = true;
                BetterMultiplayer.Instance.Log("Auto-showed menu on Menu_Title");
            }
        }

        private bool wasToggleKeyPressedLastFrame = false;

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private bool CheckInControlToggleKeyPressed()
        {
            var provider = InputManager.KeyboardProvider;
            if (provider == null) return false;

            Key targetKey = Key.F10;
            if (BetterMultiplayer.MenuToggleKeySetting != null)
            {
                var configuredKey = BetterMultiplayer.MenuToggleKeySetting.Value;
                if (Enum.TryParse<Key>(configuredKey.ToString(), true, out var parsedKey))
                {
                    targetKey = parsedKey;
                }
            }

            bool keyDown = provider.GetKeyIsPressed(targetKey);
            bool pressed = keyDown && !wasToggleKeyPressedLastFrame;
            wasToggleKeyPressedLastFrame = keyDown;
            return pressed;
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private bool CheckInControlBumpersPressed()
        {
            var device = InputManager.ActiveDevice;
            if (device == null) return false;

            return device.LeftBumper.IsPressed && device.RightBumper.IsPressed;
        }

        void Update()
        {
            bool guiHasKeyboard = GUIUtility.keyboardControl != 0;

            // Keyboard toggle fallback (using both legacy Input and InControl for cross-platform robustness)
            bool togglePressed = false;
            if (!guiHasKeyboard)
            {
                try
                {
                    if (BetterMultiplayer.MenuToggleKeySetting != null)
                    {
                        togglePressed = Input.GetKeyDown(BetterMultiplayer.MenuToggleKeySetting.Value);
                    }
                    else
                    {
                        togglePressed = Input.GetKeyDown(KeyCode.F10);
                    }
                }
                catch {}

                try
                {
                    if (CheckInControlToggleKeyPressed())
                    {
                        togglePressed = true;
                    }
                }
                catch {}
            }

            if (togglePressed)
            {
                showMenu = !showMenu;
                BetterMultiplayer.Instance.Log($"Toggled menu via key. showMenu is now: {showMenu}");
            }

            // Controller toggle: Hold LB + RB (joystick bumpers) for 1 second
            bool bumpersPressed = false;
            try
            {
                bumpersPressed = CheckInControlBumpersPressed();
            }
            catch {}

            if (!bumpersPressed)
            {
                try
                {
                    bumpersPressed = Input.GetKey(KeyCode.JoystickButton4) && Input.GetKey(KeyCode.JoystickButton5);
                }
                catch {}
            }

            if (bumpersPressed)
            {
                bumperPressTimer += Time.unscaledDeltaTime;
                if (bumperPressTimer >= 1.0f && !bumperToggled)
                {
                    showMenu = !showMenu;
                    bumperToggled = true;
                    BetterMultiplayer.Instance.Log($"Toggled menu via controller bumpers. showMenu is now: {showMenu}");
                }
            }
            else
            {
                bumperPressTimer = 0f;
                bumperToggled = false;
            }

            // Manage input and cursor state when menu is open
            if (showMenu)
            {
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;
                if (HeroController.instance != null && !wasControlRelinquished)
                {
                    try
                    {
                        HeroController.instance.RelinquishControl();
                        wasControlRelinquished = true;
                    }
                    catch {}
                }
            }
            else
            {
                if (wasControlRelinquished)
                {
                    Cursor.visible = false;
                    Cursor.lockState = CursorLockMode.Locked;
                    if (HeroController.instance != null)
                    {
                        try
                        {
                            HeroController.instance.RegainControl();
                            wasControlRelinquished = false;
                        }
                        catch {}
                    }
                }
            }

            // Periodically send position if connected
            if (NetworkManager.IsClientConnected && HeroController.instance != null)
            {
                if (Time.unscaledTime - lastSendTime >= SendInterval)
                {
                    lastSendTime = Time.unscaledTime;
                    SendPosition();
                }
            }

            // Poll for inventory/ability state changes
            ItemSync.Update();
        }

        void LateUpdate()
        {
            // Keep skins applied AFTER animations have completed for this frame
            SkinManager.UpdateSkins();
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

                // Force idle animation if in menus/paused
                bool isPaused = GameManager.instance != null && 
                               (GameManager.instance.IsGamePaused() || GameManager.instance.gameState != GlobalEnums.GameState.PLAYING);
                if (isPaused)
                {
                    animName = "idle";
                }

                float normX = 0f;
                float normY = 0f;
                if (GameManager.instance != null && GameManager.instance.sceneWidth > 0f && GameManager.instance.sceneHeight > 0f)
                {
                    normX = pos.x / GameManager.instance.sceneWidth;
                    normY = pos.y / GameManager.instance.sceneHeight;
                }

                int health = PlayerData.instance != null ? PlayerData.instance.health : 5;
                int maxHealth = PlayerData.instance != null ? PlayerData.instance.maxHealth : 5;
                int healthBlue = PlayerData.instance != null ? PlayerData.instance.healthBlue : 0;

                string packet = $"POS|{sceneName}|{pos.x.ToString("F3", CultureInfo.InvariantCulture)}|{pos.y.ToString("F3", CultureInfo.InvariantCulture)}|{scaleX.ToString("F2", CultureInfo.InvariantCulture)}|{animName}|{normX.ToString("F4", CultureInfo.InvariantCulture)}|{normY.ToString("F4", CultureInfo.InvariantCulture)}|{health}|{maxHealth}|{healthBlue}";
                NetworkManager.SendPacket(packet);
            }
            catch (Exception ex)
            {
                BetterMultiplayer.Instance.LogError("Error in SendPosition: " + ex);
            }
        }

        private Rect menuWindowRect = new Rect(10, 40, 260, 220);
        private bool wasMenuShownLastFrame = false;

        void OnGUI()
        {
            // Draw a permanent tiny "X" button at top-left (10, 10) to toggle menu visibility
            if (GUI.Button(new Rect(10, 10, 25, 25), "X"))
            {
                showMenu = !showMenu;
                BetterMultiplayer.Instance.Log($"Toggled menu via on-screen X button. showMenu is now: {showMenu}");
            }

            if (!showMenu)
            {
                wasMenuShownLastFrame = false;
                return;
            }

            int boxWidth = showSkinsMenu ? 500 : 260;
            int boxHeight = showSkinsMenu ? 340 : 220;
            menuWindowRect.x = 10;
            menuWindowRect.y = 40;
            menuWindowRect.width = boxWidth;
            menuWindowRect.height = boxHeight;

            bool menuOpenedThisFrame = !wasMenuShownLastFrame;
            wasMenuShownLastFrame = true;

            string toggleKeyName = BetterMultiplayer.MenuToggleKeySetting != null ? BetterMultiplayer.MenuToggleKeySetting.Value.ToString() : "F10";
            string windowTitle = $"betterMultiplayer ({toggleKeyName} / Hold LB+RB)";

            // Use GUI.skin.box as the style to render a solid, interactable box background that handles mouse events.
            // Pass the title directly to the window call.
            menuWindowRect = GUI.Window(10985, menuWindowRect, DrawMenuWindow, windowTitle, GUI.skin.box);

            // Focus and bring window to front to prevent races on open
            if (menuOpenedThisFrame)
            {
                GUI.FocusWindow(10985);
                GUI.BringWindowToFront(10985);
                GUI.FocusControl("IPField");
            }

            // Refocus and bring to front if clicked inside window bounds (from OnGUI)
            if (Event.current != null && Event.current.type == EventType.MouseDown && menuWindowRect.Contains(Event.current.mousePosition))
            {
                GUI.FocusWindow(10985);
                GUI.BringWindowToFront(10985);
            }
        }

        void DrawMenuWindow(int windowID)
        {
            int boxWidth = showSkinsMenu ? 500 : 260;
            int boxHeight = showSkinsMenu ? 340 : 220;

            if (showSkinsMenu)
            {
                GUI.Label(new Rect(10, 30, 220, 25), "Select Active Skin:");
                
                int scrollY = 60;
                int scrollHeight = 210;
                if (!string.IsNullOrEmpty(skinWarning))
                {
                    GUI.color = Color.red;
                    GUI.Label(new Rect(10, 55, 220, 25), skinWarning);
                    GUI.color = Color.white;
                    scrollY = 80;
                    scrollHeight = 190;
                }
                
                var skins = SkinManager.GetAvailableSkins();
                skinsScrollPosition = GUI.BeginScrollView(
                    new Rect(10, scrollY, 220, scrollHeight), 
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
                            if (BetterMultiplayer.ActiveSkinSetting != null)
                            {
                                BetterMultiplayer.ActiveSkinSetting.Value = skinName;
                            }
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
                GUI.Box(new Rect(250, 30, 220, 240), "Skin Preview");
                if (highlightedPreview != null)
                {
                    GUI.DrawTexture(new Rect(260, 60, 200, 130), highlightedPreview, ScaleMode.ScaleToFit);
                }
                else
                {
                    GUI.Label(new Rect(260, 110, 200, 30), "No Preview Available");
                }

                // Draw Metadata
                GUI.Label(new Rect(260, 200, 200, 20), "Author: " + highlightedAuthor);
                GUI.Label(new Rect(260, 220, 200, 45), "Description: " + highlightedDesc);
            }
            else if (NetworkManager.IsClientConnected)
            {
                GUI.Label(new Rect(10, 30, 220, 25), "Status: " + (NetworkManager.IsServer ? "Hosting (Connected)" : "Connected to Host"));
                GUI.Label(new Rect(10, 55, 220, 25), "Partner Location: " + NetworkManager.RemoteSceneName);
                if (GUI.Button(new Rect(10, 85, 220, 30), "Sync My Items to Partner"))
                {
                    ItemSync.SendAllItems();
                }
                if (GUI.Button(new Rect(10, 125, 220, 30), "Disconnect"))
                {
                    NetworkManager.Stop();
                }
            }
            else if (NetworkManager.IsServer)
            {
                GUI.Label(new Rect(10, 30, 220, 30), "Status: Hosting (Waiting...)");
                if (GUI.Button(new Rect(10, 70, 220, 30), "Stop Server"))
                {
                    NetworkManager.Stop();
                }
            }
            else
            {
                if (GUI.Button(new Rect(10, 30, 105, 30), "Host Server"))
                {
                    NetworkManager.StartServer(10985);
                }

                GUI.Label(new Rect(10, 70, 60, 30), "Host IP:");
                GUI.SetNextControlName("IPField");
                ipAddress = GUI.TextField(new Rect(70, 70, 160, 30), ipAddress);

                if (GUI.Button(new Rect(10, 110, 220, 30), "Connect to Host"))
                {
                    NetworkManager.Connect(ipAddress, 10985);
                }
            }

            // Select Skin button at the bottom of the menu
            if (GUI.Button(new Rect(showSkinsMenu ? 130 : 10, boxHeight - 55, 220, 30), showSkinsMenu ? "Back to Main Menu" : "Select Skin..."))
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
