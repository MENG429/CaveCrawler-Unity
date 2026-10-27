using System.Collections.Generic;
using UnityEngine;

namespace CB
{
    public class CBRoom
    {
        public int X0;
        public int X1;      // inclusive
        public int Y0;
        public int Y1;      // inclusive
        public int CenterX { get { return (X0 + X1) >> 1; } }
    }

    public class CBGenResult
    {
        public CBTileWorld World;
        public Vector2Int SpawnTile;
        public Vector2Int ExitTile;
        public List<CBRoom> Rooms = new List<CBRoom>();
        public int AttemptsUsed;
        public bool UsedForcedTunnel;
    }

    /// <summary>
    /// Linear cave sequence: spawn room -> N normal rooms -> exit room holding the portal.
    /// Each room is carved with a cellular automaton, adjacent rooms are joined by a corridor.
    ///
    /// HARD REQUIREMENT: spawn must reach the exit. We flood fill from spawn, seal every
    /// unreachable pocket, and verify the exit tile is in the reachable set. If it is not we
    /// carve a forced 3-high tunnel and re-validate; failing that we re-roll the whole level.
    /// </summary>
    public static class CBCaveGenerator
    {
        public static CBGenResult Generate(CBConfig config, int seed)
        {
            for (int attempt = 0; attempt < config.maxGenerationAttempts; attempt++)
            {
                var result = TryGenerate(config, seed + attempt * 7919);
                if (result != null)
                {
                    result.AttemptsUsed = attempt + 1;
                    return result;
                }
            }

            // Absolute fallback: a flat corridor level. Never returns null, so the game
            // is always playable even if the CA does something pathological.
            return BuildFallback(config);
        }

        static CBGenResult TryGenerate(CBConfig config, int seed)
        {
            var rng = new CBUtil.Rng(seed);

            int w = Mathf.Max(3, config.roomCount * config.roomWidth);
            int h = Mathf.Max(8, config.roomHeight);
            var world = new CBTileWorld(w, h);
            world.Fill(CBTileWorld.Solid);

            var rooms = new List<CBRoom>();
            for (int r = 0; r < config.roomCount; r++)
            {
                int x0 = r * config.roomWidth + 1;
                int x1 = (r + 1) * config.roomWidth - 2;
                var room = new CBRoom { X0 = x0, X1 = x1, Y0 = 2, Y1 = h - 3 };
                rooms.Add(room);
                CarveRoom(world, room, config, rng);
            }

            // Corridors between consecutive rooms.
            for (int r = 0; r < rooms.Count - 1; r++)
            {
                int seam = rooms[r].X1 + 1;
                int cy = rng.Int(rooms[r].Y0 + 2, rooms[r].Y1 - 3);
                int half = config.corridorHeight >> 1;
                world.CarveRect(seam - 3, cy - 1, seam + 3, cy + half);
            }

            // Outer shell.
            world.FillRect(0, 0, w - 1, 0);
            world.FillRect(0, h - 1, w - 1, h - 1);
            world.FillRect(0, 0, 0, h - 1);
            world.FillRect(w - 1, 0, w - 1, h - 1);

            var result = new CBGenResult { World = world, Rooms = rooms };

            // Spawn: left side of the first room.
            var spawnRoom = rooms[0];
            int spawnX = Mathf.Clamp(spawnRoom.X0 + 3, 1, w - 2);
            int spawnY = EnsureFloor(world, spawnX, spawnRoom.Y0, spawnRoom.Y1, rng);
            result.SpawnTile = new Vector2Int(spawnX, spawnY);

            // Exit: right side of the last room.
            var exitRoom = rooms[rooms.Count - 1];
            int exitX = Mathf.Clamp(exitRoom.X1 - 3, 1, w - 2);
            int exitY = EnsureFloor(world, exitX, exitRoom.Y0, exitRoom.Y1, rng);
            result.ExitTile = new Vector2Int(exitX, exitY);

            for (int pass = 0; pass < 4; pass++)
            {
                var reachable = FloodFill(world, result.SpawnTile);

                // Seal pockets that cannot be reached - keeps enemies and loot spawnable.
                for (int y = 1; y < h - 1; y++)
                    for (int x = 1; x < w - 1; x++)
                        if (world.IsEmpty(x, y) && !reachable.Contains(y * w + x))
                            world.Set(x, y, CBTileWorld.Solid);

                if (reachable.Contains(result.ExitTile.y * w + result.ExitTile.x))
                    return result;

                // Not connected - punch a guaranteed 3-high L tunnel and try again.
                result.UsedForcedTunnel = true;
                CarveGuaranteedTunnel(world, result.SpawnTile, result.ExitTile);
            }

            return null;
        }

        static void CarveRoom(CBTileWorld world, CBRoom room, CBConfig config, CBUtil.Rng rng)
        {
            int w = room.X1 - room.X0 + 1;
            int h = room.Y1 - room.Y0 + 1;
            var cells = new byte[w * h];

            for (int i = 0; i < cells.Length; i++)
                cells[i] = rng.Chance(config.initialWallChance) ? CBTileWorld.Solid : CBTileWorld.Empty;

            for (int iter = 0; iter < config.cellularIterations; iter++)
            {
                var next = new byte[w * h];
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        int n = 0;
                        for (int dy = -1; dy <= 1; dy++)
                            for (int dx = -1; dx <= 1; dx++)
                            {
                                if (dx == 0 && dy == 0) continue;
                                int nx = x + dx;
                                int ny = y + dy;
                                bool solid = (nx < 0 || ny < 0 || nx >= w || ny >= h)
                                    ? true
                                    : cells[ny * w + nx] == CBTileWorld.Solid;
                                if (solid) n++;
                            }
                        next[y * w + x] = (n >= config.cellularWallThreshold) ? CBTileWorld.Solid : CBTileWorld.Empty;
                    }
                }
                cells = next;
            }

            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    world.Set(room.X0 + x, room.Y0 + y, cells[y * w + x]);
        }

        /// <summary>Assure there is a standable floor at column x inside [y0,y1].</summary>
        public static int EnsureFloor(CBTileWorld world, int x, int y0, int y1, CBUtil.Rng rng)
        {
            int surface = world.FindSurfaceY(x, y1, y0);
            if (surface > 0) return surface;

            int mid = rng != null ? rng.Int(y0 + 2, y1 - 2) : (y0 + y1) >> 1;
            mid = Mathf.Clamp(mid, y0 + 2, y1 - 2);
            for (int x2 = x - 1; x2 <= x + 1; x2++)
            {
                world.Set(x2, mid, CBTileWorld.Empty);
                world.Set(x2, mid + 1, CBTileWorld.Empty);
                world.Set(x2, mid - 1, CBTileWorld.Solid);
            }
            return mid;
        }

        static HashSet<int> FloodFill(CBTileWorld world, Vector2Int start)
        {
            var seen = new HashSet<int>();
            int w = world.Width;
            var queue = new Queue<int>();
            int startIndex = start.y * w + start.x;
            if (!world.IsEmpty(start.x, start.y)) return seen;

            queue.Enqueue(startIndex);
            seen.Add(startIndex);

            while (queue.Count > 0)
            {
                int cur = queue.Dequeue();
                int cx = cur % w;
                int cy = cur / w;

                for (int i = 0; i < 4; i++)
                {
                    int nx = cx + (i == 0 ? 1 : i == 1 ? -1 : 0);
                    int ny = cy + (i == 2 ? 1 : i == 3 ? -1 : 0);
                    if (!world.IsEmpty(nx, ny)) continue;
                    int ni = ny * w + nx;
                    if (seen.Contains(ni)) continue;
                    seen.Add(ni);
                    queue.Enqueue(ni);
                }
            }
            return seen;
        }

        static void CarveGuaranteedTunnel(CBTileWorld world, Vector2Int a, Vector2Int b)
        {
            int x0 = Mathf.Min(a.x, b.x);
            int x1 = Mathf.Max(a.x, b.x);
            world.CarveRect(x0, a.y, x1, a.y + 2);
            int y0 = Mathf.Min(a.y, b.y);
            int y1 = Mathf.Max(a.y, b.y);
            world.CarveRect(b.x, y0, b.x + 1, y1 + 2);
        }

        static CBGenResult BuildFallback(CBConfig config)
        {
            int w = Mathf.Max(3, config.roomCount * config.roomWidth);
            int h = Mathf.Max(8, config.roomHeight);
            var world = new CBTileWorld(w, h);
            world.Fill(CBTileWorld.Solid);
            world.CarveRect(1, 3, w - 2, 7);

            var rooms = new List<CBRoom>();
            for (int r = 0; r < config.roomCount; r++)
            {
                rooms.Add(new CBRoom
                {
                    X0 = r * config.roomWidth + 1,
                    X1 = (r + 1) * config.roomWidth - 2,
                    Y0 = 2,
                    Y1 = h - 3
                });
            }

            var fallback = new CBGenResult
            {
                World = world,
                Rooms = rooms,
                SpawnTile = new Vector2Int(3, 4),
                ExitTile = new Vector2Int(w - 4, 4),
                UsedForcedTunnel = true
            };
            return fallback;
        }

        /// <summary>
        /// Picks a standable tile inside a room, away from the room edges.
        /// Returns false when the room is unusable (caller should skip the spawn).
        /// </summary>
        public static bool TryPickFloorTile(CBTileWorld world, CBRoom room, CBUtil.Rng rng, int margin, out Vector2Int tile)
        {
            tile = new Vector2Int(room.X0, room.Y0);
            if (room.X1 - room.X0 <= margin * 2) return false;

            for (int i = 0; i < 24; i++)
            {
                int x = rng.Int(room.X0 + margin, room.X1 - margin);
                int y = world.FindSurfaceY(x, room.Y1, room.Y0);
                if (y > room.Y0 && world.IsEmpty(x, y + 1))
                {
                    tile = new Vector2Int(x, y);
                    return true;
                }
            }
            return false;
        }
    }
}
