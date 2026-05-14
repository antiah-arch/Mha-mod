using System.Collections.Generic;
using UnityEngine;
using AcidemaQuirkMod.Core;

namespace AcidemaQuirkMod.Systems
{
    /// <summary>
    /// Manages all active quirk instances in the world.
    /// All WorldBox API calls use confirmed methods from Assembly-CSharp.dll:
    ///   actor.getHit(float)          — deal damage to a unit
    ///   actor.addActorTrait(trait)   — add a trait to a unit
    ///   actor.hasTrait(string)       — check if a trait is present
    ///   actor.isAlive()              — null/alive guard
    ///   actor.currentTile            — tile the actor stands on
    ///   actor.data.id                — the unit's unique string ID
    ///   EffectsLibrary.spawnCloudAcid(tile) — acid cloud VFX
    ///   World.world.units            — live actor list
    ///   AssetManager.traits.get(id)  — look up an ActorTrait by id
    /// </summary>
    public class QuirkManager : MonoBehaviour
    {
        public static QuirkManager Instance { get; private set; }

        // actor.data.id → QuirkInstance
        private readonly Dictionary<string, QuirkInstance> _unitQuirks =
            new Dictionary<string, QuirkInstance>();

        public delegate void QuirkEvent(string unitId, QuirkInstance quirk);
        public static event QuirkEvent OnQuirkRankUp;
        public static event QuirkEvent OnQuirkAwakened;
        public static event QuirkEvent OnQuirkActivated;
        public static event QuirkEvent OnQuirkDeactivated;

        // ─────────────────────────────────────────
        //  Lifecycle
        // ─────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            QuirkRegistry.Initialize();
        }

        private void Update() => Tick(Time.deltaTime);

        // ─────────────────────────────────────────
        //  Registration
        // ─────────────────────────────────────────

        public QuirkInstance AssignQuirk(string unitId, string quirkId)
        {
            if (!QuirkRegistry.TryGet(quirkId, out var def))
            {
                Debug.LogWarning($"[QuirkManager] Unknown quirk id: {quirkId}");
                return null;
            }

            var instance = new QuirkInstance
            {
                QuirkId    = quirkId,
                Definition = def,
                Factor     = def.Factor,
                Energy     = 100f,
                MaxEnergy  = 100f
            };

            RefreshAbilities(instance);
            _unitQuirks[unitId] = instance;

            // Apply passive mutation traits to the unit via addActorTrait
            dynamic actor = GetActor(unitId);
            if (actor != null && def.Factor?.PassiveMutations != null)
            {
                foreach (var mutationId in def.Factor.PassiveMutations)
                {
                    var trait = WorldBoxApi.GetTrait(mutationId);
                    if (trait != null && !WorldBoxApi.ActorHasTrait(actor, mutationId))
                        WorldBoxApi.AddActorTrait(actor, trait);
                }
            }

            Debug.Log($"[QuirkManager] Assigned '{def.DisplayName}' to unit {unitId}");
            return instance;
        }

        public bool TryGetQuirk(string unitId, out QuirkInstance quirk)
            => _unitQuirks.TryGetValue(unitId, out quirk);

        public void RemoveQuirk(string unitId) => _unitQuirks.Remove(unitId);

        // ─────────────────────────────────────────
        //  Activation / Deactivation
        // ─────────────────────────────────────────

        public bool TryActivate(string unitId)
        {
            if (!TryGetQuirk(unitId, out var q)) return false;
            if (q.IsActive) return true;
            if (q.Cooldown > 0f) return false;
            if (q.Energy <= 0f) return false;

            q.IsActive = true;
            OnQuirkActivated?.Invoke(unitId, q);
            return true;
        }

        public void Deactivate(string unitId)
        {
            if (!TryGetQuirk(unitId, out var q) || !q.IsActive) return;
            q.IsActive = false;
            OnQuirkDeactivated?.Invoke(unitId, q);
        }

        public void Toggle(string unitId)
        {
            if (!TryGetQuirk(unitId, out var q)) return;
            if (q.IsActive) Deactivate(unitId); else TryActivate(unitId);
        }

        // ─────────────────────────────────────────
        //  Main tick
        // ─────────────────────────────────────────

        private void Tick(float dt)
        {
            foreach (var pair in _unitQuirks)
            {
                TickCooldown(pair.Value, dt);
                if (pair.Value.IsActive) TickActive(pair.Key, pair.Value, dt);
                else                     TickRegeneration(pair.Value, dt);
            }
        }

        private void TickCooldown(QuirkInstance q, float dt)
        {
            if (q.Cooldown > 0f) q.Cooldown = Mathf.Max(0f, q.Cooldown - dt);
        }

        private void TickActive(string unitId, QuirkInstance q, float dt)
        {
            var d = q.Definition.Drawback;

            // Stamina drain
            if (d.HasStaminaDrain)
            {
                q.Energy -= d.StaminaDrainPerSec * dt;
                if (q.Energy <= 0f)
                {
                    q.Energy = 0f;
                    Deactivate(unitId);
                    q.Cooldown = 3f;
                    return;
                }
            }

            // Overheat — builds up while active
            if (d.HasOverheat)
            {
                q.Heat += dt * 10f;
                if (q.Heat >= d.OverheatThreshold)
                {
                    ApplySelfDamage(unitId, q, d.OverheatDamagePerSec * dt);
                    if (q.Heat >= d.OverheatThreshold * 1.5f)
                        Deactivate(unitId);
                }
            }
        }

        private void TickRegeneration(QuirkInstance q, float dt)
        {
            // Regen energy and cool heat while quirk is inactive
            q.Energy = Mathf.Min(q.MaxEnergy, q.Energy + 15f * dt);
            q.Heat   = Mathf.Max(0f, q.Heat - 20f * dt);
        }

        // ─────────────────────────────────────────
        //  Damage — Actor.getHit(float pDamage)
        // ─────────────────────────────────────────

        public void ApplySelfDamage(string unitId, QuirkInstance q, float amount)
        {
            float compat = q.Factor?.BodyCompatibility ?? 1f;
            if (compat < q.Definition.Drawback.LowCompatibilityThreshold)
                amount *= q.Definition.Drawback.LowCompatibilityMultiplier;

            dynamic actor = GetActor(unitId);
            if (actor == null || !WorldBoxApi.IsActorAlive(actor)) return;

            // actor.getHit — confirmed method in Actor class (Assembly-CSharp)
            actor.getHit(amount);
        }

        public void ApplyRecoil(string unitId, QuirkInstance q, float damageDealt)
        {
            if (!q.Definition.Drawback.HasRecoil) return;
            ApplySelfDamage(unitId, q, damageDealt * q.Definition.Drawback.RecoilDamagePercent);
        }

        // ─────────────────────────────────────────
        //  XP & Progression
        // ─────────────────────────────────────────

        public void AddXP(string unitId, int amount, string reason = "")
        {
            if (!TryGetQuirk(unitId, out var q)) return;
            if (q.IsAwakened) return;

            q.XP += amount;
            CheckRankUp(unitId, q);
        }

        public void RegisterKill(string unitId)
        {
            if (!TryGetQuirk(unitId, out var q)) return;
            q.KillCount++;
            AddXP(unitId, 25, "kill");
            CheckAwakeningTrigger(unitId, q);
        }

        public void RegisterNearDeath(string unitId)
        {
            if (!TryGetQuirk(unitId, out var q)) return;
            q.NearDeathCount++;
            AddXP(unitId, 150, "near-death");
            CheckAwakeningTrigger(unitId, q);
        }

        private void CheckRankUp(string unitId, QuirkInstance q)
        {
            var nextRank = NextRankAfter(q.CurrentRank);
            if (nextRank == null) return;
            if (!q.Definition.RankThresholds.TryGetValue(nextRank.Value, out int required)) return;
            if (q.XP < required) return;

            q.CurrentRank = nextRank.Value;
            RefreshAbilities(q);
            OnQuirkRankUp?.Invoke(unitId, q);
            Debug.Log($"[Quirk] {unitId} ranked up to {q.CurrentRank}!");
        }

        private void CheckAwakeningTrigger(string unitId, QuirkInstance q)
        {
            if (q.IsAwakened || q.CurrentRank < QuirkRank.S) return;
            bool stress = q.NearDeathCount >= 3;
            bool xp     = q.XP >= q.Definition.RankThresholds[QuirkRank.Awakened];
            if (stress || xp) TriggerAwakening(unitId, q);
        }

        private void TriggerAwakening(string unitId, QuirkInstance q)
        {
            q.IsAwakened  = true;
            q.CurrentRank = QuirkRank.Awakened;
            q.MaxEnergy  *= 1.5f;
            q.Energy      = q.MaxEnergy;

            RefreshAbilities(q);
            OnQuirkAwakened?.Invoke(unitId, q);

            // VFX: acid cloud burst at the unit's current tile
            // EffectsLibrary.spawnCloudAcid — confirmed in Assembly-CSharp
            dynamic actor = GetActor(unitId);
            var tile = WorldBoxApi.GetActorCurrentTile(actor);
            if (tile != null)
                WorldBoxApi.CallEffectsLibrary("spawnCloudAcid", tile);

            Debug.Log($"[Quirk] AWAKENING triggered for {unitId}!");
        }

        // ─────────────────────────────────────────
        //  Ability management
        // ─────────────────────────────────────────

        private void RefreshAbilities(QuirkInstance q)
        {
            q.ActiveAbilities.Clear();
            foreach (var pair in q.Definition.AbilitiesUnlockedAt)
                if (pair.Key <= q.CurrentRank)
                    q.ActiveAbilities.AddRange(pair.Value);
        }

        public bool HasAbility(string unitId, string abilityId)
        {
            if (!TryGetQuirk(unitId, out var q)) return false;
            return q.ActiveAbilities.Contains(abilityId);
        }

        // ─────────────────────────────────────────
        //  Stat application
        // ─────────────────────────────────────────

        public void ApplyQuirkModifiers(string unitId,
            ref float damage, ref float speed, ref float hp, ref float armor)
        {
            if (!TryGetQuirk(unitId, out var q) || !q.IsActive) return;
            var m = q.GetCurrentModifiers();
            damage *= m.DamageMultiplier;
            speed  *= m.SpeedMultiplier;
            hp     *= m.HPMultiplier;
            armor  *= m.ArmorMultiplier;
        }

        // ─────────────────────────────────────────
        //  Actor lookup — World.world.units
        // ─────────────────────────────────────────

        /// <summary>
        /// Finds a live Actor by its data.id.
        /// World.world.units — confirmed list in Assembly-CSharp.
        /// actor.data.id     — confirmed field in ActorData.
        /// </summary>
        public static dynamic GetActor(string unitId)
        {
            if (string.IsNullOrEmpty(unitId)) return null;
            foreach (dynamic actor in WorldBoxApi.GetWorldActors())
            {
                if (WorldBoxApi.GetActorId(actor) == unitId)
                    return actor;
            }
            return null;
        }

        // ─────────────────────────────────────────
        //  Utilities
        // ─────────────────────────────────────────

        private static QuirkRank? NextRankAfter(QuirkRank rank)
        {
            return rank switch
            {
                QuirkRank.E  => QuirkRank.D,
                QuirkRank.D  => QuirkRank.C,
                QuirkRank.C  => QuirkRank.B,
                QuirkRank.B  => QuirkRank.A,
                QuirkRank.A  => QuirkRank.S,
                QuirkRank.S  => QuirkRank.SS,
                QuirkRank.SS => QuirkRank.Awakened,
                _            => null
            };
        }
    }
}