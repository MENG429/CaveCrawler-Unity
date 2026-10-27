using System.Collections.Generic;
using UnityEngine;

namespace CB
{
    /// <summary>
    /// Registry of every weapon in the replica. Numbers are transcribed from the official
    /// Caveblazers wiki (damage range / speed tier) and must not be "rebalanced".
    ///
    /// Built in memory at boot so no .asset file is required; CBWeaponLibrary.Register()
    /// is public so authored assets can be added later without touching this file.
    /// </summary>
    public static class CBWeaponLibrary
    {
        static readonly List<CBWeaponDef> s_all = new List<CBWeaponDef>();
        static readonly List<CBSwordDef> s_swords = new List<CBSwordDef>();
        static readonly List<CBBowDef> s_bows = new List<CBBowDef>();
        static readonly Dictionary<string, CBWeaponDef> s_byId = new Dictionary<string, CBWeaponDef>();
        static bool s_built;

        public static IReadOnlyList<CBWeaponDef> All { get { return s_all; } }
        public static IReadOnlyList<CBSwordDef> Swords { get { return s_swords; } }
        public static IReadOnlyList<CBBowDef> Bows { get { return s_bows; } }

        public const string StartingSwordId = "sword_spiked_stick";
        public const string StartingBowId = "bow_recurve";

        public static void Build()
        {
            if (s_built) return;
            s_built = true;

            // ---------------- Melee ----------------
            Register(Sword("sword_spiked_stick", "Spiked Stick", 5, 7, CBFireRate.Medium, 6f));

            var battleaxe = Sword("sword_orc_battleaxe", "Orc Battleaxe", 10, 12, CBFireRate.Slow, 12f);
            battleaxe.EveryHitIsHeavy = true;
            Register(battleaxe);

            var skull = Sword("sword_skull_stick", "Skull Stick", 3, 5, CBFireRate.Medium, 6f);
            skull.KillsPerMaxHPBonus = 2;   // "+1 max HP per 2 kills"
            skull.MaxHPPerBonus = 1;
            Register(skull);

            var blast = Sword("sword_explosion", "Explosion Sword", 7, 9, CBFireRate.Medium, 10f);
            Register(blast);
            blast.ExplodesOnHit = true;
            blast.ExplosionRadius = 2.2f;
            blast.ExplosionDamage = 10;

            // ---------------- Bows ----------------
            Register(Bow("bow_recurve", "Recurve Bow", 4, 6, CBFireRate.Medium, 30f, 18f));

            var bomb = Bow("bow_bomb", "Bomb Bow", 8, 10, CBFireRate.Slow, 26f, 20f);
            Register(bomb);
            bomb.ExplodesOnHit = true;
            bomb.ExplosionRadius = 2.6f;
            bomb.ExplosionDamage = 14;

            var poison = Bow("bow_poison", "Poison Bow", 5, 7, CBFireRate.Medium, 30f, 18f);
            Register(poison);
            poison.PoisonChance = 0.15f;

            var piercing = Bow("bow_piercing", "Piercing Bow", 5, 7, CBFireRate.Medium, 34f, 14f);
            Register(piercing);
            piercing.PierceCount = 2;

            var fire = Bow("bow_fire", "Fire Bow", 5, 7, CBFireRate.Medium, 30f, 18f);
            Register(fire);
            fire.BurnChance = 0.15f;

            var ice = Bow("bow_ice", "Ice Bow", 4, 6, CBFireRate.Medium, 30f, 18f);
            Register(ice);
            ice.SlowChance = 0.25f;

            var hallowed = Bow("bow_hallowed", "Hallowed Bow", 6, 8, CBFireRate.Medium, 52f, 10f);
            Register(hallowed);
            hallowed.SpreadDegrees = 7f;   // "very fast arrows, inaccurate"

            Register(Bow("bow_metal", "Metal Bow", 3, 5, CBFireRate.Fast, 36f, 14f));

            var elven = Bow("bow_elven", "Elven Bow", 7, 9, CBFireRate.Medium, 32f, 16f);
            Register(elven);
            elven.PierceChance = 0.50f;
            elven.PierceCount = 3;

            var crossfire = Bow("bow_crossfire", "Crossfire Bow", 3, 5, CBFireRate.VerySlow, 28f, 18f);
            Register(crossfire);
            crossfire.ArrowsPerShot = 3;
            crossfire.SpreadDegrees = 9f;

            var bazwood = Bow("bow_bazwood", "Bazwood Bow", 1, 3, CBFireRate.VerySlow, 26f, 16f);
            Register(bazwood);
            bazwood.ArrowsPerShot = 5;
            bazwood.SpreadDegrees = 22f;

            Register(Bow("bow_ardent", "Ardent Bow", 10, 12, CBFireRate.Slow, 34f, 16f));
            Register(Bow("bow_hand_cannon", "Hand Cannon", 14, 16, CBFireRate.VerySlow, 30f, 20f));
            Register(Bow("bow_xandu_blaster", "Xandu Blaster", 3, 5, CBFireRate.Extreme, 44f, 8f));

            var powitzer = Bow("bow_powitzer", "Powitzer", 24, 26, CBFireRate.Charge, 40f, 14f);
            Register(powitzer);
            powitzer.IsChargeWeapon = true;
            powitzer.ChargeTime = 1.1f;
            // Charge mechanic is NOT implemented in v1 - it fires on the interval instead.
        }

        static CBSwordDef Sword(string id, string name, int dmin, int dmax, CBFireRate rate, float knockback)
        {
            var def = ScriptableObject.CreateInstance<CBSwordDef>();
            def.Id = id;
            def.DisplayName = name;
            def.Kind = CBWeaponKind.Melee;
            def.DamageMin = dmin;
            def.DamageMax = dmax;
            def.FireRate = rate;
            def.Knockback = knockback;
            def.Tint = new Color(0.85f, 0.88f, 0.95f);
            def.hideFlags = HideFlags.HideAndDontSave;
            return def;
        }

        static CBBowDef Bow(string id, string name, int dmin, int dmax, CBFireRate rate, float arrowSpeed, float arrowGravity)
        {
            var def = ScriptableObject.CreateInstance<CBBowDef>();
            def.Id = id;
            def.DisplayName = name;
            def.Kind = CBWeaponKind.Bow;
            def.DamageMin = dmin;
            def.DamageMax = dmax;
            def.FireRate = rate;
            def.ArrowSpeed = arrowSpeed;
            def.ArrowGravity = arrowGravity;
            def.ArrowsPerShot = 1;
            def.Knockback = 5f;
            def.Tint = new Color(0.90f, 0.86f, 0.70f);
            def.hideFlags = HideFlags.HideAndDontSave;
            return def;
        }

        public static CBWeaponDef Register(CBWeaponDef def)
        {
            if (def == null) return null;
            s_all.Add(def);
            s_byId[def.Id] = def;

            var sword = def as CBSwordDef;
            if (sword != null && !s_swords.Contains(sword)) s_swords.Add(sword);

            var bow = def as CBBowDef;
            if (bow != null && !s_bows.Contains(bow)) s_bows.Add(bow);

            return def;
        }

        public static CBWeaponDef Get(string id)
        {
            CBWeaponDef def;
            return s_byId.TryGetValue(id, out def) ? def : null;
        }

        public static CBBowDef RandomBow(CBUtil.Rng rng)
        {
            if (s_bows.Count == 0) return null;
            return s_bows[rng.Pick(s_bows.Count)];
        }

        public static CBSwordDef RandomSword(CBUtil.Rng rng)
        {
            if (s_swords.Count == 0) return null;
            return s_swords[rng.Pick(s_swords.Count)];
        }
    }
}
