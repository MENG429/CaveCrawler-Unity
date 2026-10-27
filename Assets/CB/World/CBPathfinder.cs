using System.Collections.Generic;
using UnityEngine;

namespace CB
{
    /// <summary>
    /// Grid A* over empty tiles.
    /// - Walkers: 8-directional, but any upward move costs extra (it implies a jump), so the
    ///   search naturally prefers running along the ground / dropping down.
    /// - Flyers: uniform 8-directional cost.
    ///
    /// One instance per world. Scratch buffers are preallocated and reused (no per-query GC).
    /// </summary>
    public class CBPathfinder
    {
        public enum Mode { Walker, Flyer }

        readonly CBTileWorld m_world;
        readonly int m_w;
        readonly int m_h;
        readonly int m_count;

        readonly float[] m_g;
        readonly float[] m_f;
        readonly int[] m_parent;
        readonly int[] m_state;      // 0 = unseen, 1 = open, 2 = closed
        readonly int[] m_stamp;
        readonly int[] m_order;      // binary heap of node indices
        int m_stampId;
        int m_heapSize;
        int m_maxNodes;

        const int Unseen = 0;
        const int Open = 1;
        const int Closed = 2;

        // Walker cost model
        const float CostHorizontal = 1.0f;
        const float CostDrop = 1.25f;
        const float CostJump = 3.5f;

        public int LastNodesExpanded { get; private set; }

        public CBPathfinder(CBTileWorld world)
        {
            m_world = world;
            m_w = world.Width;
            m_h = world.Height;
            m_count = m_w * m_h;

            m_g = new float[m_count];
            m_f = new float[m_count];
            m_parent = new int[m_count];
            m_state = new int[m_count];
            m_stamp = new int[m_count];
            // Lazy-deletion heaps can hold a node more than once, so over-allocate.
            m_order = new int[m_count * 4 + 1];
            m_stampId = 0;
            m_maxNodes = 4000;
        }

        public void SetMaxNodes(int n) { m_maxNodes = n > 0 ? n : 1; }

        /// <summary>
        /// Finds a path between tile coordinates. Returns false when unreachable or when the
        /// node budget is exhausted. <paramref name="result"/> is reused, not allocated.
        /// </summary>
        public bool FindPath(int sx, int sy, int tx, int ty, Mode mode, List<Vector2Int> result)
        {
            result.Clear();
            LastNodesExpanded = 0;
            if (m_world == null) return false;
            if (!m_world.IsEmpty(tx, ty) || !m_world.IsEmpty(sx, sy)) return false;

            m_stampId++;
            int stamp = m_stampId;
            m_heapSize = 0;

            int start = sy * m_w + sx;
            int goal = ty * m_w + tx;

            m_stamp[start] = stamp;
            m_g[start] = 0f;
            m_f[start] = Heuristic(sx, sy, tx, ty, mode);
            m_parent[start] = -1;
            m_state[start] = Open;
            HeapPush(start);

            int expanded = 0;
            bool found = false;

            while (m_heapSize > 0)
            {
                int current = HeapPop();
                if (m_stamp[current] != stamp) continue;
                if (m_state[current] == Closed) continue;
                m_state[current] = Closed;

                if (current == goal) { found = true; break; }

                if (++expanded > m_maxNodes) break;
                LastNodesExpanded = expanded;

                int cx = current % m_w;
                int cy = current / m_w;

                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dy == 0) continue;

                        int nx = cx + dx;
                        int ny = cy + dy;
                        if (!m_world.IsInside(nx, ny)) continue;
                        if (!m_world.IsEmpty(nx, ny)) continue;

                        // No corner cutting through rock.
                        if (dx != 0 && dy != 0)
                        {
                            if (!m_world.IsEmpty(cx + dx, cy) || !m_world.IsEmpty(cx, cy + dy)) continue;
                        }

                        // Walkers cannot move diagonally straight up without ground logic -
                        // keep diagonals but price them as a jump when they go up.
                        int ni = ny * m_w + nx;
                        float stepCost = StepCost(dx, dy, mode);

                        if (m_stamp[ni] != stamp)
                        {
                            m_stamp[ni] = stamp;
                            m_g[ni] = float.MaxValue;
                            m_state[ni] = Unseen;
                            m_parent[ni] = -1;
                        }
                        if (m_state[ni] == Closed) continue;

                        float tentative = m_g[current] + stepCost;
                        if (tentative < m_g[ni])
                        {
                            m_g[ni] = tentative;
                            m_f[ni] = tentative + Heuristic(nx, ny, tx, ty, mode);
                            m_parent[ni] = current;
                            m_state[ni] = Open;
                            HeapPush(ni);
                        }
                    }
                }
            }

            if (!found) return false;

            // Reconstruct.
            int node = goal;
            int guard = 0;
            while (node != -1 && guard++ <= m_count)
            {
                result.Add(new Vector2Int(node % m_w, node / m_w));
                if (node == start) break;
                node = m_stamp[node] == stamp ? m_parent[node] : -1;
            }
            result.Reverse();
            return result.Count > 0;
        }

        float StepCost(int dx, int dy, Mode mode)
        {
            if (mode == Mode.Flyer)
            {
                return (dx != 0 && dy != 0) ? 1.4142f : 1.0f;
            }

            if (dy > 0) return (dx != 0) ? CostJump * 1.15f : CostJump;
            if (dy < 0) return (dx != 0) ? CostDrop * 1.15f : CostDrop;
            return CostHorizontal;
        }

        float Heuristic(int x, int y, int tx, int ty, Mode mode)
        {
            int dx = Mathf.Abs(x - tx);
            int dy = Mathf.Abs(y - ty);
            // Octile distance.
            return (dx > dy) ? (dx - dy) + 1.4142f * dy : (dy - dx) + 1.4142f * dx;
        }

        // ---- binary min-heap keyed on m_f ----
        void HeapPush(int node)
        {
            if (m_heapSize >= m_order.Length - 1) return;   // budget guard, never overflow
            m_heapSize++;
            int i = m_heapSize;
            m_order[i] = node;
            while (i > 1)
            {
                int p = i >> 1;
                if (m_f[m_order[p]] <= m_f[m_order[i]]) break;
                int tmp = m_order[p]; m_order[p] = m_order[i]; m_order[i] = tmp;
                i = p;
            }
        }

        int HeapPop()
        {
            int top = m_order[1];
            m_order[1] = m_order[m_heapSize];
            m_heapSize--;
            int i = 1;
            while (true)
            {
                int l = i << 1;
                int r = l + 1;
                int smallest = i;
                if (l <= m_heapSize && m_f[m_order[l]] < m_f[m_order[smallest]]) smallest = l;
                if (r <= m_heapSize && m_f[m_order[r]] < m_f[m_order[smallest]]) smallest = r;
                if (smallest == i) break;
                int tmp = m_order[i]; m_order[i] = m_order[smallest]; m_order[smallest] = tmp;
                i = smallest;
            }
            return top;
        }
    }
}
