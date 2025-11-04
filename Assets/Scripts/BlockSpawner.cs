
using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum BlockDifficulty
{
    Easy,
    Medium,
    Hard
}


/// <summary>
/// Simple BlockSpawner: creates TetrisBlock GameObjects below a GridGenerator.
/// Attach to an empty GameObject. Set the `gridReference` to your GridGenerator.
/// - If no GridGenerator is assigned, spawns at the spawner object's position.
/// - By default it creates multiple blocks in a row with configurable spacing.
/// </summary>
[DisallowMultipleComponent]
public class BlockSpawner : MonoBehaviour
{
    [Header("AI Difficulty Balancing")]
    public BlockDifficulty currentDifficulty = BlockDifficulty.Easy;
    // Danh sách shape chia theo độ khó
    private static readonly List<TetrisBlock.BlockBlastType> EasyShapes = new()
{
    TetrisBlock.BlockBlastType.Single,
    TetrisBlock.BlockBlastType.Pair_Horizontal,
    TetrisBlock.BlockBlastType.Pair_Vertical,
    TetrisBlock.BlockBlastType.Square2,
    TetrisBlock.BlockBlastType.Line3_Horizontal,
    TetrisBlock.BlockBlastType.Line3_Vertical,
    TetrisBlock.BlockBlastType.SmallL,
    TetrisBlock.BlockBlastType.SmallL_R90,
    TetrisBlock.BlockBlastType.SmallL_R180,
    TetrisBlock.BlockBlastType.SmallL_R270
};


    private static readonly List<TetrisBlock.BlockBlastType> MediumShapes = new()
{
    TetrisBlock.BlockBlastType.Line4_Horizontal,
    TetrisBlock.BlockBlastType.Line4_Vertical,
    TetrisBlock.BlockBlastType.TShape,
    TetrisBlock.BlockBlastType.TShape_R90,
    TetrisBlock.BlockBlastType.TShape_R180,
    TetrisBlock.BlockBlastType.TShape_R270,
    TetrisBlock.BlockBlastType.ZShape,
    TetrisBlock.BlockBlastType.ZShape_Mirror,
    TetrisBlock.BlockBlastType.SShape,
    TetrisBlock.BlockBlastType.SShape_Mirror,
    TetrisBlock.BlockBlastType.Corner3x3,
    TetrisBlock.BlockBlastType.Plus,
    TetrisBlock.BlockBlastType.TallL,
    TetrisBlock.BlockBlastType.Rect2x3_H,
    TetrisBlock.BlockBlastType.Rect2x3_V
};



    private static readonly List<TetrisBlock.BlockBlastType> HardShapes = new()
{
    TetrisBlock.BlockBlastType.BigSquare,
    TetrisBlock.BlockBlastType.HollowSquare,
    TetrisBlock.BlockBlastType.CrossX,
    TetrisBlock.BlockBlastType.UShape,
    TetrisBlock.BlockBlastType.UShape_R90,
    TetrisBlock.BlockBlastType.L5Shape,
    TetrisBlock.BlockBlastType.L5Shape_R90,
    TetrisBlock.BlockBlastType.Corner5x5,
    TetrisBlock.BlockBlastType.Zigzag5,
    TetrisBlock.BlockBlastType.Snake,
    TetrisBlock.BlockBlastType.Spiral
};



    [Header("Grid Reference")]
    [Tooltip("Reference to the GridGenerator in the scene")]
    public GridGenerator gridReference;

    [Header("Multi-Spawn Settings")]
    [Tooltip("Number of blocks to spawn in a row")]
    [Range(1, 3)]
    public int blocksPerSpawn = 3;
    [Tooltip("Horizontal spacing between spawned blocks")]
    public float horizontalSpacing = 2f;

    [Header("Spawn Options")]
    [Tooltip("If true the spawner will automatically spawn blocks on Start")]
    public bool spawnOnStart = true;
    [Tooltip("If > 0 the spawner will repeatedly spawn blocks every interval seconds")]
    public float spawnInterval = 0f;
    [Tooltip("If true the spawner will use its own Transform.position (+ spawnOffset) instead of computing a position based on GridGenerator")]
    public bool useSpawnerPosition = false;
    [Tooltip("Local offset applied when using the spawner's position as the spawn point")]
    public Vector3 spawnOffset = Vector3.zero;

    [Header("Block Settings")]
    [Tooltip("If true a random BlockBlastType will be chosen for each spawned block")]
    public bool randomizeType = true;
    public TetrisBlock.BlockBlastType defaultType = TetrisBlock.BlockBlastType.Square2;
    [Tooltip("Weights for each BlockBlastType when randomizing (leave empty for equal weights)")]
    public float[] shapeWeights;
    [Tooltip("Optional color palette for spawned blocks (leave empty for HSV random colors)")]
    public Color[] colorPalette;
    [Tooltip("If true the spawned block will be generated (cells created) immediately")]
    public bool generateImmediately = true;
    [Tooltip("If true spawned blocks will snap to the grid if gridReference is set and snapToGrid is enabled on the block")]
    public bool snapSpawnedToGrid = false;

    private Coroutine _spawnCoroutine;

    // --- Block Blast slot management ---
    [Header("Block Blast Slot System")]
    [Tooltip("Số lượng block được hiển thị cùng lúc (thường = 3)")]
    public int slotCount = 3;

    // Danh sách giữ các block hiện đang chờ trong spawner
    private TetrisBlock[] currentBlocks;

    // Khoảng cách giữa các slot hiển thị
    public float slotSpacing = 2.5f;

    // Vị trí gốc của slot đầu tiên (tính từ transform của spawner)
    public Vector3 slotBaseOffset = new Vector3(0, 0, 0);
    public HintSystem hintSystem;
    private void Start()
    {
        hintSystem = new HintSystem(gridReference);
        // Try to find grid reference if not set
        if (gridReference == null)
            gridReference = FindObjectOfType<GridGenerator>();

        // if (spawnOnStart)
        // {
        //     SpawnBlock();
        // }

        if (spawnOnStart)
        {
            InitializeSlots();
            SpawnNewSet();
        }

        if (spawnInterval > 0f)
        {
            _spawnCoroutine = StartCoroutine(SpawnLoop());
        }
    }

    private IEnumerator SpawnLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(spawnInterval);
            SpawnBlock();
        }
    }

    /// <summary>
    /// Spawn multiple TetrisBlock GameObjects in a horizontal row at the computed spawn position.
    /// </summary>
    [ContextMenu("Spawn Blocks")]
    public void SpawnBlock()
    {
        if (blocksPerSpawn <= 0) return;

        Vector3 baseSpawnPos = ComputeSpawnPosition();

        // Calculate start position for the row of blocks
        // Center the blocks horizontally
        float totalWidth = (blocksPerSpawn - 1) * horizontalSpacing;
        float startX = baseSpawnPos.x - totalWidth / 2f;

        // Create a parent object for this group of blocks
        GameObject groupParent = new GameObject($"BlockGroup_{Time.frameCount}");
        groupParent.transform.position = baseSpawnPos;

        // Spawn blocks in a row
        for (int blockIndex = 0; blockIndex < blocksPerSpawn; blockIndex++)
        {
            Vector3 spawnPos = new Vector3(startX + (blockIndex * horizontalSpacing), baseSpawnPos.y, baseSpawnPos.z);

            GameObject go = new GameObject($"TetrisBlock_Spawned_{blockIndex}");
            go.transform.SetParent(groupParent.transform);
            go.transform.position = spawnPos;
            var tb = go.AddComponent<TetrisBlock>();

            // Configure block's grid integration settings
            tb.matchGridStyle = true;         // Match grid's visual style by default
            tb.snapToGrid = snapSpawnedToGrid; // Use spawner's snap setting

            // Configure the block
            tb.shapeSet = TetrisBlock.ShapeSet.BlockBlast;

            // Handle shape selection with weights
            if (randomizeType)
            {
                if (shapeWeights != null && shapeWeights.Length > 0)
                {
                    // Use weighted random selection
                    float totalWeight = 0f;
                    foreach (float w in shapeWeights)
                        totalWeight += w;

                    float rand = UnityEngine.Random.value * totalWeight;
                    float cumulative = 0f;

                    bool found = false;
                    for (int shapeIndex = 0; shapeIndex < shapeWeights.Length && shapeIndex < System.Enum.GetValues(typeof(TetrisBlock.BlockBlastType)).Length; shapeIndex++)
                    {
                        cumulative += shapeWeights[shapeIndex];
                        if (rand <= cumulative)
                        {
                            tb.blockBlastType = (TetrisBlock.BlockBlastType)shapeIndex;
                            found = true;
                            break;
                        }
                    }

                    if (!found) // Fallback if weights are invalid
                        tb.blockBlastType = defaultType;
                }
                else
                {
                    // Equal probability for all types
                    tb.blockBlastType = (TetrisBlock.BlockBlastType)UnityEngine.Random.Range(0, System.Enum.GetValues(typeof(TetrisBlock.BlockBlastType)).Length);
                }
            }
            else
            {
                tb.blockBlastType = defaultType;
            }

            // Configure color
            tb.randomizeColor = true; // Keep TetrisBlock's built-in color system
            if (colorPalette != null && colorPalette.Length > 0)
            {
                tb.colorPalette = colorPalette; // Use our configured palette
            }

            tb.generateOnStart = false; // we'll call GenerateBlock ourselves when ready
            tb.snapToGrid = snapSpawnedToGrid;
            tb.gridReference = this.gridReference; // Pass our grid reference to the block

            if (generateImmediately)
            {
                tb.GenerateBlock();
            }
        }
    }

    /// <summary>
    /// Khởi tạo danh sách slot
    /// </summary>
    private void InitializeSlots()
    {
        currentBlocks = new TetrisBlock[slotCount];
    }

    /// <summary>
    /// Tạo ra 3 block mới khi tất cả slot trống
    /// </summary>
    private void SpawnNewSet()
    {

        // 🔹 Hỏi AI độ khó hiện tại
        var ai = FindObjectOfType<AIBlockBalancer>();
        if (ai != null)
            currentDifficulty = ai.GetCurrentDifficulty();

        // Nếu mảng chưa được khởi tạo
        if (currentBlocks == null || currentBlocks.Length != slotCount)
            InitializeSlots();

        for (int i = 0; i < slotCount; i++)
        {
            // Spawn mỗi block tại vị trí slot riêng biệt
            // Căn giữa 3 slot quanh spawner
            float totalWidth = (slotCount - 1) * slotSpacing;
            Vector3 startPos = transform.position + slotBaseOffset - new Vector3(totalWidth / 2f, 0, 0);
            Vector3 slotPos = startPos + new Vector3(i * slotSpacing, 0, 0);

            GameObject go = new GameObject($"TetrisBlock_Slot_{i}");
            go.transform.position = slotPos;
            go.transform.SetParent(transform);

            var tb = go.AddComponent<TetrisBlock>();
            tb.gridReference = gridReference;
            tb.generateOnStart = false;
            tb.shapeSet = TetrisBlock.ShapeSet.BlockBlast;
            // tb.blockBlastType = randomizeType
            //     ? (TetrisBlock.BlockBlastType)UnityEngine.Random.Range(0, System.Enum.GetValues(typeof(TetrisBlock.BlockBlastType)).Length)
            //     : defaultType;
            if (randomizeType)
            {
                tb.blockBlastType = GetShapeAdaptive(FindObjectOfType<AIBlockBalancer>()?.GetPerformanceScore() ?? 0f);


            }
            else
            {
                tb.blockBlastType = defaultType;
            }

            tb.matchGridStyle = true;
            tb.snapToGrid = true;
            tb.generateOnStart = false;
            tb.randomizeColor = true;

            if (colorPalette != null && colorPalette.Length > 0)
                tb.colorPalette = colorPalette;

            tb.GenerateBlock();

            // Gán callback khi block được đặt thành công
            var listener = go.AddComponent<BlockPlacedListener>();
            listener.onPlaced = OnBlockPlacedFromSlot;
            listener.spawnerIndex = i;

            currentBlocks[i] = tb;
        }
    }
    /// <summary>
    /// Gọi khi một block trong slot được đặt lên grid thành công
    /// </summary>
    private void OnBlockPlacedFromSlot(int index)
    {
        if (currentBlocks == null || index < 0 || index >= currentBlocks.Length) return;
        currentBlocks[index] = null;

        // Kiểm tra nếu tất cả slot đều trống => spawn set mới
        bool allUsed = true;
        foreach (var b in currentBlocks)
        {
            if (b != null)
            {
                allUsed = false;
                break;
            }
        }

        if (allUsed)
        {
            // Spawn 3 block mới
            SpawnNewSet();
        }
    }


    /// <summary>
    /// Compute a world position to spawn the block below the grid.
    /// If gridReference is null, returns this spawner's transform position.
    /// </summary>
    private Vector3 ComputeSpawnPosition()
    {
        // If user wants to use the spawner's position explicitly, return that (with optional offset)
        if (useSpawnerPosition)
            return transform.position + spawnOffset;

        // If no grid reference is provided, try to find one
        if (gridReference == null)
        {
            gridReference = FindObjectOfType<GridGenerator>();
            if (gridReference == null)
                return transform.position + spawnOffset;
        }

        // replicate the grid origin math from GridGenerator
        float cell = gridReference.cellSize;
        int cols = gridReference.cols;
        int rows = gridReference.rows;
        float totalW = cols * cell;
        float totalH = rows * cell;
        Vector2 gOrigin = new Vector2(-totalW / 2f + cell / 2f, -totalH / 2f + cell / 2f);
        Vector2 worldOrigin = (Vector2)gridReference.transform.position + gOrigin;

        // choose center column
        int centerCol = cols / 2; // integer division picks the middle-left for even cols
        float spawnX = worldOrigin.x + centerCol * cell;
        // place one full cell below the bottom row so the block appears under the grid
        float spawnY = worldOrigin.y - cell;

        return new Vector3(spawnX, spawnY, 0f);
    }
    public void OnHintButtonPressed()
    {
        List<TetrisBlock> blocks = new List<TetrisBlock>(FindObjectsOfType<TetrisBlock>());
        hintSystem.ShowHint(blocks);
    }

    /// <summary>
    /// Sinh block theo độ khó (được AI điều chỉnh)
    /// </summary>
    public void SpawnBlockWithDifficulty(BlockDifficulty difficulty)
    {
        // 🔒 Chỉ spawn nếu tất cả slot đã trống
        if (currentBlocks != null)
        {
            bool hasActive = false;
            foreach (var b in currentBlocks)
            {
                if (b != null)
                {
                    hasActive = true;
                    break;
                }
            }

            if (hasActive)
            {
                Debug.Log("[Spawner] Still have active blocks → Skip spawning.");
                return;
            }
        }

        List<TetrisBlock.BlockBlastType> shapePool = EasyShapes;

        switch (difficulty)
        {
            case BlockDifficulty.Medium:
                shapePool = MediumShapes;
                break;
            case BlockDifficulty.Hard:
                shapePool = HardShapes;
                break;
        }

        // Random 3 block mới
        for (int i = 0; i < currentBlocks.Length; i++)
        {
            var chosenShape = shapePool[UnityEngine.Random.Range(0, shapePool.Count)];
            Vector3 pos = transform.position + new Vector3(i * slotSpacing, 0, 0);

            GameObject go = new GameObject($"AI_TetrisBlock_{difficulty}_{i}");
            go.transform.position = pos;
            go.transform.SetParent(transform);

            var tb = go.AddComponent<TetrisBlock>();
            tb.gridReference = gridReference;
            tb.shapeSet = TetrisBlock.ShapeSet.BlockBlast;
            tb.blockBlastType = chosenShape;
            tb.generateOnStart = false;
            tb.randomizeColor = true;
            tb.GenerateBlock();

            currentBlocks[i] = tb;
        }

        Debug.Log($"[Spawner] Spawned 3 new blocks with difficulty: {difficulty}");
    }

    public TetrisBlock.BlockBlastType GetShapeAdaptive(float performanceScore)
    {
        // Danh sách shape từng độ khó
        List<TetrisBlock.BlockBlastType> easyShapes = EasyShapes;
        List<TetrisBlock.BlockBlastType> mediumShapes = MediumShapes;
        List<TetrisBlock.BlockBlastType> hardShapes = HardShapes;

        // 🔸 Ngưỡng bảo vệ: mỗi loại luôn có ít nhất 10% xác suất
        const float minWeight = 0.1f;

        // 🔸 Tính trọng số thô dựa vào performance
        float easyWeight = 1f - (performanceScore / 100f);    // càng giỏi → ít Easy
        float hardWeight = (performanceScore / 100f);          // càng giỏi → nhiều Hard
        float mediumWeight = 1f - Mathf.Abs(0.5f - (performanceScore / 100f)) * 2f;

        // 🔸 Đảm bảo mỗi loại >= minWeight
        // easyWeight = Mathf.Max(easyWeight, minWeight);
        // mediumWeight = Mathf.Max(mediumWeight, minWeight);
        // hardWeight = Mathf.Max(hardWeight, minWeight);


        easyWeight = Mathf.Lerp(0.7f, 0.4f, performanceScore / 100f);
        mediumWeight = Mathf.Lerp(0.25f, 0.4f, performanceScore / 100f);
        hardWeight = Mathf.Lerp(0.05f, 0.2f, performanceScore / 100f);
        // 🔸 Chuẩn hoá lại tổng trọng số
        float total = easyWeight + mediumWeight + hardWeight;
        easyWeight /= total;
        mediumWeight /= total;
        hardWeight /= total;

        // (Tuỳ chọn) Debug: hiển thị tỷ lệ hiện tại
        Debug.Log($"[AI] ShapeDistribution = E:{easyWeight * 100f:F0}% / M:{mediumWeight * 100f:F0}% / H:{hardWeight * 100f:F0}% (score={performanceScore:F1})");

        // 🔸 Chọn shape theo xác suất
        float r = UnityEngine.Random.value;
        if (r < easyWeight)
            return easyShapes[UnityEngine.Random.Range(0, easyShapes.Count)];
        else if (r < easyWeight + mediumWeight)
            return mediumShapes[UnityEngine.Random.Range(0, mediumShapes.Count)];
        else
            return hardShapes[UnityEngine.Random.Range(0, hardShapes.Count)];
    }




    private void OnDisable()
    {
        if (_spawnCoroutine != null)
            StopCoroutine(_spawnCoroutine);
    }
}
