using UnityEngine;
using UnityEngine.Tilemaps;

namespace CB
{
    /// <summary>
    /// Renders a CBTileWorld with Unity Tilemaps (batched, cheap, and the Tile assets are
    /// trivially swappable for real pixel art later).
    /// No colliders are generated here - collision is handled by CBTileWorld/CBBody.
    /// </summary>
    public class CBLevelView : MonoBehaviour
    {
        Tilemap m_backdrop;
        Tilemap m_rock;
        Tilemap m_accent;

        public void Build(CBTileWorld world, CBConfig config, int seed)
        {
            var gridObject = new GameObject("CB_Grid");
            gridObject.transform.SetParent(transform, false);
            var grid = gridObject.AddComponent<Grid>();
            grid.cellSize = new Vector3(1f, 1f, 0f);
            grid.cellGap = Vector3.zero;
            grid.cellLayout = GridLayout.CellLayout.Rectangle;
            grid.cellSwizzle = GridLayout.CellSwizzle.XYZ;

            m_backdrop = MakeTilemap(gridObject.transform, "CB_Backdrop", -10);
            m_rock = MakeTilemap(gridObject.transform, "CB_Rock", 0);
            m_accent = MakeTilemap(gridObject.transform, "CB_Accent", 1);

            var backdropTile = ScriptableObject.CreateInstance<Tile>();
            backdropTile.sprite = CBAssets.Solid(CBAssets.Backdrop);
            backdropTile.hideFlags = HideFlags.HideAndDontSave;

            // Pixel-art tiles: 4 deterministic variants per layer, picked per-cell by hash.
            // When the tileset sheet is missing the placeholder solid-colour tiles remain.
            Tile[] rockTiles = MakeArtTiles("tileset_cave", "rock", 4);
            if (rockTiles == null) rockTiles = MakeArtTiles("tileset_cave", "rock2", 4);
            Tile[] accentTiles = MakeArtTiles("tileset_cave", "accent", 3);

            var rockTile = ScriptableObject.CreateInstance<Tile>();
            rockTile.sprite = CBAssets.Solid(CBAssets.Rock);
            rockTile.hideFlags = HideFlags.HideAndDontSave;

            var accentMossTile = ScriptableObject.CreateInstance<Tile>();
            accentMossTile.sprite = CBAssets.Solid(CBAssets.RockMoss);
            accentMossTile.hideFlags = HideFlags.HideAndDontSave;

            var accentOreTile = ScriptableObject.CreateInstance<Tile>();
            accentOreTile.sprite = CBAssets.Solid(CBAssets.RockAccent);
            accentOreTile.hideFlags = HideFlags.HideAndDontSave;

            var pos = new Vector3Int();
            for (int y = 0; y < world.Height; y++)
            {
                for (int x = 0; x < world.Width; x++)
                {
                    pos.x = x;
                    pos.y = y;
                    m_backdrop.SetTile(pos, backdropTile);

                    if (!world.IsSolid(x, y)) continue;

                    float hash = CBUtil.Hash01(x + seed, y);
                    m_rock.SetTile(pos, rockTiles != null
                        ? rockTiles[(int)(hash * rockTiles.Length) % rockTiles.Length]
                        : rockTile);

                    if (hash < config.accentTileChance)
                    {
                        m_accent.SetTile(pos, accentTiles != null
                            ? accentTiles[(int)(hash * 997f) % accentTiles.Length]
                            : (hash < config.accentTileChance * 0.5f ? accentMossTile : accentOreTile));
                    }
                }
            }

            // Cave parallax backdrop (self-disabling when the sheet is missing).
            var parallaxObject = new GameObject("CB_Parallax");
            parallaxObject.transform.SetParent(transform, false);
            CBArt.Clip bg;
            parallaxObject.AddComponent<CBParallax>()
                .Build(CBArt.TryGetClip("bg_parallax", "bg", out bg) ? bg : null,
                       new Vector2(world.Width, world.Height));
        }

        /// <summary>
        /// Builds up to count Tile assets from a tileset clip. Returns null when the art is
        /// unavailable so callers can fall back to placeholder tiles. The tileset is a fixed
        /// 8x8 grid of 126px cells at ppu 126, i.e. every sprite is exactly one world unit.
        /// </summary>
        static Tile[] MakeArtTiles(string sheetName, string clipName, int count)
        {
            CBArt.Clip clip;
            if (!CBArt.TryGetClip(sheetName, clipName, out clip)) return null;

            int n = Mathf.Min(count, clip.Frames.Length);
            if (n <= 0) return null;

            var tiles = new Tile[n];
            for (int i = 0; i < n; i++)
            {
                var tile = ScriptableObject.CreateInstance<Tile>();
                tile.sprite = clip.Frames[i];
                tile.hideFlags = HideFlags.HideAndDontSave;
                tiles[i] = tile;
            }
            return tiles;
        }

        Tilemap MakeTilemap(Transform parent, string name, int sortingOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var map = go.AddComponent<Tilemap>();
            var renderer = go.AddComponent<TilemapRenderer>();
            renderer.sortingOrder = sortingOrder;
            map.origin = Vector3Int.zero;
            return map;
        }
    }
}
