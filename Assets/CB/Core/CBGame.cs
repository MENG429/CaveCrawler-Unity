using System.Collections.Generic;
using UnityEngine;

namespace CB
{
    /// <summary>
    /// Central game state and level lifecycle. Everything is assembled at runtime, so there
    /// is no hand-built scene to maintain.
    /// </summary>
    public class CBGame : MonoBehaviour, ICBFixedStep
    {
        static CBGame s_instance;
        public static CBGame Instance { get { return s_instance; } }

        public CBConfig Config { get; private set; }
        public CBLevel Level { get; private set; }
        public CBPlayerController Player { get; private set; }
        public CBUtil.Rng Rng { get; private set; }

        public int LevelDepth { get; private set; }
        public int CurrentSeed { get; private set; }
        public string CurrentPrompt { get; private set; }

        Transform m_levelRoot;
        Transform m_entityRoot;
        Transform m_pickupRoot;
        Transform m_projectileRoot;

        static readonly List<ICBInteractable> s_interactables = new List<ICBInteractable>();

        public CBTileWorld World { get { return Level != null ? Level.World : null; } }
        public Transform ProjectileRoot { get { return m_projectileRoot; } }
        public Transform PickupRoot { get { return m_pickupRoot; } }

        void Awake()
        {
            s_instance = this;
        }

        // ---------------- Boot ----------------
        public void Boot(CBConfig config)
        {
            Config = config;
            Rng = new CBUtil.Rng(config.randomSeed);

            CBWeaponLibrary.Build();
            CBEquipLibrary.Build();
            CBBlessingLibrary.Build();
            CBFx.EnsureHost();

            // Pixel-art sheets load lazily on first use; warmup here only touches the
            // frame table so missing textures degrade to placeholders silently.
            try { CBArt.Warmup(); }
            catch (System.Exception e) { Debug.LogWarning("CBArt warmup failed, placeholder art stays active: " + e.Message); }

            m_levelRoot = MakeRoot("CB_LevelRoot");
            m_entityRoot = MakeRoot("CB_EntityRoot");
            m_pickupRoot = MakeRoot("CB_PickupRoot");
            m_projectileRoot = MakeRoot("CB_ProjectileRoot");

            StartRun();

            SetupCamera();
            SetupHud();

            CBGameClock.Instance.Register(this, CBStepOrder.Items + 1);
        }

        Transform MakeRoot(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            return go.transform;
        }

        void SetupCamera()
        {
            GameObject host;
            var existing = Camera.main;
            if (existing != null)
            {
                host = existing.gameObject;
            }
            else
            {
                host = new GameObject("CB_Camera");
                host.AddComponent<Camera>();
                if (Object.FindObjectOfType<AudioListener>() == null) host.AddComponent<AudioListener>();
            }

            var follow = host.GetComponent<CBCamera2D>();
            if (follow == null) follow = host.AddComponent<CBCamera2D>();
            follow.Init(Config, Player);
            follow.SetBounds(World);
        }

        void SetupHud()
        {
            var go = new GameObject("CB_Hud");
            go.transform.SetParent(transform, false);
            var hud = go.AddComponent<CBHud>();
            hud.Init(Config);
        }

        public void StartRun()
        {
            LevelDepth = 1;

            // A dead player must go, otherwise a restart leaves two players in the world.
            if (Player != null)
            {
                Destroy(Player.gameObject);
                Player = null;
            }

            ClearLevel();
            BuildLevel(Rng.SeedNext());
            SpawnPlayer();

            if (CBCamera2D.Instance != null)
            {
                CBCamera2D.Instance.SetTarget(Player);
                CBCamera2D.Instance.SetBounds(World);
            }
        }

        public void RestartRun()
        {
            StartRun();
        }

        public void NextLevel()
        {
            LevelDepth++;
            ClearLevel();
            BuildLevel(Rng.SeedNext());
            PlacePlayerAtSpawn();

            if (CBCamera2D.Instance != null)
            {
                CBCamera2D.Instance.SetBounds(World);
                CBCamera2D.Instance.SnapToTarget();
            }
        }

        void BuildLevel(int seed)
        {
            CurrentSeed = seed;

            var levelObject = new GameObject("CB_Level");
            levelObject.transform.SetParent(m_levelRoot, false);
            Level = levelObject.AddComponent<CBLevel>();
            Level.Build(Config, seed);

            PopulateLevel();
        }

        Vector2 PlayerSpawnWorld()
        {
            if (Level == null) return Vector2.zero;
            Vector2 tile = Level.SpawnPoint;
            return new Vector2(tile.x, tile.y - 0.5f + Config.playerHeight * 0.5f + 0.02f);
        }

        /// <summary>
        /// Rebinds the player to the freshly generated grid. Without this the player keeps
        /// colliding against the previous level's tiles.
        /// </summary>
        void PlacePlayerAtSpawn()
        {
            if (Player == null || World == null) return;

            // CBBody exposes World as a public field by design (it is a data-forward physics
            // component), so rebind it directly - it intentionally has no SetWorld accessor.
            Player.Body.World = World;
            if (Player.Combat != null) Player.Combat.SetWorld(World);
            Player.Body.UseGravity = true;
            Player.Body.Teleport(PlayerSpawnWorld());
        }

        void SpawnPlayer()
        {
            var go = new GameObject("CB_Player");
            go.transform.SetParent(m_entityRoot, false);

            var player = go.AddComponent<CBPlayerController>();
            player.InitPlayer(Config, World, PlayerSpawnWorld());

            var combat = go.AddComponent<CBPlayerCombat>();
            combat.Init(Config, player, World);
            player.Combat = combat;

            Player = player;
        }

        // ---------------- Population ----------------
        void PopulateLevel()
        {
            if (Level == null || Level.World == null) return;

            // Exit portal.
            var portalObject = new GameObject("CB_Portal");
            portalObject.transform.SetParent(m_entityRoot, false);
            var portal = portalObject.AddComponent<CBPortal>();
            portal.Init(Config, Level.ExitPoint + new Vector2(0f, 0.6f));

            var rooms = Level.Rooms;
            for (int r = 0; r < rooms.Count; r++)
            {
                var room = rooms[r];

                // Enemies: skip the spawn room so the player always gets a safe start.
                if (r > 0)
                {
                    int count = Rng.Int(Config.enemiesPerRoomMin, Config.enemiesPerRoomMax);
                    for (int i = 0; i < count; i++) SpawnEnemy(room);
                }

                for (int i = 0; i < Config.chestsPerRoom; i++) SpawnChest(room);
                for (int i = 0; i < Config.pedestalsPerRoom; i++) SpawnPedestal(room);
                if (Rng.Chance(Config.blessingShrineChance)) SpawnBlessingShrine(room);
            }
        }

        void SpawnBlessingShrine(CBRoom room)
        {
            Vector2Int tile;
            if (!Level.TryPickSpawnTile(room, Rng, 4, out tile)) return;
            var go = new GameObject("CB_BlessingShrine");
            go.transform.SetParent(m_entityRoot, false);
            var shrine = go.AddComponent<CBBlessingShrine>();
            shrine.Init(Config, new Vector2(tile.x + 0.5f, tile.y + 0.6f));
        }

        void SpawnEnemy(CBRoom room)
        {
            Vector2Int tile;
            if (!Level.TryPickSpawnTile(room, Rng, 3, out tile)) return;

            Vector2 position = new Vector2(tile.x + 0.5f, tile.y + 0.9f);
            int roll = Rng.Int(0, 99);

            if (roll < 45)
            {
                var go = new GameObject("CB_Chaser");
                go.transform.SetParent(m_entityRoot, false);
                var enemy = go.AddComponent<CBEnemyMelee>();
                enemy.InitMelee(Config, World, Level.Pathfinder, position, Rng.Chance(0.25f));
            }
            else if (roll < 75)
            {
                var go = new GameObject("CB_Bat");
                go.transform.SetParent(m_entityRoot, false);
                var enemy = go.AddComponent<CBEnemyBat>();
                enemy.InitBat(Config, World, Level.Pathfinder, position + new Vector2(0f, 1.5f));
            }
            else
            {
                var go = new GameObject("CB_Archer");
                go.transform.SetParent(m_entityRoot, false);
                var enemy = go.AddComponent<CBEnemyArcher>();
                enemy.InitArcher(Config, World, Level.Pathfinder, position);
            }
        }

        void SpawnChest(CBRoom room)
        {
            Vector2Int tile;
            if (!Level.TryPickSpawnTile(room, Rng, 4, out tile)) return;

            var go = new GameObject("CB_Chest");
            go.transform.SetParent(m_entityRoot, false);
            var chest = go.AddComponent<CBChest>();
            chest.Init(Config, new Vector2(tile.x + 0.5f, tile.y + 0.35f));
        }

        void SpawnPedestal(CBRoom room)
        {
            Vector2Int tile;
            if (!Level.TryPickSpawnTile(room, Rng, 4, out tile)) return;

            bool wantBow = Rng.Chance(0.5f);
            CBWeaponDef def = wantBow ? (CBWeaponDef)CBWeaponLibrary.RandomBow(Rng) : CBWeaponLibrary.RandomSword(Rng);
            if (def == null) return;

            var go = new GameObject("CB_Pedestal");
            go.transform.SetParent(m_entityRoot, false);
            var pedestal = go.AddComponent<CBPedestal>();
            pedestal.Init(Config, new Vector2(tile.x + 0.5f, tile.y + 0.35f), def);
        }

        // ---------------- Loot ----------------
        public void DropLoot(Vector2 position)
        {
            int coins = Rng.Int(Config.coinDropMin, Config.coinDropMax);
            for (int i = 0; i < coins; i++)
            {
                CBPickup.Spawn(Config, World, m_pickupRoot, position, CBPickup.Kind.Coin, 1, null);
            }

            if (Rng.Chance(Config.potionDropChance))
            {
                CBPickup.Spawn(Config, World, m_pickupRoot, position, CBPickup.Kind.Potion, Config.potionHeal, null);
            }

            if (Rng.Chance(Config.weaponDropChance))
            {
                bool wantBow = Rng.Chance(0.5f);
                CBWeaponDef def = wantBow ? (CBWeaponDef)CBWeaponLibrary.RandomBow(Rng) : CBWeaponLibrary.RandomSword(Rng);
                if (def != null) CBPickup.Spawn(Config, World, m_pickupRoot, position, CBPickup.Kind.Weapon, 0, def);
            }

            if (Rng.Chance(Config.equipmentDropChance))
            {
                var equip = CBEquipLibrary.Roll(Rng);
                if (equip != null) CBPickup.Spawn(Config, World, m_pickupRoot, position, CBPickup.Kind.Equipment, 0, null, equip);
            }
        }

        // ---------------- Interactables ----------------
        public static void RegisterInteractable(ICBInteractable item)
        {
            if (item != null && !s_interactables.Contains(item)) s_interactables.Add(item);
        }

        public static void UnregisterInteractable(ICBInteractable item)
        {
            if (item != null) s_interactables.Remove(item);
        }

        // ---------------- Fixed step ----------------
        public void FixedStep(float dt)
        {
            if (Player == null) return;

            if (CBInput.ConsumeRestart())
            {
                RestartRun();
                return;
            }

            if (CBInput.ConsumeRegen())
            {
                ClearLevel();
                BuildLevel(Rng.SeedNext());
                PlacePlayerAtSpawn();
                if (CBCamera2D.Instance != null)
                {
                    CBCamera2D.Instance.SetBounds(World);
                    CBCamera2D.Instance.SnapToTarget();
                }
                return;
            }

            UpdatePrompt();

            if (Player.Dead) return;

            if (CBInput.ConsumeInteract())
            {
                var target = FindInteractable();
                if (target != null) target.Interact(Player);
            }
        }

        void UpdatePrompt()
        {
            CurrentPrompt = string.Empty;
            if (Player == null || Player.Dead || Player.Body == null) return;

            ICBInteractable best = null;
            float bestDist = float.MaxValue;

            for (int i = 0; i < s_interactables.Count; i++)
            {
                var item = s_interactables[i];
                if (item == null || !item.IsAvailable) continue;

                var behaviour = item as MonoBehaviour;
                if (behaviour == null) continue;

                float d = Vector2.Distance(Player.Body.Position, behaviour.transform.position);
                if (d > item.InteractRadius || d >= bestDist) continue;
                bestDist = d;
                best = item;
            }

            if (best != null) CurrentPrompt = best.Prompt;
        }

        ICBInteractable FindInteractable()
        {
            if (Player == null || Player.Body == null) return null;

            ICBInteractable best = null;
            float bestDist = float.MaxValue;

            for (int i = 0; i < s_interactables.Count; i++)
            {
                var item = s_interactables[i];
                if (item == null || !item.IsAvailable) continue;

                var behaviour = item as MonoBehaviour;
                if (behaviour == null) continue;

                float d = Vector2.Distance(Player.Body.Position, behaviour.transform.position);
                if (d > item.InteractRadius || d >= bestDist) continue;
                bestDist = d;
                best = item;
            }
            return best;
        }

        // ---------------- Teardown ----------------
        void ClearLevel()
        {
            for (int i = s_interactables.Count - 1; i >= 0; i--) s_interactables.RemoveAt(i);

            DestroyChildren(m_levelRoot);
            DestroyChildren(m_pickupRoot);
            DestroyChildren(m_projectileRoot);

            // Keep the player; remove everything else.
            if (m_entityRoot != null)
            {
                for (int i = m_entityRoot.childCount - 1; i >= 0; i--)
                {
                    var child = m_entityRoot.GetChild(i);
                    if (Player != null && child.gameObject == Player.gameObject) continue;
                    Destroy(child.gameObject);
                }
            }

            CBActorRegistry.Clear();
            if (Player != null && !Player.Dead) CBActorRegistry.Add(Player);

            Level = null;
        }

        static void DestroyChildren(Transform root)
        {
            if (root == null) return;
            for (int i = root.childCount - 1; i >= 0; i--) Destroy(root.GetChild(i).gameObject);
        }

        public void OnPlayerDied()
        {
            // Respawn is manual (R) so the death state is readable.
            if (CBCamera2D.Instance != null) CBCamera2D.Instance.Shake(0.6f);
        }
    }
}
