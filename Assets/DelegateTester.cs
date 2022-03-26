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

    private void Awake()
    {
        scrollRect.OnSubItemSpawnEvent += SetSubItemText;

        List<GameObject> list1 = new List<GameObject>();
        list1.Add(item1);
        list1.Add(item2);
        list1.Add(item1);
        list1.Add(item3);
        list1.Add(item2);
        list1.Add(item6);
        scrollRect.AddItemGroup(5, 25, list1, item1);

        List<GameObject> list2 = new List<GameObject>();
        list2.Add(item1);
        list2.Add(item7);
        list2.Add(item2);
        list2.Add(item3);
        scrollRect.AddItemGroup(1, 7, list2, item1);
    }

    void Start() 
    { 

        //scrollRect.gameObject.SetActive(true);
    }

    private void OnGUI()
    {
        if (GUI.Button(new Rect(0, 0, 100, 50), "item remove test"))
        {
            scrollRect.RemoveItemDynamic(itemGroupIdx, itemIdx);
        }

        if (GUI.Button(new Rect(0, 100, 100, 50), "subItem remove test"))
        {
            scrollRect.RemoveSubItemDynamic(itemGroupIdx, subItemIdx);
        }
    }

    private void SetSubItemText(GameObject subItem)
    {
        subItem.GetComponent<MyItem>().SetText("???");
    }
}
