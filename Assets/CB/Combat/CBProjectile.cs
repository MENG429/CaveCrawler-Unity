using UnityEngine;

namespace CB
{
    /// <summary>
    /// Parabolic arrow. Arrows have no ammo cost in Caveblazers - the bow is balanced by
    /// fire interval and damage only, so nothing here consumes a resource.
    /// </summary>
    public class CBProjectile : MonoBehaviour, ICBFixedStep
    {
        CBTileWorld m_world;
        CBConfig m_config;
        CBBowDef m_bow;

        Vector2 m_position;
        Vector2 m_velocity;
        float m_gravity;
        float m_life;
        int m_damage;
        float m_knockback;
        int m_pierceLeft;
        CBActor.TeamKind m_targetTeam;
        CBActor m_owner;
        Color m_colour;
        CBWeaponDef m_reclaim;

        SpriteRenderer m_sr;
        bool m_dead;

        /// <summary>Assign-then-Init: never read assigned fields in Awake.</summary>
        public void Init(
            CBConfig config,
            CBTileWorld world,
            Vector2 position,
            Vector2 velocity,
            float gravity,
            int damage,
            float knockback,
            int pierce,
            CBActor.TeamKind targetTeam,
            CBActor owner,
            Color colour,
            CBBowDef bow)
        {
            m_config = config;
            m_world = world;
            m_position = position;
            m_velocity = velocity;
            m_gravity = gravity;
            m_damage = damage;
            m_knockback = knockback;
            m_pierceLeft = pierce;
            m_targetTeam = targetTeam;
            m_owner = owner;
            m_colour = colour;
            m_bow = bow;
            m_life = config.arrowLife;
            m_dead = false;

            m_sr = gameObject.AddComponent<SpriteRenderer>();
            m_sr.sprite = CBAssets.Solid(colour);
            m_sr.color = colour;
            m_sr.drawMode = SpriteDrawMode.Sliced;
            m_sr.size = new Vector2(0.55f, 0.16f);
            m_sr.sortingOrder = 25;

            ApplyArt();

            transform.position = new Vector3(position.x, position.y, 0f);
            Orient();

            CBGameClock.Instance.Register(this, CBStepOrder.Projectile);
        }

        /// <summary>
        /// Swaps the placeholder block for a projectile sprite. Sprites face right, which
        /// matches Orient()'s Atan2 convention. Missing sheet -> placeholder stays.
        /// </summary>
        void ApplyArt()
        {
            string clipName;
            if (m_bow != null)
            {
                switch (m_bow.Id)
                {
                    case "bow_fire": clipName = "p_fire"; break;
                    case "bow_ice": clipName = "p_ice"; break;
                    case "bow_poison": clipName = "p_poison"; break;
                    case "bow_hand_cannon": clipName = "p_ball"; break;
                    default: clipName = "p_arrow"; break;
                }
            }
            else
            {
                // Enemy arrows target the player (the archer has no bow def).
                clipName = m_targetTeam == CBActor.TeamKind.Player ? "p_enemy" : "p_arrow";
            }

            var sprite = CBArt.FirstSprite("projectiles", clipName);
            if (sprite == null) return;

            m_sr.sprite = sprite;
            m_sr.drawMode = SpriteDrawMode.Simple;
            m_sr.color = Color.white;
        }

        public void FixedStep(float dt)
        {
            if (m_dead) return;

            m_life -= dt;
            if (m_life <= 0f) { Despawn(false); return; }

            m_velocity.y -= m_gravity * dt;

            // Sub-step so fast arrows cannot tunnel through a 1-unit tile.
            Vector2 delta = m_velocity * dt;
            float dist = delta.magnitude;
            int steps = Mathf.Max(1, Mathf.CeilToInt(dist / 0.25f));
            Vector2 step = delta / steps;

            for (int i = 0; i < steps; i++)
            {
                m_position += step;

                if (m_world != null && m_world.IsSolidAtWorld(m_position))
                {
                    Despawn(true);
                    return;
                }

                if (TryHitActors()) return;
            }

            transform.position = new Vector3(m_position.x, m_position.y, 0f);
            Orient();
        }

        void Orient()
        {
            float angle = Mathf.Atan2(m_velocity.y, m_velocity.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }

        bool TryHitActors()
        {
            var results = CBActorRegistry.Scratch;
            int count = CBActorRegistry.QueryCircle(m_position, m_config.arrowRadius + 0.18f, m_targetTeam, results);

            for (int i = 0; i < count; i++)
            {
                var target = results[i];
                if (target == null || target.Dead || target == m_owner) continue;

                Vector2 dir = m_velocity.sqrMagnitude > 0.001f ? m_velocity.normalized : new Vector2(1f, 0f);

                var info = new CBDamageInfo
                {
                    Amount = m_damage,
                    Knockback = dir * m_knockback,
                    Source = m_owner,
                    Weapon = m_bow
                };

                if (!target.TakeDamage(info)) continue;

                ApplyProcs(target);
                if (target.Dead && m_owner is CBPlayerController)
                    ((CBPlayerController)m_owner).Combat.NotifyKill(target);
                CBFx.SpawnImpact(m_position, 1.2f);

                if (m_bow != null && m_bow.ExplodesOnHit)
                {
                    CBExplosion.Detonate(
                        m_position,
                        m_bow.ExplosionRadius,
                        m_bow.ExplosionDamage,
                        m_config.explosionKnockback,
                        m_targetTeam,
                        m_owner,
                        m_bow,
                        true);
                    Despawn(true);
                    return true;
                }

                if (m_pierceLeft > 0)
                {
                    m_pierceLeft--;
                }
                else
                {
                    Despawn(true);
                    return true;
                }
            }

            results.Clear();
            return false;
        }

        void ApplyProcs(CBActor target)
        {
            if (m_bow == null) return;
            var rng = CBGame.Instance != null ? CBGame.Instance.Rng : null;
            if (rng == null) return;

            if (m_bow.PoisonChance > 0f && rng.Chance(m_bow.PoisonChance)) target.ApplyPoison();
            if (m_bow.BurnChance > 0f && rng.Chance(m_bow.BurnChance)) target.ApplyBurn();
            if (m_bow.SlowChance > 0f && rng.Chance(m_bow.SlowChance)) target.ApplySlow();
        }

        /// <summary>Mark the thrown weapon to drop back as a reclaimable pickup when this projectile ends.</summary>
        public void SetReclaim(CBWeaponDef weapon) { m_reclaim = weapon; }

        void Despawn(bool impact)
        {
            if (m_dead) return;
            m_dead = true;
            if (impact) CBFx.SpawnBurst(m_position, 4, m_colour, 5f, 0.18f, 0.12f);

            if (m_reclaim != null && CBGame.Instance != null && CBGame.Instance.PickupRoot != null)
            {
                CBPickup.Spawn(m_config, m_world, CBGame.Instance.PickupRoot, m_position, CBPickup.Kind.Weapon, 0, m_reclaim);
            }

            if (CBGameClock.Instance != null) CBGameClock.Instance.Unregister(this);
            Destroy(gameObject);
        }

        void OnDestroy()
        {
            if (CBGameClock.Instance != null) CBGameClock.Instance.Unregister(this);
        }

        public static CBProjectile Spawn(
            CBConfig config,
            CBTileWorld world,
            Vector2 position,
            Vector2 direction,
            float speed,
            float gravity,
            int damage,
            float knockback,
            int pierce,
            CBActor.TeamKind targetTeam,
            CBActor owner,
            Color colour,
            CBBowDef bow)
        {
            var host = CBGame.Instance != null ? CBGame.Instance.ProjectileRoot : null;
            var go = new GameObject("CB_Arrow");
            if (host != null) go.transform.SetParent(host, false);
            go.transform.position = new Vector3(position.x, position.y, 0f);

            var proj = go.AddComponent<CBProjectile>();
            proj.Init(config, world, position, direction.normalized * speed, gravity, damage,
                knockback, pierce, targetTeam, owner, colour, bow);
            return proj;
        }
    }
}
