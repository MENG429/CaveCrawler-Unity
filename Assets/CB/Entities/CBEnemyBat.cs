using UnityEngine;

namespace CB
{
    /// <summary>
    /// Flying enemy: hovers around the player using flyer-mode A*, telegraphs, then dives.
    /// Contact during a dive is what damages the player.
    /// </summary>
    public class CBEnemyBat : CBEnemy
    {
        enum State { Hover, Telegraph, Dive, Recover }
        State m_state = State.Hover;

        float m_bobPhase;
        Vector2 m_diveDirection;

        public void InitBat(CBConfig config, CBTileWorld world, CBPathfinder nav, Vector2 position)
        {
            InitEnemy(config, world, nav, new Vector2(config.batWidth, config.batHeight), position, config.batHP, CBAssets.Bat);
            Body.UseGravity = false;
            gameObject.name = "CB_Bat";
            m_bobPhase = position.x * 0.7f;
        }

        public override void FixedStep(float dt)
        {
            base.FixedStep(dt);
            if (Dead) return;

            RefreshTarget();

            if (!IsTargetValid() || DistanceToTarget() > Cfg.enemyActivationRange)
            {
                Body.Velocity = CBUtil.Approach(Body.Velocity.x, 0f, Cfg.batAccel * dt) * Vector2.right
                    + Vector2.up * CBUtil.Approach(Body.Velocity.y, 0f, Cfg.batAccel * dt);
                Body.Step(dt);
                return;
            }

            Activated = true;
            FaceTowards(Target.Body.Position);
            m_bobPhase += dt * 6f;

            switch (m_state)
            {
                case State.Hover: StepHover(dt); break;
                case State.Telegraph: StepTelegraph(dt); break;
                case State.Dive: StepDive(dt); break;
                case State.Recover: StepRecover(dt); break;
            }
        }

        void StepHover(float dt)
        {
            TickPath(dt, CBPathfinder.Mode.Flyer, Cfg.batRepathInterval);

            Vector2 desired;
            if (m_path.Count > 0 && m_pathIndex < m_path.Count)
            {
                Vector2 wp = World.TileCenter(m_path[m_pathIndex].x, m_path[m_pathIndex].y);
                if (Vector2.Distance(wp, Body.Position) < 0.7f) m_pathIndex++;
                desired = (wp - Body.Position).normalized * Cfg.batSpeed * SlowFactor;
            }
            else
            {
                Vector2 toTarget = Target.Body.Position - Body.Position;
                float d = toTarget.magnitude;
                Vector2 orbit = toTarget.normalized;
                if (d < Cfg.batHoverRadius) orbit = -orbit;
                desired = orbit * Cfg.batSpeed * SlowFactor;
            }

            desired.y += Mathf.Sin(m_bobPhase) * Cfg.batHoverBob;

            Body.Velocity.x = CBUtil.Approach(Body.Velocity.x, desired.x, Cfg.batAccel * dt);
            Body.Velocity.y = CBUtil.Approach(Body.Velocity.y, desired.y, Cfg.batAccel * dt);
            Body.Step(dt);

            float dist = DistanceToTarget();
            bool linedUp = Mathf.Abs(Target.Body.Position.y - Body.Position.y) < 7f;
            if (dist < Cfg.batHoverRadius * 1.4f && linedUp && HasLineOfSightToTarget())
            {
                m_state = State.Telegraph;
                StateTimer = Cfg.batDiveTelegraph;
            }
        }

        void StepTelegraph(float dt)
        {
            // Pull back slightly, then commit.
            Body.Velocity = CBUtil.Approach(Body.Velocity.x, 0f, Cfg.batAccel * dt) * Vector2.right
                + Vector2.up * (CBUtil.Approach(Body.Velocity.y, 2.2f, Cfg.batAccel * dt));
            Body.Step(dt);

            m_diveDirection = (Target.Body.Position - Body.Position).normalized;

            StateTimer -= dt;
            if (StateTimer <= 0f)
            {
                m_state = State.Dive;
                StateTimer = Cfg.batDiveDuration;
                Body.Velocity = m_diveDirection * Cfg.batDiveSpeed * SlowFactor;
                CBFx.SpawnRing(Body.Position, 0.8f, CBAssets.Bat, 0.15f);
            }
        }

        void StepDive(float dt)
        {
            Body.Step(dt);
            TryContactDamage();

            StateTimer -= dt;
            if (StateTimer <= 0f || Body.HitWallLeft || Body.HitWallRight || Body.Grounded)
            {
                m_state = State.Recover;
                StateTimer = Cfg.batRecover;
                Body.Velocity *= 0.2f;
            }
        }

        void StepRecover(float dt)
        {
            Body.Velocity.x = CBUtil.Approach(Body.Velocity.x, 0f, Cfg.batAccel * dt);
            Body.Velocity.y = CBUtil.Approach(Body.Velocity.y, 3.0f, Cfg.batAccel * dt);
            Body.Step(dt);

            StateTimer -= dt;
            if (StateTimer <= 0f)
            {
                m_state = State.Hover;
                m_bobPhase = 0f;
            }
        }

        void TryContactDamage()
        {
            var results = CBActorRegistry.Scratch;
            int count = CBActorRegistry.QueryCircle(Body.Position, Body.HalfW + 0.25f, TeamKind.Player, results);
            for (int i = 0; i < count; i++)
            {
                var target = results[i];
                if (target == null || target.Dead) continue;
                Vector2 dir = (target.Body.Position - Body.Position).normalized;
                var info = new CBDamageInfo
                {
                    Amount = Cfg.batDamage,
                    Knockback = dir * Cfg.chaserKnockback * 0.8f,
                    Source = this
                };
                target.TakeDamage(info);
            }
            results.Clear();
        }

        protected override void OnDamaged(CBDamageInfo info)
        {
            base.OnDamaged(info);
            if (m_state == State.Hover)
            {
                m_state = State.Telegraph;
                StateTimer = Cfg.batDiveTelegraph * 0.5f;
            }
        }
    }
}
