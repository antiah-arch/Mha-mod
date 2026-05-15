using System.Collections.Generic;
using AcidemaQuirkMod.Core;

namespace AcidemaQuirkMod.Core
{
    /// <summary>
    /// Central registry for all quirk definitions.
    /// Add new quirks here and they automatically become available
    /// to the QuirkManager and hereditary system.
    /// </summary>
    public static class QuirkRegistry
    {
        private static readonly Dictionary<string, QuirkDefinition> _quirks =
            new Dictionary<string, QuirkDefinition>();

        public static IReadOnlyDictionary<string, QuirkDefinition> All => _quirks;

        // ─────────────────────────────────────────
        //  Bootstrap
        // ─────────────────────────────────────────

        public static void Initialize()
        {
            Register(CreateAcidemQuirk());
            Register(CreateFireQuirk());
            Register(CreateIceQuirk());
            Register(CreateHardeningQuirk());
            Register(CreateLightningQuirk());
            // Add more quirks below as the mod grows
        }

        public static void Register(QuirkDefinition quirk)
        {
            if (string.IsNullOrEmpty(quirk.Id)) return;
            _quirks[quirk.Id!] = quirk;
        }

        public static bool TryGet(string id, out QuirkDefinition def)
            => _quirks.TryGetValue(id, out def);

        // ─────────────────────────────────────────
        //  ACIDEMA — Acid Emitter (the hero quirk)
        // ─────────────────────────────────────────

        private static QuirkDefinition CreateAcidemQuirk() => new QuirkDefinition
        {
            Id          = "acid_emission",
            DisplayName = "Acid Emission",
            Description = "Generates and controls highly corrosive acid. " +
                          "The user's body chemistry is naturally resistant to acidic compounds, " +
                          "but overuse dissolves even their hardened tissues.",
            Type       = QuirkType.Emitter,
            Activation = ActivationType.Resource,

            Factor = new QuirkFactor
            {
                QuirkId            = "acid_emission",
                Type               = QuirkType.Emitter,
                PowerPotential     = 0.88f,
                BodyCompatibility  = 0.72f,
                NaturalResistances = new List<string> { "acid", "poison", "corrosion" },
                PassiveMutations   = new List<string>
                {
                    "acidic_skin",       // contact with skin slowly corrodes armor
                    "glowing_pupils",    // eyes glow faint green
                    "corrosive_saliva"   // bite attacks apply acid debuff
                }
            },

            BaseModifiers = new QuirkStatModifiers
            {
                DamageMultiplier     = 1.25f,
                SpeedMultiplier      = 1.0f,
                HPMultiplier         = 0.95f,   // slightly squishy — power over endurance
                ArmorMultiplier      = 0.9f,
                AttackRange          = 1.5f,    // acid splash range
                AreaOfEffect         = 1.0f,
                CritChanceBonus      = 0.05f,
                PoisonChanceBonus    = 0.40f,
                CorrosionChanceBonus = 0.55f    // hallmark effect — melts armor
            },

            Drawback = new QuirkDrawback
            {
                HasSelfInjury         = true,
                SelfInjuryPerUse      = 3f,     // overuse burns own hands

                HasOverheat           = true,
                OverheatThreshold     = 100f,
                OverheatDamagePerSec  = 5f,     // acid floods own system

                HasStaminaDrain       = true,
                StaminaDrainPerSec    = 8f,

                HasRecoil             = false,

                LowCompatibilityThreshold = 0.4f,
                LowCompatibilityMultiplier = 2.0f
            },

            RankScaling = new Dictionary<QuirkRank, QuirkStatModifiers>
            {
                { QuirkRank.E, new QuirkStatModifiers { DamageMultiplier = 1.0f } },
                { QuirkRank.D, new QuirkStatModifiers { DamageMultiplier = 1.1f, PoisonChanceBonus = 0.05f } },
                { QuirkRank.C, new QuirkStatModifiers { DamageMultiplier = 1.2f, AreaOfEffect = 0.5f } },
                { QuirkRank.B, new QuirkStatModifiers { DamageMultiplier = 1.35f, CorrosionChanceBonus = 0.1f } },
                { QuirkRank.A, new QuirkStatModifiers { DamageMultiplier = 1.5f, AttackRange = 1.0f, AreaOfEffect = 0.5f } },
                { QuirkRank.S, new QuirkStatModifiers { DamageMultiplier = 1.75f, SpeedMultiplier = 1.1f, CritChanceBonus = 0.05f } },
                { QuirkRank.SS, new QuirkStatModifiers { DamageMultiplier = 2.0f, AreaOfEffect = 1.0f, HPMultiplier = 1.05f } },
                {
                    QuirkRank.Awakened, new QuirkStatModifiers
                    {
                        DamageMultiplier     = 2.5f,
                        SpeedMultiplier      = 1.2f,
                        HPMultiplier         = 1.1f,
                        AreaOfEffect         = 2.0f,
                        AttackRange          = 2.0f,
                        CorrosionChanceBonus = 0.25f,
                        PoisonChanceBonus    = 0.15f,
                        CritChanceBonus      = 0.1f
                    }
                }
            },

            AbilitiesUnlockedAt = new Dictionary<QuirkRank, List<string>>
            {
                { QuirkRank.E, new List<string> { "acid_spit"          } },   // basic ranged attack
                { QuirkRank.C, new List<string> { "acid_pool"          } },   // create a puddle that damages
                { QuirkRank.B, new List<string> { "armor_melt"         } },   // remove enemy armor permanently
                { QuirkRank.A, new List<string> { "acid_wave"          } },   // cone AoE burst
                { QuirkRank.S, new List<string> { "acid_aura"          } },   // passive ring of corrosion
                { QuirkRank.Awakened, new List<string> { "total_dissolution" } }  // dissolve terrain + large units
            }
        };

        // ─────────────────────────────────────────
        //  FIRE — Emitter template
        // ─────────────────────────────────────────

        private static QuirkDefinition CreateFireQuirk() => new QuirkDefinition
        {
            Id          = "fire_emission",
            DisplayName = "Hellflame",
            Description = "Generates and controls fire at will.",
            Type       = QuirkType.Emitter,
            Activation = ActivationType.Resource,

            Factor = new QuirkFactor
            {
                QuirkId            = "fire_emission",
                Type               = QuirkType.Emitter,
                PowerPotential     = 0.82f,
                BodyCompatibility  = 0.78f,
                NaturalResistances = new List<string> { "fire", "heat", "burn" }
            },

            BaseModifiers = new QuirkStatModifiers
            {
                DamageMultiplier = 1.30f,
                AreaOfEffect     = 1.2f,
                AttackRange      = 1.0f
            },

            Drawback = new QuirkDrawback
            {
                HasOverheat          = true,
                OverheatThreshold    = 100f,
                OverheatDamagePerSec = 8f,
                HasStaminaDrain      = true,
                StaminaDrainPerSec   = 10f
            },

            RankScaling = new Dictionary<QuirkRank, QuirkStatModifiers>
            {
                { QuirkRank.S,       new QuirkStatModifiers { DamageMultiplier = 1.6f, AreaOfEffect = 0.8f } },
                { QuirkRank.Awakened, new QuirkStatModifiers { DamageMultiplier = 2.2f, AreaOfEffect = 1.5f } }
            },

            AbilitiesUnlockedAt = new Dictionary<QuirkRank, List<string>>
            {
                { QuirkRank.E, new List<string> { "fireball"     } },
                { QuirkRank.B, new List<string> { "flame_wave"   } },
                { QuirkRank.S, new List<string> { "inferno_mode" } }
            }
        };

        // ─────────────────────────────────────────
        //  ICE — Emitter template
        // ─────────────────────────────────────────

        private static QuirkDefinition CreateIceQuirk() => new QuirkDefinition
        {
            Id          = "ice_emission",
            DisplayName = "Half-Cold",
            Description = "Creates and shapes ice from moisture in the air.",
            Type       = QuirkType.Emitter,
            Activation = ActivationType.Resource,

            Factor = new QuirkFactor
            {
                QuirkId            = "ice_emission",
                Type               = QuirkType.Emitter,
                PowerPotential     = 0.75f,
                BodyCompatibility  = 0.80f,
                NaturalResistances = new List<string> { "ice", "cold", "freeze" }
            },

            BaseModifiers = new QuirkStatModifiers
            {
                DamageMultiplier = 1.1f,
                ArmorMultiplier  = 1.2f,
                AttackRange      = 2.0f
            },

            Drawback = new QuirkDrawback
            {
                HasSelfInjury    = true,
                SelfInjuryPerUse = 2f   // frostbite risk on the left side
            },

            RankScaling = new Dictionary<QuirkRank, QuirkStatModifiers>
            {
                { QuirkRank.S,        new QuirkStatModifiers { DamageMultiplier = 1.4f, ArmorMultiplier = 1.4f } },
                { QuirkRank.Awakened, new QuirkStatModifiers { DamageMultiplier = 2.0f, ArmorMultiplier = 1.6f, AttackRange = 2.0f } }
            },

            AbilitiesUnlockedAt = new Dictionary<QuirkRank, List<string>>
            {
                { QuirkRank.E, new List<string> { "ice_wall"   } },
                { QuirkRank.B, new List<string> { "blizzard"   } },
                { QuirkRank.S, new List<string> { "glacial_age"} }
            }
        };

        // ─────────────────────────────────────────
        //  HARDENING — Transformation template
        // ─────────────────────────────────────────

        private static QuirkDefinition CreateHardeningQuirk() => new QuirkDefinition
        {
            Id          = "hardening",
            DisplayName = "Hardening",
            Description = "Hardens the skin into near-impenetrable rock-like armor.",
            Type       = QuirkType.Transformation,
            Activation = ActivationType.Toggle,

            Factor = new QuirkFactor
            {
                QuirkId            = "hardening",
                Type               = QuirkType.Transformation,
                PowerPotential     = 0.70f,
                BodyCompatibility  = 0.95f,
                NaturalResistances = new List<string> { "physical", "slash", "pierce" }
            },

            BaseModifiers = new QuirkStatModifiers
            {
                ArmorMultiplier  = 2.0f,
                HPMultiplier     = 1.15f,
                SpeedMultiplier  = 0.85f   // heavier = slower
            },

            Drawback = new QuirkDrawback
            {
                HasStaminaDrain    = true,
                StaminaDrainPerSec = 5f
            },

            RankScaling = new Dictionary<QuirkRank, QuirkStatModifiers>
            {
                { QuirkRank.S,        new QuirkStatModifiers { ArmorMultiplier = 1.3f, HPMultiplier = 1.1f } },
                { QuirkRank.Awakened, new QuirkStatModifiers { ArmorMultiplier = 1.6f, HPMultiplier = 1.2f, SpeedMultiplier = 1.05f } }
            },

            AbilitiesUnlockedAt = new Dictionary<QuirkRank, List<string>>
            {
                { QuirkRank.E, new List<string> { "skin_harden"       } },
                { QuirkRank.B, new List<string> { "spike_counter"     } },
                { QuirkRank.S, new List<string> { "unbreakable_armor" } }
            }
        };

        // ─────────────────────────────────────────
        //  LIGHTNING — Emitter with speed bonus
        // ─────────────────────────────────────────

        private static QuirkDefinition CreateLightningQuirk() => new QuirkDefinition
        {
            Id          = "lightning_emission",
            DisplayName = "Electrification",
            Description = "Generates electricity, boosts speed, but risks losing control.",
            Type       = QuirkType.Emitter,
            Activation = ActivationType.EmotionTrigger,

            Factor = new QuirkFactor
            {
                QuirkId            = "lightning_emission",
                Type               = QuirkType.Emitter,
                PowerPotential     = 0.85f,
                BodyCompatibility  = 0.65f,   // low compat = dangerous
                NaturalResistances = new List<string> { "lightning", "stun", "paralysis" }
            },

            BaseModifiers = new QuirkStatModifiers
            {
                DamageMultiplier = 1.4f,
                SpeedMultiplier  = 1.5f,
                CritChanceBonus  = 0.10f
            },

            Drawback = new QuirkDrawback
            {
                HasRecoil                  = true,
                RecoilDamagePercent        = 0.08f,
                HasOverheat                = true,
                OverheatThreshold          = 100f,
                OverheatDamagePerSec       = 12f,   // loses control at max
                LowCompatibilityThreshold  = 0.4f,
                LowCompatibilityMultiplier = 2.5f
            },

            RankScaling = new Dictionary<QuirkRank, QuirkStatModifiers>
            {
                { QuirkRank.S,        new QuirkStatModifiers { DamageMultiplier = 1.5f, SpeedMultiplier = 1.3f } },
                { QuirkRank.Awakened, new QuirkStatModifiers { DamageMultiplier = 2.2f, SpeedMultiplier = 1.6f, CritChanceBonus = 0.1f } }
            },

            AbilitiesUnlockedAt = new Dictionary<QuirkRank, List<string>>
            {
                { QuirkRank.E, new List<string> { "shock_touch"     } },
                { QuirkRank.B, new List<string> { "lightning_dash"  } },
                { QuirkRank.S, new List<string> { "storm_surge"     } }
            }
        };
    }
}