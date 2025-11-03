using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
    // Giữ SpriteRenderer của từng ô để có thể điều khiển màu/alpha khi clear
    private SpriteRenderer[,] cellRenderers;

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

    /// <summary>
    /// Kiểm tra tất cả hàng & cột, và xóa những hàng/cột đã đầy.
    /// </summary>
    public void CheckAndClearFullLines()
    {
        if (gridOccupied == null) return;

        // Danh sách hàng & cột đầy
        System.Collections.Generic.List<int> fullRows = new System.Collections.Generic.List<int>();
        System.Collections.Generic.List<int> fullCols = new System.Collections.Generic.List<int>();

        // Kiểm tra hàng
        for (int y = 0; y < rows; y++)
        {
            bool rowFull = true;
            for (int x = 0; x < cols; x++)
            {
                if (!gridOccupied[x, y])
                {
                    rowFull = false;
                    break;
                }
            }
            if (rowFull)
                fullRows.Add(y);
        }

        // Kiểm tra cột
        for (int x = 0; x < cols; x++)
        {
            bool colFull = true;
            for (int y = 0; y < rows; y++)
            {
                if (!gridOccupied[x, y])
                {
                    colFull = false;
                    break;
                }
            }
            if (colFull)
                fullCols.Add(x);
        }

        // Xóa hàng đầy
        foreach (int y in fullRows)
        {
            ClearRow(y);
        }

        // Xóa cột đầy
        foreach (int x in fullCols)
        {
            ClearColumn(x);
        }
        //   Debug.Log($"cellRenderers[{x},{y}] = {(cellRenderers[x, y] != null ? "OK" : "NULL")}");
        // Sau khi xóa hàng/cột xong -> căn lại toàn bộ block
        RealignPlacedBlocks();

    }

    /// <summary>
    /// Đặt lại trạng thái các ô trong hàng y = false (trống)
    /// </summary>
    private void ClearRow(int y)
    {
        StartCoroutine(ClearRowCoroutine(y));
    }

    private IEnumerator ClearRowCoroutine(int y)
    {
        // Tính lại vị trí gốc của grid (góc trái dưới)
        float totalWidth = cols * cellSize;
        float totalHeight = rows * cellSize;
        Vector3 gridOrigin = transform.position - new Vector3(totalWidth / 2f - cellSize / 2f, totalHeight / 2f - cellSize / 2f, 0);

        for (int x = 0; x < cols; x++)
        {
            gridOccupied[x, y] = false;

            // Tính vị trí world của ô đang clear
            Vector3 cellPos = gridOrigin + new Vector3(x * cellSize, y * cellSize, 0);
            SpawnFadeCell(cellPos);
        }

        yield return new WaitForSeconds(0.35f);
        DeleteCellsOnRow(y);
        Debug.Log($"Cleared row {y} visually");
    }

    /// <summary>
    /// Đặt lại trạng thái các ô trong cột x = false (trống)
    /// </summary>
    private void ClearColumn(int x)
    {
        StartCoroutine(ClearColumnCoroutine(x));
    }

    private IEnumerator ClearColumnCoroutine(int x)
    {
        float totalWidth = cols * cellSize;
        float totalHeight = rows * cellSize;
        Vector3 gridOrigin = transform.position - new Vector3(totalWidth / 2f - cellSize / 2f, totalHeight / 2f - cellSize / 2f, 0);

        for (int y = 0; y < rows; y++)
        {
            gridOccupied[x, y] = false;

            Vector3 cellPos = gridOrigin + new Vector3(x * cellSize, y * cellSize, 0);
            SpawnFadeCell(cellPos);
        }

        yield return new WaitForSeconds(0.35f);
        DeleteCellsOnColumn(x);
        Debug.Log($"Cleared column {x} visually");
    }

    private void SpawnFadeCell(Vector3 position)
    {
        GameObject fx = new GameObject("ClearFXCell");
        fx.transform.position = position;

        var sr = fx.AddComponent<SpriteRenderer>();
        sr.sprite = s_pixelSprite;
        sr.color = new Color(cellColor.r, cellColor.g, cellColor.b, 1f);
        sr.sortingOrder = 50; // đảm bảo nằm trên grid

        StartCoroutine(FadeAndDestroy(sr));
    }

    private IEnumerator FadeAndDestroy(SpriteRenderer sr)
    {
        float duration = 0.3f;
        float t = 0f;
        Color start = sr.color;

        while (t < duration)
        {
            t += Time.deltaTime;
            float a = Mathf.Lerp(1f, 0f, t / duration);
            sr.color = new Color(start.r, start.g, start.b, a);
            yield return null;
        }

        Destroy(sr.gameObject);
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

                // Lưu SpriteRenderer để điều khiển sau này
                if (cellRenderers == null || cellRenderers.GetLength(0) != cols || cellRenderers.GetLength(1) != rows)
                    cellRenderers = new SpriteRenderer[cols, rows];
                cellRenderers[c, r] = sr;


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

    // --- Replace previous DeleteCellsOnRow/DeleteCellsOnColumn with this safer implementation ---

    private void DeleteCellsOnRow(int targetRow)
    {
        float totalWidth = cols * cellSize;
        float totalHeight = rows * cellSize;
        Vector3 gridOrigin = transform.position - new Vector3(totalWidth / 2f - cellSize / 2f, totalHeight / 2f - cellSize / 2f, 0);

        TetrisBlock[] allBlocks = FindObjectsOfType<TetrisBlock>();
        string cellsParentName = "_Block_Cells";

        foreach (var block in allBlocks)
        {
            // Bỏ qua block chưa được đặt vào grid
            if (block == null) continue;
            if (block.draggable) continue;
            if (block.gridReference != this) continue;

            // Thêm kiểm tra an toàn — chỉ xóa nếu block thực sự "đang nằm trên grid"
            if (!IsBlockPlacedOnGrid(block))
                continue;


            var cellsParent = block.transform.Find(cellsParentName);
            if (cellsParent == null) continue;

            var toDelete = new System.Collections.Generic.List<Transform>();

            // Collect only actual cell children (filter by SpriteRenderer and/or name prefix)
            foreach (Transform cell in cellsParent)
            {
                if (cell == null) continue;
                var sr = cell.GetComponent<SpriteRenderer>();
                if (sr == null) continue; // không phải ô hiển thị -> skip

                Vector3 worldPos = cell.position;
                int gridX = Mathf.FloorToInt((worldPos.x - gridOrigin.x) / cellSize);
                int gridY = Mathf.FloorToInt((worldPos.y - gridOrigin.y) / cellSize);

                // Only consider cells that actually map inside this grid
                if (gridX < 0 || gridX >= cols || gridY < 0 || gridY >= rows) continue;

                if (gridY == targetRow)
                {
                    toDelete.Add(cell);
                }
            }

            if (toDelete.Count > 0)
            {
                StartCoroutine(ClearCellAndDestroyBlockSafe(
                    toDelete.Select(t => t.gameObject).ToList(),
                    cellsParent,
                    block.gameObject
                ));
            }

        }
    }

    private void DeleteCellsOnColumn(int targetCol)
    {
        float totalWidth = cols * cellSize;
        float totalHeight = rows * cellSize;
        Vector3 gridOrigin = transform.position - new Vector3(totalWidth / 2f - cellSize / 2f, totalHeight / 2f - cellSize / 2f, 0);

        TetrisBlock[] allBlocks = FindObjectsOfType<TetrisBlock>();
        string cellsParentName = "_Block_Cells";

        foreach (var block in allBlocks)
        {
            // Bỏ qua block chưa được đặt vào grid
            if (block == null) continue;
            if (block.draggable) continue;
            if (block.gridReference != this) continue;

            // Thêm kiểm tra an toàn — chỉ xóa nếu block thực sự "đang nằm trên grid"
            if (!IsBlockPlacedOnGrid(block))
                continue;


            var cellsParent = block.transform.Find(cellsParentName);
            if (cellsParent == null) continue;

            var toDelete = new System.Collections.Generic.List<Transform>();

            foreach (Transform cell in cellsParent)
            {
                if (cell == null) continue;
                var sr = cell.GetComponent<SpriteRenderer>();
                if (sr == null) continue;

                Vector3 worldPos = cell.position;
                int gridX = Mathf.RoundToInt((worldPos.x - gridOrigin.x) / cellSize);
                int gridY = Mathf.RoundToInt((worldPos.y - gridOrigin.y) / cellSize);

                // Only consider cells that actually map inside this grid
                if (gridX < 0 || gridX >= cols || gridY < 0 || gridY >= rows) continue;

                if (gridX == targetCol)
                {
                    toDelete.Add(cell);
                }
            }

            if (toDelete.Count > 0)
            {
                StartCoroutine(ClearCellAndDestroyBlockSafe(
                    toDelete.Select(t => t.gameObject).ToList(),
                    cellsParent,
                    block.gameObject
                ));
            }

        }
    }


    /// <summary>
    /// Nếu parent (cellsParent) rỗng sau delay thì destroy luôn block parent.
    /// </summary>
    private IEnumerator DestroyBlockIfEmptyAfterDelay(Transform cellsParent, GameObject blockGameObject, float delay)
    {
        yield return new WaitForSeconds(delay);

        // cellsParent có thể đã bị thay đổi (destroyed) — kiểm tra an toàn
        if (cellsParent == null)
        {
            yield break;
        }

        if (cellsParent.childCount == 0)
        {
            Destroy(blockGameObject);
        }
    }


    /// <summary>
    /// Fade nhẹ rồi destroy cell.
    /// </summary>
    private IEnumerator FadeAndDestroyCell(GameObject cell)
    {
        var sr = cell.GetComponent<SpriteRenderer>();
        if (sr == null)
        {
            Destroy(cell);
            yield break;
        }

        Color start = sr.color;
        float duration = 0.3f;
        float t = 0f;

        while (t < duration)
        {
            t += Time.deltaTime;
            float a = Mathf.Lerp(1f, 0f, t / duration);
            sr.color = new Color(start.r, start.g, start.b, a);
            yield return null;
        }

        Destroy(cell);
    }

    // --- Fade đồng bộ và xóa block an toàn ---
    private IEnumerator ClearCellAndDestroyBlockSafe(List<GameObject> cells, Transform cellsParent, GameObject blockGO)
    {
        if (cells == null || cells.Count == 0) yield break;

        // ✅ XÓA LINE CỦA CÁC CELL BỊ MERGE
        foreach (var cell in cells)
        {
            if (cell == null) continue;

            Transform block = cell.transform.parent?.parent;
            if (block == null) continue;

            Transform linesParent = block.Find("_Block_Lines");
            if (linesParent == null) continue;

            Vector3 cellPos = cell.transform.position;
            float tolerance = cellSize * 0.51f; // gần 1 cell

            List<Transform> lineToDelete = new List<Transform>();
            foreach (Transform line in linesParent)
            {
                if (line == null) continue;
                Vector3 linePos = line.position;

                // nếu line nằm sát hoặc giao cell → xóa line
                if (Vector3.Distance(linePos, cellPos) < tolerance)
                    lineToDelete.Add(line);
            }

            foreach (var l in lineToDelete)
            {
                if (l != null)
                    Destroy(l.gameObject);
            }
        }
        // ✅ HẾT PHẦN XÓA LINE

        // Sau khi đã xóa các cell và block liên quan:
        yield return new WaitForSeconds(0); // đảm bảo các Destroy() thực hiện xong

        // ✅ Vẽ lại line cho toàn bộ grid để tránh mất viền
        RebuildAllBlockLines();



        // ---- phần fade và destroy cell như cũ ----
        float duration = 0.3f;
        float t = 0f;
        List<SpriteRenderer> renderers = new List<SpriteRenderer>();
        foreach (var c in cells)
        {
            if (c == null) continue;
            var sr = c.GetComponent<SpriteRenderer>();
            if (sr != null) renderers.Add(sr);
        }

        // fade đồng thời tất cả cell
        while (t < duration)
        {
            t += Time.deltaTime;
            float a = Mathf.Lerp(1f, 0f, t / duration);
            foreach (var sr in renderers)
            {
                if (sr != null)
                {
                    Color col = sr.color;
                    sr.color = new Color(col.r, col.g, col.b, a);
                }
            }
            yield return null;
        }

        // xoá cell sau fade
        foreach (var c in cells)
        {
            if (c != null)
                Destroy(c);
        }

        yield return new WaitForEndOfFrame();

        // nếu block trống => xoá luôn
        if (cellsParent == null) yield break;
        if (cellsParent.childCount == 0 && blockGO != null)
            Destroy(blockGO);


    }

    private bool IsBlockPlacedOnGrid(TetrisBlock block)
    {
        if (block == null || block.gridReference != this)
            return false;

        // Kiểm tra: nếu bất kỳ ô của block nằm trong vùng grid -> coi là đã đặt
        float cell = cellSize;
        float totalW = cols * cell;
        float totalH = rows * cell;
        Vector2 gOrigin = new Vector2(-totalW / 2f + cell / 2f, -totalH / 2f + cell / 2f);
        Vector2 worldOrigin = (Vector2)transform.position + gOrigin;

        Vector3 blockPos = block.transform.position;
        Vector2 localToOrigin = (Vector2)blockPos - worldOrigin;
        int gx = Mathf.RoundToInt(localToOrigin.x / cell);
        int gy = Mathf.RoundToInt(localToOrigin.y / cell);

        return gx >= 0 && gx < cols && gy >= 0 && gy < rows;
    }
    // --- Snap tất cả block đang trên grid về đúng tọa độ cell ---
    /*************  ✨ Windsurf Command ⭐  *************/
    /// <summary>
    /// Ensures that the static pixel sprite is created if it is null.
    /// The pixel sprite is used to draw the grid lines.
    /// </summary>
    /*******  6c0585fb-f54c-4cb2-b896-9c3448fcff33  *******/
    private void RealignPlacedBlocks()
    {
        float cell = cellSize;
        float totalW = cols * cell;
        float totalH = rows * cell;
        Vector2 gOrigin = new Vector2(-totalW / 2f + cell / 2f, -totalH / 2f + cell / 2f);
        Vector2 worldOrigin = (Vector2)transform.position + gOrigin;

        foreach (var block in FindObjectsOfType<TetrisBlock>())
        {
            if (block == null) continue;
            if (block.gridReference != this) continue;
            if (block.draggable) continue; // bỏ qua block chưa được đặt
            if (!IsBlockPlacedOnGrid(block)) continue;

            Vector3 pos = block.transform.position;

            // Snap chính xác theo lưới (không trôi float)
            float snappedX = Mathf.Round((pos.x - worldOrigin.x) / cell) * cell + worldOrigin.x;
            float snappedY = Mathf.Round((pos.y - worldOrigin.y) / cell) * cell + worldOrigin.y;
            block.transform.position = new Vector3(snappedX, snappedY, pos.z);
        }
    }

    private void DeleteCorrespondingLinesForCell(GameObject cell)
    {
        if (cell == null) return;

        // tìm block cha
        Transform block = cell.transform.parent?.parent;
        if (block == null) return;

        Transform linesParent = block.Find("_Block_Lines");
        if (linesParent == null) return;

        // vị trí cell (so sánh bằng khoảng cách nhỏ)
        Vector3 cellPos = cell.transform.position;
        float tolerance = cellSize * 0.51f; // khoảng gần 1 cell

        // duyệt tất cả line trong block
        List<Transform> toDelete = new List<Transform>();
        foreach (Transform line in linesParent)
        {
            if (line == null) continue;
            Vector3 linePos = line.position;

            // nếu line nằm sát hoặc giao cell → xóa line
            if (Vector3.Distance(linePos, cellPos) < tolerance)
                toDelete.Add(line);
        }

        foreach (var l in toDelete)
        {
            if (l != null)
                Destroy(l.gameObject);
        }
    }
    private void RebuildAllBlockLines()
    {
        foreach (var block in FindObjectsOfType<TetrisBlock>())
        {
            if (block == null) continue;
            if (block.draggable) continue; // chỉ block đã được đặt trên grid

            // Xóa tất cả line cũ và tạo lại line mới cho từng cell
            Transform linesParent = block.transform.Find("_Block_Lines");
            if (linesParent != null)
            {
                foreach (Transform child in linesParent)
                    Destroy(child.gameObject);
            }

            // Gọi lại hàm vẽ line (giống logic trong GenerateBlock)
            Transform cellsParent = block.transform.Find("_Block_Cells");
            if (cellsParent == null) continue;

            foreach (Transform cell in cellsParent)
            {
                if (cell == null) continue;
                Vector2 cellPos = cell.localPosition;
                float cellSize = block.cellSize;
                Color lineColor = block.internalLineColor;
                float thick = block.internalLineThickness;

                block.CreateLine(linesParent,
                    new Vector2(cellPos.x, cellPos.y + cellSize / 2f),
                    new Vector2(cellSize, thick),
                    lineColor, 4);

                block.CreateLine(linesParent,
                    new Vector2(cellPos.x, cellPos.y - cellSize / 2f),
                    new Vector2(cellSize, thick),
                    lineColor, 4);

                block.CreateLine(linesParent,
                    new Vector2(cellPos.x - cellSize / 2f, cellPos.y),
                    new Vector2(thick, cellSize),
                    lineColor, 4);

                block.CreateLine(linesParent,
                    new Vector2(cellPos.x + cellSize / 2f, cellPos.y),
                    new Vector2(thick, cellSize),
                    lineColor, 4);
            }

            // Cập nhật sorting order
            block.RefreshSortingOrder();
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
