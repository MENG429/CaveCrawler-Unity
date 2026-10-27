using UnityEngine;

namespace CB
{
    public enum CBWeaponKind { Melee, Bow }

    /// <summary>
    /// Data-driven weapon definition. Every Caveblazers weapon is pure data here -
    /// adding a new one means adding a CreateInstance block in CBWeaponLibrary (or an
    /// .asset file), never touching gameplay code.
    /// </summary>
    public abstract class CBWeaponDef : ScriptableObject
    {
        [Header("Identity")]
        public string Id = "weapon";
        public string DisplayName = "Weapon";
        public CBWeaponKind Kind = CBWeaponKind.Melee;

        [Header("Damage")]
        public int DamageMin = 1;
        public int DamageMax = 2;

        [Header("Rate")]
        public CBFireRate FireRate = CBFireRate.Medium;
        public float FireIntervalOverride = -1f;   // <=0 means "use the tier table"

        [Header("Feel")]
        public float Knockback = 6f;
        public Color Tint = Color.white;

        public float FireInterval
        {
            get { return FireIntervalOverride > 0f ? FireIntervalOverride : CBFireRates.Get(FireRate); }
        }

        public int RollDamage(CBUtil.Rng rng)
        {
            if (rng == null) return DamageMin;
            return rng.Int(Mathf.Min(DamageMin, DamageMax), Mathf.Max(DamageMin, DamageMax));
        }

        public abstract string Describe();
    }

    /// <summary>Melee weapons. Caveblazers swords have no ammo and a 3-hit combo.</summary>
    [CreateAssetMenu(menuName = "CB/Sword", fileName = "CBSword")]
    public class CBSwordDef : CBWeaponDef
    {
        [Header("Melee")]
        public bool EveryHitIsHeavy = false;     // Orc Battleaxe
        public bool ExplodesOnHit = false;       // Explosion Sword
        public float ExplosionRadius = 2.2f;
        public int ExplosionDamage = 10;

        [Header("Skull Stick style scaling")]
        public int KillsPerMaxHPBonus = 0;       // 0 = disabled; 2 == "+1 max HP per 2 kills"
        public int MaxHPPerBonus = 1;

        public CBSwordDef()
        {
            Kind = CBWeaponKind.Melee;
        }

        public override string Describe()
        {
            return string.Format("{0}  {1}-{2} dmg  {3}", DisplayName, DamageMin, DamageMax, FireRate);
        }
    }

    /// <summary>
    /// Bows. NOTE: Caveblazers bows have NO ammo - arrows are infinite and the weapon is
    /// balanced purely through fire interval and damage. Do not add an ammo counter.
    /// </summary>
    [CreateAssetMenu(menuName = "CB/Bow", fileName = "CBBow")]
    public class CBBowDef : CBWeaponDef
    {
        [Header("Projectile")]
        public float ArrowSpeed = 30f;
        public float ArrowGravity = 18f;
        public int ArrowsPerShot = 1;
        public float SpreadDegrees = 0f;
        public int PierceCount = 0;

        [Header("Procs")]
        [Range(0f, 1f)] public float PoisonChance = 0f;
        [Range(0f, 1f)] public float BurnChance = 0f;
        [Range(0f, 1f)] public float SlowChance = 0f;
        [Range(0f, 1f)] public float PierceChance = 0f;
        public bool ExplodesOnHit = false;
        public float ExplosionRadius = 2.2f;
        public int ExplosionDamage = 12;

        [Header("Charge")]
        public bool IsChargeWeapon = false;      // Powitzer - NOT implemented in v1
        public float ChargeTime = 0f;

        public CBBowDef()
        {
            Kind = CBWeaponKind.Bow;
        }

        public override string Describe()
        {
            return string.Format("{0}  {1}-{2} dmg  {3}", DisplayName, DamageMin, DamageMax, FireRate);
        }
    }
}
