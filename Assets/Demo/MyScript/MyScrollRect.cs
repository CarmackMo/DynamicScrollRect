using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;

public class MyScrollRect : UIBehaviour, IBeginDragHandler, IEndDragHandler, IDragHandler, IScrollHandler
{
    #region 用户设定相关
    public enum ScrollMovementType
    {
        Unrestricted,   // Unrestricted movement -- can scroll forever
        Elastic,        // Restricted but flexible -- can go past the edges, but springs back in place
        Clamped,        // Restricted movement where it's not possible to go past the edges
    }

    public enum ScrollbarVisibility
    {
        Permanent,
        AutoHide,
        AutoHideAndExpandViewport,
    }

    [Serializable]
    public class ScrollRectEvent : UnityEvent<Vector2> { }
    
    [Serializable]
    public class ItemGroupConfig
    {
        public int nestedItemIdx = -1;
        public int subItemCount = 0;
        
        public int itemCount { get { return itemList.Count; } }
        public int displayItemCount { get { return displayItemList.Count; } }
        public int displaySubItemCount { get { return displaySubItemList.Count; } }
        public int nestedConstrainCount { get { return (itemList[nestedItemIdx].TryGetComponent<GridLayoutGroup>(out var layout) && (layout.constraint != GridLayoutGroup.Constraint.Flexible)) ? layout.constraintCount : 1; } }


        [NonSerialized] public int firstItemIdx = 0;
        [NonSerialized] public int lastItemIdx = 0;
        [NonSerialized] public int firstSubItemIdx = 0;
        [NonSerialized] public int lastSubItemIdx = 0;

        public List<GameObject> itemList = new List<GameObject>();
        public List<GameObject> subItemList = new List<GameObject>();
        public GameObject subItem = null;

        [NonSerialized] public List<GameObject> displayItemList = new List<GameObject>();
        [NonSerialized] public List<GameObject> displaySubItemList = new List<GameObject>();
    }


    [Header("ScrollView参数")]
    [SerializeField]
    private float scrollSensitivity = 1f;
    public float ScrollSensitivity { get { return scrollSensitivity; } set { scrollSensitivity = value; } }

    [SerializeField]
    private float rubberScale = 1f;
    public float RubberScale { get { return rubberScale; } set { rubberScale = value; } }

    [SerializeField]
    private float displayOffset = 0f;
    public float DisplayOffset { get { return displayOffset; } set { displayOffset = value; } }

    [SerializeField]
    private bool reverseDirection = false;
    public bool ReverseDirection { get { return reverseDirection; } set { reverseDirection = value; } }

    [SerializeField]
    private bool horizontal = true;
    public bool Horizontal { get { return horizontal; } set { horizontal = value; } }

    [SerializeField]
    private bool vertical = true;
    public bool Vertical { get { return vertical; } set { vertical = value; } }

    [SerializeField]
    private GameObject scrollView = null;
    public GameObject ScrollView { get { return scrollView; } set { scrollView = value; } }

    [SerializeField]
    private GameObject scrollContent = null;
    public GameObject ScrollContent { get { return scrollContent; } set { scrollContent = value; } }

    [SerializeField]
    private GameObject item = null;
    public GameObject Item { get { return item; } set { item = value; } }

    [SerializeField]
    private List<ItemGroupConfig> itemGroupList = new List<ItemGroupConfig>();
    public List<ItemGroupConfig> ItemGroupList { get { return itemGroupList; } set { itemGroupList = value; } }




    [Header("ScrollBar参数")]
    [SerializeField]
    private Scrollbar horizontalScrollbar = null;
    public Scrollbar HorizontalScrollbar { get { return horizontalScrollbar; } set { horizontalScrollbar = value; } }

    [SerializeField]
    private Scrollbar verticalScrollbar = null;
    public Scrollbar VerticalScrollbar { get { return verticalScrollbar; } set { verticalScrollbar = value; } }

    [SerializeField]
    private ScrollbarVisibility horizontalScrollbarVisibility = ScrollbarVisibility.AutoHide;
    public ScrollbarVisibility HorizontalScrollbarVisibility { get { return horizontalScrollbarVisibility; } set { horizontalScrollbarVisibility = value; SetDirtyCaching(); } }

    [SerializeField]
    private ScrollbarVisibility verticalScrollbarVisibility = ScrollbarVisibility.AutoHide;
    public ScrollbarVisibility VerticalScrollbarVisibility { get { return verticalScrollbarVisibility; } set { verticalScrollbarVisibility = value; SetDirtyCaching(); } }

    [SerializeField]
    private ScrollMovementType movementType = ScrollMovementType.Elastic;
    public ScrollMovementType MovementType { get { return movementType; } set { movementType = value; } }

    [SerializeField]
    private float elasticity = 0.1f;          /* Only used for ScrollMovementType.Elastic */
    public float Elasticity { get { return elasticity; } set { elasticity = value; } }

    [SerializeField]
    private bool inertia = true;
    public bool Inertia { get { return inertia; } set { inertia = value; } }

    [SerializeField]
    private float decelerationRate = 0.135f; /* Only used when inertia is enabled */
    public float DecelerationRate { get { return decelerationRate; } set { decelerationRate = value; } }

    [SerializeField]
    private ScrollRectEvent onValueChanged = new ScrollRectEvent();
    public ScrollRectEvent OnValueChanged { get { return onValueChanged; } set { onValueChanged = value; } }

    #endregion


    #region 私有UI组件相关

    private RectTransform scrollViewRect;
    private RectTransform scrollContentRect;
    private RectTransform horizontalScrollbarRect;
    private RectTransform verticalScrollbarRect;
    private RectTransform rect;

    private GridLayoutGroup gridLayout = null;

    private Bounds scrollViewBounds;
    private Bounds scrollContentBounds;
    private Bounds prevScrollViewBounds;
    private Bounds prevScrollContentBounds;

    private Vector2 velocity;
    private Vector2 cursorStartPos;
    private Vector2 contentStartPos;
    private Vector2 prevPos = Vector2.zero;
    protected Vector2 normalizedPosition { get { return new Vector2(horizontalNormalizedPosition, verticalNormalizedPosition); }
                                           set { SetNormalizedPosition(value.x, 0); SetNormalizedPosition(value.y, 1); } }
    
    protected float horizontalNormalizedPosition { get { return GetHorizontalNormalizedPosition(); } set { SetNormalizedPosition(value, 0); } }
    protected float verticalNormalizedPosition { get { return GetVerticalNormalizedPosition(); } set { SetNormalizedPosition(value, 1); } }
    private bool horizontalScrollingNeeded { get { if (Application.isPlaying) return scrollContentBounds.size.x > scrollViewBounds.size.x + 0.01f; else return true; } }
    private bool verticalScrollingNeeded { get { if (Application.isPlaying) return scrollContentBounds.size.y > scrollViewBounds.size.y + 0.01f; else return true; } }

    private int layoutConstrainCount = -1;
    protected int LayoutConstraintCount { get { return GetLayoutConstraintCount(); } }
    protected int startLine { get { return Mathf.CeilToInt((float)(firstItemIdx) / layoutConstrainCount); } }                       // the first line of item that display in scroll view among all lines of items
    protected int currentLines { get { return Mathf.CeilToInt((float)(lastItemIdx - firstItemIdx) / layoutConstrainCount); } }      // the amount of lines of items that are displaying in the scroll view
    protected int totalLines { get { return Mathf.CeilToInt((float)(itemCount) / layoutConstrainCount); } }                         // the amount of lines regarding to all items

    private float itemSpacing = -1.0f;
    protected float ItemSpacing { get { return GetItemSpacing(); } }

    protected float contentLeftPadding = 0;
    protected float contentRightPadding = 0;
    protected float contentTopPadding = 0;
    protected float contentDownPadding = 0;
    protected float threshold = 0f;

    private bool isDragging;


    #endregion


    #region 内容数据相关

    private int firstItemIdx = 0;
    private int lastItemIdx = 0;
    private int firstItemGroupIdx = 0;
    private int lastItemGroupIdx = 0;

    private int itemGroupCount { get { return itemGroupList.Count; } }
    private int displayItemCount { get { return displayItemList.Count; } }
    private int displayItemGroupCount { get { return displayItemGroupList.Count; } }

    private List<GameObject> displayItemList = new List<GameObject>();
    private List<ItemGroupConfig> displayItemGroupList = new List<ItemGroupConfig>();

    #endregion


    #region 对象池相关

    private int despawnItemCountStart = 0;
    private int despawnItemCountEnd = 0;
    private int despawnSubItemCountStart = 0;
    private int despawnSubItemCountEnd = 0;

    //public Stack<GameObject> itemPool = new Stack<GameObject>();

    private class ItemPoolMember : MonoBehaviour { public GameObject prefab = null; }    /* Added to freshly instantiated objects, so we can link back to the correct pool on despawn. */

    protected Dictionary<GameObject, Stack<GameObject>> itemPoolDict = new Dictionary<GameObject, Stack<GameObject>>();
    #endregion


    #region Debug相关，可删除

    public int itemCount = 20;
    private Vector2 scrollBoundMax;
    private Vector2 scrollBoundMin;
    private Vector2 contentBoundMax;
    private Vector2 contentBoundMin;

    #endregion



    protected override void Awake()
    {
        scrollViewRect = scrollView.GetComponent<RectTransform>();
        scrollContentRect = scrollContent.GetComponent<RectTransform>();
        rect = GetComponent<RectTransform>();

        SetScrollBar(SetHorizontalNormalizedPosition, ref horizontalScrollbar, ref horizontalScrollbarRect);
        SetScrollBar(SetVerticalNormalizedPosition, ref verticalScrollbar, ref verticalScrollbarRect);
        GetLayoutConstraintCount();
        GetItemSpacing();
    }

    protected override void Start()
    {

        RefillItemGroup(out _, itemGroupList[0]);


        //RefillItems();
    }

    protected virtual void LateUpdate()
    {
        if (!scrollContentRect)
            return;

        EnsureLayoutHasRebuilt();
        UpdateScrollbarVisibility();
        UpdateBounds(false);
        float deltaTime = Time.unscaledDeltaTime;
        Vector2 offset = CalculateOffset(Vector2.zero);

        if (!isDragging && (offset != Vector2.zero || velocity != Vector2.zero))
        {
            Vector2 position = scrollContentRect.anchoredPosition;
            for (int axis = 0; axis < 2; axis++)
            {
                /* Apply spring physics if movement is elastic and scrollContent has an offset from the view. */
                if (movementType == ScrollMovementType.Elastic && offset[axis] != 0)
                {
                    float speed = velocity[axis];
                    position[axis] = Mathf.SmoothDamp(scrollContentRect.anchoredPosition[axis], scrollContentRect.anchoredPosition[axis] + offset[axis], ref speed, elasticity, Mathf.Infinity, deltaTime);
                    velocity[axis] = speed;
                }
                /* Else move scrollContent according to velocity with deceleration applied. */
                else if (inertia)
                {
                    velocity[axis] *= Mathf.Pow(decelerationRate, deltaTime);
                    if (Mathf.Abs(velocity[axis]) < 1)
                        velocity[axis] = 0;
                    position[axis] += velocity[axis] * deltaTime;
                }
                /* If we have neither elaticity or friction, there shouldn't be any velocity. */
                else
                {
                    velocity[axis] = 0;
                }
            }

            if (velocity != Vector2.zero)
            {
                if (movementType == ScrollMovementType.Clamped)
                {
                    offset = CalculateOffset(position - scrollContentRect.anchoredPosition);
                    position += offset;
                }

                SetContentAnchoredPosition(position);
            }
        }

        if (isDragging && inertia)
        {
            Vector3 newVelocity = (scrollContentRect.anchoredPosition - prevPos) / deltaTime;
            velocity = Vector3.Lerp(velocity, newVelocity, deltaTime * 10);
        }

        if (scrollViewBounds != prevScrollViewBounds || scrollContentBounds != prevScrollContentBounds || scrollContentRect.anchoredPosition != prevPos)
        {
            UpdateScrollbars(offset);
            onValueChanged.Invoke(normalizedPosition);
            UpdatePrevData();
        }
    }


    #region 列表元素增加

    #region 旧版单种类元素增加
    public bool AddItemAtStart(out float size, bool considerSpacing, GameObject prefab, GameObject parent)
    {
        size = 0;

        if (itemCount >= 0 && firstItemIdx < layoutConstrainCount)
            return false;

        for (int i = 0; i < layoutConstrainCount; i++)
        {
            /* Add the gameObject of the item to the scrollContent */
            GameObject newItem = SpawnItem(prefab);
            newItem.transform.parent = parent.transform;
            newItem.transform.SetAsFirstSibling();

            /* Update the information for the items that are currently displaying */
            displayItemList.Reverse();
            displayItemList.Add(newItem);
            displayItemList.Reverse();
            firstItemIdx--;
            size = Mathf.Max(GetItemSize(newItem.GetComponent<RectTransform>(), considerSpacing), size);
            newItem.GetComponent<MyItem>().SetText(firstItemIdx.ToString());
            newItem.gameObject.name = firstItemIdx.ToString();
        }

        threshold = Mathf.Max(threshold, size * 1.5f);        /* 用途暂时不明 */

        /* Update the parameter of the scrollContent UI */
        if (!reverseDirection)
        {
            Vector2 offset = GetVector2(size);
            scrollContentRect.anchoredPosition += offset;
            prevPos += offset;
            contentStartPos += offset;
        }

        return true;
    }


    public bool AddItemAtEnd(out float size, bool considerSpacing, GameObject prefab, GameObject parent)
    {
        size = 0;

        if (itemCount >= 0 && lastItemIdx >= itemCount)
            return false;




        ///* Add the gameObject of the item to the scrollContent */
        //GameObject newItem = SpawnItem();
        //newItem.transform.parent = scrollContent.transform;
        //newItem.transform.SetAsLastSibling();

        ///* Update the information for the items that are currently displaying */
        //displayItemList.Add(newItem);
        //lastItemIdx++;
        //newItem.GetComponent<MyItem>().SetText(lastItemIdx.ToString());
        //newItem.gameObject.name = lastItemIdx.ToString();



        int availableItems = scrollContentRect.childCount - (despawnItemCountStart + despawnItemCountEnd);
        int count = layoutConstrainCount - (availableItems % layoutConstrainCount);
        for (int i = 0; i < count; i++)
        {
            /* Add the gameObject of the item to the scrollContent */
            GameObject newItem = SpawnItem(prefab);
            newItem.transform.parent = parent.transform;
            newItem.transform.SetAsLastSibling();

            /* Update the information for the items that are currently displaying */
            newItem.GetComponent<MyItem>().SetText(lastItemIdx.ToString());
            newItem.gameObject.name = lastItemIdx.ToString();
            displayItemList.Add(newItem);
            lastItemIdx++;
            size = Mathf.Max(GetItemSize(newItem.GetComponent<RectTransform>(), considerSpacing), size);

            if (itemCount >= 0 && lastItemIdx >= itemCount)
                break;
        }

        threshold = Mathf.Max(threshold, size * 1.5f);        // 用途暂时不明

        if (reverseDirection)
        {
            Vector2 offset = GetVector2(size);
            scrollContentRect.anchoredPosition -= offset;
            prevPos -= offset;
            contentStartPos -= offset;
        }

        return true;
    }

    #endregion


    #region 新版多种类元素增加
    public bool AddItemAtStart(out float size, bool considerSpacing, GameObject prefab, GameObject parent, ItemGroupConfig itemGroup)
    {
        size = 0;

        if (itemGroup.itemCount >= 0 && itemGroup.firstItemIdx < layoutConstrainCount)
            return false;

        for (int i = 0; i < layoutConstrainCount; i++)
        {
            /* Add the gameObject of the item to the scrollContent */
            GameObject newItem = SpawnItem(prefab);
            newItem.transform.parent = parent.transform;
            newItem.transform.SetAsFirstSibling();

            /* Update the information for the items that are currently displaying */
            displayItemList.Reverse();
            displayItemList.Add(newItem);
            displayItemList.Reverse();
            firstItemIdx--;
            itemGroup.displayItemList.Reverse();
            itemGroup.displayItemList.Add(newItem);
            itemGroup.displayItemList.Reverse();
            itemGroup.firstItemIdx--;

            size = Mathf.Max(GetItemSize(newItem.GetComponent<RectTransform>(), considerSpacing), size);
            newItem.GetComponent<MyItem>().SetText(firstItemIdx.ToString());
            newItem.gameObject.name = "Group" + itemGroupList.IndexOf(itemGroup) + " Item" + itemGroup.firstItemIdx.ToString();
        }

        /* Update the parameter of the scrollContent UI */
        if (!reverseDirection)
        {
            Vector2 offset = GetVector2(size);
            scrollContentRect.anchoredPosition += offset;
            prevPos += offset;
            contentStartPos += offset;
        }

        return true;
    }

    public bool AddItemAtEnd(out float size, bool considerSpacing, GameObject prefab, GameObject parent, ItemGroupConfig itemGroup)
    {
        size = 0;

        if (itemGroup.itemCount >= 0 && itemGroup.lastItemIdx >= itemGroup.itemCount)
            return false;

        //int availableItems = scrollContentRect.childCount - (despawnItemCountStart + despawnItemCountEnd);
        int availableItems = displayItemCount - (despawnItemCountStart + despawnItemCountEnd);
        int count = layoutConstrainCount - (availableItems % layoutConstrainCount);
        for (int i = 0; i < count; i++)
        {
            /* Add the gameObject of the item to the scrollContent */
            GameObject newItem = SpawnItem(prefab);
            newItem.transform.parent = parent.transform;
            newItem.transform.SetAsLastSibling();

            /* Update the information for the items that are currently displaying */
            newItem.GetComponent<MyItem>().SetText(lastItemIdx.ToString());
            newItem.gameObject.name = "Group" + itemGroupList.IndexOf(itemGroup) + " Item" + itemGroup.lastItemIdx.ToString();
            displayItemList.Add(newItem);
            lastItemIdx++;
            itemGroup.displayItemList.Add(newItem);
            itemGroup.lastItemIdx++;
            size = Mathf.Max(GetItemSize(newItem.GetComponent<RectTransform>(), considerSpacing), size);

            if (itemGroup.itemCount >= 0 && itemGroup.lastItemIdx >= itemGroup.itemCount)
                break;
        }

        if (reverseDirection)
        {
            Vector2 offset = GetVector2(size);
            scrollContentRect.anchoredPosition -= offset;
            prevPos -= offset;
            contentStartPos -= offset;
        }

        return true;
    }

    public bool AddSubItemAtStart(out float size, bool considerSpacing, GameObject prefab, GameObject parent, ItemGroupConfig itemGroup)
    {
        size = 0;
        if (itemGroup.subItemCount >= 0 && itemGroup.firstSubItemIdx < itemGroup.nestedConstrainCount)
            return false;

        /* For the case when subitems cannot fully fill the last row */
        int count = itemGroup.nestedConstrainCount;
        if (itemGroup.displaySubItemCount == 0 && itemGroup.subItemCount % itemGroup.nestedConstrainCount != 0)
            count = itemGroup.subItemCount % itemGroup.nestedConstrainCount;

        for (int i = 0; i < count; i++)
        {
            /* Add the gameObject of the item to the parent */
            GameObject newItem = SpawnItem(prefab);
            newItem.transform.parent = parent.transform;
            newItem.transform.SetAsFirstSibling();

            /* Update the information for the items that are currently displaying */
            itemGroup.displaySubItemList.Reverse();
            itemGroup.displaySubItemList.Add(newItem);
            itemGroup.displaySubItemList.Reverse();
            itemGroup.firstSubItemIdx--;
            size = Mathf.Max(GetSubItemSize(newItem.GetComponent<RectTransform>(), parent.GetComponent<RectTransform>(), considerSpacing), size);
            newItem.GetComponent<MyItem>().SetText(itemGroup.firstSubItemIdx.ToString());
            newItem.gameObject.name = itemGroup.firstSubItemIdx.ToString();
        }

        /* Update the parameter of the scrollContent UI */
        if (!reverseDirection)
        {
            Vector2 offset = GetVector2(size);
            parent.GetComponent<RectTransform>().anchoredPosition += offset;
            scrollContentRect.anchoredPosition += offset;
            prevPos += offset;
            contentStartPos += offset;
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(parent.GetComponent<RectTransform>());
        LayoutRebuilder.ForceRebuildLayoutImmediate(scrollContentRect);
        return true;
    }

    public bool AddSubItemAtEnd(out float size, bool considerSpacing, GameObject prefab, GameObject parent, ItemGroupConfig itemGroup)
    {
        size = 0;
        if (itemGroup.subItemCount >= 0 && itemGroup.lastSubItemIdx >= itemGroup.subItemCount)
            return false;

        int availableSubItems = itemGroup.displaySubItemCount - (despawnSubItemCountStart + despawnSubItemCountEnd);
        int count = itemGroup.nestedConstrainCount - (availableSubItems % itemGroup.nestedConstrainCount);
        for (int i = 0; i < count; i++)
        {
            /* Add the gameObject of the item to the scrollContent */
            GameObject newItem = SpawnItem(prefab);
            newItem.transform.parent = parent.transform;
            newItem.transform.SetAsLastSibling();

            /* Update the information for the items that are currently displaying */
            newItem.GetComponent<MyItem>().SetText(itemGroup.lastSubItemIdx.ToString());
            newItem.gameObject.name = itemGroup.lastSubItemIdx.ToString();
            itemGroup.displaySubItemList.Add(newItem);
            itemGroup.lastSubItemIdx++;
            size = Mathf.Max(GetSubItemSize(newItem.GetComponent<RectTransform>(), parent.GetComponent<RectTransform>(), considerSpacing), size);

            if (itemGroup.subItemCount >= 0 && itemGroup.lastSubItemIdx >= itemGroup.subItemCount)
                break;
        }

        if (reverseDirection)
        {
            Vector2 offset = GetVector2(size);
            parent.GetComponent<RectTransform>().anchoredPosition -= offset;
            scrollContentRect.anchoredPosition -= offset;
            prevPos -= offset;
            contentStartPos -= offset;
        }

        return true;
    }

    public bool AddItemGroupAtStart(out float size, GameObject parent)
    {
        size = 0;
        if (firstItemGroupIdx <= 0)
            return false;

        var newItemGroup = itemGroupList[firstItemGroupIdx - 1];
        if (displayItemGroupList.Contains(newItemGroup))
            return false;

        if (AddItemAtStart(out size, true, newItemGroup.itemList[newItemGroup.firstItemIdx - 1], parent, newItemGroup))
        {
            if (newItemGroup.firstItemIdx == newItemGroup.nestedItemIdx && newItemGroup.subItemCount >= 0)
                AddSubItemAtStart(out size, false, newItemGroup.subItem, newItemGroup.displayItemList[0], newItemGroup);

            displayItemGroupList.Reverse();
            displayItemGroupList.Add(newItemGroup);
            displayItemGroupList.Reverse();
            firstItemGroupIdx--;

            return true;
        }
        else
            return false;
    }


    public bool AddItemGroupAtEnd(out float size, GameObject parent)
    {
        size = 0;
        if (lastItemGroupIdx >= itemGroupCount)
            return false;

        var newItemGroup = itemGroupList[lastItemGroupIdx];
        if (displayItemGroupList.Contains(newItemGroup))
            return false;

        if (AddItemAtEnd(out size, false, newItemGroup.itemList[newItemGroup.lastItemIdx], parent, newItemGroup))
        {
            if (newItemGroup.lastItemIdx - 1 == newItemGroup.nestedItemIdx && newItemGroup.subItemCount >= 0)
                AddSubItemAtEnd(out size, false, newItemGroup.subItem, newItemGroup.displayItemList[newItemGroup.displayItemCount - 1], newItemGroup);

            displayItemGroupList.Add(newItemGroup);
            lastItemGroupIdx++;

            return true;
        }
        else
            return false;

    }

    #endregion

    #endregion


    #region 列表元素删减

    #region 旧版单种类元素删减

    public bool RemoveItemAtStart(out float size, bool considerSpacing)
    {
        size = 0;
        int availableItems = scrollContentRect.childCount - (despawnItemCountStart + despawnItemCountEnd);




        //if (firstItemIdx == itemCount - 1)
        //    return false;

        ///* Remove the gameObject of the item from the scrollContent */
        //GameObject oldItem = displayItemList[0];
        //displayItemList.RemoveAt(0);
        //DespawnItem(oldItem);

        ///* Update the information for the items that are currently displaying */
        //firstItemIdx++;



        /* special case: when moving or dragging, we cannot simply delete start when we've reached the end */
        if ((isDragging || velocity != Vector2.zero) && itemCount >= 0 && lastItemIdx + layoutConstrainCount >= itemCount)
            return false;
        if (availableItems <= 0)
            return false;

        for (int i = 0; i < layoutConstrainCount; i++)
        {
            /* Add the item to the waiting list of despawn */
            GameObject oldItem = displayItemList[despawnItemCountStart];
            AddToItemDespawnList(true);

            /* Update the information for the items that are currently displaying */
            size = Mathf.Max(GetItemSize(oldItem.GetComponent<RectTransform>(), considerSpacing), size);
            availableItems--;
            firstItemIdx++;

            if (availableItems == 0)
                break;
        }

        /* Update the parameter of the scrollContent UI */
        if (!reverseDirection)
        {
            Vector2 offset = GetVector2(size);
            scrollContentRect.anchoredPosition -= offset;
            prevPos -= offset;
            contentStartPos -= offset;
        }

        return true;
    }

    public bool RemoveItemAtEnd(out float size, bool considerSpacing)
    {
        size = 0;
        int availableItems = scrollContentRect.childCount - (despawnItemCountStart + despawnItemCountEnd);




        //if (lastItemIdx == 0)
        //    return false;

        ///* Remove the gameObject of the item from the scrollContent */
        //GameObject oldItem = displayItemList[displayItemCount - 1];
        //displayItemList.RemoveAt(displayItemCount - 1);
        //DespawnItem(oldItem);

        ///* Update the information for the items that are currently displaying */
        //lastItemIdx--;



        /* special case: when moving or dragging, we cannot simply delete end when we've reached the top */
        if ((isDragging || velocity != Vector2.zero) && itemCount >= 0 && firstItemIdx < layoutConstrainCount)
            return false;
        if (availableItems <= 0)
            return false;

        for (int i = 0; i < layoutConstrainCount; i++)
        {
            /* Remove the gameObject of the item from the scrollContent */
            GameObject oldItem = displayItemList[displayItemCount - 1 - despawnItemCountEnd];
            AddToItemDespawnList(false);

            /* Update the information for the items that are currently displaying */
            size = Mathf.Max(GetItemSize(oldItem.GetComponent<RectTransform>(), considerSpacing), size);
            availableItems--;
            lastItemIdx--;

            if (lastItemIdx % layoutConstrainCount == 0 || availableItems == 0)
                break;
        }

        /* Update the parameter of the scrollContent UI */
        if (reverseDirection)
        {
            Vector2 offset = GetVector2(size);
            scrollContentRect.anchoredPosition += offset;
            prevPos += offset;
            contentStartPos += offset;
        }

        return true;
    }

    #endregion


    #region 新版多种类元素删减

    public bool RemoveItemAtStart(out float size, bool considerSpacing, ItemGroupConfig itemGroup)
    {
        size = 0;
        int availableItems = itemGroup.displayItemCount - (despawnItemCountStart + despawnItemCountEnd);

        if (availableItems <= 0)
            return false;

        /* special case: when moving or dragging, we cannot continue delete subitems at start when we've reached the end of the whole scroll content */
        /* we reach the end of the whole scroll content when: the item group at end is the last item group AND the item at end inside the item group is
         * the last item AND if the last item is a nested item, the subitems in last row is the last row of subitems */
        var lastItemGroup = displayItemGroupList[displayItemGroupCount - 1];
        if ((isDragging || velocity != Vector2.zero) &&
            lastItemGroupIdx >= itemGroupCount &&
            lastItemGroup.lastItemIdx >= lastItemGroup.itemCount &&
            (lastItemGroup.lastItemIdx != lastItemGroup.nestedItemIdx + 1 ||
             lastItemGroup.lastItemIdx == lastItemGroup.nestedItemIdx + 1 && lastItemGroup.lastSubItemIdx + lastItemGroup.nestedConstrainCount >= lastItemGroup.subItemCount))
            return false;

        for (int i = 0; i < layoutConstrainCount; i++)
        {
            /* Add the item to the waiting list of despawn */
            GameObject oldItem = itemGroup.displayItemList[despawnItemCountStart];
            AddToItemDespawnList(true);

            /* Update the information for the items that are currently displaying */
            size = Mathf.Max(GetItemSize(oldItem.GetComponent<RectTransform>(), considerSpacing), size);
            availableItems--;
            itemGroup.firstItemIdx++;
            firstItemIdx++;

            if (availableItems == 0)
                break;
        }

        /* Update the parameter of the scrollContent UI */
        if (!reverseDirection)
        {
            Vector2 offset = GetVector2(size);
            scrollContentRect.anchoredPosition -= offset;
            prevPos -= offset;
            contentStartPos -= offset;
        }

        return true;
    }

    public bool RemoveItemAtEnd(out float size, bool considerSpacing, ItemGroupConfig itemGroup)
    {
        size = 0;
        int availableItems = itemGroup.displayItemCount - (despawnItemCountStart + despawnItemCountEnd);

        if (availableItems <= 0)
            return false;

        /* special case: when moving or dragging, we cannot continue delete subitems at end when we've reached the start of the whole scroll content */
        /* we reach the start of the whole scroll content when: the item group at start is the first item group AND the item at first inside the item group is
         * the first item AND if the first item is a nested item, the subitems in first row is the first row of subitems */
        var firstItemGroup = displayItemGroupList[0];
        if ((isDragging || velocity != Vector2.zero) &&
            firstItemGroupIdx <= 0 &&
            firstItemGroup.firstItemIdx <= 0 &&
            (firstItemGroup.firstItemIdx != firstItemGroup.nestedItemIdx ||
             firstItemGroup.firstItemIdx == firstItemGroup.nestedItemIdx && firstItemGroup.firstItemIdx < firstItemGroup.nestedConstrainCount))
            return false;

        for (int i = 0; i < layoutConstrainCount; i++)
        {
            /* Remove the gameObject of the item from the scrollContent */
            GameObject oldItem = itemGroup.displayItemList[itemGroup.displayItemCount - 1 - despawnItemCountEnd];
            AddToItemDespawnList(false);

            /* Update the information for the items that are currently displaying */
            size = Mathf.Max(GetItemSize(oldItem.GetComponent<RectTransform>(), considerSpacing), size);
            availableItems--;
            itemGroup.lastItemIdx--;
            lastItemIdx--;

            if (itemGroup.lastItemIdx % layoutConstrainCount == 0 || availableItems == 0)
                break;
        }

        /* Update the parameter of the scrollContent UI */
        if (reverseDirection)
        {
            Vector2 offset = GetVector2(size);
            scrollContentRect.anchoredPosition += offset;
            prevPos += offset;
            contentStartPos += offset;
        }

        return true;
    }

    public bool RemoveSubItemAtStart(out float size, bool considerSpacing, GameObject parent, ItemGroupConfig itemGroup)
    {
        size = 0;
        int availableSubItems = itemGroup.displaySubItemCount - (despawnSubItemCountStart + despawnSubItemCountEnd);

        if (availableSubItems <= 0)
            return false;

        /* special case: when moving or dragging, we cannot continue delete subitems at start when we've reached the end of the whole scroll content */
        /* we reach the end of the whole scroll content when: the item group at end is the last item group AND the item at end inside the item group is
         * the last item is the last item AND if the last item is a nested item, the subitems in last row is the last row of subitems */
        var lastItemGroup = displayItemGroupList[displayItemGroupCount - 1];
        if ((isDragging || velocity != Vector2.zero) &&
            lastItemGroupIdx >= itemGroupCount &&
            lastItemGroup.lastItemIdx >= lastItemGroup.itemCount &&
            (lastItemGroup.lastItemIdx != lastItemGroup.nestedItemIdx + 1 ||
             lastItemGroup.lastItemIdx == lastItemGroup.nestedItemIdx + 1 && lastItemGroup.lastSubItemIdx + lastItemGroup.nestedConstrainCount >= lastItemGroup.subItemCount))
            return false;

        // TO DO: the logic here is not safety, if the subitems cannot fully fill the nested item
        for (int i = 0; i < itemGroup.nestedConstrainCount; i++)
        {
            /* Add the item to the waiting list of despawn */
            GameObject oldItem = itemGroup.displaySubItemList[despawnSubItemCountStart];
            AddToSubItemDespawnList(true);

            /* Update the information for the items that are currently displaying */
            size = Mathf.Max(GetSubItemSize(oldItem.GetComponent<RectTransform>(), parent.GetComponent<RectTransform>(), considerSpacing), size);
            availableSubItems--;
            itemGroup.firstSubItemIdx++;

            if (availableSubItems == 0)
                break;
        }

        /* Update the parameter of the scrollContent UI */
        if (!reverseDirection)
        {
            Vector2 offset = GetVector2(size);
            //parent.GetComponent<RectTransform>().anchoredPosition -= offset;
            scrollContentRect.anchoredPosition -= offset;
            prevPos -= offset;
            contentStartPos -= offset;
        }

        if (size != 0)
        {
            ClearSubItemDespawnList(itemGroup);
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(scrollContentRect);
        }

        return true;
    }

    public bool RemoveSubItemAtEnd(out float size, bool considerSpacing, GameObject parent, ItemGroupConfig itemGroup)
    {
        size = 0;
        int availableSubItems = itemGroup.displaySubItemCount - (despawnSubItemCountStart + despawnSubItemCountEnd);

        if (availableSubItems <= 0)
            return false;

        /* special case: when moving or dragging, we cannot continue delete subitems at end when we've reached the start of the whole scroll content */
        /* we reach the start of the whole scroll content when: the item group at start is the first item group AND the item at first inside the item group is
         * the first item AND if the first item is a nested item, the subitems in first row is the first row of subitems */
        var firstItemGroup = displayItemGroupList[0];
        if ((isDragging || velocity != Vector2.zero) &&
            firstItemGroupIdx <= 0 &&
            firstItemGroup.firstItemIdx <= 0 &&
            (firstItemGroup.firstItemIdx != firstItemGroup.nestedItemIdx ||
             firstItemGroup.firstItemIdx == firstItemGroup.nestedItemIdx && firstItemGroup.firstItemIdx < firstItemGroup.nestedConstrainCount))
            return false;

        // TO DO: the logic here is not safety, if subitems cannot fully fill the nested item
        for (int i = 0; i < itemGroup.nestedConstrainCount; i++)
        {
            /* Remove the gameObject of the item from the scrollContent */
            GameObject oldItem = itemGroup.displaySubItemList[itemGroup.displaySubItemCount - 1 - despawnSubItemCountEnd];
            AddToSubItemDespawnList(false);

            /* Update the information for the items that are currently displaying */
            size = Mathf.Max(GetItemSize(oldItem.GetComponent<RectTransform>(), considerSpacing), size);
            availableSubItems--;
            itemGroup.lastSubItemIdx--;

            if (itemGroup.lastSubItemIdx % itemGroup.nestedConstrainCount == 0 || availableSubItems == 0)
                break;
        }

        /* Update the parameter of the scrollContent UI */
        if (reverseDirection)
        {
            Vector2 offset = GetVector2(size);
            //parent.GetComponent<RectTransform>().anchoredPosition += offset;
            scrollContentRect.anchoredPosition += offset;
            prevPos += offset;
            contentStartPos += offset;
        }

        if (size != 0)
        {
            ClearSubItemDespawnList(itemGroup);
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(scrollContentRect);
        }

        return true;
    }

    public bool RemoveItemGroupAtStart()
    {
        if (firstItemGroupIdx >= itemGroupCount - 1)
            return false;

        var currentItemGroup = displayItemGroupList[0];
        if (currentItemGroup.firstItemIdx < currentItemGroup.itemCount)
            return false;

        displayItemGroupList.RemoveAt(0);
        firstItemGroupIdx++;

        return true;
    }

    public bool RemoveItemGroupAtEnd()
    {
        if (lastItemGroupIdx <= 0)
            return false;

        var currentItemGroup = displayItemGroupList[displayItemGroupCount - 1];
        if (currentItemGroup.lastItemIdx > 0)
            return false;

        displayItemGroupList.RemoveAt(displayItemGroupCount - 1);
        lastItemGroupIdx--;

        return true;
    }

    #endregion

    #endregion


    #region 对象池操作

    protected void DespawnItem(GameObject obj)
    {
        ItemPoolMember pm = obj.GetComponent<ItemPoolMember>();
        if (pm == null || itemPoolDict.ContainsKey(pm.prefab) == false)
        {
            Debug.LogFormat(" Object '{0}' wasn't spawned from a pool. Destroying it instead. ", obj.name);
            Destroy(obj);
        }
        else
        {
            obj.gameObject.SetActive(false);

            if (!itemPoolDict[pm.prefab].Contains(obj))
                itemPoolDict[pm.prefab].Push(obj);
        }
    }

    public GameObject SpawnItem(GameObject prefab)
    {
        GameObject obj = null;

        if (!itemPoolDict.ContainsKey(prefab))
        {
            obj = Instantiate(prefab) as GameObject;
            obj.AddComponent<ItemPoolMember>().prefab = prefab;

            itemPoolDict.Add(prefab, new Stack<GameObject>());
        }
        else if (itemPoolDict[prefab].Count == 0)
        {
            obj = Instantiate(prefab) as GameObject;
            obj.AddComponent<ItemPoolMember>().prefab = prefab;
        }
        else
        {
            obj = itemPoolDict[prefab].Pop();

            if (obj == null)
                obj = SpawnItem(prefab);
        }

        obj.gameObject.SetActive(true);
        return obj;
    }

    public void ClearPool()
    {
        foreach (Stack<GameObject> pool in itemPoolDict.Values)
        {
            pool.Clear();
        }

        itemPoolDict.Clear();
    }

    public void AddToItemDespawnList(bool fromStart, int count = 1)
    {
        if (fromStart)
            despawnItemCountStart += count;
        else
            despawnItemCountEnd += count;
    }

    public void ClearItemDespawnList()
    {
        Debug.Assert(scrollContentRect.childCount >= despawnItemCountStart + despawnItemCountEnd);
        if (despawnItemCountStart > 0)
        {
            for (int i = 1; i <= despawnItemCountStart; i++)
            {
                GameObject item = displayItemList[0];
                displayItemList.RemoveAt(0);
                DespawnItem(item);
            }
            despawnItemCountStart = 0;
        }
        if (despawnItemCountEnd > 0)
        {
            for (int i = 1; i <= despawnItemCountEnd; i++)
            {
                GameObject item = displayItemList[displayItemCount - 1];
                displayItemList.RemoveAt(displayItemCount - 1);
                DespawnItem(item);
            }
            despawnItemCountEnd = 0;
        }
    }

    public void ClearItemDespawnList(ItemGroupConfig itemGroup)
    {
        Debug.Assert(itemGroup.displayItemCount >= despawnItemCountStart + despawnItemCountEnd);
        if (despawnItemCountStart > 0)
        {
            for (int i = 1; i <= despawnItemCountStart; i++)
            {
                GameObject item = itemGroup.displayItemList[0];
                itemGroup.displayItemList.RemoveAt(0);
                displayItemList.RemoveAt(0);
                DespawnItem(item);
            }
            despawnItemCountStart = 0;
        }
        if (despawnItemCountEnd > 0)
        {
            for (int i = 1; i <= despawnItemCountEnd; i++)
            {
                GameObject item = itemGroup.displayItemList[itemGroup.displayItemCount - 1];
                itemGroup.displayItemList.RemoveAt(itemGroup.displayItemCount - 1);
                displayItemList.RemoveAt(displayItemCount - 1);
                DespawnItem(item);
            }
            despawnItemCountEnd = 0;
        }
    }

    public void AddToSubItemDespawnList(bool fromStart, int count = 1)
    {
        if (fromStart)
            despawnSubItemCountStart += count;
        else
            despawnSubItemCountEnd += count;
    }

    public void ClearSubItemDespawnList(ItemGroupConfig itemGroup)
    {
        Debug.Assert(itemGroup.displaySubItemCount >= despawnSubItemCountStart + despawnSubItemCountEnd);
        if (despawnSubItemCountStart > 0)
        {
            for (int i = 1; i <= despawnSubItemCountStart; i++)
            {
                GameObject item = itemGroup.displaySubItemList[0];
                itemGroup.displaySubItemList.RemoveAt(0);
                DespawnItem(item);
            }
            despawnSubItemCountStart = 0;
        }
        if (despawnSubItemCountEnd > 0)
        {
            for (int i = 1; i <= despawnSubItemCountEnd; i++)
            {
                GameObject item = itemGroup.displaySubItemList[itemGroup.displaySubItemCount - 1];
                itemGroup.displaySubItemList.RemoveAt(itemGroup.displaySubItemCount - 1);
                DespawnItem(item);
            }
            despawnSubItemCountEnd = 0;
        }
    }

    #endregion


    #region 工具类函数

    #region 未归类

    protected void SetDirty()
    {
        if (!IsActive())
            return;

        LayoutRebuilder.MarkLayoutForRebuild(rect);
    }

    protected void SetDirtyCaching()
    {
        if (!IsActive())
            return;

        //CanvasUpdateRegistry.RegisterCanvasElementForLayoutRebuild(this);
        LayoutRebuilder.MarkLayoutForRebuild(rect);
    }

    public virtual void StopMovement()
    {
        velocity = Vector2.zero;
    }

    private void EnsureLayoutHasRebuilt()
    {
        if (!CanvasUpdateRegistry.IsRebuildingLayout())
            Canvas.ForceUpdateCanvases();
    }

    #endregion


    #region 私有成员赋值&计算相关

    private void SetScrollBar(UnityAction<float> callBack, ref Scrollbar scrollbar, ref RectTransform scrollbarRect)
    {
        if (scrollbar)
        {
            scrollbar.onValueChanged.RemoveAllListeners();
            scrollbar.onValueChanged.AddListener(callBack);
            scrollbarRect = scrollbar.GetComponent<RectTransform>();
        }
    }

    private int GetLayoutConstraintCount()
    {
        if (layoutConstrainCount != -1)
            return layoutConstrainCount;

        layoutConstrainCount = 1;
        if (scrollContentRect != null)
        {
            GridLayoutGroup layout = scrollContentRect.GetComponent<GridLayoutGroup>();
            if (layout != null)
            {
                if (layout.constraint == GridLayoutGroup.Constraint.Flexible)
                    Debug.LogError("[LoopScrollRect] Flexible not supported yet");
                layoutConstrainCount = layout.constraintCount;
            }
        }
        return layoutConstrainCount;
    }

    private float GetItemSpacing()
    {
        if (itemSpacing != -1)
            return itemSpacing;

        itemSpacing = 0;
        if (scrollContentRect != null)
        {
            HorizontalOrVerticalLayoutGroup layout = scrollContentRect.GetComponent<HorizontalOrVerticalLayoutGroup>();
            if (layout != null)
            {
                itemSpacing = layout.spacing;
                contentLeftPadding = layout.padding.left;
                contentRightPadding = layout.padding.right;
                contentTopPadding = layout.padding.top;
                contentDownPadding = layout.padding.bottom;
            }
            gridLayout = scrollContentRect.GetComponent<GridLayoutGroup>();
            if (gridLayout != null)
            {
                itemSpacing = GetAbsDimension(gridLayout.spacing);
                contentLeftPadding = gridLayout.padding.left;
                contentRightPadding = gridLayout.padding.right;
                contentTopPadding = gridLayout.padding.top;
                contentDownPadding = gridLayout.padding.bottom;
            }
        }
        return itemSpacing;
    }

    private float GetSubItemSpacing(RectTransform subContentRect)
    {
        float spacing = 0f;
        if (subContentRect != null)
        {
            var layout1 = subContentRect.GetComponent<HorizontalOrVerticalLayoutGroup>();
            var layout2 = subContentRect.GetComponent<GridLayoutGroup>();

            if (layout1 != null)
                spacing = layout1.spacing;

            if (layout2 != null)
                spacing = GetAbsDimension(layout2.spacing);
        }
        else
        {
            Debug.LogError("[GetSubItemSpacing] subContentRect does not exist! ");
        }
        return spacing;
    } 

    private void UpdatePrevData()
    {
        if (scrollContentRect == null)
            prevPos = Vector2.zero;
        else
            prevPos = scrollContentRect.anchoredPosition;
        prevScrollViewBounds = scrollViewBounds;
        prevScrollContentBounds = scrollContentBounds;
    }

    #endregion


    #region 通用UI计算相关

    protected virtual float GetItemSize(RectTransform itemRect, bool considerSpacing)
    {
        float size = considerSpacing ? itemSpacing : 0;

        if (vertical && !horizontal)
        {
            if (gridLayout != null)
                size += gridLayout.cellSize.y;
            else
                size += itemRect.rect.height;
        }
        else
        {
            if (gridLayout != null)
                size += gridLayout.cellSize.x;
            else
                size += itemRect.rect.width;
        }

        if (vertical && !horizontal)
            size *= scrollContentRect.localScale.y;
        else
            size *= scrollContentRect.localScale.x;

        return size;
    }

    protected virtual float GetSubItemSize(RectTransform subItemRect, RectTransform subContentRect, bool considerSpacing)
    {
        float size = considerSpacing ? GetSubItemSpacing(subContentRect) : 0;
        var layout = subContentRect.GetComponent<GridLayoutGroup>();

        /* calcualte item size */
        if (vertical && !horizontal)
        {
            if (layout != null)
                size += layout.cellSize.y;
            else
                size += subItemRect.rect.height;
        }
        else
        {
            if (layout != null)
                size += layout.cellSize.x;
            else
                size += subItemRect.rect.width;
        }

        size *= GetAbsDimension(subContentRect.localScale);     // TO DO

        if (vertical && !horizontal)
            size *= subContentRect.localScale.y;
        else
            size *= subContentRect.localScale.x;

        return size;
    }

    protected virtual Vector2 GetVector2(float value)
    {
        if (vertical && !horizontal)
            return new Vector2(0, value);
        else
            return new Vector2(-value, 0);
    }

    protected virtual float GetDimension(Vector2 vector)
    {
        if (vertical && !horizontal)
            return vector.y;
        else
            return -vector.x;
    }

    protected virtual float GetAbsDimension(Vector2 vector)
    {
        if (vertical && !horizontal)
            return vector.y;
        else
            return vector.x;
    }

    private static float RubberDelta(float overStretching, float viewSize)
    {
        return (1 - (1 / ((Mathf.Abs(overStretching) * 0.55f / viewSize) + 1))) * viewSize * Mathf.Sign(overStretching);
    }

    protected virtual void SetContentAnchoredPosition(Vector2 position)
    {
        if (!horizontal)
            position.x = scrollContentRect.anchoredPosition.x;
        if (!vertical)
            position.y = scrollContentRect.anchoredPosition.y;

        if ((position - scrollContentRect.anchoredPosition).sqrMagnitude > 0.001f)
        {
            scrollContentRect.anchoredPosition = position;
            UpdateBounds(true);
        }
    }

    private Vector2 CalculateOffset(Vector2 delta)
    {
        Vector2 offset = Vector2.zero;
        if (movementType == ScrollMovementType.Unrestricted)
            return offset;
        if (movementType == ScrollMovementType.Clamped)
        {
            if (itemCount < 0)
                return offset;
            if (GetDimension(delta) < 0 && firstItemIdx > 0)            // cannot continue move down if already reach the top of scrollContent
                return offset;
            if (GetDimension(delta) > 0 && lastItemIdx < itemCount)     // cannot continue move up if already reach the top of scrollContent
                return offset;
        }

        Vector2 min = scrollContentBounds.min;
        Vector2 max = scrollContentBounds.max;

        if (horizontal)
        {
            min.x += delta.x;
            max.x += delta.x;
            if (min.x > scrollViewBounds.min.x)
                offset.x = scrollViewBounds.min.x - min.x;
            else if (max.x < scrollViewBounds.max.x)
                offset.x = scrollViewBounds.max.x - max.x;
        }

        if (vertical)
        {
            min.y += delta.y;
            max.y += delta.y;
            if (max.y < scrollViewBounds.max.y)
                offset.y = scrollViewBounds.max.y - max.y;
            else if (min.y > scrollViewBounds.min.y)
                offset.y = scrollViewBounds.min.y - min.y;
        }

        return offset;
    }

    #endregion


    #region scrollview跳转相关

    public int GetFirstItem(out float offset)
    {
        //if (direction == LoopScrollRectDirection.Vertical)
        if (vertical)
            offset = scrollViewBounds.max.y - scrollContentBounds.max.y;
        else
            offset = scrollContentBounds.min.x - scrollViewBounds.min.x;
        int idx = 0;
        if (lastItemIdx > firstItemIdx)
        {
            float size = GetItemSize(displayItemList[0].GetComponent<RectTransform>(), false);
            while (size + offset <= 0 && firstItemIdx + idx + layoutConstrainCount < lastItemIdx)
            {
                offset += size;
                idx += layoutConstrainCount;
                size = GetItemSize(displayItemList[idx].GetComponent<RectTransform>(), true);
            }
        }
        return idx + firstItemIdx;
    }

    public int GetLastItem(out float offset)
    {
        //if (direction == LoopScrollRectDirection.Vertical)
        if (vertical)
            offset = scrollContentBounds.min.y - scrollViewBounds.min.y;
        else
            offset = scrollViewBounds.max.x - scrollContentBounds.max.x;
        int idx = 0;
        if (lastItemIdx > firstItemIdx)
        {
            int currItemCount = displayItemCount;
            float size = GetItemSize(displayItemList[currItemCount - 1 - idx].GetComponent<RectTransform>(), false);
            while (size + offset <= 0 && firstItemIdx < lastItemIdx - idx - layoutConstrainCount)
            {
                offset += size;
                idx += layoutConstrainCount;
                size = GetItemSize(displayItemList[currItemCount - 1 - idx].GetComponent<RectTransform>(), true);
            }
        }
        offset = -offset;
        return lastItemIdx - idx - 1;
    }

    public void SrollToCell(int index, float speed)
    {
        if (itemCount >= 0 && (index < 0 || index >= itemCount))
        {
            Debug.LogErrorFormat("invalid index {0}", index);
            return;
        }
        StopAllCoroutines();
        if (speed <= 0)
        {
            RefillItems(index);
            return;
        }
        StartCoroutine(ScrollToCellCoroutine(index, speed));
    }

    public void SrollToCellWithinTime(int index, float time)
    {
        if (itemCount >= 0 && (index < 0 || index >= itemCount))
        {
            Debug.LogErrorFormat("invalid index {0}", index);
            return;
        }
        StopAllCoroutines();
        if (time <= 0)
        {
            RefillItems(index);
            return;
        }
        float dist = 0;
        float offset = 0;
        int currentFirst = reverseDirection ? GetLastItem(out offset) : GetFirstItem(out offset);

        int targetLine = (index / layoutConstrainCount);
        int currentLine = (currentFirst / layoutConstrainCount);

        if (targetLine == currentLine)
        {
            dist = offset;
        }
        else
        {
            //if (sizeHelper != null)
            //{
            //    dist = GetDimension(sizeHelper.GetItemsSize(currentFirst) - sizeHelper.GetItemsSize(index));
            //    dist += offset;
            //}
            //else
            float elementSize = (GetAbsDimension(scrollContentBounds.size) - itemSpacing * (currentLine - 1)) / currentLine;
            dist = elementSize * (currentLine - targetLine) + itemSpacing * (currentLine - targetLine - 1);
            dist -= offset;
        }
        StartCoroutine(ScrollToCellCoroutine(index, Mathf.Abs(dist) / time));
    }

    IEnumerator ScrollToCellCoroutine(int index, float speed)
    {
        bool needMoving = true;
        while (needMoving)
        {
            yield return null;
            if (!isDragging)
            {
                float move = 0;
                if (index < firstItemIdx)
                    move = -Time.deltaTime * speed;
                else if (index >= lastItemIdx)
                    move = Time.deltaTime * speed;
                else
                {
                    scrollViewBounds = new Bounds(scrollViewRect.rect.center, scrollViewRect.rect.size);
                    var itemBounds = CalculateItemBounds(index);
                    var offset = 0.0f;
                    //if (direction == LoopScrollRectDirection.Vertical)
                    if (vertical)
                        offset = reverseDirection ? (scrollViewBounds.min.y - itemBounds.min.y) : (scrollViewBounds.max.y - itemBounds.max.y);
                    else
                        offset = reverseDirection ? (itemBounds.max.x - scrollViewBounds.max.x) : (itemBounds.min.x - scrollViewBounds.min.x);
                    /* check if we cannot move on */
                    if (itemCount >= 0)
                    {
                        if (offset > 0 && lastItemIdx == itemCount && !reverseDirection)
                        {
                            itemBounds = CalculateItemBounds(itemCount - 1);
                            /* reach bottom */
                            //if ((direction == LoopScrollRectDirection.Vertical && m_ItemBounds.min.y > scrollViewBounds.min.y) ||
                            //    (direction == LoopScrollRectDirection.Horizontal && m_ItemBounds.max.x < scrollViewBounds.max.x))
                            if ((vertical && itemBounds.min.y > scrollViewBounds.min.y) ||
                                (horizontal && itemBounds.max.x < scrollViewBounds.max.x))
                            {
                                needMoving = false;
                                break;
                            }
                        }
                        else if (offset < 0 && firstItemIdx == 0 && reverseDirection)
                        {
                            itemBounds = CalculateItemBounds(0);
                            //if ((direction == LoopScrollRectDirection.Vertical && m_ItemBounds.max.y < scrollViewBounds.max.y) ||
                            //    (direction == LoopScrollRectDirection.Horizontal && m_ItemBounds.min.x > scrollViewBounds.min.x))
                            if ((vertical && itemBounds.max.y < scrollViewBounds.max.y) ||
                                (horizontal && itemBounds.min.x > scrollViewBounds.min.x))
                            {
                                needMoving = false;
                                break;
                            }
                        }
                    }

                    float maxMove = Time.deltaTime * speed;
                    if (Mathf.Abs(offset) < maxMove)
                    {
                        needMoving = false;
                        move = offset;
                    }
                    else
                        move = Mathf.Sign(offset) * maxMove;
                }
                if (move != 0)
                {
                    Vector2 offset = GetVector2(move);
                    scrollContentRect.anchoredPosition += offset;
                    prevPos += offset;
                    contentStartPos += offset;
                    UpdateBounds(true);
                }
            }
        }
        StopMovement();
        UpdatePrevData();
    }

    #endregion


    #region scrollbar计算相关

    private float GetHorizontalNormalizedPosition()
    {
        UpdateBounds();
        if (itemCount > 0 && lastItemIdx > firstItemIdx)
        {
            float totalSize, offset;
            GetHorizonalOffsetAndSize(out totalSize, out offset);

            if (totalSize <= scrollViewBounds.size.x)
                return (scrollViewBounds.min.x > offset) ? 1 : 0;
            return (scrollViewBounds.min.x - offset) / (totalSize - scrollViewBounds.size.x);
        }
        else
            return 0.5f;
    }

    private float GetVerticalNormalizedPosition()
    {
        UpdateBounds();
        if (itemCount > 0 && lastItemIdx > firstItemIdx)
        {
            float totalSize, offset;
            GetVerticalOffsetAndSize(out totalSize, out offset);

            if (totalSize <= scrollViewBounds.size.y)
                return (offset > scrollViewBounds.max.y) ? 1 : 0;
            return (offset - scrollViewBounds.max.y) / (totalSize - scrollViewBounds.size.y);
        }
        else
            return 0.5f;
    }

    private void SetHorizontalNormalizedPosition(float value) 
    { 
        SetNormalizedPosition(value, 0); 
    }
    
    private void SetVerticalNormalizedPosition(float value) 
    { 
        SetNormalizedPosition(value, 1); 
    }

    private void SetNormalizedPosition(float value, int axis)
    {
        if (itemCount <= 0 || lastItemIdx <= firstItemIdx)
            return;

        EnsureLayoutHasRebuilt();
        UpdateBounds();

        Vector3 localPos = scrollContentRect.localPosition;
        float newLocalPos = localPos[axis];
        if (axis == 0)
        {
            float totalSize, offset;
            GetHorizonalOffsetAndSize(out totalSize, out offset);

            newLocalPos += scrollViewBounds.min.x - value * (totalSize - scrollViewBounds.size[axis]) - offset;
        }
        else if (axis == 1)
        {
            float totalSize, offset;
            GetVerticalOffsetAndSize(out totalSize, out offset);

            newLocalPos -= offset - value * (totalSize - scrollViewBounds.size.y) - scrollViewBounds.max.y;
        }

        if (Mathf.Abs(localPos[axis] - newLocalPos) > 0.01f)
        {
            localPos[axis] = newLocalPos;
            scrollContentRect.localPosition = localPos;
            velocity[axis] = 0;
            UpdateBounds(true);
        }
    }

    private void GetHorizonalOffsetAndSize(out float totalSize, out float offset)
    {
        //if (sizeHelper != null)
        //{
        //    totalSize = sizeHelper.GetItemsSize(TotalLines).x;
        //    offset = m_ContentBounds.min.x - sizeHelper.GetItemsSize(StartLine).x - contentSpacing * StartLine;
        //}
        //else
        float elementSize = (scrollContentBounds.size.x - itemSpacing * (currentLines - 1)) / currentLines;
        totalSize = elementSize * totalLines + itemSpacing * (totalLines - 1);
        offset = scrollContentBounds.min.x - (elementSize + itemSpacing) * startLine;
    }

    private void GetVerticalOffsetAndSize(out float totalSize, out float offset)
    {
        //if (sizeHelper != null)
        //{
        //    totalSize = sizeHelper.GetItemsSize(TotalLines).y;
        //    offset = m_ContentBounds.max.y + sizeHelper.GetItemsSize(StartLine).y + contentSpacing * StartLine;
        //}
        //else
        float elementSize = (scrollContentBounds.size.y - itemSpacing * (currentLines - 1)) / currentLines;
        totalSize = elementSize * totalLines + itemSpacing * (totalLines - 1);
        offset = scrollContentBounds.max.y + (elementSize + itemSpacing) * startLine;
    }

    private void UpdateScrollbars(Vector2 offset)
    {
        if (horizontalScrollbar)
        {
            if (scrollContentBounds.size.x > 0 && itemCount > 0)
            {
                float totalSize, _;
                GetHorizonalOffsetAndSize(out totalSize, out _);
                horizontalScrollbar.size = Mathf.Clamp01((scrollViewBounds.size.x - Mathf.Abs(offset.x)) / totalSize);
            }
            else
                horizontalScrollbar.size = 1;

            horizontalScrollbar.value = horizontalNormalizedPosition;
        }

        if (verticalScrollbar)
        {
            if (scrollContentBounds.size.y > 0 && itemCount > 0)
            {
                float totalSize, _;
                GetVerticalOffsetAndSize(out totalSize, out _);
                verticalScrollbar.size = Mathf.Clamp01((scrollViewBounds.size.y - Mathf.Abs(offset.y)) / totalSize);
            }
            else
                verticalScrollbar.size = 1;

            verticalScrollbar.value = verticalNormalizedPosition;
        }
    }

    void UpdateScrollbarVisibility()
    {
        if (verticalScrollbar && verticalScrollbarVisibility != ScrollbarVisibility.Permanent && verticalScrollbar.gameObject.activeSelf != verticalScrollingNeeded)
            verticalScrollbar.gameObject.SetActive(verticalScrollingNeeded);
        if (horizontalScrollbar && horizontalScrollbarVisibility != ScrollbarVisibility.Permanent && horizontalScrollbar.gameObject.activeSelf != horizontalScrollingNeeded)
            horizontalScrollbar.gameObject.SetActive(horizontalScrollingNeeded);
    }

    #endregion


    #region scrollitem相关

    ///* 该函数的用途暂时不明 */
    //public virtual void Rebuild(CanvasUpdate executing)
    //{
    //    if (executing == CanvasUpdate.Prelayout)
    //    {
    //        UpdateCachedData();
    //    }

    //    if (executing == CanvasUpdate.PostLayout)
    //    {
    //        UpdateBounds();
    //        UpdateScrollbars(Vector2.zero);
    //        UpdatePrevData();

    //        m_HasRebuiltLayout = true;
    //    }
    //}

    ///* 该函数的用途暂时不明 */
    //public void RefreshCells()
    //{
    //    if (Application.isPlaying && this.isActiveAndEnabled)
    //    {
    //        lastItemIdx = firstItemIdx;
    //        // recycle items if we can
    //        for (int i = 0; i < scrollContentRect.childCount; i++)
    //        {
    //            if (lastItemIdx < itemCount)
    //            {
    //                ProvideData(scrollContent.GetChild(i), itemTypeEnd);
    //                lastItemIdx++;
    //            }
    //            else
    //            {
    //                prefabSource.ReturnObject(scrollContent.GetChild(i));
    //                i--;
    //            }
    //        }
    //        UpdateBounds(true);
    //        UpdateScrollbars(Vector2.zero);
    //    }
    //}

    public void RefillItemsFromEnd(int endItem = 0, bool alignStart = false)
    {
        if (!Application.isPlaying)
            return;

        lastItemIdx = reverseDirection ? endItem : itemCount - endItem;
        firstItemIdx = lastItemIdx;

        if (itemCount >= 0 && firstItemIdx % layoutConstrainCount != 0)
        {
            firstItemIdx = (firstItemIdx / layoutConstrainCount) * layoutConstrainCount;
        }

        AddToItemDespawnList(!reverseDirection, lastItemIdx - firstItemIdx + 1);
        //ReturnToTempPool(!reverseDirection, m_Content.childCount);

        float sizeToFill = GetAbsDimension(scrollViewRect.rect.size);
        float sizeFilled = 0;

        bool first = true;
        while (sizeToFill > sizeFilled)
        {
            float size = 0f;
            bool addSuccess = reverseDirection ? AddItemAtEnd(out size, !first, item, scrollContent): AddItemAtStart(out size, !first, item, scrollContent);
            if (!addSuccess)
                break;
            first = false;
            sizeFilled += size;
        }
        /* refill from start in case not full yet */
        while (sizeToFill > sizeFilled)
        {
            float size = 0f;
            bool addSuccess = reverseDirection ? AddItemAtStart(out size, !first, item, scrollContent) : AddItemAtEnd(out size, !first, item, scrollContent);
            if (!addSuccess)
                break;
            first = false;
            sizeFilled += size;
        }

        Vector2 pos = scrollContentRect.anchoredPosition;
        float dist = alignStart ? 0 : Mathf.Max(0, sizeFilled - sizeToFill);
        if (reverseDirection)
            dist = -dist;
        //if (direction == LoopScrollRectDirection.Vertical)
        if (vertical)
            pos.y = dist;
        else
            pos.x = -dist;
        scrollContentRect.anchoredPosition = pos;
        contentStartPos = pos;

        ClearItemDespawnList();
        //ClearTempPool();

        /* force build bounds here so scrollbar can access newest bounds */
        LayoutRebuilder.ForceRebuildLayoutImmediate(scrollContentRect);
        CalculateContentBounds();
        UpdateScrollbars(Vector2.zero);
        StopMovement();
        UpdatePrevData();         /* 该函数的用途暂时不明 */
    }


    public void RefillItemGroup(out float sizeFilled, ItemGroupConfig itemGroup, int startItem = 0, bool fillViewRect = false, float contentOffset = 0)
    {
        sizeFilled = 0;

        if (!Application.isPlaying)
            return;

        itemGroup.firstItemIdx = reverseDirection ? itemGroup.itemCount - startItem : startItem;
        if (itemGroup.itemCount >= 0 && itemGroup.firstItemIdx % layoutConstrainCount != 0)
        {
            itemGroup.firstItemIdx = (itemGroup.firstItemIdx / layoutConstrainCount) * layoutConstrainCount;
        }
        itemGroup.lastItemIdx = itemGroup.firstItemIdx;

        /* Don't `Canvas.ForceUpdateCanvases();` here, or it will new/delete cells to change itemTypeStart/End */
        //AddToItemDespawnList(reverseDirection, scrollContentRect.childCount);
        AddToItemDespawnList(reverseDirection, itemGroup.displayItemCount);

        /* scrollViewBounds may be not ready when RefillItems on Start */
        float sizeToFill = GetAbsDimension(scrollViewRect.rect.size) + Mathf.Abs(contentOffset);
        float itemSize = 0;
        float size = 0;
        bool first = true;
        while (sizeToFill > sizeFilled)
        {
            bool addSuccess = false;
            GameObject prefab = reverseDirection ? itemGroup.itemList[itemGroup.firstItemIdx - 1] : itemGroup.itemList[itemGroup.lastItemIdx];

            if (prefab != itemGroup.itemList[itemGroup.nestedItemIdx])
            {
                addSuccess = reverseDirection ? AddItemAtStart(out size, !first, prefab, scrollContent, itemGroup) : AddItemAtEnd(out size, !first, prefab, scrollContent, itemGroup);
                if (!addSuccess)
                    break;
                first = false;
                itemSize = size;
                sizeFilled += size;
            }
            else
            {
                addSuccess = reverseDirection ? AddItemAtStart(out _, !first, prefab, scrollContent, itemGroup) : AddItemAtEnd(out _, !first, prefab, scrollContent, itemGroup);
                if (!addSuccess)
                    break;

                RefillSubItems(out float nestedItemSize, itemGroup.subItem, displayItemList[displayItemCount - 1], itemGroup, sizeToFill - sizeFilled);

                first = false;
                itemSize = nestedItemSize;
                sizeFilled += nestedItemSize;
            }
        }
        /* refill from start in case not full yet */
        while (sizeToFill > sizeFilled)
        {
            bool addSuccess = false;
            GameObject prefab = reverseDirection ? itemGroup.itemList[itemGroup.lastItemIdx] : itemGroup.itemList[itemGroup.firstItemIdx - 1];

            if (prefab != itemGroup.itemList[itemGroup.nestedItemIdx])
            {
                addSuccess = reverseDirection ? AddItemAtEnd(out size, !first, prefab, scrollContent, itemGroup) : AddItemAtStart(out size, !first, prefab, scrollContent, itemGroup);
                if (!addSuccess)
                    break;
                first = false;
                sizeFilled += size;
            }
            else
            {
                addSuccess = reverseDirection ? AddItemAtEnd(out _, !first, prefab, scrollContent, itemGroup) : AddItemAtStart(out _, !first, prefab, scrollContent, itemGroup);
                if (!addSuccess)
                    break;

                RefillSubItems(out float subContentSize, itemGroup.subItem, displayItemList[0], itemGroup, sizeToFill - sizeFilled);

                first = false;
                itemSize = subContentSize;
                sizeFilled += subContentSize;
            }
        }

        if (fillViewRect && itemSize > 0 && sizeFilled < sizeToFill)
        {
            int itemsToAddCount = (int)((sizeToFill - sizeFilled) / itemSize);                          /* calculate how many items can be added above the offset, so it still is visible in the view */
            int newOffset = startItem - itemsToAddCount;
            if (newOffset < 0) newOffset = 0;
            if (newOffset != startItem) RefillItemGroup(out sizeFilled, itemGroup, newOffset);          /* refill again, with the new offset value, and now with fillViewRect disabled. */
        }

        if (sizeFilled > 0 && displayItemGroupList.Contains(itemGroup) == false)
        {
            displayItemGroupList.Add(itemGroup);
            lastItemGroupIdx++;
        }

        Vector2 pos = scrollContentRect.anchoredPosition;
        if (vertical)
            pos.y = -contentOffset;
        else
            pos.x = contentOffset;
        scrollContentRect.anchoredPosition = pos;
        contentStartPos = pos;

        //ClearItemDespawnList();
        ClearItemDespawnList(itemGroup);

        /* force build bounds here so scrollbar can access newest bounds */
        LayoutRebuilder.ForceRebuildLayoutImmediate(scrollContentRect);
        CalculateContentBounds();
        UpdateScrollbars(Vector2.zero);
        StopMovement();
        UpdatePrevData();             /* 该函数的用途暂时不明 */
    }


    public void RefillSubItems(out float sizeFilled, GameObject prefab, GameObject parent, ItemGroupConfig itemGroup, float sizeToFill = 0, int startSubItem = 0, bool fillViewRect = false, float contentOffset = 0)
    {
        float subItemSize = 0f;
        bool first = true;
        sizeFilled = 0f;

        while (sizeToFill > sizeFilled)
        {
            float size = 0f;
            bool addSuccess = reverseDirection ? AddSubItemAtStart(out size, !first, prefab, parent, itemGroup) : AddSubItemAtEnd(out size, !first, prefab, parent, itemGroup);
            if (!addSuccess)
                break;

            first = false;
            subItemSize = size;
            sizeFilled += size;
        }
        /* refill from start in case not full yet */
        while (sizeToFill > sizeFilled)
        {
            float size = 0f;
            bool addSuccess = reverseDirection ? AddSubItemAtEnd(out size, !first, prefab, parent, itemGroup) : AddSubItemAtStart(out size, !first, prefab, parent, itemGroup);
            if (!addSuccess)
                break;

            first = false;
            sizeFilled += size;
        }

        if (fillViewRect && subItemSize > 0 && sizeFilled < sizeToFill)
        {
            int itemsToAddCount = (int)((sizeToFill - sizeFilled) / subItemSize);                       /* calculate how many items can be added above the offset, so it still is visible in the view */
            int newOffset = startSubItem - itemsToAddCount;
            if (newOffset < 0) 
                newOffset = 0;
            if (newOffset != startSubItem) 
                RefillSubItems(out sizeFilled, prefab, parent, itemGroup, sizeToFill, newOffset);      /* refill again, with the new offset value, and now with fillViewRect disabled. */
        }

        Vector2 pos = scrollContentRect.anchoredPosition;
        //if (direction == LoopScrollRectDirection.Vertical)
        if (vertical)
            pos.y = -contentOffset;
        else
            pos.x = contentOffset;
        scrollContentRect.anchoredPosition = pos;
        contentStartPos = pos;

        /* force build bounds here so scrollbar can access newest bounds */
        LayoutRebuilder.ForceRebuildLayoutImmediate(prefab.GetComponent<RectTransform>());
        LayoutRebuilder.ForceRebuildLayoutImmediate(scrollContentRect);
        CalculateContentBounds();
        UpdateScrollbars(Vector2.zero);
        StopMovement();
        UpdatePrevData();             /* 该函数的用途暂时不明 */
    }


    public void RefillItems(int startItem = 0, bool fillViewRect = false, float contentOffset = 0)
    {
        if (!Application.isPlaying)
            return;

        firstItemIdx = reverseDirection ? itemCount - startItem : startItem;
        if (itemCount >= 0 && firstItemIdx % layoutConstrainCount != 0)
        {
            firstItemIdx = (firstItemIdx / layoutConstrainCount) * layoutConstrainCount;
        }
        lastItemIdx = firstItemIdx;

        /* Don't `Canvas.ForceUpdateCanvases();` here, or it will new/delete cells to change itemTypeStart/End */
        AddToItemDespawnList(reverseDirection, scrollContentRect.childCount);
        //ReturnToTempPool(reverseDirection, m_Content.childCount);

        /* scrollViewBounds may be not ready when RefillCells on Start */
        float sizeToFill = GetAbsDimension(scrollViewRect.rect.size) + Mathf.Abs(contentOffset);
        float sizeFilled = 0;
        float itemSize = 0;

        bool first = true;
        while (sizeToFill > sizeFilled)
        {
            float size = 0;
            bool addSuccess = reverseDirection ? AddItemAtStart(out size, !first, item, scrollContent) : AddItemAtEnd(out size, !first, item, scrollContent);
            if (!addSuccess)
                break;
            first = false;
            itemSize = size;
            sizeFilled += size;
        }
        /* refill from start in case not full yet */
        while (sizeToFill > sizeFilled)
        {
            float size = 0;
            bool addSuccess = reverseDirection ? AddItemAtEnd(out size, !first, item, scrollContent) : AddItemAtStart(out size, !first, item, scrollContent);
            if (!addSuccess)
                break;
            first = false;
            sizeFilled += size;
        }

        if (fillViewRect && itemSize > 0 && sizeFilled < sizeToFill)
        {
            int itemsToAddCount = (int)((sizeToFill - sizeFilled) / itemSize);          //calculate how many items can be added above the offset, so it still is visible in the view
            int newOffset = startItem - itemsToAddCount;
            if (newOffset < 0) newOffset = 0;
            if (newOffset != startItem) RefillItems(newOffset);                         //refill again, with the new offset value, and now with fillViewRect disabled.
        }

        Vector2 pos = scrollContentRect.anchoredPosition;
        //if (direction == LoopScrollRectDirection.Vertical)
        if (vertical)
            pos.y = -contentOffset;
        else
            pos.x = contentOffset;
        scrollContentRect.anchoredPosition = pos;
        contentStartPos = pos;

        ClearItemDespawnList();
        //ClearTempPool();

        /* force build bounds here so scrollbar can access newest bounds */
        LayoutRebuilder.ForceRebuildLayoutImmediate(scrollContentRect);
        CalculateContentBounds();
        UpdateScrollbars(Vector2.zero);
        StopMovement();
        UpdatePrevData();             /* 该函数的用途暂时不明 */
    }

    public void UpdateScrollItems()
    {
        bool isChanged = false;

        /* special case 1: handling move several page upward in one frame */
        if (scrollViewBounds.max.y < scrollContentBounds.min.y && lastItemIdx > firstItemIdx)
        {
            int maxFirstItemIdx = -1;
            if (itemCount >= 0)
            {
                maxFirstItemIdx = Mathf.Max(0, itemCount - (lastItemIdx - firstItemIdx));
            }
            float currentSize = scrollContentBounds.size.y;
            float elementSize = (currentSize - itemSpacing * (currentLines - 1)) / currentLines;
            AddToItemDespawnList(true, lastItemIdx - firstItemIdx);
            //ReturnToTempPool(true, itemTypeEnd - itemTypeStart);

            firstItemIdx = lastItemIdx;

            int offsetCount = Mathf.FloorToInt((scrollContentBounds.min.y - scrollViewBounds.max.y) / (elementSize + itemSpacing));       /* Calculate the number of lines for the gap between scrollViewBounds and scrollContentBounds */
            if (maxFirstItemIdx >= 0 && firstItemIdx + offsetCount * layoutConstrainCount > maxFirstItemIdx)
            {
                offsetCount = Mathf.FloorToInt((float)(maxFirstItemIdx - firstItemIdx) / layoutConstrainCount);                     /* If the potential items in the gap is more than the actual items we can have, recalculate based on actual item number */
            }
            firstItemIdx += offsetCount * layoutConstrainCount;
            if (itemCount >= 0)
            {
                firstItemIdx = Mathf.Max(firstItemIdx, 0);                                                                          /* In case the head index is smaller than 0 */
            }
            lastItemIdx = firstItemIdx;

            float offset = offsetCount * (elementSize + itemSpacing);
            scrollContentRect.anchoredPosition -= new Vector2(0, offset + (reverseDirection ? 0 : currentSize));
            scrollContentBounds.center -= new Vector3(0, offset + currentSize / 2, 0);
            scrollContentBounds.size = Vector3.zero;
            isChanged = true;
        }

        /* special case 2: handling move several page downward in one frame */
        if (scrollViewBounds.min.y > scrollContentBounds.max.y && lastItemIdx > firstItemIdx)
        {
            float currentSize = scrollContentBounds.size.y;
            float elementSize = (currentSize - itemSpacing * (currentLines - 1)) / currentLines;
            AddToItemDespawnList(true, lastItemIdx - firstItemIdx);
            //ReturnToTempPool(false, itemTypeEnd - itemTypeStart);

            lastItemIdx = firstItemIdx;

            int offsetCount = Mathf.FloorToInt((scrollViewBounds.min.y - scrollContentBounds.max.y) / (elementSize + itemSpacing));       /* Calculate the number of lines for the gap between scrollViewBounds and scrollContentBounds */
            if (itemCount >= 0 && firstItemIdx - offsetCount * layoutConstrainCount < 0)
            {
                offsetCount = Mathf.FloorToInt((float)(firstItemIdx) / layoutConstrainCount);                                       /* If the potential items in the gap is more than the actual items we can have, recalculate based on actual item number */
            }
            firstItemIdx -= offsetCount * layoutConstrainCount;
            if (itemCount >= 0)
            {
                firstItemIdx = Mathf.Max(firstItemIdx, 0);
            }
            lastItemIdx = firstItemIdx;

            float offset = offsetCount * (elementSize + itemSpacing);
            scrollContentRect.anchoredPosition += new Vector2(0, offset + (reverseDirection ? currentSize : 0));
            scrollContentBounds.center += new Vector3(0, offset + currentSize / 2, 0);
            scrollContentBounds.size = Vector3.zero;
            isChanged = true;
        }

        /* Case 1: the bottom of the last item is much higher than the bottom the viewPort */
        /* Need to add new items at the bottom of the scrollContent */
        if (scrollViewBounds.min.y < scrollContentBounds.min.y + contentDownPadding)
        {
            /* the last item needs to consider item spacing */
            AddItemAtEnd(out float size, true, item, scrollContent);
            float deltaSize = size;

            /* equals to (bottom of scrollContent) - (new item height + upward spacing) + offset */
            while (size > 0 && scrollViewBounds.min.y < scrollContentBounds.min.y + contentDownPadding - deltaSize)
            {
                if (AddItemAtEnd(out size, true, item, scrollContent))
                    deltaSize += size;
                else
                    break;
            }

            if (deltaSize > 0)
                isChanged = true;
        }
        //if (scrollViewBounds.min.y < scrollContentBounds.min.y + displayOffset)
        //{
        //    /* the last item needs to consider item spacing */
        //    AddItemAtEnd(true, out float size);
        //    float deltaSize = size;

        //    /* equals to (bottom of scrollContent) - (new item height + upward spacing) + offset */
        //    while (size > 0 && scrollViewBounds.min.y < scrollContentBounds.min.y - deltaSize + displayOffset)
        //    {
        //        if (AddItemAtEnd(true, out size))
        //            deltaSize += size;
        //        else
        //            break;
        //    }

        //    if (deltaSize > 0)
        //        isChanged = true;
        //}

        /* Case 2: the top of the last item is much lower than the bottom of the viewPort */
        /* Need to remove old items at the bottom of the scrollContent */
        if (scrollViewBounds.min.y > scrollContentBounds.min.y + threshold + contentDownPadding)
        {
            RemoveItemAtEnd(out float size, true);
            float deltaSize = size;

            /* equals to (the bottom of scrollContent) + (last item height + upward spacing) + (second last item height + upward spacing) + offset */
            while (size > 0 && scrollViewBounds.min.y > scrollContentBounds.min.y + threshold + contentDownPadding + deltaSize)
            {
                if (RemoveItemAtEnd(out size, true))
                    deltaSize += size;
                else
                    break;
            }

            if (deltaSize > 0)
                isChanged = true;
        }
        //if (scrollViewBounds.min.y > scrollContentBounds.min.y + (itemRect.rect.height + itemSpacing) + displayOffset)
        //{
        //    RemoveItemAtEnd(true, out float size);
        //    float deltaSize = size;

        //    /* equals to (the bottom of scrollContent) + (last item height + upward spacing) + (second last item height + upward spacing) + offset */
        //    while (size > 0 && scrollViewBounds.min.y > scrollContentBounds.min.y + deltaSize + (itemRect.rect.height + itemSpacing) + displayOffset)
        //    {
        //        if (RemoveItemAtEnd(true, out size))
        //            deltaSize += size;
        //        else
        //            break;
        //    }

        //    if (deltaSize > 0)
        //        isChanged = true;
        //}

        /* Case 3: the top of the first item is much lower than the top of the viewPort */
        /* Need to add new items at the top of the scrollContent */
        if (scrollViewBounds.max.y > scrollContentBounds.max.y - contentTopPadding)
        {
            /* need to consider downward item spacing for each item */
            AddItemAtStart(out float size, true, item, scrollContent);
            float deltaSize = size;

            /* equals to (top of scrollContent) + (new item hight + downward spacing) - offset */
            while (size > 0 && scrollViewBounds.max.y > scrollContentBounds.max.y - contentTopPadding + deltaSize)
            {
                if (AddItemAtStart(out size, true, item, scrollContent))
                    deltaSize += size;
                else
                    break;
            }

            if (deltaSize > 0)
                isChanged = true;
        }
        //if (scrollViewBounds.max.y > scrollContentBounds.max.y)
        //{
        //    /* need to consider downward item spacing for each item */
        //    AddItemAtStart(true, out float size);
        //    float deltaSize = size;

        //    /* equals to (top of scrollContent) + (new item hight + downward spacing) - offset */
        //    while (size > 0 && scrollViewBounds.max.y > scrollContentBounds.max.y + deltaSize - displayOffset)
        //    {
        //        if (AddItemAtStart(true, out size))
        //            deltaSize += size;
        //        else
        //            break;
        //    }

        //    if (deltaSize > 0)
        //        isChanged = true;
        //}

        /* Case 4: the bottom of the first item is much higher than the top of the viewPort */
        /* Need to remove old items at the top of the scrollContent */
        if (scrollViewBounds.max.y < scrollContentBounds.max.y - threshold - contentTopPadding)
        {
            /* need to consider downward item spacing for each item */
            RemoveItemAtStart(out float size, true);
            float deltaSize = size;

            /* equals to (top of scrollContent) - (first item hight + downward spacing) - (next item hight + downward spacing) - offset */
            while (size > 0 && scrollViewBounds.max.y < scrollContentBounds.max.y - threshold - contentTopPadding - deltaSize)
            {
                if (RemoveItemAtStart(out size, true))
                    deltaSize += size;
                else
                    break;
            }

            if (deltaSize > 0)
                isChanged = true;
        }
        //if (scrollViewBounds.max.y < scrollContentBounds.max.y - (itemRect.rect.height + itemSpacing) - displayOffset)
        //{
        //    /* need to consider downward item spacing for each item */
        //    RemoveItemAtStart(true, out float size);
        //    float deltaSize = size;

        //    /* equals to (top of scrollContent) - (first item hight + downward spacing) - (next item hight + downward spacing) - offset */
        //    while (size > 0 && scrollViewBounds.max.y < scrollContentBounds.max.y - deltaSize - (itemRect.rect.height + itemSpacing) - displayOffset)
        //    {
        //        if (RemoveItemAtStart(true, out size))
        //            deltaSize += size;
        //        else
        //            break;
        //    }

        //    if (deltaSize > 0)
        //        isChanged = true;
        //}

        if (isChanged)
        {
            ClearItemDespawnList();
        }
    }


    public void UpdateScrollItemGroups()
    {
        scrollBoundMax = scrollViewBounds.max;
        scrollBoundMin = scrollViewBounds.min;
        contentBoundMax = scrollContentBounds.max;
        contentBoundMin = scrollContentBounds.min;


        bool isChanged = false;

        ///* special case 1: handling move several page upward in one frame */
        //if (scrollViewBounds.max.y < scrollContentBounds.min.y && lastItemIdx > firstItemIdx)
        //{
        //    int maxFirstItemIdx = -1;
        //    if (itemCount >= 0)
        //    {
        //        maxFirstItemIdx = Mathf.Max(0, itemCount - (lastItemIdx - firstItemIdx));
        //    }
        //    float currentSize = scrollContentBounds.size.y;
        //    float elementSize = (currentSize - itemSpacing * (currentLines - 1)) / currentLines;
        //    AddToItemDespawnList(true, lastItemIdx - firstItemIdx);
        //    //ReturnToTempPool(true, itemTypeEnd - itemTypeStart);

        //    firstItemIdx = lastItemIdx;

        //    int offsetCount = Mathf.FloorToInt((scrollContentBounds.min.y - scrollViewBounds.max.y) / (elementSize + itemSpacing));     /* Calculate the number of lines for the gap between scrollViewBounds and scrollContentBounds */
        //    if (maxFirstItemIdx >= 0 && firstItemIdx + offsetCount * layoutConstrainCount > maxFirstItemIdx)
        //    {
        //        offsetCount = Mathf.FloorToInt((float)(maxFirstItemIdx - firstItemIdx) / layoutConstrainCount);                         /* If the potential items in the gap is more than the actual items we can have, recalculate based on actual item number */
        //    }
        //    firstItemIdx += offsetCount * layoutConstrainCount;
        //    if (itemCount >= 0)
        //    {
        //        firstItemIdx = Mathf.Max(firstItemIdx, 0);                                                                              /* In case the head index is smaller than 0 */
        //    }
        //    lastItemIdx = firstItemIdx;

        //    float offset = offsetCount * (elementSize + itemSpacing);
        //    scrollContentRect.anchoredPosition -= new Vector2(0, offset + (reverseDirection ? 0 : currentSize));
        //    scrollContentBounds.center -= new Vector3(0, offset + currentSize / 2, 0);
        //    scrollContentBounds.size = Vector3.zero;
        //    isChanged = true;
        //}

        ///* special case 2: handling move several page downward in one frame */
        //if (scrollViewBounds.min.y > scrollContentBounds.max.y && lastItemIdx > firstItemIdx)
        //{
        //    float currentSize = scrollContentBounds.size.y;
        //    float elementSize = (currentSize - itemSpacing * (currentLines - 1)) / currentLines;
        //    AddToItemDespawnList(true, lastItemIdx - firstItemIdx);
        //    //ReturnToTempPool(false, itemTypeEnd - itemTypeStart);

        //    lastItemIdx = firstItemIdx;

        //    int offsetCount = Mathf.FloorToInt((scrollViewBounds.min.y - scrollContentBounds.max.y) / (elementSize + itemSpacing));         /* Calculate the number of lines for the gap between scrollViewBounds and scrollContentBounds */
        //    if (itemCount >= 0 && firstItemIdx - offsetCount * layoutConstrainCount < 0)
        //    {
        //        offsetCount = Mathf.FloorToInt((float)(firstItemIdx) / layoutConstrainCount);                                               /* If the potential items in the gap is more than the actual items we can have, recalculate based on actual item number */
        //    }
        //    firstItemIdx -= offsetCount * layoutConstrainCount;
        //    if (itemCount >= 0)
        //    {
        //        firstItemIdx = Mathf.Max(firstItemIdx, 0);
        //    }
        //    lastItemIdx = firstItemIdx;

        //    float offset = offsetCount * (elementSize + itemSpacing);
        //    scrollContentRect.anchoredPosition += new Vector2(0, offset + (reverseDirection ? currentSize : 0));
        //    scrollContentBounds.center += new Vector3(0, offset + currentSize / 2, 0);
        //    scrollContentBounds.size = Vector3.zero;
        //    isChanged = true;
        //}

        float itemSize = 0f;
        ItemGroupConfig currItemGroup;

        /* Case 1: the bottom of the last item is much higher than the bottom the viewPort */
        /* Need to add new items at the bottom of the scrollContent */
        currItemGroup = displayItemGroupList[displayItemGroupCount - 1];
        
        if (scrollViewBounds.min.y < scrollContentBounds.min.y + contentDownPadding)
        {
            float size = 0f;
            float deltaSize = 0f;
            if ((currItemGroup.lastItemIdx == currItemGroup.itemList.Count &&           /* Case 1: all items are displayed AND the last item is not a nested item */
                 currItemGroup.lastItemIdx != currItemGroup.nestedItemIdx + 1) || 
                (currItemGroup.lastItemIdx == currItemGroup.itemList.Count &&           /* Case 2: all items are displayed AND the last item is a nested item AND all subitems are displayed */
                 currItemGroup.lastItemIdx == currItemGroup.nestedItemIdx + 1 &&
                 currItemGroup.lastSubItemIdx == currItemGroup.subItemCount))
            {
                AddItemGroupAtEnd(out size, scrollContent);
                deltaSize += size;
            }
            else if (currItemGroup.lastItemIdx - 1 == currItemGroup.nestedItemIdx && currItemGroup.subItemCount >= 0 && currItemGroup.lastSubItemIdx < currItemGroup.subItemCount)
            {
                AddSubItemAtEnd(out size, true, currItemGroup.subItem, currItemGroup.displayItemList[currItemGroup.displayItemCount - 1], currItemGroup);
                deltaSize = size;
            }
            else
            {
                AddItemAtEnd(out size, true, currItemGroup.itemList[currItemGroup.lastItemIdx], scrollContent, currItemGroup);
                deltaSize = size;
            }

            while (size > 0 && scrollViewBounds.min.y < scrollContentBounds.min.y + contentDownPadding - deltaSize)
            {
                bool addSuccess = false;

                if ((currItemGroup.lastItemIdx == currItemGroup.itemList.Count &&           /* Case 1: all items are displayed AND the last item is not a nested item */
                     currItemGroup.lastItemIdx != currItemGroup.nestedItemIdx + 1) ||
                    (currItemGroup.lastItemIdx == currItemGroup.itemList.Count &&           /* Case 2: all items are displayed AND the last item is a nested item AND all subitems are displayed */
                     currItemGroup.lastItemIdx == currItemGroup.nestedItemIdx + 1 &&
                     currItemGroup.lastSubItemIdx == currItemGroup.subItemCount))
                {
                    addSuccess = AddItemGroupAtEnd(out size, scrollContent);
                    deltaSize += size;
                }
                else if (currItemGroup.lastItemIdx - 1 == currItemGroup.nestedItemIdx && currItemGroup.subItemCount >= 0 && currItemGroup.lastSubItemIdx < currItemGroup.subItemCount)
                {
                    addSuccess = AddSubItemAtEnd(out size, true, currItemGroup.subItem, currItemGroup.displayItemList[currItemGroup.displayItemCount - 1], currItemGroup);
                    deltaSize += size;
                }
                else
                {
                    addSuccess = AddItemAtEnd(out size, true, currItemGroup.itemList[currItemGroup.lastItemIdx], scrollContent, currItemGroup);
                    deltaSize += size;
                }

                if (!addSuccess)
                    break;
            }

            if (deltaSize > 0)
                isChanged = true;
        }

        /* Case 2: the top of the first item is much lower than the top of the viewPort */
        /* Need to add new items at the top of the scrollContent */
        currItemGroup = displayItemGroupList[0];

        if (scrollViewBounds.max.y > scrollContentBounds.max.y - contentTopPadding)
        {
            float size = 0f;
            float deltaSize = 0f;


            if ((currItemGroup.firstItemIdx <= 0 &&                                 /* Case 1: already reach the top of the item group AND the frist item is not a nested item */
                 currItemGroup.firstItemIdx != currItemGroup.nestedItemIdx) ||
                (currItemGroup.firstItemIdx <= 0 &&                                 /* Case 2: already reach the top of the item group AND the frist item is a nested item */
                 currItemGroup.firstItemIdx == currItemGroup.nestedItemIdx &&
                 currItemGroup.firstSubItemIdx <= 0))
            {
                AddItemGroupAtStart(out size, ScrollContent);
                deltaSize += size;
            }
            else if (currItemGroup.firstItemIdx == currItemGroup.nestedItemIdx && currItemGroup.subItemCount >= 0 && currItemGroup.firstSubItemIdx >= currItemGroup.nestedConstrainCount)
            {
                AddSubItemAtStart(out size, true, currItemGroup.subItem, currItemGroup.displayItemList[0], currItemGroup);
                deltaSize += size;
            }
            else
            {
                AddItemAtStart(out size, true, currItemGroup.itemList[currItemGroup.firstItemIdx - 1], scrollContent, currItemGroup);
                deltaSize += size;
            }

            while (size > 0 && scrollViewBounds.max.y > scrollContentBounds.max.y - contentTopPadding + deltaSize)
            {
                bool addSuccess = false;

                if ((currItemGroup.firstItemIdx <= 0 &&
                     currItemGroup.firstItemIdx != currItemGroup.nestedItemIdx) ||
                    (currItemGroup.firstItemIdx <= 0 &&
                     currItemGroup.firstItemIdx == currItemGroup.nestedItemIdx &&
                     currItemGroup.firstSubItemIdx <= 0))
                {
                    addSuccess = AddItemGroupAtStart(out size, ScrollContent);
                    deltaSize += size;
                }
                else if (currItemGroup.firstItemIdx == currItemGroup.nestedItemIdx && currItemGroup.subItemCount >= 0 && currItemGroup.firstSubItemIdx >= currItemGroup.nestedConstrainCount)
                {
                    addSuccess = AddSubItemAtStart(out size, true, currItemGroup.subItem, currItemGroup.displayItemList[0], currItemGroup);
                    deltaSize += size;
                }
                else
                {
                    addSuccess = AddItemAtStart(out size, true, currItemGroup.itemList[currItemGroup.firstItemIdx - 1], scrollContent, currItemGroup);
                    deltaSize += size;
                }

                if (!addSuccess)
                    break;
            }

            if (deltaSize > 0)
                isChanged = true;
        }

        /* Case 3: the top of the last item is much lower than the bottom of the viewPort */
        /* Need to remove old items at the bottom of the scrollContent */
        currItemGroup = displayItemGroupList[displayItemGroupCount - 1];
        if (currItemGroup.lastItemIdx - 1 != currItemGroup.nestedItemIdx || 
            (currItemGroup.lastItemIdx - 1 == currItemGroup.nestedItemIdx && currItemGroup.lastSubItemIdx - 1 <= currItemGroup.nestedConstrainCount))       /* special case: the last item is a nested item and it only have the first row of subitem, we consider it as a non-nested item */
            itemSize = GetItemSize(currItemGroup.displayItemList[currItemGroup.displayItemCount - 1].GetComponent<RectTransform>(), true);
        else
            itemSize = GetSubItemSize(currItemGroup.subItem.GetComponent<RectTransform>(), currItemGroup.itemList[currItemGroup.nestedItemIdx].GetComponent<RectTransform>(), true);

        if (scrollViewBounds.min.y > scrollContentBounds.min.y + itemSize + contentDownPadding)
        {
            float size = 0f;
            float deltaSize = 0f;

            if (currItemGroup.lastItemIdx <= layoutConstrainCount &&                                /* Case 1: the last item is the last item of the item group and is not a nested item */
                currItemGroup.lastItemIdx != currItemGroup.nestedItemIdx + 1)
            {
                /* Need to consider upward item spacing between another item group */
                RemoveItemAtEnd(out size, true, currItemGroup);
                deltaSize += size;
                RemoveItemGroupAtEnd();
            }
            else if(currItemGroup.lastItemIdx <= layoutConstrainCount &&                            /* Case 2: the last item is the last item of the item group and is a nested item; the last subitem is the last subitem of the item group */
                    currItemGroup.lastItemIdx == currItemGroup.nestedItemIdx + 1 &&
                    currItemGroup.lastSubItemIdx <= currItemGroup.nestedConstrainCount)
            {
                RemoveSubItemAtEnd(out size, false, currItemGroup.displayItemList[currItemGroup.displayItemCount - 1], currItemGroup);
                deltaSize += size;
                RemoveItemAtEnd(out size, true, currItemGroup);
                deltaSize += size;
                RemoveItemGroupAtEnd();
            }
            else if (currItemGroup.lastItemIdx - 1 != currItemGroup.nestedItemIdx)                  /* Case 3: the last item is not the last item of the item group and is not a nested item */
            {
                RemoveItemAtEnd(out size, true, currItemGroup);
                deltaSize += size;
            }
            else if (currItemGroup.lastItemIdx - 1 == currItemGroup.nestedItemIdx)
            {
                if (currItemGroup.lastSubItemIdx <= currItemGroup.nestedConstrainCount)             /* Case 4.1: the last item is not the last item of the item group and is a nested item; the last subitem is the last subitem of the item group */
                {
                    RemoveSubItemAtEnd(out size, false, currItemGroup.displayItemList[currItemGroup.displayItemCount - 1], currItemGroup);
                    deltaSize += size;
                    RemoveItemAtEnd(out size, true, currItemGroup);
                    deltaSize += size;
                }
                else                                                                                /* Case 4.2: the last item is not the last item of the item group and is a nested item; the last subitem is not the last subitem of the item group */
                {
                    RemoveSubItemAtEnd(out size, true, currItemGroup.displayItemList[currItemGroup.displayItemCount - 1], currItemGroup);
                    deltaSize += size;
                }
            }

            while (size > 0 && scrollViewBounds.min.y > scrollContentBounds.min.y + itemSize + contentDownPadding + deltaSize)
            {
                bool removeSuccess = false;

                if (currItemGroup.lastItemIdx <= layoutConstrainCount &&                                /* Case 1: the last item is the last item of the item group and is not a nested item */
                    currItemGroup.lastItemIdx != currItemGroup.nestedItemIdx + 1)
                {
                    /* Need to consider upward item spacing between another item group */
                    removeSuccess = RemoveItemAtEnd(out size, true, currItemGroup);
                    deltaSize += size;
                    removeSuccess = RemoveItemGroupAtEnd();
                }
                else if (currItemGroup.lastItemIdx <= layoutConstrainCount &&                            /* Case 2: the last item is the last item of the item group and is a nested item; the last subitem is the last subitem of the item group */
                        currItemGroup.lastItemIdx == currItemGroup.nestedItemIdx + 1 &&
                        currItemGroup.lastSubItemIdx <= currItemGroup.nestedConstrainCount)
                {
                    removeSuccess = RemoveSubItemAtEnd(out size, false, currItemGroup.displayItemList[currItemGroup.displayItemCount - 1], currItemGroup);
                    deltaSize += size;
                    removeSuccess = RemoveItemAtEnd(out size, true, currItemGroup);
                    deltaSize += size;
                    removeSuccess = RemoveItemGroupAtEnd();
                }
                else if (currItemGroup.lastItemIdx - 1 != currItemGroup.nestedItemIdx)                  /* Case 3: the last item is not the last item of the item group and is not a nested item */
                {
                    removeSuccess = RemoveItemAtEnd(out size, true, currItemGroup);
                    deltaSize += size;
                }
                else if (currItemGroup.lastItemIdx - 1 == currItemGroup.nestedItemIdx)
                {
                    if (currItemGroup.lastSubItemIdx <= currItemGroup.nestedConstrainCount)             /* Case 4.1: the last item is not the last item of the item group and is a nested item; the last subitem is the last subitem of the item group */
                    {
                        removeSuccess = RemoveSubItemAtEnd(out size, false, currItemGroup.displayItemList[currItemGroup.displayItemCount - 1], currItemGroup);
                        deltaSize += size;
                        removeSuccess = RemoveItemAtEnd(out size, true, currItemGroup);
                        deltaSize += size;
                    }
                    else                                                                                /* Case 4.2: the last item is not the last item of the item group and is a nested item; the last subitem is not the last subitem of the item group */
                    {
                        removeSuccess = RemoveSubItemAtEnd(out size, true, currItemGroup.displayItemList[currItemGroup.displayItemCount - 1], currItemGroup);
                        deltaSize += size;
                    }
                }

                if (!removeSuccess)
                    break;
            }

            if (deltaSize > 0)
            {
                isChanged = true;
                ClearItemDespawnList(currItemGroup);
                ClearSubItemDespawnList(currItemGroup);
            }
        }


        /* Case 4: the bottom of the first item is much higher than the top of the viewPort */
        /* Need to remove old items at the top of the scrollContent */
        currItemGroup = displayItemGroupList[0];
        if (currItemGroup.firstItemIdx != currItemGroup.nestedItemIdx || 
            (currItemGroup.firstItemIdx == currItemGroup.nestedItemIdx && currItemGroup.firstSubItemIdx + currItemGroup.nestedConstrainCount >= currItemGroup.subItemCount))            /* special case: the first item is a nested item and it only have the last row of subitem, we consider it as a non-nested item */
            itemSize = GetItemSize(currItemGroup.displayItemList[0].GetComponent<RectTransform>(), true);
        else
            itemSize = GetSubItemSize(currItemGroup.subItem.GetComponent<RectTransform>(), currItemGroup.itemList[currItemGroup.nestedItemIdx].GetComponent<RectTransform>(), true);

        if (scrollViewBounds.max.y < scrollContentBounds.max.y - itemSize - contentTopPadding)
        {
            float size = 0f;
            float deltaSize = 0f;

            if (currItemGroup.firstItemIdx >= currItemGroup.itemCount - layoutConstrainCount &&         /* Case 1: the first item is the last item and is not a nested item */
                currItemGroup.firstItemIdx != currItemGroup.nestedItemIdx)
            {
                /* Need to consider upward item spacing between another item group */
                RemoveItemAtStart(out size, true, currItemGroup);
                deltaSize += size;
                RemoveItemGroupAtStart();
            }
            else if (currItemGroup.firstItemIdx >= currItemGroup.itemCount - layoutConstrainCount &&    /* Case 2: the first item is the last item and is a nested item; the first subitem is the last subitem */
                     currItemGroup.firstItemIdx == currItemGroup.nestedItemIdx &&
                     currItemGroup.firstSubItemIdx >= currItemGroup.subItemCount - currItemGroup.nestedConstrainCount)
            {
                RemoveSubItemAtStart(out size, false, currItemGroup.displayItemList[0], currItemGroup);
                deltaSize += size;
                RemoveItemAtStart(out size, true, currItemGroup);
                deltaSize += size;
                RemoveItemGroupAtStart();
            }
            else if (currItemGroup.firstItemIdx != currItemGroup.nestedItemIdx)                         /* Case 3: the first item is not the last item and is not a nested item */
            {
                RemoveItemAtStart(out size, true, currItemGroup);
                deltaSize += size;
            }
            else if (currItemGroup.firstItemIdx == currItemGroup.nestedItemIdx)
            {
                if (currItemGroup.firstSubItemIdx >= currItemGroup.subItemCount - currItemGroup.nestedConstrainCount)   /* Case 4.1: the first item is not the last item and is a nested item; the first subitem is the last subitem */
                {
                    RemoveSubItemAtStart(out size, false, currItemGroup.displayItemList[0], currItemGroup);
                    deltaSize += size;
                    RemoveItemAtStart(out size, true, currItemGroup);
                    deltaSize += size;
                }
                else                                                                                                    /* Case 4.2: the first item is not the last item and is a nested item; the first subitem is not the last subitem */
                {
                    RemoveSubItemAtStart(out size, true, currItemGroup.displayItemList[0], currItemGroup);
                    deltaSize += size;
                }
            }

            while (size > 0 && scrollViewBounds.max.y < scrollContentBounds.max.y - itemSize - contentTopPadding - deltaSize)
            {
                bool removeSuccess = false;

                if (currItemGroup.firstItemIdx >= currItemGroup.itemCount - layoutConstrainCount &&         /* Case 1: the first item is the last item and is not a nested item */
                    currItemGroup.firstItemIdx != currItemGroup.nestedItemIdx)
                {
                    /* Need to consider upward item spacing between another item group */
                    removeSuccess = RemoveItemAtStart(out size, true, currItemGroup);
                    deltaSize += size;
                    removeSuccess = RemoveItemGroupAtStart();
                }
                else if (currItemGroup.firstItemIdx >= currItemGroup.itemCount - layoutConstrainCount &&    /* Case 2: the first item is the last item and is a nested item; the first subitem is the last subitem */
                         currItemGroup.firstItemIdx == currItemGroup.nestedItemIdx &&
                         currItemGroup.firstSubItemIdx >= currItemGroup.subItemCount - currItemGroup.nestedConstrainCount)
                {
                    removeSuccess = RemoveSubItemAtStart(out size, false, currItemGroup.displayItemList[0], currItemGroup);
                    deltaSize += size;
                    removeSuccess = RemoveItemAtStart(out size, true, currItemGroup);
                    deltaSize += size;
                    removeSuccess = RemoveItemGroupAtStart();
                }
                else if (currItemGroup.firstItemIdx != currItemGroup.nestedItemIdx)                         /* Case 3: the first item is not the last item and is not a nested item */
                {
                    removeSuccess = RemoveItemAtStart(out size, true, currItemGroup);
                    deltaSize += size;
                }
                else if (currItemGroup.firstItemIdx == currItemGroup.nestedItemIdx)
                {
                    if (currItemGroup.firstSubItemIdx >= currItemGroup.subItemCount - currItemGroup.nestedConstrainCount)   /* Case 4.1: the first item is not the last item and is a nested item; the first subitem is the last subitem */
                    {
                        removeSuccess = RemoveSubItemAtStart(out size, false, currItemGroup.displayItemList[0], currItemGroup);
                        deltaSize += size;
                        removeSuccess = RemoveItemAtStart(out size, true, currItemGroup);
                        deltaSize += size;
                    }
                    else                                                                                                    /* Case 4.2: the first item is not the last item and is a nested item; the first subitem is not the last subitem */
                    {
                        removeSuccess = RemoveSubItemAtStart(out size, true, currItemGroup.displayItemList[0], currItemGroup);
                        deltaSize += size;
                    }
                }

                if (!removeSuccess)
                    break;
            }

            if (deltaSize > 0)
            {
                isChanged = true;
                ClearItemDespawnList(currItemGroup);
                ClearSubItemDespawnList(currItemGroup);
            }
        }

        if (isChanged)
        {
            ClearItemDespawnList();
        }
    }

    #endregion


    #region bounds相关
    public void UpdateBounds(bool updateItems = false)
    {
        /* Since viewPort UI is static, therefore its rectTransform coordinate can be directly reused */
        scrollViewBounds = new Bounds(scrollViewRect.rect.center, scrollViewRect.rect.size);
        CalculateContentBounds();

        // Don't do this in Rebuild
        //if (Application.isPlaying && updateItems && UpdateItems(m_ViewBounds, m_ContentBounds))
        if (Application.isPlaying && updateItems)
        {
            //Debug.LogFormat("On update bounds, initial scrollContent bounds center: {0}, bounds size: {1}, bound max: {2}", scrollContentBounds.center, scrollContentBounds.size, scrollContentBounds.max);

            //UpdateScrollItems();
            UpdateScrollItemGroups();
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(scrollContentRect);
            CalculateContentBounds();

            //Debug.LogFormat("On update bounds, new scrollContent bounds center: {0}, bounds size: {1}, bound max: {2}", scrollContentBounds.center, scrollContentBounds.size, scrollContentBounds.max);
        }

        // Make sure scrollContent bounds are at least as large as view by adding padding if not.
        // One might think at first that if the scrollContent is smaller than the view, scrolling should be allowed.
        // However, that's not how scroll views normally work.
        // Scrolling is *only* possible when scrollContent is *larger* than view.
        // We use the pivot of the scrollContent rect to decide in which directions the scrollContent bounds should be expanded.
        // E.g. if pivot is at top, bounds are expanded downwards.
        // This also works nicely when ContentSizeFitter is used on the scrollContent.
        Vector3 contentSize = scrollContentBounds.size;
        Vector3 contentPos = scrollContentBounds.center;
        Vector3 excess = scrollViewBounds.size - contentSize;
        if (excess.x > 0)
        {
            contentPos.x -= excess.x * (scrollContentRect.pivot.x - 0.5f);
            contentSize.x = scrollViewBounds.size.x;
        }
        if (excess.y > 0)
        {
            contentPos.y -= excess.y * (scrollContentRect.pivot.y - 0.5f);
            contentSize.y = scrollViewBounds.size.y;
        }

        scrollContentBounds.size = contentSize;
        scrollContentBounds.center = contentPos;
    }

    public void CalculateContentBounds()
    {
        if (scrollContentRect == null)
            scrollContentBounds = new Bounds();

        /* scrollContent UI is dynamic, therefore needs to calculate from its world position */
        var vMin = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        var vMax = new Vector3(float.MinValue, float.MinValue, float.MinValue);
        var corners = new Vector3[4];
        var localMatrix = scrollViewRect.worldToLocalMatrix;
        scrollContentRect.GetWorldCorners(corners);

        for (int j = 0; j < 4; j++)
        {
            Vector3 v = localMatrix.MultiplyPoint3x4(corners[j]);
            vMin = Vector3.Min(v, vMin);
            vMax = Vector3.Max(v, vMax);
        }
        scrollContentBounds = new Bounds(vMin, Vector3.zero);
        scrollContentBounds.Encapsulate(vMax);
    }

    private Bounds CalculateItemBounds(int index)
    {
        if (scrollContentRect == null)
            return new Bounds();

        var vMin = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        var vMax = new Vector3(float.MinValue, float.MinValue, float.MinValue);
        var corners = new Vector3[4];
        var localMatrix = scrollViewRect.worldToLocalMatrix;
        int offset = index - firstItemIdx;
        if (offset < 0 || offset >= displayItemList.Count)
            return new Bounds();
        var rt = displayItemList[offset].GetComponent<RectTransform>();
        if (rt == null)
            return new Bounds();
        rt.GetWorldCorners(corners);
        for (int j = 0; j < 4; j++)
        {
            Vector3 v = localMatrix.MultiplyPoint3x4(corners[j]);
            vMin = Vector3.Min(v, vMin);
            vMax = Vector3.Max(v, vMax);
        }

        var bounds = new Bounds(vMin, Vector3.zero);
        bounds.Encapsulate(vMax);
        return bounds;
    }

    #endregion


    #endregion


    #region 接口类 & 回调类函数

    public virtual void OnBeginDrag(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
            return;

        if (!IsActive())
            return;

        UpdateBounds(false);

        isDragging = true;
        cursorStartPos = Vector2.zero;
        contentStartPos = scrollContentRect.anchoredPosition;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(scrollViewRect, eventData.position, eventData.pressEventCamera, out cursorStartPos);

        //Debug.LogFormat("On BeginDrag, cursor start Pos: {0}", cursorStartPos);
    }

    public virtual void OnDrag(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
            return;

        if (!IsActive())
            return;

        //Vector2 cursorEndPos;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(scrollViewRect, eventData.position, eventData.pressEventCamera, out Vector2 cursorEndPos))
            return;

        UpdateBounds(false);

        Vector2 pointerDelta = cursorEndPos - cursorStartPos;
        Vector2 position = contentStartPos + pointerDelta;

        // Offset to get scrollContent into place in the view.
        Vector2 offset = CalculateOffset(position - scrollContentRect.anchoredPosition);
        position += offset;
        if (movementType == ScrollMovementType.Elastic)
        {
            if (offset.x != 0)
                position.x = position.x - RubberDelta(offset.x, scrollViewBounds.size.x) * rubberScale;
            if (offset.y != 0)
                position.y = position.y - RubberDelta(offset.y, scrollViewBounds.size.y) * rubberScale;
        }

        SetContentAnchoredPosition(position);

        //Debug.LogFormat("On Drag, cursor Pos: {0}", cursorEndPos);
    }

    public virtual void OnEndDrag(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
            return;

        isDragging = false;
    }

    public virtual void OnScroll(PointerEventData data)
    {
        if (!IsActive())
            return;

        EnsureLayoutHasRebuilt();
        UpdateBounds(false);

        /* Down is positive for scroll events, while in UI system up is positive. */
        Vector2 delta = data.scrollDelta;
        delta.y *= -1;
        if (vertical && !horizontal)
        {
            if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
                delta.y = delta.x;
            delta.x = 0;
        }
        if (horizontal && !vertical)
        {
            if (Mathf.Abs(delta.y) > Mathf.Abs(delta.x))
                delta.x = delta.y;
            delta.y = 0;
        }

        Vector2 position = scrollContentRect.anchoredPosition;
        position += delta * scrollSensitivity;
        if (movementType == ScrollMovementType.Clamped)
            position += CalculateOffset(position - scrollContentRect.anchoredPosition);

        SetContentAnchoredPosition(position);
        UpdateBounds();
    }

    #endregion
}
