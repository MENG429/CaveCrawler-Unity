using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace CB
{
    /// <summary>
    /// Placeholder HUD drawn with IMGUI (OnGUI). Zero asset dependencies - no font or
    /// TextMeshPro import needed, so it cannot break the build. Art direction will replace
    /// this with a proper Canvas later.
    ///
    /// Shows: health, bow + throw cooldown, run stats, the held weapons, the full weapon
    /// backlog (sword / bow) with the held one marked, equipment (rings / artifacts / statue),
    /// blessings, the wall-cling hint, the interact prompt, and the control legend.
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
            var combat = player != null ? player.Combat : null;
            var loadout = player != null ? player.Loadout : null;

            GUI.color = Color.white;
            var labelStyle = new GUIStyle(GUI.skin.label);
            labelStyle.fontSize = 15;
            labelStyle.normal.textColor = Color.white;

            // ---- health bar ----
            float hp01 = (player != null && player.MaxHP > 0) ? (float)player.HP / player.MaxHP : 0f;
            DrawBar(new Rect(18, 16, 260, 20), hp01, new Color(0.85f, 0.18f, 0.22f), new Color(0.16f, 0.05f, 0.07f));
            GUI.Label(new Rect(20, 16, 260, 20),
                player != null ? string.Format("HP  {0} / {1}", player.HP, player.MaxHP) : "HP", labelStyle);

            // ---- bow + throw cooldown bars ----
            if (combat != null && combat.Bow != null)
            {
                float ready = 1f - combat.BowCooldown01;
                DrawBar(new Rect(18, 42, 160, 10), ready, new Color(0.95f, 0.78f, 0.30f), new Color(0.18f, 0.15f, 0.06f));
            }
            if (combat != null)
            {
                float ready = 1f - combat.ThrowCooldown01;
                DrawBar(new Rect(190, 42, 160, 10), ready, new Color(0.55f, 0.85f, 1.0f), new Color(0.10f, 0.16f, 0.22f));
                GUI.Label(new Rect(190, 54, 160, 16), "throw (L)", labelStyle);
            }

            // ---- stats ----
            string stats = string.Format("Depth {0}    Coins {1}    Kills {2}    Seed {3}",
                cbGame != null ? cbGame.LevelDepth : 0,
                player != null ? player.Coins : 0,
                player != null ? player.Kills : 0,
                cbGame != null ? cbGame.CurrentSeed : 0);
            GUI.Label(new Rect(18, 74, 520, 22), stats, labelStyle);

            // ---- held weapons ----
            if (combat != null)
            {
                string weapons = string.Format("Sword [{0}]: {1}    Bow [{2}]: {3}",
                    combat.HeldMeleeIndex,
                    combat.Sword != null ? combat.Sword.DisplayName : "-",
                    combat.HeldBowIndex,
                    combat.Bow != null ? combat.Bow.Describe() : "-");
                GUI.Label(new Rect(18, 98, 820, 22), weapons, labelStyle);

                // ---- weapon backlog (sword / bow) ----
                DrawInventoryRow(new Rect(18, 122, 820, 22), "Q", combat.MeleeInventory, combat.HeldMeleeIndex, labelStyle);
                DrawInventoryRow(new Rect(18, 146, 820, 22), "G", combat.BowInventory, combat.HeldBowIndex, labelStyle);
            }

            // ---- equipment + blessings ----
            if (loadout != null)
            {
                string equip = BuildEquipmentLine(loadout);
                if (!string.IsNullOrEmpty(equip))
                    GUI.Label(new Rect(18, 172, 1000, 22), equip, labelStyle);

                string bless = BuildBlessingLine(loadout);
                if (!string.IsNullOrEmpty(bless))
                    GUI.Label(new Rect(18, 194, 1000, 22), bless, labelStyle);
            }

            // ---- wall-cling hint (transient) ----
            if (player != null && player.OnWall)
            {
                var wallStyle = new GUIStyle(GUI.skin.label);
                wallStyle.fontSize = 16;
                wallStyle.normal.textColor = new Color(0.65f, 0.9f, 1.0f);
                GUI.Label(new Rect(18, Screen.height - 150, 360, 22), "WALL-CLING  (Space to wall-jump)", wallStyle);
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
            GUI.Label(new Rect(18, Screen.height - 118, 760, 96),
                "A/D move   W/S aim up-down   Space jump (x2, wall-jump)\n" +
                "J melee (air + S = dive)   K bow (hold)   L throw sword   E interact\n" +
                "Q switch sword   G switch bow   R restart   N regenerate", labelStyle);

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

        /// <summary>Renders a compact inventory row: "[Q] >1. Short Sword <  2. Dagger ..." with the held entry marked.</summary>
        void DrawInventoryRow(Rect rect, string key, IReadOnlyList<CBSwordDef> items, int held, GUIStyle style)
        {
            if (items == null || items.Count == 0) return;
            var sb = new StringBuilder();
            sb.Append("[").Append(key).Append("] ");
            for (int i = 0; i < items.Count; i++)
            {
                var d = items[i];
                if (d == null) continue;
                if (i == held) sb.Append("> ");
                sb.Append(i + 1).Append(". ").Append(d.DisplayName);
                if (i == held) sb.Append(" <");
                sb.Append("    ");
            }
            GUI.Label(rect, sb.ToString(), style);
        }

        void DrawInventoryRow(Rect rect, string key, IReadOnlyList<CBBowDef> items, int held, GUIStyle style)
        {
            if (items == null || items.Count == 0) return;
            var sb = new StringBuilder();
            sb.Append("[").Append(key).Append("] ");
            for (int i = 0; i < items.Count; i++)
            {
                var d = items[i];
                if (d == null) continue;
                if (i == held) sb.Append("> ");
                sb.Append(i + 1).Append(". ").Append(d.DisplayName);
                if (i == held) sb.Append(" <");
                sb.Append("    ");
            }
            GUI.Label(rect, sb.ToString(), style);
        }

        string BuildEquipmentLine(CBLoadout loadout)
        {
            var parts = new List<string>();
            if (loadout.rings != null)
                for (int i = 0; i < loadout.rings.Length; i++)
                    if (loadout.rings[i] != null) parts.Add("Ring: " + loadout.rings[i].DisplayName);
            for (int i = 0; i < loadout.artifacts.Count; i++)
                if (loadout.artifacts[i] != null) parts.Add("Artifact: " + loadout.artifacts[i].DisplayName);
            if (loadout.statue != null) parts.Add("Statue: " + loadout.statue.DisplayName);
            return parts.Count > 0 ? "Equip  " + string.Join("    ", parts) : "";
        }

        string BuildBlessingLine(CBLoadout loadout)
        {
            if (loadout.blessings == null || loadout.blessings.Count == 0) return "";
            var names = new List<string>();
            for (int i = 0; i < loadout.blessings.Count; i++)
                if (loadout.blessings[i] != null) names.Add(loadout.blessings[i].DisplayName);
            return "Blessings  " + string.Join("    ", names);
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
