using System;
using System.Collections.Generic;
using UnityEngine;

namespace CB
{
    /// <summary>
    /// A blessing: a one-time passive boon granted by a shrine. Data-driven so adding one
    /// is a single line in CBBlessingLibrary.Build(). Stacking is allowed unless noted.
    /// </summary>
    public class CBBlessingDef
    {
        public string Id = "blessing";
        public string DisplayName = "Blessing";
        public string Flavor = "";
        public Color Tint = Color.white;
        public CBModifier Modifier = new CBModifier();

        public string Describe()
        {
            return DisplayName;
        }
    }

    /// <summary>
    /// Registry of blessings. Effect families mirror the Caveblazers blessing design
    /// (vitality / swiftness / might / fortune / etc.).
    /// </summary>
    public static class CBBlessingLibrary
    {
        static bool s_built;
        public static readonly List<CBBlessingDef> All = new List<CBBlessingDef>();

        public static void Build()
        {
            if (s_built) return;
            s_built = true;

            Make("bless_vitality", "Blessing of Vitality", "Health flows stronger.", new Color(0.95f, 0.45f, 0.45f),
                m => { m.maxHP += 30; });
            Make("bless_swiftness", "Blessing of Swiftness", "Your feet are feathers.", new Color(0.45f, 0.95f, 0.60f),
                m => { m.moveSpeedMul *= 1.2f; });
            Make("bless_might", "Blessing of Might", "Your blows bite deeper.", new Color(0.95f, 0.35f, 0.30f),
                m => { m.meleeDamageMul *= 1.3f; });
            Make("bless_arrows", "Blessing of Arrows", "The bow sings true.", new Color(0.55f, 0.80f, 1.0f),
                m => { m.rangedDamageMul *= 1.3f; });
            Make("bless_haste", "Blessing of Haste", "Strikes come quicker.", new Color(0.95f, 0.85f, 0.40f),
                m => { m.fireRateMul *= 0.85f; });
            Make("bless_leap", "Blessing of Leaping", "The earth releases you.", new Color(0.60f, 0.85f, 1.0f),
                m => { m.jumpBonus += 1; });
            Make("bless_fortune", "Blessing of Fortune", "Gold finds its way to you.", new Color(1.0f, 0.85f, 0.25f),
                m => { m.coinMul *= 1.75f; });
            Make("bless_blood", "Blessing of Blood", "Each kill sustains you.", new Color(0.90f, 0.25f, 0.35f),
                m => { m.lifestealOnKill += 4; });
            Make("bless_bulwark", "Blessing of Bulwark", "Blows glance off you.", new Color(0.75f, 0.75f, 0.85f),
                m => { m.knockbackResist += 0.4f; });
            Make("bless_storm", "Blessing of Storms", "Arrows split the sky.", new Color(0.70f, 0.90f, 1.0f),
                m => { m.extraArrows += 1; });
            Make("bless_ruin", "Blessing of Ruin", "Slain foes erupt.", new Color(0.95f, 0.55f, 0.20f),
                m => { m.explosionOnKill = true; });
            Make("bless_giant", "Blessing of Giants", "Great vitality fills you.", new Color(0.85f, 0.80f, 0.65f),
                m => { m.maxHP += 60; m.moveSpeedMul *= 0.95f; });
        }

        static CBBlessingDef Make(string id, string name, string flavor, Color tint, Action<CBModifier> configure)
        {
            var def = new CBBlessingDef { Id = id, DisplayName = name, Flavor = flavor, Tint = tint };
            var mod = new CBModifier();
            configure(mod);
            def.Modifier = mod;
            All.Add(def);
            return def;
        }

        public static CBBlessingDef Random(CBUtil.Rng rng)
        {
            return rng != null && All.Count > 0 ? All[rng.Pick(All.Count)] : null;
        }
    }
}
