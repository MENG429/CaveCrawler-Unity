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

        // Art mode: the fx plays frames from a CBArt clip instead of scaling a colour block.
        CBArt.Clip m_artClip;
        int m_artIndex = -1;
        float m_artScale = 1f;

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
            item.m_artClip = null;
            item.m_artIndex = -1;
            item.m_artScale = 1f;
            return item;
        }

        void Release()
        {
            gameObject.SetActive(false);
            if (!s_free.Contains(this)) s_free.Add(this);
        }

        /// <summary>
        /// Animated art fx (explosion / shockwave / impact). Falls back to false when the
        /// sheet is missing so callers keep their placeholder effect.
        /// size = desired world-space extent of the largest frame.
        /// </summary>
        public static bool SpawnArt(Vector2 position, float size, string sheetName, string clipName, float life)
        {
            CBArt.Clip clip;
            if (!CBArt.TryGetClip(sheetName, clipName, out clip)) return false;

            var fx = Acquire();
            fx.m_sr.sprite = clip.Frames[0];
            fx.m_sr.drawMode = SpriteDrawMode.Simple;
            fx.m_sr.color = Color.white;
            fx.m_sr.flipX = false;
            fx.m_sr.size = Vector2.zero;   // ignored in Simple mode

            fx.m_artClip = clip;
            fx.m_artIndex = -1;
            fx.m_artScale = clip.WorldSize > 0.0001f ? size / clip.WorldSize : 1f;
            fx.transform.localScale = new Vector3(fx.m_artScale, fx.m_artScale, 1f);

            fx.transform.position = new Vector3(position.x, position.y, 0f);
            fx.transform.rotation = Quaternion.identity;
            fx.m_startScale = 1f;
            fx.m_endScale = 1f;
            fx.m_life = life;
            fx.m_maxLife = life;
            fx.m_velocity = Vector2.zero;
            fx.m_spin = 0f;
            fx.m_fade = true;
            return true;
        }

        public static void SpawnRing(Vector2 position, float radius, Color colour, float life)
        {
            // Shockwave art when available; placeholder ring otherwise.
            if (SpawnArt(position, radius * 2.2f, "vfx_impact", "shock", Mathf.Max(0.3f, life * 1.4f))) return;

            var fx = Acquire();
            fx.m_sr.sprite = CBAssets.Solid(colour);
            fx.m_sr.color = colour;
            fx.m_sr.drawMode = SpriteDrawMode.Sliced;
            fx.m_sr.flipX = false;
            fx.transform.localScale = Vector3.one;
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

        /// <summary>Explosion puff: art clip with placeholder ring+burst fallback.</summary>
        public static void SpawnExplosion(Vector2 position, float radius)
        {
            if (SpawnArt(position, radius * 2.6f, "vfx_impact", "explosion", 0.4f)) return;
            SpawnRing(position, radius, CBAssets.Fire, 0.28f);
            SpawnBurst(position, 10, CBAssets.Fire, 9f, 0.35f);
        }

        /// <summary>Hit impact: art clip with placeholder burst fallback.</summary>
        public static void SpawnImpact(Vector2 position, float size)
        {
            if (SpawnArt(position, size, "vfx_impact", "impact", 0.25f)) return;
            SpawnBurst(position, 5, Color.white, 7f, 0.2f, 0.16f);
        }

        public static void SpawnSlash(Vector2 position, Vector2 size, bool flipX, Color colour, float life)
        {
            var fx = Acquire();
            fx.m_sr.sprite = CBAssets.Solid(colour);
            fx.m_sr.color = colour;
            fx.m_sr.drawMode = SpriteDrawMode.Sliced;
            fx.m_sr.flipX = flipX;
            fx.transform.localScale = Vector3.one;
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
                fx.m_sr.drawMode = SpriteDrawMode.Sliced;
                fx.m_sr.flipX = false;
                fx.transform.localScale = Vector3.one;
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

            if (m_artClip != null)
            {
                // Art fx: advance frames, fade out. No size animation (scale is baked in).
                int count = m_artClip.Frames.Length;
                int idx = Mathf.Min((int)((m_maxLife - m_life) * m_artClip.Fps), count - 1);
                if (idx != m_artIndex && m_artClip.Frames[idx] != null)
                {
                    m_artIndex = idx;
                    m_sr.sprite = m_artClip.Frames[idx];
                }

                if (m_fade)
                {
                    var c = m_sr.color;
                    c.a = Mathf.Clamp01(m_life / Mathf.Max(0.0001f, m_maxLife));
                    m_sr.color = c;
                }

                if (m_life <= 0f)
                {
                    s_active.Remove(this);
                    Release();
                }
                return;
            }

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
