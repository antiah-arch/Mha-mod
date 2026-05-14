using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using System.IO;
using UnityEngine;
using AcidemaQuirkMod.Core;
using AcidemaQuirkMod.Systems;

namespace AcidemaQuirkMod
{
    /// <summary>
    /// Main BepInEx plugin entry point.
    ///
    /// How the trait→quirk system works:
    ///   1. traits.json defines "acid_emission" as a real WorldBox ActorTrait.
    ///   2. Any unit that gains the trait (via editor, spawn, book, inheritance)
    ///      is automatically synced into QuirkManager by Patch_TraitSync.
    ///   3. WorldBox's own traitsInherit system handles children naturally —
    ///      no custom hereditary code needed.
    ///   4. If the trait is removed, the quirk instance is also removed.
    /// </summary>
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class AcidemaQuirkPlugin : BaseUnityPlugin
    {
        public const string PluginGuid    = "com.acidema.quirkmod";
        public const string PluginName    = "Acidema Quirk Mod";
        public const string PluginVersion = "1.1.0";

        internal static ManualLogSource Log;
        private Harmony _harmony;

        public const string ACID_TRAIT_ID = "acid_emission";

        private void Awake()
        {
            Log = Logger;
            Log.LogInfo($"{PluginName} v{PluginVersion} loading...");

            QuirkRegistry.Initialize();

            var go = new GameObject("QuirkManager");
            go.AddComponent<QuirkManager>();
            DontDestroyOnLoad(go);

            LoadTraits();

            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll();

            Log.LogInfo($"{PluginName} loaded. {QuirkRegistry.All.Count} quirks registered.");
        }

        private void OnDestroy() => _harmony?.UnpatchSelf();

        private void LoadTraits()
        {
            string path = Path.Combine(Paths.PluginPath,
                "AcidemaQuirkMod", "traits.json");
            string json = File.ReadAllText(path);
            // Load into WorldBox's trait library
            AssetManager.traits.loadFromJson(json);
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  PATCH 1 — Actor.addActorTrait(ActorTrait)
    //
    //  Fires whenever any trait is added to any actor.
    //  If the added trait is "acid_emission", we register the
    //  actor into QuirkManager so the full system activates.
    //
    //  addActorTrait — confirmed method in Actor (Assembly-CSharp)
    //  ActorTrait.id — confirmed field in ActorTrait / BaseTrait
    // ─────────────────────────────────────────────────────────────

    [HarmonyPatch(typeof(Actor), "addActorTrait")]
    public static class Patch_TraitAdded
    {
        [HarmonyPostfix]
        public static void Postfix(Actor __instance, ActorTrait pTrait)
        {
            if (__instance?.data == null || pTrait == null) return;
            if (pTrait.id != AcidemaQuirkPlugin.ACID_TRAIT_ID) return;

            string unitId = __instance.data.id;

            // Don't double-register
            if (QuirkManager.Instance.TryGetQuirk(unitId, out _)) return;

            QuirkManager.Instance.AssignQuirk(unitId, AcidemaQuirkPlugin.ACID_TRAIT_ID);
            AcidemaQuirkPlugin.Log.LogInfo(
                $"[Quirk] acid_emission trait detected on {unitId} — QuirkInstance created.");
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  PATCH 2 — Actor.removeTrait(ActorTrait)
    //
    //  If the acid_emission trait is removed, clean up the
    //  QuirkInstance so no orphaned state remains.
    //
    //  removeTrait — confirmed method in Actor (Assembly-CSharp)
    // ─────────────────────────────────────────────────────────────

    [HarmonyPatch(typeof(Actor), "removeTrait")]
    public static class Patch_TraitRemoved
    {
        [HarmonyPostfix]
        public static void Postfix(Actor __instance, ActorTrait pTrait)
        {
            if (__instance?.data == null || pTrait == null) return;
            if (pTrait.id != AcidemaQuirkPlugin.ACID_TRAIT_ID) return;

            string unitId = __instance.data.id;
            QuirkManager.Instance.Deactivate(unitId);
            QuirkManager.Instance.RemoveQuirk(unitId);

            AcidemaQuirkPlugin.Log.LogInfo(
                $"[Quirk] acid_emission trait removed from {unitId} — QuirkInstance cleaned up.");
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  PATCH 3 — Actor.updateStatus()
    //
    //  Called every update cycle for each actor.
    //  We use this to:
    //    a) Safety-sync: catch any actor that has the trait but
    //       no QuirkInstance yet (e.g. loaded from a save file).
    //    b) Tick the acid aura if the unit has it active.
    //
    //  updateStatus — confirmed method in Actor (Assembly-CSharp)
    //  actor.hasTrait(string) — confirmed method
    // ─────────────────────────────────────────────────────────────

    [HarmonyPatch(typeof(Actor), "updateStatus")]
    public static class Patch_UpdateStatus
    {
        [HarmonyPostfix]
        public static void Postfix(Actor __instance)
        {
            if (__instance?.data == null || !__instance.isAlive()) return;

            string unitId = __instance.data.id;

            // Safety sync — hasTrait confirmed in Assembly-CSharp
            if (__instance.hasTrait(AcidemaQuirkPlugin.ACID_TRAIT_ID))
            {
                if (!QuirkManager.Instance.TryGetQuirk(unitId, out _))
                {
                    QuirkManager.Instance.AssignQuirk(
                        unitId, AcidemaQuirkPlugin.ACID_TRAIT_ID);
                    AcidemaQuirkPlugin.Log.LogInfo(
                        $"[Quirk] Safety-sync: registered {unitId} from save/load.");
                }
            }

            // Tick acid aura (Rank S+)
            if (QuirkManager.Instance.HasAbility(unitId, "acid_aura"))
                Abilities.AcidAbilities.TickAcidAura(unitId, Time.deltaTime);
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  PATCH 4 — Actor.unitDied()
    //
    //  Fires when a unit dies. Awards kill XP to the killer.
    //  unitDied — confirmed method in Actor (Assembly-CSharp)
    //  actor.data.kill_action.id — confirmed field path
    // ─────────────────────────────────────────────────────────────

    [HarmonyPatch(typeof(Actor), "unitDied")]
    public static class Patch_UnitDied
    {
        [HarmonyPostfix]
        public static void Postfix(Actor __instance)
        {
            if (__instance?.data == null) return;

            string killerId = __instance.data.kill_action?.id;
            if (!string.IsNullOrEmpty(killerId))
                QuirkManager.Instance.RegisterKill(killerId);
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  PATCH 5 — Actor.getHit(float)
    //
    //  Fires every time a unit takes damage.
    //  PREFIX:  scales outgoing damage by quirk modifier.
    //  POSTFIX: checks for near-death to trigger Awakening stress.
    //
    //  getHit — confirmed method in Actor (Assembly-CSharp)
    //  actor.data.health / actor.getMaxHealth() — confirmed
    // ─────────────────────────────────────────────────────────────

    [HarmonyPatch(typeof(Actor), "getHit")]
    public static class Patch_GetHit
    {
        // Scale damage if the ATTACKER has the acid quirk active
        // (We intercept via Actor.damage patch below for attacker scaling)

        [HarmonyPostfix]
        public static void Postfix(Actor __instance)
        {
            if (__instance?.data == null || !__instance.isAlive()) return;

            string unitId = __instance.data.id;
            float  hp     = __instance.data.health;
            float  maxHp  = __instance.getMaxHealth();

            // Near-death threshold: below 10% HP
            if (maxHp > 0f && (hp / maxHp) < 0.10f)
                QuirkManager.Instance.RegisterNearDeath(unitId);
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  PATCH 6 — Actor.damage(float)
    //
    //  Fires when an actor deals damage.
    //  Scales pDamage by the attacker's active quirk multiplier.
    //
    //  damage — confirmed method in Actor (Assembly-CSharp)
    // ─────────────────────────────────────────────────────────────

    [HarmonyPatch(typeof(Actor), "damage")]
    public static class Patch_DamageCalc
    {
        [HarmonyPrefix]
        public static void Prefix(Actor __instance, ref float pDamage)
        {
            if (__instance?.data == null) return;

            string unitId = __instance.data.id;
            float  speed  = 1f;
            float  hp     = 1f;
            float  armor  = 1f;

            QuirkManager.Instance.ApplyQuirkModifiers(
                unitId, ref pDamage, ref speed, ref hp, ref armor);
        }
    }
}