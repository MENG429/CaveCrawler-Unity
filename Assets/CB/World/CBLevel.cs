using System.Collections.Generic;
using UnityEngine;

namespace CB
{
    /// <summary>Facade over one generated level: grid, pathfinder, spawn anchors, view.</summary>
    public class CBLevel : MonoBehaviour
    {
        public CBTileWorld World { get; private set; }
        public CBPathfinder Pathfinder { get; private set; }
        public CBGenResult GenResult { get; private set; }
        public int Seed { get; private set; }

        public Vector2 SpawnPoint { get; private set; }
        public Vector2 ExitPoint { get; private set; }
        public Vector2Int ExitTile { get; private set; }

        readonly List<CBRoom> m_rooms = new List<CBRoom>();

        public IReadOnlyList<CBRoom> Rooms { get { return m_rooms; } }

        public void Build(CBConfig config, int seed)
        {
            Seed = seed;
            GenResult = CBCaveGenerator.Generate(config, seed);
            World = GenResult.World;
            Pathfinder = new CBPathfinder(World);
            Pathfinder.SetMaxNodes(config.pathfinderMaxNodes);

            m_rooms.Clear();
            m_rooms.AddRange(GenResult.Rooms);

            var viewObject = new GameObject("CB_LevelView");
            viewObject.transform.SetParent(transform, false);
            var view = viewObject.AddComponent<CBLevelView>();
            view.Build(World, config, seed);

            SpawnPoint = World.TileCenter(GenResult.SpawnTile.x, GenResult.SpawnTile.y);
            ExitTile = GenResult.ExitTile;
            ExitPoint = World.TileCenter(ExitTile.x, ExitTile.y);
        }

        public bool TryPickSpawnTile(CBRoom room, CBUtil.Rng rng, int margin, out Vector2Int tile)
        {
            return CBCaveGenerator.TryPickFloorTile(World, room, rng, margin, out tile);
        }

        public Vector2 TileCenterWorld(Vector2Int tile) { return World.TileCenter(tile.x, tile.y); }
    }
}
