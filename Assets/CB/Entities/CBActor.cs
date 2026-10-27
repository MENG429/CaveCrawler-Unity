using System.Collections.Generic;
using UnityEngine;

namespace CB
{
    public struct CBDamageInfo
    {
        public int Amount;
        public Vector2 Knockback;
        public CBActor Source;
        public CBWeaponDef Weapon;
        public bool IsExplosion;
        public bool IgnoreInvulnerability;
    }

    /// <summary>
    /// Base for player and enemies: health, invulnerability frames, knockback,
    /// burn/poison/slow status, and loot dropping on death.
    ///
    /// IMPORTANT: fields are assigned by the caller after AddComponent and read inside
    /// Init()/Start(), never in Awake() - AddComponent runs Awake before the caller can
    /// assign anything.
    /// </summary>
    public abstract class CBActor : MonoBehaviour, ICBFixedStep
    {
        public enum TeamKind { Player, Enemy }

        public TeamKind Team;

        protected CBBody m_body;
        protected SpriteRenderer m_renderer;
        protected CBConfig m_config;

        public int MaxHP = 1;
        public int HP = 1;
        public bool Dead { get; private set; }

        public bool FacingRight = true;
        public float InvulnerableTime = 0f;
        float m_invulnTimer;

        // Status effects
        float m_burnTimer;
        float m_poisonTimer;
        float m_slowTimer;
        float m_statusTick;

        Color m_baseColour = Color.white;
        float m_flashTimer;

        // Pixel-art animation (null when the art sheet is unavailable -> placeholder path).
        protected CBAnimPlayer m_anim;
        protected string m_artSheet;
        protected bool m_artFacesLeft;

        public bool IsInvulnerable { get { return m_invulnTimer > 0f; } }
        /// <summary>Fraction of incoming knockback ignored (0 = full, 1 = immune). Player overrides.</summary>
        public virtual float KnockbackResist { get { return 0f; } }
        public float SlowFactor { get { return m_slowTimer > 0f ? m_config.slowFactor : 1f; } }
        public bool IsBurning { get { return m_burnTimer > 0f; } }
        public bool IsPoisoned { get { return m_poisonTimer > 0f; } }

        public CBBody Body { get { return m_body; } }
        public SpriteRenderer Renderer { get { return m_renderer; } }

        public virtual void Init(CBConfig config, CBTileWorld world, Vector2 size, Vector2 position, int hp)
        {
            m_config = config;
            MaxHP = hp;
            HP = hp;
            Dead = false;

            m_body = gameObject.AddComponent<CBBody>();
            m_renderer = gameObject.AddComponent<SpriteRenderer>();
            m_renderer.sprite = CBAssets.Solid(Color.white);
            m_renderer.drawMode = SpriteDrawMode.Sliced;
            m_renderer.size = size;
            m_renderer.sortingOrder = Team == TeamKind.Player ? 20 : 15;

            m_body.Init(world, size, position);
            m_body.Gravity = config.gravity;
            m_body.FallGravityMultiplier = config.fallGravityMultiplier;
            m_body.MaxFallSpeed = config.maxFallSpeed;
            m_body.SyncTransform();

            CBActorRegistry.Add(this);
            CBGameClock.Instance.Register(this, Team == TeamKind.Player ? CBStepOrder.Player : CBStepOrder.Enemy);
        }

        public void SetColour(Color c)
        {
            m_baseColour = c;
            if (m_renderer != null) m_renderer.color = c;
        }

        // ---------------- Pixel-art binding ----------------
        /// <summary>
        /// Attaches the art animation player when the sheet exists. Anything art-driven
        /// swaps the renderer from the placeholder (Sliced colour block) to the sheet's
        /// sprites; when the sheet is missing this is a no-op and nothing downstream can
        /// null-ref because every PlayArt call re-checks.
        /// </summary>
        protected void BindArt(string sheetName, string clipName)
        {
            CBArt.Clip clip;
            if (!CBArt.TryGetClip(sheetName, clipName, out clip)) return;
            if (m_renderer == null) return;

            m_artSheet = sheetName;
            m_artFacesLeft = CBArt.FacesLeft(sheetName);
            m_anim = new CBAnimPlayer(m_renderer);
            m_anim.Play(clip, true);

            // Art must not be tinted by the placeholder colour; status effects still tint
            // from white via FixedStep.
            m_baseColour = Color.white;
            m_renderer.color = Color.white;
        }

        /// <summary>Switch animation clip. Safe (no-op) when art is not bound or clip missing.</summary>
        protected void PlayArt(string clipName, bool restart = false)
        {
            if (m_anim == null || string.IsNullOrEmpty(clipName) || m_artSheet == null) return;
            CBArt.Clip clip;
            if (!CBArt.TryGetClip(m_artSheet, clipName, out clip)) return;
            m_anim.Play(clip, restart);
        }

        /// <summary>Advance the bound animation. Call once per FixedStep.</summary>
        protected void TickArt(float dt)
        {
            if (m_anim != null) m_anim.Tick(dt);
        }

        protected virtual void OnDestroyed()
        {
            CBActorRegistry.Remove(this);
            if (CBGameClock.Instance != null) CBGameClock.Instance.Unregister(this);
        }

        void OnDestroy()
        {
            OnDestroyed();
        }

        public virtual void FixedStep(float dt)
        {
            if (Dead) return;

            if (m_invulnTimer > 0f) m_invulnTimer -= dt;

            if (m_slowTimer > 0f) m_slowTimer -= dt;
            if (m_burnTimer > 0f) m_burnTimer -= dt;
            if (m_poisonTimer > 0f) m_poisonTimer -= dt;

            m_statusTick -= dt;
            if (m_statusTick <= 0f)
            {
                m_statusTick = m_config.statusTickInterval;
                int dot = 0;
                if (m_burnTimer > 0f) dot += m_config.burnDps;
                if (m_poisonTimer > 0f) dot += m_config.poisonDps;
                if (dot > 0) ApplyDamageSilent(dot);
            }

            if (m_flashTimer > 0f)
            {
                m_flashTimer -= dt;
                if (m_renderer != null)
                    m_renderer.color = m_flashTimer > 0f ? Color.white : m_baseColour;
            }
            else if (m_renderer != null && m_burnTimer > 0f)
            {
                m_renderer.color = Color.Lerp(m_baseColour, CBAssets.Fire, 0.35f);
            }
            else if (m_renderer != null && m_poisonTimer > 0f)
            {
                m_renderer.color = Color.Lerp(m_baseColour, CBAssets.Poison, 0.35f);
            }
            else if (m_renderer != null && m_slowTimer > 0f)
            {
                m_renderer.color = Color.Lerp(m_baseColour, CBAssets.Ice, 0.35f);
            }
            else if (m_renderer != null)
            {
                m_renderer.color = m_baseColour;
            }
        }

        /// <summary>Returns true when the hit landed.</summary>
        public bool TakeDamage(CBDamageInfo info)
        {
            if (Dead) return false;
            if (!info.IgnoreInvulnerability && m_invulnTimer > 0f) return false;

            HP -= Mathf.Max(0, info.Amount);
            m_invulnTimer = InvulnerableTime;
            m_flashTimer = 0.08f;

            if (m_body != null && info.Knockback.sqrMagnitude > 0.0001f)
            {
                float resist = 1f - Mathf.Clamp01(KnockbackResist);
                m_body.Velocity += info.Knockback * resist;
            }

            OnDamaged(info);

            if (HP <= 0)
            {
                HP = 0;
                Die(info);
            }
            return true;
        }

        void ApplyDamageSilent(int amount)
        {
            HP -= amount;
            if (HP <= 0)
            {
                HP = 0;
                var info = new CBDamageInfo { Amount = amount };
                Die(info);
            }
        }

        protected virtual void OnDamaged(CBDamageInfo info) { }

        public void ApplyKnockback(Vector2 impulse)
        {
            if (m_body != null) m_body.Velocity += impulse;
        }

        public void ApplyBurn() { m_burnTimer = m_config.burnDuration; }
        public void ApplyPoison() { m_poisonTimer = m_config.poisonDuration; }
        public void ApplySlow() { m_slowTimer = m_config.slowDuration; }

        protected virtual void Die(CBDamageInfo info)
        {
            if (Dead) return;
            Dead = true;
            OnDeath(info);
        }

        protected virtual void OnDeath(CBDamageInfo info)
        {
            Destroy(gameObject);
        }

        public void SetFacing(bool right)
        {
            FacingRight = right;
            if (m_renderer != null) m_renderer.flipX = m_artFacesLeft ? right : !right;
        }

        public void FaceTowards(Vector2 target)
        {
            if (m_body == null) return;
            SetFacing(target.x >= m_body.Position.x);
        }

        public static bool IsHostile(CBActor a, CBActor b)
        {
            if (a == null || b == null) return false;
            return a.Team != b.Team;
        }
    }
}
