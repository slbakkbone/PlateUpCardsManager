using Controllers;
using HarmonyLib;
using Kitchen;
using Kitchen.Modules;
using KitchenCardsManager.Helpers;
using KitchenData;
using KitchenLib.Utils;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace KitchenCardsManager.Patches
{
    [HarmonyPatch]
    internal static class CardScrollerElement_Patch
    {
        private class CardScrollerPage
        {
            private class RowToggler
            {
                private int StartIndex;
                private int MinIndex;
                private int MaxIndex;

                private int CompletedDistance = 0;
                private readonly bool CardEnabledState;

                private List<Unlock> Cards;
                private List<UnlockCardElement> CardBank;

                public bool IsCompleted
                {
                    get
                    {
                        return (StartIndex - CompletedDistance < MinIndex) && (StartIndex + CompletedDistance > MaxIndex);
                    }
                }

                public RowToggler(int startIndex, int minIndex, int maxIndex, bool setEnabledState, List<Unlock> cards, List<UnlockCardElement> cardBank)
                {
                    StartIndex = startIndex;
                    MinIndex = minIndex;
                    MaxIndex = maxIndex;
                    CardEnabledState = setEnabledState;
                    Cards = cards;
                    CardBank = cardBank;
                }

                public bool PerformNext()
                {
                    if (!IsCompleted)
                    {
                        int[] indexToSet = new int[] { StartIndex - CompletedDistance, StartIndex + CompletedDistance };

                        for (int i = 0; i < 2; i++)
                        {
                            if (i == 0 && (indexToSet[0] == indexToSet[1]))
                            {
                                continue;
                            }
                            if (indexToSet[i] >= MinIndex && indexToSet[i] <= MaxIndex)
                            {
                                Unlock unlock = Cards[indexToSet[i]];
                                SetPreference(unlock.ID.ToString(), CardEnabledState);
                                Color newColor = GetColorByEnabledState(unlock);
                                UnlockCardElement card = CardBank[indexToSet[i]];
                                UnlockHelpers.SetColor(card, newColor);
                            }
                        }
                        CompletedDistance += 1;
                    }
                    return IsCompleted;
                }
            }

            private struct UnlockCardDisplay
            {
                public UnlockCardElement Card;
                public UnlockCardState CardState;
            }

            private readonly string Name;
            private readonly GameObject CardsManagerContainer;
            private readonly UnlockCardElement MainCard;

            private Vector3 ScrollerContainerLocalPosition;
            private readonly Vector3 SCROLLER_CONTAINER_LOCAL_POSITION_DEFAULT = new Vector3(4.54f, 3f, 10f);

            private Vector3 ScrollerContainerRotation;
            private readonly Vector3 SCROLLER_CONTAINER_ROTATION_DEFAULT = new Vector3(0f, 0f, 0f);

            private Vector3 CentreCardRestingLocalPosition;
            private readonly Vector3 CENTRE_CARD_RESTING_LOCAL_POSITION_DEFAULT = new Vector3(-0.25f, 0.6f, -0.9f);

            private Vector3 RowOffsetVector;
            private readonly Vector3 ROW_OFFSET_VECTOR_DEFAULT = new Vector3(0f, 1f, 0.68f);

            private Vector3 SelectedCardPopOffsetVector; // Vector applied to selected card in scroller to make it pop
            private readonly Vector3 SELECTED_CARD_POP_OFFSET_VECTOR_DEFAULT = new Vector3(0.0f, 0.6f, 0.0f);

            private Vector3 KeyHeldPopOffsetVector;
            private readonly Vector3 KEY_HELD_POP_OFFSET_VECTOR_DEFAULT = new Vector3(0.0f, 0.4f, 0.0f);

            private Vector3 SpecialActionAnimationEndVector;
            private readonly Vector3 SPECIAL_ACTION_ANIMATION_END_VECTOR_DEFAULT = new Vector3(0.0f, 15f, 0.0f);

            private Vector3 RadiusVector;

            private Vector3 StackingZOffset;

            private float AngularPitch;

            private float RowAngularPitchOffset;

            private List<Unlock> Cards;
            private List<UnlockCardDisplay> CardBank = new List<UnlockCardDisplay>();
            private List<int> RowsCount = new List<int>();
            private List<int> RowsStartIndex = new List<int>();
            private List<int> RowsSelectedIndex = new List<int>();

            private List<int> CustomRowKeys;

            private int VisibleRowsCount;

            private int SelectedRowIndex;

            private GameObject ParentContainer;

            private bool IsInit = false;
            internal bool Enabled
            {
                get { return ParentContainer.activeSelf; }
                set
                {
                    Init();
                    ParentContainer.SetActive(value);
                }
            }

            internal int SelectedIndex
            {
                get
                {
                    Init();
                    return RowsSelectedIndex[SelectedRowIndex];
                }
            }

            internal Unlock SelectedUnlock
            {
                get
                {
                    Init();
                    return Cards[SelectedIndex];
                }
            }

            internal Color SelectedUnlockColorByState
            {
                get
                {
                    Init();
                    return IsSelectedCardEnabled ? GetDefaultColor(SelectedUnlock.ID) : DisabledColor;
                }
            }

            internal bool IsSelectedCardEnabled
            {
                get
                {
                    return Main.PrefManager.Get<bool>(SelectedUnlock.ID.ToString());
                }
            }

            private static Dictionary<int, Color> DefaultColorDict = new Dictionary<int, Color>();
            private static readonly Color DisabledColor = new Color(0.4f, 0.4f, 0.4f);
            private static readonly Color UnknownDefaultColor = new Color(0.8f, 0f, 0f);

            private static List<RowToggler> RowTogglers = new List<RowToggler>();

            internal CardScrollerPage(
                GameObject cardsManagerContainer,
                UnlockCardElement mainCard,
                List<Unlock> cards,
                List<int> customRowKeys = null,
                int visibleRowsCount = 7,
                float curveRadius = 30f,
                float angularPitch = 1f,
                float rowAngularPitchOffset = -0.33f,
                float stackingZOffset = -0.12f,
                string name = null,
                Vector3? containerLocalPosition = null,
                Vector3? containerRotation = null,
                Vector3? centreCardRestingLocalPosition = null,
                Vector3? rowOffsetVector = null,
                Vector3? selectedCardPopOffsetVector = null,
                Vector3? keyHeldPopOffsetVector = null,
                Vector3? specialActionAnimationEndVector = null)
            {
                CardsManagerContainer = cardsManagerContainer;
                MainCard = mainCard;
                Cards = cards;
                CustomRowKeys = customRowKeys;
                VisibleRowsCount = visibleRowsCount;
                Name = name;
                RadiusVector = new Vector3(0f, curveRadius, 0f);
                StackingZOffset = new Vector3(0f, 0f, stackingZOffset);
                AngularPitch = angularPitch;
                RowAngularPitchOffset = rowAngularPitchOffset;
                ScrollerContainerLocalPosition = containerLocalPosition.HasValue ? containerLocalPosition.Value : SCROLLER_CONTAINER_LOCAL_POSITION_DEFAULT;
                ScrollerContainerRotation = containerRotation.HasValue ? containerRotation.Value : SCROLLER_CONTAINER_ROTATION_DEFAULT;
                ScrollerContainerRotation = containerRotation.HasValue ? containerRotation.Value : SCROLLER_CONTAINER_ROTATION_DEFAULT;
                CentreCardRestingLocalPosition = centreCardRestingLocalPosition.HasValue ? centreCardRestingLocalPosition.Value : CENTRE_CARD_RESTING_LOCAL_POSITION_DEFAULT;
                RowOffsetVector = rowOffsetVector.HasValue ? rowOffsetVector.Value : ROW_OFFSET_VECTOR_DEFAULT;
                SelectedCardPopOffsetVector = selectedCardPopOffsetVector.HasValue ? selectedCardPopOffsetVector.Value : SELECTED_CARD_POP_OFFSET_VECTOR_DEFAULT;
                KeyHeldPopOffsetVector = keyHeldPopOffsetVector.HasValue ? keyHeldPopOffsetVector.Value : KEY_HELD_POP_OFFSET_VECTOR_DEFAULT;
                SpecialActionAnimationEndVector = specialActionAnimationEndVector.HasValue ? specialActionAnimationEndVector.Value : SPECIAL_ACTION_ANIMATION_END_VECTOR_DEFAULT;

                Init();
            }

            private string GetRowGroup(int index)
            {
                return CustomRowKeys == null ? UnlockHelpers.GetCardGroup(Cards[index]) : CustomRowKeys[index].ToString();
            }

            private void Init()
            {
                if (!IsInit)
                {
                    if (ParentContainer != null)
                    {
                        Object.Destroy(ParentContainer);
                    }
                    ParentContainer = new GameObject(Name == null ? "Card Scroller Page (Clone)" : Name);
                    ParentContainer.transform.parent = CardsManagerContainer.transform;
                    ParentContainer.transform.localScale = Vector3.one;
                    ParentContainer.transform.localPosition = Vector3.zero;
                    ParentContainer.transform.localEulerAngles = Vector3.zero;
                    ParentContainer.SetActive(false);
                    GameObject scrollerContainer = new GameObject();
                    scrollerContainer.transform.parent = ParentContainer.transform;
                    scrollerContainer.transform.localScale = Vector3.one;
                    scrollerContainer.transform.localPosition = ScrollerContainerLocalPosition;
                    scrollerContainer.transform.localEulerAngles = ScrollerContainerRotation;

                    CardBank.Clear();
                    RowsCount.Clear();
                    RowsStartIndex.Clear();
                    RowsSelectedIndex.Clear();

                    SelectedRowIndex = 0;

                    string prevGroup = null;
                    int rowStartCardIndex = 0;
                    for (int i = 0; i < Cards.Count; i++)
                    {
                        Unlock card = Cards[i];
                        string group = GetRowGroup(i);
                        if (prevGroup == null || prevGroup != group)
                        {
                            if (RowsCount.Count > 0)
                            {
                                rowStartCardIndex += RowsCount.Last();
                            }
                            RowsStartIndex.Add(rowStartCardIndex);
                            RowsSelectedIndex.Add(rowStartCardIndex);
                            RowsCount.Add(0);
                        }
                        prevGroup = group;
                        RowsCount[RowsCount.Count - 1]++;
                        UnlockCardElement unlockCardElement = Object.Instantiate(MainCard, scrollerContainer.transform, worldPositionStays: true);
                        unlockCardElement.transform.localScale = Vector3.one;
                        unlockCardElement.transform.localRotation = Quaternion.identity;
                        unlockCardElement.SetUnlock(card);
                        if (!DefaultColorDict.ContainsKey(card.ID))
                        {
                            DefaultColorDict.Add(card.ID, UnlockHelpers.GetColor(unlockCardElement));
                        }

                        UnlockCardState unlockCardState = new UnlockCardState(unlockCardElement);

                        CardBank.Add(new UnlockCardDisplay {
                            Card = unlockCardElement,
                            CardState = unlockCardState
                        });
                    }
                    IsInit = true;
                }
            }

            internal int SelectNextCard()
            {
                RowsSelectedIndex[SelectedRowIndex]++;
                ClampSelectedIndex();
                return SelectedIndex;
            }

            internal int SelectPreviousCard()
            {
                RowsSelectedIndex[SelectedRowIndex]--;
                ClampSelectedIndex();
                return SelectedIndex;
            }

            internal void ClampSelectedIndex()
            {
                int rowMin = 0;
                int rowMax = 0;
                for (int i = 0; i < RowsCount.Count; i++)
                {
                    if (i > 0)
                    {
                        rowMin += RowsCount[i - 1];
                    }
                    rowMax = rowMin + RowsCount[i] - 1;
                    if (i >= SelectedRowIndex)
                    {
                        break;
                    }
                }
                RowsSelectedIndex[SelectedRowIndex] = Mathf.Clamp(SelectedIndex, rowMin, rowMax);
            }

            internal int SelectNextRow()
            {
                SelectedRowIndex++;
                ClampSelectedRowIndex();
                return SelectedIndex;
            }

            internal int SelectPreviousRow()
            {
                SelectedRowIndex--;
                ClampSelectedRowIndex();
                return SelectedIndex;
            }

            private void ClampSelectedRowIndex()
            {
                SelectedRowIndex = Mathf.Clamp(SelectedRowIndex, 0, RowsCount.Count - 1);
            }

            internal void Redraw(bool forceInit = false)
            {
                if (!IsInit || forceInit)
                {
                    Init();
                    IsInit = true;
                }

                int row = 0;
                string prevGroup = null;
                for (int i = 0; i < CardBank.Count; i++)
                {
                    UnlockCardElement unlockCardElement = CardBank[i].Card;
                    UnlockCardState unlockCardState = CardBank[i].CardState;
                    string group = GetRowGroup(i);
                    if (prevGroup != null && prevGroup != group)
                    {
                        row++;
                    }
                    prevGroup = group;
                    int rowAtBase = (RowsCount.Count - VisibleRowsCount - 1) < 0 ? 0 : ((SelectedRowIndex > (RowsCount.Count - VisibleRowsCount)) ? (RowsCount.Count - VisibleRowsCount) : SelectedRowIndex);
                    int rowOffset = row - rowAtBase;
                    unlockCardElement.transform.localPosition = (CentreCardRestingLocalPosition - RadiusVector) +
                        (Quaternion.AngleAxis((i - RowsSelectedIndex[row]) * AngularPitch + (rowOffset * RowAngularPitchOffset), Vector3.back) * (RadiusVector + (rowOffset * RowOffsetVector))) +
                        (i - RowsSelectedIndex[row]) * StackingZOffset;

                    // Pop up selected card pop further if grab held
                    if (i == SelectedIndex)
                    {
                        unlockCardElement.transform.localPosition += SelectedCardPopOffsetVector;
                        if (IsPerformingAnimation)
                        {
                            unlockCardElement.transform.localPosition += KeyHeldPopOffsetVector + (SpecialActionAnimationEndVector * AnimationProgress);
                        }
                        else if (IsGrabDown)
                        {
                            unlockCardElement.transform.localPosition -= SelectedCardPopOffsetVector * GrabHeldTime / GrabHeldThreshold;
                        }
                        else if (IsReadyDown)
                        {
                            unlockCardElement.transform.localPosition += KeyHeldPopOffsetVector * ReadyHeldTime / ReadyHeldThreshold;
                        }
                    }

                    unlockCardElement.transform.localEulerAngles = new Vector3(0.0f, 0.0f, (RowsSelectedIndex[row] - i) * AngularPitch - rowOffset * RowAngularPitchOffset);

                    UnlockHelpers.SetColor(unlockCardElement, GetColorByEnabledState(Cards[i]));
                    UnlockHelpers.SetAutoAddIndicator(unlockCardState, Cards[i]);
                }
            }

            internal void AddRowToggler()
            {
                RowTogglers.Add(new RowToggler(SelectedIndex, RowsStartIndex[SelectedRowIndex], RowsStartIndex[SelectedRowIndex] + RowsCount[SelectedRowIndex] - 1, IsSelectedCardEnabled, Cards, CardBank.Select(x => x.Card).ToList()));
            }

            internal bool RowToggleStep()
            {
                bool performed = false;
                for (int i = RowTogglers.Count - 1; i > -1; i--)
                {
                    if (RowTogglers[i].PerformNext())
                    {
                        RowTogglers.RemoveAt(i);
                    }
                    performed = true;
                }
                return performed;
            }

            internal Color ToggleEnabledSelectedCard()
            {
                ToggleEnabledPreference(SelectedUnlock);
                Color newColor = SelectedUnlockColorByState;
                UnlockHelpers.SetColor(CardBank[SelectedIndex].Card, newColor);
                return newColor;
            }

            internal bool ToggleAutoAddSelectedCard()
            {
                return ToggleAutoAddPreference(SelectedUnlock);
            }

            internal bool AddSelectedCardToRun(out string statusMessage)
            {
                return CardsManagerController.AddProgressionUnlock(SelectedUnlock.ID, out statusMessage);
            }

            private static bool SetPreference(string preferenceID, bool value)
            {
                Main.PrefManager.Set<bool>(preferenceID, value);
                return value;
            }

            private static bool ToggleEnabledPreference(Unlock unlock)
            {
                string enabledPreferenceID = unlock.ID.ToString();
                bool newValue = !Main.PrefManager.Get<bool>(enabledPreferenceID);
                return SetPreference(enabledPreferenceID, newValue);
            }

            private static bool ToggleAutoAddPreference(Unlock unlock)
            {
                string enabledPreferenceID = unlock.ID.ToString() + "_AutoAdd";
                bool newValue = !Main.PrefManager.Get<bool>(enabledPreferenceID);
                return SetPreference(enabledPreferenceID, newValue);
            }

            private static Color GetColorByEnabledState(Unlock unlock)
            {
                return UnlockHelpers.GetEnabledState(unlock) ? GetDefaultColor(unlock.ID) : DisabledColor;
            }

            private static Color GetDefaultColor(int unlockID)
            {
                if (DefaultColorDict.ContainsKey(unlockID))
                {
                    return DefaultColorDict[unlockID];
                }
                else
                {
                    DefaultColorDict.Add(unlockID, UnknownDefaultColor);
                    return UnknownDefaultColor;
                }
            }
        }

        struct ActiveCardsCache
        {
            public int HashCode => GetHashCode();

            List<ICard> _cards = new List<ICard>();
            public List<ICard> Cards
            {
                get
                {
                    return _cards;
                }
                set
                {
                    List<ICard> newCards = new List<ICard>();
                    for (int i = 0; i < value.Count; i++)
                    {
                        newCards.Add(value[i]);
                    }
                    _cards = newCards;
                }
            }

            public ActiveCardsCache()
            {
                Cards = new List<ICard>();
            }

            public ActiveCardsCache(List<ICard> cards)
            {
                Cards = cards;
            }

            public bool IsChanged(List<ICard> check)
            {
                return IsChanged(new ActiveCardsCache(check));
            }

            public bool IsChanged(ActiveCardsCache check)
            {
                return HashCode != check.HashCode;
            }

            public override int GetHashCode()
            {
                int hash = 17;

                foreach (ICard card in _cards)
                {
                    hash = hash * 31 + card.GetHashCode();
                }

                return hash;
            }
        }

        internal static bool MenuOpenedFromModPreferences = false;

        private static List<CardScrollerPage> _pages = new List<CardScrollerPage>();
        private static int _selectedPageIndex = 0;

        private static GameObject ScrollersContainer;
        private static bool IsCardManagerMode = false;

        private static bool _init = false;
        private static Vector3 _parentOriginalLocalPosition;

        private static readonly Vector3 _viewNewLocalPosition = new Vector3(-0.22f, -2.3f, -4f);
        private static readonly Vector3 _scrollersContainerRotation = new Vector3(0.0f, 0.0f, 0.0f);
        private static readonly float _scrollersContainerZ = -3.5f;

        private static readonly MethodInfo mSetIndex = ReflectionUtils.GetMethod<CardScrollerElement>("SetIndex", BindingFlags.Instance | BindingFlags.NonPublic);

        private static float GrabHeldTime = 0f;
        private static bool PerformedGrab = false;
        private static float GrabHeldThreshold => Main.PrefManager?.Get<float>(Main.CARDS_MANAGER_ENABLE_DISABLE_HOLD_DURATION) ?? 1.5f;
        internal static bool IsGrabDown { get => GrabHeldTime > 0f; }

        private static float ReadyHeldTime = 0f;
        private static bool PerformedReady = false;
        private static float ReadyHeldThreshold => Main.PrefManager?.Get<float>(Main.CARDS_MANAGER_ADD_REMOVE_HOLD_DURATION) ?? 3f;
        internal static bool IsReadyDown { get => ReadyHeldTime > 0f; }

        private static float StopMovingHeldTime = 0f;
        private static bool PerformedStopMoving = false;
        private static float StopMovingHeldThreshold => Main.PrefManager?.Get<float>(Main.CARDS_MANAGER_AUTO_ADD_HOLD_DURATION) ?? 1.5f;
        internal static bool IsStopMovingDown { get => StopMovingHeldTime > 0f; }

        private static bool HandleInteractionRequireRedraw = false;

        internal static float AnimationProgress { get => AnimationCurrentTime / AnimationTotalTime; }
        private static float AnimationTotalTime = 0.5f;
        private static float AnimationCurrentTime = 0f;
        internal static bool IsPerformingAnimation { get; private set; }
        internal static bool IsAnimationDone { get => AnimationProgress > 1f; }

        private static bool IsScrolling = false;
        private static float scrollHeldTime = 0f;
        private const float scrollContinuousLowerThreshold = 0.5f;
        private const float scrollSpeedMax = 5f;
        private static float scrollTickProgress = 0f;
        private const float minScrollDelay = 0.05f;

        static ActiveCardsCache Cache = new ActiveCardsCache();

        // Game v1.4.4+ changed the pause menu's native "Active Cards" entry point to open
        // Kitchen.CardListMenu instead of a CardScrollerElement-hosting menu, so none of this
        // file's patches fired there anymore. CardListMenu's own rendering (via
        // Kitchen.Modules.DeckViewerElement) shows cards in a grid with no enable/disable/remove
        // support and no clear "selected card" indicator we can hook cleanly - so instead of
        // grafting our logic onto it, we skip CardListMenu's native Setup entirely and reuse
        // the exact same working mechanism as the "Edit Cards" Preferences menu
        // (Main.CardsManagerScrollerMenu): spawn a real CardScrollerElement into this screen's
        // own Container/ModuleList. Leaving MenuOpenedFromModPreferences/IsCardManagerMode
        // false keeps SetIndex_Postfix on the vanilla "active cards" branch
        // (ActiveCards_DisplayTweak), which already supports hold-to-remove via
        // HandleReadyButtonHold and has a real selected-card indicator.
        [HarmonyPatch(typeof(CardListMenu), "Setup")]
        [HarmonyPrefix]
        private static bool CardListMenu_Setup_Prefix(int player_id, Transform ___Container, ModuleList ___ModuleList)
        {
            MenuOpenedFromModPreferences = false;
            IsCardManagerMode = false;

            CardScrollerElement cardScrollerElement = ModuleDirectory.Add<CardScrollerElement>(___Container, new Vector2(0f, 0f));
            cardScrollerElement.SetCardList(GameInfo.AllCurrentCards);
            ___ModuleList.AddModule(cardScrollerElement, cardScrollerElement.transform.localPosition.ToFlat());

            return false;
        }

        [HarmonyPatch(typeof(CardScrollerElement), "SetIndex")]
        [HarmonyPrefix]
        private static bool SetIndex_Prefix()
        {
            if (ScrollersContainer == null || ScrollersContainer.transform.parent == null)
            {
                _init = false;
                IsCardManagerMode = false;
            }

            if (IsCardManagerMode)
            {
                return false;
            }
            else
            {
                return true;
            }
        }

        [HarmonyPatch(typeof(CardScrollerElement), "SetIndex")]
        [HarmonyPostfix]
        private static void SetIndex_Postfix(CardScrollerElement __instance, ref int ___Index, Vector3 ___CardViewOffsetChange)
        {
            if (!IsCardManagerMode)
            {
                InitPages(__instance);
                if (GameInfo.AllCurrentCards.Count == 0 || MenuOpenedFromModPreferences)
                {
                    IsCardManagerMode = true;
                }
                else
                {
                    ScrollersContainer.SetActive(false);
                    __instance.Container.SetActive(true);
                    ActiveCards_DisplayTweak(__instance, ___Index, ___CardViewOffsetChange);
                }
            }
            MenuOpenedFromModPreferences = false;

            if (IsCardManagerMode)
            {
                for (int i = 0; i < _pages.Count; i++)
                {
                    bool enabled = false;
                    if (i == _selectedPageIndex)
                        enabled = true;
                    _pages[i].Enabled = enabled;
                }

                ScrollersContainer.SetActive(true);
                __instance.Container.SetActive(false);
                CardManagerSelectorView(__instance);
            }
        }


        private static void ActiveCards_DisplayTweak(CardScrollerElement instance, int selectedIndex, Vector3 cardViewOffsetChange)
        {
            instance.Card.transform.parent.localPosition = _parentOriginalLocalPosition;
            List<UnlockCardElement> CardBank = instance.CardBank;
            for (int i = 0; i < CardBank.Count; i++)
            {
                UnlockCardElement unlockCardElement = CardBank[i];
                unlockCardElement.transform.localPosition = ((i == selectedIndex) ? new Vector3(0.5f, 0f, 0f) : Vector3.zero) * (PerformedReady ? 1f : 1f - ReadyHeldTime / ReadyHeldThreshold) + (i - selectedIndex + 5) * (cardViewOffsetChange);
            }
        }

        private static void CardManagerSelectorView(CardScrollerElement instance)
        {
            instance.Card.SetUnlock(_pages[_selectedPageIndex].SelectedUnlock);
            UnlockHelpers.SetColor(instance.Card, _pages[_selectedPageIndex].SelectedUnlockColorByState);
            instance.Card.transform.parent.localPosition = _viewNewLocalPosition;
            _pages[_selectedPageIndex].Redraw();
        }

        private static void InitPages(CardScrollerElement instance)
        {
            if (!_init)
            {
                if (ScrollersContainer != null)
                {
                    Object.Destroy(ScrollersContainer);
                }
                ScrollersContainer = new GameObject("Card Manager Scrollers");
                ScrollersContainer.transform.parent = instance.Card.transform.parent;
                ScrollersContainer.transform.localScale = instance.Container.transform.localScale;
                Vector3 containerPosition = instance.Container.transform.localPosition;
                containerPosition.z = _scrollersContainerZ;
                ScrollersContainer.transform.localPosition = containerPosition;
                ScrollersContainer.transform.localEulerAngles = _scrollersContainerRotation;
                _pages.Clear();

                // Non-modded unlocks page
                Main.LogInfo($"Adding unmodded cards page.");
                CardScrollerPage page = new CardScrollerPage(
                    ScrollersContainer,
                    instance.Card,
                    UnlockHelpers.GetAllUnmoddedUnlocksEnumerable().ToList());
                _pages.Add(page);

                HashSet<int> registeredUnlockIDs = new HashSet<int>();

                Main.LogInfo($"Adding registered modded cards page.");
                foreach (CardsManagerUtil.UnlockPageData unlockPageData in CardsManagerUtil.GetRegisteredPages())
                {
                    List<int> keys = new List<int>();
                    List<Unlock> cards = new List<Unlock>();

                    foreach (KeyValuePair<int, List<Unlock>> kvp in unlockPageData.Rows.OrderBy(x => x.Key))
                    {
                        int rowKey = kvp.Key;
                        List<int> order = unlockPageData.Order[rowKey];
                        List<Unlock> unlocks = kvp.Value;
                        foreach (Unlock unlock in unlocks.OrderBy(x => order[unlocks.IndexOf(x)]))
                        { keys.Add(rowKey);
                            cards.Add(unlock);
                            if (!registeredUnlockIDs.Contains(unlock.ID))
                            {
                                registeredUnlockIDs.Add(unlock.ID);
                            }
                        }
                    }

                    page = new CardScrollerPage(
                        ScrollersContainer,
                        instance.Card,
                        cards,
                        name: unlockPageData.Name,
                        customRowKeys: keys);
                    _pages.Add(page);
                }

                List<Unlock> nonregisteredModdedUnlocks = UnlockHelpers.GetAllModdedUnlocksEnumerable().Where(x => !registeredUnlockIDs.Contains(x.ID)).ToList();
                Main.LogInfo($"Registered {registeredUnlockIDs.Count} modded card(s) to mod author-defined pages.");
                Main.LogInfo($"{nonregisteredModdedUnlocks.Count} modded card(s) not registered.");
                // Modded unlocks page (Excluding those registered on custom pages, if there are modded unlocks)
                if (nonregisteredModdedUnlocks.Count > 0)
                {
                    Main.LogInfo("Added to unsorted modded cards page.");
                    page = new CardScrollerPage(
                        ScrollersContainer,
                        instance.Card,
                        nonregisteredModdedUnlocks);
                    _pages.Add(page);
                }
                else
                {
                    Main.LogInfo("Skipping unsorted modded cards page.");
                }
                _selectedPageIndex = 0;
                _pages[_selectedPageIndex].Enabled = true;

                _parentOriginalLocalPosition = instance.Card.transform.parent.localPosition;
                //CreateLegendDisplay();
                _init = true;
            }
        }

        private static void CreateLegendDisplay()
        {
            GameObject legendDisplay = new GameObject();
            legendDisplay.transform.parent = ScrollersContainer.transform;
            legendDisplay.transform.localScale = Vector3.one;
            legendDisplay.transform.localPosition = Vector3.zero;
            legendDisplay.transform.localRotation = Quaternion.identity;
        }

        private enum ToggleType
        {
            None,
            Single,
            Row
        }

        [HarmonyPatch(typeof(CardScrollerElement), "HandleInteraction")]
        [HarmonyPrefix]
        private static bool HandleInteraction(ref bool __result, ref CardScrollerElement __instance, ref int ___Index, ref List<ICard> ___Cards, InputState state)
        {
            if (Cache.IsChanged(new ActiveCardsCache(GameInfo.AllCurrentCards)))
            {
                Main.LogInfo("Cache Update");
                UpdateActiveCardsCardBank(ref __instance, ref ___Index, ref ___Cards);
            }

            if (!IsScrolling)
            {
                scrollHeldTime = 0f;
                scrollTickProgress = 0f;
            }
            IsScrolling = false;

            if (HandleToggleAutoAdd(state, out ToggleType toggleAutoAddType))
            {
                switch (toggleAutoAddType)
                {
                    case ToggleType.Single:
                        ToggleAutoAddCard();
                        _pages[_selectedPageIndex].Redraw();
                        break;
                    case ToggleType.Row:
                        break;
                    default:
                        break;
                }
            }

            if (HandleToggleCard(state, out ToggleType toggleCardType))
            {
                switch (toggleCardType)
                {
                    case ToggleType.Single:
                        ToggleCard(__instance.Card);
                        _pages[_selectedPageIndex].Redraw();
                        break;
                    case ToggleType.Row:
                        ToggleRow(__instance.Card);
                        break;
                    default:
                        break;
                }
            }

            if (HandleReadyButtonHold(state, (___Cards.Count > 0 ? ___Cards[___Index] : null)))
            {
                string s;
                if (IsCardManagerMode)
                {
                    AddSelectedCardToRun(out s);
                }
                else
                {
                    RemoveSelectedCardFromRun((___Cards[___Index] as Unlock).ID, out s);
                }
                Main.LogInfo(s);
            }

            if (HandleAnimation() || GrabHeldTime > 0f || ReadyHeldTime > 0f || PerformedReady)
            {
                HandleInteractionRequireRedraw = true;
                __result = true;
            }

            if (HandlePageChange(state))
            {
                HandleInteractionRequireRedraw = true;
                __result = true;
                return false;
            }

            if (HandleInteractionRequireRedraw)
            {
                HandleInteractionRequireRedraw = false;
                mSetIndex.Invoke(__instance, new object[] { IsCardManagerMode ? _pages[_selectedPageIndex].SelectedIndex : ___Index });
                return false;
            }

            if (!IsCardManagerMode)
            {
                return true;
            }


            List<Orientation> scrollDirection = new List<Orientation>();
            bool isPressed = false;

            if (state.MenuLeft == ButtonState.Pressed || state.MenuLeft == ButtonState.Held)
            {
                isPressed = state.MenuLeft == ButtonState.Pressed;
                scrollDirection.Add(Orientation.Left);
            }
            if (state.MenuRight == ButtonState.Pressed || state.MenuRight == ButtonState.Held)
            {
                isPressed = state.MenuRight == ButtonState.Pressed;
                scrollDirection.Add(Orientation.Right);
            }
            if (state.MenuUp == ButtonState.Pressed || state.MenuUp == ButtonState.Held)
            {
                isPressed = state.MenuUp == ButtonState.Pressed;
                scrollDirection.Add(Orientation.Up);
            }
            if (state.MenuDown == ButtonState.Pressed || state.MenuDown == ButtonState.Held)
            {
                isPressed = state.MenuDown == ButtonState.Pressed;
                scrollDirection.Add(Orientation.Down);
            }

            if (scrollDirection.Count == 0)
            {
                __result = false;
                return false;
            }

            if (scrollDirection.Count == 1)
            {
                IsScrolling = true;
                if (isPressed || (scrollHeldTime > scrollContinuousLowerThreshold && scrollTickProgress > scrollSpeedMax / scrollHeldTime * minScrollDelay))
                {
                    switch (scrollDirection.First())
                    {
                        case Orientation.Left:
                            mSetIndex.Invoke(__instance, new object[] { _pages[_selectedPageIndex].SelectPreviousCard() });
                            break;
                        case Orientation.Right:
                            mSetIndex.Invoke(__instance, new object[] { _pages[_selectedPageIndex].SelectNextCard() });
                            break;
                        case Orientation.Up:
                            mSetIndex.Invoke(__instance, new object[] { _pages[_selectedPageIndex].SelectNextRow() });
                            break;
                        case Orientation.Down:
                            mSetIndex.Invoke(__instance, new object[] { _pages[_selectedPageIndex].SelectPreviousRow() });
                            break;
                    }
                    scrollTickProgress = 0f;
                }
                scrollTickProgress += Time.deltaTime;
                scrollHeldTime = Mathf.Clamp(scrollHeldTime + Time.deltaTime, 0, scrollSpeedMax);
            }
            __result = true;
            return false;
        }

        private static void UpdateActiveCardsCardBank(ref CardScrollerElement instance, ref int selectedIndex, ref List<ICard> cards)
        {
            Cache.Cards = GameInfo.AllCurrentCards;
            int index = selectedIndex;
            for (int i = 0; i < instance.CardBank.Count; i++)
            {
                instance.CardBank[i].Destroy();
            }
            instance.CardBank.Clear();
            instance.SetCardList(GameInfo.AllCurrentCards);
            if (cards.Count == 0)
                selectedIndex = 0;
            else
                selectedIndex = Mathf.Clamp(index, 0, cards.Count - 1);
        }

        private static bool HandleAnimation()
        {
            bool animationUpdate = false;
            for (int i = _pages.Count - 1; i > -1; i--)
            {
                if (_pages[i].RowToggleStep())
                {
                    animationUpdate = true;
                }
            }

            if (IsPerformingAnimation)
            {
                if (IsAnimationDone)
                {
                    ReadyHeldTime = 0f;
                    AnimationCurrentTime = 0f;
                    IsPerformingAnimation = false;
                }
                else
                {
                    AnimationCurrentTime += Time.deltaTime;
                    animationUpdate = true;
                }
                HandleInteractionRequireRedraw = true;
            }
            return animationUpdate;
        }

        private static bool HandlePageChange(InputState state)
        {
            bool changePage = false;
            if (state.InteractAction == ButtonState.Pressed)
            {
                if (GameInfo.CurrentScene != SceneType.Kitchen)
                    IsCardManagerMode = true;

                if (IsCardManagerMode)
                {
                    _selectedPageIndex++;
                    if (_selectedPageIndex >= _pages.Count)
                    {
                        if (GameInfo.CurrentScene == SceneType.Kitchen)
                        {
                            IsCardManagerMode = false;
                        }
                        _selectedPageIndex = 0;
                    }
                }
                else
                {
                    IsCardManagerMode = true;
                }
                changePage = true;
            }
            return changePage;
        }

        private static bool HandleToggleCard(InputState state, out ToggleType toggleType)
        {
            toggleType = ToggleType.None;
            if (IsCardManagerMode && !IsReadyDown && !IsStopMovingDown)
            {
                if (!PerformedGrab && (state.GrabAction == ButtonState.Held || state.GrabAction == ButtonState.Pressed))
                {
                    GrabHeldTime += Time.deltaTime;
                    if (GrabHeldTime > GrabHeldThreshold)
                    {
                        toggleType = ToggleType.Row;
                        PerformedGrab = true;
                    }
                    return true;
                }
                else if (state.GrabAction == ButtonState.Released)
                {
                    if (!PerformedGrab && GrabHeldTime > 0f)
                    {
                        toggleType = ToggleType.Single;
                    }
                    PerformedGrab = false;
                    GrabHeldTime = 0f;
                    _pages[_selectedPageIndex].Redraw();
                    return true;
                }
            }
            else
            {
                PerformedGrab = false;
                GrabHeldTime = 0f;
            }
            return false;
        }

        private static bool HandleToggleAutoAdd(InputState state, out ToggleType toggleType)
        {
            toggleType = ToggleType.None;
            if (IsCardManagerMode && !IsGrabDown && !IsReadyDown)
            {
                if (!PerformedStopMoving && (state.StopMoving == ButtonState.Held || state.StopMoving == ButtonState.Pressed))
                {
                    StopMovingHeldTime += Time.deltaTime;
                    if (StopMovingHeldTime > StopMovingHeldThreshold)
                    {
                        toggleType = ToggleType.Row;
                        PerformedStopMoving = true;
                    }
                    return true;
                }
                else if (state.StopMoving == ButtonState.Released)
                {
                    if (!PerformedStopMoving && StopMovingHeldTime > 0f)
                    {
                        toggleType = ToggleType.Single;
                    }
                    PerformedStopMoving = false;
                    StopMovingHeldTime = 0f;
                    _pages[_selectedPageIndex].Redraw();
                    return true;
                }
            }
            else
            {
                PerformedStopMoving = false;
                StopMovingHeldTime = 0f;
            }
            return false;
        }

        private static bool HandleReadyButtonHold(InputState state, ICard activeCardSelected)
        {
            if (state.SecondaryAction1 == ButtonState.Released)
            {
                PerformedReady = false;
                ReadyHeldTime = 0f;
                HandleInteractionRequireRedraw = true;
            }

            else if (NetworkHelper.IsHost() && !PerformedReady && CardsManagerController.IsInKitchen
                && (state.SecondaryAction1 == ButtonState.Held || state.SecondaryAction1 == ButtonState.Pressed))
            {
                string statusMessage = "Unknown ready held error";
                if ((IsCardManagerMode && CardsManagerController.CanBeAddedToRun(_pages[_selectedPageIndex].SelectedUnlock.ID, out statusMessage)) ||
                    (!IsCardManagerMode && activeCardSelected != null && activeCardSelected is Unlock activeUnlockSelected && CardsManagerController.CanBeRemovedFromRun(activeUnlockSelected.ID, out statusMessage)))
                {
                    ReadyHeldTime += Time.deltaTime;
                    HandleInteractionRequireRedraw = true;
                    if (ReadyHeldTime > ReadyHeldThreshold)
                    {
                        IsPerformingAnimation = true;
                        PerformedReady = true;
                        return true;
                    }
                }
                else
                {
                    Main.LogInfo(statusMessage);
                    PerformedReady = true;
                }
            }
            return false;
        }

        private static void ToggleRow(UnlockCardElement mainCardDisplay)
        {
            ToggleCard(mainCardDisplay);
            _pages[_selectedPageIndex].AddRowToggler();
        }

        private static void ToggleCard(UnlockCardElement mainCardDisplay)
        {
            Color newColor = _pages[_selectedPageIndex].ToggleEnabledSelectedCard();
            if (mainCardDisplay != null)
            {
                UnlockHelpers.SetColor(mainCardDisplay, newColor);
            }
        }

        private static void ToggleAutoAddCard()
        {
            bool isAutoAdd = _pages[_selectedPageIndex].ToggleAutoAddSelectedCard();
        }

        private static void AddSelectedCardToRun(out string statusMessage)
        {
            _pages[_selectedPageIndex].AddSelectedCardToRun(out statusMessage);
        }

        private static bool RemoveSelectedCardFromRun(int unlockID, out string statusMessage)
        {
            return CardsManagerController.RemoveProgressionUnlock(unlockID, out statusMessage);
        }
    }
}
