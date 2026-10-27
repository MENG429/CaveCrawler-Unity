using UnityEngine;

namespace CB
{
    /// <summary>
    /// Player locomotion. Every number here comes from CBConfig - nothing is hardcoded.
    ///
    /// Feel-critical details (all three are mandatory for the Caveblazers feel):
    ///   * coyote time      - you may still jump for a moment after leaving a ledge
    ///   * jump buffering   - a press slightly before landing is remembered and honoured
    ///   * variable height  - releasing jump early cuts the rise
    /// Plus a double jump and the air/ground acceleration split.
    /// </summary>
    public class CBPlayerController : CBActor
    {
        public CBPlayerCombat Combat { get; set; }

        CBTileWorld m_world;

        float m_coyote;
        float m_jumpBuffer;
        int m_jumpsUsed;
        bool m_jumpCutApplied;

        public bool IsDiving { get; private set; }
        float m_diveTimer;
        float m_diveCooldown;

        public int Coins { get; set; }
        public int Kills { get; set; }

        bool m_wasGroundedLastStep;
        float m_landTimer;

        // Wall-climb state
        public bool OnWall { get; private set; }
        int m_wallSide;          // -1 = left wall, +1 = right wall, 0 = none
        float m_wallGrace;
        float m_wallJumpLock;

        // Art state
        float m_airOverride;     // double-jump flip frames override the fall cycle
        int m_lastSwingSeq = -1;

        // Run-scoped loadout (equipment + blessings), persisted across levels.
        public CBLoadout Loadout { get; private set; }

        public bool CanAct { get { return !Dead && !IsDiving; } }

        public void InitPlayer(CBConfig config, CBTileWorld world, Vector2 position)
        {
            Team = TeamKind.Player;
            Init(config, world, new Vector2(config.playerWidth, config.playerHeight), position, config.maxHP);
            m_world = world;
            InvulnerableTime = config.invulnerableTime;
            Loadout = new CBLoadout();
            Loadout.Init(config.ringSlots);
            SetColour(CBAssets.Player);
            BindArt("player_idle_run", "idle");
            gameObject.name = "CB_Player";
        }

        public override float KnockbackResist { get { return Loadout != null ? Loadout.Mods.knockbackResist : 0f; } }

        public bool AddEquipment(CBEquipDef def)
        {
            if (Loadout == null || !Loadout.AddEquipment(def)) return false;
            if (def != null && def.Modifier != null && def.Modifier.maxHP > 0) AddMaxHP(def.Modifier.maxHP);
            if (Body != null) CBFx.SpawnRing(Body.Position, 1.2f, def != null ? def.Tint : CBAssets.Portal, 0.3f);
            return true;
        }

        public bool AddBlessing(CBBlessingDef def)
        {
            if (Loadout == null || !Loadout.AddBlessing(def)) return false;
            if (def != null && def.Modifier != null && def.Modifier.maxHP > 0) AddMaxHP(def.Modifier.maxHP);
            if (Body != null) CBFx.SpawnRing(Body.Position, 1.2f, def != null ? def.Tint : CBAssets.Portal, 0.3f);
            return true;
        }

        /// <summary>
        /// State-driven clip selection. Every branch degrades to the previous clip when art
        /// is missing, because PlayArt is a no-op without a bound player.
        /// </summary>
        void UpdateArt()
        {
            if (m_anim == null) return;

            string clip;
            bool restart = false;

            if (Combat != null && Combat.IsSwinging)
            {
                int seq = Combat.SwingSeq;
                restart = seq != m_lastSwingSeq;
                m_lastSwingSeq = seq;
                clip = "swing" + Mathf.Clamp(Combat.ComboIndex, 0, 2);
            }
            else if (Combat != null && Combat.BowAnimTime > 0f)
            {
                clip = "bow";
            }
            else if (IsDiving)
            {
                clip = "dive";
            }
            else if (!m_body.Grounded)
            {
                if (m_airOverride > 0f)
                {
                    m_airOverride -= 1f / 60f;
                    clip = "double";
                }
                else
                {
                    clip = m_body.Velocity.y > 1.5f ? "jump" : "fall";
                }
            }
            else
            {
                clip = Mathf.Abs(m_body.Velocity.x) > 0.6f ? "run" : "idle";
            }

            PlayArt(clip, restart);
        }

        public override void FixedStep(float dt)
        {
            if (Dead) { m_body.Step(dt); return; }

            UpdateArt();
            TickArt(dt);

            if (m_diveCooldown > 0f) m_diveCooldown -= dt;
            if (m_landTimer > 0f) m_landTimer -= dt;

            if (IsDiving)
            {
                StepDive(dt);
                return;
            }

            // ---- timers ----
            if (m_body.Grounded)
            {
                m_coyote = m_config.coyoteTime;
                m_jumpsUsed = 0;
                m_jumpCutApplied = false;
            }
            else
            {
                m_coyote -= dt;
            }

            int maxJumps = m_config.maxJumps + (Loadout != null ? Loadout.Mods.jumpBonus : 0);
            float moveSpeed = m_config.moveSpeed * (Loadout != null ? Loadout.Mods.moveSpeedMul : 1f);

            // ---- wall-climb state (only when airborne and not in a post-walljump lockout) ----
            if (!m_body.Grounded && m_wallJumpLock <= 0f)
            {
                int side = 0;
                if (SolidAtSide(-1)) side = -1;
                else if (SolidAtSide(1)) side = 1;
                if (side != 0) { m_wallSide = side; m_wallGrace = m_config.wallStickGrace; }
                else { m_wallGrace -= dt; if (m_wallGrace <= 0f) m_wallSide = 0; }
            }
            else
            {
                if (m_wallJumpLock > 0f) m_wallJumpLock -= dt;
                m_wallSide = 0;
            }
            OnWall = m_wallSide != 0;

            // Edge input: latched in Update, consumed here.
            if (CBInput.ConsumeJump()) m_jumpBuffer = m_config.jumpBufferTime;

            if (m_jumpBuffer > 0f)
            {
                m_jumpBuffer -= dt;

                bool groundJump = m_body.Grounded || m_coyote > 0f;
                if (groundJump)
                {
                    DoJump(m_config.jumpSpeed);
                    m_jumpsUsed = 1;
                    m_coyote = 0f;
                    m_jumpBuffer = 0f;
                }
                else if (OnWall && m_wallSide != 0)
                {
                    DoWallJump();
                    m_jumpsUsed = 1;
                    m_jumpBuffer = 0f;
                }
                else if (m_jumpsUsed < maxJumps)
                {
                    m_jumpsUsed++;
                    DoJump(m_config.doubleJumpSpeed);
                    m_jumpBuffer = 0f;
                    m_airOverride = 0.35f;   // show the double-jump flip before the fall cycle
                    CBFx.SpawnRing(m_body.Feet, 0.7f, CBAssets.Player, 0.18f);
                }
            }

            // Variable jump height: cut the rise once when the button comes up.
            if (!m_jumpCutApplied && !CBInput.JumpHeld && m_body.Velocity.y > 0f)
            {
                m_body.Velocity.y *= m_config.jumpCutMultiplier;
                m_jumpCutApplied = true;
            }

            // ---- dive attack (air + down + attack) ----
            if (!m_body.Grounded && CBInput.DownHeld && CBInput.ConsumeAttack() && m_diveCooldown <= 0f)
            {
                StartDive();
                return;
            }

            // ---- horizontal movement ----
            float input = CBInput.Horizontal;
            float target = input * moveSpeed;
            float accel;
            float friction;

            if (m_body.Grounded)
            {
                accel = m_config.groundAccel;
                friction = m_config.groundFriction;
            }
            else
            {
                accel = m_config.airAccel * m_config.airControlMultiplier;
                friction = m_config.airFriction;
            }

            if (Mathf.Abs(input) > 0.01f)
            {
                float delta = accel * dt;
                m_body.Velocity.x = CBUtil.Approach(m_body.Velocity.x, target, delta);
                SetFacing(input > 0f);
            }
            else
            {
                m_body.Velocity.x = CBUtil.Approach(m_body.Velocity.x, 0f, friction * dt);
            }

            // ---- wall cling / climb (overrides fall once we are sticking) ----
            if (OnWall && !m_body.Grounded)
            {
                bool pushingInto = Mathf.Abs(input) < 0.01f || Mathf.Sign(input) == m_wallSide;
                if (pushingInto)
                {
                    if (CBInput.UpHeld && !CBInput.DownHeld) m_body.Velocity.y = m_config.wallClimbSpeed;
                    else m_body.Velocity.y = Mathf.Max(m_body.Velocity.y, -m_config.wallSlideSpeed);
                    if (Mathf.Sign(input) == m_wallSide && Mathf.Abs(m_body.Velocity.x) > 1.5f)
                        m_body.Velocity.x = Mathf.Sign(m_body.Velocity.x) * 1.5f;
                }
            }

            m_body.Step(dt);

            if (m_body.Grounded && !m_wasGroundedLastStep) m_landTimer = m_config.landRecovery;
            m_wasGroundedLastStep = m_body.Grounded;
        }

        void DoJump(float speed)
        {
            m_body.Velocity.y = speed;
            m_jumpCutApplied = false;
            m_body.Grounded = false;
            m_coyote = 0f;
        }

        void DoWallJump()
        {
            int away = -m_wallSide;   // push off the wall we are clinging to
            m_body.Velocity.x = away * m_config.wallJumpX;
            m_body.Velocity.y = m_config.wallJumpY;
            m_body.Grounded = false;
            m_jumpCutApplied = false;
            m_wallSide = 0;
            m_wallJumpLock = m_config.wallJumpLockTime;
            SetFacing(away > 0f);
            CBFx.SpawnRing(m_body.Feet, 0.6f, CBAssets.Player, 0.15f);
        }

        bool SolidAtSide(int dir)
        {
            if (m_body == null || m_world == null) return false;
            float x = m_body.Position.x + dir * (m_body.HalfW + 0.06f);
            return m_world.IsSolidAtWorld(new Vector2(x, m_body.Position.y));
        }

        // ---------------- Dive attack ----------------
        void StartDive()
        {
            IsDiving = true;
            m_diveTimer = 0f;
            m_body.Velocity = new Vector2(CBInput.Horizontal * m_config.diveAirDrift, -m_config.diveSpeed);
            m_body.UseGravity = false;
            if (Combat != null) Combat.CancelMelee();
        }

        void StepDive(float dt)
        {
            m_diveTimer += dt;

            // Small lateral control while diving - Caveblazers lets you nudge the drop.
            float input = CBInput.Horizontal;
            float targetX = input * m_config.diveAirDrift;
            m_body.Velocity.x = CBUtil.Approach(m_body.Velocity.x, targetX, m_config.airAccel * dt);
            m_body.Velocity.y = -m_config.diveSpeed;
            if (Mathf.Abs(input) > 0.01f) SetFacing(input > 0f);

            m_body.Step(dt);

            bool landed = m_body.Grounded && m_diveTimer >= m_config.diveMinTime;
            if (landed)
            {
                LandDive();
            }
        }

        void LandDive()
        {
            IsDiving = false;
            m_body.UseGravity = true;
            m_diveCooldown = m_config.diveCooldown;

            Vector2 centre = m_body.Feet + new Vector2(0f, 0.35f);

            CBExplosion.Detonate(
                centre,
                m_config.diveLandRadius,
                m_config.diveLandDamage,
                m_config.diveLandKnockback,
                CBActor.TeamKind.Enemy,
                this,
                null,
                true);

            CBFx.SpawnRing(centre, m_config.diveLandRadius, CBAssets.Slash, 0.25f);
            CBGameClock.HitStop(m_config.diveLandHitstop);
            if (CBCamera2D.Instance != null) CBCamera2D.Instance.Shake(m_config.screenShakeDive);

            m_body.Velocity.y = 0f;
            m_jumpsUsed = 0;
        }

        protected override void OnDamaged(CBDamageInfo info)
        {
            CBGameClock.HitStop(m_config.hurtHitstop);
            if (CBCamera2D.Instance != null) CBCamera2D.Instance.Shake(m_config.screenShakeHit);

            if (IsDiving)
            {
                IsDiving = false;
                m_body.UseGravity = true;
            }
            if (info.Knockback.x == 0f && m_body != null)
            {
                m_body.Velocity.y += m_config.hurtLift;
            }
        }

        protected override void OnDeath(CBDamageInfo info)
        {
            base.OnDeath(info);
            if (CBGame.Instance != null) CBGame.Instance.OnPlayerDied();
        }

        public void Heal(int amount)
        {
            HP = Mathf.Min(MaxHP, HP + amount);
        }

        public void AddMaxHP(int amount)
        {
            MaxHP += amount;
            HP += amount;
        }
    }
}
