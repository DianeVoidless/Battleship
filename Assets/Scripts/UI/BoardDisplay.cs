using UnityEngine;

public class BoardDisplay : MonoBehaviour
{
    public GameObject _CardDisplayPrefab;
    public CardArtDatabase _ArtDatabase;

    public void ShowBoard(PlayerState player)
    {
        foreach (Transform child in transform) // NEW: clear out the old cells first, same pattern ShowHand already uses
        {
            Destroy(child.gameObject);
        }

        foreach (GridCell cell in player._Grid)
        {
            GameObject cardObject = Instantiate(_CardDisplayPrefab, transform);
            CardDisplay display = cardObject.GetComponent<CardDisplay>();

            Sprite sprite = _ArtDatabase.GetShipSprite(cell._Ship, player._Color, cell._Revealed);
            display.SetSprite(sprite);
            display._RepresentedCell = cell;
        }
    }
}
