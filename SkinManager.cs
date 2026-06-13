using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace BetterMultiplayer
{
    public static class SkinManager
    {
        public static string SelectedSkin { get; private set; } = "Default";
        public static Texture2D LocalSkinTexture { get; private set; }
        public static Texture2D RemoteSkinTexture { get; private set; }

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
                if (skinName != "Default")
                {
                    string skinPath = Path.Combine(skinsDir, skinName);
                    string knightPng = Path.Combine(skinPath, "Knight.png");
                    if (File.Exists(knightPng))
                    {
                        tex = LoadTexture(knightPng);
                    }
                }

                RemoteSkinTexture = tex;
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
    }
}
