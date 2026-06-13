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

                    var animator = mask.GetComponent<tk2dSpriteAnimator>();
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
                partnerHealthbar.transform.SetParent(mainHealthbar.transform.parent, false);
                
                // Position it above the player's own health bar, and scale it down slightly
                partnerHealthbar.transform.localPosition = mainHealthbar.transform.localPosition + new Vector3(0.5f, 0.9f, 0f);
                partnerHealthbar.transform.localScale = mainHealthbar.transform.localScale * 0.65f;
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
                // Clear existing masks
                foreach (var mask in maskClones)
                {
                    if (mask != null) Destroy(mask);
                }
                maskClones.Clear();

                // Find a template mask in the main healthbar
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

                    // Disable all PlayMakerFSMs on it so it doesn't run the main player's health logic
                    foreach (var fsm in maskClone.GetComponents<PlayMakerFSM>())
                    {
                        fsm.enabled = false;
                    }

                    maskClones.Add(maskClone);
                }
            }
            catch (Exception ex)
            {
                BetterMultiplayer.Instance.LogError("Error rebuilding partner masks: " + ex);
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
