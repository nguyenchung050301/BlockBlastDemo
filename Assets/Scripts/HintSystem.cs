using UnityEngine;
using System.Collections.Generic;

public class HintSystem
{
    private GridGenerator grid;
    private const string HintParentName = "_Hint_Preview";
    private Color hintColor;

    public HintSystem(GridGenerator gridRef, Color color)
    {
        grid = gridRef;
        hintColor = color;
    }
    public HintSystem(GridGenerator gridRef)
    {
        grid = gridRef;
        hintColor = new Color(0.5f, 0.5f, 0.5f); // màu mặc định
    }

    /// <summary>
    /// Quét toàn bộ grid để tìm vị trí tốt nhất cho một trong các block.
    /// </summary>
    public void ShowHint(List<TetrisBlock> availableBlocks)
    {
        if (grid == null || availableBlocks == null || availableBlocks.Count == 0)
        {
            Debug.LogWarning("HintSystem: grid hoặc block list chưa được gán.");
            return;
        }

        // Xoá hint cũ
        ClearHint();

        int bestScore = int.MinValue;
        TetrisBlock bestBlock = null;
        Vector2Int bestPos = Vector2Int.zero;

        // Duyệt qua từng block và vị trí có thể
        foreach (var block in availableBlocks)
        {
            if (block == null || block.gridReference == null || !block.draggable)
                continue;

            foreach (var pos in GetAllValidPositions(block))
            {
                int score = EvaluatePlacement(block, pos);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestBlock = block;
                    bestPos = pos;
                }
            }
        }

        if (bestBlock != null)
        {
            DrawHint(bestBlock, bestPos);
            Debug.Log($"[HintSystem] Gợi ý: Block '{bestBlock.name}' nên đặt ở ({bestPos.x}, {bestPos.y}) - Score {bestScore}");
        }
        else
        {
            Debug.Log("[HintSystem] Không tìm thấy vị trí hợp lệ cho bất kỳ block nào.");
        }
    }

    private int EvaluatePlacement(TetrisBlock block, Vector2Int pos)
    {
        int score = 0;
        bool[,] tempGrid = grid.CloneOccupancy();

        foreach (var off in block.GetOffsets())
        {
            int cx = pos.x + off.x;
            int cy = pos.y + off.y;
            if (cx < 0 || cy < 0 || cx >= grid.cols || cy >= grid.rows)
                return int.MinValue;
            if (tempGrid[cx, cy])
                return int.MinValue;

            tempGrid[cx, cy] = true;
        }

        int fullLines = grid.CountFullLines(tempGrid);
        int holes = grid.CountEmptyHoles(tempGrid);

        score += fullLines * 100;
        score -= holes * 10;

        return score;
    }

    private List<Vector2Int> GetAllValidPositions(TetrisBlock block)
    {
        List<Vector2Int> result = new List<Vector2Int>();

        for (int gx = 0; gx < grid.cols; gx++)
        {
            for (int gy = 0; gy < grid.rows; gy++)
            {
                if (CanPlace(block, gx, gy))
                    result.Add(new Vector2Int(gx, gy));
            }
        }

        return result;
    }

    private bool CanPlace(TetrisBlock block, int gx, int gy)
    {
        foreach (var off in block.GetOffsets())
        {
            int cx = gx + off.x;
            int cy = gy + off.y;
            if (cx < 0 || cy < 0 || cx >= grid.cols || cy >= grid.rows)
                return false;
            if (grid.IsCellOccupied(cx, cy))
                return false;
        }
        return true;
    }

    private void DrawHint(TetrisBlock block, Vector2Int pos)
    {
        Transform hintParent = GetOrCreateChild(grid.transform, HintParentName);
        ClearChildren(hintParent);

        float cell = grid.cellSize;
        float totalW = grid.cols * cell;
        float totalH = grid.rows * cell;
        Vector2 gOrigin = new Vector2(-totalW / 2f + cell / 2f, -totalH / 2f + cell / 2f);
        Vector2 worldOrigin = (Vector2)grid.transform.position + gOrigin;

        foreach (var off in block.GetOffsets())
        {
            int cx = pos.x + off.x;
            int cy = pos.y + off.y;
            Vector2 worldPos = worldOrigin + new Vector2(cx * cell, cy * cell);

            GameObject hintCell = new GameObject($"hint_{cx}_{cy}");
            hintCell.transform.SetParent(hintParent, false);
            hintCell.transform.localPosition = new Vector3(worldPos.x - grid.transform.position.x, worldPos.y - grid.transform.position.y, 0f);

            var sr = hintCell.AddComponent<SpriteRenderer>();
            sr.sprite = block.GetPixelSprite();
            sr.color = hintColor;
            sr.sortingOrder = 10;
            hintCell.transform.localScale = new Vector3(cell, cell, 1f);
        }
    }

    public void ClearHint()
    {
        var parent = grid.transform.Find(HintParentName);
        if (parent != null)
            ClearChildren(parent);
    }

    // --- Helpers ---
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
                Object.Destroy(child);
            else
                Object.DestroyImmediate(child);
        }
    }
}
