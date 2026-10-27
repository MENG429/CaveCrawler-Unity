using UnityEngine;

namespace CB
{
    /// <summary>
    /// Orthographic follow camera. It SNAPS to the player the moment it is initialised -
    /// otherwise the player spawns off-screen and the game looks like the controls are dead.
    /// </summary>
    public class CBCamera2D : MonoBehaviour
    {
        static CBCamera2D s_instance;
        public static CBCamera2D Instance { get { return s_instance; } }

        CBConfig m_config;
        Camera m_camera;
        CBPlayerController m_target;

        Vector2 m_lookAhead;
        float m_shake;

        float m_minX, m_maxX, m_minY, m_maxY;
        bool m_hasBounds;

        public void Init(CBConfig config, CBPlayerController target)
        {
            m_config = config;
            m_target = target;

            m_camera = GetComponent<Camera>();
            if (m_camera == null) m_camera = gameObject.AddComponent<Camera>();

            m_camera.orthographic = true;
            m_camera.orthographicSize = config.cameraOrthoSize;
            m_camera.clearFlags = CameraClearFlags.SolidColor;
            m_camera.backgroundColor = new Color(0.02f, 0.02f, 0.04f);
            m_camera.nearClipPlane = -100f;
            m_camera.farClipPlane = 100f;

            s_instance = this;
            SnapToTarget();
        }

        public void SetBounds(CBTileWorld world)
        {
            if (world == null) { m_hasBounds = false; return; }

            float halfHeight = m_config.cameraOrthoSize;
            float halfWidth = halfHeight * ((m_camera != null && m_camera.aspect > 0f) ? m_camera.aspect : (16f / 9f));

            m_minX = halfWidth;
            m_maxX = world.Width - halfWidth;
            m_minY = halfHeight;
            m_maxY = world.Height - halfHeight;

            if (m_minX > m_maxX) { m_minX = m_maxX = world.Width * 0.5f; }
            if (m_minY > m_maxY) { m_minY = m_maxY = world.Height * 0.5f; }
            m_hasBounds = true;

            SnapToTarget();
        }

        /// <summary>Re-bind to a new player (used after a run restart).</summary>
        public void SetTarget(CBPlayerController target)
        {
            m_target = target;
            SnapToTarget();
        }

        public void SnapToTarget()
        {
            if (m_target == null || m_target.Body == null) return;

            m_lookAhead = Vector2.zero;
            Vector2 p = m_target.Body.Position;
            transform.position = new Vector3(p.x, p.y, -10f);
            ClampToBounds();
        }

        public void Shake(float amount)
        {
            if (amount > m_shake) m_shake = amount;
        }

        void LateUpdate()
        {
            if (m_target == null || m_target.Body == null) return;

            float dt = Time.deltaTime;

            // Look ahead in the direction of travel.
            Vector2 target = m_target.Body.Position;
            float desiredLook = CBUtil.SignOrZero(m_target.Body.Velocity.x) * m_config.cameraLookAhead;
            m_lookAhead.x = Mathf.Lerp(m_lookAhead.x, desiredLook, 1f - Mathf.Exp(-m_config.cameraLookAheadLerp * dt));

            Vector2 desired = target + m_lookAhead;
            Vector3 current = transform.position;
            float k = 1f - Mathf.Exp(-m_config.cameraFollowLerp * dt);

            Vector3 next = new Vector3(
                Mathf.Lerp(current.x, desired.x, k),
                Mathf.Lerp(current.y, desired.y, k),
                -10f);

            transform.position = next;
            ClampToBounds();

            if (m_shake > 0f)
            {
                m_shake -= m_config.cameraShakeDecay * dt * m_shake;
                if (m_shake < 0.001f) m_shake = 0f;
                else
                {
                    // Deterministic wobble - no UnityEngine.Random anywhere in gameplay.
                    float t = Time.time * 47f;
                    float sx = Mathf.Sin(t) * m_shake;
                    float sy = Mathf.Cos(t * 1.37f) * m_shake;
                    transform.position += new Vector3(sx, sy, 0f);
                }
            }
        }

        void ClampToBounds()
        {
            if (!m_hasBounds) return;
            var p = transform.position;
            p.x = Mathf.Clamp(p.x, m_minX, m_maxX);
            p.y = Mathf.Clamp(p.y, m_minY, m_maxY);
            transform.position = p;
        }
    }
}
