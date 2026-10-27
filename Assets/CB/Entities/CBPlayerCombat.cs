using System.Collections.Generic;
using UnityEngine;

namespace CB
{
    /// <summary>
    /// Melee combo + bow firing + weapon inventory.
    ///
    /// Every swing is a three-phase state machine (windup -> active -> recover) with
    /// per-combo-step values from CBConfig, and the third hit is the heavy finisher.
    /// </summary>
    public class CBPlayerCombat : MonoBehaviour, ICBFixedStep
    {
        public enum SwingPhase { None, Windup, Active, Recover }

        CBConfig m_config;
        CBPlayerController m_player;
        CBTileWorld m_world;

        // Weapon inventory: every collected weapon is kept; the held index selects the active one.
        readonly List<CBSwordDef> m_meleeInv = new List<CBSwordDef>();
        readonly List<CBBowDef> m_bowInv = new List<CBBowDef>();
        int m_heldMelee;
        int m_heldBow;
        float m_throwCd;

        public CBSwordDef Sword { get { return m_meleeInv.Count > 0 ? m_meleeInv[m_heldMelee] : null; } }
        public CBBowDef Bow { get { return m_bowInv.Count > 0 ? m_bowInv[m_heldBow] : null; } }
        public IReadOnlyList<CBSwordDef> MeleeInventory { get { return m_meleeInv; } }
        public IReadOnlyList<CBBowDef> BowInventory { get { return m_bowInv; } }
        public int HeldMeleeIndex { get { return m_heldMelee; } }
        public int HeldBowIndex { get { return m_heldBow; } }
        public float ThrowCooldown01 { get { return m_config != null ? Mathf.Clamp01(m_throwCd / m_config.throwCooldown) : 0f; } }

        SwingPhase m_phase = SwingPhase.None;
        float m_phaseTimer;
        int m_comboIndex;
        float m_comboTimer;
        float m_swingCooldown;
        float m_bowCooldown;

        readonly HashSet<CBActor> m_hitThisSwing = new HashSet<CBActor>();

        public SwingPhase Phase { get { return m_phase; } }
        public int ComboIndex { get { return m_comboIndex; } }
        public bool IsSwinging { get { return m_phase != SwingPhase.None; } }
        /// <summary>Monotonic counter - the player animator restarts the swing clip when it changes.</summary>
        public int SwingSeq { get; private set; }
        /// <summary>Time remaining for the bow-shot animation pose.</summary>
        public float BowAnimTime { get; private set; }
        public float BowCooldown01 { get { return Bow != null ? Mathf.Clamp01(m_bowCooldown / Mathf.Max(0.01f, Bow.FireInterval)) : 1f; } }

        public void Init(CBConfig config, CBPlayerController player, CBTileWorld world)
        {
            m_config = config;
            m_player = player;
            m_world = world;

            m_meleeInv.Add((CBSwordDef)CBWeaponLibrary.Get(CBWeaponLibrary.StartingSwordId));
            m_bowInv.Add((CBBowDef)CBWeaponLibrary.Get(CBWeaponLibrary.StartingBowId));
            m_heldMelee = 0;
            m_heldBow = 0;

            CBGameClock.Instance.Register(this, CBStepOrder.Player + 1);
        }

        float MeleeDamageMul()
        {
            return (m_player != null && m_player.Loadout != null) ? m_player.Loadout.Mods.meleeDamageMul : 1f;
        }

        void OnDestroy()
        {
            if (CBGameClock.Instance != null) CBGameClock.Instance.Unregister(this);
        }

        /// <summary>Point the combat system at the new level's grid after a level rebuild.</summary>
        public void SetWorld(CBTileWorld world) { m_world = world; }

        /// <summary>
        /// Adds a weapon to the inventory and makes it the held one. The previously held weapon
        /// stays in the backpack - nothing is discarded. Inventory is capped; when full the
        /// oldest entry is dropped to make room.
        /// </summary>
        public void Equip(CBWeaponDef def)
        {
            if (def == null) return;
            var sword = def as CBSwordDef;
            if (sword != null)
            {
                if (!m_meleeInv.Contains(sword))
                {
                    if (m_meleeInv.Count >= m_config.meleeInventoryMax) m_meleeInv.RemoveAt(0);
                    m_meleeInv.Add(sword);
                }
                m_heldMelee = m_meleeInv.IndexOf(sword);
                return;
            }
            var bow = def as CBBowDef;
            if (bow != null)
            {
                if (!m_bowInv.Contains(bow))
                {
                    if (m_bowInv.Count >= m_config.bowInventoryMax) m_bowInv.RemoveAt(0);
                    m_bowInv.Add(bow);
                }
                m_heldBow = m_bowInv.IndexOf(bow);
            }
        }

        public void SwitchMelee()
        {
            if (m_meleeInv.Count <= 1) return;
            m_heldMelee = (m_heldMelee + 1) % m_meleeInv.Count;
            if (m_player != null && m_player.Body != null) CBFx.SpawnRing(m_player.Body.Position, 0.8f, CBAssets.Portal, 0.16f);
        }

        public void SwitchBow()
        {
            if (m_bowInv.Count <= 1) return;
            m_heldBow = (m_heldBow + 1) % m_bowInv.Count;
            if (m_player != null && m_player.Body != null) CBFx.SpawnRing(m_player.Body.Position, 0.8f, CBAssets.Portal, 0.16f);
        }

        /// <summary>Called when the player starts a dive - a dive replaces the swing.</summary>
        public void CancelMelee()
        {
            m_phase = SwingPhase.None;
            m_phaseTimer = 0f;
            m_hitThisSwing.Clear();
        }

        public void FixedStep(float dt)
        {
            if (m_player == null || m_player.Dead) return;

            if (m_swingCooldown > 0f) m_swingCooldown -= dt;
            if (m_comboTimer > 0f) m_comboTimer -= dt;
            if (m_bowCooldown > 0f) m_bowCooldown -= dt;
            if (m_throwCd > 0f) m_throwCd -= dt;
            if (BowAnimTime > 0f) BowAnimTime -= dt;

            // ---- weapon switching / throwing ----
            if (CBInput.ConsumeSwitchMelee()) SwitchMelee();
            if (CBInput.ConsumeSwitchBow()) SwitchBow();
            if (CBInput.ConsumeThrow()) TryThrow();

            // ---- melee input ----
            if (m_phase == SwingPhase.None && m_swingCooldown <= 0f && !m_player.IsDiving)
            {
                if (CBInput.ConsumeAttack()) StartSwing();
            }

            UpdateSwing(dt);

            // ---- bow: hold to fire, no ammo ----
            if (Bow != null && m_bowCooldown <= 0f && CBInput.ShootHeld && !m_player.IsDiving)
            {
                FireBow();
            }
        }

        // ---------------- Melee ----------------
        void StartSwing()
        {
            m_comboIndex = m_comboTimer > 0f ? (m_comboIndex + 1) % 3 : 0;
            m_comboTimer = 0f;
            m_hitThisSwing.Clear();
            m_phase = SwingPhase.Windup;
            m_phaseTimer = WindupTime;
            SwingSeq++;
        }

        int EffectiveIndex
        {
            get { return (Sword != null && Sword.EveryHitIsHeavy) ? 2 : m_comboIndex; }
        }

        float WindupTime { get { return m_config.comboWindup[ClampIndex(EffectiveIndex)]; } }
        float ActiveTime { get { return m_config.comboActive[ClampIndex(EffectiveIndex)]; } }
        float RecoverTime { get { return m_config.comboRecover[ClampIndex(EffectiveIndex)]; } }
        float DamageMul { get { return m_config.comboDamageMul[ClampIndex(EffectiveIndex)]; } }
        float KnockMul { get { return m_config.comboKnockMul[ClampIndex(EffectiveIndex)]; } }

        static int ClampIndex(int i) { return i < 0 ? 0 : (i > 2 ? 2 : i); }

        void UpdateSwing(float dt)
        {
            if (m_phase == SwingPhase.None) return;

            m_phaseTimer -= dt;

            if (m_phase == SwingPhase.Active) EvaluateHitbox();

            if (m_phaseTimer > 0f) return;

            switch (m_phase)
            {
                case SwingPhase.Windup:
                    m_phase = SwingPhase.Active;
                    m_phaseTimer = ActiveTime;
                    SpawnSlashFx();
                    break;

                case SwingPhase.Active:
                    m_phase = SwingPhase.Recover;
                    m_phaseTimer = RecoverTime;
                    m_comboTimer = m_config.comboResetTime;
                    break;

                case SwingPhase.Recover:
                    m_phase = SwingPhase.None;
                    m_swingCooldown = 0f;
                    break;
            }
        }

        void SpawnSlashFx()
        {
            if (m_player == null || m_player.Body == null) return;
            bool right = m_player.FacingRight;
            Vector2 origin = m_player.Body.Position + new Vector2(right ? 1f : -1f, m_config.meleeOffsetY);
            CBFx.SpawnSlash(origin, new Vector2(m_config.meleeReachX * 1.6f, m_config.meleeHeight * 0.7f),
                !right, CBAssets.Slash, ActiveTime + 0.06f);
        }

        void EvaluateHitbox()
        {
            if (m_player == null || m_player.Body == null || Sword == null) return;

            var body = m_player.Body;
            bool right = m_player.FacingRight;
            float reach = m_config.meleeReachX;

            var box = new Rect(
                body.Position.x + (right ? body.HalfW * 0.4f : -body.HalfW * 0.4f - reach),
                body.Position.y - m_config.meleeHeight * 0.5f + m_config.meleeOffsetY,
                reach,
                m_config.meleeHeight);

            var results = CBActorRegistry.Scratch;
            int count = CBActorRegistry.QueryBox(box, CBActor.TeamKind.Enemy, results);

            for (int i = 0; i < count; i++)
            {
                var target = results[i];
                if (target == null || target.Dead) continue;
                if (m_hitThisSwing.Contains(target)) continue;

                int damage = Mathf.Max(1, Mathf.RoundToInt(Sword.RollDamage(CBGame.Instance != null ? CBGame.Instance.Rng : null) * DamageMul * MeleeDamageMul()));
                float dirX = target.Body.Position.x >= body.Position.x ? 1f : -1f;

                var info = new CBDamageInfo
                {
                    Amount = damage,
                    Knockback = new Vector2(dirX * Sword.Knockback * KnockMul, Sword.Knockback * 0.25f * KnockMul),
                    Source = m_player,
                    Weapon = Sword
                };

                bool landed = target.TakeDamage(info);
                if (!landed) continue;

                m_hitThisSwing.Add(target);

                bool heavy = EffectiveIndex == 2;
                CBGameClock.HitStop(heavy ? m_config.heavyHitstop : m_config.meleeHitstop);
                if (CBCamera2D.Instance != null)
                    CBCamera2D.Instance.Shake(heavy ? m_config.screenShakeHit * 2f : m_config.screenShakeHit);

                CBFx.SpawnImpact(target.Body.Position, 1.6f);

                if (Sword.ExplodesOnHit)
                {
                    CBExplosion.Detonate(target.Body.Position, Sword.ExplosionRadius, Sword.ExplosionDamage,
                        m_config.explosionKnockback, CBActor.TeamKind.Enemy, m_player, Sword, true);
                }

                if (target.Dead) OnKilledWithSword(target);
            }

            results.Clear();
        }

        // ---------------- Bow ----------------
        /// <summary>Full 360-degree aim from the movement keys; falls back to facing when idle.</summary>
        Vector2 AimDir()
        {
            float x = CBInput.Horizontal;
            float y = (CBInput.UpHeld ? 1f : 0f) - (CBInput.DownHeld ? 1f : 0f);
            var v = new Vector2(x, y);
            if (v.sqrMagnitude < 0.04f)
                v = m_player != null && m_player.FacingRight ? new Vector2(1f, 0f) : new Vector2(-1f, 0f);
            return v.normalized;
        }

        void FireBow()
        {
            if (Bow == null || m_player == null || m_player.Body == null) return;

            var mods = m_player.Loadout != null ? m_player.Loadout.Mods : null;
            float fireRateMul = mods != null ? mods.fireRateMul : 1f;
            float dmgMul = mods != null ? mods.rangedDamageMul : 1f;
            int extraArrows = mods != null ? mods.extraArrows : 0;

            m_bowCooldown = Bow.FireInterval * Mathf.Max(0.2f, fireRateMul);
            BowAnimTime = 0.4f;   // hold the draw/release pose briefly for the animator

            Vector2 aim = AimDir();
            bool rightish = aim.x >= 0f;
            Vector2 origin = m_player.Body.Position + new Vector2(rightish ? m_config.bowMuzzleOffset : -m_config.bowMuzzleOffset, 0.15f);

            int shots = Mathf.Max(1, Bow.ArrowsPerShot + extraArrows);
            float spread = Bow.SpreadDegrees;
            float baseDeg = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg;

            var rng = CBGame.Instance != null ? CBGame.Instance.Rng : null;

            for (int i = 0; i < shots; i++)
            {
                float offset;
                if (shots == 1)
                {
                    offset = spread > 0f ? (rng != null ? rng.Signed() * spread : 0f) : 0f;
                }
                else
                {
                    float t = (i / (float)(shots - 1)) * 2f - 1f;   // -1 .. 1
                    offset = t * spread;
                }

                float angleDeg = baseDeg + offset + (rng != null ? rng.Signed() * 0.6f : 0f);
                float rad = angleDeg * Mathf.Deg2Rad;
                var dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));

                int damage = Mathf.Max(1, Mathf.RoundToInt(Bow.RollDamage(rng) * dmgMul));
                int pierce = Bow.PierceCount;
                if (Bow.PierceChance > 0f && pierce == 0)
                {
                    pierce = (rng != null && rng.Chance(Bow.PierceChance)) ? 2 : 0;
                }

                CBProjectile.Spawn(
                    m_config,
                    m_world,
                    origin,
                    dir,
                    Bow.ArrowSpeed,
                    Bow.ArrowGravity,
                    damage,
                    m_config.arrowKnockback,
                    pierce,
                    CBActor.TeamKind.Enemy,
                    m_player,
                    CBAssets.Arrow,
                    Bow);
            }

            CBFx.SpawnBurst(origin, 3, Bow.Tint, 4f, 0.12f, 0.12f);
        }

        // ---------------- Throw ----------------
        /// <summary>
        /// Hurls the held melee weapon as a spinning projectile. It deals the sword's damage on
        /// hit and drops back as a reclaimable pickup on landing, so you never lose a weapon.
        /// </summary>
        void TryThrow()
        {
            if (m_throwCd > 0f || m_player == null || m_player.IsDiving || m_player.Body == null || Sword == null) return;

            var sword = Sword;
            m_throwCd = m_config.throwCooldown;

            Vector2 aim = AimDir();
            Vector2 origin = m_player.Body.Position + new Vector2(aim.x * 0.4f, 0.1f);
            var rng = CBGame.Instance != null ? CBGame.Instance.Rng : null;
            int dmg = Mathf.Max(1, Mathf.RoundToInt(sword.RollDamage(rng) * m_config.throwDamageMul));

            var proj = CBProjectile.Spawn(
                m_config, m_world, origin, aim,
                m_config.throwSpeed, m_config.throwGravity,
                dmg, m_config.arrowKnockback * 1.5f, 1,
                CBActor.TeamKind.Enemy, m_player, sword.Tint, null);
            if (proj != null) proj.SetReclaim(sword);

            // Clear from hand; it returns as a pickup on landing.
            if (m_meleeInv.Count > 1)
            {
                m_meleeInv.Remove(sword);
                m_heldMelee = Mathf.Min(m_heldMelee, m_meleeInv.Count - 1);
            }
            else
            {
                m_meleeInv.Clear();
                m_heldMelee = 0;
            }

            if (m_player.Body != null) CBFx.SpawnBurst(m_player.Body.Position, 5, sword.Tint, 6f, 0.18f, 0.1f);
        }

        // ---------------- Kill tracking ----------------
        /// <summary>Aggregates loadout-derived on-kill effects (lifesteal, explosion-on-kill).</summary>
        void OnKill(CBActor victim)
        {
            if (m_player == null) return;
            var mods = m_player.Loadout != null ? m_player.Loadout.Mods : null;
            if (mods == null) return;
            if (mods.lifestealOnKill > 0) m_player.Heal(mods.lifestealOnKill);
            if (mods.explosionOnKill && victim != null && victim.Body != null)
                CBExplosion.Detonate(victim.Body.Position, m_config.explosionRadius, m_config.explosionDamage,
                    m_config.explosionKnockback, CBActor.TeamKind.Enemy, m_player, null, true);
        }

        void OnKilledWithSword(CBActor victim)
        {
            if (m_player == null) return;
            m_player.Kills++;
            OnKill(victim);

            if (Sword != null && Sword.KillsPerMaxHPBonus > 0 && m_player.Kills % Sword.KillsPerMaxHPBonus == 0)
            {
                m_player.AddMaxHP(Sword.MaxHPPerBonus);
                if (m_player.Body != null) CBFx.SpawnRing(m_player.Body.Position, 1.2f, CBAssets.Potion, 0.35f);
            }
        }

        /// <summary>Used by projectile/explosion kills so Skull Stick still scales and loadout on-kill effects fire.</summary>
        public void NotifyKill(CBActor victim)
        {
            if (victim == null || m_player == null) return;
            m_player.Kills++;
            OnKill(victim);

            if (Sword != null && Sword.KillsPerMaxHPBonus > 0 && m_player.Kills % Sword.KillsPerMaxHPBonus == 0)
                m_player.AddMaxHP(Sword.MaxHPPerBonus);
        }
    }
}
