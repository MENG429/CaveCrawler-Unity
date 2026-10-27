using UnityEngine;

namespace CB
{
    /// <summary>
    /// A one-shot shrine. Interacting grants a random blessing the player does not already own,
    /// then the shrine is spent. Visual is a placeholder glow block (no dedicated art sheet yet).
    /// </summary>
    public class CBBlessingShrine : MonoBehaviour, ICBInteractable
    {
        CBConfig m_config;
        bool m_used;
        SpriteRenderer m_sr;

        public float InteractRadius { get { return 1.9f; } }
        public string Prompt { get { return m_used ? "" : "Pray for a blessing  (E)"; } }
        public bool IsAvailable { get { return !m_used; } }

        public void Init(CBConfig config, Vector2 position)
        {
            m_config = config;
            transform.position = new Vector3(position.x, position.y, 0f);

            m_sr = CBAssets.MakeSprite(gameObject, new Color(0.75f, 0.55f, 1.0f), new Vector2(0.7f, 1.0f), 9);
            gameObject.name = "CB_BlessingShrine";
            CBGame.RegisterInteractable(this);
        }

        void OnDestroy()
        {
            CBGame.UnregisterInteractable(this);
        }

        public void Interact(CBPlayerController player)
        {
            if (m_used || player == null) return;
            m_used = true;

            var rng = CBGame.Instance != null ? CBGame.Instance.Rng : null;
            if (rng == null) return;

            int granted = 0;
            for (int i = 0; i < m_config.blessingsPerShrine; i++)
            {
                var blessing = CBBlessingLibrary.Random(rng);
                if (blessing == null) break;
                if (player.AddBlessing(blessing)) granted++;
            }

            if (m_sr != null) m_sr.color = new Color(0.35f, 0.30f, 0.45f);
            CBFx.SpawnRing(transform.position, 2.0f, new Color(0.75f, 0.55f, 1.0f), 0.45f);
        }
    }
}
