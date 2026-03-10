using UnityEngine;
using UnityEngine.EventSystems;

public class ClickboxCallbackSender : MonoBehaviour, IPointerClickHandler
{

    IPointerClickHandler parent;

    void Awake()
    {
        parent = transform.parent.gameObject.GetComponent<IPointerClickHandler>();
    }

    public void OnPointerClick(PointerEventData e)
    {
        parent.OnPointerClick(e);
    }

}
