using System.Collections.Generic;
using UnityEngine;

namespace CB
{
    /// <summary>
    /// Shared enemy plumbing: player tracking, throttled grid A* pathfinding,
    /// path steering with jump handling, and the loot drop on death.
    /// </summary>
    public abstract class CBEnemy : CBActor
    {
        protected CBConfig Cfg;
        protected CBTileWorld World;
        protected CBPathfinder Nav;
        protected CBPlayerController Target;

        protected readonly List<Vector2Int> m_path = new List<Vector2Int>();
        protected int m_pathIndex;
        float m_repathTimer;

        protected bool Activated;
        protected float StateTimer;

        public void InitEnemy(
            CBConfig config,
            CBTileWorld world,
            CBPathfinder nav,
            Vector2 size,
            Vector2 position,
            int hp,
            Color colour)
        {
            Team = TeamKind.Enemy;
            Init(config, world, size, position, hp);
            Cfg = config;
            World = world;
            Nav = nav;
            InvulnerableTime = config.enemyHitInvuln;
            SetColour(colour);
        }

        protected void RefreshTarget()
        {
            Target = CBGame.Instance != null ? CBGame.Instance.Player : null;
        }

        protected bool IsTargetValid()
        {
            return Target != null && !Target.Dead && Target.Body != null;
        }

        protected float DistanceToTarget()
        {
            if (!IsTargetValid()) return float.MaxValue;
            return Vector2.Distance(Body.Position, Target.Body.Position);
        }

        protected void TickPath(float dt, CBPathfinder.Mode mode, float interval)
        {
            if (Nav == null || World == null || !IsTargetValid()) return;

            m_repathTimer -= dt;
            if (m_repathTimer > 0f) return;
            m_repathTimer = interval > 0f ? interval : Cfg.enemyRepathInterval;

            var from = World.WorldToTile(Body.Position);
            var to = World.WorldToTile(Target.Body.Position);

            if (Nav.FindPath(from.x, from.y, to.x, to.y, mode, m_path))
            {
                m_pathIndex = 0;
            }
            else
            {
                m_path.Clear();
            }
        }

        /// <summary>Steers horizontally along the path (or straight at the target as a fallback).</summary>
        protected float SteerAlongPath(float speed, float accel, float dt)
        {
            float wantX = 0f;

            if (m_path.Count > 0)
            {
                while (m_pathIndex < m_path.Count)
                {
                    var node = m_path[m_pathIndex];
                    Vector2 wp = World.TileCenter(node.x, node.y);
                    if (Mathf.Abs(wp.x - Body.Position.x) < 0.45f && Mathf.Abs(wp.y - Body.Position.y) < 1.3f)
                    {
                        m_pathIndex++;
                        continue;
                    }
                    break;
                }

                if (m_pathIndex < m_path.Count)
                {
                    var node = m_path[m_pathIndex];
                    Vector2 wp = World.TileCenter(node.x, node.y);
                    float dx = wp.x - Body.Position.x;
                    if (Mathf.Abs(dx) > 0.12f) wantX = Mathf.Sign(dx);
                }
            }
            else if (IsTargetValid())
            {
                float dx = Target.Body.Position.x - Body.Position.x;
                if (Mathf.Abs(dx) > 0.3f) wantX = Mathf.Sign(dx);
            }

            float target = wantX * speed * SlowFactor;
            Body.Velocity.x = CBUtil.Approach(Body.Velocity.x, target, accel * dt);

            if (Mathf.Abs(wantX) > 0.01f) SetFacing(wantX > 0f);
            return wantX;
        }

        /// <summary>Jump when the next waypoint is above us or a wall blocks the way.</summary>
        protected bool TryPathJump(float jumpSpeed)
        {
            if (!Body.Grounded) return false;

            bool blocked = Body.HitWallLeft || Body.HitWallRight;
            bool above = false;

            if (m_path.Count > 0 && m_pathIndex < m_path.Count)
            {
                var node = m_path[m_pathIndex];
                Vector2 wp = World.TileCenter(node.x, node.y);
                above = wp.y > Body.Position.y + 0.7f;
            }

            if (!blocked && !above) return false;
            Body.Velocity.y = jumpSpeed;
            return true;
        }

        protected bool HasLineOfSightToTarget()
        {
            if (!IsTargetValid()) return false;
            return World.HasLineOfSight(Body.Position, Target.Body.Position);
        }

        protected override void OnDeath(CBDamageInfo info)
        {
            var colour = m_renderer != null ? m_renderer.color : Color.gray;
            CBFx.SpawnBurst(Body.Position, 9, colour, 9f, 0.35f, 0.18f);

            var cbGame = CBGame.Instance;
            if (cbGame != null) cbGame.DropLoot(Body.Position);

            base.OnDeath(info);
        }

        protected override void OnDamaged(CBDamageInfo info)
        {
            CBFx.SpawnImpact(Body.Position, 1.3f);
        }
    }
}
