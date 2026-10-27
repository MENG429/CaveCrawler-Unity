using System.Collections.Generic;
using UnityEngine;

namespace CB
{
    /// <summary>
    /// Placeholder art: solid-colour 4x4 pixel blocks.
    /// IMPORTANT: sprites are created with SpriteMeshType.FullRect and a 1px border so they
    /// can be used with SpriteDrawMode.Sliced without Unity emitting
    /// "Sprite Tiling might not appear correctly" every frame.
    /// All gameplay code reads colour/size from CBConfig or from a SpriteRenderer - never from here.
    /// </summary>
    public static class CBAssets
    {
        public const int SpritePixelsPerUnit = 4;

        static readonly Dictionary<int, Sprite> s_cache = new Dictionary<int, Sprite>();

        // ---------------- Palette (dark cavern) ----------------
        public static readonly Color Rock       = new Color(0.22f, 0.20f, 0.26f);
        public static readonly Color RockAccent = new Color(0.33f, 0.26f, 0.21f);
        public static readonly Color RockMoss   = new Color(0.18f, 0.28f, 0.24f);
        public static readonly Color Backdrop   = new Color(0.06f, 0.05f, 0.09f);

        public static readonly Color Player     = new Color(0.62f, 0.82f, 0.96f);
        public static readonly Color Chaser     = new Color(0.36f, 0.66f, 0.38f);
        public static readonly Color Bruiser    = new Color(0.62f, 0.46f, 0.28f);
        public static readonly Color Bat        = new Color(0.48f, 0.32f, 0.60f);
        public static readonly Color Archer     = new Color(0.78f, 0.56f, 0.30f);

        public static readonly Color Coin       = new Color(0.96f, 0.80f, 0.22f);
        public static readonly Color Potion     = new Color(0.90f, 0.26f, 0.32f);
        public static readonly Color Chest      = new Color(0.56f, 0.40f, 0.20f);
        public static readonly Color Pedestal   = new Color(0.40f, 0.38f, 0.46f);
        public static readonly Color Portal     = new Color(0.36f, 0.76f, 1.00f);

        public static readonly Color Arrow      = new Color(0.86f, 0.86f, 0.76f);
        public static readonly Color EnemyArrow = new Color(0.95f, 0.60f, 0.35f);
        public static readonly Color Slash      = new Color(0.95f, 0.97f, 1.00f);
        public static readonly Color Fire       = new Color(1.00f, 0.62f, 0.22f);
        public static readonly Color Poison     = new Color(0.45f, 0.90f, 0.40f);
        public static readonly Color Ice        = new Color(0.55f, 0.85f, 1.00f);

        public static Sprite Solid(Color colour)
        {
            int key = ColorToKey(colour);
            Sprite cached;
            if (s_cache.TryGetValue(key, out cached) && cached != null) return cached;

            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.hideFlags = HideFlags.HideAndDontSave;

            var px = new Color32[16];
            var c32 = (Color32)colour;
            for (int i = 0; i < px.Length; i++) px[i] = c32;
            tex.SetPixels32(px);
            tex.Apply(false, true);

            // FullRect + explicit border => safe for Sliced / Tiled draw modes.
            var sprite = Sprite.Create(
                tex,
                new Rect(0f, 0f, 4f, 4f),
                new Vector2(0.5f, 0.5f),
                SpritePixelsPerUnit,
                0u,
                SpriteMeshType.FullRect,
                new Vector4(1f, 1f, 1f, 1f));

            sprite.name = "cb_solid_" + key.ToString("X8");
            sprite.hideFlags = HideFlags.HideAndDontSave;
            s_cache[key] = sprite;
            return sprite;
        }

        /// <summary>
        /// Creates a SpriteRenderer-driven quad entity.
        /// NOTE: never GameObject.CreatePrimitive in 2D (it brings a 3D BoxCollider).
        /// </summary>
        public static SpriteRenderer MakeSprite(GameObject host, Color colour, Vector2 size, int sortingOrder, string sortingLayer = "Default")
        {
            var sr = host.AddComponent<SpriteRenderer>();
            sr.sprite = Solid(colour);
            sr.drawMode = SpriteDrawMode.Sliced;
            sr.size = size;
            sr.sortingOrder = sortingOrder;
            if (!string.IsNullOrEmpty(sortingLayer)) sr.sortingLayerName = sortingLayer;
            return sr;
        }

        static int ColorToKey(Color c)
        {
            return ((int)(c.r * 255f) << 16) | ((int)(c.g * 255f) << 8) | (int)(c.b * 255f);
        }
    }
}
