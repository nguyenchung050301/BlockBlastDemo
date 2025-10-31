using UnityEngine;

/// <summary>
/// Generates a 2D grid of square cells and draws grid lines.
/// Usage: Attach to a GameObject in the scene and press the "Generate Grid" context menu or enable Generate On Start.
/// - Dark blue cells by default
/// - Alternating black lines (every other line visible)
/// </summary>
[ExecuteAlways]
public class GridGenerator : MonoBehaviour, IGridOccupancy
{
    [Header("Grid")]
    public int rows = 8;
    public int cols = 8;
    public float cellSize = 1f;

    [Header("Cell Appearance")]
    public Color cellColor = new Color(0.0f, 0.2f, 0.4f, 1f); // dark blue

    [Header("Lines")]
    public Color lineColor = Color.black;
    [Tooltip("Thickness in world units")]
    public float lineThickness = 0.05f;
    [Tooltip("If true: every other line will be visible (alternating). If false: all lines visible.")]
    public bool alternateLines = false;

    [Header("Options")]
    public bool generateOnStart = false;

    // cached 1x1 pixel sprite used for cells and lines
    private static Sprite s_pixelSprite;
    // --- Occupancy logic for Block Blast ---
    [HideInInspector]
    public bool[,] gridOccupied; // true = ô đã bị chiếm

    private const string CellsParentName = "_Grid_Cells";
    private const string LinesParentName = "_Grid_Lines";

    private void Start()
    {
        if (generateOnStart)
            GenerateGrid();
    }

    /// <summary>
    /// Khởi tạo lại trạng thái chiếm ô (gọi sau khi GenerateGrid)
    /// </summary>
    private void InitializeOccupancy()
    {
        gridOccupied = new bool[cols, rows];
    }

    /// <summary>
    /// Kiểm tra ô có bị chiếm không
    /// </summary>
    public bool IsCellOccupied(int x, int y)
    {
        if (gridOccupied == null) InitializeOccupancy();
        if (x < 0 || y < 0 || x >= cols || y >= rows) return true; // coi như bị chiếm nếu ngoài phạm vi
        return gridOccupied[x, y];
    }

    /// <summary>
    /// Đặt trạng thái chiếm ô
    /// </summary>
    public void SetCellOccupied(int x, int y, bool occupied)
    {
        if (gridOccupied == null) InitializeOccupancy();
        if (x < 0 || y < 0 || x >= cols || y >= rows) return;
        gridOccupied[x, y] = occupied;
    }

    [ContextMenu("Generate Grid")]
    public void GenerateGrid()
    {
        if (rows <= 0 || cols <= 0)
        {
            Debug.LogWarning("Rows and cols must be > 0");
            return;
        }

        EnsurePixelSprite();

        // create or get parents
        Transform cellsParent = GetOrCreateChild(transform, CellsParentName);
        Transform linesParent = GetOrCreateChild(transform, LinesParentName);

        // clear existing children
        ClearChildren(cellsParent);
        ClearChildren(linesParent);

        // center offset so grid is centered on the parent object's position
        float totalWidth = cols * cellSize;
        float totalHeight = rows * cellSize;
        Vector2 origin = new Vector2(-totalWidth / 2f + cellSize / 2f, -totalHeight / 2f + cellSize / 2f);

        // create cells. Make them slightly smaller than `cellSize` so there is a visible gap between each cell
        float innerSize = Mathf.Max(0.001f, cellSize - lineThickness);
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                GameObject cell = new GameObject($"cell_{r}_{c}");
                cell.transform.SetParent(cellsParent, false);
                cell.transform.localPosition = new Vector3(origin.x + c * cellSize, origin.y + r * cellSize, 0f);

                var sr = cell.AddComponent<SpriteRenderer>();
                sr.sprite = s_pixelSprite;
                sr.color = cellColor;
                // grid cells render at base sorting order
                sr.sortingOrder = 0;
                cell.transform.localScale = new Vector3(innerSize, innerSize, 1f);
            }
        }

        // draw vertical lines (cols+1)
        for (int i = 0; i <= cols; i++)
        {
            bool visible = !alternateLines || (i % 2 == 0);
            CreateLine(linesParent, new Vector2(origin.x - cellSize / 2f + i * cellSize, 0f),
                       new Vector2(lineThickness, totalHeight + lineThickness), visible ? lineColor : new Color(0, 0, 0, 0), 1);
        }

        // draw horizontal lines (rows+1)
        for (int i = 0; i <= rows; i++)
        {
            bool visible = !alternateLines || (i % 2 == 0);
            CreateLine(linesParent, new Vector2(0f, origin.y - cellSize / 2f + i * cellSize),
                       new Vector2(totalWidth + lineThickness, lineThickness), visible ? lineColor : new Color(0, 0, 0, 0), 1);
        }
        InitializeOccupancy();
    }


    private void CreateLine(Transform parent, Vector2 localPos, Vector2 size, Color color, int sortingOrder = 0)
    {
        GameObject line = new GameObject("line");
        line.transform.SetParent(parent, false);
        line.transform.localPosition = new Vector3(localPos.x, localPos.y, 0f);

        var sr = line.AddComponent<SpriteRenderer>();
        sr.sprite = s_pixelSprite;
        sr.color = color;
        // allow caller to determine ordering. grid lines default to sortingOrder 1
        sr.sortingOrder = sortingOrder;
        line.transform.localScale = new Vector3(size.x, size.y, 1f);
    }

    private Transform GetOrCreateChild(Transform parent, string name)
    {
        var child = parent.Find(name);
        if (child != null) return child;
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go.transform;
    }

    private void ClearChildren(Transform parent)
    {
        // remove all children (works both in editor and play mode)
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            var child = parent.GetChild(i).gameObject;
            if (Application.isPlaying)
                Destroy(child);
            else
                DestroyImmediate(child);
        }
    }

    private void EnsurePixelSprite()
    {
        if (s_pixelSprite != null) return;

        Texture2D tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        s_pixelSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        s_pixelSprite.name = "__generated_pixel_sprite";
    }
}
