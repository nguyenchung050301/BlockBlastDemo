using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// AI quản lý độ khó của block spawn dựa trên hành vi người chơi
/// </summary>
public class AIBlockBalancer : MonoBehaviour
{
    // === Thống kê hành vi người chơi ===
    private float lastPlacementTime = 0f;          // thời gian đặt block gần nhất
    private float totalPlacementTime = 0f;         // tổng thời gian giữa các lượt
    private float averagePlacementTime = 0f;       // thời gian trung bình giữa các lượt
    private int totalBlocksPlaced = 0;             // tổng số block đã đặt
    private int totalLinesCleared = 0;             // tổng số hàng/cột đã clear
    private float performanceScore = 0f;

    private BlockDifficulty currentDifficulty = BlockDifficulty.Easy;

    private static readonly Dictionary<TetrisBlock.BlockBlastType, BlockDifficulty> shapeDifficulty =
    new Dictionary<TetrisBlock.BlockBlastType, BlockDifficulty>
{
    // Easy shapes
    { TetrisBlock.BlockBlastType.Single, BlockDifficulty.Easy },
    //{ TetrisBlock.BlockBlastType.Pair, BlockDifficulty.Easy },
    { TetrisBlock.BlockBlastType.Square2, BlockDifficulty.Easy },
    //{ TetrisBlock.BlockBlastType.Line3, BlockDifficulty.Easy },
    { TetrisBlock.BlockBlastType.Line3_Vertical, BlockDifficulty.Easy },
   // { TetrisBlock.BlockBlastType.Line4, BlockDifficulty.Easy },
    { TetrisBlock.BlockBlastType.Line4_Vertical, BlockDifficulty.Easy },

    // Medium shapes
    { TetrisBlock.BlockBlastType.SmallL, BlockDifficulty.Medium },
    { TetrisBlock.BlockBlastType.SmallL_R90, BlockDifficulty.Medium },
    { TetrisBlock.BlockBlastType.SmallL_R180, BlockDifficulty.Medium },
    { TetrisBlock.BlockBlastType.SmallL_R270, BlockDifficulty.Medium },
    { TetrisBlock.BlockBlastType.Plus, BlockDifficulty.Medium },
    { TetrisBlock.BlockBlastType.TShape, BlockDifficulty.Medium },
    { TetrisBlock.BlockBlastType.TShape_R90, BlockDifficulty.Medium },
    { TetrisBlock.BlockBlastType.TShape_R180, BlockDifficulty.Medium },
    { TetrisBlock.BlockBlastType.TShape_R270, BlockDifficulty.Medium },
    { TetrisBlock.BlockBlastType.ZShape, BlockDifficulty.Medium },
    { TetrisBlock.BlockBlastType.ZShape_Mirror, BlockDifficulty.Medium },
    { TetrisBlock.BlockBlastType.SShape, BlockDifficulty.Medium },
    { TetrisBlock.BlockBlastType.SShape_Mirror, BlockDifficulty.Medium },

    // Hard shapes
    { TetrisBlock.BlockBlastType.BigSquare, BlockDifficulty.Hard },
    { TetrisBlock.BlockBlastType.TallL, BlockDifficulty.Hard },
    { TetrisBlock.BlockBlastType.Corner3x3, BlockDifficulty.Hard },
    { TetrisBlock.BlockBlastType.HollowSquare, BlockDifficulty.Hard },
    { TetrisBlock.BlockBlastType.CrossX, BlockDifficulty.Hard },
    { TetrisBlock.BlockBlastType.UShape, BlockDifficulty.Hard },
};

    private int comboStreak = 0;
    private BlockSpawner spawner;
    private GridGenerator grid;

    private void Start()
    {
        spawner = FindObjectOfType<BlockSpawner>();
        grid = FindObjectOfType<GridGenerator>();
    }

    /// <summary>
    /// Gọi khi người chơi đặt xong 1 block (kể cả có combo hay không)
    /// </summary>
    public void OnBlockPlaced(bool wasCombo)
    {
        if (spawner == null) return;

        // === Ghi nhận thời gian và thống kê ===
        float now = Time.time;

        if (lastPlacementTime > 0f)
        {
            float delta = now - lastPlacementTime;
            totalPlacementTime += delta;
            averagePlacementTime = totalPlacementTime / (totalBlocksPlaced + 1);
        }

        lastPlacementTime = now;
        totalBlocksPlaced++;

        if (wasCombo)
            totalLinesCleared++;

        Debug.Log($"[AI] Block #{totalBlocksPlaced} | LinesCleared={totalLinesCleared} | AvgTime={averagePlacementTime:F2}s");

        // === Bước 2: Tính điểm hiệu suất người chơi ===
        float clearRate = (float)totalLinesCleared / Mathf.Max(totalBlocksPlaced, 1);
        // Công thức tính điểm
        performanceScore = (clearRate * 120f) + (comboStreak * 3f) - (averagePlacementTime * 1.8f);
        performanceScore = Mathf.Clamp(performanceScore, 0f, 100f);
        Debug.Log($"[AI] PerformanceScore = {performanceScore:F1}");

        // === Bước 3 + 4: Điều chỉnh độ khó dựa trên performanceScore có vùng đệm ===
        var previousDifficulty = currentDifficulty;

        // Vùng ngưỡng độ khó
        float mediumUp = 28f;
        float mediumDown = 22f;
        float hardUp = 50f;
        float hardDown = 40f;

        switch (currentDifficulty)
        {
            case BlockDifficulty.Easy:
                if (performanceScore >= mediumUp)
                    currentDifficulty = BlockDifficulty.Medium;
                break;

            case BlockDifficulty.Medium:
                if (performanceScore >= hardUp)
                    currentDifficulty = BlockDifficulty.Hard;
                else if (performanceScore < mediumDown)
                    currentDifficulty = BlockDifficulty.Easy;
                break;

            case BlockDifficulty.Hard:
                if (performanceScore < hardDown)
                    currentDifficulty = BlockDifficulty.Medium;
                break;
        }

        // Nếu có thay đổi độ khó thì thông báo và cập nhật cho Spawner
        if (currentDifficulty != previousDifficulty)
        {
            Debug.Log($"[AI] Difficulty changed: {previousDifficulty} → {currentDifficulty}");
            if (spawner != null)
                spawner.currentDifficulty = currentDifficulty;
        }

        // === Bước 5: Theo dõi combo streak (phụ) ===
        if (wasCombo)
        {
            comboStreak++;
            Debug.Log($"[AI] Combo streak: {comboStreak}");
        }
        else
        {
            comboStreak = Mathf.Max(comboStreak - 1, 0);
        }

        // === Log cuối cùng ===
        Debug.Log($"[AI] Current difficulty = {currentDifficulty}");

        // LƯU Ý: KHÔNG gọi SpawnBlockWithDifficulty ở đây — spawner quản lý spawn theo flow gốc
    }

    /// <summary>
    /// Tính toán độ khó dựa trên combo streak hiện tại
    /// </summary>
    private BlockDifficulty CalculateDifficulty()
    {
        if (comboStreak <= 1)
            return BlockDifficulty.Easy;
        else if (comboStreak <= 3)
            return BlockDifficulty.Medium;
        else
            return BlockDifficulty.Hard;
    }
    public BlockDifficulty GetCurrentDifficulty()
    {
        return currentDifficulty;
    }
    public float GetPerformanceScore()
    {
        return performanceScore;
    }
}
