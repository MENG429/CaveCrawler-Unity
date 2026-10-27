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

        public CBSwordDef Sword { get; private set; }
        public CBBowDef Bow { get; private set; }

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
        public float BowCooldown01 { get { return Bow != null ? Mathf.Clamp01(m_bowCooldown / Bow.FireInterval) : 1f; } }

        public void Init(CBConfig config, CBPlayerController player, CBTileWorld world)
        {
            m_config = config;
            m_player = player;
            m_world = world;

            Sword = (CBSwordDef)CBWeaponLibrary.Get(CBWeaponLibrary.StartingSwordId);
            Bow = (CBBowDef)CBWeaponLibrary.Get(CBWeaponLibrary.StartingBowId);

            CBGameClock.Instance.Register(this, CBStepOrder.Player + 1);
        }

        void OnDestroy()
        {
            if (CBGameClock.Instance != null) CBGameClock.Instance.Unregister(this);
        }

        /// <summary>Point the combat system at the new level's grid after a level rebuild.</summary>
        public void SetWorld(CBTileWorld world) { m_world = world; }

        public void Equip(CBWeaponDef def)
        {
            if (def == null) return;
            var sword = def as CBSwordDef;
            if (sword != null) { Sword = sword; return; }
            var bow = def as CBBowDef;
            if (bow != null) Bow = bow;
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

                int damage = Mathf.Max(1, Mathf.RoundToInt(Sword.RollDamage(CBGame.Instance != null ? CBGame.Instance.Rng : null) * DamageMul));
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

                CBFx.SpawnBurst(target.Body.Position, heavy ? 8 : 4, CBAssets.Slash, 8f, 0.2f, 0.16f);

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
        void FireBow()
        {
            if (Bow == null || m_player == null || m_player.Body == null) return;

            m_bowCooldown = Bow.FireInterval;

            bool right = m_player.FacingRight;
            float baseAngle = 0f;
            if (CBInput.UpHeld) baseAngle = 35f;
            else if (CBInput.DownHeld && m_player.Body.Grounded) baseAngle = -18f;

            Vector2 origin = m_player.Body.Position + new Vector2(right ? m_config.bowMuzzleOffset : -m_config.bowMuzzleOffset, 0.15f);

            int shots = Mathf.Max(1, Bow.ArrowsPerShot);
            float spread = Bow.SpreadDegrees;

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
                    float t = shots == 1 ? 0f : (i / (float)(shots - 1)) * 2f - 1f;   // -1 .. 1
                    offset = t * spread;
                }

                float angleDeg = (right ? baseAngle : 180f - baseAngle) + offset + (rng != null ? rng.Signed() * 0.6f : 0f);
                float rad = angleDeg * Mathf.Deg2Rad;
                var dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));

                int damage = Bow.RollDamage(rng);
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

        // ---------------- Kill tracking ----------------
        void OnKilledWithSword(CBActor victim)
        {
            if (m_player == null || Sword == null) return;
            m_player.Kills++;

            if (Sword.KillsPerMaxHPBonus > 0 && m_player.Kills % Sword.KillsPerMaxHPBonus == 0)
            {
                m_player.AddMaxHP(Sword.MaxHPPerBonus);
                CBFx.SpawnRing(m_player.Body.Position, 1.2f, CBAssets.Potion, 0.35f);
            }
        }

        /// <summary>Used by projectile/explosion kills so Skull Stick still scales.</summary>
        public void NotifyKill(CBActor victim)
        {
            if (victim == null || m_player == null) return;
            m_player.Kills++;
            if (Sword != null && Sword.KillsPerMaxHPBonus > 0 && m_player.Kills % Sword.KillsPerMaxHPBonus == 0)
            {
                m_player.AddMaxHP(Sword.MaxHPPerBonus);
            }
        }
    }
}
