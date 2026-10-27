using UnityEngine;

namespace CB
{
    /// <summary>
    /// AABB body resolved against the tile grid. Axis separated, sub-stepped so nothing
    /// tunnels at high fall speed. Position is the CENTRE of the box (matches SpriteRenderer).
    ///
    /// Deliberately not a Rigidbody2D: exact control over gravity / coyote / variable jump
    /// is what the replica's feel depends on.
    /// </summary>
    public class CBBody : MonoBehaviour
    {
        const float MaxSubStep = 0.10f;
        const float SnapStep = 0.01f;
        const float GroundProbe = 0.06f;

        Vector2 m_size = new Vector2(0.8f, 1.6f);

        public Vector2 Position;
        public Vector2 Velocity;

        public CBTileWorld World;

        public bool Grounded;
        public bool WasGrounded;
        public bool HitCeiling;
        public bool HitWallLeft;
        public bool HitWallRight;

        public bool UseGravity = true;
        public float GravityScale = 1f;
        public float Gravity = 46f;
        public float FallGravityMultiplier = 1.35f;
        public float MaxFallSpeed = 28f;

        public Vector2 Size { get { return m_size; } }
        public float HalfW { get { return m_size.x * 0.5f; } }
        public float HalfH { get { return m_size.y * 0.5f; } }
        public Vector2 Feet { get { return new Vector2(Position.x, Position.y - HalfH); } }
        public Rect Box { get { return new Rect(Position.x - HalfW, Position.y - HalfH, m_size.x, m_size.y); } }

        public void Init(CBTileWorld world, Vector2 size, Vector2 position)
        {
            World = world;
            m_size = size;
            Position = position;
            Velocity = Vector2.zero;
        }

        public void SetSize(Vector2 size) { m_size = size; }

        public void Teleport(Vector2 p)
        {
            Position = p;
            Velocity = Vector2.zero;
            UpdateGrounded();
            SyncTransform();
        }

        public Rect BoxAt(Vector2 p)
        {
            return new Rect(p.x - HalfW, p.y - HalfH, m_size.x, m_size.y);
        }

        public bool OverlapsSolid(Vector2 p)
        {
            return World != null && World.OverlapsSolid(BoxAt(p));
        }

        /// <summary>Integrate gravity, then move. Call once per fixed step.</summary>
        public void Step(float dt)
        {
            WasGrounded = Grounded;

            if (UseGravity)
            {
                float g = Gravity * GravityScale;
                if (Velocity.y < 0f) g *= FallGravityMultiplier;
                Velocity.y -= g * dt;
                if (Velocity.y < -MaxFallSpeed) Velocity.y = -MaxFallSpeed;
            }

            Move(Velocity * dt);
        }

        public void Move(Vector2 delta)
        {
            HitCeiling = false;
            HitWallLeft = false;
            HitWallRight = false;

            MoveAxis(new Vector2(delta.x, 0f));
            MoveAxis(new Vector2(0f, delta.y));

            UpdateGrounded();

            // Kill the stored velocity component that got blocked, otherwise the next
            // step keeps pushing into the wall and the actor "sticks".
            if (HitWallLeft && Velocity.x < 0f) Velocity.x = 0f;
            if (HitWallRight && Velocity.x > 0f) Velocity.x = 0f;
            if (HitCeiling && Velocity.y > 0f) Velocity.y = 0f;

            SyncTransform();
        }

        void MoveAxis(Vector2 delta)
        {
            float dist = delta.x != 0f ? delta.x : delta.y;
            if (Mathf.Abs(dist) < 1e-7f) return;

            Vector2 dir = delta.x != 0f
                ? new Vector2(Mathf.Sign(delta.x), 0f)
                : new Vector2(0f, Mathf.Sign(delta.y));

            float remaining = Mathf.Abs(dist);
            bool blocked = false;

            while (remaining > 1e-6f)
            {
                float s = Mathf.Min(remaining, MaxSubStep);
                Vector2 test = Position + dir * s;
                if (World != null && World.OverlapsSolid(BoxAt(test)))
                {
                    blocked = true;
                    break;
                }
                Position = test;
                remaining -= s;
            }

            if (!blocked) return;

            // Nudge flush against the surface so grounded / wall checks stay reliable.
            float moved = 0f;
            while (moved < MaxSubStep)
            {
                Vector2 test = Position + dir * SnapStep;
                if (World != null && World.OverlapsSolid(BoxAt(test))) break;
                Position = test;
                moved += SnapStep;
            }

            if (dir.x > 0f) HitWallRight = true;
            else if (dir.x < 0f) HitWallLeft = true;
            else if (dir.y > 0f) HitCeiling = true;
        }

        void UpdateGrounded()
        {
            if (World == null) { Grounded = false; return; }
            if (Velocity.y > 0.001f) { Grounded = false; return; }

            Rect probe = new Rect(
                Position.x - HalfW + 0.02f,
                Position.y - HalfH - GroundProbe,
                m_size.x - 0.04f,
                GroundProbe);

            Grounded = World.OverlapsSolid(probe);
        }

        public void SyncTransform()
        {
            transform.position = new Vector3(Position.x, Position.y, 0f);
        }
    }
}
