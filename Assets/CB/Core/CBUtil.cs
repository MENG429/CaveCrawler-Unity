using System;
using System.Collections.Generic;
using UnityEngine;

namespace CB
{
    public static class CBUtil
    {
        /// <summary>Deterministic RNG wrapper. Never use UnityEngine.Random for gameplay.</summary>
        public class Rng
        {
            System.Random m_rng;

            public Rng(int seed) { m_rng = new System.Random(seed); }

            public int SeedNext() { return m_rng.Next(); }
            public int Int(int minInclusive, int maxInclusive)
            {
                if (maxInclusive < minInclusive) return minInclusive;
                return m_rng.Next(minInclusive, maxInclusive + 1);
            }
            public float Float(float min, float max) { return min + (float)m_rng.NextDouble() * (max - min); }
            public bool Chance(float p) { return (float)m_rng.NextDouble() < p; }
            public float Signed() { return (float)m_rng.NextDouble() * 2f - 1f; }
            public int Pick(int count) { return count <= 0 ? 0 : m_rng.Next(count); }
            public T Pick<T>(IList<T> list) { return list.Count == 0 ? default(T) : list[m_rng.Next(list.Count)]; }
        }

        public static float Approach(float current, float target, float delta)
        {
            if (current < target) return Mathf.Min(current + delta, target);
            if (current > target) return Mathf.Max(current - delta, target);
            return target;
        }

        public static float SignOrZero(float v)
        {
            if (v > 0.0001f) return 1f;
            if (v < -0.0001f) return -1f;
            return 0f;
        }

        public static Rect BoxFromCenter(Vector2 center, Vector2 size)
        {
            return new Rect(center.x - size.x * 0.5f, center.y - size.y * 0.5f, size.x, size.y);
        }

        public static bool BoxOverlaps(Rect a, Rect b)
        {
            return a.xMax > b.xMin && a.xMin < b.xMax && a.yMax > b.yMin && a.yMin < b.yMax;
        }

        public static bool CircleOverlapsBox(Vector2 center, float radius, Rect box)
        {
            float cx = Mathf.Clamp(center.x, box.xMin, box.xMax);
            float cy = Mathf.Clamp(center.y, box.yMin, box.yMax);
            float dx = center.x - cx;
            float dy = center.y - cy;
            return dx * dx + dy * dy <= radius * radius;
        }

        /// <summary>Cheap deterministic hash used for per-tile visual variation.</summary>
        public static float Hash01(int a, int b)
        {
            uint h = (uint)(a * 73856093) ^ (uint)(b * 19349663);
            h ^= h >> 13;
            h *= 1274126177u;
            h ^= h >> 16;
            return (h & 0xFFFFFF) / (float)0xFFFFFF;
        }
    }
}
