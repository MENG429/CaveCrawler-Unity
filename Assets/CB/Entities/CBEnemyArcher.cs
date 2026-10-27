using UnityEngine;

namespace CB
{
    /// <summary>
    /// Ranged enemy: keeps a standoff distance, needs line of sight, then fires a
    /// parabolic arrow at the player.
    /// </summary>
    public class CBEnemyArcher : CBEnemy
    {
        enum State { Reposition, Aim, Fire }
        State m_state = State.Reposition;
        State m_lastArtState = (State)(-1);

        float m_fireCooldown;

        public void InitArcher(CBConfig config, CBTileWorld world, CBPathfinder nav, Vector2 position)
        {
            InitEnemy(config, world, nav, new Vector2(config.archerWidth, config.archerHeight), position, config.archerHP, CBAssets.Archer);
            BindArt("enemy_archer", "idle");
            gameObject.name = "CB_Archer";
            m_fireCooldown = config.archerFireInterval * 0.5f;
        }

        void UpdateArt()
        {
            if (m_anim == null) return;

            string clip;
            bool restart = m_state != m_lastArtState;
            m_lastArtState = m_state;

            switch (m_state)
            {
                case State.Aim: clip = "draw"; break;
                case State.Fire: clip = "release"; break;
                default: clip = Mathf.Abs(Body.Velocity.x) > 0.4f ? "walk" : "idle"; restart = false; break;
            }

            PlayArt(clip, restart);
        }

        public override void FixedStep(float dt)
        {
            base.FixedStep(dt);
            if (Dead) return;

            UpdateArt();
            TickArt(dt);

            RefreshTarget();
            if (!IsTargetValid())
            {
                Body.Velocity.x = CBUtil.Approach(Body.Velocity.x, 0f, Cfg.archerAccel * dt);
                Body.Step(dt);
                return;
            }

            float dist = DistanceToTarget();
            Activated = dist <= Cfg.enemyActivationRange;
            if (!Activated)
            {
                Body.Velocity.x = CBUtil.Approach(Body.Velocity.x, 0f, Cfg.archerAccel * dt);
                Body.Step(dt);
                return;
            }

            FaceTowards(Target.Body.Position);
            if (m_fireCooldown > 0f) m_fireCooldown -= dt;

            switch (m_state)
            {
                case State.Reposition:
                    StepReposition(dt, dist);
                    break;

                case State.Aim:
                    Body.Velocity.x = CBUtil.Approach(Body.Velocity.x, 0f, Cfg.archerAccel * dt);
                    Body.Step(dt);
                    StateTimer -= dt;
                    if (StateTimer <= 0f)
                    {
                        Shoot();
                        m_state = State.Fire;
                        StateTimer = 0.25f;
                    }
                    break;

                case State.Fire:
                    Body.Velocity.x = CBUtil.Approach(Body.Velocity.x, 0f, Cfg.archerAccel * dt);
                    Body.Step(dt);
                    StateTimer -= dt;
                    if (StateTimer <= 0f) m_state = State.Reposition;
                    break;
            }
        }

        void StepReposition(float dt, float dist)
        {
            TickPath(dt, CBPathfinder.Mode.Walker, Cfg.enemyRepathInterval);

            // Standoff logic overrides the path direction: back off when too close,
            // close in when too far, otherwise hold and shoot.
            float dx = Target.Body.Position.x - Body.Position.x;
            float desiredDir;

            if (dist < Cfg.archerMinRange) desiredDir = -Mathf.Sign(dx);
            else if (dist > Cfg.archerMaxRange) desiredDir = Mathf.Sign(dx);
            else desiredDir = 0f;

            if (Mathf.Abs(desiredDir) > 0.01f)
            {
                float target = desiredDir * Cfg.archerSpeed * SlowFactor;
                Body.Velocity.x = CBUtil.Approach(Body.Velocity.x, target, Cfg.archerAccel * dt);
                SetFacing(dx >= 0f);   // keep facing the player even while backing off
                TryPathJump(Cfg.chaserJumpSpeed);
            }
            else
            {
                Body.Velocity.x = CBUtil.Approach(Body.Velocity.x, 0f, Cfg.archerAccel * dt);
            }

            Body.Step(dt);

            bool inBand = dist >= Cfg.archerMinRange * 0.85f && dist <= Cfg.archerMaxRange;
            if (inBand && m_fireCooldown <= 0f && HasLineOfSightToTarget())
            {
                m_state = State.Aim;
                StateTimer = Cfg.archerWindup;
            }
        }

        void Shoot()
        {
            m_fireCooldown = Cfg.archerFireInterval;

            Vector2 origin = Body.Position + new Vector2(FacingRight ? 0.6f : -0.6f, 0.25f);
            Vector2 target = Target.Body.Position;

            // Solve a lofted arc toward the target (flat-ish, gravity-compensated).
            Vector2 delta = target - origin;
            float g = Cfg.archerArrowGravity;
            float v = Cfg.archerArrowSpeed;

            float angle = SolveBallisticAngle(delta.x, delta.y, v, g);
            if (float.IsNaN(angle)) angle = delta.x >= 0f ? 0f : Mathf.PI;

            Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            if (delta.x < 0f && dir.x > 0f) dir.x = -dir.x;

            CBProjectile.Spawn(
                Cfg,
                World,
                origin,
                dir,
                v,
                g,
                Cfg.archerDamage,
                Cfg.archerKnockback,
                0,
                TeamKind.Player,
                this,
                CBAssets.EnemyArrow,
                null);

            CBFx.SpawnBurst(origin, 3, CBAssets.EnemyArrow, 4f, 0.12f, 0.12f);
        }

        /// <summary>Low-arc solution of the ballistic angle problem. Returns NaN if unreachable.</summary>
        static float SolveBallisticAngle(float x, float y, float v, float g)
        {
            if (g <= 0.0001f) return Mathf.Atan2(y, x);
            float xAbs = Mathf.Abs(x);
            float v2 = v * v;
            float root = v2 * v2 - g * (g * xAbs * xAbs + 2f * y * v2);
            if (root < 0f) return float.NaN;
            return Mathf.Atan((v2 - Mathf.Sqrt(root)) / (g * xAbs));
        }
    }
}
