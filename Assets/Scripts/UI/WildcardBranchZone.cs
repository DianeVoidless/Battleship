using UnityEngine;
using UnityEngine.EventSystems;

public class WildcardBranchZone : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    public bool _IsGatedBranch; 

    private CardDisplay _ParentCard;

    void Awake()
    {
        _ParentCard = GetComponentInParent<CardDisplay>();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        _ParentCard.SetWildcardHover(_IsGatedBranch, true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _ParentCard.SetWildcardHover(_IsGatedBranch, false);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        _ParentCard.OnWildcardBranchClicked(_IsGatedBranch, eventData);
    }
}