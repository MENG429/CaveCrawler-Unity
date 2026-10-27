using System;
using System.Collections.Generic;
using UnityEngine;

namespace CB
{
    public enum CBEquipKind { Ring, Artifact, Statue }

    /// <summary>
    /// Equippable passive item (ring / artifact / statue). Pure data, built in memory at boot
    /// so no .asset file is required. Add a new one in CBEquipLibrary.Build().
    /// </summary>
    public class CBEquipDef
    {
        public string Id = "equip";
        public string DisplayName = "Equip";
        public CBEquipKind Kind = CBEquipKind.Ring;
        public Color Tint = Color.white;
        public CBModifier Modifier = new CBModifier();

        public string Describe()
        {
            return string.Format("{0}  [{1}]", DisplayName, Kind);
        }
    }

    /// <summary>
    /// Registry of every ring / artifact / statue in the replica, transcribed from the
    /// Caveblazers item design (names + effect families). Built once at boot.
    /// </summary>
    public static class CBEquipLibrary
    {
        static bool s_built;

        public static readonly List<CBEquipDef> Rings = new List<CBEquipDef>();
        public static readonly List<CBEquipDef> Artifacts = new List<CBEquipDef>();
        public static readonly List<CBEquipDef> Statues = new List<CBEquipDef>();

        public static void Build()
        {
            if (s_built) return;
            s_built = true;

            // ---------------- Rings ----------------
            Make(CBEquipKind.Ring, "ring_power", "Ring of Power", new Color(0.95f, 0.80f, 0.30f),
                m => { m.meleeDamageMul *= 1.25f; m.rangedDamageMul *= 1.25f; });
            Make(CBEquipKind.Ring, "ring_haste", "Ring of Haste", new Color(0.40f, 0.95f, 0.60f),
                m => { m.moveSpeedMul *= 1.18f; });
            Make(CBEquipKind.Ring, "ring_leaping", "Ring of Leaping", new Color(0.55f, 0.80f, 1.0f),
                m => { m.jumpBonus += 1; });
            Make(CBEquipKind.Ring, "ring_regeneration", "Ring of Regeneration", new Color(0.95f, 0.55f, 0.55f),
                m => { m.maxHP += 25; });
            Make(CBEquipKind.Ring, "ring_wealth", "Ring of Wealth", new Color(1.0f, 0.85f, 0.20f),
                m => { m.coinMul *= 1.5f; });
            Make(CBEquipKind.Ring, "ring_bulwark", "Ring of Bulwark", new Color(0.70f, 0.70f, 0.80f),
                m => { m.knockbackResist += 0.5f; });

            // ---------------- Artifacts ----------------
            Make(CBEquipKind.Artifact, "artifact_rage", "Artifact of Rage", new Color(0.95f, 0.30f, 0.30f),
                m => { m.meleeDamageMul *= 1.4f; m.rangedDamageMul *= 1.4f; m.moveSpeedMul *= 1.1f; });
            Make(CBEquipKind.Artifact, "artifact_moon", "Artifact of the Moon", new Color(0.65f, 0.75f, 1.0f),
                m => { m.fireRateMul *= 0.8f; m.extraArrows += 1; });
            Make(CBEquipKind.Artifact, "artifact_watcher", "Eye of the Watcher", new Color(0.55f, 1.0f, 0.85f),
                m => { m.lifestealOnKill += 3; });
            Make(CBEquipKind.Artifact, "artifact_lexicon", "Emerald Lexicon", new Color(0.45f, 0.95f, 0.55f),
                m => { m.explosionOnKill = true; });

            // ---------------- Statues ----------------
            Make(CBEquipKind.Statue, "statue_titan", "Statue of the Titan", new Color(0.80f, 0.78f, 0.70f),
                m => { m.maxHP += 50; m.moveSpeedMul *= 0.9f; });
            Make(CBEquipKind.Statue, "statue_storm", "Statue of the Storm", new Color(0.75f, 0.85f, 1.0f),
                m => { m.rangedDamageMul *= 1.6f; m.fireRateMul *= 0.9f; });
            Make(CBEquipKind.Statue, "statue_blood", "Statue of Blood", new Color(0.90f, 0.25f, 0.30f),
                m => { m.lifestealOnKill += 6; m.meleeDamageMul *= 1.2f; });
        }

        static CBEquipDef Make(CBEquipKind kind, string id, string name, Color tint, Action<CBModifier> configure)
        {
            var def = new CBEquipDef { Id = id, DisplayName = name, Kind = kind, Tint = tint };
            var mod = new CBModifier();
            configure(mod);
            def.Modifier = mod;

            if (kind == CBEquipKind.Ring) Rings.Add(def);
            else if (kind == CBEquipKind.Artifact) Artifacts.Add(def);
            else Statues.Add(def);
            return def;
        }

        public static CBEquipDef RandomRing(CBUtil.Rng rng)
        {
            return rng != null && Rings.Count > 0 ? Rings[rng.Pick(Rings.Count)] : null;
        }

        public static CBEquipDef RandomArtifact(CBUtil.Rng rng)
        {
            return rng != null && Artifacts.Count > 0 ? Artifacts[rng.Pick(Artifacts.Count)] : null;
        }

        public static CBEquipDef RandomStatue(CBUtil.Rng rng)
        {
            return rng != null && Statues.Count > 0 ? Statues[rng.Pick(Statues.Count)] : null;
        }

        /// <summary>Weighted roll: rings common, artifacts uncommon, statues rare.</summary>
        public static CBEquipDef Roll(CBUtil.Rng rng)
        {
            if (rng == null) return null;
            int roll = rng.Int(0, 99);
            if (roll < 60) return RandomRing(rng);
            if (roll < 90) return RandomArtifact(rng);
            return RandomStatue(rng);
        }
    }
}
