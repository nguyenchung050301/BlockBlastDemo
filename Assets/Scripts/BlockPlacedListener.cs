using UnityEngine;
using System;

public class BlockPlacedListener : MonoBehaviour
{
    public Action<int> onPlaced;
    public int spawnerIndex;

    private TetrisBlock block;

    private void Start()
    {
        block = GetComponent<TetrisBlock>();
    }

    private void Update()
    {
        if (block == null || block.draggable) return;

        // Khi block đã được đặt (draggable = false), ta gọi callback và xóa listener
        onPlaced?.Invoke(spawnerIndex);
        Destroy(this);
    }
}
