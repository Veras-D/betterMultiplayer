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
                    animator.Play(animName);
                }
            }
        }
    }
}
