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

                var spriteTransform = HeroController.instance.transform.Find("Sprite");
                var localSprite = spriteTransform != null ? spriteTransform.GetComponent<tk2dSprite>() : HeroController.instance.GetComponentInChildren<tk2dSprite>();
                var localAnimator = spriteTransform != null ? spriteTransform.GetComponent<tk2dSpriteAnimator>() : HeroController.instance.GetComponentInChildren<tk2dSpriteAnimator>();
                var localRenderer = spriteTransform != null ? spriteTransform.GetComponent<MeshRenderer>() : HeroController.instance.GetComponentInChildren<MeshRenderer>();

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

                // Create Cloak child to support custom cloak skins and animations
                Transform localCloak = HeroController.instance.transform.Find("Cloak");
                if (localCloak != null)
                {
                    GameObject puppetCloak = new GameObject("Cloak");
                    puppetCloak.transform.SetParent(puppet.transform);
                    puppetCloak.transform.localPosition = localCloak.localPosition;
                    puppetCloak.transform.localRotation = localCloak.localRotation;
                    puppetCloak.transform.localScale = localCloak.localScale;

                    var localCloakSprite = localCloak.GetComponent<tk2dSprite>();
                    if (localCloakSprite != null)
                    {
                        tk2dSprite.AddComponent(puppetCloak, localCloakSprite.Collection, localCloakSprite.spriteId);
                    }

                    var localCloakAnimator = localCloak.GetComponent<tk2dSpriteAnimator>();
                    if (localCloakAnimator != null)
                    {
                        tk2dSpriteAnimator.AddComponent(puppetCloak, localCloakAnimator.Library, localCloakAnimator.DefaultClipId);
                    }

                    var localCloakRenderer = localCloak.GetComponent<MeshRenderer>();
                    var puppetCloakRenderer = puppetCloak.GetComponent<MeshRenderer>();
                    if (localCloakRenderer != null && puppetCloakRenderer != null)
                    {
                        puppetCloakRenderer.sharedMaterials = localCloakRenderer.sharedMaterials;
                    }
                    
                    puppetCloak.layer = 2;
                }

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
                var spriteTransform = HeroController.instance.transform.Find("Sprite");
                var localSprite = spriteTransform != null ? spriteTransform.GetComponent<tk2dSprite>() : HeroController.instance.GetComponentInChildren<tk2dSprite>();
                if (localSprite != null)
                {
                    sprite = tk2dSprite.AddComponent(gameObject, localSprite.Collection, localSprite.spriteId);
                    
                    var localRenderer = spriteTransform != null ? spriteTransform.GetComponent<MeshRenderer>() : HeroController.instance.GetComponentInChildren<MeshRenderer>();
                    var puppetRenderer = GetComponent<MeshRenderer>();
                    if (localRenderer != null && puppetRenderer != null)
                    {
                        puppetRenderer.sharedMaterials = localRenderer.sharedMaterials;
                    }
                }
            }

            if (animator == null && HeroController.instance != null)
            {
                var spriteTransform = HeroController.instance.transform.Find("Sprite");
                var localAnimator = spriteTransform != null ? spriteTransform.GetComponent<tk2dSpriteAnimator>() : HeroController.instance.GetComponentInChildren<tk2dSpriteAnimator>();
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

            // Sync cloak animation if the cloak exists
            var rCloak = transform.Find("Cloak");
            if (rCloak != null)
            {
                var cloakAnim = rCloak.GetComponent<tk2dSpriteAnimator>();
                if (cloakAnim != null && !string.IsNullOrEmpty(animName))
                {
                    if (cloakAnim.Library != null && cloakAnim.Library.GetClipByName(animName) != null)
                    {
                        cloakAnim.Play(animName);
                    }
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

        private static GameObject GetSpellPrefab(string fsmVariableName)
        {
            if (HeroController.instance == null) return null;
            try
            {
                foreach (var fsm in HeroController.instance.GetComponents<PlayMakerFSM>())
                {
                    if (fsm.FsmName == "Spell Control")
                    {
                        var variable = fsm.FsmVariables.GetFsmGameObject(fsmVariableName);
                        if (variable != null)
                        {
                            return variable.Value;
                        }
                    }
                }
            }
            catch {}
            return null;
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
                        slash.name = "Remote_" + slash.name;
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
                        dash.name = "Remote_" + dash.name;
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
                        wings.name = "Remote_" + wings.name;
                        wings.transform.localScale = transform.localScale;
                        SanitizeEffect(wings);
                        Destroy(wings, 0.6f);
                    }
                }
                else if (newClip.Contains("spell1") || newClip.Contains("fireball"))
                {
                    bool upgraded = newClip.Contains("2") || newClip.Contains("upgraded") || newClip.Contains("void") || newClip.Contains("shadesoul");
                    GameObject prefab = upgraded ? GetSpellPrefab("Fireball 2 Prefab") : GetSpellPrefab("Fireball Prefab");
                    if (prefab == null) prefab = GetSpellPrefab("Fireball Prefab") ?? GetSpellPrefab("Fireball 2 Prefab");

                    if (prefab != null)
                    {
                        GameObject spell = Instantiate(prefab, transform.position, transform.rotation);
                        spell.name = "Remote_" + spell.name;
                        spell.transform.localScale = transform.localScale;
                        SanitizeEffect(spell);
                        Destroy(spell, 1.5f);
                    }
                }
                else if (newClip.Contains("spell2") || newClip.Contains("quake") || newClip.Contains("dive"))
                {
                    bool upgraded = newClip.Contains("2") || newClip.Contains("upgraded") || newClip.Contains("void") || newClip.Contains("dark");
                    GameObject prefab = upgraded ? GetSpellPrefab("Quake 2 Prefab") : GetSpellPrefab("Quake Prefab");
                    if (prefab == null) prefab = GetSpellPrefab("Quake Prefab") ?? GetSpellPrefab("Quake 2 Prefab");

                    if (prefab != null)
                    {
                        GameObject spell = Instantiate(prefab, transform.position, transform.rotation);
                        spell.name = "Remote_" + spell.name;
                        spell.transform.localScale = transform.localScale;
                        SanitizeEffect(spell);
                        Destroy(spell, 1.5f);
                    }
                }
                else if (newClip.Contains("spell4") || newClip.Contains("scream") || newClip.Contains("shriek"))
                {
                    bool upgraded = newClip.Contains("2") || newClip.Contains("upgraded") || newClip.Contains("void") || newClip.Contains("shriek");
                    GameObject prefab = upgraded ? GetSpellPrefab("Scream 2 Prefab") : GetSpellPrefab("Scream Prefab");
                    if (prefab == null) prefab = GetSpellPrefab("Scream Prefab") ?? GetSpellPrefab("Scream 2 Prefab");

                    if (prefab != null)
                    {
                        GameObject spell = Instantiate(prefab, transform.position, transform.rotation);
                        spell.name = "Remote_" + spell.name;
                        spell.transform.localScale = transform.localScale;
                        SanitizeEffect(spell);
                        Destroy(spell, 1.5f);
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

                // Strip other game-logic scripts but KEEP PlayMakerFSM so the visual effect works!
                foreach (var comp in effectObj.GetComponentsInChildren<Component>(true))
                {
                    if (comp == null) continue;
                    
                    if (comp is PlayMakerFSM)
                    {
                        continue;
                    }
                    
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
            catch (Exception ex)
            {
                BetterMultiplayer.Instance.LogError("Error sanitizing effect: " + ex);
            }
        }
    }
}
