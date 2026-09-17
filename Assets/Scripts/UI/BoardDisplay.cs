using UnityEngine;
using System.Collections.Generic;

public class BoardDisplay : MonoBehaviour
{
    public GameObject _CardDisplayPrefab;
    public CardArtDatabase _ArtDatabase;

    private const int _Columns = 4; // NEW: must match the Grid Layout Group's Fixed Column Count on this board's panel

    public void ShowBoard(PlayerState player, bool rotate180) // CHANGED: rotate180 now means "flip vertically" (row order reversed, column order kept)
    {
        foreach (Transform child in transform)
        {
            Destroy(child.gameObject);
        }

        List<GridCell> cellsToShow = rotate180 ? FlipRowsVertically(player._Grid) : player._Grid; // CHANGED

        foreach (GridCell cell in cellsToShow)
        {
            GameObject cardObject = Instantiate(_CardDisplayPrefab, transform);
            CardDisplay display = cardObject.GetComponent<CardDisplay>();

            Sprite sprite = _ArtDatabase.GetShipSprite(cell, player._Color);
            display.SetSprite(sprite);
            display._RepresentedCell = cell;

            Sprite shieldSprite = _ArtDatabase.GetShieldSprite(cell, player._Color);
            display.SetShieldOverlay(shieldSprite);
        }
    }

    private List<GridCell> FlipRowsVertically(List<GridCell> grid) // NEW: reverses the ORDER of rows top-to-bottom, but keeps each row's own left-to-right order
    {
        List<GridCell> flipped = new List<GridCell>(grid.Count);
        int rowCount = grid.Count / _Columns;

        for (int row = rowCount - 1; row >= 0; row--)
        {
            for (int col = 0; col < _Columns; col++)
            {
                flipped.Add(grid[row * _Columns + col]);
            }
        }

        return flipped;
    }
}