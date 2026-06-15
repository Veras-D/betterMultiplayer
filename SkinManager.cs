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
        public static Texture2D LocalLiquidTexture { get; private set; }
        public static Texture2D LocalHitPtTexture { get; private set; }
        public static Texture2D LocalDeathPtTexture { get; private set; }
 
        public static Texture2D RemoteCloakTexture { get; private set; }
        public static Texture2D RemoteLiquidTexture { get; private set; }
        public static Texture2D RemoteVSTexture { get; private set; }
        public static Texture2D RemoteWingsTexture { get; private set; }
        public static Texture2D RemoteSprintTexture { get; private set; }
        public static Texture2D RemoteVoidSpellsTexture { get; private set; }
        public static Texture2D RemoteWraithsTexture { get; private set; }
        public static Texture2D RemoteShriekTexture { get; private set; }
        public static Texture2D RemoteShadeTexture { get; private set; }
        public static Texture2D RemoteHitPtTexture { get; private set; }
        public static Texture2D RemoteDeathPtTexture { get; private set; }

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
                LocalHUDTexture = LoadExtraTexture(skinName, "HUD.png", flipVertical: true) ?? LoadExtraTexture(skinName, "Hud.png", flipVertical: true);
                if (LocalHUDTexture != null && (LocalHUDTexture.width != 2048 || LocalHUDTexture.height != 2048))
                {
                    BetterMultiplayer.Instance.LogError(
                        $"[Skin] {skinName}/Hud.png is {LocalHUDTexture.width}x{LocalHUDTexture.height} — " +
                        "must be 2048x2048 (Custom Knight atlas format). HUD skin will not be applied.");
                    LocalHUDTexture = null;
                }
                LocalVSTexture = LoadExtraTexture(skinName, "VS.png");
                LocalWingsTexture = LoadExtraTexture(skinName, "Wings.png");
                LocalSprintTexture = LoadExtraTexture(skinName, "Sprint.png") ?? LoadExtraTexture(skinName, "sprint.png");
                LocalVoidSpellsTexture = LoadExtraTexture(skinName, "VoidSpells.png") ?? LoadExtraTexture(skinName, "voidSpells.png");
                LocalWraithsTexture = LoadExtraTexture(skinName, "Wraiths.png") ?? LoadExtraTexture(skinName, "wraiths.png");
                LocalShriekTexture = LoadExtraTexture(skinName, "Shriek.png") ?? LoadExtraTexture(skinName, "shriek.png");
                LocalOrbFullTexture = LoadExtraTexture(skinName, "OrbFull.png") ?? LoadExtraTexture(skinName, "orbFull.png");
                LocalShadeTexture = LoadExtraTexture(skinName, "Shade.png") ?? LoadExtraTexture(skinName, "shade.png");
                LocalLiquidTexture = LoadExtraTexture(skinName, "Liquid.png") ?? LoadExtraTexture(skinName, "liquid.png");
                LocalHitPtTexture = LoadExtraTexture(skinName, "HitPt.png") ?? LoadExtraTexture(skinName, "hitPt.png") ?? LoadExtraTexture(skinName, "hitpt.png");
                LocalDeathPtTexture = LoadExtraTexture(skinName, "Deathpt.png") ?? LoadExtraTexture(skinName, "deathPt.png") ?? LoadExtraTexture(skinName, "deathpt.png");
 
                BetterMultiplayer.Instance.Log($"Successfully applied local skin: {skinName}");
                if (CharmIconList.Instance != null)
                {
                    ApplyCharmSkins(CharmIconList.Instance);
                }
                hudSkinPending = true;
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
                Texture2D liquid = null;
                Texture2D hitPt = null;
                Texture2D deathPt = null;
 
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
                    liquid = LoadExtraTexture(skinName, "Liquid.png") ?? LoadExtraTexture(skinName, "liquid.png");
                    hitPt = LoadExtraTexture(skinName, "HitPt.png") ?? LoadExtraTexture(skinName, "hitPt.png") ?? LoadExtraTexture(skinName, "hitpt.png");
                    deathPt = LoadExtraTexture(skinName, "Deathpt.png") ?? LoadExtraTexture(skinName, "deathPt.png") ?? LoadExtraTexture(skinName, "deathpt.png");
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
                RemoteLiquidTexture = liquid;
                RemoteHitPtTexture = hitPt;
                RemoteDeathPtTexture = deathPt;
 
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
        private static bool hudSkinPending = false;
        private static int hudSkinRetryCount = 0;
        private const int MAX_HUD_RETRY_FRAMES = 300; // ~5 seconds at 60fps before giving up
        private static MeshRenderer localMeshRenderer;
        private static MeshRenderer remoteMeshRenderer;
        private static MeshRenderer localCloakRenderer;
        private static MeshRenderer remoteCloakRenderer;

        private static MeshRenderer GetLocalMeshRenderer()
        {
            if (localMeshRenderer == null && HeroController.instance != null && HeroController.instance.gameObject != null)
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
            if (localCloakRenderer == null && HeroController.instance != null && HeroController.instance.gameObject != null)
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
                    hudSkinPending = true;
                    hudSkinRetryCount = 0;
                }

                if (hudSkinPending)
                {
                    hudSkinRetryCount++;
                    if (hudSkinRetryCount > MAX_HUD_RETRY_FRAMES)
                    {
                        // Give up to avoid lag; tk2dSprite_Awake_Patch will catch HUD sprites as they wake
                        hudSkinPending = false;
                        hudSkinRetryCount = 0;
                    }
                    else
                    {
                        var hudCanvas = GameObject.Find("Hud Canvas");
                        if (hudCanvas != null)
                        {
                            UpdateHUDSkin();
                            hudSkinPending = false;  // only clear once HUD was actually found and skinned
                            hudSkinRetryCount = 0;
                        }
                    }
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

        private static Texture2D LoadTexture(string filePath, bool flipVertical = false)
        {
            try
            {
                byte[] fileData = File.ReadAllBytes(filePath);
                // mipmaps=false: runtime-loaded mipmaps on a sprite atlas bleed
                // sprite edges into adjacent sprite regions at lower mip
                // levels, which is what causes the "appears during animation
                // then disappears" artifact when the mip sampler reads the
                // wrong area of the atlas as the sprite scales.
                Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (UnityEngine.ImageConversion.LoadImage(tex, fileData))
                {
                    // UnityEngine.ImageConversion.LoadImage loads PNGs
                    // bottom-up (Y=0 at the bottom of the runtime texture),
                    // but the Custom Knight Hud.png was authored top-down
                    // and tk2dSpriteDefinition.uvs[] were baked against that
                    // orientation. Without flipping, every UV sample hits
                    // the vertically-mirrored version of the atlas, which is
                    // the actual root cause of the "life icon in extra soul
                    // slot" / "geo counter shows soul vessel" / etc. mismaps.
                    //
                    // Custom Knight itself ships a precompiled Unity asset
                    // bundle (not a raw PNG), so Unity's asset import handles
                    // the orientation flip at build time. We have to do it
                    // at runtime, but ONLY for the Hud.png — the character
                    // Knight.png is applied per-renderer via
                    // MaterialPropertyBlock and works with the default
                    // (flipped) load. The flipVertical flag lets us opt in
                    // per-file.
                    if (flipVertical)
                    {
                        tex = FlipTextureVertically(tex);
                    }
                    tex.filterMode = FilterMode.Bilinear;
                    tex.anisoLevel = 4;
                    tex.wrapMode = TextureWrapMode.Clamp;
                    return tex;
                }
            }
            catch (Exception ex)
            {
                BetterMultiplayer.Instance.LogError("Error loading texture from file: " + ex);
            }
            return null;
        }

        private static Texture2D FlipTextureVertically(Texture2D original)
        {
            var pixels = original.GetPixels();
            int w = original.width;
            int h = original.height;
            var flipped = new Texture2D(w, h, original.format, false);
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    flipped.SetPixel(x, h - 1 - y, pixels[y * w + x]);
                }
            }
            flipped.Apply();
            return flipped;
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

        private static Texture2D GetCachedTexture(string skinName, string fileName, bool flipVertical = false)
        {
            if (skinName == "Default") return null;

            // The cached texture is keyed by filename only, so a flipVertical
            // call against an already-cached, non-flipped texture (or vice
            // versa) would return the wrong orientation. We append a
            // suffix to the cache key to keep flipped and non-flipped
            // copies separate.
            string cacheKey = fileName + (flipVertical ? "|flip" : "");

            if (!textureCache.TryGetValue(skinName, out var skinCache))
            {
                skinCache = new Dictionary<string, Texture2D>();
                textureCache[skinName] = skinCache;
            }

            if (skinCache.TryGetValue(cacheKey, out var cachedTex))
            {
                return cachedTex;
            }

            try
            {
                string skinPath = Path.Combine(skinsDir, skinName);
                string fullPath = Path.Combine(skinPath, fileName);
                if (File.Exists(fullPath))
                {
                    Texture2D tex = LoadTexture(fullPath, flipVertical);
                    if (tex != null)
                    {
                        skinCache[cacheKey] = tex;
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

        private static Texture2D LoadExtraTexture(string skinName, string fileName, bool flipVertical = false)
        {
            return GetCachedTexture(skinName, fileName, flipVertical);
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
 
        public static void ReskinMaterial(Material mat, Texture2D defaultTex, bool forceHud = false)
        {
            if (mat == null) return;
            
            string matName = mat.name ?? "";
            string texName = mat.mainTexture != null ? mat.mainTexture.name : "";

            if (forceHud)
            {
                if (LocalHUDTexture != null)
                {
                    mat.mainTexture = LocalHUDTexture;
                    BetterMultiplayer.Instance.Log($"[ReskinMaterial] Force-applied HUD texture to material '{matName}' (tex was '{(mat.mainTexture != null ? mat.mainTexture.name : "null")}')");
                }
                return;
            }

            if (matName.IndexOf("HitPt", StringComparison.OrdinalIgnoreCase) >= 0 ||
                texName.IndexOf("HitPt", StringComparison.OrdinalIgnoreCase) >= 0 ||
                matName.IndexOf("Hit_Pt", StringComparison.OrdinalIgnoreCase) >= 0 ||
                texName.IndexOf("Hit_Pt", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                if (LocalHitPtTexture != null)
                {
                    mat.mainTexture = LocalHitPtTexture;
                }
            }
            else if (matName.IndexOf("Deathpt", StringComparison.OrdinalIgnoreCase) >= 0 ||
                texName.IndexOf("Deathpt", StringComparison.OrdinalIgnoreCase) >= 0 ||
                matName.IndexOf("Death_Pt", StringComparison.OrdinalIgnoreCase) >= 0 ||
                texName.IndexOf("Death_Pt", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                if (LocalDeathPtTexture != null)
                {
                    mat.mainTexture = LocalDeathPtTexture;
                }
            }
            else if (matName.IndexOf("Liquid", StringComparison.OrdinalIgnoreCase) >= 0 ||
                texName.IndexOf("Liquid", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                if (LocalLiquidTexture != null)
                {
                    mat.mainTexture = LocalLiquidTexture;
                }
            }
            else if (matName.IndexOf("orbfull", StringComparison.OrdinalIgnoreCase) >= 0 ||
                texName.IndexOf("orbfull", StringComparison.OrdinalIgnoreCase) >= 0 ||
                matName.IndexOf("soul_orb", StringComparison.OrdinalIgnoreCase) >= 0 ||
                texName.IndexOf("soul_orb", StringComparison.OrdinalIgnoreCase) >= 0 ||
                matName.IndexOf("vessel", StringComparison.OrdinalIgnoreCase) >= 0 ||
                texName.IndexOf("vessel", StringComparison.OrdinalIgnoreCase) >= 0 ||
                matName.IndexOf("orb", StringComparison.OrdinalIgnoreCase) >= 0 ||
                texName.IndexOf("orb", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                if (LocalOrbFullTexture != null)
                {
                    mat.mainTexture = LocalOrbFullTexture;
                }
            }
            else if (matName.IndexOf("HUD", StringComparison.OrdinalIgnoreCase) >= 0 ||
                texName.IndexOf("HUD", StringComparison.OrdinalIgnoreCase) >= 0 ||
                matName.IndexOf("health", StringComparison.OrdinalIgnoreCase) >= 0 ||
                texName.IndexOf("health", StringComparison.OrdinalIgnoreCase) >= 0 ||
                matName.IndexOf("geo", StringComparison.OrdinalIgnoreCase) >= 0 ||
                texName.IndexOf("geo", StringComparison.OrdinalIgnoreCase) >= 0 ||
                matName.IndexOf("heart", StringComparison.OrdinalIgnoreCase) >= 0 ||
                texName.IndexOf("heart", StringComparison.OrdinalIgnoreCase) >= 0 ||
                matName.IndexOf("lifeblood", StringComparison.OrdinalIgnoreCase) >= 0 ||
                texName.IndexOf("lifeblood", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                if (LocalHUDTexture != null)
                {
                    mat.mainTexture = LocalHUDTexture;
                }
            }
        }

        public static void ReskinCollection(tk2dSpriteCollectionData collection, Texture2D customTex, bool forceHud = false)
        {
            if (collection == null || customTex == null) return;
            
            if (collection.material != null)
            {
                ReskinMaterial(collection.material, customTex, forceHud);
            }
            if (collection.materials != null)
            {
                for (int i = 0; i < collection.materials.Length; i++)
                {
                    if (collection.materials[i] != null)
                    {
                        ReskinMaterial(collection.materials[i], customTex, forceHud);
                    }
                }
            }
            if (collection.spriteDefinitions != null)
            {
                for (int i = 0; i < collection.spriteDefinitions.Length; i++)
                {
                    var def = collection.spriteDefinitions[i];
                    if (def != null && def.material != null)
                    {
                        ReskinMaterial(def.material, customTex, forceHud);
                    }
                }
            }
        }

        private static void DumpHierarchy(GameObject go, string indent)
        {
            if (go == null) return;
            string componentList = "";
            foreach (var c in go.GetComponents<Component>())
            {
                if (c != null) componentList += c.GetType().Name + ", ";
            }
            BetterMultiplayer.Instance.Log($"{indent}- {go.name} (active={go.activeSelf}) | Components: {componentList}");
            
            var renderer = go.GetComponent<Renderer>();
            if (renderer != null)
            {
                BetterMultiplayer.Instance.Log($"{indent}  Renderer: {renderer.GetType().Name}");
                if (renderer.sharedMaterials != null)
                {
                    for (int i = 0; i < renderer.sharedMaterials.Length; i++)
                    {
                        var mat = renderer.sharedMaterials[i];
                        if (mat != null)
                        {
                            BetterMultiplayer.Instance.Log($"{indent}    Material[{i}]: {mat.name} | Shader: {mat.shader.name} | mainTex: {(mat.mainTexture != null ? mat.mainTexture.name : "null")}");
                        }
                    }
                }
            }

            for (int i = 0; i < go.transform.childCount; i++)
            {
                DumpHierarchy(go.transform.GetChild(i).gameObject, indent + "  ");
            }
        }

        public static void UpdateHUDSkin()
        {
            if (LocalHUDTexture == null && LocalOrbFullTexture == null && LocalLiquidTexture == null && LocalHitPtTexture == null && LocalDeathPtTexture == null) return;
 
            // Use the proper way to access the HUD canvas
            GameObject hudCanvas = null;
            try
            {
                if (GameCameras.instance != null)
                {
                    hudCanvas = GameCameras.instance.hudCanvas;
                }
            }
            catch { }
            if (hudCanvas == null)
            {
                hudCanvas = GameObject.Find("Hud Canvas");
            }
            if (hudCanvas == null) return;

            // One-time diagnostic: dump HUD canvas structure so we can see what
            // sprites and materials exist. Helps identify which element is glitchy.
            DumpHUDStructure(hudCanvas);

            // One-time diagnostic: log every HUD GameObject's active/enabled
            // state and the actual rendered sprite name. This is the key
            // diagnostic the user needs to see — if the Health GameObjects
            // are reporting enabled=false, the game is hiding them, and
            // no amount of mod-level texture work will make them visible.
            DumpHUDVisibility(hudCanvas);

            // === HUD texture: replace the entire HUD atlas ===
            // The standard Custom Knight approach: get the material from the
            // "Health 1" sprite's current sprite definition and set its
            // mainTexture to the skin's Hud.png. Since every HUD sprite
            // shares the same material (or materialInst), this single swap
            // updates the entire HUD atlas.
            //
            // We also reset the _MainTex_ST tiling/offset to (1,1,0,0) and
            // set the new texture's wrapMode to Clamp. Without this, leftover
            // values from the original DXT5 atlas cause the new texture to be
            // sampled at wrong scale/offset, which is what produced the
            // "unscaled" / mismapped sprite look the user reported.
            if (LocalHUDTexture != null)
            {
                BetterMultiplayer.Instance.Log($"[HUD] Hud.png size: {LocalHUDTexture.width}x{LocalHUDTexture.height}");
                // Find ALL distinct materials used by HUD sprites, not just
                // "Health 1". The HUD has multiple collections (HUD Cln,
                // HUD_Soulorb_fills, Charm Blocker Cln, HUD Extras Cln) and
                // each may use a different material. Missing any of them
                // leaves a subset of HUD sprites using the default atlas.
                HashSet<Material> updatedMaterials = new HashSet<Material>();
                int spriteCount = 0;
                foreach (var sprite in hudCanvas.GetComponentsInChildren<tk2dSprite>(true))
                {
                    if (sprite == null || sprite.Collection == null) continue;
                    var def = sprite.GetCurrentSpriteDef();
                    if (def == null) continue;
                    spriteCount++;

                    if (def.material != null && updatedMaterials.Add(def.material))
                    {
                        if (def.material.mainTexture != LocalHUDTexture)
                        {
                            def.material.mainTexture = LocalHUDTexture;
                        }
                        def.material.SetTextureScale("_MainTex", new Vector2(1f, 1f));
                        def.material.SetTextureOffset("_MainTex", new Vector2(0f, 0f));
                    }
                    if (def.materialInst != null && def.materialInst != def.material && updatedMaterials.Add(def.materialInst))
                    {
                        if (def.materialInst.mainTexture != LocalHUDTexture)
                        {
                            def.materialInst.mainTexture = LocalHUDTexture;
                        }
                        def.materialInst.SetTextureScale("_MainTex", new Vector2(1f, 1f));
                        def.materialInst.SetTextureOffset("_MainTex", new Vector2(0f, 0f));
                    }
                }
                BetterMultiplayer.Instance.Log($"[UpdateHUDSkin] Applied HUD.png to {updatedMaterials.Count} material(s) across {spriteCount} sprites");
            }

            // === OrbFull texture: replace the "Orb Full" SpriteRenderer's sprite ===
            // The soul vessel uses a UGUI SpriteRenderer, not tk2dSprite.
            if (LocalOrbFullTexture != null)
            {
                SpriteRenderer orbFull = null;
                SpriteRenderer pulseSprite = null;
                foreach (var sr in hudCanvas.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    if (sr == null) continue;
                    string n = sr.gameObject.name;
                    if (n == "Orb Full") orbFull = sr;
                    else if (n == "Pulse Sprite") pulseSprite = sr;
                }
                if (orbFull != null && orbFull.sprite != null)
                {
                    Sprite newSprite = Sprite.Create(LocalOrbFullTexture,
                        new Rect(0f, 0f, LocalOrbFullTexture.width, LocalOrbFullTexture.height),
                        orbFull.sprite.pivot / orbFull.sprite.rect.size,
                        orbFull.sprite.pixelsPerUnit);
                    orbFull.sprite = newSprite;
                    BetterMultiplayer.Instance.Log("[UpdateHUDSkin] Applied OrbFull.png to 'Orb Full' SpriteRenderer");
                }
                // Destroy the pulse effect since it would show the old orb
                if (pulseSprite != null && pulseSprite.gameObject != null)
                {
                    UnityEngine.Object.Destroy(pulseSprite.gameObject);
                }
            }
        }

        private static bool hudDumped = false;
        private static void DumpHUDStructure(GameObject hudCanvas)
        {
            try
            {
                BetterMultiplayer.Instance.Log("=== HUD CANVAS DUMP ===");
                int idx = 0;
                foreach (var s in hudCanvas.GetComponentsInChildren<tk2dSprite>(true))
                {
                    if (s == null) continue;
                    var def = s.GetCurrentSpriteDef();
                    string matName = (def != null && def.material != null) ? def.material.name : "null";
                    string texName = (def != null && def.material != null && def.material.mainTexture != null) ? def.material.mainTexture.name : "null";
                    string colName = s.Collection != null ? s.Collection.name : "null";
                    // tk2d often uses a per-sprite material instance. Check it too.
                    string miName = "null";
                    string miTex = "null";
                    if (def != null && def.materialInst != null)
                    {
                        miName = def.materialInst.name;
                        if (def.materialInst.mainTexture != null) miTex = def.materialInst.mainTexture.name;
                    }
                    BetterMultiplayer.Instance.Log($"  [tk2d] #{idx++} '{s.gameObject.name}' col='{colName}' mat='{matName}' tex='{texName}' matInst='{miName}' miTex='{miTex}'");
                }
                int sidx = 0;
                foreach (var sr in hudCanvas.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    if (sr == null) continue;
                    string texName = (sr.sprite != null && sr.sprite.texture != null) ? sr.sprite.texture.name : "null";
                    BetterMultiplayer.Instance.Log($"  [SR ] #{sidx++} '{sr.gameObject.name}' tex='{texName}'");
                }
                BetterMultiplayer.Instance.Log("=== END HUD DUMP ===");
                hudDumped = true;
            }
            catch (Exception ex)
            {
                BetterMultiplayer.Instance.LogError("Error dumping HUD: " + ex);
            }
        }

        // Logs every HUD GameObject's activeSelf, enabled, and the sprite's
        // current spriteId, spriteName, color, and bounds. This is the
        // diagnostic the user needs to see exactly what the game is
        // showing right now — if the Health N GameObjects are reporting
        // enabled=false or activeSelf=false, the game is hiding them
        // and no amount of mod-level texture work will fix it.
        private static bool hudVisibilityDumped = false;
        private static void DumpHUDVisibility(GameObject hudCanvas)
        {
            if (hudVisibilityDumped) return;
            try
            {
                BetterMultiplayer.Instance.Log("=== HUD VISIBILITY DUMP ===");
                int idx = 0;
                foreach (var s in hudCanvas.GetComponentsInChildren<tk2dSprite>(true))
                {
                    if (s == null) continue;
                    var def = s.GetCurrentSpriteDef();
                    var rend = s.GetComponent<MeshRenderer>();
                    bool active = s.gameObject.activeSelf;
                    bool enabled = s.gameObject.activeInHierarchy;
                    bool rendEnabled = rend != null && rend.enabled;
                    string spriteName = def != null && !string.IsNullOrEmpty(def.name) ? def.name : "?";
                    string pos = s.transform.localPosition.ToString("F1");
                    string scale = s.transform.localScale.ToString("F2");
                    BetterMultiplayer.Instance.Log(
                        $"  [Vis] #{idx++} '{s.gameObject.name}' "
                        + $"active={active} enabled={enabled} rend={rendEnabled} "
                        + $"sprite='{spriteName}' pos={pos} scale={scale}");
                }
                hudVisibilityDumped = true;
            }
            catch (Exception ex)
            {
                BetterMultiplayer.Instance.LogError("Error in DumpHUDVisibility: " + ex);
            }
        }

        // Extracts every HUD sprite from the game's vanilla atlas at its
        // current UV coordinates and saves each one as a separate PNG file
        // named after the sprite (with the collection name as a prefix to
        // disambiguate "Health 1" in 'HUD Cln' vs, say, "Health 1" in
        // some other collection). The dump goes to
        // BepInEx/plugins/betterMultiplayer/_dump/ as both per-sprite PNGs
        // (one file per sprite) and a single JSON sidecar with the exact
        // pixel rectangles so a downstream template-matching script has
        // everything it needs.
        public static void DumpHUDSprites()
        {
            try
            {
                GameObject hudCanvas = null;
                try
                {
                    if (GameCameras.instance != null)
                    {
                        hudCanvas = GameCameras.instance.hudCanvas;
                    }
                }
                catch { }
                if (hudCanvas == null)
                {
                    hudCanvas = GameObject.Find("Hud Canvas");
                }
                if (hudCanvas == null)
                {
                    BetterMultiplayer.Instance.LogError("[DumpHUDSprites] Hud Canvas not found");
                    return;
                }

                string dllPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                string dllDir = Path.GetDirectoryName(dllPath);
                string dumpDir = Path.Combine(dllDir, "_dump");
                if (!Directory.Exists(dumpDir))
                {
                    Directory.CreateDirectory(dumpDir);
                }

                int dumpedCount = 0;
                int skippedCount = 0;
                int skippedNoDef = 0;
                int skippedNoTex = 0;
                int skippedSmall = 0;
                HashSet<Texture2D> dumpedAtlases = new HashSet<Texture2D>();

                // Build a JSON sidecar describing each sprite's UV rect
                // in the source atlas. The downstream script uses these
                // pixel rectangles as the source-of-truth search areas.
                var json = new Dictionary<string, object>();
                json["atlasWidth"] = 0;
                json["atlasHeight"] = 0;
                var jsonSprites = new List<object>();
                json["sprites"] = jsonSprites;

                // First pass: log one example sprite so we can see the
                // def / material / texture state.
                foreach (var s0 in hudCanvas.GetComponentsInChildren<tk2dSprite>(true))
                {
                    if (s0 == null) continue;
                    var def0 = s0.GetCurrentSpriteDef();
                    BetterMultiplayer.Instance.Log(
                        $"[DumpHUDSprites] Sample: '{s0.gameObject.name}' "
                        + $"def={(def0 != null ? "ok" : "null")} "
                        + $"col={(s0.Collection != null ? s0.Collection.name : "null")} "
                        + $"mat={(def0 != null && def0.material != null ? def0.material.name : "null")} "
                        + $"matInst={(def0 != null && def0.materialInst != null ? def0.materialInst.name : "null")} "
                        + $"mainTex={(def0 != null && def0.material != null && def0.material.mainTexture != null ? def0.material.mainTexture.name + "(" + def0.material.mainTexture.GetType().Name + ")" : "null")}");
                    break;
                }

                foreach (var s in hudCanvas.GetComponentsInChildren<tk2dSprite>(true))
                {
                    if (s == null) continue;
                    var def = s.GetCurrentSpriteDef();
                    if (def == null) { skippedCount++; skippedNoDef++; continue; }

                    // Try multiple sources for the atlas texture. The
                    // def.material.mainTexture can be null even when the
                    // sprite is rendering fine (e.g. when the material is
                    // a per-sprite instance, or the texture is on a
                    // shared "atlas" material the sprite references
                    // indirectly). Fall back to the renderer's material
                    // and the collection's material.
                    Texture2D atlas = null;
                    if (def.material != null) atlas = def.material.mainTexture as Texture2D;
                    if (atlas == null && def.materialInst != null)
                    {
                        atlas = def.materialInst.mainTexture as Texture2D;
                    }
                    if (atlas == null && def.materialInst != null)
                    {
                        // materialInst is a copy of material; read from it
                        // via the underlying property name
                        atlas = def.materialInst.GetTexture("_MainTex") as Texture2D;
                    }
                    if (atlas == null && s.Collection != null)
                    {
                        if (s.Collection.material != null)
                            atlas = s.Collection.material.mainTexture as Texture2D;
                    }
                    if (atlas == null)
                    {
                        // Last resort: ask the renderer's shared material
                        var rend = s.GetComponent<MeshRenderer>();
                        if (rend != null && rend.sharedMaterial != null)
                        {
                            atlas = rend.sharedMaterial.mainTexture as Texture2D;
                        }
                    }
                    if (atlas == null) { skippedCount++; skippedNoTex++; continue; }

                    // Copy to RGBA32 once per atlas (handles DXT5/BC3).
                    Texture2D readable = dumpedAtlases.Contains(atlas)
                        ? null
                        : MakeReadableCopy(atlas);
                    if (readable != null)
                    {
                        dumpedAtlases.Add(atlas);
                        jsonSprites.Add(new Dictionary<string, object>
                        {
                            { "_atlas", "atlas_" + readable.width + "x" + readable.height + ".png" },
                            { "width", readable.width },
                            { "height", readable.height }
                        });
                        json["atlasWidth"] = readable.width;
                        json["atlasHeight"] = readable.height;
                        // Save the readable atlas as a reference (once)
                        File.WriteAllBytes(Path.Combine(dumpDir, "atlas_" + readable.width + "x" + readable.height + ".png"), readable.EncodeToPNG());
                    }

                    if (def.uvs == null || def.uvs.Length < 4) { skippedCount++; continue; }

                    // Convert UV (0..1) to pixel coordinates. Y is flipped
                    // because Unity textures have Y=0 at the bottom but
                    // PNG/UI conventions have Y=0 at the top — and the
                    // Custom Knight atlas was authored top-down.
                    Vector2 uvMin = def.uvs[0];
                    Vector2 uvMax = def.uvs[0];
                    for (int i = 1; i < def.uvs.Length; i++)
                    {
                        uvMin = Vector2.Min(uvMin, def.uvs[i]);
                        uvMax = Vector2.Max(uvMax, def.uvs[i]);
                    }
                    int texW = atlas.width;
                    int texH = atlas.height;
                    // Flip Y so the dumped PNG matches what an image
                    // editor shows (top-down, matching the Custom Knight
                    // authored layout).
                    int px = Mathf.Clamp(Mathf.RoundToInt(uvMin.x * texW), 0, texW - 1);
                    int pyTop = Mathf.Clamp(Mathf.RoundToInt((1f - uvMax.y) * texH), 0, texH - 1);
                    int pw = Mathf.Clamp(Mathf.RoundToInt((uvMax.x - uvMin.x) * texW), 1, texW - px);
                    int ph = Mathf.Clamp(Mathf.RoundToInt((uvMax.y - uvMin.y) * texH), 1, texH - pyTop);
                    if (pw < 2 || ph < 2) { skippedCount++; skippedSmall++; continue; }

                    // Get the readable copy (or a fresh one) to read pixels from
                    Texture2D src = readable != null ? readable : MakeReadableCopy(atlas);
                    if (src == null) { skippedCount++; continue; }
                    if (!dumpedAtlases.Contains(src))
                    {
                        dumpedAtlases.Add(src);
                        File.WriteAllBytes(Path.Combine(dumpDir, "atlas_" + src.width + "x" + src.height + ".png"), src.EncodeToPNG());
                    }

                    Color[] pixels = src.GetPixels(px, pyTop, pw, ph);
                    Texture2D spriteTex = new Texture2D(pw, ph, TextureFormat.RGBA32, false);
                    spriteTex.SetPixels(pixels);
                    spriteTex.Apply();

                    // Filename: collection_spriteName.png (sanitized)
                    string colName = s.Collection != null ? s.Collection.name : "unknown";
                    string safeCol = SanitizeFileName(colName);
                    string safeName = SanitizeFileName(s.gameObject.name);
                    string filename = safeCol + "__" + safeName + ".png";
                    string filePath = Path.Combine(dumpDir, filename);
                    File.WriteAllBytes(filePath, spriteTex.EncodeToPNG());

                    jsonSprites.Add(new Dictionary<string, object>
                    {
                        { "file", filename },
                        { "collection", colName },
                        { "gameObject", s.gameObject.name },
                        { "uvMin", new Dictionary<string, float> { { "x", uvMin.x }, { "y", uvMin.y } } },
                        { "uvMax", new Dictionary<string, float> { { "x", uvMax.x }, { "y", uvMax.y } } },
                        { "atlasPx", new Dictionary<string, int> { { "x", px }, { "y", pyTop }, { "w", pw }, { "h", ph } } },
                        { "atlasSize", new Dictionary<string, int> { { "w", texW }, { "h", texH } } }
                    });

                    UnityEngine.Object.Destroy(spriteTex);
                    dumpedCount++;
                }

                // Write the JSON sidecar
                string jsonPath = Path.Combine(dumpDir, "hud_sprites.json");
                File.WriteAllText(jsonPath, MiniJson.Serialize(json));
                BetterMultiplayer.Instance.Log(
                    $"[DumpHUDSprites] Dumped {dumpedCount} sprites to {dumpDir} "
                    + $"(skipped {skippedCount}: noDef={skippedNoDef}, noTex={skippedNoTex}, small={skippedSmall}). "
                    + $"JSON sidecar: {jsonPath}");
            }
            catch (Exception ex)
            {
                BetterMultiplayer.Instance.LogError("Error in DumpHUDSprites: " + ex);
            }
        }

        private static Texture2D MakeReadableCopy(Texture2D source)
        {
            if (source == null) return null;
            try
            {
                var copy = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
                var pixels = source.GetPixels();
                copy.SetPixels(pixels);
                copy.Apply();
                return copy;
            }
            catch (Exception ex)
            {
                BetterMultiplayer.Instance.LogError("MakeReadableCopy failed: " + ex.Message);
                return null;
            }
        }

        private static string SanitizeFileName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "unnamed";
            char[] invalid = Path.GetInvalidFileNameChars();
            var sb = new System.Text.StringBuilder(name.Length);
            foreach (char c in name)
            {
                sb.Append(Array.IndexOf(invalid, c) >= 0 ? '_' : c);
            }
            return sb.ToString();
        }

        // Tiny JSON writer — avoids a Newtonsoft.Json dependency for the dump
        // sidecar. Only handles the shapes we actually emit.
        private static class MiniJson
        {
            public static string Serialize(object obj)
            {
                var sb = new System.Text.StringBuilder();
                Write(sb, obj, 0);
                return sb.ToString();
            }
            private static void Write(System.Text.StringBuilder sb, object obj, int indent)
            {
                if (obj == null) { sb.Append("null"); return; }
                if (obj is bool) { sb.Append(((bool)obj) ? "true" : "false"); return; }
                if (obj is string) { WriteString(sb, (string)obj); return; }
                if (obj is int || obj is long || obj is float || obj is double)
                {
                    sb.Append(System.Convert.ToString(obj, System.Globalization.CultureInfo.InvariantCulture));
                    return;
                }
                if (obj is System.Collections.IDictionary)
                {
                    var dict = (System.Collections.IDictionary)obj;
                    if (dict.Count == 0) { sb.Append("{}"); return; }
                    sb.Append("{\n");
                    bool first = true;
                    foreach (System.Collections.DictionaryEntry kv in dict)
                    {
                        if (!first) sb.Append(",\n"); first = false;
                        Indent(sb, indent + 1);
                        WriteString(sb, kv.Key.ToString());
                        sb.Append(": ");
                        Write(sb, kv.Value, indent + 1);
                    }
                    sb.Append("\n"); Indent(sb, indent); sb.Append("}");
                    return;
                }
                if (obj is System.Collections.IList)
                {
                    var list = (System.Collections.IList)obj;
                    if (list.Count == 0) { sb.Append("[]"); return; }
                    sb.Append("[\n");
                    bool first = true;
                    foreach (var item in list)
                    {
                        if (!first) sb.Append(",\n"); first = false;
                        Indent(sb, indent + 1);
                        Write(sb, item, indent + 1);
                    }
                    sb.Append("\n"); Indent(sb, indent); sb.Append("]");
                    return;
                }
                sb.Append("null");
            }
            private static void WriteString(System.Text.StringBuilder sb, string s)
            {
                sb.Append('"');
                foreach (char c in s)
                {
                    if (c == '"' || c == '\\') sb.Append('\\').Append(c);
                    else if (c == '\n') sb.Append("\\n");
                    else if (c == '\r') sb.Append("\\r");
                    else if (c == '\t') sb.Append("\\t");
                    else sb.Append(c);
                }
                sb.Append('"');
            }
            private static void Indent(System.Text.StringBuilder sb, int n)
            {
                for (int i = 0; i < n; i++) sb.Append("  ");
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

                if (!isRemote)
                {
                    // NOTE: the previous "liquid"/"vessel"/"orbfull"/"soul_orb"
                    // texture overrides were REMOVED. Those names match HUD
                    // children (Liquid, Vessel 1-4) and were overwriting the
                    // shared material's mainTexture with LocalLiquidTexture /
                    // LocalOrbFullTexture, which corrupted the entire HUD atlas
                    // (Health 1-11, Geo Sprite, etc.) because they share the
                    // same material. The level Liquid and the soul Orb Full
                    // are now handled separately: Orb Full via the
                    // SpriteRenderer replacement in UpdateHUDSkin, and the
                    // level liquid is left as default (the HUD covers all
                    // important liquid visuals via Hud.png).
                }

                if (__instance.Collection != null)
                {
                    string colName = __instance.Collection.name;
                    if (!string.IsNullOrEmpty(colName))
                    {
                        // Skip sprites whose GameObject name matches the old
                        // HUD override patterns, even when they're in a
                        // non-HUD collection. This prevents any leftover
                        // override from firing on accidentally-matching
                        // names.
                        if (name.IndexOf("liquid", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            name.IndexOf("orbfull", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            name.IndexOf("soul_orb", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            name.IndexOf("vessel", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            return;
                        }

                        // Re-apply the HUD texture for any HUD sprite whose
                        // collection name indicates it's part of the HUD
                        // atlas. The game calls SetSprite() on these sprites
                        // during state changes (animations, taking damage,
                        // recovering life) and that call can reset or
                        // re-instance the material's mainTexture. This
                        // Postfix fires on every SetSprite, so re-applying
                        // here keeps the skin stable across all state
                        // changes.
                        if (SkinManager.LocalHUDTexture != null)
                        {
                            bool isHUDCollection =
                                colName.IndexOf("HUD", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                colName.IndexOf("Soulorb", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                colName.IndexOf("Charm Blocker", StringComparison.OrdinalIgnoreCase) >= 0;
                            if (isHUDCollection)
                            {
                                var hudDef = __instance.GetCurrentSpriteDef();
                                if (hudDef != null)
                                {
                                    if (hudDef.material != null && hudDef.material.mainTexture != SkinManager.LocalHUDTexture)
                                    {
                                        hudDef.material.mainTexture = SkinManager.LocalHUDTexture;
                                    }
                                    if (hudDef.materialInst != null && hudDef.materialInst != hudDef.material
                                        && hudDef.materialInst.mainTexture != SkinManager.LocalHUDTexture)
                                    {
                                        hudDef.materialInst.mainTexture = SkinManager.LocalHUDTexture;
                                    }
                                }
                            }
                        }
                    }
                }


   
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
                    if (name == "Sprite" && __instance.transform.parent != null && HeroController.instance != null && __instance.transform.parent.gameObject == HeroController.instance.gameObject)
                    {
                        if (SkinManager.LocalSkinTexture != null)
                        {
                            ApplyTexture(__instance, SkinManager.LocalSkinTexture);
                        }
                    }
                    // Check if it is local player cloak
                    else if (name == "Cloak" && __instance.transform.parent != null && HeroController.instance != null && __instance.transform.parent.gameObject == HeroController.instance.gameObject)
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
 
        public static void ApplyTexture(tk2dSprite sprite, Texture2D tex)
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

        public static void ApplyHUDTexture(tk2dSprite sprite, Texture2D tex)
        {
            if (sprite == null || tex == null) return;
            var renderer = sprite.GetComponent<MeshRenderer>();
            if (renderer != null && renderer.sharedMaterial != null)
            {
                renderer.SetPropertyBlock(null);
                if (renderer.sharedMaterial.mainTexture != tex)
                {
                    renderer.sharedMaterial.mainTexture = tex;
                }
            }
            if (sprite.Collection != null)
            {
                if (sprite.Collection.material != null && sprite.Collection.material.mainTexture != tex)
                {
                    sprite.Collection.material.mainTexture = tex;
                }
                if (sprite.Collection.materials != null)
                {
                    for (int i = 0; i < sprite.Collection.materials.Length; i++)
                    {
                        if (sprite.Collection.materials[i] != null && sprite.Collection.materials[i].mainTexture != tex)
                        {
                            sprite.Collection.materials[i].mainTexture = tex;
                        }
                    }
                }
            }
            var def = sprite.GetCurrentSpriteDef();
            if (def != null && def.material != null && def.material.mainTexture != tex)
            {
                def.material.mainTexture = tex;
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

    [HarmonyPatch(typeof(GeoControl), "Start")]
    public static class GeoControl_Start_Patch
    {
        public static void Postfix(GeoControl __instance)
        {
            try
            {
                // The geo pickup animation lives in a SEPARATE material from the HUD
                // sheet. Custom skins' Hud.png is the HUD atlas (2048x2048), but the
                // geo animation frames are at different positions than the original
                // atlas, so replacing the geo material with the user's Hud.png would
                // make the animation show the wrong sprites (spiky / glitchy).
                //
                // The geo counter icon next to "631" lives in the HUD material and is
                // therefore already covered by the Hud.png swap on "Health 1".
                //
                // Intentionally do nothing here. If a future skin provides a separate
                // Geo.png, hook it up here.
                return;
            }
            catch (Exception ex)
            {
                BetterMultiplayer.Instance.LogError("Error in GeoControl Start Patch: " + ex);
            }
        }
    }
}
