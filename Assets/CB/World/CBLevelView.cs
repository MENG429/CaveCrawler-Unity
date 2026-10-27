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

                    m_rock.SetTile(pos, rockTile);

                    float hash = CBUtil.Hash01(x + seed, y);
                    if (hash < config.accentTileChance)
                    {
                        m_accent.SetTile(pos, hash < config.accentTileChance * 0.5f ? accentMossTile : accentOreTile);
                    }
                }
            }

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
