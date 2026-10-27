using UnityEngine;

namespace CB
{
    /// <summary>
    /// Coin / health potion / dropped weapon. Falls with tile collision, drifts toward the
    /// player when close (magnet), and is consumed on contact.
    /// </summary>
    public class CBPickup : MonoBehaviour, ICBFixedStep
    {
        public enum Kind { Coin, Potion, Weapon, Equipment }

        CBConfig m_config;
        CBTileWorld m_world;
        CBBody m_body;
        SpriteRenderer m_sr;

        Kind m_kind;
        int m_amount;
        CBWeaponDef m_weapon;
        CBEquipDef m_equip;
        float m_age;
        bool m_consumed;

        public void Init(CBConfig config, CBTileWorld world, Vector2 position, Kind kind, int amount, CBWeaponDef weapon, CBEquipDef equip = null)
        {
            m_config = config;
            m_world = world;
            m_kind = kind;
            m_amount = amount;
            m_weapon = weapon;
            m_equip = equip;
            m_age = 0f;

            float size = kind == Kind.Weapon ? 0.55f : (kind == Kind.Equipment ? 0.45f : 0.38f);
            Color colour = kind == Kind.Coin ? CBAssets.Coin
                : kind == Kind.Potion ? CBAssets.Potion
                : kind == Kind.Equipment ? (equip != null ? equip.Tint : Color.white)
                : (weapon != null ? weapon.Tint : Color.white);

            m_body = gameObject.AddComponent<CBBody>();
            m_body.Init(world, new Vector2(size, size), position);
            m_body.Gravity = config.gravity;
            m_body.MaxFallSpeed = config.maxFallSpeed;

            m_sr = CBAssets.MakeSprite(gameObject, colour, new Vector2(size, size), 12);
            ApplyArt();

            // Small pop so drops read clearly.
            m_body.Velocity = new Vector2(RandomPop(), 5.5f);

            gameObject.name = "CB_Pickup_" + kind;
            CBGameClock.Instance.Register(this, CBStepOrder.Items);
        }

        CBAnimPlayer m_anim;

        /// <summary>
        /// Coin spin / potion / weapon art. Weapons map by id through CBArt.WeaponClip.
        /// Any missing sheet leaves the placeholder colour block in place.
        /// </summary>
        void ApplyArt()
        {
            string sheet = null;
            string clipName = null;

            if (m_kind == Kind.Coin) { sheet = "pickups"; clipName = "coin"; }
            else if (m_kind == Kind.Potion) { sheet = "pickups"; clipName = "potion"; }
            else if (m_weapon != null)
            {
                CBArt.Clip weaponClip = CBArt.WeaponClip(m_weapon);
                if (weaponClip == null) return;
                m_anim = new CBAnimPlayer(m_sr);
                m_anim.Play(weaponClip, true);
                return;
            }

            CBArt.Clip clip;
            if (sheet == null || !CBArt.TryGetClip(sheet, clipName, out clip)) return;

            m_anim = new CBAnimPlayer(m_sr);
            m_anim.Play(clip, true);
        }

        float RandomPop()
        {
            var rng = CBGame.Instance != null ? CBGame.Instance.Rng : null;
            return rng != null ? rng.Float(-3.5f, 3.5f) : 0f;
        }

        void OnDestroy()
        {
            if (CBGameClock.Instance != null) CBGameClock.Instance.Unregister(this);
        }

        public void FixedStep(float dt)
        {
            if (m_consumed) return;
            m_age += dt;

            var player = CBGame.Instance != null ? CBGame.Instance.Player : null;

            if (player != null && !player.Dead && player.Body != null)
            {
                Vector2 delta = player.Body.Position - m_body.Position;
                float dist = delta.magnitude;

                if (dist < m_config.pickupMagnetRadius + 0.55f && m_age > 0.25f)
                {
                    Collect(player);
                    return;
                }

                if (dist < m_config.pickupMagnetRadius * 3.2f && m_age > 0.35f)
                {
                    Vector2 pull = delta.normalized * m_config.pickupMagnetForce * dt;
                    m_body.Velocity += pull;
                }
            }

            m_body.Step(dt);

            if (m_anim != null) m_anim.Tick(dt);

            // Bob for readability.
            if (m_sr != null && m_body.Grounded)
            {
                m_sr.transform.localPosition = new Vector3(0f, Mathf.Sin(m_age * 5f) * 0.06f, 0f);
            }
        }

        void Collect(CBPlayerController player)
        {
            if (m_kind == Kind.Coin)
            {
                float coinMul = (player.Loadout != null) ? player.Loadout.Mods.coinMul : 1f;
                player.Coins += Mathf.Max(1, Mathf.RoundToInt(m_amount * coinMul));
            }
            else if (m_kind == Kind.Potion)
            {
                if (player.HP >= player.MaxHP) return;   // leave it on the ground
                player.Heal(m_amount > 0 ? m_amount : m_config.potionHeal);
            }
            else if (m_kind == Kind.Weapon && m_weapon != null)
            {
                if (player.Combat != null) player.Combat.Equip(m_weapon);
            }
            else if (m_kind == Kind.Equipment && m_equip != null)
            {
                player.AddEquipment(m_equip);
            }

            m_consumed = true;
            CBFx.SpawnBurst(m_body.Position, 5, m_sr != null ? m_sr.color : Color.white, 6f, 0.2f, 0.12f);
            if (CBGameClock.Instance != null) CBGameClock.Instance.Unregister(this);
            Destroy(gameObject);
        }

        public static CBPickup Spawn(CBConfig config, CBTileWorld world, Transform parent, Vector2 position, Kind kind, int amount, CBWeaponDef weapon, CBEquipDef equip = null)
        {
            var go = new GameObject("CB_Pickup");
            if (parent != null) go.transform.SetParent(parent, false);
            var pickup = go.AddComponent<CBPickup>();
            pickup.Init(config, world, position, kind, amount, weapon, equip);
            return pickup;
        }
    }
}
