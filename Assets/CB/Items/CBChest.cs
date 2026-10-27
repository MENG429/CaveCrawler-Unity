using UnityEngine;

namespace CB
{
    /// <summary>Treasure chest: opens with Interact, drops coins and often a potion or weapon.</summary>
    public class CBChest : MonoBehaviour, ICBInteractable
    {
        CBConfig m_config;
        SpriteRenderer m_sr;
        bool m_opened;
        bool m_art;

        public float InteractRadius { get { return 1.8f; } }
        public string Prompt { get { return m_opened ? "" : "Open chest  (E)"; } }
        public bool IsAvailable { get { return !m_opened; } }

        public void Init(CBConfig config, Vector2 position)
        {
            m_config = config;
            transform.position = new Vector3(position.x, position.y, 0f);

            m_sr = CBAssets.MakeSprite(gameObject, CBAssets.Chest, new Vector2(1.1f, 0.75f), 8);

            // Art chest replaces the placeholder body + lid stack entirely.
            var closed = CBArt.FirstSprite("pickups", "chest_closed");
            if (closed != null)
            {
                m_art = true;
                m_sr.sprite = closed;
                m_sr.drawMode = SpriteDrawMode.Simple;
                m_sr.color = Color.white;
            }
            else
            {
                // Two stacked boxes read as a chest without any art asset.
                var lid = new GameObject("Lid");
                lid.transform.SetParent(transform, false);
                lid.transform.localPosition = new Vector3(0f, 0.5f, 0f);
                CBAssets.MakeSprite(lid, CBAssets.RockAccent, new Vector2(1.15f, 0.28f), 9);
            }

            gameObject.name = "CB_Chest";
            CBGame.RegisterInteractable(this);
        }

        void OnDestroy()
        {
            CBGame.UnregisterInteractable(this);
        }

        public void Interact(CBPlayerController player)
        {
            if (m_opened) return;
            m_opened = true;

            var cbGame = CBGame.Instance;
            var rng = cbGame != null ? cbGame.Rng : null;
            if (rng == null) return;

            Vector2 at = transform.position;

            int coinCount = rng.Int(5, 12);
            for (int i = 0; i < coinCount; i++)
            {
                Vector2 scatter = at + new Vector2(rng.Float(-0.6f, 0.6f), 0.7f);
                CBPickup.Spawn(m_config, cbGame.World, cbGame.PickupRoot, scatter, CBPickup.Kind.Coin, 1, null);
            }

            if (rng.Chance(0.45f))
            {
                CBPickup.Spawn(m_config, cbGame.World, cbGame.PickupRoot, at + new Vector2(0.6f, 0.5f),
                    CBPickup.Kind.Potion, m_config.potionHeal, null);
            }

            if (rng.Chance(0.55f))
            {
                bool wantBow = rng.Chance(0.5f);
                CBWeaponDef def = wantBow ? (CBWeaponDef)CBWeaponLibrary.RandomBow(rng) : CBWeaponLibrary.RandomSword(rng);
                if (def != null)
                {
                    CBPickup.Spawn(m_config, cbGame.World, cbGame.PickupRoot, at + new Vector2(-0.7f, 0.6f),
                        CBPickup.Kind.Weapon, 0, def);
                }
            }

            CBFx.SpawnRing(at, 1.6f, CBAssets.Coin, 0.35f);
            CBFx.SpawnBurst(at, 12, CBAssets.Coin, 8f, 0.45f, 0.16f);

            var opened = CBArt.FirstSprite("pickups", "chest_open");
            if (opened != null)
            {
                m_sr.sprite = opened;
                m_sr.color = Color.white;
            }
            else if (!m_art && m_sr != null)
            {
                m_sr.color = CBAssets.RockAccent;
            }
        }
    }
}
