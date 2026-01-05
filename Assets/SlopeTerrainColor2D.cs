using UnityEngine;

public class SlopeTerrainColor2D : MonoBehaviour
{
    public RandomSlopeTerrain2D terrain;

    [Header("Mars Colors")]
    public Color marsBright = new Color(0.78f, 0.42f, 0.24f);
    public Color marsMid    = new Color(0.55f, 0.30f, 0.18f);
    public Color marsDark   = new Color(0.28f, 0.14f, 0.08f);

    [Header("Rendering")]
    public float zOffset = 1f; // behind arrows
    public string sortingLayerName = "Default";
    public int sortingOrder = -10;

    private SpriteRenderer[,] cells;

    private void Start()
    {
        if (terrain == null)
            terrain = GetComponent<RandomSlopeTerrain2D>();

        BuildCells();
        UpdateColors();
    }

    [ContextMenu("Build Cells")]
    public void BuildCells()
    {
        if (terrain == null) return;

        cells = new SpriteRenderer[terrain.width, terrain.height];

        for (int x = 0; x < terrain.width; x++)
        {
            for (int y = 0; y < terrain.height; y++)
            {
                GameObject go = new GameObject("Cell_" + x + "_" + y);
                go.transform.SetParent(transform, false);

                SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = GetWhiteSprite();
                sr.sortingLayerName = sortingLayerName;
                sr.sortingOrder = sortingOrder;

                Vector3 pos = terrain.CellCenterWorld(x, y);
                pos.z += zOffset;
                go.transform.position = pos;

                float s = terrain.cellSize;
                go.transform.localScale = new Vector3(terrain.cellSize, terrain.cellSize, 1f);

                cells[x, y] = sr;
            }
        }
    }

    [ContextMenu("Update Colors")]
    public void UpdateColors()
    {
        if (terrain == null || cells == null) return;


        for (int x = 0; x < terrain.width; x++)
        {
            for (int y = 0; y < terrain.height; y++)
            {
                float s = terrain.GetSlope(x, y);
                float maxAbs = terrain.maxAbsSlope;

                float tRaw = Mathf.Clamp01(Mathf.Abs(s) / maxAbs);

                // Curve shaping
                float t = (s > 0f)
                    ? Mathf.Pow(tRaw, 0.55f)   // peaks
                    : Mathf.Pow(tRaw, 0.85f);  // pits

                Color c;
                if (s > 0f)
                {
                    // Mid → Bright (uplands)
                    c = Color.Lerp(marsMid, marsBright, t);
                }
                else if (s < 0f)
                {
                    // Mid → Dark (pits)
                    c = Color.Lerp(marsMid, marsDark, t);
                }
                else
                {
                    c = marsMid;
                }

                cells[x, y].color = c;

            }
        }
    }

    private static Sprite _whiteSprite;

    private Sprite GetWhiteSprite()
    {
        if (_whiteSprite != null)
            return _whiteSprite;

        Texture2D tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();

        // IMPORTANT: pixelsPerUnit = 1f so the sprite is 1 world unit wide
        _whiteSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);

        return _whiteSprite;
    }

}
