using UnityEngine;

namespace CB
{
    /// <summary>Item pedestal: shows one weapon, Interact swaps it into the matching slot.</summary>
    public class CBPedestal : MonoBehaviour, ICBInteractable
    {
        CBConfig m_config;
        CBWeaponDef m_weapon;
        SpriteRenderer m_base;
        SpriteRenderer m_icon;
        float m_spin;

        public float InteractRadius { get { return 2.0f; } }
        public string Prompt { get { return m_weapon != null ? ("Take " + m_weapon.DisplayName + "  (E)") : ""; } }
        public bool IsAvailable { get { return m_weapon != null; } }

        public void Init(CBConfig config, Vector2 position, CBWeaponDef weapon)
        {
            m_config = config;
            m_weapon = weapon;
            transform.position = new Vector3(position.x, position.y, 0f);

            m_base = CBAssets.MakeSprite(gameObject, CBAssets.Pedestal, new Vector2(1.0f, 0.7f), 7);

            var baseSprite = CBArt.FirstSprite("pickups", "pedestal");
            if (baseSprite != null)
            {
                m_base.sprite = baseSprite;
                m_base.drawMode = SpriteDrawMode.Simple;
                m_base.color = Color.white;
            }

            var iconObject = new GameObject("Icon");
            iconObject.transform.SetParent(transform, false);
            iconObject.transform.localPosition = new Vector3(0f, 0.95f, 0f);
            m_icon = CBAssets.MakeSprite(iconObject, weapon != null ? weapon.Tint : Color.white, new Vector2(0.7f, 0.35f), 11);

            var weaponSprite = weapon != null ? CBArt.WeaponClip(weapon) : null;
            if (weaponSprite != null && weaponSprite.Frames.Length > 0)
            {
                m_icon.sprite = weaponSprite.Frames[0];
                m_icon.drawMode = SpriteDrawMode.Simple;
                m_icon.color = Color.white;
            }

            gameObject.name = "CB_Pedestal";
            CBGame.RegisterInteractable(this);
        }

        void Update()
        {
            if (m_icon == null) return;
            m_spin += Time.deltaTime;
            m_icon.transform.localPosition = new Vector3(0f, 0.95f + Mathf.Sin(m_spin * 2f) * 0.08f, 0f);
        }

        void OnDestroy()
        {
            CBGame.UnregisterInteractable(this);
        }

        public void Interact(CBPlayerController player)
        {
            if (m_weapon == null || player == null || player.Combat == null) return;

            CBWeaponDef previous;
            var bow = m_weapon as CBBowDef;
            var sword = m_weapon as CBSwordDef;

            if (bow != null)
            {
                previous = player.Combat.Bow;
                player.Combat.Equip(m_weapon);
            }
            else if (sword != null)
            {
                previous = player.Combat.Sword;
                player.Combat.Equip(m_weapon);
            }
            else
            {
                return;
            }

            // Swap: the replaced weapon stays on the pedestal.
            m_weapon = previous;
            if (m_icon != null && previous != null)
            {
                var nextClip = CBArt.WeaponClip(previous);
                if (nextClip != null && nextClip.Frames.Length > 0)
                {
                    m_icon.sprite = nextClip.Frames[0];
                    m_icon.color = Color.white;
                }
                else
                {
                    m_icon.color = previous.Tint;
                }
            }

            CBFx.SpawnRing(transform.position, 1.4f, CBAssets.Portal, 0.3f);
        }
    }
}
