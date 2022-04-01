using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;

public class DynamicScrollRect : UIBehaviour, IInitializePotentialDragHandler, IBeginDragHandler, IEndDragHandler, IDragHandler, IScrollHandler, ICanvasElement, ILayoutElement, ILayoutGroup
{
    #region 用户设定相关

    public enum ScrollDirection
    {
        Vertical,
        Horizontal,
    }

    public enum ScrollMovementType
    {
        Unrestricted,   /* Unrestricted movement -- can scroll forever */
        Elastic,        /* Restricted but flexible -- can go past the edges, but springs back in place */
        Clamped,        /* Restricted movement where it's not possible to go past the edges */
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
        public int itemGroupIdx = -1;
        public int nestedItemIdx = -1;      /* value that smaller than 0 means there is no nested item in the item group */
        public int subItemCount = 0;
        private int constrainCount = int.MinValue;

        public int itemCount { get { return itemList.Count; } }
        public int displayItemCount { get { return displayItemList.Count; } }
        public int displaySubItemCount { get { return displaySubItemList.Count; } }
        public int nestedConstrainCount { get { if (constrainCount == int.MinValue) { constrainCount = (nestedItemIdx >= 0 && itemList[nestedItemIdx].TryGetComponent<GridLayoutGroup>(out var layout) && (layout.constraint != GridLayoutGroup.Constraint.Flexible)) ? layout.constraintCount : 1; } return constrainCount; } }


        [NonSerialized] public int firstItemIdx = 0;
        [NonSerialized] public int lastItemIdx = 0;
        [NonSerialized] public int firstSubItemIdx = 0;
        [NonSerialized] public int lastSubItemIdx = 0;

        public List<GameObject> itemList = new List<GameObject>();
        public GameObject subItem = null;

        [NonSerialized] public List<GameObject> displayItemList = new List<GameObject>();
        [NonSerialized] public List<GameObject> displaySubItemList = new List<GameObject>();

        public ItemGroupConfig() { }

        public ItemGroupConfig(int nestedItemIdx, int subItemCount, List<GameObject> itemList, GameObject subItem)
        {
            this.nestedItemIdx = nestedItemIdx;
            this.subItemCount = subItemCount;
            this.itemList = itemList;
            this.subItem = subItem;
            this.displayItemList = new List<GameObject>();
            this.displaySubItemList = new List<GameObject>(); 
        }

        public RectTransform GetItemRect(int itemIdx)
        {
            return itemList[itemIdx].GetComponent<RectTransform>();
        }

        public RectTransform GetDisplayItemRect(int itemIdx)
        {
            return displayItemList[itemIdx].GetComponent<RectTransform>();
        }

        public RectTransform GetSubItemRect()
        {
            return subItem.GetComponent<RectTransform>();
        }


    }


    [Header("ScrollView参数")]
    [SerializeField]
    private ScrollDirection direction = ScrollDirection.Vertical;
    public ScrollDirection Direction { get { return direction; } set { direction = value; } }

    [SerializeField]
    private float scrollSensitivity = 1f;
    public float ScrollSensitivity { get { return scrollSensitivity; } set { scrollSensitivity = value; } }

    [SerializeField]
    private float rubberScale = 1f;
    public float RubberScale { get { return rubberScale; } set { rubberScale = value; } }

    /* reverse direction function is still under development */
    [HideInInspector]
    private bool reverseDirection = false;
    public bool ReverseDirection { get { return reverseDirection; } set { reverseDirection = value; } }

    [SerializeField]
    private GameObject scrollView = null;
    public GameObject ScrollView { get { return scrollView; } set { scrollView = value; } }

    [SerializeField]
    private GameObject scrollContent = null;
    public GameObject ScrollContent { get { return scrollContent; } set { scrollContent = value; } }

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
    private float horizontalScrollbarSpacing;
    public float HorizontalScrollbarSpacing { get { return horizontalScrollbarSpacing; } set { horizontalScrollbarSpacing = value; SetDirty(); } }

    [SerializeField]
    private float verticalScrollbarSpacing;
    public float VerticalScrollbarSpacing { get { return verticalScrollbarSpacing; } set { verticalScrollbarSpacing = value; SetDirty(); } }

    [SerializeField]
    private ScrollMovementType movementType = ScrollMovementType.Elastic;
    public ScrollMovementType MovementType { get { return movementType; } set { movementType = value; } }

    [SerializeField]
    private float elasticity = 0.1f;                /* Only used for ScrollMovementType.Elastic */
    public float Elasticity { get { return elasticity; } set { elasticity = value; } }

    [SerializeField]
    private bool inertia = true;
    public bool Inertia { get { return inertia; } set { inertia = value; } }

    [SerializeField]
    private float decelerationRate = 0.135f;        /* Only used when inertia is enabled */
    public float DecelerationRate { get { return decelerationRate; } set { decelerationRate = value; } }

    [SerializeField]
    private ScrollRectEvent onValueChanged = new ScrollRectEvent();
    public ScrollRectEvent OnValueChanged { get { return onValueChanged; } set { onValueChanged = value; } }

    #endregion


    #region 私有UI组件相关

    private RectTransform rect;
    private RectTransform scrollViewRect;
    private RectTransform scrollContentRect;
    private RectTransform horizontalScrollbarRect;
    private RectTransform verticalScrollbarRect;

    private RectTransform Rect { get { if (rect = null) { rect = GetComponent<RectTransform>(); } return rect; } }
    private RectTransform ScrollViewRect { get { if (scrollViewRect == null) { scrollViewRect = scrollView.GetComponent<RectTransform>(); } return scrollViewRect; } }
    private RectTransform ScrollContentRect { get { if (scrollContentRect == null) { scrollContentRect = scrollContent.GetComponent<RectTransform>(); } return scrollContentRect; } }

    private DrivenRectTransformTracker tracker;

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

    private float itemSpacing = -1.0f;
    protected float ItemSpacing { get { return GetItemSpacing(); } }

    protected float contentLeftPadding = 0;
    protected float contentRightPadding = 0;
    protected float contentTopPadding = 0;
    protected float contentDownPadding = 0;
    protected float threshold = 0f;

    private bool isDragging;

    private bool hasRebuiltLayout = false;
    private bool horizontalSliderExpand;
    private bool verticalSliderExpand;
    private float horizontalSliderHeight;
    private float verticalSliderWidth;

    #endregion


    #region 内容数据相关

    private int firstItemGroupIdx = 0;
    private int lastItemGroupIdx = 0;

    private int itemGroupCount { get { return itemGroupList.Count; } }
    private int displayItemGroupCount { get { return displayItemGroupList.Count; } }

    private List<ItemGroupConfig> displayItemGroupList = new List<ItemGroupConfig>();

    #endregion


    #region 对象池相关

    private class DynamicScrollRectItem : MonoBehaviour { public GameObject prefab = null; }    /* Added to freshly instantiated objects, so we can link back to the correct pool on despawn. */

    protected Dictionary<GameObject, Stack<GameObject>> itemPoolDict = new Dictionary<GameObject, Stack<GameObject>>();
    
    #endregion


    #region Debug相关，可删除

    public int itemCount = 20;
    private Vector2 scrollBoundMax;
    private Vector2 scrollBoundMin;
    private Vector2 contentBoundMax;
    private Vector2 contentBoundMin;

    #endregion



    #region Monobehaviour相关

    protected override void Awake()
    {
        base.Awake();

        scrollViewRect = scrollView.GetComponent<RectTransform>();
        scrollContentRect = scrollContent.GetComponent<RectTransform>();
        rect = GetComponent<RectTransform>();

        OnSpawnItemAtStartEvent += OnSpawnItemAtStart;
        OnSpawnItemAtEndEvent += OnSpawnItemAtEnd;
        OnSpawnSubItemAtStartEvent += OnSpawnSubItemAtStart;
        OnSpawnSubItemAtEndEvent += OnSpawnSubItemAtEnd;
        OnDespawnItemAtStartEvent += OnDespawnItemAtStart;
        OnDespawnItemAtEndEvent += OnDespawnItemAtEnd;
        OnDespawnSubItemAtStartEvent += OnDespawnSubItemAtStart;
        OnDespawnSubItemAtEndEvent += OnDespawnSubItemAtEnd;
        OnAddItemDynamicEvent += OnAddItemDynamic;
        OnRemoveItemDynamicEvent += OnRemoveItemDynamic;
        OnAddSubItemDynamicEvent += OnAddSubItemDynamic;
        OnRemoveSubItemDynamicEvent += OnRemoveSubItemDynamic;
    }

    protected override void Start()
    {
        base.Start();

        SetScrollBar(SetHorizontalNormalizedPosition, ref horizontalScrollbar, ref horizontalScrollbarRect);
        SetScrollBar(SetVerticalNormalizedPosition, ref verticalScrollbar, ref verticalScrollbarRect);
        GetItemSpacing();

        RefillScrollContent();
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

    protected override void OnEnable()
    {
        base.OnEnable();

        if (horizontalScrollbar)
            horizontalScrollbar.onValueChanged.AddListener(SetHorizontalNormalizedPosition);
        if (verticalScrollbar)
            verticalScrollbar.onValueChanged.AddListener(SetVerticalNormalizedPosition);

        CanvasUpdateRegistry.RegisterCanvasElementForLayoutRebuild(this);
    }

    protected override void OnDisable()
    {
        CanvasUpdateRegistry.UnRegisterCanvasElementForRebuild(this);

        if (horizontalScrollbar)
            horizontalScrollbar.onValueChanged.RemoveListener(SetHorizontalNormalizedPosition);
        if (verticalScrollbar)
            verticalScrollbar.onValueChanged.RemoveListener(SetVerticalNormalizedPosition);

        hasRebuiltLayout = false;
        velocity = Vector2.zero;
        tracker.Clear();
        LayoutRebuilder.MarkLayoutForRebuild(scrollViewRect);
        base.OnDisable();
    }

    protected override void OnDestroy()
    {
        OnSpawnItemAtStartEvent -= OnSpawnItemAtStart;
        OnSpawnItemAtEndEvent -= OnSpawnItemAtEnd;
        OnSpawnSubItemAtStartEvent -= OnSpawnSubItemAtStart;
        OnSpawnSubItemAtEndEvent -= OnSpawnSubItemAtEnd;
        OnDespawnItemAtStartEvent -= OnDespawnItemAtStart;
        OnDespawnItemAtEndEvent -= OnDespawnItemAtEnd;
        OnDespawnSubItemAtStartEvent -= OnDespawnSubItemAtStart;
        OnDespawnSubItemAtEndEvent -= OnDespawnSubItemAtEnd;
        OnAddItemDynamicEvent -= OnAddItemDynamic;
        OnRemoveItemDynamicEvent -= OnRemoveItemDynamic;
        OnAddSubItemDynamicEvent -= OnAddSubItemDynamic;
        OnRemoveSubItemDynamicEvent -= OnRemoveSubItemDynamic;
        base.OnDestroy();
    }

    #endregion


    #region 列表元素增加

    private bool AddItemAtStart(out float size, bool considerSpacing, GameObject prefab, GameObject parent, ItemGroupConfig itemGroup)
    {
        size = 0;

        if (itemGroup.itemCount <= 0 || itemGroup.firstItemIdx <= 0)
            return false;

        /* Special case: we will not spawn the nested item if there is not subItem inside, but we will record the data of the nested item still */
        if (itemGroup.firstItemIdx - 1 == itemGroup.nestedItemIdx && itemGroup.subItemCount <= 0)
        {
            itemGroup.displayItemList.Insert(0, prefab);
            itemGroup.firstItemIdx--;
            OnSpawnItemAtStartEvent(itemGroup);
        }
        else
        {
            GameObject newItem = Spawn(prefab);
            newItem.transform.SetParent(parent.transform, false);
            newItem.transform.SetAsFirstSibling();
            itemGroup.displayItemList.Insert(0, newItem);
            itemGroup.firstItemIdx--;
            size = GetItemSize(newItem.GetComponent<RectTransform>(), considerSpacing);
            OnSpawnItemAtStartEvent(itemGroup, newItem);
        }

        /* Update the parameter of the scrollContent UI */
        if (!reverseDirection)
        {
            Vector2 offset = GetVector2(size);
            scrollContentRect.anchoredPosition += offset;
            prevPos += offset;
            contentStartPos += offset;
        }

        /* Used for testing, can be deleted */
        PrintAllIGInformation("AddItemAtStart", itemGroup);

        return true;
    }

    private bool AddItemAtEnd(out float size, bool considerSpacing, GameObject prefab, GameObject parent, ItemGroupConfig itemGroup)
    {
        size = 0;

        if (itemGroup.itemCount <= 0 || itemGroup.lastItemIdx >= itemGroup.itemCount)
            return false;

        /* Special case: we will not spawn the nested item if there is no subItem inside, but we will record the data of the nested item still */
        if (itemGroup.lastItemIdx == itemGroup.nestedItemIdx && itemGroup.subItemCount <= 0)
        {
            itemGroup.displayItemList.Add(prefab);
            itemGroup.lastItemIdx++;
            OnSpawnItemAtEndEvent(itemGroup);
        }
        else
        {
            GameObject newItem = Spawn(prefab);
            newItem.transform.SetParent(parent.transform, false);
            newItem.transform.SetAsLastSibling();
            itemGroup.displayItemList.Add(newItem);
            itemGroup.lastItemIdx++;
            size = GetItemSize(newItem.GetComponent<RectTransform>(), considerSpacing);
            OnSpawnItemAtEndEvent(itemGroup, newItem);
        }

        if (reverseDirection)
        {
            Vector2 offset = GetVector2(size);
            scrollContentRect.anchoredPosition -= offset;
            prevPos -= offset;
            contentStartPos -= offset;
        }

        /* Used for testing, can be deleted */
        PrintAllIGInformation("AddItemAtEnd", itemGroup);

        return true;
    }

    private bool AddSubItemAtStart(out float size, bool considerSpacing, GameObject prefab, GameObject parent, ItemGroupConfig itemGroup)
    {
        size = 0;

        /* For the case when subitems cannot fully fill the last row */
        int count = itemGroup.nestedConstrainCount;
        if (itemGroup.firstSubItemIdx > itemGroup.subItemCount - (itemGroup.subItemCount % itemGroup.nestedConstrainCount))
            count = itemGroup.subItemCount % itemGroup.nestedConstrainCount;

        if (itemGroup.firstSubItemIdx <= 0 || (itemGroup.firstSubItemIdx - count) % itemGroup.nestedConstrainCount != 0)
            return false;

        for (int i = 0; i < count; i++)
        {
            GameObject newSubItem = Spawn(prefab);
            newSubItem.transform.SetParent(parent.transform, false);
            newSubItem.transform.SetAsFirstSibling();
            itemGroup.displaySubItemList.Insert(0, newSubItem);
            itemGroup.firstSubItemIdx--;
            OnSpawnSubItemAtStartEvent(itemGroup, newSubItem);
        }
        size = GetSubItemSize(prefab.GetComponent<RectTransform>(), parent.GetComponent<RectTransform>(), considerSpacing);

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

        /* Used for testing, can be deleted */
        PrintAllIGInformation("AddSubItemAtStart", itemGroup);

        return true;
    }

    private bool AddSubItemAtEnd(out float size, bool considerSpacing, GameObject prefab, GameObject parent, ItemGroupConfig itemGroup)
    {
        size = 0;
        if (itemGroup.lastSubItemIdx >= itemGroup.subItemCount)
            return false;

        int count = itemGroup.nestedConstrainCount - (itemGroup.displaySubItemCount % itemGroup.nestedConstrainCount);
        for (int i = 0; i < count; i++)
        {
            GameObject newSubItem = Spawn(prefab);
            newSubItem.transform.SetParent(parent.transform, false);
            newSubItem.transform.SetAsLastSibling();
            itemGroup.displaySubItemList.Add(newSubItem);
            itemGroup.lastSubItemIdx++;
            OnSpawnSubItemAtEndEvent(itemGroup, newSubItem);

            if (itemGroup.lastSubItemIdx >= itemGroup.subItemCount)
                break;
        }
        size = GetSubItemSize(prefab.GetComponent<RectTransform>(), parent.GetComponent<RectTransform>(), considerSpacing);

        if (reverseDirection)
        {
            Vector2 offset = GetVector2(size);
            parent.GetComponent<RectTransform>().anchoredPosition -= offset;
            scrollContentRect.anchoredPosition -= offset;
            prevPos -= offset;
            contentStartPos -= offset;
        }

        /* Used for testing, can be deleted */
        PrintAllIGInformation("AddSubItemAtEnd", itemGroup);

        return true;
    }

    private bool AddItemGroupAtStart(out float size, GameObject parent)
    {
        size = 0;
        if (firstItemGroupIdx <= 0)
            return false;

        var newItemGroup = itemGroupList[firstItemGroupIdx - 1];
        newItemGroup.itemGroupIdx = firstItemGroupIdx - 1;
        if (displayItemGroupList.Contains(newItemGroup))
            return false;

        if (AddItemAtStart(out size, true, newItemGroup.itemList[newItemGroup.firstItemIdx - 1], parent, newItemGroup))
        {
            if (newItemGroup.firstItemIdx == newItemGroup.nestedItemIdx && newItemGroup.subItemCount > 0 && newItemGroup.firstSubItemIdx > 0)
                AddSubItemAtStart(out size, false, newItemGroup.subItem, newItemGroup.displayItemList[0], newItemGroup);

            displayItemGroupList.Insert(0, newItemGroup);
            firstItemGroupIdx--;

            /* Used for testing, can be deleted */
            PrintAllIGInformation("AddItemGroupAtStart", newItemGroup);

            return true;
        }
        else
            return false;
    }


    private bool AddItemGroupAtEnd(out float size, GameObject parent)
    {
        size = 0;
        if (lastItemGroupIdx >= itemGroupCount)
            return false;

        var newItemGroup = itemGroupList[lastItemGroupIdx];
        newItemGroup.itemGroupIdx = lastItemGroupIdx;
        if (displayItemGroupList.Contains(newItemGroup))
            return false;

        if (AddItemAtEnd(out size, false, newItemGroup.itemList[newItemGroup.lastItemIdx], parent, newItemGroup))
        {
            if (newItemGroup.lastItemIdx - 1 == newItemGroup.nestedItemIdx && newItemGroup.subItemCount > 0)
                AddSubItemAtEnd(out size, false, newItemGroup.subItem, newItemGroup.displayItemList[newItemGroup.displayItemCount - 1], newItemGroup);

            displayItemGroupList.Add(newItemGroup);
            lastItemGroupIdx++;

            /* Used for testing, can be deleted */
            PrintAllIGInformation("AddItemGroupAtEnd", newItemGroup);

            return true;
        }
        else
            return false;
    }


    private bool AddElementAtStart(out float size, ItemGroupConfig itemGroup)
    {
        bool addSuccess = false;

        if ((itemGroup.firstItemIdx <= 0 &&                                 /* Case 1: already reach the top of the item group AND the frist item is not a nested item */
             itemGroup.firstItemIdx != itemGroup.nestedItemIdx) ||
            (itemGroup.firstItemIdx <= 0 &&                                 /* Case 2: already reach the top of the item group AND the frist item is a nested item */
             itemGroup.firstItemIdx == itemGroup.nestedItemIdx &&
             itemGroup.firstSubItemIdx <= 0))
        {
            addSuccess = AddItemGroupAtStart(out size, ScrollContent);
        }
        else if (itemGroup.firstItemIdx == itemGroup.nestedItemIdx && 
                 itemGroup.subItemCount > 0 && 
                 itemGroup.firstSubItemIdx > 0)
        {
            addSuccess = AddSubItemAtStart(out size, true, itemGroup.subItem, itemGroup.displayItemList[0], itemGroup);
        }
        else
        {
            addSuccess = AddItemAtStart(out size, true, itemGroup.itemList[itemGroup.firstItemIdx - 1], scrollContent, itemGroup);
        }

        return addSuccess;
    }


    private bool AddElementAtEnd(out float size, ItemGroupConfig itemGroup)
    {
        bool addSuccess = false;

        if ((itemGroup.lastItemIdx == itemGroup.itemCount &&                /* Case 1: all items are displayed AND the last item is not a nested item */
             itemGroup.lastItemIdx != itemGroup.nestedItemIdx + 1) ||
            (itemGroup.lastItemIdx == itemGroup.itemCount &&                /* Case 2: all items are displayed AND the last item is a nested item AND all subitems are displayed */
             itemGroup.lastItemIdx == itemGroup.nestedItemIdx + 1 &&
             itemGroup.lastSubItemIdx == itemGroup.subItemCount))
        {
            addSuccess = AddItemGroupAtEnd(out size, scrollContent);
        }
        else if (itemGroup.lastItemIdx == itemGroup.nestedItemIdx + 1 &&    /* Case 3: the current item is a nested item, and the nested item does not reach to the end */
                 itemGroup.subItemCount > 0 &&
                 itemGroup.lastSubItemIdx < itemGroup.subItemCount)
        {
            addSuccess = AddSubItemAtEnd(out size, true, itemGroup.subItem, itemGroup.displayItemList[itemGroup.displayItemCount - 1], itemGroup);
        }
        else
        {
            addSuccess = AddItemAtEnd(out size, true, itemGroup.itemList[itemGroup.lastItemIdx], scrollContent, itemGroup);
        }

        return addSuccess;
    }

    #endregion


    #region 列表元素删减

    private bool RemoveItemAtStart(out float size, bool considerSpacing, ItemGroupConfig itemGroup)
    {
        size = 0;
        if (itemGroup.displayItemCount == 0)
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
        {
            PrintAllIGInformation("RemoveItemAtStart fail", itemGroup);
            return false;
        }

        /* Special case: only need to delete the data of the nested item if the subItem number of it is smaller than 0 (no subItem in this nested item) */
        if (itemGroup.firstItemIdx == itemGroup.nestedItemIdx && itemGroup.subItemCount <= 0)
        {
            itemGroup.displayItemList.RemoveAt(0);
            itemGroup.firstItemIdx++;
            OnDespawnItemAtStartEvent(itemGroup);
        }
        else
        {
            GameObject oldItem = itemGroup.displayItemList[0];
            size = GetItemSize(oldItem.GetComponent<RectTransform>(), considerSpacing);
            itemGroup.firstItemIdx++;
            OnDespawnItemAtStartEvent(itemGroup, oldItem);
            DespawnItem(itemGroup, true);
        }

        /* Update the parameter of the scrollContent UI */
        if (!reverseDirection)
        {
            Vector2 offset = GetVector2(size);
            scrollContentRect.anchoredPosition -= offset;
            prevPos -= offset;
            contentStartPos -= offset;
        }


        /* Used for testing, can be deleted */
        PrintAllIGInformation("RemoveItemAtStart", itemGroup);

        return true;
    }

    private bool RemoveItemAtEnd(out float size, bool considerSpacing, ItemGroupConfig itemGroup)
    {
        size = 0;
        if (itemGroup.displayItemCount == 0)
            return false;

        /* special case: when moving or dragging, we cannot continue delete subitems at end when we've reached the start of the whole scroll content */
        /* we reach the start of the whole scroll content when: the item group at start is the first item group AND the item at first inside the item group is
         * the first item AND if the first item is a nested item, the subitems in first row is the first row of subitems */
        var firstItemGroup = displayItemGroupList[0];
        if ((isDragging || velocity != Vector2.zero) &&
            firstItemGroupIdx <= 0 &&
            firstItemGroup.firstItemIdx <= 0 &&
            (firstItemGroup.firstItemIdx != firstItemGroup.nestedItemIdx ||
             firstItemGroup.firstItemIdx == firstItemGroup.nestedItemIdx && firstItemGroup.firstSubItemIdx < firstItemGroup.nestedConstrainCount))
        {
            Debug.LogFormat("RemoveItemAtEnd fail, firstItemIdx: {0}, nestedItemIdx: {1}", firstItemGroup.firstItemIdx, firstItemGroup.nestedItemIdx);
            return false;
        }

        /* Special case: only need to delete the data of the nested item if the subItem number of it is smaller than 0 (no subItem in this nested item) */
        if (itemGroup.lastItemIdx - 1 == itemGroup.nestedItemIdx && itemGroup.subItemCount <= 0)
        {
            itemGroup.displayItemList.RemoveAt(itemGroup.displayItemCount - 1);
            itemGroup.lastItemIdx--;
            OnDespawnItemAtEndEvent(itemGroup);
        }
        else
        {
            GameObject oldItem = itemGroup.displayItemList[itemGroup.displayItemCount - 1];
            size = GetItemSize(oldItem.GetComponent<RectTransform>(), considerSpacing);
            itemGroup.lastItemIdx--;
            OnDespawnItemAtEndEvent(itemGroup, oldItem);
            DespawnItem(itemGroup, false);
        }

        /* Update the parameter of the scrollContent UI */
        if (reverseDirection)
        {
            Vector2 offset = GetVector2(size);
            scrollContentRect.anchoredPosition += offset;
            prevPos += offset;
            contentStartPos += offset;
        }

        /* Used for testing, can be deleted */
        PrintAllIGInformation("RemoveItemAtEnd", itemGroup);

        return true;
    }

    private bool RemoveSubItemAtStart(out float size, bool considerSpacing, GameObject parent, ItemGroupConfig itemGroup)
    {
        size = 0;
        int availableSubItems = itemGroup.displaySubItemCount;
        if (availableSubItems == 0)
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
        {
            PrintAllIGInformation("RemoveSubItemAtStart fail", itemGroup);
            return false;
        }

        for (int i = 0; i < itemGroup.nestedConstrainCount; i++)
        {
            GameObject oldSubItem = itemGroup.displaySubItemList[0];
            availableSubItems--;
            itemGroup.firstSubItemIdx++;
            OnDespawnSubItemAtStartEvent(itemGroup, oldSubItem);
            DespawnSubItem(itemGroup, true);

            if (availableSubItems == 0)
                break;
        }
        size = GetSubItemSize(itemGroup.GetSubItemRect(), parent.GetComponent<RectTransform>(), considerSpacing);

        /* Update the parameter of the scrollContent UI */
        if (!reverseDirection)
        {
            Vector2 offset = GetVector2(size);
            scrollContentRect.anchoredPosition -= offset;
            prevPos -= offset;
            contentStartPos -= offset;
        }

        if (size != 0)
        {
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(scrollContentRect);
        }

        /* Used for testing, can be deleted */
        PrintAllIGInformation("RemoveSubItemAtStart", itemGroup);

        return true;
    }

    private bool RemoveSubItemAtEnd(out float size, bool considerSpacing, GameObject parent, ItemGroupConfig itemGroup)
    {
        size = 0;
        int availableSubItems = itemGroup.displaySubItemCount;
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
             firstItemGroup.firstItemIdx == firstItemGroup.nestedItemIdx && firstItemGroup.firstSubItemIdx < firstItemGroup.nestedConstrainCount))
        {
            Debug.LogFormat("RemoveSubItemAtEnd fail, firstItemIdx: {0}, nestedItemIdx: {1}", firstItemGroup.firstItemIdx, firstItemGroup.nestedItemIdx);
            return false;
        }

        for (int i = 0; i < itemGroup.nestedConstrainCount; i++)
        {
            GameObject oldSubItem = itemGroup.displaySubItemList[itemGroup.displaySubItemCount - 1];
            availableSubItems--;
            itemGroup.lastSubItemIdx--;
            OnDespawnSubItemAtEndEvent(itemGroup, oldSubItem);
            DespawnSubItem(itemGroup, false);

            if (itemGroup.lastSubItemIdx % itemGroup.nestedConstrainCount == 0 || availableSubItems == 0)
                break;
        }
        size = GetSubItemSize(itemGroup.GetSubItemRect(), parent.GetComponent<RectTransform>(), considerSpacing);

        /* Update the parameter of the scrollContent UI */
        if (reverseDirection)
        {
            Vector2 offset = GetVector2(size);
            scrollContentRect.anchoredPosition += offset;
            prevPos += offset;
            contentStartPos += offset;
        }

        if (size != 0)
        {
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(scrollContentRect);
        }

        /* Used for testing, can be deleted */
        PrintAllIGInformation("RemoveSubItemAtEnd", itemGroup);


        return true;
    }

    private bool RemoveItemGroupAtStart()
    {
        if (displayItemGroupCount <= 0 || firstItemGroupIdx >= itemGroupCount)
        {
            Debug.LogFormat("RemoveItemGroupAtStart fail, displayItemGroupCount: {0}, firstItemGroupIdx: {1}", displayItemGroupCount, firstItemGroupIdx);
            return false;
        }

        var currentItemGroup = displayItemGroupList[0];
        if (currentItemGroup.displayItemCount > 0 || currentItemGroup.displaySubItemCount > 0)
        {
            Debug.LogFormat("RemoveItemGroupAtStart fail, displayItemCount: {0}, displaySubItemCount: {1}", currentItemGroup.displayItemCount, currentItemGroup.displaySubItemCount);
            return false;
        }

        displayItemGroupList.RemoveAt(0);
        firstItemGroupIdx++;

        /* Used for testing, can be deleted */
        PrintAllIGInformation("RemoveItemGroupAtStart", currentItemGroup);

        return true;
    }

    private bool RemoveItemGroupAtEnd()
    {
        if (displayItemGroupCount <= 0 || lastItemGroupIdx <= 0)
        {
            Debug.LogFormat("RemoveItemGroupAtEnd fail, displayItemGroupCount: {0}, lastItemGroupIdx: {1}， ", displayItemGroupCount, lastItemGroupIdx);
            return false;
        }

        var currentItemGroup = displayItemGroupList[displayItemGroupCount - 1];
        if (currentItemGroup.displayItemCount > 0 || currentItemGroup.displaySubItemCount > 0)
        {
            Debug.LogFormat("RemoveItemGroupAtEnd fail, displayItemCount: {0}, displaySubItemCount: {1}", currentItemGroup.displayItemCount, currentItemGroup.displaySubItemCount);
            return false;
        }

        displayItemGroupList.RemoveAt(displayItemGroupCount - 1);
        lastItemGroupIdx--;

        /* Used for testing, can be deleted */
        PrintAllIGInformation("RemoveItemGroupAtEnd", currentItemGroup);

        return true;
    }


    private bool RemoveElementAtStart(out float deltaSize, ItemGroupConfig itemGroup)
    {
        bool removeSuccess = false;
        float size = 0f;
        deltaSize = 0f;

        if (itemGroup.firstItemIdx == itemGroup.itemCount - 1 &&                            /* Case 1: the first item is the last item and is not a nested item */
            itemGroup.firstItemIdx != itemGroup.nestedItemIdx)
        {
            //PrintAllIGInformation("Remove element at start,  Before case 1", itemGroup);

            /* Need to consider upward item spacing between another item group */
            removeSuccess = RemoveItemAtStart(out size, true, itemGroup);
            deltaSize += size;
            removeSuccess = RemoveItemGroupAtStart();
        }
        else if (itemGroup.firstItemIdx == itemGroup.itemCount - 1 &&                       /* Case 2: the first item is the last item and is a nested item; the first subitem is the last subitem */
                 itemGroup.firstItemIdx == itemGroup.nestedItemIdx &&
                 itemGroup.firstSubItemIdx >= itemGroup.subItemCount - itemGroup.nestedConstrainCount)
        {
            //PrintAllIGInformation("Remove element at start,  Before case 2", itemGroup);

            removeSuccess = RemoveSubItemAtStart(out size, false, itemGroup.displayItemList[0], itemGroup);
            deltaSize += size;
            removeSuccess = RemoveItemAtStart(out size, true, itemGroup);
            deltaSize += size;
            removeSuccess = RemoveItemGroupAtStart();
        }
        else if (itemGroup.firstItemIdx != itemGroup.nestedItemIdx)                         /* Case 3: the first item is not the last item and is not a nested item */
        {
            //PrintAllIGInformation("Remove element at start,  Before case 3", itemGroup);

            removeSuccess = RemoveItemAtStart(out size, true, itemGroup);
            deltaSize += size;
        }
        else if (itemGroup.firstItemIdx == itemGroup.nestedItemIdx)
        {
            if (itemGroup.firstSubItemIdx + itemGroup.nestedConstrainCount >= itemGroup.lastSubItemIdx)    /* Case 4.1: the first item is not the last item and is a nested item; the first subitem is the last subitem */
            {
                //PrintAllIGInformation("Remove element at start,  Before case 4", itemGroup);

                removeSuccess = RemoveSubItemAtStart(out size, false, itemGroup.displayItemList[0], itemGroup);
                deltaSize += size;
                removeSuccess = RemoveItemAtStart(out size, true, itemGroup);
                deltaSize += size;
            }
            else                                                                                                    /* Case 4.2: the first item is not the last item and is a nested item; the first subitem is not the last subitem */
            {
                //PrintAllIGInformation("Remove element at start,  Before case 5", itemGroup);

                removeSuccess = RemoveSubItemAtStart(out size, true, itemGroup.displayItemList[0], itemGroup);
                deltaSize += size;
            }
        }

        return removeSuccess;
    }


    private bool RemoveElementAtEnd(out float deltaSize, ItemGroupConfig itemGroup)
    {
        bool removeSuccess = false;
        float size = 0f;
        deltaSize = 0f;

        if (itemGroup.lastItemIdx == 1 &&                                /* Case 1: the last item is the last item of the item group and is not a nested item */
            itemGroup.lastItemIdx != itemGroup.nestedItemIdx + 1)
        {
            //PrintAllIGInformation("Remove element at end,  Before case 1", itemGroup);

            /* Need to consider upward item spacing between another item group */
            removeSuccess = RemoveItemAtEnd(out size, true, itemGroup);
            deltaSize += size;
            removeSuccess = RemoveItemGroupAtEnd();
        }
        else if (itemGroup.lastItemIdx == 1 &&                            /* Case 2: the last item is the last item of the item group and is a nested item; the last subitem is the last subitem of the item group */
                 itemGroup.lastItemIdx == itemGroup.nestedItemIdx + 1 &&
                 itemGroup.lastSubItemIdx <= itemGroup.nestedConstrainCount)
        {
            //PrintAllIGInformation("Remove element at end,  Before case 2", itemGroup);

            removeSuccess = RemoveSubItemAtEnd(out size, false, itemGroup.displayItemList[itemGroup.displayItemCount - 1], itemGroup);
            deltaSize += size;
            removeSuccess = RemoveItemAtEnd(out size, true, itemGroup);
            deltaSize += size;
            removeSuccess = RemoveItemGroupAtEnd();
        }
        else if (itemGroup.lastItemIdx - 1 != itemGroup.nestedItemIdx)                  /* Case 3: the last item is not the last item of the item group and is not a nested item */
        {
            //PrintAllIGInformation("Remove element at end,  Before case 3", itemGroup);

            removeSuccess = RemoveItemAtEnd(out size, true, itemGroup);
            deltaSize += size;
        }
        else if (itemGroup.lastItemIdx - 1 == itemGroup.nestedItemIdx)
        {
            if ((itemGroup.lastSubItemIdx - itemGroup.nestedConstrainCount) <= itemGroup.firstSubItemIdx)             /* Case 4.1: the last item is not the last item of the item group and is a nested item; the last subitem is the last subitem of the item group */
            {
                //PrintAllIGInformation("Remove element at end,  Before case 4", itemGroup);

                removeSuccess = RemoveSubItemAtEnd(out size, false, itemGroup.displayItemList[itemGroup.displayItemCount - 1], itemGroup);
                deltaSize += size;
                removeSuccess = RemoveItemAtEnd(out size, true, itemGroup);
                deltaSize += size;
            }
            else                                                                                /* Case 4.2: the last item is not the last item of the item group and is a nested item; the last subitem is not the last subitem of the item group */
            {
                //PrintAllIGInformation("Remove element at end,  Before case 5", itemGroup);

                removeSuccess = RemoveSubItemAtEnd(out size, true, itemGroup.displayItemList[itemGroup.displayItemCount - 1], itemGroup);
                deltaSize += size;
            }
        }

        return removeSuccess;
    }

    #endregion


    #region 对象池操作

    protected GameObject Spawn(GameObject prefab)
    {
        GameObject obj = null;

        if (!itemPoolDict.ContainsKey(prefab))
        {
            obj = Instantiate(prefab) as GameObject;
            obj.AddComponent<DynamicScrollRectItem>().prefab = prefab;

            itemPoolDict.Add(prefab, new Stack<GameObject>());
        }
        else if (itemPoolDict[prefab].Count == 0)
        {
            obj = Instantiate(prefab) as GameObject;
            obj.AddComponent<DynamicScrollRectItem>().prefab = prefab;
        }
        else
        {
            obj = itemPoolDict[prefab].Pop();

            if (obj == null)
                obj = Spawn(prefab);
        }

        obj.gameObject.SetActive(true);
        return obj;
    }

    protected void Despawn(GameObject obj)
    {
        DynamicScrollRectItem pm = obj.GetComponent<DynamicScrollRectItem>();
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

    protected void DespawnItem(ItemGroupConfig itemGroup, bool fromStart)
    {
        if (itemGroup.displayItemCount == 0)
        {
            Debug.LogError("DynamicScrollRect: DespawnItem(): no item on display now but still trying to remove one!");
            return;
        }

        if (fromStart)
        {
            GameObject item = itemGroup.displayItemList[0];
            itemGroup.displayItemList.RemoveAt(0);
            Despawn(item);
        }
        else
        {
            GameObject item = itemGroup.displayItemList[itemGroup.displayItemCount - 1];
            itemGroup.displayItemList.RemoveAt(itemGroup.displayItemCount - 1);
            Despawn(item);
        }
    }

    protected void DespawnSubItem(ItemGroupConfig itemGroup, bool fromStart)
    {
        if (itemGroup.displaySubItemCount== 0)
        {
            Debug.LogError("DynamicScrollRect: DespawnSubItem(): no subItem on display now but still trying to remove one!");
            return;
        }

        if (fromStart)
        {
            GameObject subItem = itemGroup.displaySubItemList[0];
            itemGroup.displaySubItemList.RemoveAt(0);
            Despawn(subItem);
        }
        else
        {
            GameObject subItem = itemGroup.displaySubItemList[itemGroup.displaySubItemCount - 1];
            itemGroup.displaySubItemList.RemoveAt(itemGroup.displaySubItemCount - 1);
            Despawn(subItem);
        }
    }

    protected void ClearPool()
    {
        foreach (Stack<GameObject> pool in itemPoolDict.Values)
        {
            pool.Clear();
        }

        itemPoolDict.Clear();
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

        CanvasUpdateRegistry.RegisterCanvasElementForLayoutRebuild(this);
        LayoutRebuilder.MarkLayoutForRebuild(rect);
    }

    protected void UpdateCachedData()
    {
        Transform transform = this.transform;
        horizontalScrollbarRect = horizontalScrollbar == null ? null : horizontalScrollbar.transform as RectTransform;
        verticalScrollbarRect = verticalScrollbar == null ? null : verticalScrollbar.transform as RectTransform;

        /* These are true if either the elements are children, or they don't exist at all. */
        bool viewIsChild = (scrollViewRect.parent == transform);
        bool hScrollbarIsChild = (!horizontalScrollbarRect || horizontalScrollbarRect.parent == transform);
        bool vScrollbarIsChild = (!verticalScrollbarRect || verticalScrollbarRect.parent == transform);
        bool allAreChildren = (viewIsChild && hScrollbarIsChild && vScrollbarIsChild);

        horizontalSliderExpand = allAreChildren && horizontalScrollbarRect && horizontalScrollbarVisibility == ScrollbarVisibility.AutoHideAndExpandViewport;
        verticalSliderExpand = allAreChildren && verticalScrollbarRect && verticalScrollbarVisibility == ScrollbarVisibility.AutoHideAndExpandViewport;
        horizontalSliderHeight = (horizontalScrollbarRect == null ? 0 : horizontalScrollbarRect.rect.height);
        verticalSliderWidth = (verticalScrollbarRect == null ? 0 : verticalScrollbarRect.rect.width);
    }

    protected virtual void StopMovement()
    {
        velocity = Vector2.zero;
    }

    private void EnsureLayoutHasRebuilt()
    {
        if (!hasRebuiltLayout && !CanvasUpdateRegistry.IsRebuildingLayout())
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

    private void SetContentSizeFitter()
    {
        if (scrollContentRect != null)
        {
            ContentSizeFitter sizeFitter = scrollContentRect.GetComponent<ContentSizeFitter>();
            if (sizeFitter != null)
            {
                if (direction == ScrollDirection.Vertical)
                {
                    sizeFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
                    sizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                }
                else
                {
                    sizeFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
                    sizeFitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
                }
            }
        }
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

        if (direction == ScrollDirection.Vertical)
            size += itemRect.rect.height;
        else
            size += itemRect.rect.width;

        if (direction == ScrollDirection.Vertical)
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
        if (direction == ScrollDirection.Vertical)
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

        size *= GetAbsDimension(subContentRect.localScale);     // TO DO    // DO WHAT ??

        if (direction == ScrollDirection.Vertical)
            size *= subContentRect.localScale.y;
        else
            size *= subContentRect.localScale.x;

        return size;
    }


    protected virtual float GetItemGroupSize(ItemGroupConfig itemGroup)
    {
        float totalItemSize = 0f;
        float totalSubItemSize = 0f;

        foreach (GameObject item in itemGroup.itemList)
        {
            if (itemGroup.itemList.IndexOf(item) == itemGroup.nestedItemIdx && itemGroup.subItemCount <= 0)
                continue;

            totalItemSize += GetItemSize(item.GetComponent<RectTransform>(), false);
        }

        /* Need to condiser downward spacing between each item group except the last item group */
        if (itemGroup != itemGroupList[ItemGroupList.Count - 1])
            totalItemSize += itemSpacing * (itemGroup.itemCount - (itemGroup.subItemCount <= 0 ? 1 : 0));
        else
            totalItemSize += itemSpacing * (itemGroup.itemCount - (itemGroup.subItemCount <= 0 ? 2 : 1));

        int totalSubItemLines = Mathf.CeilToInt((float)itemGroup.subItemCount / (float)itemGroup.nestedConstrainCount);
        totalSubItemLines = totalSubItemLines > 0 ? totalSubItemLines : 0;
        totalSubItemSize += totalSubItemLines * GetSubItemSize(itemGroup.GetSubItemRect(), itemGroup.GetItemRect(itemGroup.nestedItemIdx), false);
        totalSubItemSize += (totalSubItemLines - 1) * GetSubItemSpacing(itemGroup.GetItemRect(itemGroup.nestedItemIdx));

        return totalItemSize + totalSubItemSize;
    }


    protected virtual Vector2 GetVector2(float value)
    {
        if (direction == ScrollDirection.Vertical)
            return new Vector2(0, value);
        else
            return new Vector2(-value, 0);
    }

    protected virtual Vector3 GetVector3(float value)
    {
        if (direction == ScrollDirection.Vertical)
            return new Vector3(0, value, 0);
        else
            return new Vector3(-value, 0, 0);
    }

    protected virtual Vector3 GetAbsVector3(float value)
    {
        if (direction == ScrollDirection.Vertical)
            return new Vector3(0, value, 0);
        else
            return new Vector3(value, 0, 0);
    }

    protected virtual float GetDimension(Vector2 vector)
    {
        if (direction == ScrollDirection.Vertical)
            return vector.y;
        else
            return -vector.x;
    }

    protected virtual float GetAbsDimension(Vector2 vector)
    {
        if (direction == ScrollDirection.Vertical)
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
        if (direction != ScrollDirection.Horizontal)
            position.x = scrollContentRect.anchoredPosition.x;
        if (direction != ScrollDirection.Vertical)
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

            ItemGroupConfig headItemGroup = itemGroupList[0];
            ItemGroupConfig tailItemGroup = itemGroupList[itemGroupCount - 1];
            /* Can continue move up as long as scrollContent dosn't reach top yet */
            if (GetDimension(delta) < 0 && (headItemGroup.nestedItemIdx == 0 ? headItemGroup.firstSubItemIdx > 0 : headItemGroup.firstItemIdx > 0))
                return offset;
            /* Can continue move down as long as scrollContent dosn't reach bottom yet */
            if (GetDimension(delta) > 0 && (tailItemGroup.nestedItemIdx == tailItemGroup.itemCount - 1 ? tailItemGroup.lastSubItemIdx < tailItemGroup.subItemCount : tailItemGroup.lastItemIdx < tailItemGroup.itemCount))
                return offset;
        }

        Vector2 min = scrollContentBounds.min;
        Vector2 max = scrollContentBounds.max;

        if (direction == ScrollDirection.Horizontal)
        {
            min.x += delta.x;
            max.x += delta.x;
            if (min.x > scrollViewBounds.min.x)
                offset.x = scrollViewBounds.min.x - min.x;
            else if (max.x < scrollViewBounds.max.x)
                offset.x = scrollViewBounds.max.x - max.x;
        }

        if (direction == ScrollDirection.Vertical)
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

    public void ScrollToItemGroup(int itemGroupIdx)
    {
        if (itemGroupIdx < 0 || itemGroupIdx >= itemGroupCount)
        {
            Debug.LogError("DynamicScrollRect: ScrollToItemGroup(): using invalid item group index!");
            return;
        }

        ScrollToItem(itemGroupIdx, 0);
    }


    public void ScrollToItem(int itemGroupIdx, int itemIdx)
    {
        if (itemGroupIdx < 0 || itemGroupIdx >= itemGroupCount)
        {
            Debug.LogError("DynamicScrollRect: ScrollToItem(): using invalid item group index!");
            return;
        }
        if (itemIdx < 0 || itemIdx >= itemGroupList[itemGroupIdx].itemCount)
        {
            Debug.LogError("DynamicScrollRect: ScrollToItem(): using invalid item index!");
            return;
        }

        bool upward;
        float offsetSize = 0f;
        ItemGroupConfig currItemGroup = itemGroupList[firstItemGroupIdx];

        /* Case 1: If the new location is before the head item of the head item group */
        if (itemGroupIdx < firstItemGroupIdx ||
           (itemGroupIdx == firstItemGroupIdx && itemIdx <= currItemGroup.firstItemIdx))
        {
            upward = false;
            if (direction == ScrollDirection.Vertical)
                offsetSize -= Mathf.Abs(scrollContentBounds.max.y - scrollViewBounds.max.y);
            else if (direction == ScrollDirection.Horizontal)
                offsetSize -= Mathf.Abs(scrollContentBounds.min.x - scrollViewBounds.min.x);

            if (currItemGroup.firstItemIdx == currItemGroup.nestedItemIdx)
            {
                int subItemLines = Mathf.CeilToInt((float)(currItemGroup.firstSubItemIdx) / currItemGroup.nestedConstrainCount);
                float subItemSize = GetSubItemSize(currItemGroup.GetSubItemRect(), currItemGroup.GetItemRect(currItemGroup.nestedItemIdx), false);
                float subItemSpacing = GetSubItemSpacing(currItemGroup.GetItemRect(currItemGroup.nestedItemIdx));
                offsetSize -= subItemSize * subItemLines + subItemSpacing * subItemLines;
            }

            for (int IGIdx = firstItemGroupIdx; IGIdx >= itemGroupIdx; IGIdx--)
            {
                currItemGroup = itemGroupList[IGIdx];
                int bound = IGIdx == itemGroupIdx ? itemIdx : 0;


                for (int IIdx = currItemGroup.firstItemIdx - 1; IIdx >= bound; IIdx--)
                {
                    if (IIdx == currItemGroup.nestedItemIdx)
                    {
                        int subItemLines = Mathf.CeilToInt((float)(currItemGroup.firstSubItemIdx) / currItemGroup.nestedConstrainCount);
                        float subItemSize = GetSubItemSize(currItemGroup.GetSubItemRect(), currItemGroup.GetItemRect(currItemGroup.nestedItemIdx), false);
                        float subItemSpacing = GetSubItemSpacing(currItemGroup.GetItemRect(currItemGroup.nestedItemIdx));
                        offsetSize -= subItemSize * subItemLines + subItemSpacing * (subItemLines - 1);
                    }

                    offsetSize -= GetItemSize(currItemGroup.GetItemRect(IIdx), true);
                }
            }
        }
        /* Case 2: If the new location is after the head item of the head item group */
        else
        {
            upward = true;
            if (direction == ScrollDirection.Vertical)
                offsetSize -= Mathf.Abs(scrollContentBounds.max.y - scrollViewBounds.max.y);
            else if (direction == ScrollDirection.Horizontal)
                offsetSize -= Mathf.Abs(scrollContentBounds.min.x - scrollViewBounds.min.x);

            for (int IGIdx = firstItemGroupIdx; IGIdx <= itemGroupIdx; IGIdx++)
            {
                currItemGroup = itemGroupList[IGIdx];
                int bound = IGIdx == itemGroupIdx ? itemIdx : currItemGroup.itemCount; 

                for ( int IIdx = currItemGroup.firstItemIdx; IIdx < bound; IIdx++)
                {
                    if (IIdx == currItemGroup.nestedItemIdx)
                    {
                        int subItemLines = Mathf.CeilToInt((float)(currItemGroup.subItemCount - currItemGroup.firstSubItemIdx) / currItemGroup.nestedConstrainCount);
                        float subItemSize = GetSubItemSize(currItemGroup.GetSubItemRect(), currItemGroup.GetItemRect(currItemGroup.nestedItemIdx), false);
                        float subItemSpacing = GetSubItemSpacing(currItemGroup.GetItemRect(currItemGroup.nestedItemIdx));
                        offsetSize += subItemSize * subItemLines + subItemSpacing * (subItemLines - 1);
                    }

                    bool considerSpacing = !(IGIdx == itemGroupCount - 1 && IIdx == itemGroupList[itemGroupCount - 1].itemCount - 1);
                    offsetSize += GetItemSize(currItemGroup.GetItemRect(IIdx), considerSpacing);
                }
            }
        }

        StartCoroutine(ScrollTo(offsetSize, 0.5f, upward));
    }


    public void ScrollToSubItem(int itemGroupIdx, int subItemIdx)
    {
        if (itemGroupIdx < 0 || itemGroupIdx >= itemGroupCount)
        {
            Debug.LogError("DynamicScrollRect: ScrollToSubItem(): using invalid item group index!");
            return;
        }
        if (subItemIdx < 0 || subItemIdx >= itemGroupList[itemGroupIdx].subItemCount)
        {
            Debug.LogError("DynamicScrollRect: ScrollToSubItem(): using invalid subItem index!");
            return;
        }

        bool upward;
        float offsetSize = 0f;
        ItemGroupConfig currItemGroup = itemGroupList[firstItemGroupIdx];

        /* Case 1: If the new location is before the head item of the head item group */
        if (itemGroupIdx < firstItemGroupIdx ||
           (itemGroupIdx == firstItemGroupIdx && subItemIdx <= currItemGroup.firstSubItemIdx))
        {
            upward = false;
            if (direction == ScrollDirection.Vertical)
                offsetSize -= Mathf.Abs(scrollContentBounds.max.y - scrollViewBounds.max.y);
            else if (direction == ScrollDirection.Horizontal)
                offsetSize -= Mathf.Abs(scrollContentBounds.min.x - scrollViewBounds.min.x);

            for (int IGIdx = firstItemGroupIdx; IGIdx >= itemGroupIdx; IGIdx--)
            {
                currItemGroup = itemGroupList[IGIdx];
                int bound = IGIdx == itemGroupIdx ? currItemGroup.nestedItemIdx : 0;

                if (currItemGroup.firstItemIdx == currItemGroup.nestedItemIdx)
                {
                    int subItemLines = Mathf.CeilToInt((float)(currItemGroup.firstSubItemIdx) / currItemGroup.nestedConstrainCount);
                    float subItemSize = GetSubItemSize(currItemGroup.GetSubItemRect(), currItemGroup.GetItemRect(currItemGroup.nestedItemIdx), false);
                    float subItemSpacing = GetSubItemSpacing(currItemGroup.GetItemRect(currItemGroup.nestedItemIdx));
                    offsetSize -= subItemSize * subItemLines + subItemSpacing * subItemLines;
                }

                for (int IIdx = currItemGroup.firstItemIdx - 1; IIdx >= bound; IIdx--)
                {
                    if (IIdx == currItemGroup.nestedItemIdx)
                    {
                        int subItemLines;
                        if (IGIdx == itemGroupIdx)
                            subItemLines = Mathf.CeilToInt((float)(currItemGroup.firstSubItemIdx) / currItemGroup.nestedConstrainCount) - 
                                           Mathf.FloorToInt((float)(subItemIdx + 1) / currItemGroup.nestedConstrainCount);
                        else
                            subItemLines = Mathf.CeilToInt((float)(currItemGroup.firstSubItemIdx) / currItemGroup.nestedConstrainCount);

                        float subItemSize = GetSubItemSize(currItemGroup.GetSubItemRect(), currItemGroup.GetItemRect(currItemGroup.nestedItemIdx), false);
                        float subItemSpacing = GetSubItemSpacing(currItemGroup.GetItemRect(currItemGroup.nestedItemIdx));
                        offsetSize -= subItemSize * subItemLines + subItemSpacing * (subItemLines - 1);
                    }

                    bool considerSpacing = !(IGIdx == itemGroupIdx && IIdx == currItemGroup.nestedItemIdx);
                    offsetSize -= GetItemSize(currItemGroup.GetItemRect(IIdx), considerSpacing);
                }
            }
        }
        else
        {
            upward = true;
            if (direction == ScrollDirection.Vertical)
                offsetSize -= Mathf.Abs(scrollContentBounds.max.y - scrollViewBounds.max.y);
            else if (direction == ScrollDirection.Horizontal)
                offsetSize -= Mathf.Abs(scrollContentBounds.min.x - scrollViewBounds.min.x);

            for (int IGIdx = firstItemGroupIdx; IGIdx <= itemGroupIdx; IGIdx++)
            {
                currItemGroup = itemGroupList[IGIdx];
                int bound = IGIdx == itemGroupIdx ? currItemGroup.nestedItemIdx : currItemGroup.itemCount - 1;

                for (int IIdx = currItemGroup.firstItemIdx; IIdx <= bound; IIdx++)
                {
                    if (IIdx == currItemGroup.nestedItemIdx)
                    {
                        int subItemLines;
                        if (IGIdx == itemGroupIdx)
                            subItemLines = Mathf.FloorToInt((float)(subItemIdx - currItemGroup.firstItemIdx) / currItemGroup.nestedConstrainCount);
                        else
                            subItemLines = Mathf.CeilToInt((float)(currItemGroup.subItemCount - currItemGroup.firstSubItemIdx) / currItemGroup.nestedConstrainCount);

                        float subItemSize = GetSubItemSize(currItemGroup.GetSubItemRect(), currItemGroup.GetItemRect(currItemGroup.nestedItemIdx), false);
                        float subItemSpacing = GetSubItemSpacing(currItemGroup.GetItemRect(currItemGroup.nestedItemIdx));
                        offsetSize += subItemSize * subItemLines + subItemSpacing * (subItemLines - (IGIdx == itemGroupIdx ? 0 : 1));
                    }

                    bool considerSpacing = !(IGIdx == itemGroupIdx && IIdx == currItemGroup.nestedItemIdx);
                    offsetSize += GetItemSize(currItemGroup.GetItemRect(IIdx), considerSpacing);
                }
            }
        }

        StartCoroutine(ScrollTo(offsetSize, 0.5f, upward));
    }

    public IEnumerator ScrollTo(float offsetSize, float time, bool upward)
    {
        Vector2 velocity = Vector2.zero;
        Vector2 prevPos = GetVector2(offsetSize);
        Vector2 currPos = GetVector2(offsetSize);

        while (Vector2.Distance(currPos, Vector2.zero) >= 1f)
        {
            currPos = Vector2.SmoothDamp(currPos, Vector2.zero, ref velocity, time);
            scrollContentRect.anchoredPosition += prevPos - currPos;
            UpdateBounds(true);
            prevPos = currPos;
            yield return null;
        }

        if (upward)
            RemoveElementAtStart(out _, itemGroupList[firstItemGroupIdx]);

        yield break;
    }

    #endregion


    #region scrollbar计算相关

    private float GetHorizontalNormalizedPosition()
    {
        UpdateBounds();
        if (itemGroupCount > 0 && lastItemGroupIdx > firstItemGroupIdx)
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
        if (itemGroupCount > 0 && lastItemGroupIdx > firstItemGroupIdx)
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
        if (itemGroupCount <= 0 || lastItemGroupIdx <= firstItemGroupIdx)
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
        totalSize = 0;
        offset = scrollContentBounds.min.x;

        foreach (ItemGroupConfig itemGroup in itemGroupList)
        {
            totalSize += GetItemGroupSize(itemGroup);
        }

        ItemGroupConfig headItemGroup = itemGroupList[firstItemGroupIdx];
        for (int i = 0; i < firstItemGroupIdx; i++)
        {
            offset -= GetItemGroupSize(itemGroupList[i]);
        }

        /* Notice that head item group might not fully dispaly its items */
        for (int j = 0; j < headItemGroup.firstItemIdx; j++)
        {
            if (j != headItemGroup.nestedItemIdx)
                offset -= GetItemSize(headItemGroup.GetItemRect(j), true);
            else
            {
                int hiddenSubItemLines = Mathf.CeilToInt((float)headItemGroup.subItemCount / (float)headItemGroup.nestedConstrainCount);
                hiddenSubItemLines = hiddenSubItemLines > 0 ? hiddenSubItemLines : 0;
                offset -= hiddenSubItemLines * GetSubItemSize(headItemGroup.GetSubItemRect(), headItemGroup.GetItemRect(headItemGroup.nestedItemIdx), false);
                offset -= (hiddenSubItemLines - 1) * GetSubItemSpacing(headItemGroup.GetItemRect(headItemGroup.nestedItemIdx));
            }
        }

        /* Special case: the head item in the head item group is a nested item, and it might not fully display its subItems */
        if (headItemGroup.firstItemIdx == headItemGroup.nestedItemIdx)
        {
            int hiddenSubItemLines = Mathf.CeilToInt((float)(headItemGroup.firstSubItemIdx) / (float)headItemGroup.nestedConstrainCount);
            hiddenSubItemLines = hiddenSubItemLines > 0 ? hiddenSubItemLines : 0;
            offset -= hiddenSubItemLines * GetSubItemSize(headItemGroup.GetSubItemRect(), headItemGroup.GetItemRect(headItemGroup.nestedItemIdx), true);
        }
    }

    private void GetVerticalOffsetAndSize(out float totalSize, out float offset)
    {
        totalSize = 0;
        offset = scrollContentBounds.max.y;

        foreach (ItemGroupConfig itemGroup in itemGroupList)
        {
            totalSize += GetItemGroupSize(itemGroup);
        }

        ItemGroupConfig headItemGroup = itemGroupList[firstItemGroupIdx];
        for (int i = 0; i < firstItemGroupIdx; i++)
        {
            offset += GetItemGroupSize(itemGroupList[i]);
        }

        /* Notice that head item group might not fully dispaly its items */
        for (int j = 0; j < headItemGroup.firstItemIdx; j++)
        {
            if (j != headItemGroup.nestedItemIdx)
                offset += GetItemSize(headItemGroup.GetItemRect(j), true);
            else
            {
                int hiddenSubItemLines = Mathf.CeilToInt((float)headItemGroup.subItemCount / (float)headItemGroup.nestedConstrainCount);
                hiddenSubItemLines = hiddenSubItemLines > 0 ? hiddenSubItemLines : 0;
                offset += hiddenSubItemLines * GetSubItemSize(headItemGroup.GetSubItemRect(), headItemGroup.GetItemRect(headItemGroup.nestedItemIdx), false);
                offset += (hiddenSubItemLines - 1) * GetSubItemSpacing(headItemGroup.GetItemRect(headItemGroup.nestedItemIdx));
            }
        }

        /* Special case: the head item in the head item group is a nested item, and it might not fully display its subItems */
        if (headItemGroup.firstItemIdx == headItemGroup.nestedItemIdx)
        {
            int hiddenSubItemLines = Mathf.CeilToInt((float)(headItemGroup.firstSubItemIdx) / (float)headItemGroup.nestedConstrainCount);
            hiddenSubItemLines = hiddenSubItemLines > 0 ? hiddenSubItemLines : 0;
            offset += hiddenSubItemLines * GetSubItemSize(headItemGroup.GetSubItemRect(), headItemGroup.GetItemRect(headItemGroup.nestedItemIdx), true);
        }
    }

    private void UpdateScrollbars(Vector2 offset)
    {
        if (horizontalScrollbar)
        {
            if (scrollContentBounds.size.x > 0 && itemGroupCount > 0)
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
            if (scrollContentBounds.size.y > 0 && itemGroupCount > 0)
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

    private void UpdateScrollbarVisibility()
    {
        if (verticalScrollbar && verticalScrollbarVisibility != ScrollbarVisibility.Permanent && verticalScrollbar.gameObject.activeSelf != verticalScrollingNeeded)
            verticalScrollbar.gameObject.SetActive(verticalScrollingNeeded);
        if (horizontalScrollbar && horizontalScrollbarVisibility != ScrollbarVisibility.Permanent && horizontalScrollbar.gameObject.activeSelf != horizontalScrollingNeeded)
            horizontalScrollbar.gameObject.SetActive(horizontalScrollingNeeded);
    }

    private void UpdateScrollbarLayout()
    {
        if (verticalSliderExpand && horizontalScrollbar)
        {
            tracker.Add(this, horizontalScrollbarRect,
                          DrivenTransformProperties.AnchorMinX |
                          DrivenTransformProperties.AnchorMaxX |
                          DrivenTransformProperties.SizeDeltaX |
                          DrivenTransformProperties.AnchoredPositionX);
            horizontalScrollbarRect.anchorMin = new Vector2(0, horizontalScrollbarRect.anchorMin.y);
            horizontalScrollbarRect.anchorMax = new Vector2(1, horizontalScrollbarRect.anchorMax.y);
            horizontalScrollbarRect.anchoredPosition = new Vector2(0, horizontalScrollbarRect.anchoredPosition.y);
            if (verticalScrollingNeeded)
                horizontalScrollbarRect.sizeDelta = new Vector2(-(verticalSliderWidth + verticalScrollbarSpacing), horizontalScrollbarRect.sizeDelta.y);
            else
                horizontalScrollbarRect.sizeDelta = new Vector2(0, horizontalScrollbarRect.sizeDelta.y);
        }

        if (horizontalSliderExpand && verticalScrollbar)
        {
            tracker.Add(this, verticalScrollbarRect,
                          DrivenTransformProperties.AnchorMinY |
                          DrivenTransformProperties.AnchorMaxY |
                          DrivenTransformProperties.SizeDeltaY |
                          DrivenTransformProperties.AnchoredPositionY);
            verticalScrollbarRect.anchorMin = new Vector2(verticalScrollbarRect.anchorMin.x, 0);
            verticalScrollbarRect.anchorMax = new Vector2(verticalScrollbarRect.anchorMax.x, 1);
            verticalScrollbarRect.anchoredPosition = new Vector2(verticalScrollbarRect.anchoredPosition.x, 0);
            if (horizontalScrollingNeeded)
                verticalScrollbarRect.sizeDelta = new Vector2(verticalScrollbarRect.sizeDelta.x, -(horizontalSliderHeight + horizontalScrollbarSpacing));
            else
                verticalScrollbarRect.sizeDelta = new Vector2(verticalScrollbarRect.sizeDelta.x, 0);
        }
    }

    #endregion


    #region scrollitem相关

    public void RefillScrollContent(int itemGroupBeginIdx = 0, float contentOffset = 0f)
    {
        float size = 0f;
        float sizeFilled = 0f;
        float sizeToFill = GetAbsDimension(scrollViewRect.rect.size) + Mathf.Abs(contentOffset);

        if (!Application.isPlaying || itemGroupCount == 0)
            return;

        /* Remove all existing on-display element of the scroll, from tail to head. */
        /* After remove, the head pointer and tail pointer of item groups, items, and subItems should equal to each other respectively */
        ItemGroupConfig headItemGroup = itemGroupList[firstItemGroupIdx < itemGroupCount ? firstItemGroupIdx : itemGroupCount - 1];
        ItemGroupConfig currItemGroup = itemGroupList[lastItemGroupIdx > 0 ? lastItemGroupIdx - 1 : 0];
        while (true)
        {
            bool removeSuccess = false;

            if (headItemGroup.displayItemCount == 0 && headItemGroup.displaySubItemCount == 0)      /* Remove all displaying items and subitems from tail to head */
                break;

            removeSuccess = RemoveElementAtEnd(out _, currItemGroup);
            currItemGroup = itemGroupList[lastItemGroupIdx > 0 ? lastItemGroupIdx - 1 : 0];

            if (!removeSuccess)
                break;
        }

        firstItemGroupIdx = itemGroupBeginIdx;
        lastItemGroupIdx = itemGroupBeginIdx;
        AddItemGroupAtEnd(out sizeFilled, scrollContent);

        currItemGroup = itemGroupList[firstItemGroupIdx < itemGroupCount ? firstItemGroupIdx : itemGroupCount - 1];
        while (sizeFilled < sizeToFill)
        {
            bool addSuccess = AddElementAtEnd(out size, currItemGroup);
            sizeFilled += size;
            currItemGroup = itemGroupList[lastItemGroupIdx > 0 ? lastItemGroupIdx - 1 : 0];

            if (!addSuccess)
                break;
        }

        /* refill from start in case not full yet */
        currItemGroup = itemGroupList[firstItemGroupIdx < itemGroupCount ? firstItemGroupIdx : itemGroupCount - 1];
        while (sizeFilled < sizeToFill)
        {
            bool addSuccess = AddElementAtStart(out size, currItemGroup);
            sizeFilled += size;
            currItemGroup = itemGroupList[firstItemGroupIdx < itemGroupCount ? firstItemGroupIdx : itemGroupCount - 1];

            if (!addSuccess)
                break;
        }

        Vector2 pos = scrollContentRect.anchoredPosition;
        if (direction == ScrollDirection.Vertical)
            pos.y = -contentOffset;
        else
            pos.x = contentOffset;
        scrollContentRect.anchoredPosition = pos;
        contentStartPos = pos;

        /* force build bounds here so scrollbar can access newest bounds */
        LayoutRebuilder.ForceRebuildLayoutImmediate(scrollContentRect);
        CalculateContentBounds();
        UpdateScrollbars(Vector2.zero);
        StopMovement();
        UpdatePrevData();
    }


    public void UpdateScrollItemGroups()
    {
        ///* Used for testing, can be deleted */
        //PrintAllIGInformation();+
        scrollBoundMax = scrollViewBounds.max;
        scrollBoundMin = scrollViewBounds.min;
        contentBoundMax = scrollContentBounds.max;
        contentBoundMin = scrollContentBounds.min;


        if (itemGroupCount == 0)
            return;

        ItemGroupConfig currItemGroup;

        //// TO DO: Fast version, unstable
        /* special case 1: handling move several page upward in one frame */
        if ((direction == ScrollDirection.Vertical   && scrollViewBounds.max.y < scrollContentBounds.min.y) ||
            (direction == ScrollDirection.Horizontal && scrollViewBounds.min.x > scrollContentBounds.max.x) &&
            lastItemGroupIdx > firstItemGroupIdx)
        {
            float contentSize = GetAbsDimension(scrollContentBounds.size);
            float offsetSize = 0f;
            float deltaSize = 0f;
            float size = 0f;

            if (direction == ScrollDirection.Vertical)
                offsetSize = GetDimension(scrollContentBounds.min) - GetDimension(scrollViewBounds.max);
            else
                offsetSize = GetDimension(scrollContentBounds.max) - GetDimension(scrollViewBounds.min);

            ItemGroupConfig tailItemGroup = itemGroupList[lastItemGroupIdx > 0 ? lastItemGroupIdx - 1 : 0];
            currItemGroup = itemGroupList[firstItemGroupIdx];

            print("????????????????????????????????");
            PrintAllIGInformation("Special Case 1, Before the whole process");

            /* Remove all existing on-display element of the scroll, from head to head. */
            /* After remove, the head pointer and tail pointer of item groups, items, and subItems should equal to each other respectively */
            while (true)
            {
                bool removeSuccess = false;

                if (tailItemGroup.displayItemCount == 0 && tailItemGroup.displaySubItemCount == 0)          /* Remove all displaying items and subitems from head to tail */
                    break;

                removeSuccess = RemoveElementAtStart(out _, currItemGroup);
                currItemGroup = itemGroupList[firstItemGroupIdx];

                if (!removeSuccess)
                    break;
            }

            /* Used for testing, can be deleted */
            var temp1 = displayItemGroupList;
            var temp2 = firstItemGroupIdx;
            var temp3 = lastItemGroupIdx;
            PrintAllIGInformation("Special case 1, After clearing old elements");

            RemoveItemGroupAtStart();

            /* Set the head pointer and tail pointer of item groups, items, and subItems to the element which located in the head of the scroll view */
            /* We do this by virtually adding element to the scroll view (not create element object, only alter element data) */
            currItemGroup = itemGroupList[lastItemGroupIdx > 0 ? lastItemGroupIdx - 1 : 0];
            while (deltaSize < offsetSize)
            {
                /* Virtually add item group at end */
                if ((currItemGroup.lastItemIdx == currItemGroup.itemCount && currItemGroup.lastItemIdx != currItemGroup.nestedItemIdx + 1) ||
                    (currItemGroup.lastItemIdx == currItemGroup.itemCount && currItemGroup.lastItemIdx == currItemGroup.nestedItemIdx + 1 && currItemGroup.lastSubItemIdx == currItemGroup.subItemCount))
                {
                    if (lastItemGroupIdx >= itemGroupCount)
                        break;

                    lastItemGroupIdx++;
                    firstItemGroupIdx++;
                    currItemGroup = itemGroupList[lastItemGroupIdx > 0 ? lastItemGroupIdx - 1 : 0];
                }
                /* Virtually add subItems at end */
                else if (currItemGroup.lastItemIdx - 1 == currItemGroup.nestedItemIdx && currItemGroup.subItemCount > 0 && currItemGroup.lastSubItemIdx < currItemGroup.subItemCount)
                {
                    if (currItemGroup.lastSubItemIdx >= currItemGroup.subItemCount)
                        break;

                    int count = currItemGroup.nestedConstrainCount - (currItemGroup.lastSubItemIdx % currItemGroup.nestedConstrainCount);
                    if (currItemGroup.lastSubItemIdx >= currItemGroup.subItemCount - (currItemGroup.subItemCount % currItemGroup.nestedConstrainCount))
                        count = currItemGroup.subItemCount - currItemGroup.lastSubItemIdx;

                    if (currItemGroup.lastSubItemIdx + count > currItemGroup.subItemCount)
                    {
                        Debug.LogErrorFormat("Special case 1, vritually add subItem at end fail, lastSubItemIdx: {0}, count: {1}", currItemGroup.lastSubItemIdx, count);
                        break;
                    }

                    currItemGroup.lastSubItemIdx += count;
                    currItemGroup.firstSubItemIdx += count;
                    size = 0;
                    size = GetSubItemSize(currItemGroup.GetSubItemRect(), currItemGroup.GetItemRect(currItemGroup.nestedItemIdx), true) ;
                    deltaSize += size;
                }
                /* Virtually add items at end */
                else
                {
                    if (currItemGroup.lastItemIdx >= currItemGroup.itemCount)
                        break;

                    currItemGroup.lastItemIdx++;
                    currItemGroup.firstItemIdx++;
                    size = 0;
                    size = GetItemSize(currItemGroup.GetItemRect(currItemGroup.lastItemIdx - 1), true);
                    deltaSize += size;
                }
            }
            
            deltaSize = 0f;

            /* Used for testing, can be deleted */
            PrintAllIGInformation("Special case 1, After reseting pointer to tail");

            firstItemGroupIdx = lastItemGroupIdx > 0 ? lastItemGroupIdx - 1 : 0;
            displayItemGroupList.Add(currItemGroup);

            AddItemAtStart(out size, false, currItemGroup.itemList[currItemGroup.lastItemIdx > 0 ? currItemGroup.lastItemIdx - 1 : 0], scrollContent, currItemGroup);
            deltaSize += size;

            if (currItemGroup.lastItemIdx - 1 == currItemGroup.nestedItemIdx && currItemGroup.subItemCount > 0 && currItemGroup.lastItemIdx < currItemGroup.subItemCount)
            {
                AddSubItemAtStart(out size, false, currItemGroup.subItem, currItemGroup.displayItemList[currItemGroup.displayItemCount - 1], currItemGroup);
                deltaSize += size;
            }

            scrollContentRect.anchoredPosition -= GetVector2(offsetSize);
            scrollContentBounds.center -= GetVector3(offsetSize + (contentSize + deltaSize) / 2);
            scrollContentBounds.size = GetAbsVector3(deltaSize);

            /* Used for testing, can be deleted */
            Debug.LogFormat("Special case 1, after reloaction, offsetSize: {0}, contentSize: {1}, deltaSize: {2}", offsetSize, contentSize, deltaSize);
            Debug.LogFormat("Special case 1, after reloaction, scrollContentRect.anchoredPosition : {0}, scrollContentBounds.center: {1}", scrollContentRect.anchoredPosition, scrollContentBounds.center);
            PrintAllIGInformation("Special case 1, After the whole process finish");
        }


        // TO DO: Fast version, unstable 
        /* special case 2: handling move several page downward in one frame */
        if ((direction == ScrollDirection.Vertical   && scrollViewBounds.min.y > scrollContentBounds.max.y) ||
            (direction == ScrollDirection.Horizontal && scrollViewBounds.max.x < scrollContentBounds.min.x) &&
            lastItemGroupIdx > firstItemGroupIdx)
        {
            float contentSize = GetAbsDimension(scrollContentBounds.size);
            float offsetSize = 0f;
            float deltaSize = 0f;
            float size = 0f;

            if (direction == ScrollDirection.Vertical)
                offsetSize = GetDimension(scrollViewBounds.min) - GetDimension(scrollContentBounds.max);
            else
                offsetSize = GetDimension(scrollViewBounds.max) - GetDimension(scrollContentBounds.min);

            ItemGroupConfig headItemGroup = itemGroupList[firstItemGroupIdx];
            currItemGroup = itemGroupList[lastItemGroupIdx > 0 ? lastItemGroupIdx - 1 : 0];


            print("////////////////////////////, Special case 2");
            PrintAllIGInformation("Special case 2, Before the whole process");

            /* Remove all existing on-display element of the scroll, from tail to head. */
            /* After remove, the head pointer and tail pointer of item groups, items, and subItems should equal to each other respectively */
            while (true)
            {
                bool removeSuccess = false;

                if (headItemGroup.displayItemCount == 0 && headItemGroup.displaySubItemCount == 0)      /* Remove all displaying items and subitems from tail to head */
                    break;

                removeSuccess = RemoveElementAtEnd(out _, currItemGroup);
                currItemGroup = itemGroupList[lastItemGroupIdx > 0 ? lastItemGroupIdx - 1 : 0];

                if (!removeSuccess)
                    break;
            }

            /* Used for testing, can be deleted */
            PrintAllIGInformation("Special case 2, After clearing old elements");

            RemoveItemGroupAtEnd();

            /* Set the head pointer and tail pointer of item groups, items, and subItems to the element which located in the head of the scroll view */
            /* We do this by virtually adding element to the scroll view (not create element object, only alter element data) */
            currItemGroup = itemGroupList[firstItemGroupIdx];
            while (deltaSize < offsetSize + GetAbsDimension(scrollViewBounds.size))
            {
                /* Virtually add item group at start */
                if ((currItemGroup.firstItemIdx <= 0 && currItemGroup.firstItemIdx != currItemGroup.nestedItemIdx) ||
                    (currItemGroup.firstItemIdx <= 0 && currItemGroup.firstItemIdx == currItemGroup.nestedItemIdx && currItemGroup.firstSubItemIdx <= 0))
                {
                    if (firstItemGroupIdx <= 0)
                        break;

                    firstItemGroupIdx--;
                    lastItemGroupIdx--;
                    currItemGroup = itemGroupList[firstItemGroupIdx];
                }
                /* Virtually add subItem at start */
                else if (currItemGroup.firstItemIdx == currItemGroup.nestedItemIdx && currItemGroup.subItemCount > 0 && currItemGroup.firstSubItemIdx > 0)
                {
                    if (currItemGroup.firstSubItemIdx <= 0)
                        break;

                    int count = (currItemGroup.firstSubItemIdx) % currItemGroup.nestedConstrainCount;
                    if (count == 0)
                        count = currItemGroup.nestedConstrainCount;
                    if (currItemGroup.firstSubItemIdx < currItemGroup.nestedConstrainCount)
                        count = currItemGroup.firstSubItemIdx;

                    if (currItemGroup.firstSubItemIdx - count < 0)
                    {
                        Debug.LogErrorFormat("Special case 2, vritually add subItem at start fail, firstSubItemIdx: {0}, count: {1}", currItemGroup.firstSubItemIdx, count);
                        break;
                    }

                    currItemGroup.firstSubItemIdx -= count;
                    currItemGroup.lastSubItemIdx -= count;
                    size = 0;
                    size = GetSubItemSize(currItemGroup.GetSubItemRect(), currItemGroup.GetItemRect(currItemGroup.nestedItemIdx), true);
                    deltaSize += size;
                }
                /* Virtually add item at start */
                else
                {
                    if (currItemGroup.firstItemIdx <= 0)
                        break;

                    currItemGroup.firstItemIdx--;
                    currItemGroup.lastItemIdx--;
                    size = 0;
                    size = GetItemSize(currItemGroup.GetItemRect(currItemGroup.firstItemIdx), true);
                    deltaSize += size;
                }
            }

            deltaSize = 0f;

            /* Used for testing, can be deleted */
            PrintAllIGInformation("Special case 2, After reseting pointer to head");

            lastItemGroupIdx = firstItemGroupIdx + 1;
            displayItemGroupList.Add(currItemGroup);

            AddItemAtEnd(out size, false, currItemGroup.itemList[currItemGroup.lastItemIdx], scrollContent, currItemGroup);
            deltaSize += size;

            if (currItemGroup.lastItemIdx - 1 == currItemGroup.nestedItemIdx && currItemGroup.subItemCount > 0 && currItemGroup.lastSubItemIdx < currItemGroup.subItemCount)
            {
                AddSubItemAtEnd(out size, false, currItemGroup.subItem, currItemGroup.displayItemList[currItemGroup.displayItemCount - 1], currItemGroup);
                deltaSize += size;
            }

            scrollContentRect.anchoredPosition += GetVector2(offsetSize + GetAbsDimension(scrollViewBounds.size) + (reverseDirection ? contentSize : 0));
            scrollContentBounds.center += GetVector3(offsetSize + GetAbsDimension(scrollViewBounds.size) + (contentSize - deltaSize) / 2);
            scrollContentBounds.size = GetAbsVector3(deltaSize);


            /* Used for testing, can be deleted */
            Debug.LogFormat("Special case 2, after reloaction, offsetSize: {0}, contentSize: {1}, deltaSize: {2}", offsetSize, contentSize, deltaSize);
            Debug.LogFormat("Special case 2, after reloaction, scrollContentRect.anchoredPosition : {0}, scrollContentBounds.center: {1}", scrollContentRect.anchoredPosition, scrollContentBounds.center);
            PrintAllIGInformation("Special case 2, After the whole process finish");
        }


        float itemSize = 0f;

        /* Case 1: the bottom of the last item is much higher than the bottom the viewPort */
        /* Need to add new items at the bottom of the scrollContent */
        currItemGroup = itemGroupList[lastItemGroupIdx > 0 ? lastItemGroupIdx - 1 : 0];
        if ((direction == ScrollDirection.Vertical   && scrollViewBounds.min.y < scrollContentBounds.min.y + contentDownPadding) ||
            (direction == ScrollDirection.Horizontal && scrollViewBounds.max.x > scrollContentBounds.max.x - contentRightPadding))
        {
            float size = 0f;
            float deltaSize = 0f;

            AddElementAtEnd(out size, currItemGroup);
            currItemGroup = itemGroupList[lastItemGroupIdx > 0 ? lastItemGroupIdx - 1 : 0];
            deltaSize += size;

            while ((direction == ScrollDirection.Vertical   && scrollViewBounds.min.y < scrollContentBounds.min.y + contentDownPadding - deltaSize) ||
                   (direction == ScrollDirection.Horizontal && scrollViewBounds.max.x > scrollContentBounds.max.x - contentRightPadding + deltaSize) &&
                   size > 0)
            {
                bool addSuccess = AddElementAtEnd(out size, currItemGroup);
                currItemGroup = itemGroupList[lastItemGroupIdx > 0 ? lastItemGroupIdx - 1 : 0];
                deltaSize += size;

                if (!addSuccess)
                    break;
            }
        }

        /* Case 2: the top of the first item is much lower than the top of the viewPort */
        /* Need to add new items at the top of the scrollContent */
        currItemGroup = itemGroupList[firstItemGroupIdx < itemGroupCount ? firstItemGroupIdx : itemGroupCount - 1];
        if ((direction == ScrollDirection.Vertical   && scrollViewBounds.max.y > scrollContentBounds.max.y - contentTopPadding) ||
            (direction == ScrollDirection.Horizontal && scrollViewBounds.min.x < scrollContentBounds.min.x + contentLeftPadding))
        {
            float size = 0f;
            float deltaSize = 0f;

            AddElementAtStart(out size, currItemGroup);
            currItemGroup = itemGroupList[firstItemGroupIdx < itemGroupCount ? firstItemGroupIdx : itemGroupCount - 1];
            deltaSize += size;

            while ((direction == ScrollDirection.Vertical   && scrollViewBounds.max.y > scrollContentBounds.max.y - contentTopPadding + deltaSize) ||
                   (direction == ScrollDirection.Horizontal && scrollViewBounds.min.x < scrollContentBounds.min.x + contentLeftPadding - deltaSize) &&
                   size > 0)
            {
                bool addSuccess = AddElementAtStart(out size, currItemGroup);
                currItemGroup = itemGroupList[firstItemGroupIdx < itemGroupCount ? firstItemGroupIdx : itemGroupCount - 1];
                deltaSize += size;

                if (!addSuccess)
                    break;
            }
        }

        /* Case 3: the top of the last item is much lower than the bottom of the viewPort */
        /* Need to remove old items at the bottom of the scrollContent */
        currItemGroup = ItemGroupList[lastItemGroupIdx > 0 ? lastItemGroupIdx - 1 : 0];
        if (currItemGroup.lastItemIdx - 1 != currItemGroup.nestedItemIdx ||
            (currItemGroup.lastItemIdx - 1 == currItemGroup.nestedItemIdx && currItemGroup.lastSubItemIdx <= currItemGroup.nestedConstrainCount))       /* special case: the last item is a nested item and it only have the first row of subitem, we consider it as a non-nested item */
            itemSize = GetItemSize(currItemGroup.GetDisplayItemRect(currItemGroup.displayItemCount - 1), true);
        else
            itemSize = GetSubItemSize(currItemGroup.GetSubItemRect(), currItemGroup.GetItemRect(currItemGroup.nestedItemIdx), true);

        if ((direction == ScrollDirection.Vertical   && scrollViewBounds.min.y > scrollContentBounds.min.y + itemSize + contentDownPadding) ||
            (direction == ScrollDirection.Horizontal && scrollViewBounds.max.x < scrollContentBounds.max.x - itemSize - contentRightPadding))
        {
            float size = 0f;
            float deltaSize = 0f;

            RemoveElementAtEnd(out size, currItemGroup);
            currItemGroup = ItemGroupList[lastItemGroupIdx > 0 ? lastItemGroupIdx - 1 : 0];
            deltaSize += size;

            while ((direction == ScrollDirection.Vertical   && scrollViewBounds.min.y > scrollContentBounds.min.y + itemSize + contentDownPadding + deltaSize) ||
                   (direction == ScrollDirection.Horizontal && scrollViewBounds.max.x < scrollContentBounds.max.x - itemSize - contentRightPadding - deltaSize) &&
                   size > 0)
            {
                bool removeSuccess = RemoveElementAtEnd(out size, currItemGroup);
                currItemGroup = ItemGroupList[lastItemGroupIdx > 0 ? lastItemGroupIdx - 1 : 0];
                deltaSize += size;

                if (!removeSuccess)
                    break;
            }
        }

        /* Case 4: the bottom of the first item is much higher than the top of the viewPort */
        /* Need to remove old items at the top of the scrollContent */
        currItemGroup = itemGroupList[firstItemGroupIdx < itemGroupCount ? firstItemGroupIdx : itemGroupCount - 1];
        if (currItemGroup.firstItemIdx != currItemGroup.nestedItemIdx ||
            (currItemGroup.firstItemIdx == currItemGroup.nestedItemIdx && currItemGroup.firstSubItemIdx + currItemGroup.nestedConstrainCount >= currItemGroup.subItemCount))            /* special case: the first item is a nested item and it only have the last row of subitem, we consider it as a non-nested item */
            itemSize = GetItemSize(currItemGroup.GetDisplayItemRect(0), true);
        else
            itemSize = GetSubItemSize(currItemGroup.GetSubItemRect(), currItemGroup.GetItemRect(currItemGroup.nestedItemIdx), true);

        if ((direction == ScrollDirection.Vertical   && scrollViewBounds.max.y < scrollContentBounds.max.y - itemSize - contentTopPadding) ||
            (direction == ScrollDirection.Horizontal && scrollViewBounds.min.x > scrollContentBounds.min.x + itemSize + contentLeftPadding))
        {
            float size = 0f;
            float deltaSize = 0f;

            RemoveElementAtStart(out size, currItemGroup);
            currItemGroup = itemGroupList[firstItemGroupIdx < itemGroupCount ? firstItemGroupIdx : itemGroupCount - 1];
            deltaSize += size;

            while ((direction == ScrollDirection.Vertical   && scrollViewBounds.max.y < scrollContentBounds.max.y - itemSize - contentTopPadding - deltaSize) ||
                   (direction == ScrollDirection.Horizontal && scrollViewBounds.min.x > scrollContentBounds.min.x + itemSize + contentLeftPadding + deltaSize) &&
                   size > 0)
            {
                bool removeSuccess = RemoveElementAtStart(out size, currItemGroup);
                currItemGroup = itemGroupList[firstItemGroupIdx < itemGroupCount ? firstItemGroupIdx : itemGroupCount - 1];
                deltaSize += size;

                if (!removeSuccess)
                    break;
            }
        }
    }

    #endregion


    #region bounds相关
    protected void UpdateBounds(bool updateItems = false)
    {
        /* Since viewPort UI is static, therefore its rectTransform coordinate can be directly reused */
        scrollViewBounds = new Bounds(scrollViewRect.rect.center, scrollViewRect.rect.size);
        CalculateContentBounds();

        /* Don't do this in Rebuild */
        if (Application.isPlaying && updateItems)
        {
            UpdateScrollItemGroups();
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(scrollContentRect);
            CalculateContentBounds();
        }

        /* Make sure scrollContent bounds are at least as large as view by adding padding if not. */
        /* One might think at first that if the scrollContent is smaller than the view, scrolling should be allowed. */
        /* However, that's not how scroll views normally work. */
        /* Scrolling is *only* possible when scrollContent is *larger* than view. */
        /* We use the pivot of the scrollContent rect to decide in which directions the scrollContent bounds should be expanded. */
        /* E.g. if pivot is at top, bounds are expanded downwards. */
        /* This also works nicely when ContentSizeFitter is used on the scrollContent. */
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

    protected void CalculateContentBounds()
    {
        if (ScrollContentRect == null)
            scrollContentBounds = new Bounds();

        /* scrollContent UI is dynamic, therefore needs to calculate from its world position */
        var vMin = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        var vMax = new Vector3(float.MinValue, float.MinValue, float.MinValue);
        var corners = new Vector3[4];
        var localMatrix = scrollViewRect.worldToLocalMatrix;
        ScrollContentRect.GetWorldCorners(corners);

        for (int j = 0; j < 4; j++)
        {
            Vector3 v = localMatrix.MultiplyPoint3x4(corners[j]);
            vMin = Vector3.Min(v, vMin);
            vMax = Vector3.Max(v, vMax);
        }
        scrollContentBounds = new Bounds(vMin, Vector3.zero);
        scrollContentBounds.Encapsulate(vMax);
    }

    #endregion

    #endregion


    #region 接口类 & 回调类函数

    #region UI交互接口

    public virtual void OnInitializePotentialDrag(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
            return;

        velocity = Vector2.zero;
    }

    public virtual void OnBeginDrag(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
            return;

        if (!IsActive())
            return;

        StopAllCoroutines();
        UpdateBounds(false);

        isDragging = true;
        cursorStartPos = Vector2.zero;
        contentStartPos = scrollContentRect.anchoredPosition;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(scrollViewRect, eventData.position, eventData.pressEventCamera, out cursorStartPos);
    }

    public virtual void OnDrag(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
            return;

        if (!IsActive())
            return;

        Vector2 cursorEndPos;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(scrollViewRect, eventData.position, eventData.pressEventCamera, out cursorEndPos))
            return;

        UpdateBounds(false);

        Vector2 pointerDelta = cursorEndPos - cursorStartPos;
        Vector2 position = contentStartPos + pointerDelta;

        /* Offset to get scrollContent into place in the view. */
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
        StopAllCoroutines();
        UpdateBounds(false);

        /* Down is positive for scroll events, while in UI system up is positive. */
        Vector2 delta = data.scrollDelta;
        delta.y *= -1;

        if (direction == ScrollDirection.Vertical)
        {
            if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
                delta.y = delta.x;
            delta.x = 0;
        }
        if (direction == ScrollDirection.Horizontal)
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


    #region Canvas绘制接口

    public virtual void LayoutComplete() { }

    public virtual void GraphicUpdateComplete() { }

    public virtual void Rebuild(CanvasUpdate executing)
    {
        if (executing == CanvasUpdate.Prelayout)
            UpdateCachedData();

        if (executing == CanvasUpdate.PostLayout)
        {
            UpdateBounds();
            UpdateScrollbars(Vector2.zero);
            UpdatePrevData();

            hasRebuiltLayout = true;
        }
    }

    #endregion


    #region LayoutGroup & LayoutElement 接口

    public virtual float minWidth { get { return -1; } }
    public virtual float minHeight { get { return -1; } }
    public virtual float preferredWidth { get { return -1; } }
    public virtual float preferredHeight { get { return -1; } }
    public virtual float flexibleWidth { get { return -1; } }
    public virtual float flexibleHeight { get { return -1; } }
    public virtual int layoutPriority { get { return -1; } }

    public virtual void CalculateLayoutInputHorizontal() { }

    public virtual void CalculateLayoutInputVertical() { }

    public virtual void SetLayoutHorizontal()
    {
        tracker.Clear();

        if (horizontalSliderExpand || verticalSliderExpand)
        {
            tracker.Add(this, scrollViewRect,
                DrivenTransformProperties.Anchors |
                DrivenTransformProperties.SizeDelta |
                DrivenTransformProperties.AnchoredPosition);

            /* Make view full size to see if content fits. */
            scrollViewRect.anchorMin = Vector2.zero;
            scrollViewRect.anchorMax = Vector2.one;
            scrollViewRect.sizeDelta = Vector2.zero;
            scrollViewRect.anchoredPosition = Vector2.zero;

            /* Recalculate content layout with this size to see if it fits when there are no scrollbars. */
            LayoutRebuilder.ForceRebuildLayoutImmediate(scrollContentRect);
            scrollViewBounds = new Bounds(scrollViewRect.rect.center, scrollViewRect.rect.size);
            CalculateContentBounds();
        }

        /* If it doesn't fit vertically, enable vertical scrollbar and shrink view horizontally to make room for it. */
        if (verticalSliderExpand && verticalScrollingNeeded)
        {
            scrollViewRect.sizeDelta = new Vector2(-(verticalSliderWidth + verticalScrollbarSpacing), scrollViewRect.sizeDelta.y);

            /* Recalculate content layout with this size to see if it fits vertically */
            /* when there is a vertical scrollbar (which may reflowed the content to make it taller). */
            LayoutRebuilder.ForceRebuildLayoutImmediate(scrollContentRect);
            scrollViewBounds = new Bounds(scrollViewRect.rect.center, scrollViewRect.rect.size);
            CalculateContentBounds();
        }

        /* If it doesn't fit horizontally, enable horizontal scrollbar and shrink view vertically to make room for it. */
        if (horizontalSliderExpand && horizontalScrollingNeeded)
        {
            scrollViewRect.sizeDelta = new Vector2(scrollViewRect.sizeDelta.x, -(horizontalSliderHeight + horizontalScrollbarSpacing));
            scrollViewBounds = new Bounds(scrollViewRect.rect.center, scrollViewRect.rect.size);
            CalculateContentBounds();
        }

        /* If the vertical slider didn't kick in the first time, and the horizontal one did, */
        /* we need to check again if the vertical slider now needs to kick in. */
        /* If it doesn't fit vertically, enable vertical scrollbar and shrink view horizontally to make room for it. */
        if (verticalSliderExpand && verticalScrollingNeeded && scrollViewRect.sizeDelta.x == 0 && scrollViewRect.sizeDelta.y < 0)
        {
            scrollViewRect.sizeDelta = new Vector2(-(verticalSliderWidth + verticalScrollbarSpacing), scrollViewRect.sizeDelta.y);
        }
    }

    public virtual void SetLayoutVertical()
    {
        UpdateScrollbarLayout();
        scrollViewBounds = new Bounds(ScrollViewRect.rect.center, ScrollViewRect.rect.size);
        CalculateContentBounds();
    }

    #endregion


    #region ItemGroup, Item, SubItem外部接口

    public delegate void OnSpawnItemAtStartDelegate(ItemGroupConfig itemGroup, GameObject item = null);
    public event OnSpawnItemAtStartDelegate OnSpawnItemAtStartEvent;
    public delegate void OnSpawnItemAtEndDelegate(ItemGroupConfig itemGroup, GameObject item = null);
    public event OnSpawnItemAtEndDelegate OnSpawnItemAtEndEvent;
    public delegate void OnSpawnSubItemAtStartDelegate(ItemGroupConfig itemGroup, GameObject subItem = null);
    public event OnSpawnSubItemAtStartDelegate OnSpawnSubItemAtStartEvent;
    public delegate void OnSpawnSubItemAtEndDelegate(ItemGroupConfig itemGroup, GameObject subItem = null);
    public event OnSpawnSubItemAtEndDelegate OnSpawnSubItemAtEndEvent;
    public delegate void OnDespawnItemAtStartDelegate(ItemGroupConfig itemGroup, GameObject item = null);
    public event OnDespawnItemAtStartDelegate OnDespawnItemAtStartEvent;
    public delegate void OnDespawnItemAtEndDelegate(ItemGroupConfig itemGroup, GameObject item = null);
    public event OnDespawnItemAtEndDelegate OnDespawnItemAtEndEvent;
    public delegate void OnDespawnSubItemAtStartDelegate(ItemGroupConfig itemGroup, GameObject subItem = null);
    public event OnDespawnSubItemAtStartDelegate OnDespawnSubItemAtStartEvent;
    public delegate void OnDespawnSubItemAtEndDelegate(ItemGroupConfig itemGroup, GameObject subItem = null);
    public event OnDespawnSubItemAtEndDelegate OnDespawnSubItemAtEndEvent;
    public delegate void OnAddItemDynamicDelegate(ItemGroupConfig itemGroup, GameObject item = null);
    public event OnAddItemDynamicDelegate OnAddItemDynamicEvent;
    public delegate void OnRemoveItemDynamicDelegate(ItemGroupConfig itemGroup, GameObject item = null);
    public event OnRemoveItemDynamicDelegate OnRemoveItemDynamicEvent;
    public delegate void OnAddSubItemDynamicDelegate(ItemGroupConfig itemGroup, GameObject subItem = null, GameObject oldSubItem = null);
    public event OnAddSubItemDynamicDelegate OnAddSubItemDynamicEvent;
    public delegate void OnRemoveSubItemDynamicDelegate(ItemGroupConfig itemGroup, GameObject subItem = null, GameObject newSubItem = null);
    public event OnRemoveSubItemDynamicDelegate OnRemoveSubItemDynamicEvent;

    public void OnSpawnItemAtStart(ItemGroupConfig itemGroup, GameObject item = null) { }
    public void OnSpawnItemAtEnd(ItemGroupConfig itemGroup, GameObject item = null) { }
    public void OnSpawnSubItemAtStart(ItemGroupConfig itemGroup, GameObject subItem = null) { }
    public void OnSpawnSubItemAtEnd(ItemGroupConfig itemGroup, GameObject subItem = null) { }
    public void OnDespawnItemAtStart(ItemGroupConfig itemGroup, GameObject item = null) { }
    public void OnDespawnItemAtEnd(ItemGroupConfig itemGroup, GameObject item = null) { }
    public void OnDespawnSubItemAtStart(ItemGroupConfig itemGroup, GameObject subItem = null) { }
    public void OnDespawnSubItemAtEnd(ItemGroupConfig itemGroup, GameObject subItem = null) { }
    public void OnAddItemDynamic(ItemGroupConfig itemGroup, GameObject item = null) { }
    public void OnRemoveItemDynamic(ItemGroupConfig itemGroup, GameObject item = null) { }
    public void OnAddSubItemDynamic(ItemGroupConfig itemGroup, GameObject subItem = null, GameObject oldSubItem = null) { }
    public void OnRemoveSubItemDynamic(ItemGroupConfig itemGroup, GameObject subItem = null, GameObject newSubItem = null) { }


    public void AddItemGroupStatic(int nestItemIdx, int subItemCount, List<GameObject> itemList, GameObject subItem)
    {
        ItemGroupConfig newItemGroup = new ItemGroupConfig(nestItemIdx, subItemCount, itemList, subItem);
        itemGroupList.Add(newItemGroup);
        newItemGroup.itemGroupIdx = itemGroupList.IndexOf(newItemGroup);
    }

    public void AlterItemGroupStatic(int itemGroupIdx, int? nestItemIdx, int? subItemCount, List<GameObject> itemList = null, GameObject subItem = null)
    {
        ItemGroupConfig itemGroup = itemGroupList[itemGroupIdx];
        if (nestItemIdx.HasValue)
            itemGroup.nestedItemIdx = nestItemIdx.Value;
        if (subItemCount.HasValue)
            itemGroup.subItemCount = subItemCount.Value;
        if (itemList != null)
            itemGroup.itemList = itemList;
        if (subItem != null)
            itemGroup.subItem = subItem;
    }

    public void RemoveItemGroupStatic(int itemGroupIdx)
    {
        itemGroupList.RemoveAt(itemGroupIdx);
    }

    /// <summary>
    /// Applicable for adding item when not in gameplay. Noted that adding nested item is not supported
    /// </summary>
    public void AddItemStatic(int itemGroupIdx, int itemIdx, GameObject itemPrefab)
    {
        ItemGroupConfig itemGroup = itemGroupList[itemGroupIdx];

        if (itemIdx <= itemGroup.nestedItemIdx)
        {
            itemGroup.itemList.Insert(itemIdx, itemPrefab);
            itemGroup.nestedItemIdx++;
        }
        else
            itemGroup.itemList.Insert(itemIdx, itemPrefab);
    }

    /// <summary>
    /// Applicable for adding item during gameplay. Noted that adding nested item is not supported
    /// </summary>
    public void AddItemDynamic(int itemGroupIdx, int itemIdx, GameObject itemPrefab)
    {
        ItemGroupConfig itemGroup = itemGroupList[itemGroupIdx];
        if (itemIdx < 0 || itemIdx > itemGroup.itemCount)
        {
            Debug.LogError("DynamicScrollRect: AddItemDynamic(): using invalid item index!");
            return;
        }

        /* Case 1: if the item we need to add is before the displaying area, update both head pointer and 
         *         tail pointer of displaying items */
        if (itemGroupIdx < firstItemGroupIdx ||
           (itemGroupIdx == firstItemGroupIdx && itemIdx < itemGroup.firstItemIdx))
        {
            itemGroup.firstItemIdx++;
            itemGroup.lastItemIdx++;
            OnAddItemDynamicEvent(itemGroup);
            AddItemStatic(itemGroupIdx, itemIdx, itemPrefab);
        }
        /* Case 2: if the item we need to add is after the displaying area */
        else if (itemGroupIdx >= lastItemGroupIdx ||
                (itemGroupIdx == lastItemGroupIdx - 1 && itemIdx >= itemGroup.lastItemIdx))
        {
            OnAddItemDynamicEvent(itemGroup);
            AddItemStatic(itemGroupIdx, itemIdx, itemPrefab);
        }
        /* Case 3: if the item we need to add is within the displaying area, spawn the item object and 
         *         update tail pointer of displaying items */
        else if ((itemGroupIdx == firstItemGroupIdx && itemIdx >= itemGroup.firstItemIdx) ||
                 (itemGroupIdx == lastItemGroupIdx - 1 && itemIdx < itemGroup.lastItemIdx) ||
                 (itemGroupIdx > firstItemGroupIdx && itemGroupIdx < lastItemGroupIdx - 1))
        {
            GameObject newItem = Instantiate(itemPrefab) as GameObject;
            newItem.transform.SetParent(scrollContentRect, false);
            newItem.transform.SetSiblingIndex(itemGroup.displayItemList[0].transform.GetSiblingIndex() + itemIdx - itemGroup.firstItemIdx);
            itemGroup.displayItemList.Insert(itemIdx - itemGroup.firstItemIdx, newItem);
            itemGroup.lastItemIdx++;

            OnAddItemDynamicEvent(itemGroup, newItem);
            AddItemStatic(itemGroupIdx, itemIdx, itemPrefab);

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(scrollContentRect);
            UpdateBounds(true);
        }
    }

    /// <summary>
    /// Applicable for removing item when not in gameplay
    /// </summary>
    public void RemoveItemStatic(int itemGroupIdx, int itemIdx)
    {
        ItemGroupConfig itemGroup = itemGroupList[itemGroupIdx];
        if (itemIdx < 0 || itemIdx >= itemGroup.itemCount)
            return;

        if (itemIdx < itemGroup.nestedItemIdx)
        {
            itemGroup.itemList.RemoveAt(itemIdx);
            itemGroup.nestedItemIdx--;
        }
        else if (itemIdx > itemGroup.nestedItemIdx)
            itemGroup.itemList.RemoveAt(itemIdx);
        else
            Debug.LogError("DynamicScrollRect: RemoveItemStatic(): Cannot directly remove a nested item!");
    }

    /// <summary>
    /// Applicable for removing item during gameplay
    /// </summary>
    public void RemoveItemDynamic(int itemGroupIdx, int itemIdx)
    {
        ItemGroupConfig itemGroup = itemGroupList[itemGroupIdx];
        if (itemIdx < 0 || itemIdx >= itemGroup.itemCount)
        {
            Debug.LogError("DynamicScrollRect: RemoveItemDynamic(): using invalid item index!"); 
            return;
        }
        if (itemIdx == itemGroup.nestedItemIdx)
        {
            Debug.LogError("DynamicScrollRect: RemoveItemDynamic(): Cannot directly remove a nested item!"); 
            return;
        }

        /* Case 1: if the item we need to remove is before the displaying area, update both head pointer and 
         *         tail pointer of displaying items */
        if (itemGroupIdx < firstItemGroupIdx ||
           (itemGroupIdx == firstItemGroupIdx && itemIdx < itemGroup.firstItemIdx))
        {
            itemGroup.firstItemIdx--;
            itemGroup.lastItemIdx--;
            OnRemoveItemDynamicEvent(itemGroup);
            RemoveItemStatic(itemGroupIdx, itemIdx);
        }
        /* Case 2: if the item we need to remove is after the displaying area */
        else if (itemGroupIdx >= lastItemGroupIdx ||
                (itemGroupIdx == lastItemGroupIdx - 1 && itemIdx >= itemGroup.lastItemIdx))
        {
            OnRemoveItemDynamicEvent(itemGroup);
            RemoveItemStatic(itemGroupIdx, itemIdx);
        }
        /* Case 3: if the item we need to remove is within the displaying area, despawn the item object and 
         *         update tail pointer of displaying items */
        else if ((itemGroupIdx == firstItemGroupIdx && itemIdx >= itemGroup.firstItemIdx) ||
                 (itemGroupIdx == lastItemGroupIdx - 1 && itemIdx < itemGroup.lastItemIdx) ||
                 (itemGroupIdx > firstItemGroupIdx && itemGroupIdx < lastItemGroupIdx - 1))
        {
            GameObject item = itemGroup.displayItemList[itemIdx - itemGroup.firstItemIdx];
            itemGroup.displayItemList.RemoveAt(itemIdx - itemGroup.firstItemIdx);
            itemGroup.lastItemIdx--;
            OnRemoveItemDynamicEvent(itemGroup, item);
            Despawn(item);
            RemoveItemStatic(itemGroupIdx, itemIdx);

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(scrollContentRect);
            UpdateBounds(true);
        }
    }

    /// <summary>
    /// Applicable for adding subItem when not in gameplay
    /// </summary>
    public void AddSubItemStatic(int itemGroupIdx)
    {
        ItemGroupConfig itemGroup = itemGroupList[itemGroupIdx];
        itemGroup.subItemCount++;
    }

    /// <summary>
    /// Applicable for adding subItem during gameplay
    /// </summary>
    public void AddSubItemDynamic(int itemGroupIdx, int subItemIdx)
    {
        ItemGroupConfig itemGroup = itemGroupList[itemGroupIdx];
        if (subItemIdx < 0 || subItemIdx > itemGroup.subItemCount)
        {
            Debug.LogError("DynamicScrollRect: AddSubItemDynamic(): using invalid subItem index!");
            return;
        }

        /* Case 1: if all subItems are not displaying and they are before the displaying area, update both
         *         head pointer and tail pointer of displaying subItems */
        if (itemGroup.firstSubItemIdx >= itemGroup.subItemCount)
        {
            itemGroup.firstSubItemIdx++;
            itemGroup.lastSubItemIdx++;
            OnAddSubItemDynamicEvent(itemGroup);
            AddSubItemStatic(itemGroupIdx);
        }
        /* Case 2: some subItems are displaying and the subItem we need to add is before the last
         *         displaying subItem (it might be displaying or not displaying), spawn the subItem object
         *         and update tail pointer if needed */
        else if (itemGroup.firstSubItemIdx < itemGroup.subItemCount &&
                 itemGroup.lastSubItemIdx > 0 &&
                 itemGroup.lastSubItemIdx > subItemIdx)
        {
            int index = subItemIdx - itemGroup.firstSubItemIdx > 0 ? subItemIdx - itemGroup.firstSubItemIdx : 0;
            GameObject newSubItem = Instantiate(itemGroup.subItem) as GameObject;
            GameObject parent = itemGroup.displayItemList[itemGroup.nestedItemIdx - itemGroup.firstItemIdx];
            newSubItem.transform.SetParent(parent.transform, false);
            newSubItem.transform.SetSiblingIndex(itemGroup.displaySubItemList[0].transform.GetSiblingIndex() + index);
            itemGroup.displaySubItemList.Insert(index, newSubItem);

            /* Case 2.1: if the last displaying subItem is the last subItem of all */
            if (itemGroup.lastSubItemIdx >= itemGroup.subItemCount)
            {
                OnAddSubItemDynamicEvent(itemGroup, newSubItem);
                itemGroup.lastSubItemIdx++;
            }
            /* Case 2.2: if there are subItems after the last displaying subItem (they are not displaying)  */
            else
            {
                GameObject oldSubItem = itemGroup.displaySubItemList[itemGroup.displaySubItemCount - 1];
                itemGroup.displaySubItemList.RemoveAt(itemGroup.displaySubItemCount - 1);
                OnAddSubItemDynamicEvent(itemGroup, newSubItem, oldSubItem);
                Despawn(oldSubItem);
            }

            AddSubItemStatic(itemGroupIdx);
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(scrollContentRect);
            UpdateBounds(true);
        }
        /* Case 3: some subItems are displaying and the subitem we need to add is after the last
         *         displaying subItem. Or all subItems are not displaying and they are after the 
         *         displaying area */
        else if (itemGroup.lastSubItemIdx <= subItemIdx)
        {
            OnAddSubItemDynamicEvent(itemGroup);
            AddSubItemStatic(itemGroupIdx);
        }
    }

    /// <summary>
    /// Applicable for removing subItem when not in gameplay
    /// </summary>
    public void RemoveSubItemStatic (int itemGroupIdx)
    {
        ItemGroupConfig itemGroup = itemGroupList[itemGroupIdx];
        if (itemGroup.subItemCount > 0)
            itemGroup.subItemCount--;
        else
            return;
    }

    /// <summary>
    /// Applicable for removing subItem during gameplay
    /// </summary>
    public void RemoveSubItemDynamic(int itemGroupIdx, int subItemIdx)
    {
        ItemGroupConfig itemGroup = itemGroupList[itemGroupIdx];
        if (subItemIdx < 0 || subItemIdx >= itemGroup.subItemCount)
        {
            Debug.LogError("DynamicScrollRect: RemoveSubItemDynamic(): using invalid subItem index!");
            return;
        }

        /* Case 1: if all subItems are not displaying and they are before the displaying area, update both
         *         head pointer and tail pointer of displaying subItems */
        if (itemGroup.firstSubItemIdx >= itemGroup.subItemCount)
        {
            itemGroup.firstSubItemIdx--;
            itemGroup.lastSubItemIdx--;
            OnRemoveSubItemDynamicEvent(itemGroup);
            RemoveSubItemStatic(itemGroupIdx);
        }
        /* Case 2: some subItems are displaying and the subItem we need to remove is before the last
         *         displaying subItem (it might be displaying or not displaying), despawn the subItem object
         *         and update tail pointer if needed */
        else if (itemGroup.firstSubItemIdx < itemGroup.subItemCount &&
                 itemGroup.lastSubItemIdx > 0 &&
                 itemGroup.lastSubItemIdx > subItemIdx)
        {
            int index = subItemIdx - itemGroup.firstSubItemIdx > 0 ? subItemIdx - itemGroup.firstSubItemIdx : 0;
            GameObject subItem = itemGroup.displaySubItemList[index];
            itemGroup.displaySubItemList.RemoveAt(index);

            /* Case 2.1: if the last displaying subItem is the last subItem of all */
            if (itemGroup.lastSubItemIdx >= itemGroup.subItemCount)
            {
                itemGroup.lastSubItemIdx--;
                OnRemoveSubItemDynamicEvent(itemGroup, subItem);
            }
            /* Case 2.2: if there are subItems after the last displaying subItem (they are not displaying)  */
            else
            {
                GameObject newSubItem = Spawn(itemGroup.subItem);
                GameObject parent = itemGroup.displayItemList[itemGroup.nestedItemIdx - itemGroup.firstItemIdx];
                newSubItem.transform.SetParent(parent.transform, false);
                newSubItem.transform.SetAsLastSibling();
                itemGroup.displaySubItemList.Add(newSubItem);
                OnRemoveSubItemDynamicEvent(itemGroup, subItem, newSubItem);
            }

            Despawn(subItem);
            RemoveSubItemStatic(itemGroupIdx);
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(scrollContentRect);
            UpdateBounds(true);
        }
        /* Case 3: some subItems are displaying and the subitem we need to remove is after the last
         *         displaying subItem. Or all subItems are not displaying and they are after the 
         *         displaying area */
        else if (itemGroup.lastSubItemIdx <= subItemIdx)
        {
            OnRemoveSubItemDynamicEvent(itemGroup);
            RemoveSubItemStatic(itemGroupIdx);
        }
    }

    #endregion

    #endregion





    #region 测试Log

    private void PrintAllIGInformation(string callName = "", ItemGroupConfig operatIG = null)
    {
        //if (itemGroupList[1].displayItemCount == 0 && (itemGroupList[1].firstItemIdx != 0 && itemGroupList[1].lastItemIdx != 0))
        //    return;

        print(" ===================== ");
        foreach (ItemGroupConfig itemGroup in itemGroupList)
        {
            int IGIdx;
            int operatIGIdx = -1;

            switch (itemGroup.nestedItemIdx)
            {
                case 3: IGIdx = 0; break;
                case 1: IGIdx = 1; break;
                case 0: IGIdx = 2; break;
                case 2: IGIdx = 3; break;
                default: IGIdx = 0; break;
            }

            if (operatIG != null && operatIG.nestedItemIdx == 3)
                operatIGIdx = 0;
            else if (operatIG != null && operatIG.nestedItemIdx == 1)
                operatIGIdx = 1;
            else if (operatIG != null && operatIG.nestedItemIdx == 0)
                operatIGIdx = 2;
            else if (operatIG != null && operatIG.nestedItemIdx == 2)
                operatIGIdx = 3;

            Debug.LogFormat("Item Group Index: {0}, Call By: {1}, Operating Item Group Index: {2}", IGIdx.ToString(), callName, operatIGIdx.ToString());
            Debug.LogFormat("Display Item Group Count: {0}, First Item Group Index: {1}, Last Item Group Index: {2}", displayItemGroupCount, firstItemGroupIdx, lastItemGroupIdx);
            Debug.LogFormat("Display Item Count: {0}, First Item Index: {1}, Last Item Index: {2}", itemGroup.displayItemCount, itemGroup.firstItemIdx, itemGroup.lastItemIdx);
            Debug.LogFormat("Display SubItem Count: {0}, First SubItem Index: {1}, Last SubItem Index: {2}", itemGroup.displaySubItemCount, itemGroup.firstSubItemIdx, itemGroup.lastSubItemIdx);
            print("\n");
        }
    }


    #endregion

}
