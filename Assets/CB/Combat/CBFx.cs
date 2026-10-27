using System.Collections.Generic;
using UnityEngine;

namespace CB
{
    /// <summary>
    /// Cosmetic-only FX. Runs on real time (MonoBehaviour.Update) so it keeps animating
    /// during hitstop, which reads better than freezing the flash.
    /// Pooled - no runtime allocation after warm-up.
    /// </summary>
    public class CBFx : MonoBehaviour
    {
        static Transform s_host;
        static readonly List<CBFx> s_free = new List<CBFx>();
        static readonly List<CBFx> s_active = new List<CBFx>();

        SpriteRenderer m_sr;
        float m_life;
        float m_maxLife;
        float m_startScale;
        float m_endScale;
        Vector2 m_velocity;
        float m_spin;
        bool m_fade;

        public static void EnsureHost()
        {
            if (s_host != null) return;
            var go = new GameObject("CB_Fx");
            DontDestroyOnLoad(go);
            s_host = go.transform;
        }

        public static void ResetAll()
        {
            for (int i = s_active.Count - 1; i >= 0; i--)
            {
                if (s_active[i] != null) s_active[i].Release();
            }
            s_active.Clear();
            s_free.Clear();
        }

        static CBFx Acquire()
        {
            EnsureHost();
            CBFx item;
            if (s_free.Count > 0)
            {
                item = s_free[s_free.Count - 1];
                s_free.RemoveAt(s_free.Count - 1);
            }
            else
            {
                var go = new GameObject("fx");
                go.transform.SetParent(s_host, false);
                item = go.AddComponent<CBFx>();
                item.m_sr = go.AddComponent<SpriteRenderer>();
                item.m_sr.drawMode = SpriteDrawMode.Sliced;
            }
            item.gameObject.SetActive(true);
            s_active.Add(item);
            return item;
        }

        void Release()
        {
            gameObject.SetActive(false);
            if (!s_free.Contains(this)) s_free.Add(this);
        }

        public static void SpawnRing(Vector2 position, float radius, Color colour, float life)
        {
            var fx = Acquire();
            fx.m_sr.sprite = CBAssets.Solid(colour);
            fx.m_sr.color = colour;
            fx.m_sr.flipX = false;
            fx.transform.position = new Vector3(position.x, position.y, 0f);
            fx.transform.rotation = Quaternion.identity;
            fx.m_sr.size = new Vector2(0.35f, 0.35f);
            fx.m_startScale = 0.35f;
            fx.m_endScale = radius * 2f;
            fx.m_life = life;
            fx.m_maxLife = life;
            fx.m_velocity = Vector2.zero;
            fx.m_spin = 0f;
            fx.m_fade = true;
        }

        public static void SpawnSlash(Vector2 position, Vector2 size, bool flipX, Color colour, float life)
        {
            var fx = Acquire();
            fx.m_sr.sprite = CBAssets.Solid(colour);
            fx.m_sr.color = colour;
            fx.m_sr.flipX = flipX;
            fx.transform.position = new Vector3(position.x, position.y, 0f);
            fx.transform.rotation = Quaternion.identity;
            fx.m_sr.size = size;
            fx.m_startScale = 1f;
            fx.m_endScale = 1f;
            fx.m_life = life;
            fx.m_maxLife = life;
            fx.m_velocity = Vector2.zero;
            fx.m_spin = 0f;
            fx.m_fade = true;
        }

        public static void SpawnBurst(Vector2 position, int count, Color colour, float speed, float life, float size = 0.18f)
        {
            for (int i = 0; i < count; i++)
            {
                var fx = Acquire();
                fx.m_sr.sprite = CBAssets.Solid(colour);
                fx.m_sr.color = colour;
                fx.m_sr.flipX = false;
                fx.transform.position = new Vector3(position.x, position.y, 0f);
                fx.transform.rotation = Quaternion.identity;
                fx.m_sr.size = new Vector2(size, size);
                fx.m_startScale = 1f;
                fx.m_endScale = 0.2f;

                float angle = (i / (float)Mathf.Max(1, count)) * Mathf.PI * 2f;
                fx.m_velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;
                fx.m_life = life;
                fx.m_maxLife = life;
                fx.m_spin = 0f;
                fx.m_fade = true;
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            m_life -= dt;

            if (m_velocity.sqrMagnitude > 0.0001f)
            {
                var p = transform.position;
                p.x += m_velocity.x * dt;
                p.y += m_velocity.y * dt;
                transform.position = p;
                m_velocity *= (1f - Mathf.Min(1f, 3f * dt));
            }

            float t = 1f - Mathf.Clamp01(m_life / Mathf.Max(0.0001f, m_maxLife));
            float scale = Mathf.Lerp(m_startScale, m_endScale, t);
            m_sr.size = new Vector2(scale, scale);

            if (m_fade)
            {
                var c = m_sr.color;
                c.a = Mathf.Clamp01(1f - t);
                m_sr.color = c;
            }

            if (m_life <= 0f)
            {
                s_active.Remove(this);
                Release();
            }
        }

        void OnDestroy()
        {
            s_active.Remove(this);
            s_free.Remove(this);
        }
    }
}
