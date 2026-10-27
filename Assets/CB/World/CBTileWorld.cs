using UnityEngine;

namespace CB
{
    /// <summary>
    /// Tile grid. One tile == one world unit. Tile (x,y) occupies world rect [x,x+1] x [y,y+1].
    /// Plain data - rendering lives in CBLevelView, collision queries live here.
    /// </summary>
    public class CBTileWorld
    {
        public const byte Empty = 0;
        public const byte Solid = 1;

        readonly byte[] m_cells;
        public int Width { get; private set; }
        public int Height { get; private set; }

        public CBTileWorld(int width, int height)
        {
            Width = width;
            Height = height;
            m_cells = new byte[width * height];
        }

        public int Index(int x, int y) { return y * Width + x; }

        public bool IsInside(int x, int y)
        {
            return x >= 0 && y >= 0 && x < Width && y < Height;
        }

        public byte Get(int x, int y)
        {
            if (!IsInside(x, y)) return Solid; // outside the cave is rock
            return m_cells[Index(x, y)];
        }

        public void Set(int x, int y, byte value)
        {
            if (!IsInside(x, y)) return;
            m_cells[Index(x, y)] = value;
        }

        public bool IsSolid(int x, int y) { return Get(x, y) == Solid; }
        public bool IsEmpty(int x, int y) { return Get(x, y) == Empty; }

        public void Fill(byte value)
        {
            for (int i = 0; i < m_cells.Length; i++) m_cells[i] = value;
        }

        /// <summary>Carve an inclusive rectangle to Empty.</summary>
        public void CarveRect(int x0, int y0, int x1, int y1)
        {
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                    Set(x, y, Empty);
        }

        /// <summary>Fill an inclusive rectangle to Solid.</summary>
        public void FillRect(int x0, int y0, int x1, int y1)
        {
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                    Set(x, y, Solid);
        }

        public bool IsSolidAtWorld(Vector2 p)
        {
            return IsSolid(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.y));
        }

        public Vector2Int WorldToTile(Vector2 p)
        {
            return new Vector2Int(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.y));
        }

        public Vector2 TileCenter(int x, int y)
        {
            return new Vector2(x + 0.5f, y + 0.5f);
        }

        /// <summary>True if any solid tile overlaps the world-space rect.</summary>
        public bool OverlapsSolid(Rect r)
        {
            int x0 = Mathf.FloorToInt(r.xMin);
            int x1 = Mathf.FloorToInt(r.xMax - 1e-4f);
            int y0 = Mathf.FloorToInt(r.yMin);
            int y1 = Mathf.FloorToInt(r.yMax - 1e-4f);

            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    if (IsSolid(x, y)) return true;
                }
            }
            return false;
        }

        /// <summary>Count of the 8 neighbours that are solid (out of bounds counts as solid).</summary>
        public int SolidNeighbours(int x, int y)
        {
            int n = 0;
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0) continue;
                    if (!IsEmpty(x + dx, y + dy)) n++;
                }
            return n;
        }

        /// <summary>Sample-based line of sight. Used by archers and enemy activation.</summary>
        public bool HasLineOfSight(Vector2 from, Vector2 to)
        {
            float dx = to.x - from.x;
            float dy = to.y - from.y;
            float dist = Mathf.Sqrt(dx * dx + dy * dy);
            if (dist < 0.001f) return true;

            int samples = Mathf.CeilToInt(dist / 0.25f);
            for (int i = 1; i < samples; i++)
            {
                float t = i / (float)samples;
                if (IsSolidAtWorld(new Vector2(from.x + dx * t, from.y + dy * t))) return false;
            }
            return true;
        }

        /// <summary>
        /// Top-most empty tile at column x that has solid ground beneath it, searching downward.
        /// Returns -1 when nothing suitable exists.
        /// </summary>
        public int FindSurfaceY(int x, int yTop, int yBottom)
        {
            for (int y = yTop; y > yBottom; y--)
            {
                if (IsEmpty(x, y) && !IsEmpty(x, y - 1)) return y;
            }
            return -1;
        }
    }
}
