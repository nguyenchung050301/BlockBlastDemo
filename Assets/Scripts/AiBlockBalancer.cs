using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// AI quản lý độ khó của block spawn dựa trên hành vi người chơi
/// </summary>
public class AIBlockBalancer : MonoBehaviour
{
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

        // Nếu có combo → tăng streak
        if (wasCombo)
        {
            comboStreak++;
            Debug.Log($"[AI] Combo streak: {comboStreak}");
        }
        else
        {
            // Nếu không combo → giảm dần độ khó
            comboStreak = Mathf.Max(comboStreak - 1, 0);
        }

        // Cập nhật độ khó dựa trên combo
        BlockDifficulty difficulty = CalculateDifficulty();

        // Ghi lại trạng thái vào spawner
        spawner.currentDifficulty = difficulty;

        // Log ra cho debug
        Debug.Log($"[AI] Current difficulty = {difficulty}");

        // Khi người chơi vừa hoàn thành combo, spawn block “thưởng”
        if (wasCombo)
        {
            Debug.Log($"[AI] Combo streak {comboStreak}! Next spawn difficulty: {difficulty}");
            spawner.SpawnBlockWithDifficulty(difficulty);
        }
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
}
