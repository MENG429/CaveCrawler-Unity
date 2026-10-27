using UnityEngine;

namespace CB
{
    /// <summary>
    /// Additive / multiplicative stat deltas granted by blessings and equipment.
    /// Multiplicative fields default to 1 (identity); additive fields default to 0.
    /// Aggregated by <see cref="CBLoadout"/> into a single modifier the gameplay code reads.
    /// </summary>
    public class CBModifier
    {
        public int maxHP;
        public float moveSpeedMul = 1f;
        public int jumpBonus;
        public float meleeDamageMul = 1f;
        public float rangedDamageMul = 1f;
        public float fireRateMul = 1f;      // <1 = faster fire
        public int lifestealOnKill;
        public float knockbackResist;       // 0..1 fraction of incoming knockback ignored
        public float coinMul = 1f;
        public int extraArrows;
        public bool explosionOnKill;

        public void Add(CBModifier o)
        {
            if (o == null) return;
            maxHP += o.maxHP;
            jumpBonus += o.jumpBonus;
            lifestealOnKill += o.lifestealOnKill;
            extraArrows += o.extraArrows;
            moveSpeedMul *= o.moveSpeedMul;
            meleeDamageMul *= o.meleeDamageMul;
            rangedDamageMul *= o.rangedDamageMul;
            fireRateMul *= o.fireRateMul;
            coinMul *= o.coinMul;
            knockbackResist = Mathf.Clamp01(knockbackResist + o.knockbackResist);
            explosionOnKill = explosionOnKill || o.explosionOnKill;
        }
    }
}
