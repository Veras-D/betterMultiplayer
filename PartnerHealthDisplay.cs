using System;
using System.Collections.Generic;
using UnityEngine;

namespace BetterMultiplayer
{
    public class PartnerHealthDisplay : MonoBehaviour
    {
        private GameObject partnerHealthbar;
        private List<GameObject> maskClones = new List<GameObject>();
        private int lastMaxHealth = 0;
        private int lastHealthBlue = 0;

        void Update()
        {
            // Only show partner health if connected, and in a gameplay scene with HeroController active
            if (!NetworkManager.IsClientConnected || HeroController.instance == null)
            {
                DestroyHealthbar();
                return;
            }

            GameObject mainHealthbar = GameObject.Find("Healthbar");
            if (mainHealthbar == null || !mainHealthbar.activeInHierarchy)
            {
                DestroyHealthbar();
                return;
            }

            // Create healthbar if not exists
            if (partnerHealthbar == null)
            {
                CreateHealthbar(mainHealthbar);
            }

            // If partner health variables changed, update/rebuild the masks
            int targetMaxHealth = NetworkManager.RemoteMaxHealth;
            int targetHealthBlue = NetworkManager.RemoteHealthBlue;
            int targetHealth = NetworkManager.RemoteHealth;

            if (partnerHealthbar != null)
            {
                // Position it above the player's own health bar, and scale it down slightly
                partnerHealthbar.transform.position = mainHealthbar.transform.position + new Vector3(0.0f, 1.2f, -0.05f);
                
                Vector3 mainLossy = mainHealthbar.transform.lossyScale;
                partnerHealthbar.transform.localScale = new Vector3(
                    Mathf.Abs(mainLossy.x) * 0.65f, 
                    Mathf.Abs(mainLossy.y) * 0.65f, 
                    Mathf.Abs(mainLossy.z) * 0.65f
                );

                if (Time.frameCount % 300 == 0)
                {
                    BetterMultiplayer.Instance.Log($"[HUD Debug] mainHealthbar={mainHealthbar.name}, parent={(mainHealthbar.transform.parent != null ? mainHealthbar.transform.parent.name : "null")}, localPos={mainHealthbar.transform.localPosition}, worldPos={mainHealthbar.transform.position}, scale={mainHealthbar.transform.localScale}, lossyScale={mainHealthbar.transform.lossyScale}, layer={mainHealthbar.layer}");
                    BetterMultiplayer.Instance.Log($"[HUD Debug] partnerHealthbar={partnerHealthbar.name}, parent={(partnerHealthbar.transform.parent != null ? partnerHealthbar.transform.parent.name : "null")}, localPos={partnerHealthbar.transform.localPosition}, worldPos={partnerHealthbar.transform.position}, scale={partnerHealthbar.transform.localScale}, layer={partnerHealthbar.layer}");
                }

                int totalMasksNeeded = targetMaxHealth + targetHealthBlue;
                if (maskClones.Count != totalMasksNeeded || targetMaxHealth != lastMaxHealth || targetHealthBlue != lastHealthBlue)
                {
                    RebuildMasks(mainHealthbar, targetMaxHealth, targetHealthBlue);
                    lastMaxHealth = targetMaxHealth;
                    lastHealthBlue = targetHealthBlue;
                }

                // Update mask visual states
                for (int i = 0; i < maskClones.Count; i++)
                {
                    GameObject mask = maskClones[i];
                    if (mask == null) continue;

                    var animator = mask.GetComponentInChildren<tk2dSpriteAnimator>();
                    if (animator != null)
                    {
                        if (i < targetHealth)
                        {
                            // Full red mask
                            PlayClipSafely(animator, "Idle", "Idle");
                        }
                        else if (i < targetMaxHealth)
                        {
                            // Empty mask
                            PlayClipSafely(animator, "Empty", "Empty");
                        }
                        else
                        {
                            // Blue lifeblood mask
                            PlayClipSafely(animator, "Set Blue", "Blue");
                        }
                    }
                }
            }
        }

        private void CreateHealthbar(GameObject mainHealthbar)
        {
            try
            {
                BetterMultiplayer.Instance.Log("Creating PartnerHealthbar...");
                partnerHealthbar = new GameObject("PartnerHealthbar");
                
                // Parent to Hud Canvas directly to prevent Playmaker HUD's layout/FSM and negative scale interference
                GameObject hudCanvas = GameObject.Find("Hud Canvas");
                if (hudCanvas != null)
                {
                    partnerHealthbar.transform.SetParent(hudCanvas.transform, false);
                }
                else
                {
                    partnerHealthbar.transform.SetParent(mainHealthbar.transform.parent, false);
                }
                partnerHealthbar.layer = mainHealthbar.layer;
                
                // Position it above the player's own health bar, and scale it down slightly
                partnerHealthbar.transform.position = mainHealthbar.transform.position + new Vector3(0.0f, 1.2f, -0.05f);
                
                Vector3 mainLossy = mainHealthbar.transform.lossyScale;
                partnerHealthbar.transform.localScale = new Vector3(
                    Mathf.Abs(mainLossy.x) * 0.65f, 
                    Mathf.Abs(mainLossy.y) * 0.65f, 
                    Mathf.Abs(mainLossy.z) * 0.65f
                );
            }
            catch (Exception ex)
            {
                BetterMultiplayer.Instance.LogError("Error creating PartnerHealthbar: " + ex);
            }
        }

        private void RebuildMasks(GameObject mainHealthbar, int maxHealth, int healthBlue)
        {
            try
            {
                // Find a template mask in the main healthbar first
                GameObject templateMask = null;
                foreach (Transform child in mainHealthbar.transform)
                {
                    if (child.name.IndexOf("health_unit", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        templateMask = child.gameObject;
                        break;
                    }
                }

                if (templateMask == null) return;

                // Clear existing masks only after templateMask is confirmed to exist
                foreach (var mask in maskClones)
                {
                    if (mask != null) Destroy(mask);
                }
                maskClones.Clear();

                // Determine spacing
                float spacing = 0.85f;
                List<Transform> units = new List<Transform>();
                foreach (Transform t in mainHealthbar.transform)
                {
                    if (t.name.IndexOf("health_unit", StringComparison.OrdinalIgnoreCase) >= 0) units.Add(t);
                }
                if (units.Count > 1)
                {
                    spacing = units[1].localPosition.x - units[0].localPosition.x;
                }

                int totalMasks = maxHealth + healthBlue;
                BetterMultiplayer.Instance.Log($"Rebuilding partner masks: MaxHealth={maxHealth}, Blue={healthBlue}, Spacing={spacing}");

                for (int i = 0; i < totalMasks; i++)
                {
                    GameObject maskClone = Instantiate(templateMask, partnerHealthbar.transform);
                    maskClone.transform.localPosition = new Vector3(i * spacing, 0f, 0f);
                    maskClone.transform.localScale = Vector3.one;
                    SetLayerRecursive(maskClone, mainHealthbar.layer);

                    // Ensure the clone is active, even if the template was deactivated (e.g. if the local player took damage)
                    maskClone.SetActive(true);

                    // Disable all PlayMakerFSMs on it so it doesn't run the main player's health logic
                    foreach (var fsm in maskClone.GetComponentsInChildren<PlayMakerFSM>(true))
                    {
                        fsm.enabled = false;
                    }

                    // Ensure all child GameObjects (like Health 1) are active
                    foreach (Transform child in maskClone.transform)
                    {
                        child.gameObject.SetActive(true);
                    }

                    // Force reskin on it immediately
                    var tk2d = maskClone.GetComponentInChildren<tk2dSprite>();
                    if (tk2d != null && tk2d.Collection != null && SkinManager.LocalHUDTexture != null)
                    {
                        SkinManager.ReskinCollection(tk2d.Collection, SkinManager.LocalHUDTexture);
                    }

                    maskClones.Add(maskClone);
                }
            }
            catch (Exception ex)
            {
                BetterMultiplayer.Instance.LogError("Error rebuilding partner masks: " + ex);
            }
        }

        private void SetLayerRecursive(GameObject go, int layer)
        {
            if (go == null) return;
            go.layer = layer;
            foreach (Transform child in go.transform)
            {
                SetLayerRecursive(child.gameObject, layer);
            }
        }

        private void PlayClipSafely(tk2dSpriteAnimator animator, string preferred, string fallback)
        {
            if (animator == null) return;
            if (animator.Library != null)
            {
                foreach (var clip in animator.Library.clips)
                {
                    if (clip != null && clip.name == preferred)
                    {
                        animator.Play(preferred);
                        return;
                    }
                }
                foreach (var clip in animator.Library.clips)
                {
                    if (clip != null && clip.name == fallback)
                    {
                        animator.Play(fallback);
                        return;
                    }
                }
            }
            animator.Play("Idle");
        }

        private void DestroyHealthbar()
        {
            if (partnerHealthbar != null)
            {
                Destroy(partnerHealthbar);
                partnerHealthbar = null;
            }
            maskClones.Clear();
            lastMaxHealth = 0;
            lastHealthBlue = 0;
        }

        void OnDestroy()
        {
            DestroyHealthbar();
        }
    }
}
