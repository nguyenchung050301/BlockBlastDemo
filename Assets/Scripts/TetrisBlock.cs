using UnityEngine;
using System.Linq;
using System.Collections.Generic;
using System.Collections;

/// <summary>
/// Generates a tetromino-like block composed of square cells (like Tetris pieces).
/// Attach to an empty GameObject and use the context menu "Generate Block" to create the piece children.
/// Supports basic rotation (Rotate CW) and optional BoxCollider2D on each cell.
/// </summary>
[ExecuteAlways]
public class TetrisBlock : MonoBehaviour
{
    public enum ShapeSet { BlockBlast, Custom }
    public enum BlockBlastType { Single, Pair, Square2, Line3, SmallL, Plus, BigSquare }

    [Header("Block")]
    public ShapeSet shapeSet = ShapeSet.BlockBlast;
    [Tooltip("Choose a Block Blast preset shape when Shape Set = BlockBlast")]
    public BlockBlastType blockBlastType = BlockBlastType.Square2;
    [Tooltip("Use when Shape Set = Custom. Define cell offsets relative to the block origin.")]
    public Vector2Int[] customOffsets;
    public Color cellColor = new Color(0.0f, 0.4f, 0.7f, 1f);

    private Vector3 initialPosition; // Store the initial position of the block
    [Header("Color")]
    [Tooltip("If true, each generated block will get a random color.")]
    public bool randomizeColor = true;
    [Tooltip("Optional palette of colors to pick from. If empty, a random HSV color will be used.")]
    public Color[] colorPalette;
    public float cellSize = 1f;
    public bool addCollider = false;
    public bool generateOnStart = false;
    [Header("Internal Lines")]
    public bool drawInternalLines = true;
    public Color internalLineColor = Color.black;
    [Tooltip("Thickness in world units for internal block lines")]
    public float internalLineThickness = 0.05f;
    [Header("Grid Integration")]
    [Tooltip("Optional reference to a GridGenerator in the scene. When set and MatchGridStyle=true, the block will use the grid's line color/thickness and can snap to grid cell size.")]
    public GridGenerator gridReference;
    [Tooltip("If true and gridReference is set, block internal lines will match the grid's line color and thickness.")]
    public bool matchGridStyle = true;
    [Tooltip("If true and gridReference is set, generated block will align its cell positions to the grid cell centers (snaps transform position)")]
    public bool snapToGrid = false;
    [Header("Drag & Snap")]
    [Tooltip("Allow dragging the block with the mouse (requires a Collider2D on the block or parent).")]
    public bool draggable = true;
    [Tooltip("Automatically add a BoxCollider2D sized to the block so it can be dragged.")]
    public bool addDragCollider = true;

    private static Sprite s_pixelSprite;
    private const string CellsParentName = "_Block_Cells";
    // remember the last generated cell offsets so snapping can validate all cells
    private Vector2Int[] _lastOffsets;
    private const string PreviewParentName = "_Block_Preview";
    [Header("Preview")]
    [Tooltip("Color used for block preview when dragging")]
    public Color previewColor = new Color(0.5f, 0.5f, 0.5f, 0.3f);
    // chosen color for the current block instance
    private Color _chosenColor;
    private int _originalSortingOrder = 2;
    private string _originalSortingLayer;
    private const string DraggingSortingLayer = "DraggingBlock";

    [Header("Scale settings")]
    public float spawnerScale = 0.8f;     // scale khi ở spawner
    public float gridScale = 1.0f;        // scale khi đặt vào grid
    public float scaleTransitionSpeed = 10f; // tốc độ phóng to/thu nhỏ
    private bool isInSpawner = true;      // đánh dấu block đang ở spawner

    private void Start()
    {
        // khi spawn ra, block sẽ nhỏ hơn gốc
        transform.localScale = Vector3.one * spawnerScale;
        isInSpawner = true;

        if (generateOnStart)
            GenerateBlock();
    }

    [ContextMenu("Generate Block")]
    public void GenerateBlock()
    {


        EnsurePixelSprite();

        Transform cellsParent = GetOrCreateChild(transform, CellsParentName);
        ClearChildren(cellsParent);

        Vector2Int[] offsets;
        if (shapeSet == ShapeSet.Custom && customOffsets != null && customOffsets.Length > 0)
            offsets = customOffsets;
        else if (shapeSet == ShapeSet.BlockBlast)
            offsets = GetOffsetsForBlockBlast(blockBlastType);
        else
            offsets = GetOffsetsForBlockBlast(blockBlastType);

        // choose color for this block
        _chosenColor = cellColor;
        if (randomizeColor)
        {
            if (colorPalette != null && colorPalette.Length > 0)
                _chosenColor = colorPalette[Random.Range(0, colorPalette.Length)];
            else
            {
                float h = Random.value;
                float s = Random.Range(0.6f, 1f);
                float v = Random.Range(0.6f, 1f);
                _chosenColor = Color.HSVToRGB(h, s, v);
            }
        }

        // create cells
        // Store initial position when generating the block
        initialPosition = transform.position;

        foreach (var off in offsets)
        {
            GameObject cell = new GameObject($"cell_{off.x}_{off.y}");
            cell.transform.SetParent(cellsParent, false);
            cell.transform.localPosition = new Vector3(off.x * cellSize, off.y * cellSize, 0f);

            var sr = cell.AddComponent<SpriteRenderer>();
            sr.sprite = s_pixelSprite;
            sr.color = _chosenColor;
            // ensure blocks render above the grid overlay (higher sorting order)
            sr.sortingOrder = 2;
            cell.transform.localScale = new Vector3(cellSize, cellSize, 1f);

            if (addCollider)
            {
                var bc = cell.AddComponent<BoxCollider2D>();
                // make collider slightly smaller so lines/gaps don't block clicks
                bc.size = new Vector2(0.9f * cellSize, 0.9f * cellSize);
                bc.offset = Vector2.zero;
            }
        }

        // draw internal separator lines for block cells (only edges without neighbor)
        Transform linesParent = GetOrCreateChild(transform, "_Block_Lines");
        ClearChildren(linesParent);
        // if requested, override style from GridGenerator
        if (matchGridStyle && gridReference != null)
        {
            internalLineColor = gridReference.lineColor;
            internalLineThickness = gridReference.lineThickness;
        }

        if (snapToGrid && gridReference != null)
        {
            // Snap this transform so cells will land on the grid centers.
            // Compute nearest grid cell world position for current transform position and snap.
            // We assume gridReference uses its GameObject position as center.
            float gCell = gridReference.cellSize;
            Vector3 world = transform.position;
            float snappedX = Mathf.Round(world.x / gCell) * gCell;
            float snappedY = Mathf.Round(world.y / gCell) * gCell;
            transform.position = new Vector3(snappedX, snappedY, transform.position.z);
        }

        // store offsets for snapping
        _lastOffsets = offsets;

        // Store initial spawn position
        initialPosition = transform.position;

        // optionally add or update a parent collider so block can be dragged
        if (addDragCollider)
        {
            // compute bounding box from offsets
            int minX = int.MaxValue, maxX = int.MinValue, minY = int.MaxValue, maxY = int.MinValue;
            foreach (var off in offsets)
            {
                if (off.x < minX) minX = off.x;
                if (off.x > maxX) maxX = off.x;
                if (off.y < minY) minY = off.y;
                if (off.y > maxY) maxY = off.y;
            }

            float totalWidth = (maxX - minX + 1) * cellSize;
            float totalHeight = (maxY - minY + 1) * cellSize;
            float centerX = (minX + maxX) * 0.5f * cellSize;
            float centerY = (minY + maxY) * 0.5f * cellSize;

            var bc = GetComponent<BoxCollider2D>();
            if (bc == null) bc = gameObject.AddComponent<BoxCollider2D>();
            bc.offset = new Vector2(centerX, centerY);
            bc.size = new Vector2(totalWidth, totalHeight);
        }

        if (drawInternalLines)
        {
            // Draw four lines around each cell so every block cell is individually outlined.
            // This will draw top/bottom/left/right for each cell (duplicates may overlap for adjacent cells).
            foreach (Vector2Int off in offsets)
            {
                Vector2 cellPos = new Vector2(off.x * cellSize, off.y * cellSize);
                // Tạo 4 line (top, bottom, left, right) cho mỗi ô vuông
                CreateLine(linesParent,
                    new Vector2(cellPos.x, cellPos.y + cellSize / 2f),
                    new Vector2(cellSize, internalLineThickness),
                    internalLineColor, 4); // top

                CreateLine(linesParent,
                    new Vector2(cellPos.x, cellPos.y - cellSize / 2f),
                    new Vector2(cellSize, internalLineThickness),
                    internalLineColor, 4); // bottom

                CreateLine(linesParent,
                    new Vector2(cellPos.x - cellSize / 2f, cellPos.y),
                    new Vector2(internalLineThickness, cellSize),
                    internalLineColor, 4); // left

                CreateLine(linesParent,
                    new Vector2(cellPos.x + cellSize / 2f, cellPos.y),
                    new Vector2(internalLineThickness, cellSize),
                    internalLineColor, 4); // right



            }
        }
    }

    // [ContextMenu("Rotate CW")]
    // public void RotateCW()
    // {
    //     // rotate child cells 90 degrees clockwise around origin
    //     Transform cellsParent = transform.Find(CellsParentName);
    //     if (cellsParent == null) return;

    //     for (int i = 0; i < cellsParent.childCount; i++)
    //     {
    //         var child = cellsParent.GetChild(i);
    //         Vector3 p = child.localPosition;
    //         // (x, y) -> ( -y, x ) for CW rotation around origin
    //         child.localPosition = new Vector3(-p.y, p.x, p.z);
    //     }
    // }


    private Vector2Int[] GetOffsetsForBlockBlast(BlockBlastType t)
    {
        switch (t)
        {
            case BlockBlastType.Single:
                return new[] { new Vector2Int(0, 0) };
            case BlockBlastType.Pair:
                return new[] { new Vector2Int(0, 0), new Vector2Int(1, 0) };
            case BlockBlastType.Square2:
                return new[] { new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(0, 1), new Vector2Int(1, 1) };
            case BlockBlastType.Line3:
                return new[] { new Vector2Int(-1, 0), new Vector2Int(0, 0), new Vector2Int(1, 0) };
            case BlockBlastType.SmallL:
                return new[] { new Vector2Int(0, 0), new Vector2Int(0, 1), new Vector2Int(1, 0) };
            case BlockBlastType.Plus:
                return new[] { new Vector2Int(0, 0), new Vector2Int(-1, 0), new Vector2Int(1, 0), new Vector2Int(0, -1), new Vector2Int(0, 1) };
            case BlockBlastType.BigSquare:
                return new[] {
                    new Vector2Int(-1, -1), new Vector2Int(0, -1), new Vector2Int(1, -1),
                    new Vector2Int(-1, 0), new Vector2Int(0, 0), new Vector2Int(1, 0),
                    new Vector2Int(-1, 1), new Vector2Int(0, 1), new Vector2Int(1, 1)
                };
            default:
                return new[] { new Vector2Int(0, 0) };
        }
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
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            var child = parent.GetChild(i).gameObject;
            if (Application.isPlaying)
                Destroy(child);
            else
                DestroyImmediate(child);
        }
    }

    /// <summary>
    /// Returns the block to its initial position
    /// </summary>
    public void ReturnToStart()
    {
        transform.position = initialPosition;
    }

    private bool IsPositionValidOnGrid(Vector3 position)
    {
        if (gridReference == null || _lastOffsets == null || _lastOffsets.Length == 0) return false;

        float cell = gridReference.cellSize;
        int cols = gridReference.cols;
        int rows = gridReference.rows;
        float totalW = cols * cell;
        float totalH = rows * cell;
        Vector2 gOrigin = new Vector2(-totalW / 2f + cell / 2f, -totalH / 2f + cell / 2f);
        Vector2 worldOrigin = (Vector2)gridReference.transform.position + gOrigin;

        // Calculate grid coordinates
        Vector2 localToOrigin = (Vector2)position - worldOrigin;
        int gx = Mathf.RoundToInt(localToOrigin.x / cell);
        int gy = Mathf.RoundToInt(localToOrigin.y / cell);

        // Check if any part of the block would be outside the grid
        foreach (var offset in _lastOffsets)
        {
            int cx = gx + offset.x;
            int cy = gy + offset.y;
            if (cx < 0 || cx >= cols || cy < 0 || cy >= rows)
            {
                return false;
            }
        }

        return true;
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

    // Creates a simple rectangular line sprite (uses the same 1x1 pixel sprite as cells).
    public void CreateLine(Transform parent, Vector2 localPos, Vector2 size, Color color, int sortingOrder = 0)
    {
        GameObject line = new GameObject("line");
        line.transform.SetParent(parent, false);
        line.transform.localPosition = new Vector3(localPos.x, localPos.y, 0f);

        var sr = line.AddComponent<SpriteRenderer>();
        sr.sprite = s_pixelSprite;
        sr.color = color;
        sr.sortingOrder = sortingOrder + 1; // ensure lines render above cells
        line.transform.localScale = new Vector3(size.x, size.y, 1f);
    }

    // --- Dragging support ---
    private Vector3 _dragOffsetWorld;
    private Camera _cam;
    private Vector3 _dragStartPosition; // Store position when starting drag

    private void OnMouseDown()
    {


        // Nếu đang ở spawner mà block đã di chuyển xa khỏi vị trí ban đầu -> phóng to
        if (isInSpawner && Vector3.Distance(transform.position, _dragStartPosition) > 0.1f)
        {
            transform.localScale = Vector3.one * gridScale;
            isInSpawner = false;
        }


        if (!draggable) return;
        if (!Application.isPlaying) return;
        _cam = Camera.main;
        if (_cam == null) return;

        // Store the starting position
        _dragStartPosition = transform.position;

        Vector3 mouseWorld = _cam.ScreenToWorldPoint(Input.mousePosition);
        mouseWorld.z = transform.position.z;
        _dragOffsetWorld = transform.position - mouseWorld;

        // Khi bắt đầu kéo: tăng sorting order và đổi sang layer DraggingBlock
        Transform cellsParent = transform.Find("_Block_Cells");
        Transform linesParent = transform.Find("_Block_Lines");

        // Cell dưới
        if (cellsParent != null)
        {
            foreach (var sr in cellsParent.GetComponentsInChildren<SpriteRenderer>())
            {
                _originalSortingOrder = sr.sortingOrder;
                _originalSortingLayer = sr.sortingLayerName;
                sr.sortingLayerName = DraggingSortingLayer;
                sr.sortingOrder = 500; // cell
            }
        }

        // Line trên
        if (linesParent != null)
        {
            foreach (var sr in linesParent.GetComponentsInChildren<SpriteRenderer>())
            {
                _originalSortingOrder = sr.sortingOrder;
                _originalSortingLayer = sr.sortingLayerName;
                sr.sortingLayerName = DraggingSortingLayer;
                sr.sortingOrder = 501; // line cao hơn cell
            }
        }



    }

    private void OnMouseDrag()
    {
        // Nếu đang ở spawner mà block đã di chuyển xa khỏi vị trí ban đầu -> phóng to
        if (isInSpawner && Vector3.Distance(transform.position, _dragStartPosition) > 0.1f)
        {
            transform.localScale = Vector3.one * gridScale;
            isInSpawner = false;
        }

        if (!draggable) return;
        if (!Application.isPlaying) return;
        if (_cam == null) _cam = Camera.main;
        if (_cam == null) return;
        Vector3 mouseWorld = _cam.ScreenToWorldPoint(Input.mousePosition);
        mouseWorld.z = transform.position.z;
        transform.position = mouseWorld + _dragOffsetWorld;
        // update preview according to where the block would snap if released now
        UpdatePreviewAtWorldPosition(transform.position);
    }

    private void OnMouseUp()
    {
        if (!draggable) return;
        if (!Application.isPlaying) return;

        // Nếu không có grid hoặc thả ra ngoài vùng grid → revert về spawner
        if (gridReference == null || !IsReleasedInsideGrid(transform.position))
        {
            transform.position = initialPosition;

            foreach (var sr in GetComponentsInChildren<SpriteRenderer>())
            {
                sr.sortingLayerName = _originalSortingLayer;
                sr.sortingOrder = _originalSortingOrder;
            }

            transform.localScale = Vector3.one * spawnerScale;
            isInSpawner = true;

            RefreshSortingOrder();
            ClearPreview();
            return;
        }

        // Thử snap vào grid
        bool snapSuccessful = SnapToGrid();

        if (snapSuccessful && CanPlaceOnGrid())
        {
            // ✅ Đặt thành công lên grid
            PlaceOnGrid();
            draggable = false;

            foreach (var sr in GetComponentsInChildren<SpriteRenderer>())
            {
                sr.sortingLayerName = _originalSortingLayer;
                sr.sortingOrder = _originalSortingOrder;
            }

            // giữ nguyên kích thước to
            transform.localScale = Vector3.one * gridScale;
            isInSpawner = false;
        }
        else
        {
            // ❌ Bất kỳ trường hợp nào snap thất bại (dù đang trong vùng grid)
            // → revert vị trí, thu nhỏ lại
            transform.position = initialPosition;

            foreach (var sr in GetComponentsInChildren<SpriteRenderer>())
            {
                sr.sortingLayerName = _originalSortingLayer;
                sr.sortingOrder = _originalSortingOrder;
            }

            // ✅ Thu nhỏ lại ngay lập tức
            transform.localScale = Vector3.one * spawnerScale;
            isInSpawner = true;
        }

        RefreshSortingOrder();
        ClearPreview();
    }


    public bool SnapToGrid()
    {
        if (gridReference == null) return false;

        // Store original position in case we need to revert
        Vector3 originalPosition = transform.position;

        // Need offsets information (which cells compose this block)
        if (_lastOffsets == null || _lastOffsets.Length == 0)
        {
            // fallback to simple snap if offsets unknown
            float gCell = gridReference.cellSize;
            float gTotalW = gridReference.cols * gCell;
            float gTotalH = gridReference.rows * gCell;
            Vector2 gOrigin = new Vector2(-gTotalW / 2f + gCell / 2f, -gTotalH / 2f + gCell / 2f);
            Vector2 worldOrigin = (Vector2)gridReference.transform.position + gOrigin;

            Vector2 localToOriginFallback = (Vector2)transform.position - worldOrigin;
            float snappedX = Mathf.Round(localToOriginFallback.x / gCell) * gCell + worldOrigin.x;
            float snappedY = Mathf.Round(localToOriginFallback.y / gCell) * gCell + worldOrigin.y;
            Vector3 newPosition = new Vector3(snappedX, snappedY, transform.position.z);

            // Only apply if the position would be valid
            if (IsPositionValidOnGrid(newPosition))
            {
                transform.position = newPosition;
                return true;
            }
            transform.position = originalPosition;
            return false;
        }

        float cell = gridReference.cellSize;
        int cols = gridReference.cols;
        int rows = gridReference.rows;
        float totalW = cols * cell;
        float totalH = rows * cell;
        Vector2 gOrigin2 = new Vector2(-totalW / 2f + cell / 2f, -totalH / 2f + cell / 2f);
        Vector2 worldOrigin2 = (Vector2)gridReference.transform.position + gOrigin2;

        // compute allowed grid index range for the block origin (gx,gy) so that
        // for every cell offset off, gx+off.x in [0, cols-1] and gy+off.y in [0, rows-1]
        int minOffX = int.MaxValue, maxOffX = int.MinValue, minOffY = int.MaxValue, maxOffY = int.MinValue;
        foreach (var o in _lastOffsets)
        {
            if (o.x < minOffX) minOffX = o.x;
            if (o.x > maxOffX) maxOffX = o.x;
            if (o.y < minOffY) minOffY = o.y;
            if (o.y > maxOffY) maxOffY = o.y;
        }

        int minGx = -minOffX;
        int maxGx = (cols - 1) - maxOffX;
        int minGy = -minOffY;
        int maxGy = (rows - 1) - maxOffY;

        // find nearest candidate gx,gy from current position
        Vector2 localToOrigin = (Vector2)transform.position - worldOrigin2;
        int curGx = Mathf.RoundToInt(localToOrigin.x / cell);
        int curGy = Mathf.RoundToInt(localToOrigin.y / cell);

        int snapGx = Mathf.Clamp(curGx, minGx, Mathf.Max(minGx, maxGx));
        int snapGy = Mathf.Clamp(curGy, minGy, Mathf.Max(minGy, maxGy));

        float finalX = worldOrigin2.x + snapGx * cell;
        float finalY = worldOrigin2.y + snapGy * cell;
        Vector3 snappedPosition = new Vector3(finalX, finalY, transform.position.z);

        // Only apply the new position if it's valid on the grid
        if (IsPositionValidOnGrid(snappedPosition))
        {
            transform.position = snappedPosition;
            return true;
        }

        // If not valid, keep original position
        transform.position = originalPosition;
        return false;
    }

    // --- Preview helpers ---
    private void UpdatePreviewAtWorldPosition(Vector3 worldPos)
    {
        if (gridReference == null || _lastOffsets == null || _lastOffsets.Length == 0) return;

        float cell = gridReference.cellSize;
        int cols = gridReference.cols;
        int rows = gridReference.rows;
        float totalW = cols * cell;
        float totalH = rows * cell;
        Vector2 gOrigin = new Vector2(-totalW / 2f + cell / 2f, -totalH / 2f + cell / 2f);
        Vector2 worldOrigin = (Vector2)gridReference.transform.position + gOrigin;

        // find nearest grid indices (may be outside bounds)
        Vector2 localToOrigin = (Vector2)worldPos - worldOrigin;
        int gx = Mathf.RoundToInt(localToOrigin.x / cell);
        int gy = Mathf.RoundToInt(localToOrigin.y / cell);

        // determine validity: all (gx+off.x, gy+off.y) must be inside [0..cols-1] / [0..rows-1]
        bool valid = true;
        foreach (var o in _lastOffsets)
        {
            int cx = gx + o.x;
            int cy = gy + o.y;
            if (cx < 0 || cx >= cols || cy < 0 || cy >= rows)
            {
                valid = false;
                break;
            }
        }

        // create preview parent under grid so positions align easily
        Transform previewParent = GetOrCreateChild(gridReference.transform, PreviewParentName);
        ClearChildren(previewParent);

        // Only show preview if position is valid
        if (!valid)
        {
            ClearChildren(previewParent);
            return;
        }

        foreach (var o in _lastOffsets)
        {
            int cx = gx + o.x;
            int cy = gy + o.y;
            Vector2 cellWorld = worldOrigin + new Vector2(cx * cell, cy * cell);
            GameObject p = new GameObject($"preview_{cx}_{cy}");
            p.transform.SetParent(previewParent, false);
            // local position relative to grid transform
            Vector2 local = cellWorld - (Vector2)gridReference.transform.position;
            p.transform.localPosition = new Vector3(local.x, local.y, 0f);
            var sr = p.AddComponent<SpriteRenderer>();
            sr.sprite = s_pixelSprite;
            sr.color = previewColor;
            sr.sortingOrder = 1; // place preview behind block cells (block cells use sortingOrder = 2)
            p.transform.localScale = new Vector3(cell, cell, 1f);
        }
    }

    private void ClearPreview()
    {
        if (gridReference == null) return;
        var parent = gridReference.transform.Find(PreviewParentName);
        if (parent == null) return;
        ClearChildren(parent);
    }
    // --- Block Blast specific placement logic ---

    /// <summary>
    /// Kiểm tra xem block hiện tại có thể đặt vào vị trí này không (dựa theo grid).
    /// </summary>
    public bool CanPlaceOnGrid()
    {
        if (gridReference == null || _lastOffsets == null) return false;

        float cell = gridReference.cellSize;
        int cols = gridReference.cols;
        int rows = gridReference.rows;
        float totalW = cols * cell;
        float totalH = rows * cell;
        Vector2 gOrigin = new Vector2(-totalW / 2f + cell / 2f, -totalH / 2f + cell / 2f);
        Vector2 worldOrigin = (Vector2)gridReference.transform.position + gOrigin;

        // Tính toạ độ grid của khối
        Vector2 localToOrigin = (Vector2)transform.position - worldOrigin;
        int gx = Mathf.RoundToInt(localToOrigin.x / cell);
        int gy = Mathf.RoundToInt(localToOrigin.y / cell);

        // Kiểm tra xem tất cả ô trong block có nằm trong grid và chưa bị chiếm không
        foreach (var offset in _lastOffsets)
        {
            int cx = gx + offset.x;
            int cy = gy + offset.y;

            if (cx < 0 || cx >= cols || cy < 0 || cy >= rows)
                return false;

            // Nếu GridGenerator đã có logic "occupied", thì kiểm tra tại đây
            if (gridReference is IGridOccupancy gridOcc && gridOcc.IsCellOccupied(cx, cy))
                return false;
        }

        return true;
    }
    public void PlaceOnGrid()
    {
        if (gridReference == null || _lastOffsets == null) return;
        if (!(gridReference is IGridOccupancy gridOcc)) return;

        float cell = gridReference.cellSize;
        int cols = gridReference.cols;
        int rows = gridReference.rows;
        float totalW = cols * cell;
        float totalH = rows * cell;
        Vector2 gOrigin = new Vector2(-totalW / 2f + cell / 2f, -totalH / 2f + cell / 2f);
        Vector2 worldOrigin = (Vector2)gridReference.transform.position + gOrigin;

        Vector2 localToOrigin = (Vector2)transform.position - worldOrigin;
        int gx = Mathf.RoundToInt(localToOrigin.x / cell);
        int gy = Mathf.RoundToInt(localToOrigin.y / cell);

        foreach (var offset in _lastOffsets)
        {
            int cx = gx + offset.x;
            int cy = gy + offset.y;

            // bounds safety check
            if (cx < 0 || cx >= cols || cy < 0 || cy >= rows)
            {
                // nếu bất kỳ ô nào nằm ngoài grid thì revert vị trí và abort
                transform.position = initialPosition;
                return;
            }

            // Nếu ô đã bị chiếm, revert và abort (an toàn)
            if (gridOcc.IsCellOccupied(cx, cy))
            {
                transform.position = initialPosition;
                return;
            }
        }

        // Nếu đến đây thì tất cả ô hợp lệ — đánh dấu occupancy
        foreach (var offset in _lastOffsets)
        {
            int cx = gx + offset.x;
            int cy = gy + offset.y;
            gridOcc.SetCellOccupied(cx, cy, true);
        }

        // Tùy: bạn có thể đặt lại vị trí block transform cho khớp cell trung tâm nếu cần,
        // nhưng hiện giữ nguyên transform để tránh các chỉnh động gây lệch.
        // Sau khi đặt block xong, kiểm tra hàng/cột đầy
        if (gridReference != null)
        {
            gridReference.CheckAndClearFullLines();
        }

        // khóa block để không kéo nữa
        draggable = false;
    }



    /// <summary>
    /// Trả về true nếu worldPos (vị trí thả) nằm *bên trong* vùng lưới (ít nhất 1 ô của origin sẽ nằm trong [0..cols-1]/[0..rows-1]).
    /// Chú ý: không kiểm tra occupancy ở đây — chỉ kiểm tra ranh giới lưới.
    /// </summary>
    private bool IsReleasedInsideGrid(Vector3 worldPos)
    {
        if (gridReference == null || _lastOffsets == null || _lastOffsets.Length == 0) return false;

        float cell = gridReference.cellSize;
        int cols = gridReference.cols;
        int rows = gridReference.rows;
        float totalW = cols * cell;
        float totalH = rows * cell;
        Vector2 gOrigin = new Vector2(-totalW / 2f + cell / 2f, -totalH / 2f + cell / 2f);
        Vector2 worldOrigin = (Vector2)gridReference.transform.position + gOrigin;

        // toạ độ tương đối so với origin
        Vector2 localToOrigin = (Vector2)worldPos - worldOrigin;

        // chỉ cần toạ độ origin (gx,gy) gần nhất — nếu origin này nằm trong lưới, ta coi release là "inside"
        int gx = Mathf.RoundToInt(localToOrigin.x / cell);
        int gy = Mathf.RoundToInt(localToOrigin.y / cell);

        return (gx >= 0 && gx < cols && gy >= 0 && gy < rows);
    }
    // --- Đảm bảo line luôn render trên cell ---
    public void RefreshSortingOrder()
    {
        // cells parent
        Transform cellsParent = transform.Find("_Block_Cells");
        if (cellsParent != null)
        {
            foreach (var sr in cellsParent.GetComponentsInChildren<SpriteRenderer>())
            {
                if (sr != null)
                    sr.sortingOrder = 2; // cell thấp hơn
            }
        }

        // lines parent
        Transform linesParent = transform.Find("_Block_Lines");
        if (linesParent != null)
        {
            foreach (var sr in linesParent.GetComponentsInChildren<SpriteRenderer>())
            {
                if (sr != null)
                    sr.sortingOrder = 4; // line cao hơn
            }
        }
    }
    public Vector2Int[] GetOffsets()
    {
        return _lastOffsets != null ? _lastOffsets : new Vector2Int[0];
    }
    public Sprite GetPixelSprite()
    {
        EnsurePixelSprite();
        return s_pixelSprite;
    }

    // private IEnumerator ScaleTo(Vector3 targetScale)
    // {
    //     while (Vector3.Distance(transform.localScale, targetScale) > 0.001f)
    //     {
    //         transform.localScale = Vector3.Lerp(
    //             transform.localScale,
    //             targetScale,
    //             Time.deltaTime * scaleTransitionSpeed
    //         );
    //         yield return null;
    //     }
    //     transform.localScale = targetScale;
    // }

}
