using System;
using System.Collections.Generic;
using UnityEngine;

namespace AcidemaQuirkMod.Core
{
    // ─────────────────────────────────────────
    //  Quirk classification
    // ─────────────────────────────────────────

    public enum QuirkType
    {
        Emitter,        // Creates / controls something external
        Transformation, // Temporarily changes the user's body
        Mutant          // Permanent passive body modification
    }

    public enum ActivationType
    {
        Passive,        // Always active — no input needed
        Toggle,         // Player turns it on/off
        EmotionTrigger, // Fires when anger/fear/hp crosses a threshold
        Gesture,        // Requires a specific action (sprint, jump, etc.)
        Resource        // Consumes an energy bar to fire
    }

    public enum QuirkRank
    {
        E, D, C, B, A, S, SS, Awakened
    }

    // ─────────────────────────────────────────
    //  Hereditary inheritance result
    // ─────────────────────────────────────────

    public enum InheritanceResult
    {
        PrimaryParent,   // Gets one parent's quirk
        SecondaryParent, // Gets the other parent's quirk
        Fusion,          // Gets a blend of both
        Mutation,        // Gets a brand new variant
        Quirkless        // Inherits nothing (~20% chance)
    }

    // ─────────────────────────────────────────
    //  Quirk Factor — the genetic blueprint
    // ─────────────────────────────────────────

    [Serializable]
    public class QuirkFactor
    {
        public string? QuirkId;
        public QuirkType Type;
        public float PowerPotential;   // 0–1  how strong this factor can grow
        public float BodyCompatibility; // 0–1  how well the body handles the power
        // Low compatibility = more drawback damage when used

        // Resistances gained from the Quirk Factor
        public List<string> NaturalResistances = new List<string>();
        // e.g. "acid", "fire", "ice", "electricity"

        // Mutation traits always present even before Awakening
        public List<string> PassiveMutations = new List<string>();
        // e.g. "acidic_skin", "glowing_pupils", "corrosive_saliva"
    }

    // ─────────────────────────────────────────
    //  Quirk definition — the template
    // ─────────────────────────────────────────

    [Serializable]
    public class QuirkDefinition
    {
        public string? Id;
        public string? DisplayName;
        public string? Description;
        public QuirkType Type;
        public ActivationType Activation;
        public QuirkFactor? Factor;

        // Stat modifiers applied when the quirk is active
        public QuirkStatModifiers BaseModifiers = new QuirkStatModifiers();

        // Drawback definition
        public QuirkDrawback Drawback = new QuirkDrawback();

        // Progression thresholds — XP required to reach each rank
        public Dictionary<QuirkRank, int> RankThresholds = new Dictionary<QuirkRank, int>
        {
            { QuirkRank.E,        0    },
            { QuirkRank.D,        500  },
            { QuirkRank.C,        1500 },
            { QuirkRank.B,        3500 },
            { QuirkRank.A,        7000 },
            { QuirkRank.S,        13000},
            { QuirkRank.SS,       22000},
            { QuirkRank.Awakened, 35000}
        };

        // Per-rank modifiers that scale on top of the base
        public Dictionary<QuirkRank, QuirkStatModifiers> RankScaling =
            new Dictionary<QuirkRank, QuirkStatModifiers>();

        // Abilities unlocked at specific ranks
        public Dictionary<QuirkRank, List<string>> AbilitiesUnlockedAt =
            new Dictionary<QuirkRank, List<string>>();
    }

    // ─────────────────────────────────────────
    //  Stat modifiers
    // ─────────────────────────────────────────

    [Serializable]
    public class QuirkStatModifiers
    {
        public float DamageMultiplier     = 1f;
        public float SpeedMultiplier      = 1f;
        public float HPMultiplier         = 1f;
        public float ArmorMultiplier      = 1f;
        public float AttackRange          = 0f;   // additive tile bonus
        public float AreaOfEffect         = 0f;   // radius in tiles
        public float CritChanceBonus      = 0f;   // additive %
        public float PoisonChanceBonus    = 0f;
        public float CorrosionChanceBonus = 0f;
        public bool  GrantsFlight         = false;
        public bool  GrantsWaterBreathing = false;
    }

    // ─────────────────────────────────────────
    //  Drawback
    // ─────────────────────────────────────────

    [Serializable]
    public class QuirkDrawback
    {
        public bool   HasOverheat           = false;
        public float  OverheatThreshold     = 100f;
        public float  OverheatDamagePerSec  = 0f;

        public bool   HasSelfInjury         = false;
        public float  SelfInjuryPerUse      = 0f;   // flat HP damage

        public bool   HasStaminaDrain       = false;
        public float  StaminaDrainPerSec    = 0f;

        public bool   HasRecoil             = false;
        public float  RecoilDamagePercent   = 0f;   // % of damage dealt

        // Compatibility check — if body compat < threshold, injury is doubled
        public float  LowCompatibilityThreshold = 0.4f;
        public float  LowCompatibilityMultiplier = 2f;
    }

    // ─────────────────────────────────────────
    //  Runtime instance — attached to a unit
    // ─────────────────────────────────────────

    [Serializable]
    public class QuirkInstance
    {
        public string?         QuirkId;
        public QuirkDefinition? Definition;
        public QuirkFactor? Factor;

        // Progression
        public int            XP             = 0;
        public QuirkRank      CurrentRank    = QuirkRank.E;
        public bool           IsAwakened     = false;

        // Runtime state
        public bool           IsActive       = false;
        public float          Energy         = 100f;      // 0–100 energy bar
        public float          MaxEnergy      = 100f;
        public float          Heat           = 0f;        // 0–100 overheat meter
        public float          Cooldown       = 0f;        // seconds remaining

        // Kill tracking (for progression events)
        public int            KillCount      = 0;
        public int            NearDeathCount = 0;        // stress events

        // Animation timers
        public float          _auraPulseTimer = 0f;      // acid aura VFX pulse interval

        // Unlocked abilities at this rank
        public List<string>   ActiveAbilities = new List<string>();

        public QuirkStatModifiers GetCurrentModifiers()
        {
            var def = Definition;
            if (def == null) return new QuirkStatModifiers();
            var base_ = def.BaseModifiers;

            if (!def.RankScaling.TryGetValue(CurrentRank, out var scale))
                return base_;

            return new QuirkStatModifiers
            {
                DamageMultiplier     = base_.DamageMultiplier     * scale.DamageMultiplier,
                SpeedMultiplier      = base_.SpeedMultiplier      * scale.SpeedMultiplier,
                HPMultiplier         = base_.HPMultiplier         * scale.HPMultiplier,
                ArmorMultiplier      = base_.ArmorMultiplier      * scale.ArmorMultiplier,
                AttackRange          = base_.AttackRange          + scale.AttackRange,
                AreaOfEffect         = base_.AreaOfEffect         + scale.AreaOfEffect,
                CritChanceBonus      = base_.CritChanceBonus      + scale.CritChanceBonus,
                PoisonChanceBonus    = base_.PoisonChanceBonus    + scale.PoisonChanceBonus,
                CorrosionChanceBonus = base_.CorrosionChanceBonus + scale.CorrosionChanceBonus,
                GrantsFlight         = base_.GrantsFlight         || scale.GrantsFlight,
                GrantsWaterBreathing = base_.GrantsWaterBreathing || scale.GrantsWaterBreathing
            };
        }

        public bool HasResistance(string element)
            => Factor?.NaturalResistances?.Contains(element) ?? false;
    }
}