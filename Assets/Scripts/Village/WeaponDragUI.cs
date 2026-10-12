using UnityEngine;
using UnityEngine.EventSystems;

public static class WeaponDragContext
{
    public static string weaponId;
    public static int fromSlot = -1;

    public static void Set(string id, int slot)
    {
        weaponId = id;
        fromSlot = slot;
    }

    public static void Clear()
    {
        weaponId = null;
        fromSlot = -1;
    }
}

public class WeaponInspectable : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public string weaponId;
    public VillageScreen screen;

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (screen != null && !string.IsNullOrEmpty(weaponId))
            screen.ShowWeaponDetail(weaponId, eventData.position);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (screen != null) screen.HideWeaponDetail(weaponId);
    }
}

public class WeaponDragSource : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public string weaponId;
    public int fromSlot = -1;
    public VillageScreen screen;
    CanvasGroup group;

    void Awake()
    {
        group = GetComponent<CanvasGroup>();
        if (group == null) group = gameObject.AddComponent<CanvasGroup>();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (screen == null || string.IsNullOrEmpty(weaponId)) return;
        WeaponDragContext.Set(weaponId, fromSlot);
        group.blocksRaycasts = false;
        screen.BeginDragGhost(weaponId, eventData.position);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (screen != null) screen.MoveDragGhost(eventData.position);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (group != null) group.blocksRaycasts = true;
        WeaponDragContext.Clear();
        if (screen != null) screen.EndDragGhost();
    }
}

public class WeaponSlotDrop : MonoBehaviour, IDropHandler
{
    public int slotIndex;
    public VillageScreen screen;

    public void OnDrop(PointerEventData eventData)
    {
        if (screen == null || string.IsNullOrEmpty(WeaponDragContext.weaponId)) return;
        screen.DropOnSlot(WeaponDragContext.weaponId, slotIndex);
    }
}

public class WeaponBagDrop : MonoBehaviour, IDropHandler
{
    public VillageScreen screen;

    public void OnDrop(PointerEventData eventData)
    {
        if (screen == null || WeaponDragContext.fromSlot < 0) return;
        screen.DropOnBag(WeaponDragContext.fromSlot);
    }
}
