using System.Collections.Generic;
using UnityEngine;

namespace CB
{
    /// <summary>
    /// Flat list of live actors. Hit queries reuse a scratch buffer so the hot path
    /// allocates nothing.
    /// </summary>
    public static class CBActorRegistry
    {
        static readonly List<CBActor> s_all = new List<CBActor>();
        public static readonly List<CBActor> Scratch = new List<CBActor>();

        public static void Add(CBActor actor)
        {
            if (!s_all.Contains(actor)) s_all.Add(actor);
        }

        public static void Remove(CBActor actor)
        {
            s_all.Remove(actor);
        }

        public static void Clear()
        {
            s_all.Clear();
        }

        public static int Count { get { return s_all.Count; } }

        public static int QueryBox(Rect box, CBActor.TeamKind team, List<CBActor> results)
        {
            results.Clear();
            for (int i = 0; i < s_all.Count; i++)
            {
                var a = s_all[i];
                if (a == null || a.Dead) continue;
                if (a.Team != team) continue;
                if (a.Body == null) continue;
                if (CBUtil.BoxOverlaps(box, a.Body.Box)) results.Add(a);
            }
            return results.Count;
        }

        public static int QueryCircle(Vector2 centre, float radius, CBActor.TeamKind team, List<CBActor> results)
        {
            results.Clear();
            for (int i = 0; i < s_all.Count; i++)
            {
                var a = s_all[i];
                if (a == null || a.Dead) continue;
                if (a.Team != team) continue;
                if (a.Body == null) continue;
                if (CBUtil.CircleOverlapsBox(centre, radius, a.Body.Box)) results.Add(a);
            }
            return results.Count;
        }

        public static CBActor FindNearest(Vector2 point, CBActor.TeamKind team, float maxRange)
        {
            CBActor best = null;
            float bestDist = maxRange * maxRange;
            for (int i = 0; i < s_all.Count; i++)
            {
                var a = s_all[i];
                if (a == null || a.Dead || a.Team != team || a.Body == null) continue;
                float d = (a.Body.Position - point).sqrMagnitude;
                if (d < bestDist) { bestDist = d; best = a; }
            }
            return best;
        }
    }
}
