using UnityEngine;

public class CapturedShipsPileDisplay : MonoBehaviour // CHANGED: this pile is built from pre-existing individual card slots, not one swappable sprite
{
    public GameObject[] _ShipSlots; // NEW: the 5 CapturedShip child objects, in the order they should appear

    public void Refresh(int capturedCount)
    {
        for (int i = 0; i < _ShipSlots.Length; i++)
        {
            _ShipSlots[i].SetActive(i < capturedCount);
        }
    }
}