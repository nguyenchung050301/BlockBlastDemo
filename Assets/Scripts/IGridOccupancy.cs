using UnityEngine;

public interface IGridOccupancy
{
    bool IsCellOccupied(int x, int y);
    void SetCellOccupied(int x, int y, bool occupied);
}
