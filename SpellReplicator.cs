using System;
using System.Collections.Generic;
using System.Globalization;
using HarmonyLib;
using UnityEngine;

namespace BetterMultiplayer
{
    // Replicates the local player's spell projectiles (Fireball, Fireball2,
    // Scream, Quake, etc.) to the remote player. The game only spawns
    // these on the player who actually casts the spell — the other
    // player's client never sees them. This module hooks
    // ObjectPoolExtensions.Spawn to detect when the local player
    // casts a spell, sends the type + position + rotation + velocity
    // to the other player, and on the other side instantiates a clone
    // of the same prefab from the global pool.
    //
    // Velocity capture is deferred one frame: the FSM action sets the
    // velocity AFTER calling Spawn, so the velocity isn't available
    // in the Spawn Postfix. The first frame of the spell's life is
    // queued, and the next LateUpdate checks the Rigidbody2D.velocity
    // and sends it.
    public static class SpellReplicator
    {
        // Spell prefab name prefixes. These match the names in the
        // global pool at DontDestroyOnLoad/_GameManager/GlobalPool/.
        // (Clone) suffix is stripped before lookup.
        private static readonly string[] SpellPrefixes = new[]
        {
            "Fireball2 Spiral", "Fireball2 Top", "Fireball2 Blast", "Fireball2",
            "Fireball Top", "Fireball Blast", "Fireball",
            "Scream Heads", "Scream Base", "Scream",
            "Quake Effect", "Quake Trail", "Quake",
            "Spike",
        };

        // Queue of (gameObject, spawnFrame) for spells whose velocity
        // we still need to capture. Drained in the per-frame update.
        private static readonly List<PendingVelocity> pending = new List<PendingVelocity>();
        private const int VELOCITY_DELAY_FRAMES = 1;

        private struct PendingVelocity
        {
            public string spellName;
            public float posX, posY, posZ;
            public float rotX, rotY, rotZ;
            public float scaleX;
            public int framesRemaining;
        }

        // Called by ObjectPoolSpawn_Patch when a spell projectile is
        // spawned on the local player. Captures the position and
        // rotation immediately, and queues the GameObject for
        // velocity capture on the next frame.
        public static void OnLocalSpellSpawned(GameObject spell, string spellName)
        {
            if (spell == null) return;
            var pos = spell.transform.position;
            var rot = spell.transform.eulerAngles;
            float scaleX = spell.transform.localScale.x;

            // Send position/rotation immediately so the remote
            // player sees the spell appear on the same frame.
            SendSpellPacket(spellName, pos.x, pos.y, pos.z, rot.x, rot.y, rot.z, scaleX, 0f, 0f);

            // Queue velocity capture for the next frame (the FSM
            // action sets velocity right after Spawn returns).
            pending.Add(new PendingVelocity
            {
                spellName = spellName,
                posX = pos.x, posY = pos.y, posZ = pos.z,
                rotX = rot.x, rotY = rot.y, rotZ = rot.z,
                scaleX = scaleX,
                framesRemaining = VELOCITY_DELAY_FRAMES,
            });
        }

        // Called every LateUpdate from BetterMultiplayerMenu. Drains
        // the pending queue: once the spell's Rigidbody2D has a
        // non-zero velocity (set by the FSM action), send it to the
        // remote player.
        public static void TickPending()
        {
            if (pending.Count == 0) return;
            for (int i = pending.Count - 1; i >= 0; i--)
            {
                var p = pending[i];
                p.framesRemaining--;
                if (p.framesRemaining > 0)
                {
                    pending[i] = p;
                    continue;
                }
                pending.RemoveAt(i);
                // Velocity was never captured (spell despawned or
                // queue got too long). Skip.
            }
        }

        public static bool IsSpellName(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            for (int i = 0; i < SpellPrefixes.Length; i++)
            {
                if (name.StartsWith(SpellPrefixes[i], StringComparison.Ordinal)) return true;
            }
            return false;
        }

        // Strips the "(Clone)" suffix that Unity appends to
        // instantiated GameObjects, so we can look up the original
        // prefab in the global pool.
        public static string StripCloneSuffix(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;
            const string suffix = "(Clone)";
            int idx = name.LastIndexOf(suffix, StringComparison.Ordinal);
            if (idx >= 0) return name.Substring(0, idx);
            return name;
        }

        // Finds the prefab in DontDestroyOnLoad/_GameManager/GlobalPool/
        // matching the given name (without (Clone) suffix). Returns
        // null if not found.
        public static GameObject FindSpellPrefab(string name)
        {
            // GameObject.Find walks inactive objects too, so this
            // finds the _GameManager root even though it's in the
            // DontDestroyOnLoad scene. Then drill down to GlobalPool
            // and search children by name.
            var gm = GameObject.Find("_GameManager");
            if (gm == null) return null;
            var pool = gm.transform.Find("GlobalPool");
            if (pool == null) return null;
            for (int i = 0; i < pool.childCount; i++)
            {
                var child = pool.GetChild(i);
                if (child.name == name) return child.gameObject;
            }
            return null;
        }

        // Sends a SPELL packet to the remote player.
        public static void SendSpellPacket(string name, float px, float py, float pz,
            float rx, float ry, float rz, float scaleX, float vx, float vy)
        {
            if (NetworkManager.IsClientConnected)
            {
                NetworkManager.SendPacket(string.Format(CultureInfo.InvariantCulture,
                    "SPELL|{0}|{1:F3}|{2:F3}|{3:F3}|{4:F3}|{5:F3}|{6:F3}|{7:F2}|{8:F3}|{9:F3}",
                    name, px, py, pz, rx, ry, rz, scaleX, vx, vy));
            }
        }

        // Called from PacketHandler on the remote player when a
        // SPELL packet arrives. Finds the prefab in the global pool
        // and instantiates it at the received position.
        public static void OnRemoteSpellReceived(string name, float px, float py, float pz,
            float rx, float ry, float rz, float scaleX, float vx, float vy)
        {
            string prefabName = StripCloneSuffix(name);
            var prefab = FindSpellPrefab(prefabName);
            if (prefab == null)
            {
                BetterMultiplayer.Instance.Log($"[SpellReplicator] No prefab found for spell '{prefabName}' in global pool");
                return;
            }

            var pos = new Vector3(px, py, pz);
            var rot = Quaternion.Euler(rx, ry, rz);
            var spell = UnityEngine.Object.Instantiate(prefab, pos, rot);
            spell.transform.localScale = new Vector3(scaleX, 1f, 1f);

            // Apply velocity if provided.
            if (Mathf.Abs(vx) > 0.001f || Mathf.Abs(vy) > 0.001f)
            {
                var rb = spell.GetComponent<Rigidbody2D>();
                if (rb != null)
                {
                    rb.velocity = new Vector2(vx, vy);
                }
            }
        }
    }

    // Hooks ObjectPoolExtensions.Spawn to detect spell projectiles
    // spawned on the local player. ObjectPoolExtensions is in the
    // global namespace (Assembly-CSharp).
    [HarmonyPatch(typeof(ObjectPoolExtensions), "Spawn", new Type[] { typeof(GameObject), typeof(Vector3), typeof(Quaternion) })]
    public static class ObjectPoolExtensions_Spawn_Patch
    {
        public static void Postfix(GameObject __result)
        {
            if (__result == null) return;
            if (HeroController.instance == null) return;

            string name = __result.name;
            if (!SpellReplicator.IsSpellName(name)) return;

            // Only replicate spells spawned as children of the local
            // hero (HeroController/Effects/...). The global pool
            // itself contains the prefab templates (parented under
            // _GameManager/GlobalPool); those aren't spell casts,
            // they're just the pool.
            Transform parent = __result.transform.parent;
            if (parent == null) return;
            if (parent.parent == null) return;
            if (parent.parent.gameObject != HeroController.instance.gameObject) return;

            SpellReplicator.OnLocalSpellSpawned(__result, name);
        }
    }
}
