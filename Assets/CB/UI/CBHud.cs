using UnityEngine;

namespace CB
{
    /// <summary>
    /// Placeholder HUD drawn with IMGUI (OnGUI). Zero asset dependencies - no font or
    /// TextMeshPro import needed, so it cannot break the build. Art direction will replace
    /// this with a proper Canvas later.
    /// </summary>
    public class CBHud : MonoBehaviour
    {
        CBConfig m_config;
        bool m_ready;

        public void Init(CBConfig config)
        {
            m_config = config;
            m_ready = true;
        }

        void OnGUI()
        {
            if (!m_ready) return;

            var cbGame = CBGame.Instance;
            var player = cbGame != null ? cbGame.Player : null;

            GUI.color = Color.white;
            var labelStyle = new GUIStyle(GUI.skin.label);
            labelStyle.fontSize = 15;
            labelStyle.normal.textColor = Color.white;

            // ---- health bar ----
            float hp01 = (player != null && player.MaxHP > 0) ? (float)player.HP / player.MaxHP : 0f;
            DrawBar(new Rect(18, 16, 260, 20), hp01, new Color(0.85f, 0.18f, 0.22f), new Color(0.16f, 0.05f, 0.07f));
            GUI.Label(new Rect(20, 16, 260, 20),
                player != null ? string.Format("HP  {0} / {1}", player.HP, player.MaxHP) : "HP", labelStyle);

            // ---- bow cooldown ----
            if (player != null && player.Combat != null && player.Combat.Bow != null)
            {
                float ready = 1f - player.Combat.BowCooldown01;
                DrawBar(new Rect(18, 42, 160, 10), ready, new Color(0.95f, 0.78f, 0.30f), new Color(0.18f, 0.15f, 0.06f));
            }

            // ---- stats ----
            string stats = string.Format("Depth {0}    Coins {1}    Kills {2}    Seed {3}",
                cbGame != null ? cbGame.LevelDepth : 0,
                player != null ? player.Coins : 0,
                player != null ? player.Kills : 0,
                cbGame != null ? cbGame.CurrentSeed : 0);
            GUI.Label(new Rect(18, 60, 520, 22), stats, labelStyle);

            // ---- weapons ----
            if (player != null && player.Combat != null)
            {
                string weapons = string.Format("Sword: {0}    Bow: {1}",
                    player.Combat.Sword != null ? player.Combat.Sword.DisplayName : "-",
                    player.Combat.Bow != null ? player.Combat.Bow.Describe() : "-");
                GUI.Label(new Rect(18, 84, 640, 22), weapons, labelStyle);
            }

            // ---- interact prompt ----
            if (cbGame != null && !string.IsNullOrEmpty(cbGame.CurrentPrompt))
            {
                var promptStyle = new GUIStyle(GUI.skin.label);
                promptStyle.fontSize = 17;
                promptStyle.normal.textColor = new Color(1f, 0.92f, 0.55f);
                promptStyle.alignment = TextAnchor.MiddleCenter;
                GUI.Label(new Rect(Screen.width * 0.5f - 220, Screen.height - 120, 440, 30), cbGame.CurrentPrompt, promptStyle);
            }

            // ---- controls ----
            GUI.Label(new Rect(18, Screen.height - 118, 640, 96),
                "A/D move   Space jump (x2)   J melee (air + S = dive)   K bow   E interact\n" +
                "R restart run   N regenerate level", labelStyle);

            // ---- death overlay ----
            if (player != null && player.Dead)
            {
                var big = new GUIStyle(GUI.skin.label);
                big.fontSize = 46;
                big.normal.textColor = new Color(1f, 0.35f, 0.35f);
                big.alignment = TextAnchor.MiddleCenter;
                GUI.Label(new Rect(Screen.width * 0.5f - 300, Screen.height * 0.5f - 60, 600, 90), "YOU DIED", big);

                var sub = new GUIStyle(GUI.skin.label);
                sub.fontSize = 20;
                sub.normal.textColor = Color.white;
                sub.alignment = TextAnchor.MiddleCenter;
                GUI.Label(new Rect(Screen.width * 0.5f - 300, Screen.height * 0.5f + 30, 600, 40), "Press R to restart", sub);
            }

            // ---- debug ----
            if (m_config != null && m_config.showDebugOverlay && CBGameClock.Instance != null)
            {
                var dbg = new GUIStyle(GUI.skin.label);
                dbg.fontSize = 12;
                dbg.normal.textColor = new Color(0.7f, 0.9f, 0.7f);
                GUI.Label(new Rect(Screen.width - 250, 16, 240, 70),
                    string.Format("tick {0}  sim {1:F1}s\nactors {2}\nfrozen {3}",
                        CBGameClock.Instance.Tick,
                        CBGameClock.Instance.SimTime,
                        CBActorRegistry.Count,
                        CBGameClock.Instance.Frozen), dbg);
            }

            GUI.color = Color.white;
        }

        static void DrawBar(Rect rect, float fill, Color colour, Color background)
        {
            var old = GUI.color;
            GUI.color = background;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = colour;
            GUI.DrawTexture(new Rect(rect.x + 2f, rect.y + 2f, (rect.width - 4f) * Mathf.Clamp01(fill), rect.height - 4f), Texture2D.whiteTexture);
            GUI.color = old;
        }
    }
}
