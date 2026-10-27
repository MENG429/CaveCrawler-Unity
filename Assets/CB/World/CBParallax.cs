using UnityEngine;

namespace CB
{
    /// <summary>
    /// Cave parallax backdrop built from the bg_parallax sheet. The whole texture is one
    /// sprite (ppu 16 -> 96x64 world units) tiled across the level bounds plus a margin;
    /// the whole layer drifts at a fraction of camera speed for depth.
    /// If the sheet is unavailable the component disables itself and the dark clear colour
    /// plus the placeholder backdrop tiles remain - the game is unaffected.
    /// </summary>
    public class CBParallax : MonoBehaviour
    {
        const float FactorX = 0.75f;   // moves at 25% of camera speed
        const float FactorY = 0.85f;

        public void Build(CBArt.Clip clip, Vector2 worldSize)
        {
            if (clip == null || clip.Frames == null || clip.Frames.Length == 0)
            {
                enabled = false;
                return;
            }

            var sr = gameObject.AddComponent<SpriteRenderer>();
            sr.sprite = clip.Frames[0];
            sr.drawMode = SpriteDrawMode.Simple;
            sr.sortingOrder = -20;   // behind the -10 backdrop tilemap

            // The bg texture is authored as one full-bleed image, so it never tiles at the
            // texture level - we repeat whole sprites instead (no bleeding seams).
            float cellW = sr.sprite.bounds.size.x;
            float cellH = sr.sprite.bounds.size.y;
            if (cellW < 0.5f || cellH < 0.5f) { enabled = false; return; }

            float driftX = (1f - FactorX) * worldSize.x;
            float driftY = (1f - FactorY) * worldSize.y;

            int cols = Mathf.CeilToInt((worldSize.x + driftX) / cellW) + 2;
            int rows = Mathf.CeilToInt((worldSize.y + driftY) / cellH) + 2;

            float x0 = -(cols - 1) * cellW * 0.5f;
            float y0 = worldSize.y * 0.5f - rows * cellH * 0.5f;

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    // Column 0, row 0 keeps the component's own renderer (one fewer object).
                    var cell = (r == 0 && c == 0)
                        ? gameObject
                        : new GameObject("CB_ParallaxCell");
                    if (cell != gameObject)
                    {
                        cell.transform.SetParent(transform, false);
                        var child = cell.AddComponent<SpriteRenderer>();
                        child.sprite = sr.sprite;
                        child.drawMode = SpriteDrawMode.Simple;
                        child.sortingOrder = -20;
                    }
                    cell.transform.localPosition = new Vector3(x0 + c * cellW, y0 + r * cellH, 0f);
                }
            }

            gameObject.name = "CB_Parallax";
        }

        void LateUpdate()
        {
            var cam = CBCamera2D.Instance;
            if (cam == null) return;

            Vector3 p = cam.transform.position;
            transform.position = new Vector3(p.x * (1f - FactorX), p.y * (1f - FactorY), 10f);
        }
    }
}
