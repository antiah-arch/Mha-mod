using System;
using System.Linq;
using UnityEngine;
using Random = UnityEngine.Random;
using AcidemaQuirkMod.Core;
using AcidemaQuirkMod.Systems;

namespace AcidemaQuirkMod.Abilities
{
    /// <summary>
    /// All Acid Emission abilities with full animations.
    ///
    /// Animation API confirmed from Assembly-CSharp.dll:
    ///   actor.setAnimationState(string)          — set actor sprite anim state
    ///   actor.makeFlash(Color)                   — flash the actor sprite a colour
    ///   actor.startColorEffect(Color, float)     — tint actor for duration
    ///   actor.spawnSlash(WorldTile)              — melee slash VFX on a tile
    ///   actor.spawnBurst(WorldTile)              — burst particle on a tile
    ///   actor.spawnBurstSpecial(WorldTile)       — larger burst variant
    ///   EffectsLibrary.spawnCloudAcid(tile)      — acid cloud on tile
    ///   EffectsLibrary.spawnFlash(tile)          — generic flash on tile
    ///   EffectsLibrary.spawnExplosionWave(tile)  — explosion ring on tile
    ///   EffectsLibrary.spawnStatusParticle(actor, string) — status particle on unit
    ///   EffectsLibrary.acidBloodEffect(actor)    — acid blood splatter on unit
    ///   EffectsLibrary.acidTouchEffect(tile)     — acid touch on tile
    ///   MapObjectHelper.addPoisonedEffectOnTarget(actor) — poison status
    /// </summary>
    public static class AcidAbilities
    {
        // Acid green colour used across all animations
        private static readonly Color AcidGreen      = new Color(0.11f, 0.62f, 0.46f, 1f);
        private static readonly Color AcidGreenDim   = new Color(0.11f, 0.62f, 0.46f, 0.5f);
        private static readonly Color AcidYellow     = new Color(0.55f, 0.80f, 0.10f, 1f);
        private static readonly Color AcidWhite      = new Color(0.80f, 1.00f, 0.70f, 1f);


        // ─────────────────────────────────────────
        //  acid_spit  (Rank E)
        //  Short wind-up → projectile launch → hit flash
        // ─────────────────────────────────────────

        public static void AcidSpit(string casterId)
        {
            if (!QuirkManager.Instance.HasAbility(casterId, "acid_spit")) return;
            if (!QuirkManager.Instance.TryGetQuirk(casterId, out var q)) return;

            dynamic caster = QuirkManager.GetActor(casterId);
            if (caster == null || !caster.isAlive()) return;

            var   mods  = q.GetCurrentModifiers();
            float dmg   = 15f * mods.DamageMultiplier;
            float range = 5f  + mods.AttackRange;

            ConsumeEnergy(q, 10f);

            // ── CAST ANIMATION ──
            // Flash caster acid-green briefly (charging up acid)
            caster.makeFlash(AcidGreen);

            // Tint caster green for 0.3s during spit
            caster.startColorEffect(AcidGreenDim, 0.3f);

            // Small burst at caster's feet — acid building up
            if (caster.currentTile != null)
                caster.spawnBurst(caster.currentTile);

            // ── FIND TARGET ──
            dynamic target = GetNearestEnemy(caster, range);
            if (target == null) return;

            // ── PROJECTILE HIT ANIMATION ──
            // Acid touch effect on the target's tile (projectile landing)
            if (target.currentTile != null)
            {
                WorldBoxApi.CallEffectsLibrary("acidTouchEffect", target.currentTile);
                WorldBoxApi.CallEffectsLibrary("spawnCloudAcid", target.currentTile);
            }

            // Acid blood splatter on target — confirmed method in Assembly-CSharp
            WorldBoxApi.CallEffectsLibrary("acidBloodEffect", target);

            // Flash target green on hit
            target.makeFlash(AcidGreen);

            // ── DAMAGE & STATUS ──
            target.getHit(dmg);

            if (Random.value < mods.PoisonChanceBonus)
                WorldBoxApi.CallMapObjectHelper("addPoisonedEffectOnTarget", target);

            if (Random.value < mods.CorrosionChanceBonus)
                CorrodArmor(target, 0.10f);

            // ── SELF-INJURY ANIMATION ──
            if (q.Definition.Drawback.HasSelfInjury)
            {
                // Faint red flash on caster (acid burns their own hand)
                caster.makeFlash(new Color(1f, 0.2f, 0.2f, 0.4f));
                QuirkManager.Instance.ApplySelfDamage(casterId, q,
                    q.Definition.Drawback.SelfInjuryPerUse);
            }

            QuirkManager.Instance.ApplyRecoil(casterId, q, dmg);
        }

        // ─────────────────────────────────────────
        //  acid_pool  (Rank C)
        //  Caster slams ground → acid spreads tile by tile with cloud VFX
        // ─────────────────────────────────────────

        public static void AcidPool(string casterId)
        {
            if (!QuirkManager.Instance.HasAbility(casterId, "acid_pool")) return;
            if (!QuirkManager.Instance.TryGetQuirk(casterId, out var q)) return;

            dynamic caster = QuirkManager.GetActor(casterId);
            if (caster == null || !caster.isAlive()) return;

            var   mods   = q.GetCurrentModifiers();
            float dps    = 8f * mods.DamageMultiplier;
            int   radius = Mathf.RoundToInt(1.5f + mods.AreaOfEffect);

            ConsumeEnergy(q, 25f);
            q.Heat += 15f;

            // ── CAST ANIMATION ──
            // Bright acid-white flash on caster — releasing acid
            caster.makeFlash(AcidWhite);

            // Full green tint for 0.5s
            caster.startColorEffect(AcidGreen, 0.5f);

            // Big burst at caster feet — the "slam"
            if (caster.currentTile != null)
            {
                caster.spawnBurstSpecial(caster.currentTile);
                WorldBoxApi.CallEffectsLibrary("acidTouchEffect", caster.currentTile);
            }

            // ── POOL SPREAD ANIMATION ──
            // Acid cloud + touch effect on every tile in radius
            var centerTile = caster.currentTile;
            if (centerTile == null) return;

            foreach (var tile in WorldBoxApi.GetTilesAround(centerTile, radius))
            {
                WorldBoxApi.CallEffectsLibrary("spawnCloudAcid", tile);
                WorldBoxApi.CallEffectsLibrary("acidTouchEffect", tile);

                dynamic actor = WorldBoxApi.GetActorFromTile(tile);
                if (actor == null || !actor.isAlive() || !IsEnemy(caster, actor)) continue;

                // Hit flash on each affected actor
                actor.makeFlash(AcidGreen);
                WorldBoxApi.CallEffectsLibrary("acidBloodEffect", actor);

                actor.getHit(dps);

                if (Random.value < mods.PoisonChanceBonus)
                    WorldBoxApi.CallMapObjectHelper("addPoisonedEffectOnTarget", actor);
            }
        }

        // ─────────────────────────────────────────
        //  armor_melt  (Rank B)
        //  Sustained acid spray → armor visually corrodes → debuff lands
        // ─────────────────────────────────────────

        public static void ArmorMelt(string casterId)
        {
            if (!QuirkManager.Instance.HasAbility(casterId, "armor_melt")) return;
            if (!QuirkManager.Instance.TryGetQuirk(casterId, out var q)) return;

            dynamic caster = QuirkManager.GetActor(casterId);
            if (caster == null || !caster.isAlive()) return;

            var   mods  = q.GetCurrentModifiers();
            float range = 3f + mods.AttackRange;

            float strip = 0.4f;
            if (q.CurrentRank >= QuirkRank.S)        strip += 0.2f;
            if (q.CurrentRank == QuirkRank.Awakened) strip += 0.2f;

            ConsumeEnergy(q, 30f);

            // ── CAST ANIMATION ──
            // Caster glows yellow-green — concentrating acid
            caster.startColorEffect(AcidYellow, 0.6f);
            caster.makeFlash(AcidYellow);

            if (caster.currentTile != null)
                caster.spawnBurst(caster.currentTile);

            // ── TARGET ──
            dynamic target = GetNearestEnemy(caster, range);
            if (target == null) return;

            // ── MELT ANIMATION ON TARGET ──
            // Acid touch on target tile — the spray landing
            if (target.currentTile != null)
            {
                WorldBoxApi.CallEffectsLibrary("acidTouchEffect", target.currentTile);
                WorldBoxApi.CallEffectsLibrary("spawnCloudAcid", target.currentTile);

                // Explosion wave — armor "shattering" from the acid
                WorldBoxApi.CallEffectsLibrary("spawnExplosionWave", target.currentTile);
            }

            // Acid blood — armor visibly dissolving
            WorldBoxApi.CallEffectsLibrary("acidBloodEffect", target);

            // Sustained green tint on target — acid eating the armor
            target.startColorEffect(AcidGreen, 1.0f);
            target.makeFlash(AcidGreen);

            // ── ARMOR STRIP ──
            CorrodArmor(target, strip);

            // ── SELF-INJURY ANIMATION ──
            caster.makeFlash(new Color(1f, 0.3f, 0.3f, 0.5f));
            QuirkManager.Instance.ApplySelfDamage(casterId, q, 5f);
        }

        // ─────────────────────────────────────────
        //  acid_wave  (Rank A)
        //  Full-body flash → wave ring expands → hits all in radius
        // ─────────────────────────────────────────

        public static void AcidWave(string casterId)
        {
            if (!QuirkManager.Instance.HasAbility(casterId, "acid_wave")) return;
            if (!QuirkManager.Instance.TryGetQuirk(casterId, out var q)) return;

            dynamic caster = QuirkManager.GetActor(casterId);
            if (caster == null || !caster.isAlive()) return;

            var   mods  = q.GetCurrentModifiers();
            float dmg   = 35f * mods.DamageMultiplier;
            float range = 4f  + mods.AttackRange;

            ConsumeEnergy(q, 45f);
            q.Heat += 25f;

            // ── CAST ANIMATION ──
            // Bright white flash — acid wave detonating
            caster.makeFlash(AcidWhite);
            caster.startColorEffect(AcidGreen, 0.8f);

            if (caster.currentTile != null)
            {
                // Large burst + explosion ring at the epicentre
                caster.spawnBurstSpecial(caster.currentTile);
                WorldBoxApi.CallEffectsLibrary("spawnExplosionWave", caster.currentTile);
                WorldBoxApi.CallEffectsLibrary("spawnCloudAcid", caster.currentTile);
            }

            // ── WAVE SPREAD ANIMATION + DAMAGE ──
            int iRange = Mathf.RoundToInt(range);
            foreach (var tile in WorldBoxApi.GetTilesAround(caster.currentTile, iRange))
            {
                // Acid cloud cascades outward on every tile
                WorldBoxApi.CallEffectsLibrary("spawnCloudAcid", tile);
                WorldBoxApi.CallEffectsLibrary("acidTouchEffect", tile);

                dynamic actor = WorldBoxApi.GetActorFromTile(tile);
                if (actor == null || !actor.isAlive() || !IsEnemy(caster, actor)) continue;

                // Each hit unit gets a flash + acid blood
                actor.makeFlash(AcidGreen);
                WorldBoxApi.CallEffectsLibrary("acidBloodEffect", actor);

                actor.getHit(dmg);

                if (Random.value < mods.CorrosionChanceBonus)
                    CorrodArmor(actor, 0.15f);

                if (Random.value < mods.PoisonChanceBonus)
                    WorldBoxApi.CallMapObjectHelper("addPoisonedEffectOnTarget", actor);
            }

            // ── SELF-INJURY ANIMATION ──
            // Red-tinted flash — big wave costs the user
            caster.makeFlash(new Color(1f, 0.2f, 0.2f, 0.6f));
            QuirkManager.Instance.ApplySelfDamage(casterId, q, 8f);
        }

        // ─────────────────────────────────────────
        //  acid_aura  (Rank S)
        //  Continuous green tint on caster + acid touch on nearby tiles every tick
        // ─────────────────────────────────────────

        public static void TickAcidAura(string casterId, float dt)
        {
            if (!QuirkManager.Instance.HasAbility(casterId, "acid_aura")) return;
            if (!QuirkManager.Instance.TryGetQuirk(casterId, out var q)) return;
            if (!q.IsActive) return;

            dynamic caster = QuirkManager.GetActor(casterId);
            if (caster == null || !caster.isAlive()) return;

            var   mods  = q.GetCurrentModifiers();
            float dps   = 4f * mods.DamageMultiplier * dt;
            int   range = Mathf.RoundToInt(2f + mods.AreaOfEffect * 0.5f);

            ConsumeEnergy(q, 4f * dt);

            // ── AURA ANIMATION ──
            // Pulse caster with a faint green tint every ~0.5s
            q._auraPulseTimer += dt;
            if (q._auraPulseTimer >= 0.5f)
            {
                q._auraPulseTimer = 0f;
                caster.startColorEffect(AcidGreenDim, 0.4f);

                // Status particle rising off the caster — acid vapour
                WorldBoxApi.CallEffectsLibrary("spawnStatusParticle", caster, "acid");
            }

            // ── DAMAGE + PER-TILE ANIMATION ──
            if (caster.currentTile == null) return;
            var tiles = WorldBoxApi.GetTilesAround(caster.currentTile, range);

            foreach (var tile in tiles)
            {
                // Subtle acid touch flicker on ground tiles
                if (Random.value < 0.15f)
                    WorldBoxApi.CallEffectsLibrary("acidTouchEffect", tile);

                dynamic actor = WorldBoxApi.GetActorFromTile(tile);
                if (actor == null || !actor.isAlive() || !IsEnemy(caster, actor)) continue;

                actor.getHit(dps);

                // Small flash on each ticking victim
                if (Random.value < 0.2f)
                    actor.makeFlash(AcidGreenDim);
            }
        }

        // ─────────────────────────────────────────
        //  total_dissolution  (Rank Awakened)
        //  Full nuke animation — blinding flash, explosion wave,
        //  cascading acid clouds, every unit hit gets acid blood + dissolve tint
        // ─────────────────────────────────────────

        public static void TotalDissolution(string casterId)
        {
            if (!QuirkManager.Instance.HasAbility(casterId, "total_dissolution")) return;
            if (!QuirkManager.Instance.TryGetQuirk(casterId, out var q)) return;
            if (!q.IsAwakened) return;

            dynamic caster = QuirkManager.GetActor(casterId);
            if (caster == null || !caster.isAlive()) return;

            var   mods  = q.GetCurrentModifiers();
            float dmg   = 120f * mods.DamageMultiplier;
            int   range = Mathf.RoundToInt(5f + mods.AreaOfEffect);

            q.Energy   = 0f;
            q.Heat    += 80f;
            q.Cooldown = 60f;

            // ── CAST ANIMATION — PHASE 1: CHARGE ──
            // Blinding white flash on caster — the user dissolving their own limits
            caster.makeFlash(AcidWhite);
            caster.startColorEffect(AcidYellow, 1.5f);

            if (caster.currentTile != null)
            {
                // Special burst + nuke-level explosion ring at epicentre
                caster.spawnBurstSpecial(caster.currentTile);
                WorldBoxApi.CallEffectsLibrary("spawnExplosionWave", caster.currentTile);
                WorldBoxApi.CallEffectsLibrary("spawnCloudAcid", caster.currentTile);
                WorldBoxApi.CallEffectsLibrary("acidTouchEffect", caster.currentTile);
            }

            // ── CAST ANIMATION — PHASE 2: DISSOLVE WAVE ──
            foreach (var tile in WorldBoxApi.GetTilesAround(caster.currentTile, range))
            {
                // Every tile gets acid cloud + touch — total saturation
                WorldBoxApi.CallEffectsLibrary("spawnCloudAcid", tile);
                WorldBoxApi.CallEffectsLibrary("acidTouchEffect", tile);

                // Scattered flash pops across the blast radius
                if (Random.value < 0.4f)
                    WorldBoxApi.CallEffectsLibrary("spawnFlash", tile);

                dynamic actor = WorldBoxApi.GetActorFromTile(tile);
                if (actor == null || !actor.isAlive() || !IsEnemy(caster, actor)) continue;

                // ── PER-TARGET HIT ANIMATION ──
                // Acid blood + full green dissolve tint on every victim
                WorldBoxApi.CallEffectsLibrary("acidBloodEffect", actor);
                actor.makeFlash(AcidWhite);
                actor.startColorEffect(AcidGreen, 2.0f);  // long tint — dissolving

                // Status particle — acid fumes rising from the victim
                WorldBoxApi.CallEffectsLibrary("spawnStatusParticle", actor, "acid");

                actor.getHit(dmg);
                WorldBoxApi.CallMapObjectHelper("addPoisonedEffectOnTarget", actor);
                CorrodArmor(actor, 0.5f);
            }

            // ── SELF-INJURY ANIMATION ──
            // Deep red flash — massive personal cost
            caster.makeFlash(new Color(1f, 0.0f, 0.0f, 0.9f));
            caster.startColorEffect(new Color(0.8f, 0.1f, 0.1f, 0.5f), 1.0f);
            QuirkManager.Instance.ApplySelfDamage(casterId, q, 40f);

            Debug.Log($"[TotalDissolution] {casterId} — {dmg:F0} dmg, r={range}");
        }

        // ─────────────────────────────────────────
        //  Helpers
        // ─────────────────────────────────────────

        private static void CorrodArmor(dynamic target, float fraction)
        {
            if (target == null) return;
            float current = WorldBoxApi.GetActorArmor(target);
            if (current <= 0f) return;
            WorldBoxApi.SetActorArmor(target, Mathf.Max(0f, current - current * fraction));
        }

        private static dynamic GetNearestEnemy(dynamic caster, float range)
        {
            if (caster.currentTile == null) return null;
            int   iRange      = Mathf.RoundToInt(range);
            dynamic nearest   = null;
            float nearestDist = float.MaxValue;

            foreach (var tile in WorldBoxApi.GetTilesAround(caster.currentTile, iRange))
            {
                dynamic actor = WorldBoxApi.GetActorFromTile(tile);
                if (actor == null || !actor.isAlive() || !IsEnemy(caster, actor)) continue;

                float dist = caster.distanceToActorTile(actor);
                if (dist < nearestDist) { nearestDist = dist; nearest = actor; }
            }
            return nearest;
        }

        private static bool IsEnemy(dynamic a, dynamic b)
            => a != null && b != null && a.kingdom != b.kingdom;

        private static void ConsumeEnergy(QuirkInstance q, float amount)
            => q.Energy = Mathf.Max(0f, q.Energy - amount);
    }
}