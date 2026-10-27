using UnityEngine;

namespace CB
{
    /// <summary>Exit portal at the end of the level. Interact to descend to the next cavern.</summary>
    public class CBPortal : MonoBehaviour, ICBInteractable
    {
        CBConfig m_config;
        SpriteRenderer m_sr;
        float m_spin;

        public float InteractRadius { get { return 2.2f; } }
        public string Prompt { get { return "Descend  (E)"; } }
        public bool IsAvailable { get { return true; } }

        public void Init(CBConfig config, Vector2 position)
        {
            m_config = config;
            transform.position = new Vector3(position.x, position.y, 0f);
            m_sr = CBAssets.MakeSprite(gameObject, CBAssets.Portal, new Vector2(1.4f, 2.0f), 6);
            gameObject.name = "CB_Portal";
            CBGame.RegisterInteractable(this);
        }

        void Update()
        {
            if (m_sr == null) return;
            m_spin += Time.deltaTime;
            float pulse = 1f + Mathf.Sin(m_spin * 3f) * 0.08f;
            m_sr.size = new Vector2(1.4f * pulse, 2.0f / pulse);
            var c = CBAssets.Portal;
            c.a = 0.75f + Mathf.Sin(m_spin * 5f) * 0.2f;
            m_sr.color = c;
        }

        void OnDestroy()
        {
            CBGame.UnregisterInteractable(this);
        }

        public void Interact(CBPlayerController player)
        {
            var cbGame = CBGame.Instance;
            if (cbGame != null) cbGame.NextLevel();
        }
    }
}
