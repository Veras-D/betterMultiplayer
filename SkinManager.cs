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

        public static Texture2D RemoteCloakTexture { get; private set; }
        public static Texture2D RemoteVSTexture { get; private set; }
        public static Texture2D RemoteWingsTexture { get; private set; }
        public static Texture2D RemoteSprintTexture { get; private set; }
        public static Texture2D RemoteVoidSpellsTexture { get; private set; }
        public static Texture2D RemoteWraithsTexture { get; private set; }
        public static Texture2D RemoteShriekTexture { get; private set; }

        private static string skinsDir;
        private static List<string> availableSkins = new List<string>();

        private static MaterialPropertyBlock localBlock;
        private static MaterialPropertyBlock remoteBlock;

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
                    string skinPath = Path.Combine(skinsDir, skinName);
                    string knightPng = Path.Combine(skinPath, "Knight.png");
                    if (File.Exists(knightPng))
                    {
                        tex = LoadTexture(knightPng);
                        if (tex == null) return false;
                    }
                    else
                    {
                        return false;
                    }
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

                BetterMultiplayer.Instance.Log($"Successfully applied local skin: {skinName}");
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

                if (skinName != "Default")
                {
                    string skinPath = Path.Combine(skinsDir, skinName);
                    string knightPng = Path.Combine(skinPath, "Knight.png");
                    if (File.Exists(knightPng))
                    {
                        tex = LoadTexture(knightPng);
                    }
                    cloak = LoadExtraTexture(skinName, "Cloak.png");
                    vs = LoadExtraTexture(skinName, "VS.png");
                    wings = LoadExtraTexture(skinName, "Wings.png");
                    sprint = LoadExtraTexture(skinName, "Sprint.png") ?? LoadExtraTexture(skinName, "sprint.png");
                    voidSpells = LoadExtraTexture(skinName, "VoidSpells.png") ?? LoadExtraTexture(skinName, "voidSpells.png");
                    wraiths = LoadExtraTexture(skinName, "Wraiths.png") ?? LoadExtraTexture(skinName, "wraiths.png");
                    shriek = LoadExtraTexture(skinName, "Shriek.png") ?? LoadExtraTexture(skinName, "shriek.png");
                }

                RemoteSkinTexture = tex;
                RemoteCloakTexture = cloak;
                RemoteVSTexture = vs;
                RemoteWingsTexture = wings;
                RemoteSprintTexture = sprint;
                RemoteVoidSpellsTexture = voidSpells;
                RemoteWraithsTexture = wraiths;
                RemoteShriekTexture = shriek;

                BetterMultiplayer.Instance.Log($"Loaded skin for partner: {skinName}");
            }
            catch (Exception ex)
            {
                BetterMultiplayer.Instance.LogError($"Error applying remote skin {skinName}: " + ex);
            }
        }

        public static void UpdateSkins()
        {
            try
            {
                // Update cloak and HUD
                UpdateCloakSkin();
                UpdateHUDSkin();

                if (HeroController.instance != null)
                {
                    var sprite = HeroController.instance.GetComponentInChildren<tk2dSprite>();
                    if (sprite != null)
                    {
                        var renderer = sprite.GetComponent<MeshRenderer>();
                        if (renderer != null)
                        {
                            if (LocalSkinTexture != null)
                            {
                                if (localBlock == null)
                                {
                                    localBlock = new MaterialPropertyBlock();
                                }
                                renderer.GetPropertyBlock(localBlock);
                                localBlock.SetTexture("_MainTex", LocalSkinTexture);
                                renderer.SetPropertyBlock(localBlock);
                            }
                            else
                            {
                                renderer.SetPropertyBlock(null);
                            }
                        }
                    }
                }

                if (NetworkManager.puppet != null)
                {
                    var sprite = NetworkManager.puppet.GetComponentInChildren<tk2dSprite>();
                    if (sprite != null)
                    {
                        var renderer = sprite.GetComponent<MeshRenderer>();
                        if (renderer != null)
                        {
                            if (RemoteSkinTexture != null)
                            {
                                if (remoteBlock == null)
                                {
                                    remoteBlock = new MaterialPropertyBlock();
                                }
                                renderer.GetPropertyBlock(remoteBlock);
                                remoteBlock.SetTexture("_MainTex", RemoteSkinTexture);
                                renderer.SetPropertyBlock(remoteBlock);
                            }
                            else
                            {
                                renderer.SetPropertyBlock(null);
                            }
                        }
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

        private static Texture2D LoadExtraTexture(string skinName, string fileName)
        {
            if (skinName == "Default") return null;
            try
            {
                string skinPath = Path.Combine(skinsDir, skinName);
                string fullPath = Path.Combine(skinPath, fileName);
                if (File.Exists(fullPath))
                {
                    return LoadTexture(fullPath);
                }
            }
            catch (Exception ex)
            {
                BetterMultiplayer.Instance.LogError($"Error loading extra texture {fileName} for skin {skinName}: " + ex);
            }
            return null;
        }

        private static MaterialPropertyBlock cloakBlock;
        private static MaterialPropertyBlock remoteCloakBlock;
 
        public static void UpdateCloakSkin()
        {
            if (HeroController.instance != null)
            {
                Transform cloakTransform = HeroController.instance.transform.Find("Cloak");
                if (cloakTransform != null)
                {
                    var renderer = cloakTransform.GetComponent<MeshRenderer>();
                    if (renderer != null)
                    {
                        if (LocalCloakTexture != null)
                        {
                            if (cloakBlock == null) cloakBlock = new MaterialPropertyBlock();
                            renderer.GetPropertyBlock(cloakBlock);
                            cloakBlock.SetTexture("_MainTex", LocalCloakTexture);
                            renderer.SetPropertyBlock(cloakBlock);
                        }
                        else
                        {
                            renderer.SetPropertyBlock(null);
                        }
                    }
                }
            }

            if (NetworkManager.puppet != null)
            {
                Transform cloakTransform = NetworkManager.puppet.transform.Find("Cloak");
                if (cloakTransform != null)
                {
                    var renderer = cloakTransform.GetComponent<MeshRenderer>();
                    if (renderer != null)
                    {
                        if (RemoteCloakTexture != null)
                        {
                            if (remoteCloakBlock == null) remoteCloakBlock = new MaterialPropertyBlock();
                            renderer.GetPropertyBlock(remoteCloakBlock);
                            remoteCloakBlock.SetTexture("_MainTex", RemoteCloakTexture);
                            renderer.SetPropertyBlock(remoteCloakBlock);
                        }
                        else
                        {
                            renderer.SetPropertyBlock(null);
                        }
                    }
                }
            }
        }
 
        public static void UpdateHUDSkin()
        {
            if (LocalHUDTexture == null && LocalOrbFullTexture == null) return;
 
            GameObject hudCanvas = GameObject.Find("Hud Camera/Hud Canvas");
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
                string objName = parent.name;

                if (LocalHUDTexture != null && (texName.IndexOf("hud", StringComparison.OrdinalIgnoreCase) >= 0 || objName.IndexOf("health", StringComparison.OrdinalIgnoreCase) >= 0 || objName.IndexOf("geo", StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    var block = new MaterialPropertyBlock();
                    sr.GetPropertyBlock(block);
                    block.SetTexture("_MainTex", LocalHUDTexture);
                    sr.SetPropertyBlock(block);
                }
                else if (LocalOrbFullTexture != null && (texName.IndexOf("orbfull", StringComparison.OrdinalIgnoreCase) >= 0 || texName.IndexOf("soul_orb", StringComparison.OrdinalIgnoreCase) >= 0 || objName.IndexOf("orb", StringComparison.OrdinalIgnoreCase) >= 0 || objName.IndexOf("vessel", StringComparison.OrdinalIgnoreCase) >= 0))
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
                string objName = parent.name;

                if (LocalHUDTexture != null && (colName.IndexOf("hud", StringComparison.OrdinalIgnoreCase) >= 0 || objName.IndexOf("health", StringComparison.OrdinalIgnoreCase) >= 0 || objName.IndexOf("geo", StringComparison.OrdinalIgnoreCase) >= 0))
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
                else if (LocalOrbFullTexture != null && (colName.IndexOf("orbfull", StringComparison.OrdinalIgnoreCase) >= 0 || colName.IndexOf("soul_orb", StringComparison.OrdinalIgnoreCase) >= 0 || objName.IndexOf("orb", StringComparison.OrdinalIgnoreCase) >= 0 || objName.IndexOf("vessel", StringComparison.OrdinalIgnoreCase) >= 0))
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
    }
 
    [HarmonyPatch(typeof(tk2dSprite), "Start")]
    public static class tk2dSprite_Start_Patch
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
                    if (SkinManager.RemoteVSTexture != null && 
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
                             (name.Contains("Dash Effect") || name.Contains("dash") || name.Contains("Dash") || name.Contains("sprint") || name.Contains("Sprint")))
                    {
                        ApplyTexture(__instance, SkinManager.RemoteSprintTexture);
                    }
                }
                else
                {
                    if (SkinManager.LocalVSTexture != null && 
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
                             (name.StartsWith("Dash Effect") || name.Contains("dash") || name.Contains("Dash") || name.Contains("sprint") || name.Contains("Sprint")))
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
                    BetterMultiplayer.Instance.LogError("Error in tk2dSprite_Start_Patch: " + ex);
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
}
