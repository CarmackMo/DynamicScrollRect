using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DynamicScrollRectController : MonoBehaviour
{
    public bool scriptControlDemo = false;

    public GameObject item1;
    public GameObject item2;
    public GameObject item3;
    public GameObject item4;
    public GameObject item5;
    public GameObject item6;
    public GameObject item7;

    public DynamicScrollRect scrollRectVertical;
    public DynamicScrollRect scrollRectHorizontal;

    public int itemGroupIdx;
    public int itemIdx;
    public int subItemIdx;
    public float scrollTime;
    public GameObject newItem;

    private void Awake()
    {
        if (scriptControlDemo)
            InitScrollRect();

        if (scrollRectVertical != null)
        {
            scrollRectVertical.OnSpawnItemAtStartEvent += OnSpawnItemAtStart;
            scrollRectVertical.OnSpawnItemAtEndEvent += OnSpawnItemAtEnd;
            scrollRectVertical.OnSpawnSubItemAtStartEvent += OnSpawnSubItemAtStart;
            scrollRectVertical.OnSpawnSubItemAtEndEvent += OnSpawnSubItemAtEnd;
        }
        if (scrollRectHorizontal != null)
        {
            scrollRectHorizontal.OnSpawnItemAtStartEvent += OnSpawnItemAtStart;
            scrollRectHorizontal.OnSpawnItemAtEndEvent += OnSpawnItemAtEnd;
            scrollRectHorizontal.OnSpawnSubItemAtStartEvent += OnSpawnSubItemAtStart;
            scrollRectHorizontal.OnSpawnSubItemAtEndEvent += OnSpawnSubItemAtEnd;
        }
    }

    void Start() 
    {

    }

    private void OnGUI()
    {
        if (scriptControlDemo)
        {
            if (GUI.Button(new Rect(0, 0, 100, 50), "item remove test"))
            {
                scrollRectHorizontal.RemoveItemDynamic(itemGroupIdx, itemIdx);
            }

            if (GUI.Button(new Rect(0, 50, 100, 50), "subItem remove test"))
            {
                scrollRectHorizontal.RemoveSubItemDynamic(itemGroupIdx, subItemIdx);
            }

            if (GUI.Button(new Rect(0, 100, 100, 50), "item add test"))
            {
                scrollRectHorizontal.AddItemDynamic(itemGroupIdx, itemIdx, newItem);
            }

            if (GUI.Button(new Rect(0, 150, 100, 50), "subItem add test"))
            {
                scrollRectHorizontal.AddSubItemDynamic(itemGroupIdx, subItemIdx);
            }

            if (GUI.Button(new Rect(0, 200, 100, 50), "scroll to item test"))
            {
                scrollRectHorizontal.ScrollToItem(itemGroupIdx, itemIdx, scrollTime);
            }

            if (GUI.Button(new Rect(0, 250, 100, 50), "scroll to subItem test"))
            {
                scrollRectHorizontal.ScrollToSubItem(itemGroupIdx, subItemIdx, scrollTime);
            }
        }
    }

    private void InitScrollRect()
    {
        List<GameObject> list1 = new List<GameObject>();
        list1.Add(item1);
        list1.Add(item2);
        list1.Add(item1);
        list1.Add(item3);
        list1.Add(item2);
        list1.Add(item6);
        scrollRectHorizontal.AddItemGroupStatic(5, 25, list1, item1);

        List<GameObject> list2 = new List<GameObject>();
        list2.Add(item2);
        list2.Add(item1);
        list2.Add(item7);
        list2.Add(item2);
        list2.Add(item3);
        list2.Add(item1);
        list2.Add(item2);
        list2.Add(item2);
        scrollRectHorizontal.AddItemGroupStatic(2, 7, list2, item1);
    }


    private void OnSpawnItemAtStart(DynamicScrollRect.ItemGroupConfig itemGroup, GameObject item = null)
    {
        if (item != null)
        {
            item.GetComponent<MyItem>().SetText($"{itemGroup.itemGroupIdx}.{itemGroup.firstItemIdx}");
            item.gameObject.name = $"Group{itemGroup.itemGroupIdx} Item{itemGroup.firstItemIdx}";
        }
    }

    private void OnSpawnItemAtEnd(DynamicScrollRect.ItemGroupConfig itemGroup, GameObject item = null)
    {
        if (item != null)
        {
            item.GetComponent<MyItem>().SetText($"{itemGroup.itemGroupIdx}.{itemGroup.lastItemIdx - 1}");
            item.gameObject.name = $"Group{itemGroup.itemGroupIdx} Item{itemGroup.lastItemIdx - 1}";
        }
    }

    private void OnSpawnSubItemAtStart(DynamicScrollRect.ItemGroupConfig itemGroup, GameObject subItem = null)
    {
        if (subItem != null)
        {
            subItem.GetComponent<MyItem>().SetText($"{itemGroup.itemGroupIdx}.{itemGroup.nestedItemIdx}.{itemGroup.firstSubItemIdx}");
            subItem.gameObject.name = $"Group{itemGroup.itemGroupIdx} SubItem{itemGroup.firstSubItemIdx}";
        }
    }

    private void OnSpawnSubItemAtEnd(DynamicScrollRect.ItemGroupConfig itemGroup, GameObject subItem = null)
    {
        if (subItem != null)
        {
            subItem.GetComponent<MyItem>().SetText($"{itemGroup.itemGroupIdx}.{itemGroup.nestedItemIdx}.{itemGroup.lastSubItemIdx - 1}");
            subItem.gameObject.name = $"Group{itemGroup.itemGroupIdx} SubItem{itemGroup.lastSubItemIdx - 1}";
        }
    }
}
