# Dynamic Scroll Rect

## Introduction

In many scenarios, there may exists a large numbers of elements in the `ScrollRect` (e.g. equipment inventory of a RPG game). Original `ScrollRect` implemented by Untiy builds all elements during gameplay, which costs a lot of time instantiating and drawing as well as memory for the elements that doesn't within the view port. 

@ivomarel's [InfinityScroll](https://github.com/ivomarel/InfinityScroll) and @qiankanglai's [LoopScrollRect](https://github.com/qiankanglai/LoopScrollRect) provide a solution by making the `ScrollRect` reusable. The basic idea of the solution is only build the elements that will be shown in the view port and dynamically add or remove elements during gameplay. In both *InfinityScroll* and *LoopScrollRect*, all elements are organized within the `LayoutGroup` that attached to the ScrollRect. 

In some cases however, elements might need to attach `LayoutGroup` as well. Take equipment inventory as an example, assume player has range weapon, melee weapon, and magic weapon. To put all weapons classified accroding to their type in the same scroll view, the best way is to put four elements into the `LayoutGroup` of ScrollRect, they will acts as the container of each type of weapons. Then put weapons into the `LayoutGroup` of their corresponding container elements. In this case, there are two layers of `LayoutGroup` structure.

**DynamicScrollRect** supports double layers of `LayoutGroup` structure with all the advantages of single layer `LayoutGroup` structure remains. If you need to construct a scroll view to display plenty of elements and need to classify these elements at the same time, this is absolutely what you need!


## Features

The original idea comes from @ivomarel's [InfinityScroll](https://github.com/ivomarel/InfinityScroll). After serveral refactorisations, I almost rewrite all the codes:

- Support double layers of `LayoutGroup` structure. 
- DynamicScrollRect manages elements in unit of `itemGroup`. User can flexablely arrange the layout of elements by configuring itemGroup.
- First layer only support `VerticalLayoutGroup` or `HorizontalLayoutGroup`, `GridLayoutGroup` is supported by second layer.
- Using object pool to improve performance when adding or removing elements.
- Support `ScrollBar`
- Support reloacation scroll view to specific itemGroup
- Reverse direction is not supported yet


## Example

Assume you are constructing a scroll view to display range weapon, melee weapon, magic weapon. For each weapon type, you want to have an element as a title to indicate the type of weapon for contents below and an element as a container to store single weapon elements.

In this case you will need three itemGroups, corresponding to three types of weapon. After that you need to provide two prefabs to the itemList of each itemGroup. One for the title element another for the containter element. Noted that the element container needs to attach `LayoutGroup` component. You also need to provide a prefab to the subItem of each itemGroup. It will act as the element that represent single weapon.

The arrangment of the layout of DynamicScrollRect is base on the order of itemGroups. You can alter the arrangement by change itemGroup order.

For more infromation and quick example implmentation, please refer to the DynamicScrolRect objects in the demo scene. 



## Instructions

Different from the Unity *ScrollView* in which the element layout is managed by *ScrollView* itself, *DynamicScrollRect* divides the whole content into groups and manages them by `ItemGroupConfig` classes. By specifying the layout and hierarchy of the elements in item groups, you can obtain your desired scroll view layout. Below are two ways to configure item groups. 

+ ### Control with inspector
  You can configure item groups ahead in the Unity inspector panel. After attaching the *DynamicScrollRect* as a component of your object, *"Item Group List"* can be found in the *DynamicScrollRect* configuration panel. Each element in the *"Item Group List"* is an item group, you can configure the parameters of the item group in its panel.

  - **Item Group Idx:** index of the item group in the item group list as well as the scroll view. This is an optional parameter, as the program will automatically set its value during initialization.
  - **Nested Item Idx:** index of the item that contains subItems in this item group.
  - **SubItem Count:** number of subItem in this item group. If you don't need nested hierarchy in this item group, please set its value smaller than 1.
  - **Item List:** list of item prefabs of this item group. The order in the list determines the order of items displayed in the scroll view.
  - **SubItem:** prefab of subItem.

  Noted that the order in *'Item Group List'* will also determine the order of item groups displayed in the scroll view.
  
  ![InspectorPanel](Images/InspectorPanel.png)

+ ### Control with scripts
  *DynamicScrollRect* provides several APIs by which you can manage the scroll view using scripts.
  To initialize or manage scroll view before it enters runtime, you can use:
  - **`AddItemGroupStatic()`**
  - **`AlterItemGroupStatic()`**
  - **`RemoveItemGroupStatic()`**
  - **`AddItemStatic()`**
  - **`RemoveItemStatic()`**
  - **`AddSubItemStatic()`**
  - **`RemoveSubItemStatic()`**
  
  To mange scroll view during runtime, you can use:
  - **`AddItemDynamic()`**
  - **`RemoveItemDynamic()`**
  - **`AddSubItemDynamic()`**
  - **`RemoveSubItemDynamic()`**
  
  For more infromation about these APIs, please refer to **APIs** section below.

:exclamation: **Important remark** :exclamation:
Noted that `GridLayoutGroup` is not allowed in the scroll content of scroll view, only `VerticalLayoutGroup` or `HorizontalLayoutGroup` is allowed. However, you can apply all these three types of layout group on the nested item. It is strongly recommended that don't add any child objects to a nested item as it might lead to incorrect display order right after you add subItems to the scroll view during gameplay.

There are two demo scenes in the Unity project for you to better understand how *DynamicScrollRect* works. 

## APIs

*DynamicScrollRect* provide several APIs and callback delegates for you to better manage the scroll view by script. They are listed as below: 

+ ### Inherit APIs
  *DynamicScrollRect* inherits several native Unity UGUI APIs including: `ICanvasElement`, `ILayoutElement`, `ILayoutGroup` and implements them within the code. You don't need to concern these APIs, but you need to be cautious before modifying these APIs as this might lead to unpredictable errors.

  ```csharp
  virtual void CalculateLayoutInputHorizontal()

  virtual void CalculateLayoutInputVertical()

  virtual void SetLayoutHorizontal()

  virtual void SetLayoutVertical()

  virtual void LayoutComplete()

  virtual void GraphicUpdateComplete()

  virtual void Rebuild(CanvasUpdate executing)
  ```

+ ### UI event APIs
  *DynamicScrollRect* also inherits native Unity event system APIs including: `IInitializePotentialDragHandler`, `IBeginDragHandler`, `IEndDragHandler`, `IDragHandler`, `IScrollHandler`. You can override these APIs according to your need.

  ```csharp
  virtual void OnInitializePotentialDrag(PointerEventData eventData)

  virtual void OnBeginDrag(PointerEventData eventData)

  virtual void OnDrag(PointerEventData eventData)

  virtual void OnEndDrag(PointerEventData eventData)

  virtual void OnScroll(PointerEventData data)
  ```

+ ### Scrollview alter APIs
  You can use these APIs to manage the scroll view before or during runtime. Noted that those APIs' names end with *"Static"* should be called when the scroll view is not in runtime. While those APIs' names end with *Dynamic* should be called when the scroll view is during runtime.

  ```csharp
  void AddItemGroupStatic(int nestItemIdx, int subItemCount, List<GameObject> itemList, GameObject subItem)
  /* Add an item group. You need to specify those basic parameters of the item group. */

  void AlterItemGroupStatic(int itemGroupIdx, int? nestItemIdx, int? subItemCount, List<GameObject> itemList = null, GameObject subItem = null)
  /* Alter the configuration of an existing item group. You need to specify the index of item group you want to alter. */

  void RemoveItemGroupStatic(int itemGroupIdx)
  /* Remove an existing item group. */

  void RefillScrollContent(int itemGroupBeginIdx = 0, float contentOffset = 0f)
  /* Refill the whole scroll view. You can specify the starting item group of the updated scroll view. */

  void AddItemStatic(int itemGroupIdx, int itemIdx, GameObject itemPrefab)
  /* Add an item to an existing item group. You need to specify the index of the new item and provide its prefab. */

  void AddItemDynamic(int itemGroupIdx, int itemIdx, GameObject itemPrefab)
  /* Add an item to an existing item group during gameplay. */

  void RemoveItemStatic(int itemGroupIdx, int itemIdx)
  /* Remove an item from an existing item group. */

  void RemoveItemDynamic(int itemGroupIdx, int itemIdx)
  /* Remove an item from an item group during gameplay. */

  void AddSubItemStatic(int itemGroupIdx)
  /* Add a subItem to the nested item of an existing item group. Since subItems are using the same prefab, you don't need to specify the index and prefab of the new subItem. */

  void AddSubItemDynamic(int itemGroupIdx, int subItemIdx)
  /* Add a subItem to an item group during gameplay. Since subItems are instanciated and set as the children of the nested item during gameplay, you need to specify the location of the new subItem when adding it. */

  void RemoveSubItemStatic(int itemGroupIdx)
  /* Remove a subItem from an existing item group. */

  void RemoveSubItemDynamic(int itemGroupIdx, int subItemIdx)
  /* Remove a subItem from an item group during gameplay. */
  ```

+ ### Scrollview alter callbacks
  You may need to initialize the item or subItem right after you add it to the scroll view, or you may need to execute some logic right after you remove an item or subItem. *DynamicScrollRect* provides several callback delegates to meet your demands.

  ```csharp
  delegate void OnSpawnItemAtStartDelegate(ItemGroupConfig itemGroup, GameObject item = null);
  event OnSpawnItemAtStartDelegate OnSpawnItemAtStartEvent;
  /* Callback when DynamicScrollRect add item at the start of scrollview. */
  
  delegate void OnSpawnItemAtEndDelegate(ItemGroupConfig itemGroup, GameObject item = null);
  event OnSpawnItemAtEndDelegate OnSpawnItemAtEndEvent;
  /* Callback when DynamicScrollRect add item at the end of scrollview. */
  
  delegate void OnSpawnSubItemAtStartDelegate(ItemGroupConfig itemGroup, GameObject subItem = null);
  event OnSpawnSubItemAtStartDelegate OnSpawnSubItemAtStartEvent;
  /* Callback when DynamicScrollRect add subItem at the start of scrollview. */
  
  delegate void OnSpawnSubItemAtEndDelegate(ItemGroupConfig itemGroup, GameObject subItem = null);
  event OnSpawnSubItemAtEndDelegate OnSpawnSubItemAtEndEvent;
  /* Callback when DynamicScrollRect add subItem at the end of scrollview. */
  
  delegate void OnDespawnItemAtStartDelegate(ItemGroupConfig itemGroup, GameObject item = null);
  event OnDespawnItemAtStartDelegate OnDespawnItemAtStartEvent;
  /* Callback when DynamicScrollRect remove item at the start of scrollview. */
  
  delegate void OnDespawnItemAtEndDelegate(ItemGroupConfig itemGroup, GameObject item = null);
  event OnDespawnItemAtEndDelegate OnDespawnItemAtEndEvent;
  /* Callback when DynamicScrollRect remove item at the end of scrollview. */
  
  delegate void OnDespawnSubItemAtStartDelegate(ItemGroupConfig itemGroup, GameObject subItem = null);
  event OnDespawnSubItemAtStartDelegate OnDespawnSubItemAtStartEvent;
  /* Callback when DynamicScrollRect remove subItem at the start of scrollview. */    
  
  delegate void OnDespawnSubItemAtEndDelegate(ItemGroupConfig itemGroup, GameObject subItem = null);
  event OnDespawnSubItemAtEndDelegate OnDespawnSubItemAtEndEvent;
  /* Callback when DynamicScrollRect remove subItem at the end of scrollview. */    
  
  delegate void OnAddItemDynamicDelegate(ItemGroupConfig itemGroup, GameObject item = null);
  event OnAddItemDynamicDelegate OnAddItemDynamicEvent;
  /* Callback when you add item to the scroll view during gameplay */     
  
  delegate void OnRemoveItemDynamicDelegate(ItemGroupConfig itemGroup, GameObject item = null);
  event OnRemoveItemDynamicDelegate OnRemoveItemDynamicEvent;
  /* Callback when you remove item from the scroll view during gameplay */      
  
  delegate void OnAddSubItemDynamicDelegate(ItemGroupConfig itemGroup, GameObject subItem = null, GameObject oldSubItem = null);
  event OnAddSubItemDynamicDelegate OnAddSubItemDynamicEvent;
  /* Callback when you add subItem to the scroll view during gameplay */    
  
  delegate void OnRemoveSubItemDynamicDelegate(ItemGroupConfig itemGroup, GameObject subItem = null, GameObject newSubItem = null);
  event OnRemoveSubItemDynamicDelegate OnRemoveSubItemDynamicEvent;
  /* Callback when you remove subItem from the scroll view during gameplay */    
  ``` 

+ ### Scrollview relocation APIs
  You can use these APIs to conveniently jump to specific item group, item and subItem. *DynamicScrollRect* uses a coroutine to relocate the scroll view to the destinated location, you can specify the time that relocation takes.

  ```csharp
  void ScrollToItemGroup(int itemGroupIdx, float time)
  /* Jump to specific item group. */

  void ScrollToItem(int itemGroupIdx, int itemIdx, float time)
  /* Jump to specific item. */

  void ScrollToSubItem(int itemGroupIdx, int subItemIdx, float time)
  /* Jump to specific subItem. */

  IEnumerator ScrollTo(float offsetSize, float time, bool upward)
  /* Relocate for 'offsetSize' amount of distance */
  ```


## Future Plans

Some functions are still under development, for example:
- Add support to reverse scroll direction.
- Add support to add and remove item groups during gameplay.
- Improve performance

If you have any new functions that want to add to *DynamicScrollRect* or discover any bugs, please feel free to write them down in [Issues](https://github.com/WaterFriend/DynamicScrollRect/issues) page of this repository.

