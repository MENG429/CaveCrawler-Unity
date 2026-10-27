using UnityEngine;

namespace CB
{
    /// <summary>
    /// Melee chaser (goblin / orc). Walks the A* path, jumps when the path demands it,
    /// and swings a short-range hitbox when in range.
    /// </summary>
    public class CBEnemyMelee : CBEnemy
    {
        enum State { Chase, Windup, Active, Recover }
        State m_state = State.Chase;
        State m_lastArtState = (State)(-1);

        public int ContactDamage = 9;
        public float AttackRange = 1.25f;
        public float AttackKnockback = 7f;

        bool m_hitApplied;

        public void InitMelee(CBConfig config, CBTileWorld world, CBPathfinder nav, Vector2 position, bool heavy)
        {
            float w = heavy ? config.chaserWidth * 1.25f : config.chaserWidth;
            float h = heavy ? config.chaserHeight * 1.25f : config.chaserHeight;
            int hp = heavy ? Mathf.RoundToInt(config.chaserHP * 1.8f) : config.chaserHP;

            InitEnemy(config, world, nav, new Vector2(w, h), position, hp,
                heavy ? CBAssets.Bruiser : CBAssets.Chaser);
            BindArt("enemy_goblin", "idle");

            ContactDamage = heavy ? Mathf.RoundToInt(config.chaserDamage * 1.6f) : config.chaserDamage;
            AttackKnockback = heavy ? config.chaserKnockback * 1.5f : config.chaserKnockback;
            gameObject.name = heavy ? "CB_Bruiser" : "CB_Chaser";
        }

        void UpdateArt()
        {
            if (m_anim == null) return;

            string clip;
            bool restart = m_state != m_lastArtState;
            m_lastArtState = m_state;

            switch (m_state)
            {
                case State.Windup: clip = "windup"; break;
                case State.Active:
                case State.Recover: clip = "strike"; break;
                default: clip = Mathf.Abs(Body.Velocity.x) > 0.4f ? "run" : "idle"; restart = false; break;
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
                Body.Velocity.x = CBUtil.Approach(Body.Velocity.x, 0f, Cfg.chaserAccel * dt);
                Body.Step(dt);
                return;
            }

            float dist = DistanceToTarget();
            Activated = dist <= Cfg.enemyActivationRange;
            if (!Activated)
            {
                Body.Velocity.x = CBUtil.Approach(Body.Velocity.x, 0f, Cfg.chaserAccel * dt);
                Body.Step(dt);
                return;
            }

            switch (m_state)
            {
                case State.Chase:
                    StepChase(dt, dist);
                    break;

                case State.Windup:
                    Body.Velocity.x = CBUtil.Approach(Body.Velocity.x, 0f, Cfg.chaserAccel * dt);
                    Body.Step(dt);
                    FaceTowards(Target.Body.Position);
                    StateTimer -= dt;
                    if (StateTimer <= 0f) { m_state = State.Active; StateTimer = Cfg.chaserActive; m_hitApplied = false; }
                    break;

                case State.Active:
                    Body.Step(dt);
                    StateTimer -= dt;
                    if (!m_hitApplied) { m_hitApplied = true; TryHitPlayer(); }
                    if (StateTimer <= 0f) { m_state = State.Recover; StateTimer = Cfg.chaserRecover; }
                    break;

                case State.Recover:
                    Body.Velocity.x = CBUtil.Approach(Body.Velocity.x, 0f, Cfg.chaserAccel * dt);
                    Body.Step(dt);
                    StateTimer -= dt;
                    if (StateTimer <= 0f) m_state = State.Chase;
                    break;
            }
        }

        void StepChase(float dt, float dist)
        {
            TickPath(dt, CBPathfinder.Mode.Walker, Cfg.enemyRepathInterval);
            SteerAlongPath(Cfg.chaserSpeed, Cfg.chaserAccel, dt);
            TryPathJump(Cfg.chaserJumpSpeed);
            Body.Step(dt);

            bool verticalClose = Mathf.Abs(Target.Body.Position.y - Body.Position.y) < 1.6f;
            if (dist <= AttackRange && verticalClose && HasLineOfSightToTarget())
            {
                m_state = State.Windup;
                StateTimer = Cfg.chaserWindup;
                Body.Velocity.x = 0f;
            }
        }

        void TryHitPlayer()
        {
            bool right = FacingRight;
            var box = new Rect(
                Body.Position.x + (right ? 0f : -AttackRange),
                Body.Position.y - 0.8f,
                AttackRange,
                1.4f);

            var results = CBActorRegistry.Scratch;
            int count = CBActorRegistry.QueryBox(box, TeamKind.Player, results);
            for (int i = 0; i < count; i++)
            {
                var target = results[i];
                if (target == null || target.Dead) continue;
                float dirX = target.Body.Position.x >= Body.Position.x ? 1f : -1f;

                var info = new CBDamageInfo
                {
                    Amount = ContactDamage,
                    Knockback = new Vector2(dirX * AttackKnockback, AttackKnockback * 0.3f),
                    Source = this
                };
                target.TakeDamage(info);
            }
            results.Clear();

            CBFx.SpawnSlash(Body.Position + new Vector2(right ? 0.7f : -0.7f, 0f),
                new Vector2(1.1f, 0.9f), !right, CBAssets.Fire, 0.12f);
        }
    }
}
