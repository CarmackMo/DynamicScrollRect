using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DelegateTester : MonoBehaviour
{
    public GameObject item1;
    public GameObject item2;
    public GameObject item3;
    public GameObject item4;
    public GameObject item5;
    public GameObject item6;
    public GameObject item7;

    public MyScrollRect scrollRect;


    public int itemGroupIdx;
    public int itemIdx;
    public int subItemIdx;

    public GameObject newItem;

    private void Awake()
    {
        scrollRect.OnSpawnItemAtStartEvent += OnSpawnItemAtStart;
        scrollRect.OnSpawnItemAtEndEvent += OnSpawnItemAtEnd;
        scrollRect.OnSpawnSubItemAtStartEvent += OnSpawnSubItemAtStart;
        scrollRect.OnSpawnSubItemAtEndEvent += OnSpawnSubItemAtEnd;

        List<GameObject> list1 = new List<GameObject>();
        list1.Add(item1);
        list1.Add(item2);
        list1.Add(item1);
        list1.Add(item3);
        list1.Add(item2);
        list1.Add(item6);
        scrollRect.AddItemGroupStatic(5, 25, list1, item1);

        List<GameObject> list2 = new List<GameObject>();
        list2.Add(item2);
        list2.Add(item1);
        list2.Add(item7);
        list2.Add(item2);
        list2.Add(item3);
        list2.Add(item1);
        scrollRect.AddItemGroupStatic(2, 7, list2, item1);
    }

    void Start() 
    {

    }

    private void OnGUI()
    {
        if (GUI.Button(new Rect(0, 0, 100, 50), "item remove test"))
        {
            scrollRect.RemoveItemDynamic(itemGroupIdx, itemIdx);
        }

        if (GUI.Button(new Rect(0, 50, 100, 50), "subItem remove test"))
        {
            scrollRect.RemoveSubItemDynamic(itemGroupIdx, subItemIdx);
        }

        if (GUI.Button(new Rect(0, 100, 100, 50), "item add test"))
        {
            scrollRect.AddItemDynamic(itemGroupIdx, itemIdx, newItem);
        }

        if (GUI.Button(new Rect(0, 150, 100, 50), "subItem add test"))
        {
            scrollRect.AddSubItemDynamic(itemGroupIdx, subItemIdx);
        }
    }


    private void OnSpawnItemAtStart(MyScrollRect.ItemGroupConfig itemGroup, GameObject item = null)
    {
        item.GetComponent<MyItem>().SetText($"{itemGroup.itemGroupIdx}.{itemGroup.firstItemIdx}");
        item.gameObject.name = $"Group{itemGroup.itemGroupIdx} Item{itemGroup.firstItemIdx}";
    }

    private void OnSpawnItemAtEnd(MyScrollRect.ItemGroupConfig itemGroup, GameObject item = null)
    {
        item.GetComponent<MyItem>().SetText($"{itemGroup.itemGroupIdx}.{itemGroup.lastItemIdx - 1}");
        item.gameObject.name = $"Group{itemGroup.itemGroupIdx} Item{itemGroup.lastItemIdx - 1}";
    }

    private void OnSpawnSubItemAtStart(MyScrollRect.ItemGroupConfig itemGroup, GameObject subItem = null)
    {
        subItem.GetComponent<MyItem>().SetText($"{itemGroup.itemGroupIdx}.{itemGroup.nestedItemIdx}.{itemGroup.firstSubItemIdx}");
        subItem.gameObject.name = $"Group{itemGroup.itemGroupIdx} SubItem{itemGroup.firstSubItemIdx}";
    }

    private void OnSpawnSubItemAtEnd(MyScrollRect.ItemGroupConfig itemGroup, GameObject subItem = null)
    {
        subItem.GetComponent<MyItem>().SetText($"{itemGroup.itemGroupIdx}.{itemGroup.nestedItemIdx}.{itemGroup.lastSubItemIdx - 1}");
        subItem.gameObject.name = $"Group{itemGroup.itemGroupIdx} SubItem{itemGroup.lastSubItemIdx - 1}";
    }
}
