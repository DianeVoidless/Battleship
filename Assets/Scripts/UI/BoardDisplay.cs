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

            Sprite sprite = _ArtDatabase.GetShipSprite(cell, player._Color);
            display.SetSprite(sprite);
            display._RepresentedCell = cell;

            Sprite shieldSprite = _ArtDatabase.GetShieldSprite(cell, player._Color); // NEW: shows/hides the shield overlay based on this cell's remaining shield HP
            display.SetShieldOverlay(shieldSprite);
        }
    }
}
