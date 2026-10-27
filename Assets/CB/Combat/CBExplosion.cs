using UnityEngine;

namespace CB
{
    /// <summary>Radial damage + knockback. Used by Bomb Bow, Explosion Sword and the dive landing.</summary>
    public static class CBExplosion
    {
        // Own buffer: Detonate() is often called from inside a loop that is already
        // iterating CBActorRegistry.Scratch (sword hits, arrow impacts).
        static readonly System.Collections.Generic.List<CBActor> s_hits =
            new System.Collections.Generic.List<CBActor>();

        public static void Detonate(
            Vector2 centre,
            float radius,
            int damage,
            float knockback,
            CBActor.TeamKind targetTeam,
            CBActor source,
            CBWeaponDef weapon,
            bool addLift)
        {
            var hits = s_hits;
            int count = CBActorRegistry.QueryCircle(centre, radius, targetTeam, hits);

            for (int i = 0; i < count; i++)
            {
                var target = hits[i];
                if (target == null || target.Dead || target.Body == null) continue;

                Vector2 delta = target.Body.Position - centre;
                float dist = delta.magnitude;
                Vector2 dir = dist > 0.001f ? delta / dist : new Vector2(0f, 1f);

                float falloff = 1f - Mathf.Clamp01(dist / radius);
                int amount = Mathf.Max(1, Mathf.RoundToInt(damage * falloff));

                var info = new CBDamageInfo
                {
                    Amount = amount,
                    Knockback = dir * knockback * falloff + (addLift ? new Vector2(0f, knockback * 0.25f) : Vector2.zero),
                    Source = source,
                    Weapon = weapon,
                    IsExplosion = true
                };

                target.TakeDamage(info);
            }
            hits.Clear();

            CBFx.SpawnExplosion(centre, radius);
            if (CBCamera2D.Instance != null) CBCamera2D.Instance.Shake(0.35f);
        }
    }
}
