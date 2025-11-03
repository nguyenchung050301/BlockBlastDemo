using UnityEngine;
using System.Collections.Generic;

public class AutoHintController : MonoBehaviour
{
    [Header("References")]
    public GridGenerator grid;
    public List<TetrisBlock> spawnerBlocks = new List<TetrisBlock>();

    [Header("Settings")]
    [Tooltip("Thời gian (giây) không thao tác trước khi hiển thị gợi ý.")]
    public float idleTimeThreshold = 5f;

    private float idleTimer = 0f;
    private HintSystem hintSystem;
    private bool hintShown = false;

    [Header("Hint Display Settings")]
    [Tooltip("Màu hiển thị của gợi ý hint (alpha càng thấp càng trong suốt).")]
    [SerializeField] private Color hintColor;

    private void Start()
    {
        if (grid == null)
        {
            grid = FindObjectOfType<GridGenerator>();
        }

        hintSystem = new HintSystem(grid, hintColor);

        // Tự động cập nhật danh sách block trong spawner nếu chưa gán
        if (spawnerBlocks.Count == 0)
            spawnerBlocks.AddRange(FindObjectsOfType<TetrisBlock>());
    }

    private void Update()
    {
        // Nếu người chơi có tương tác (nhấn, kéo)
        if (Input.GetMouseButtonDown(0) || Input.GetMouseButton(0))
        {
            idleTimer = 0f; // reset timer
            if (hintShown)
            {
                hintSystem.ClearHint();
                hintShown = false;
            }
            return;
        }

        // Tăng timer khi không thao tác
        idleTimer += Time.deltaTime;

        if (!hintShown && idleTimer >= idleTimeThreshold)
        {
            ShowHint();
            hintShown = true;
        }
    }

    private void ShowHint()
    {
        // Cập nhật danh sách block khả dụng hiện tại
        spawnerBlocks.Clear();
        spawnerBlocks.AddRange(FindObjectsOfType<TetrisBlock>());

        // Gọi AI hint
        hintSystem.ShowHint(spawnerBlocks);

        Debug.Log("[AutoHintController] Hiển thị gợi ý tự động sau " + idleTimeThreshold + "s không thao tác.");
    }
}
