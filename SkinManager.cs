using System;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using UnityEngine;

namespace BetterMultiplayer
{
    public static class SkinManager
    {
        public static string SelectedSkin { get; private set; } = "Default";
        public static string RemoteSelectedSkin { get; private set; } = "Default";
        public static Texture2D LocalSkinTexture { get; private set; }
        public static Texture2D RemoteSkinTexture { get; private set; }
        public static Texture2D LocalCloakTexture { get; private set; }
        public static Texture2D LocalHUDTexture { get; private set; }
        public static Texture2D LocalVSTexture { get; private set; }
        public static Texture2D LocalWingsTexture { get; private set; }
        public static Texture2D LocalSprintTexture { get; private set; }
        public static Texture2D LocalVoidSpellsTexture { get; private set; }
        public static Texture2D LocalWraithsTexture { get; private set; }
        public static Texture2D LocalShriekTexture { get; private set; }
        public static Texture2D LocalOrbFullTexture { get; private set; }
        public static Texture2D LocalShadeTexture { get; private set; }
 
        public static Texture2D RemoteCloakTexture { get; private set; }
        public static Texture2D RemoteVSTexture { get; private set; }
        public static Texture2D RemoteWingsTexture { get; private set; }
        public static Texture2D RemoteSprintTexture { get; private set; }
        public static Texture2D RemoteVoidSpellsTexture { get; private set; }
        public static Texture2D RemoteWraithsTexture { get; private set; }
        public static Texture2D RemoteShriekTexture { get; private set; }
        public static Texture2D RemoteShadeTexture { get; private set; }

        private static string skinsDir;
        private static List<string> availableSkins = new List<string>();

        private static MaterialPropertyBlock localBlock;
        private static MaterialPropertyBlock remoteBlock;
        private static MaterialPropertyBlock cloakBlock;
        private static MaterialPropertyBlock remoteCloakBlock;

        private static Dictionary<string, Dictionary<string, Texture2D>> textureCache = new Dictionary<string, Dictionary<string, Texture2D>>();

        public static void Initialize()
        {
            try
            {
                string dllPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                string dllDir = Path.GetDirectoryName(dllPath);
                skinsDir = Path.Combine(dllDir, "betterMultiplayer/Skins");

                if (!Directory.Exists(skinsDir))
                {
                    Directory.CreateDirectory(skinsDir);
                }

                RefreshAvailableSkins();
            }
            catch (Exception ex)
            {
                BetterMultiplayer.Instance.LogError("Error in SkinManager.Initialize: " + ex);
            }
        }

        public static void RefreshAvailableSkins()
        {
            try
            {
                availableSkins.Clear();
                availableSkins.Add("Default");

                if (Directory.Exists(skinsDir))
                {
                    foreach (var dir in Directory.GetDirectories(skinsDir))
                    {
                        string name = Path.GetFileName(dir);
                        availableSkins.Add(name);
                    }
                }
            }
            catch (Exception ex)
            {
                BetterMultiplayer.Instance.LogError("Error in RefreshAvailableSkins: " + ex);
            }
        }

        public static List<string> GetAvailableSkins()
        {
            return availableSkins;
        }

        public static bool ApplyLocalSkin(string skinName)
        {
            try
            {
                Texture2D tex = null;
                if (skinName != "Default")
                {
                    tex = GetCachedTexture(skinName, "Knight.png");
                    if (tex == null) return false;
                }

                SelectedSkin = skinName;
                LocalSkinTexture = tex;

                // Load extra textures
                LocalCloakTexture = LoadExtraTexture(skinName, "Cloak.png");
                LocalHUDTexture = LoadExtraTexture(skinName, "HUD.png") ?? LoadExtraTexture(skinName, "Hud.png");
                LocalVSTexture = LoadExtraTexture(skinName, "VS.png");
                LocalWingsTexture = LoadExtraTexture(skinName, "Wings.png");
                LocalSprintTexture = LoadExtraTexture(skinName, "Sprint.png") ?? LoadExtraTexture(skinName, "sprint.png");
                LocalVoidSpellsTexture = LoadExtraTexture(skinName, "VoidSpells.png") ?? LoadExtraTexture(skinName, "voidSpells.png");
                LocalWraithsTexture = LoadExtraTexture(skinName, "Wraiths.png") ?? LoadExtraTexture(skinName, "wraiths.png");
                LocalShriekTexture = LoadExtraTexture(skinName, "Shriek.png") ?? LoadExtraTexture(skinName, "shriek.png");
                LocalOrbFullTexture = LoadExtraTexture(skinName, "OrbFull.png") ?? LoadExtraTexture(skinName, "orbFull.png");
                LocalShadeTexture = LoadExtraTexture(skinName, "Shade.png") ?? LoadExtraTexture(skinName, "shade.png");

                BetterMultiplayer.Instance.Log($"Successfully applied local skin: {skinName}");
                if (CharmIconList.Instance != null)
                {
                    ApplyCharmSkins(CharmIconList.Instance);
                }
                UpdateHUDSkin();
                ForceUpdateAllSprites();
                if (NetworkManager.IsClientConnected)
                {
                    NetworkManager.SendPacket($"SKIN|{skinName}");
                }
                return true;
            }
            catch (Exception ex)
            {
                BetterMultiplayer.Instance.LogError($"Error applying local skin {skinName}: " + ex);
            }
            return false;
        }

        public static void ApplyRemoteSkin(string skinName)
        {
            if (RemoteSelectedSkin == skinName) return;

            try
            {
                Texture2D tex = null;
                Texture2D cloak = null;
                Texture2D vs = null;
                Texture2D wings = null;
                Texture2D sprint = null;
                Texture2D voidSpells = null;
                Texture2D wraiths = null;
                Texture2D shriek = null;
                Texture2D shade = null;

                if (skinName != "Default")
                {
                    tex = GetCachedTexture(skinName, "Knight.png");
                    cloak = LoadExtraTexture(skinName, "Cloak.png");
                    vs = LoadExtraTexture(skinName, "VS.png");
                    wings = LoadExtraTexture(skinName, "Wings.png");
                    sprint = LoadExtraTexture(skinName, "Sprint.png") ?? LoadExtraTexture(skinName, "sprint.png");
                    voidSpells = LoadExtraTexture(skinName, "VoidSpells.png") ?? LoadExtraTexture(skinName, "voidSpells.png");
                    wraiths = LoadExtraTexture(skinName, "Wraiths.png") ?? LoadExtraTexture(skinName, "wraiths.png");
                    shriek = LoadExtraTexture(skinName, "Shriek.png") ?? LoadExtraTexture(skinName, "shriek.png");
                    shade = LoadExtraTexture(skinName, "Shade.png") ?? LoadExtraTexture(skinName, "shade.png");
                }

                RemoteSkinTexture = tex;
                RemoteCloakTexture = cloak;
                RemoteVSTexture = vs;
                RemoteWingsTexture = wings;
                RemoteSprintTexture = sprint;
                RemoteVoidSpellsTexture = voidSpells;
                RemoteWraithsTexture = wraiths;
                RemoteShriekTexture = shriek;
                RemoteShadeTexture = shade;

                RemoteSelectedSkin = skinName;

                BetterMultiplayer.Instance.Log($"Loaded skin for partner: {skinName}");
                ForceUpdateAllSprites();
            }
            catch (Exception ex)
            {
                BetterMultiplayer.Instance.LogError($"Error applying remote skin {skinName}: " + ex);
            }
        }

        private static string lastHUDScene = "";
        private static MeshRenderer localMeshRenderer;
        private static MeshRenderer remoteMeshRenderer;
        private static MeshRenderer localCloakRenderer;
        private static MeshRenderer remoteCloakRenderer;

        private static MeshRenderer GetLocalMeshRenderer()
        {
            if (localMeshRenderer == null && HeroController.instance != null)
            {
                Transform spriteTransform = HeroController.instance.transform.Find("Sprite");
                if (spriteTransform != null)
                {
                    localMeshRenderer = spriteTransform.GetComponent<MeshRenderer>();
                }
                else
                {
                    var sprite = HeroController.instance.GetComponentInChildren<tk2dSprite>();
                    if (sprite != null)
                    {
                        localMeshRenderer = sprite.GetComponent<MeshRenderer>();
                    }
                }
            }
            return localMeshRenderer;
        }

        private static MeshRenderer GetRemoteMeshRenderer()
        {
            if (remoteMeshRenderer == null && NetworkManager.puppet != null)
            {
                remoteMeshRenderer = NetworkManager.puppet.GetComponent<MeshRenderer>();
            }
            return remoteMeshRenderer;
        }

        private static MeshRenderer GetLocalCloakRenderer()
        {
            if (localCloakRenderer == null && HeroController.instance != null)
            {
                Transform cloakTransform = HeroController.instance.transform.Find("Cloak");
                if (cloakTransform != null)
                {
                    localCloakRenderer = cloakTransform.GetComponent<MeshRenderer>();
                }
            }
            return localCloakRenderer;
        }

        private static MeshRenderer GetRemoteCloakRenderer()
        {
            if (remoteCloakRenderer == null && NetworkManager.puppet != null)
            {
                Transform cloakTransform = NetworkManager.puppet.transform.Find("Cloak");
                if (cloakTransform != null)
                {
                    remoteCloakRenderer = cloakTransform.GetComponent<MeshRenderer>();
                }
            }
            return remoteCloakRenderer;
        }

        public static void UpdateSkins()
        {
            try
            {
                string activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
                if (activeScene != lastHUDScene)
                {
                    lastHUDScene = activeScene;
                    // Reset cached renderers on scene change
                    localMeshRenderer = null;
                    remoteMeshRenderer = null;
                    localCloakRenderer = null;
                    remoteCloakRenderer = null;
                    UpdateHUDSkin();
                }

                var lCloak = GetLocalCloakRenderer();
                if (lCloak != null && LocalCloakTexture != null)
                {
                    if (cloakBlock == null) cloakBlock = new MaterialPropertyBlock();
                    lCloak.GetPropertyBlock(cloakBlock);
                    if (cloakBlock.GetTexture("_MainTex") != LocalCloakTexture)
                    {
                        cloakBlock.SetTexture("_MainTex", LocalCloakTexture);
                        lCloak.SetPropertyBlock(cloakBlock);
                    }
                }

                var rCloak = GetRemoteCloakRenderer();
                if (rCloak != null && RemoteCloakTexture != null)
                {
                    if (remoteCloakBlock == null) remoteCloakBlock = new MaterialPropertyBlock();
                    rCloak.GetPropertyBlock(remoteCloakBlock);
                    if (remoteCloakBlock.GetTexture("_MainTex") != RemoteCloakTexture)
                    {
                        remoteCloakBlock.SetTexture("_MainTex", RemoteCloakTexture);
                        rCloak.SetPropertyBlock(remoteCloakBlock);
                    }
                }

                var lMesh = GetLocalMeshRenderer();
                if (lMesh != null)
                {
                    if (LocalSkinTexture != null)
                    {
                        if (localBlock == null) localBlock = new MaterialPropertyBlock();
                        lMesh.GetPropertyBlock(localBlock);
                        localBlock.SetTexture("_MainTex", LocalSkinTexture);
                        lMesh.SetPropertyBlock(localBlock);
                    }
                    else
                    {
                        lMesh.SetPropertyBlock(null);
                    }
                }

                var rMesh = GetRemoteMeshRenderer();
                if (rMesh != null)
                {
                    if (RemoteSkinTexture != null)
                    {
                        if (remoteBlock == null) remoteBlock = new MaterialPropertyBlock();
                        rMesh.GetPropertyBlock(remoteBlock);
                        remoteBlock.SetTexture("_MainTex", RemoteSkinTexture);
                        rMesh.SetPropertyBlock(remoteBlock);
                    }
                    else
                    {
                        rMesh.SetPropertyBlock(null);
                    }
                }
            }
            catch (Exception ex)
            {
                BetterMultiplayer.Instance.LogError("Error in UpdateSkins: " + ex);
            }
        }

        private static Texture2D LoadTexture(string filePath)
        {
            try
            {
                byte[] fileData = File.ReadAllBytes(filePath);
                Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (UnityEngine.ImageConversion.LoadImage(tex, fileData))
                {
                    tex.filterMode = FilterMode.Bilinear;
                    tex.anisoLevel = 4;
                    return tex;
                }
            }
            catch (Exception ex)
            {
                BetterMultiplayer.Instance.LogError("Error loading texture from file: " + ex);
            }
            return null;
        }

        public static void LoadSkinDetails(string skinName, out Texture2D previewTex, out string author, out string desc)
        {
            previewTex = null;
            author = "Unknown";
            desc = "";

            try
            {
                if (skinName == "Default")
                {
                    author = "Team Cherry";
                    desc = "Default Hollow Knight skin.";
                    
                    string currentDllPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                    string defaultPreviewPath = Path.Combine(Path.GetDirectoryName(currentDllPath), "betterMultiplayer/default_preview.png");
                    if (File.Exists(defaultPreviewPath))
                    {
                        previewTex = LoadTexture(defaultPreviewPath);
                    }
                    return;
                }

                string skinPath = Path.Combine(skinsDir, skinName);
                
                // Load metadata
                string metaPath = Path.Combine(skinPath, "metadata.json");
                if (File.Exists(metaPath))
                {
                    string json = File.ReadAllText(metaPath);
                    author = ParseJsonValue(json, "author");
                    desc = ParseJsonValue(json, "desc");
                }

                // Load preview image
                string previewPath = Path.Combine(skinPath, "preview.png");
                if (!File.Exists(previewPath))
                {
                    string iconPath = Path.Combine(skinPath, "icon.png");
                    if (File.Exists(iconPath))
                    {
                        previewPath = iconPath;
                    }
                    else
                    {
                        iconPath = Path.Combine(skinPath, "Icon.png");
                        if (File.Exists(iconPath))
                        {
                            previewPath = iconPath;
                        }
                    }
                }

                if (File.Exists(previewPath))
                {
                    previewTex = LoadTexture(previewPath);
                }
            }
            catch (Exception ex)
            {
                BetterMultiplayer.Instance.LogError($"Error loading details for skin {skinName}: " + ex);
            }
        }

        private static string ParseJsonValue(string json, string key)
        {
            try
            {
                string searchKey = $"\"{key}\":";
                int index = json.IndexOf(searchKey);
                if (index != -1)
                {
                    int start = json.IndexOf("\"", index + searchKey.Length);
                    if (start != -1)
                    {
                        int end = json.IndexOf("\"", start + 1);
                        if (end != -1)
                        {
                            return json.Substring(start + 1, end - start - 1);
                        }
                    }
                }
            }
            catch {}
            return "Unknown";
        }

        private static Texture2D GetCachedTexture(string skinName, string fileName)
        {
            if (skinName == "Default") return null;

            if (!textureCache.TryGetValue(skinName, out var skinCache))
            {
                skinCache = new Dictionary<string, Texture2D>();
                textureCache[skinName] = skinCache;
            }

            if (skinCache.TryGetValue(fileName, out var cachedTex))
            {
                return cachedTex;
            }

            try
            {
                string skinPath = Path.Combine(skinsDir, skinName);
                string fullPath = Path.Combine(skinPath, fileName);
                if (File.Exists(fullPath))
                {
                    Texture2D tex = LoadTexture(fullPath);
                    if (tex != null)
                    {
                        skinCache[fileName] = tex;
                        return tex;
                    }
                }
            }
            catch (Exception ex)
            {
                BetterMultiplayer.Instance.LogError($"Error loading texture {fileName} for skin {skinName}: {ex}");
            }

            return null;
        }

        private static Texture2D LoadExtraTexture(string skinName, string fileName)
        {
            return GetCachedTexture(skinName, fileName);
        }

        public static void ForceUpdateAllSprites()
        {
            try
            {
                foreach (var sprite in Resources.FindObjectsOfTypeAll<tk2dSprite>())
                {
                    if (sprite != null && sprite.gameObject != null)
                    {
                        tk2dSprite_Awake_Patch.Postfix(sprite);
                    }
                }
            }
            catch (Exception ex)
            {
                BetterMultiplayer.Instance.LogError("Error in ForceUpdateAllSprites: " + ex);
            }
        }
 
        public static void UpdateCloakSkin()
        {
            var lCloak = GetLocalCloakRenderer();
            if (lCloak != null && LocalCloakTexture != null)
            {
                if (cloakBlock == null) cloakBlock = new MaterialPropertyBlock();
                lCloak.GetPropertyBlock(cloakBlock);
                cloakBlock.SetTexture("_MainTex", LocalCloakTexture);
                lCloak.SetPropertyBlock(cloakBlock);
            }

            var rCloak = GetRemoteCloakRenderer();
            if (rCloak != null && RemoteCloakTexture != null)
            {
                if (remoteCloakBlock == null) remoteCloakBlock = new MaterialPropertyBlock();
                rCloak.GetPropertyBlock(remoteCloakBlock);
                remoteCloakBlock.SetTexture("_MainTex", RemoteCloakTexture);
                rCloak.SetPropertyBlock(remoteCloakBlock);
            }
        }
 
        public static void UpdateHUDSkin()
        {
            if (LocalHUDTexture == null && LocalOrbFullTexture == null) return;
 
            GameObject hudCanvas = GameObject.Find("Hud Canvas");
            if (hudCanvas != null)
            {
                ReskinHUDRecursive(hudCanvas.transform);
            }
        }

        private static void ReskinHUDRecursive(Transform parent)
        {
            if (parent == null) return;

            var sr = parent.GetComponent<SpriteRenderer>();
            if (sr != null && sr.sprite != null && sr.sprite.texture != null)
            {
                string texName = sr.sprite.texture.name;

                if (LocalHUDTexture != null && texName.IndexOf("hud", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    var block = new MaterialPropertyBlock();
                    sr.GetPropertyBlock(block);
                    block.SetTexture("_MainTex", LocalHUDTexture);
                    sr.SetPropertyBlock(block);
                }
                else if (LocalOrbFullTexture != null && (texName.IndexOf("orbfull", StringComparison.OrdinalIgnoreCase) >= 0 || texName.IndexOf("soul_orb", StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    var block = new MaterialPropertyBlock();
                    sr.GetPropertyBlock(block);
                    block.SetTexture("_MainTex", LocalOrbFullTexture);
                    sr.SetPropertyBlock(block);
                }
            }

            var tk2d = parent.GetComponent<tk2dSprite>();
            if (tk2d != null && tk2d.Collection != null)
            {
                string colName = tk2d.Collection.name;

                if (LocalHUDTexture != null && colName.IndexOf("hud", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    var renderer = tk2d.GetComponent<MeshRenderer>();
                    if (renderer != null)
                    {
                        var block = new MaterialPropertyBlock();
                        renderer.GetPropertyBlock(block);
                        block.SetTexture("_MainTex", LocalHUDTexture);
                        renderer.SetPropertyBlock(block);
                    }
                }
                else if (LocalOrbFullTexture != null && (colName.IndexOf("orbfull", StringComparison.OrdinalIgnoreCase) >= 0 || colName.IndexOf("soul_orb", StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    var renderer = tk2d.GetComponent<MeshRenderer>();
                    if (renderer != null)
                    {
                        var block = new MaterialPropertyBlock();
                        renderer.GetPropertyBlock(block);
                        block.SetTexture("_MainTex", LocalOrbFullTexture);
                        renderer.SetPropertyBlock(block);
                    }
                }
            }

            for (int i = 0; i < parent.childCount; i++)
            {
                ReskinHUDRecursive(parent.GetChild(i));
            }
        }

        private static Sprite[] originalSpriteList;
        private static Sprite originalUnbreakableHeart;
        private static Sprite originalUnbreakableGreed;
        private static Sprite originalUnbreakableStrength;
        private static Sprite originalGrimmchildLevel1;
        private static Sprite originalGrimmchildLevel2;
        private static Sprite originalGrimmchildLevel3;
        private static Sprite originalGrimmchildLevel4;
        private static Sprite originalNymmCharm;
        private static bool backupsSaved = false;

        public static void ApplyCharmSkins(CharmIconList list)
        {
            if (list == null) return;

            try
            {
                // 1. Save backups if not already saved
                if (!backupsSaved)
                {
                    if (list.spriteList != null)
                    {
                        originalSpriteList = (Sprite[])list.spriteList.Clone();
                    }
                    originalUnbreakableHeart = list.unbreakableHeart;
                    originalUnbreakableGreed = list.unbreakableGreed;
                    originalUnbreakableStrength = list.unbreakableStrength;
                    originalGrimmchildLevel1 = list.grimmchildLevel1;
                    originalGrimmchildLevel2 = list.grimmchildLevel2;
                    originalGrimmchildLevel3 = list.grimmchildLevel3;
                    originalGrimmchildLevel4 = list.grimmchildLevel4;
                    originalNymmCharm = list.nymmCharm;
                    backupsSaved = true;
                }

                // 2. Always restore vanilla backups first so we start with a clean slate
                if (originalSpriteList != null && list.spriteList != null)
                {
                    Array.Copy(originalSpriteList, list.spriteList, Math.Min(originalSpriteList.Length, list.spriteList.Length));
                }
                list.unbreakableHeart = originalUnbreakableHeart;
                list.unbreakableGreed = originalUnbreakableGreed;
                list.unbreakableStrength = originalUnbreakableStrength;
                list.grimmchildLevel1 = originalGrimmchildLevel1;
                list.grimmchildLevel2 = originalGrimmchildLevel2;
                list.grimmchildLevel3 = originalGrimmchildLevel3;
                list.grimmchildLevel4 = originalGrimmchildLevel4;
                list.nymmCharm = originalNymmCharm;

                if (SelectedSkin == "Default")
                {
                    return;
                }

                // 3. Scan directory and Charms/ subdirectory for custom charm textures
                string skinPath = Path.Combine(skinsDir, SelectedSkin);
                List<string> charmFiles = new List<string>();

                // Check skin root directory
                if (Directory.Exists(skinPath))
                {
                    foreach (var file in Directory.GetFiles(skinPath, "*.png"))
                    {
                        string name = Path.GetFileName(file);
                        if (name.StartsWith("Charm_", StringComparison.OrdinalIgnoreCase))
                        {
                            charmFiles.Add(file);
                        }
                    }

                    // Check charms subdirectory
                    string charmsSubDir = Path.Combine(skinPath, "Charms");
                    if (Directory.Exists(charmsSubDir))
                    {
                        foreach (var file in Directory.GetFiles(charmsSubDir, "*.png"))
                        {
                            string name = Path.GetFileName(file);
                            if (name.StartsWith("Charm_", StringComparison.OrdinalIgnoreCase))
                            {
                                charmFiles.Add(file);
                            }
                        }
                    }
                }

                // 4. Apply each custom charm sprite
                foreach (var file in charmFiles)
                {
                    string filename = Path.GetFileNameWithoutExtension(file); // e.g. "Charm_23_Unbreakable"
                    Texture2D tex = LoadTexture(file);
                    if (tex == null) continue;

                    Sprite customSprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);

                    string content = filename.Substring(6); // Remove "Charm_" prefix -> "23_Unbreakable"
                    
                    // Parse number part
                    int numEnd = 0;
                    while (numEnd < content.Length && char.IsDigit(content[numEnd]))
                    {
                        numEnd++;
                    }

                    if (numEnd > 0)
                    {
                        string idStr = content.Substring(0, numEnd);
                        int charmId = int.Parse(idStr);
                        string suffix = content.Substring(numEnd).Trim('_').ToLower();

                        // Map standard and special charm fields
                        if (charmId == 23)
                        {
                            if (suffix == "unbreakable")
                            {
                                list.unbreakableHeart = customSprite;
                            }
                            else
                            {
                                ApplyToSpriteList(list, 23, customSprite);
                            }
                        }
                        else if (charmId == 24)
                        {
                            if (suffix == "unbreakable")
                            {
                                list.unbreakableGreed = customSprite;
                            }
                            else
                            {
                                ApplyToSpriteList(list, 24, customSprite);
                            }
                        }
                        else if (charmId == 25)
                        {
                            if (suffix == "unbreakable")
                            {
                                list.unbreakableStrength = customSprite;
                            }
                            else
                            {
                                ApplyToSpriteList(list, 25, customSprite);
                            }
                        }
                        else if (charmId == 39)
                        {
                            list.nymmCharm = customSprite;
                            ApplyToSpriteList(list, 39, customSprite);
                        }
                        else if (charmId == 40)
                        {
                            if (suffix == "2" || suffix == "level2")
                            {
                                list.grimmchildLevel2 = customSprite;
                            }
                            else if (suffix == "3" || suffix == "level3")
                            {
                                list.grimmchildLevel3 = customSprite;
                            }
                            else if (suffix == "4" || suffix == "level4")
                            {
                                list.grimmchildLevel4 = customSprite;
                            }
                            else
                            {
                                list.grimmchildLevel1 = customSprite;
                                ApplyToSpriteList(list, 40, customSprite);
                            }
                        }
                        else
                        {
                            ApplyToSpriteList(list, charmId, customSprite);
                        }
                    }
                }

                BetterMultiplayer.Instance.Log($"Applied custom charm skins for skin: {SelectedSkin}");
            }
            catch (Exception ex)
            {
                BetterMultiplayer.Instance.LogError("Error in ApplyCharmSkins: " + ex);
            }
        }

        private static void ApplyToSpriteList(CharmIconList list, int id, Sprite customSprite)
        {
            if (list.spriteList == null) return;
            if (id >= 0 && id < list.spriteList.Length)
            {
                list.spriteList[id] = customSprite;
            }
            else if (id - 1 >= 0 && id - 1 < list.spriteList.Length)
            {
                list.spriteList[id - 1] = customSprite;
            }
        }
    }
 
    [HarmonyPatch(typeof(tk2dSprite), "Awake")]
    public static class tk2dSprite_Awake_Patch
    {
        private static MaterialPropertyBlock block;
 
        public static void Postfix(tk2dSprite __instance)
        {
            if (__instance == null || __instance.gameObject == null) return;
            
            try
            {
                string name = __instance.gameObject.name;
                bool isRemote = name.StartsWith("Remote_");
  
                if (isRemote)
                {
                    if (SkinManager.RemoteShadeTexture != null && name.Contains("Hollow Shade"))
                    {
                        ApplyTexture(__instance, SkinManager.RemoteShadeTexture);
                    }
                    else if (SkinManager.RemoteVSTexture != null && 
                        (name.Contains("Fireball") || name.Contains("Vengeful Spirit")))
                    {
                        ApplyTexture(__instance, SkinManager.RemoteVSTexture);
                    }
                    else if (SkinManager.RemoteWingsTexture != null && 
                             (name.Contains("dJumpWings") || name.Contains("Wings") || name.Contains("dJumpFlash")))
                    {
                        ApplyTexture(__instance, SkinManager.RemoteWingsTexture);
                    }
                    else if (SkinManager.RemoteSprintTexture != null && 
                             (name.StartsWith("Remote_Dash Effect") || name.StartsWith("Remote_sprint_effect")))
                    {
                        ApplyTexture(__instance, SkinManager.RemoteSprintTexture);
                    }
                    else if (SkinManager.RemoteVoidSpellsTexture != null && 
                             (name.Contains("Fireball2") || name.Contains("Shadow Soul") || name.Contains("Shade Soul")))
                    {
                        ApplyTexture(__instance, SkinManager.RemoteVoidSpellsTexture);
                    }
                    else if (SkinManager.RemoteWraithsTexture != null && 
                             (name.Contains("Howling Wraiths") || name.Contains("Scream") || name.Contains("Wraiths")))
                    {
                        ApplyTexture(__instance, SkinManager.RemoteWraithsTexture);
                    }
                    else if (SkinManager.RemoteShriekTexture != null && 
                             (name.Contains("Abyss Shriek") || name.Contains("Abyss Scream") || name.Contains("Shriek")))
                    {
                        ApplyTexture(__instance, SkinManager.RemoteShriekTexture);
                    }
                }
                else
                {
                    // Check if it is the local player main sprite
                    if (name == "Sprite" && __instance.transform.parent != null && __instance.transform.parent.gameObject == HeroController.instance?.gameObject)
                    {
                        if (SkinManager.LocalSkinTexture != null)
                        {
                            ApplyTexture(__instance, SkinManager.LocalSkinTexture);
                        }
                    }
                    // Check if it is local player cloak
                    else if (name == "Cloak" && __instance.transform.parent != null && __instance.transform.parent.gameObject == HeroController.instance?.gameObject)
                    {
                        if (SkinManager.LocalCloakTexture != null)
                        {
                            ApplyTexture(__instance, SkinManager.LocalCloakTexture);
                        }
                    }
                    // Check if it is remote puppet main sprite
                    else if (__instance.GetComponent<RemotePlayerPuppet>() != null)
                    {
                        if (SkinManager.RemoteSkinTexture != null)
                        {
                            ApplyTexture(__instance, SkinManager.RemoteSkinTexture);
                        }
                    }
                    // Check if it is remote puppet cloak
                    else if (name == "Cloak" && __instance.transform.parent != null && __instance.transform.parent.GetComponent<RemotePlayerPuppet>() != null)
                    {
                        if (SkinManager.RemoteCloakTexture != null)
                        {
                            ApplyTexture(__instance, SkinManager.RemoteCloakTexture);
                        }
                    }
                    // Local effects
                    else if (SkinManager.LocalShadeTexture != null && name.StartsWith("Hollow Shade"))
                    {
                        ApplyTexture(__instance, SkinManager.LocalShadeTexture);
                    }
                    else if (SkinManager.LocalVSTexture != null && 
                        (name.StartsWith("Fireball") || name.StartsWith("Vengeful Spirit")))
                    {
                        ApplyTexture(__instance, SkinManager.LocalVSTexture);
                    }
                    else if (SkinManager.LocalWingsTexture != null && 
                             (name.StartsWith("dJumpWings") || name.Contains("Wings") || name.StartsWith("dJumpFlash")))
                    {
                        ApplyTexture(__instance, SkinManager.LocalWingsTexture);
                    }
                    else if (SkinManager.LocalSprintTexture != null && 
                             (name.StartsWith("Dash Effect") || name.StartsWith("sprint_effect")))
                    {
                        ApplyTexture(__instance, SkinManager.LocalSprintTexture);
                    }
                    else if (SkinManager.LocalVoidSpellsTexture != null && 
                             (name.StartsWith("Fireball2") || name.StartsWith("Shadow Soul") || name.StartsWith("Shade Soul")))
                    {
                        ApplyTexture(__instance, SkinManager.LocalVoidSpellsTexture);
                    }
                    else if (SkinManager.LocalWraithsTexture != null && 
                             (name.StartsWith("Howling Wraiths") || name.Contains("Scream") || name.Contains("Wraiths")))
                    {
                        ApplyTexture(__instance, SkinManager.LocalWraithsTexture);
                    }
                    else if (SkinManager.LocalShriekTexture != null && 
                             (name.StartsWith("Abyss Shriek") || name.Contains("Abyss Scream") || name.Contains("Shriek")))
                    {
                        ApplyTexture(__instance, SkinManager.LocalShriekTexture);
                    }
                }
            }
            catch (Exception ex)
            {
                if (BetterMultiplayer.Instance != null)
                {
                    BetterMultiplayer.Instance.LogError("Error in tk2dSprite_Awake_Patch: " + ex);
                }
            }
        }
 
        private static void ApplyTexture(tk2dSprite sprite, Texture2D tex)
        {
            var renderer = sprite.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                if (block == null) block = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(block);
                block.SetTexture("_MainTex", tex);
                renderer.SetPropertyBlock(block);
            }
        }
    }

    [HarmonyPatch(typeof(tk2dBaseSprite), nameof(tk2dBaseSprite.SetSprite), new Type[] { typeof(int) })]
    public static class tk2dBaseSprite_SetSprite_Patch
    {
        public static void Postfix(tk2dBaseSprite __instance)
        {
            if (__instance is tk2dSprite sprite)
            {
                tk2dSprite_Awake_Patch.Postfix(sprite);
            }
        }
    }

    [HarmonyPatch(typeof(tk2dBaseSprite), nameof(tk2dBaseSprite.SetSprite), new Type[] { typeof(tk2dSpriteCollectionData), typeof(int) })]
    public static class tk2dBaseSprite_SetSpriteCollection_Patch
    {
        public static void Postfix(tk2dBaseSprite __instance)
        {
            if (__instance is tk2dSprite sprite)
            {
                tk2dSprite_Awake_Patch.Postfix(sprite);
            }
        }
    }

    [HarmonyPatch(typeof(tk2dBaseSprite), nameof(tk2dBaseSprite.spriteId), MethodType.Setter)]
    public static class tk2dBaseSprite_SpriteId_Patch
    {
        public static void Postfix(tk2dBaseSprite __instance)
        {
            if (__instance is tk2dSprite sprite)
            {
                tk2dSprite_Awake_Patch.Postfix(sprite);
            }
        }
    }

    [HarmonyPatch(typeof(CharmIconList), "Start")]
    public static class CharmIconList_Start_Patch
    {
        public static void Postfix(CharmIconList __instance)
        {
            try
            {
                SkinManager.ApplyCharmSkins(__instance);
            }
            catch (Exception ex)
            {
                BetterMultiplayer.Instance.LogError("Error in CharmIconList Start Patch: " + ex);
            }
        }
    }
}
