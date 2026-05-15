using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Collections;
using System.Reflection;
using UnityEngine;
using AcidemaQuirkMod.Core;
using AcidemaQuirkMod.Systems;
using AcidemaQuirkMod.Abilities;

namespace AcidemaQuirkMod
{
    /// <summary>
    /// WorldBox does not load traits from plain JSON files in mods.
    /// Traits are registered by directly constructing ActorTrait objects
    /// and adding them to AssetManager.traits at startup.
    /// </summary>
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class AcidemaQuirkPlugin : BaseUnityPlugin
    {
        public const string PluginGuid    = "com.acidema.quirkmod";
        public const string PluginName    = "Acidema Quirk Mod";
        public const string PluginVersion = "1.1.0";

        internal static ManualLogSource Log = null!;
        private Harmony _harmony = null!;

        public const string ACID_TRAIT_ID           = "acid_emission";
        public const string ACIDIC_SKIN_TRAIT_ID    = "acidic_skin";
        public const string CORROSIVE_SALIVA_TRAIT_ID = "corrosive_saliva";

        private void Awake()
        {
            Log = Logger;
            Log.LogInfo($"{PluginName} v{PluginVersion} loading...");

            QuirkRegistry.Initialize();

            // Register traits directly into WorldBox's AssetManager
            RegisterTraits();

            var go = new GameObject("QuirkManager");
            go.AddComponent<QuirkManager>();
            DontDestroyOnLoad(go);

            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll();

            Log.LogInfo($"{PluginName} loaded. {QuirkRegistry.All.Count} quirks registered.");
        }

        private void OnDestroy() => _harmony?.UnpatchSelf();

        // ─────────────────────────────────────────
        //  Trait registration
        //  Constructs ActorTrait objects and adds them
        //  directly to AssetManager.traits library.
        // ─────────────────────────────────────────

        internal static void RegisterTraits()
        {
            try
            {
                RegisterAcidEmissionTrait();
                RegisterAcidicSkinTrait();
                RegisterCorrosiveSalivaTrait();
                Log.LogInfo("[Quirk] All traits registered successfully.");
            }
            catch (Exception e)
            {
                Log.LogError($"[Quirk] Failed to register traits: {e.Message}\n{e.StackTrace}");
            }
        }

        private static void SetTraitEditorGroup(dynamic trait)
        {
            const string groupName = "Acidema Quirk Traits";
            const string iconPath = "ui/icons/achievements/achievements_thedemon";

            TrySetTraitMember(trait, "traitCategory", groupName);
            TrySetTraitMember(trait, "category", groupName);
            TrySetTraitMember(trait, "editorCategory", groupName);
            TrySetTraitMember(trait, "traitGroup", groupName);
            TrySetTraitMember(trait, "editorGroup", groupName);
            TrySetTraitMember(trait, "group", groupName);
            TrySetTraitMember(trait, "tab", groupName);
            TrySetTraitMember(trait, "trait_box", groupName);
            TrySetTraitMember(trait, "box", groupName);
            TrySetTraitMember(trait, "showInEditor", true);
            TrySetTraitMember(trait, "isEditorTrait", true);
            TrySetTraitMember(trait, "showInTraitEditor", true);
            TrySetTraitMember(trait, "path_icon", iconPath);
            TrySetTraitMember(trait, "iconPath", iconPath);
        }

        private static void TrySetTraitMember(dynamic trait, string memberName, object value)
        {
            if (trait == null || string.IsNullOrEmpty(memberName)) return;

            object? traitObj = trait;
            var type = traitObj?.GetType();
            if (type == null) return;

            var field = type.GetField(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            if (field != null)
            {
                field.SetValue(traitObj, value);
                return;
            }

            var prop = type.GetProperty(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            if (prop != null && prop.CanWrite)
                prop.SetValue(traitObj, value, null);
        }

        private static void UnlockTrait(string traitId)
        {
            if (string.IsNullOrEmpty(traitId)) return;
            DynamicBridge.CallStaticMethod("PlayerConfig", "unlockTrait", traitId);
        }

        private static void AddTraitLocalization(string id, string displayName, string description)
        {
            if (string.IsNullOrEmpty(id)) return;

            var localizedTextManagerType = FindType("LocalizedTextManager");
            if (localizedTextManagerType == null) return;

            object? instance = null;
            var instanceProp = localizedTextManagerType.GetProperty("instance", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            if (instanceProp != null)
                instance = instanceProp.GetValue(null);
            if (instance == null)
            {
                var instanceField = localizedTextManagerType.GetField("instance", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                if (instanceField != null)
                    instance = instanceField.GetValue(null);
            }

            if (instance == null) return;

            var localizedText = DynamicBridge.GetMemberValue(instance, "localizedText")
                               ?? DynamicBridge.GetMemberValue(instance, "localizedTexts")
                               ?? DynamicBridge.GetMemberValue(instance, "localizedString")
                               ?? DynamicBridge.GetMemberValue(instance, "dictionary");

            if (localizedText is IDictionary dict)
            {
                AddDictionaryEntry(dict, "trait_" + id, displayName);
                AddDictionaryEntry(dict, "trait_" + id + "_info", description);
            }
        }

        private static void AddDictionaryEntry(IDictionary dict, string key, string value)
        {
            if (dict.Contains(key)) return;
            dict.Add(key, value);
        }

        private static Type? FindType(string typeName)
        {
            if (string.IsNullOrEmpty(typeName)) return null;
            var type = Type.GetType(typeName);
            if (type != null) return type;

            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                type = asm.GetType(typeName);
                if (type != null) return type;
            }

            return null;
        }

        private static void RegisterAcidEmissionTrait()
        {
            // Don't register twice (e.g. on scene reload)
            if (AssetManager.traits.get(ACID_TRAIT_ID) != null) return;

            // Use reflection to create ActorTrait
            var traitType = typeof(Actor).Assembly.GetType("ActorTrait");
            if (traitType == null) return;
            dynamic trait = System.Activator.CreateInstance(traitType);

            trait.id = ACID_TRAIT_ID;
            trait.name = "Acid Emission";
            trait.description = "Generates and controls corrosive acid. " +
                                "Body chemistry resists acid naturally, but overuse " +
                                "dissolves even their hardened tissues.";

            // base_stats — use reflection
            var baseStatsType = typeof(Actor).Assembly.GetType("BaseStats");
            if (baseStatsType != null)
            {
                dynamic baseStats = System.Activator.CreateInstance(baseStatsType);
                baseStats.damage = 1.25f;
                baseStats.armor = 0.9f;
                baseStats.health = 0.95f;
                baseStats.speed = 1.0f;
                baseStats.attackRange = 1.5f;
                baseStats.critical_damage_multiplier = 1.05f;
                trait.base_stats = baseStats;
            }

            // Trait editor grouping
            SetTraitEditorGroup(trait);

            // Trait flags
            trait.is_weapon_trait = false;
            trait.can_receive_traits = true;
            trait.can_edit_traits = true;
            trait.spawn_random_trait_allowed = false;

            // Sub-traits applied alongside this one
            trait.effects_traits = new System.Collections.Generic.List<string>
            {
                ACIDIC_SKIN_TRAIT_ID,
                CORROSIVE_SALIVA_TRAIT_ID
            };

            AssetManager.traits.add(trait);
            UnlockTrait(ACID_TRAIT_ID);
            AddTraitLocalization(ACID_TRAIT_ID, "Acid Emission", trait.description);
            Log.LogInfo($"[Quirk] Registered trait: {ACID_TRAIT_ID}");
        }

        private static void RegisterAcidicSkinTrait()
        {
            if (AssetManager.traits.get(ACIDIC_SKIN_TRAIT_ID) != null) return;

            var traitType = typeof(Actor).Assembly.GetType("ActorTrait");
            if (traitType == null) return;
            dynamic trait = System.Activator.CreateInstance(traitType);
            trait.id = ACIDIC_SKIN_TRAIT_ID;
            trait.name = "Acidic Skin";
            trait.description = "Skin secretes acid on contact. " +
                                "Melee attackers take damage and have their armor corroded.";

            var baseStatsType = typeof(Actor).Assembly.GetType("BaseStats");
            if (baseStatsType != null)
            {
                dynamic baseStats = System.Activator.CreateInstance(baseStatsType);
                baseStats.armor = 0.85f;
                trait.base_stats = baseStats;
            }

            SetTraitEditorGroup(trait);
            trait.is_weapon_trait = false;
            trait.can_receive_traits = true;
            trait.can_edit_traits = false;
            trait.spawn_random_trait_allowed = false;

            AssetManager.traits.add(trait);
            UnlockTrait(ACIDIC_SKIN_TRAIT_ID);
            AddTraitLocalization(ACIDIC_SKIN_TRAIT_ID, "Acidic Skin", trait.description);
            Log.LogInfo($"[Quirk] Registered trait: {ACIDIC_SKIN_TRAIT_ID}");
        }

        private static void RegisterCorrosiveSalivaTrait()
        {
            if (AssetManager.traits.get(CORROSIVE_SALIVA_TRAIT_ID) != null) return;

            var traitType = typeof(Actor).Assembly.GetType("ActorTrait");
            if (traitType == null) return;
            dynamic trait = System.Activator.CreateInstance(traitType);
            trait.id = CORROSIVE_SALIVA_TRAIT_ID;
            trait.name = "Corrosive Saliva";
            trait.description = "Bite attacks apply an acid debuff that melts armor over time.";

            var baseStatsType = typeof(Actor).Assembly.GetType("BaseStats");
            if (baseStatsType != null)
            {
                dynamic baseStats = System.Activator.CreateInstance(baseStatsType);
                baseStats.damage = 1.1f;
                trait.base_stats = baseStats;
            }

            SetTraitEditorGroup(trait);
            trait.is_weapon_trait = true;
            trait.can_receive_traits = true;
            trait.can_edit_traits = false;
            trait.spawn_random_trait_allowed = false;
            trait.give_status_id = "poisoned";

            AssetManager.traits.add(trait);
            UnlockTrait(CORROSIVE_SALIVA_TRAIT_ID);
            AddTraitLocalization(CORROSIVE_SALIVA_TRAIT_ID, "Corrosive Saliva", trait.description);
            Log.LogInfo($"[Quirk] Registered trait: {CORROSIVE_SALIVA_TRAIT_ID}");
        }
    }

    [HarmonyPatch(typeof(ActorTraitLibrary), MethodType.Constructor)]
    public static class Patch_AssetsLoaded
    {
        [HarmonyPostfix]
        public static void Postfix()
        {
            AcidemaQuirkPlugin.RegisterTraits();
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  PATCH 1 — Actor.addActorTrait(ActorTrait)
    //  Fires whenever any trait is added to any actor.
    //  If the trait is acid_emission, register a QuirkInstance.
    // ─────────────────────────────────────────────────────────────

    [HarmonyPatch(typeof(Actor), "addActorTrait")]
    public static class Patch_TraitAdded
    {
        [HarmonyPostfix]
        public static void Postfix(Actor __instance, ActorTrait pTrait)
        {
            if (pTrait == null || pTrait.id != AcidemaQuirkPlugin.ACID_TRAIT_ID) return;
            if (!WorldBoxApi.TryGetActorId(__instance, out string unitId)) return;

            if (QuirkManager.Instance.TryGetQuirk(unitId, out _)) return;

            QuirkManager.Instance.AssignQuirk(unitId, AcidemaQuirkPlugin.ACID_TRAIT_ID);
            AcidemaQuirkPlugin.Log.LogInfo(
                $"[Quirk] acid_emission trait detected on {unitId} — QuirkInstance created.");
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  PATCH 2 — Actor.removeTrait(ActorTrait)
    //  Cleans up QuirkInstance when trait is removed.
    // ─────────────────────────────────────────────────────────────

    [HarmonyPatch(typeof(Actor), "removeTrait")]
    public static class Patch_TraitRemoved
    {
        [HarmonyPostfix]
        public static void Postfix(Actor __instance, ActorTrait pTrait)
        {
            if (pTrait == null || pTrait.id != AcidemaQuirkPlugin.ACID_TRAIT_ID) return;
            if (!WorldBoxApi.TryGetActorId(__instance, out string unitId)) return;

            QuirkManager.Instance.Deactivate(unitId);
            QuirkManager.Instance.RemoveQuirk(unitId);

            AcidemaQuirkPlugin.Log.LogInfo(
                $"[Quirk] acid_emission removed from {unitId} — QuirkInstance cleaned up.");
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  PATCH 3 — Actor.updateStatus()
    //  Safety sync for save/load + ticks acid aura each frame.
    // ─────────────────────────────────────────────────────────────

    [HarmonyPatch(typeof(Actor), "updateStatus")]
    public static class Patch_UpdateStatus
    {
        [HarmonyPostfix]
        public static void Postfix(Actor __instance)
        {
            if (!WorldBoxApi.IsActorAlive(__instance)) return;
            if (!WorldBoxApi.TryGetActorId(__instance, out string unitId)) return;

            // Safety sync — catches actors loaded from save files
            if (WorldBoxApi.ActorHasTrait(__instance, AcidemaQuirkPlugin.ACID_TRAIT_ID))
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
                AcidAbilities.TickAcidAura(unitId, Time.deltaTime);
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  PATCH 4 — Actor.unitDied()
    //  Awards kill XP to the killer.
    // ─────────────────────────────────────────────────────────────

    [HarmonyPatch(typeof(Actor), "unitDied")]
    public static class Patch_UnitDied
    {
        [HarmonyPostfix]
        public static void Postfix(Actor __instance)
        {
            string? killerId = WorldBoxApi.GetActorKillActionId(__instance);
            if (!string.IsNullOrEmpty(killerId))
                QuirkManager.Instance.RegisterKill(killerId!);
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  PATCH 5 — Actor.getHit(float)
    //  Detects near-death for Awakening stress trigger.
    // ─────────────────────────────────────────────────────────────

    [HarmonyPatch(typeof(Actor), "getHit")]
    public static class Patch_GetHit
    {
        [HarmonyPostfix]
        public static void Postfix(Actor __instance)
        {
            if (!WorldBoxApi.IsActorAlive(__instance)) return;
            if (!WorldBoxApi.TryGetActorId(__instance, out string unitId)) return;

            float hp    = WorldBoxApi.GetActorHealth(__instance);
            float maxHp = WorldBoxApi.GetActorMaxHealth(__instance);

            if (maxHp > 0f && (hp / maxHp) < 0.10f)
                QuirkManager.Instance.RegisterNearDeath(unitId);
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  PATCH 6 — Actor.damage(float)
    //  Scales outgoing damage by the attacker's quirk multiplier.
    // ─────────────────────────────────────────────────────────────

    [HarmonyPatch(typeof(Actor), "damage")]
    public static class Patch_DamageCalc
    {
        [HarmonyPrefix]
        public static void Prefix(Actor __instance, ref float pDamage)
        {
            if (!WorldBoxApi.TryGetActorId(__instance, out string unitId)) return;

            float speed = 1f;
            float hp    = 1f;
            float armor = 1f;

            QuirkManager.Instance.ApplyQuirkModifiers(
                unitId, ref pDamage, ref speed, ref hp, ref armor);
        }
    }
}