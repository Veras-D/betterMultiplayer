using System;
using UnityEngine;

namespace BetterMultiplayer
{
    public class RemotePlayerPuppet : MonoBehaviour
    {
        public string username;
        private tk2dSpriteAnimator animator;
        private tk2dSprite sprite;

        public static GameObject CreatePuppet(string name)
        {
            if (HeroController.instance == null) return null;

            try
            {
                // Create a clean empty GameObject to avoid script/logic/physics conflicts
                GameObject puppet = new GameObject(name);
                UnityEngine.Object.DontDestroyOnLoad(puppet);

                var localSprite = HeroController.instance.GetComponentInChildren<tk2dSprite>();
                var localAnimator = HeroController.instance.GetComponentInChildren<tk2dSpriteAnimator>();
                var localRenderer = HeroController.instance.GetComponentInChildren<MeshRenderer>();

                if (localSprite != null)
                {
                    tk2dSprite.AddComponent(puppet, localSprite.Collection, localSprite.spriteId);
                }

                if (localAnimator != null)
                {
                    tk2dSpriteAnimator.AddComponent(puppet, localAnimator.Library, localAnimator.DefaultClipId);
                }

                var puppetRenderer = puppet.GetComponent<MeshRenderer>();
                if (localRenderer != null && puppetRenderer != null)
                {
                    puppetRenderer.sharedMaterials = localRenderer.sharedMaterials;
                }

                // Set layer to Ignore Raycast (Layer 2) to bypass player physics
                puppet.layer = 2;

                // Attach our remote puppet controller
                var puppetCtrl = puppet.AddComponent<RemotePlayerPuppet>();
                puppetCtrl.username = name;
                puppetCtrl.animator = puppet.GetComponent<tk2dSpriteAnimator>();
                puppetCtrl.sprite = puppet.GetComponent<tk2dSprite>();

                BetterMultiplayer.Instance.Log("Successfully spawned clean visual puppet for " + name);
                return puppet;
            }
            catch (Exception ex)
            {
                BetterMultiplayer.Instance.LogError("Error in CreatePuppet: " + ex);
                return null;
            }
        }

        public void UpdatePosition(float x, float y, float scaleX, string animName)
        {
            transform.position = new Vector3(x, y, transform.position.z);
            transform.localScale = new Vector3(scaleX, 1f, 1f);

            // Self-heal / initialize if components were not ready during CreatePuppet
            if (sprite == null && HeroController.instance != null)
            {
                var localSprite = HeroController.instance.GetComponentInChildren<tk2dSprite>();
                if (localSprite != null)
                {
                    sprite = tk2dSprite.AddComponent(gameObject, localSprite.Collection, localSprite.spriteId);
                    
                    var localRenderer = HeroController.instance.GetComponentInChildren<MeshRenderer>();
                    var puppetRenderer = GetComponent<MeshRenderer>();
                    if (localRenderer != null && puppetRenderer != null)
                    {
                        puppetRenderer.sharedMaterials = localRenderer.sharedMaterials;
                    }
                }
            }

            if (animator == null && HeroController.instance != null)
            {
                var localAnimator = HeroController.instance.GetComponentInChildren<tk2dSpriteAnimator>();
                if (localAnimator != null)
                {
                    animator = tk2dSpriteAnimator.AddComponent(gameObject, localAnimator.Library, localAnimator.DefaultClipId);
                }
            }
            
            if (animator != null && !string.IsNullOrEmpty(animName))
            {
                if (animator.CurrentClip == null || animator.CurrentClip.name != animName)
                {
                    string oldClip = animator.CurrentClip != null ? animator.CurrentClip.name : "";
                    animator.Play(animName);
                    OnAnimationChanged(oldClip, animName);
                }
            }
        }

        private static GameObject GetHeroField(string fieldName)
        {
            if (HeroController.instance == null) return null;
            try
            {
                var field = typeof(HeroController).GetField(fieldName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                return field?.GetValue(HeroController.instance) as GameObject;
            }
            catch
            {
                return null;
            }
        }

        private void OnAnimationChanged(string oldClip, string newClip)
        {
            try
            {
                if (HeroController.instance == null) return;

                if (newClip.StartsWith("attack") || newClip.Contains("slash"))
                {
                    GameObject prefab = null;
                    if (newClip.Contains("up"))
                    {
                        prefab = GetHeroField("upSlashPrefab");
                    }
                    else if (newClip.Contains("down"))
                    {
                        prefab = GetHeroField("downSlashPrefab");
                    }
                    else
                    {
                        prefab = GetHeroField("slashPrefab");
                    }

                    if (prefab != null)
                    {
                        GameObject slash = Instantiate(prefab, transform.position, transform.rotation);
                        slash.transform.localScale = transform.localScale;
                        SanitizeEffect(slash);
                        Destroy(slash, 0.3f);
                    }
                }
                else if (newClip == "dash")
                {
                    GameObject prefab = GetHeroField("dashParticlesPrefab");
                    if (prefab != null)
                    {
                        GameObject dash = Instantiate(prefab, transform.position, transform.rotation);
                        dash.transform.localScale = transform.localScale;
                        SanitizeEffect(dash);
                        Destroy(dash, 0.5f);
                    }
                }
                else if (newClip.Contains("double_jump") || newClip.Contains("d_jump"))
                {
                    GameObject prefab = GetHeroField("dJumpWingsPrefab");
                    if (prefab != null)
                    {
                        GameObject wings = Instantiate(prefab, transform.position, transform.rotation);
                        wings.transform.localScale = transform.localScale;
                        SanitizeEffect(wings);
                        Destroy(wings, 0.6f);
                    }
                }
            }
            catch (Exception ex)
            {
                BetterMultiplayer.Instance.LogError("Error in RemotePlayerPuppet.OnAnimationChanged: " + ex);
            }
        }

        private void SanitizeEffect(GameObject effectObj)
        {
            if (effectObj == null) return;
            try
            {
                // Set layer to Ignore Raycast to prevent any physics interactions
                effectObj.layer = 2;

                foreach (var col in effectObj.GetComponentsInChildren<Collider2D>(true))
                {
                    Destroy(col);
                }

                // Strip FSMs and other game-logic scripts so it's strictly visual
                foreach (var comp in effectObj.GetComponentsInChildren<Component>(true))
                {
                    if (comp == null) continue;
                    
                    if (comp is PlayMakerFSM)
                    {
                        Destroy(comp);
                    }
                    else
                    {
                        string typeName = comp.GetType().FullName;
                        if (typeName != null && 
                            !typeName.StartsWith("UnityEngine.Mesh") && 
                            !typeName.StartsWith("UnityEngine.Transform") && 
                            !typeName.StartsWith("UnityEngine.ParticleSystem") && 
                            !typeName.StartsWith("UnityEngine.Sprite") && 
                            !typeName.StartsWith("UnityEngine.Renderer") && 
                            !typeName.StartsWith("UnityEngine.Filter") &&
                            !typeName.StartsWith("tk2d"))
                        {
                            if (!(comp is Transform) && !(comp is Renderer) && !(comp is MeshFilter) && comp.GetType().Name != "ParticleSystem")
                            {
                                Destroy(comp);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                BetterMultiplayer.Instance.LogError("Error sanitizing effect: " + ex);
            }
        }
    }
}
