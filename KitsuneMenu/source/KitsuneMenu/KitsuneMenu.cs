using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Memory;
using CounterStrikeSharp.API.Modules.Utils;
using Menu.Enums;
using System.Collections.Concurrent;

namespace Menu
{
    public sealed partial class KitsuneMenu
    {
        private static BasePlugin _plugin = null!;
        private static readonly ConcurrentDictionary<CCSPlayerController, Stack<MenuBase>> Menus = new();
        private static readonly SayEvent OnSay = new("say", OnSayEvent);
        private static readonly SayEvent OnSayTeam = new("say_team", OnSayEvent);
        private static readonly OnTick OnTick = new(OnTickListener);
        public static event EventHandler<MenuEvent>? OnDrawMenu;

        private static readonly ConcurrentDictionary<CCSPlayerController, (MenuButtons Button, DateTime LastPress, int RepeatCount)> ButtonHoldState = new();
        private static readonly ConcurrentDictionary<CCSPlayerController, MenuButtons> LastButtonState = new();
        private static readonly ConcurrentDictionary<CCSPlayerController, (int ObserverMode, bool BlockNext)> ObserverMode = new();
        private static readonly HashSet<CCSPlayerController> FrozenPlayers = [];
        private static readonly HashSet<CCSPlayerController> PendingFreeze = [];

        private const float InitialDelay = 0.5f;
        private const float RepeatDelay = 0.1f;

        private static MenuConfiguration _config = null!;
        public static MenuTranslator Translator = null!;
        private static bool MultiCast = false;

        public KitsuneMenu(BasePlugin plugin, bool multiCast = false)
        {
            _plugin = plugin;
            MultiCast = multiCast;

            _config = new MenuConfiguration();
            _config.Initialize();

            Translator = new MenuTranslator();
            Translator.Initialize();

            _plugin.RegisterEventHandler<EventPlayerSpawn>(OnPlayerSpawnListener);
        }

        private static HookResult OnSayEvent(CCSPlayerController? controller, string message)
        {
            if (controller == null || !controller.IsValid || !Menus.TryGetValue(controller, out var value))
                return HookResult.Continue;

            var menu = value.Peek();

            if (!menu.AcceptInput)
                return HookResult.Continue;

            var selectedItem = menu.Items[menu.Option];
            if (selectedItem != null)
            {
                selectedItem.DataString = message;
                menu.AcceptInput = false;

                menu.Callback?.Invoke(_config.GetInputButton(), menu, selectedItem);
            }
            return HookResult.Handled;
        }

        private static HookResult OnPlayerSpawnListener(EventPlayerSpawn @event, GameEventInfo info)
        {
            var controller = @event.Userid;
            if (controller == null || !controller.IsValid)
                return HookResult.Continue;

            UpdatePlayerFreeze(controller);
            return HookResult.Continue;
        }

        private static void OnTickListener()
        {
            var currentTime = DateTime.Now;
            var playersToRemove = new List<CCSPlayerController>();

            foreach (var kvp in Menus)
            {
                var controller = kvp.Key;
                var menus = kvp.Value;

                if (!IsPlayerValid(controller) || menus.Count == 0)
                {
                    CleanupPlayer(controller);
                    playersToRemove.Add(controller);
                    continue;
                }

                UpdatePlayerFreeze(controller);

                var menu = menus.Peek();
                var currentButtons = GetCurrentButtons(controller);
                var selectedItem = menu.Items[menu.Option];

                HandleButtonPress(controller, currentButtons, menu, selectedItem, currentTime);

                DrawMenu(controller, menu, selectedItem);
                RaiseDrawMenu(controller, menu, selectedItem);
            }

            foreach (var player in playersToRemove)
            {
                Menus.TryRemove(player, out _);
                UpdatePlayerFreeze(player);
            }
        }

        private static bool IsPlayerValid(CCSPlayerController controller)
        {
            return controller.IsValid && controller.PlayerPawn.IsValid && controller.Connected == PlayerConnectedState.PlayerConnected;
        }

        private static void CleanupPlayer(CCSPlayerController controller)
        {
            ObserverMode.TryRemove(controller, out _);
            LastButtonState.TryRemove(controller, out _);
        }

        private static MenuButtons GetCurrentButtons(CCSPlayerController controller)
        {
            var currentButtons = (MenuButtons)controller.Buttons;
            var observerButtons = HandleObserverMode(controller);
            return observerButtons != 0 ? observerButtons : currentButtons;
        }

        private static void HandleButtonPress(CCSPlayerController controller, MenuButtons currentButtons, MenuBase menu, MenuItem? selectedItem, DateTime currentTime)
        {
            if (!LastButtonState.TryGetValue(controller, out var lastButtons))
            {
                lastButtons = 0;
            }

            bool buttonHandled = false;

            if (currentButtons != lastButtons)
            {
                if (currentButtons != 0)
                {
                    buttonHandled = HandleMenuButton(currentButtons, menu, selectedItem, controller);
                    ButtonHoldState[controller] = (currentButtons, currentTime, 0);
                }
                else
                {
                    ButtonHoldState.TryRemove(controller, out _);
                }
            }
            else if (currentButtons != 0)
            {
                if (ButtonHoldState.TryGetValue(controller, out var holdState))
                {
                    var elapsed = (currentTime - holdState.LastPress).TotalSeconds;
                    if (elapsed >= InitialDelay)
                    {
                        var repeatCount = (int)((elapsed - InitialDelay) / RepeatDelay);
                        if (repeatCount > holdState.RepeatCount)
                        {
                            buttonHandled = HandleMenuButton(currentButtons, menu, selectedItem, controller);
                            ButtonHoldState[controller] = (holdState.Button, holdState.LastPress, repeatCount);
                        }
                    }
                }
            }

            LastButtonState[controller] = currentButtons;
            menu.AcceptButtons = !buttonHandled;
        }

        private static bool CheckButton(MenuButtons buttons, MenuButtons targetButton)
        {
            if (MultiCast)
                return (buttons & targetButton) == targetButton;
            return buttons == targetButton;
        }

        private static bool HandleMenuButton(MenuButtons buttons, MenuBase menu, MenuItem? selectedItem, CCSPlayerController controller)
        {
            if (CheckButton(buttons, _config.GetSelectButton()))
            {
                if (selectedItem == null) return false;
                switch (selectedItem.Type)
                {
                    case MenuItemType.Bool:
                        if (selectedItem.Data.Length > 0)
                        {
                            selectedItem.Data[0] = selectedItem.Data[0] == 0 ? 1 : 0;
                        }
                        break;

                    case MenuItemType.ChoiceBool:
                        if (selectedItem.Data.Length > selectedItem.Option)
                        {
                            selectedItem.Data[selectedItem.Option] = selectedItem.Data[selectedItem.Option] == 0 ? 1 : 0;
                        }
                        break;

                    case MenuItemType.Input:
                        menu.AcceptInput = true;
                        break;
                }
                menu.Callback?.Invoke(_config.GetSelectButton(), menu, selectedItem);
                return true;
            }

            if (CheckButton(buttons, _config.GetUpButton()) || CheckButton(buttons, _config.GetDownButton()))
            {
                if (!menu.AcceptInput)
                {
                    List<int> selectableValues = menu.Items.Any(i => i.Type is not (MenuItemType.Spacer or MenuItemType.Text)) ? menu.Items.Where(i => i.Type is not (MenuItemType.Spacer or MenuItemType.Text)).Select(i => menu.Items.IndexOf(i)).ToList() : menu.Items.Select((item, index) => index).ToList();

                    if (selectableValues.Count > 0)
                    {
                        int currentOption = selectableValues.IndexOf(menu.Option);

                        if (CheckButton(buttons, _config.GetUpButton()))
                        {
                            if (currentOption != 0)
                                menu.Option = selectableValues[currentOption - 1];

                            menu.Callback?.Invoke(_config.GetUpButton(), menu, selectedItem);
                        }
                        else
                        {
                            if (currentOption != selectableValues.Count - 1)
                                menu.Option = selectableValues[currentOption + 1];

                            menu.Callback?.Invoke(_config.GetDownButton(), menu, selectedItem);
                        }
                    }
                }
                return true;
            }

            if ((CheckButton(buttons, _config.GetLeftButton()) || CheckButton(buttons, _config.GetRightButton())) && selectedItem != null && !menu.AcceptInput)
            {
                switch (selectedItem.Type)
                {
                    case MenuItemType.Choice:
                    case MenuItemType.ChoiceBool:
                    case MenuItemType.Button:
                        if (selectedItem.Values != null && selectedItem.Values.Count > 0)
                        {
                            if (selectedItem.Pinwheel)
                            {
                                selectedItem.Option = CheckButton(buttons, _config.GetLeftButton())
                                    ? (selectedItem.Option - 1 + selectedItem.Values.Count) % selectedItem.Values.Count
                                    : (selectedItem.Option + 1) % selectedItem.Values.Count;
                            }
                            else
                            {
                                selectedItem.Option = CheckButton(buttons, _config.GetLeftButton())
                                    ? Math.Max(0, selectedItem.Option - 1)
                                    : Math.Min(selectedItem.Values.Count - 1, selectedItem.Option + 1);
                            }
                        }
                        break;

                    case MenuItemType.Slider:
                        if (selectedItem.Data.Length > 0)
                        {
                            selectedItem.Data[0] = CheckButton(buttons, _config.GetLeftButton())
                                ? Math.Max(0, selectedItem.Data[0] - 1)
                                : Math.Min(10, selectedItem.Data[0] + 1);
                        }
                        break;
                }
                menu.Callback?.Invoke(CheckButton(buttons, _config.GetLeftButton()) ? _config.GetLeftButton() : _config.GetRightButton(), menu, selectedItem);
                return true;
            }

            if (CheckButton(buttons, _config.GetBackButton()))
            {
                if (menu.AcceptInput)
                {
                    menu.AcceptInput = false;
                }
                else if (Menus.TryGetValue(controller, out var menuStack) && menuStack.Count > 1)
                {
                    menu.Callback?.Invoke(_config.GetBackButton(), menu, null);
                    menuStack.Pop();
                }
                return false;
            }

            if (CheckButton(buttons, _config.GetExitButton()))
            {
                menu.Callback?.Invoke(_config.GetExitButton(), menu, null);

                if (controller.ObserverPawn?.Value?.ObserverServices != null && (ConVar.Find("mp_forcecamera")?.GetPrimitiveValue<int>() ?? 0) == 1)
                    controller.ObserverPawn.Value.ObserverServices.ObserverMode = (int)ObserverMode_t.OBS_MODE_IN_EYE;

                ObserverMode.TryRemove(controller, out _);
                Menus.TryRemove(controller, out _);
                InitiatePlayerFreeze(controller, false);
                return false;
            }

            return false;
        }

        private static void RaiseDrawMenu(CCSPlayerController controller, MenuBase menu, MenuItem? selectedItem)
        {
            OnDrawMenu?.Invoke(null, new MenuEvent(controller, menu, selectedItem, 0));
        }

        public static void DrawMenu(CCSPlayerController controller, MenuBase menu, MenuItem? selectedItem)
        {
            var html = "";

            if (!Menus.TryGetValue(controller, out var menus))
                return;

            html += $"\u00A0{menu.Title}";

            bool hasSelectableItems = false;
            foreach (var item in menu.Items)
            {
                if (item.Type is not (MenuItemType.Spacer or MenuItemType.Text))
                {
                    hasSelectableItems = true;
                    break;
                }
            }

            foreach (var menuItem in menu.Items)
            {
                html += $"<br>\u00A0{menu.Title.Suffix}";

                if (hasSelectableItems && selectedItem != null && menuItem == selectedItem)
                    html += menu.Cursor[(int)MenuCursor.Left];

                if (menuItem.Head != null)
                    html += menuItem.Head;

                switch (menuItem.Type)
                {
                    case MenuItemType.Choice or MenuItemType.ChoiceBool or MenuItemType.Button:
                        html += FormatValues(menu, menuItem, selectedItem!);
                        break;

                    case MenuItemType.Slider:
                        html += FormatSlider(menu, menuItem);
                        break;

                    case MenuItemType.Input:
                        html += FormatInput(menu, menuItem, selectedItem!);
                        break;

                    case MenuItemType.Bool:
                        html += FormatBool(menu, menuItem);
                        break;
                }

                if (menuItem.Tail != null)
                    html += menuItem.Tail;

                if (hasSelectableItems && selectedItem != null && menuItem == selectedItem)
                    html += menu.Cursor[(int)MenuCursor.Right];
            }

            controller.PrintToCenterHtml(html);
        }

        private static string FormatValues(MenuBase menu, MenuItem menuItem, MenuItem selectedItem)
        {
            var html = "";

            if (menuItem.Pinwheel)
            {
                var prev = menuItem.Option - 1;
                var next = menuItem.Option + 1;

                if (prev < 0)
                    prev = menuItem.Values!.Count - 1;

                if (next > menuItem.Values!.Count - 1)
                    next = 0;

                html += $"{FormatString(menu, menuItem, prev)} ";
                html += $"{FormatSelector(menu, menuItem, selectedItem, MenuCursor.Left)}{FormatString(menu, menuItem, menuItem.Option)}{FormatSelector(menu, menuItem, selectedItem, MenuCursor.Right)}";
                html += $" {FormatString(menu, menuItem, next)}";

                return html;
            }

            if (menuItem.Option == 0)
            {
                html += $"{FormatSelector(menu, menuItem, selectedItem, MenuCursor.Left)}{FormatString(menu, menuItem, 0)}{FormatSelector(menu, menuItem, selectedItem, MenuCursor.Right)}";

                for (var i = 0; i < 2 && i < menuItem.Values!.Count - 1; i++)
                    html += $" {FormatString(menu, menuItem, i + 1)}";
            }
            else if (menuItem.Option == menuItem.Values!.Count - 1)
            {
                for (var i = 2; i > 0; i--)
                {
                    if (menuItem.Option - i >= 0)
                        html += $"{FormatString(menu, menuItem, menuItem.Option - i)} ";
                }

                html += $"{FormatSelector(menu, menuItem, selectedItem, MenuCursor.Left)}{FormatString(menu, menuItem, menuItem.Option)}{FormatSelector(menu, menuItem, selectedItem, MenuCursor.Right)}";
            }
            else
                html += $"{FormatString(menu, menuItem, menuItem.Option - 1)} {FormatSelector(menu, menuItem, selectedItem, MenuCursor.Left)}{FormatString(menu, menuItem, menuItem.Option)}{FormatSelector(menu, menuItem, selectedItem, MenuCursor.Right)} {FormatString(menu, menuItem, menuItem.Option + 1)}";

            return html;
        }

        private static string FormatString(MenuBase menu, MenuItem menuItem, int index)
        {
            if (menuItem.Values == null)
                return "";

            var menuValue = menuItem.Values[index];

            if (menuItem.Type != MenuItemType.ChoiceBool)
                return menuValue.ToString();

            menuValue.Prefix = menuItem.Data[index] == 0 ? menu.Bool[(int)MenuBool.False].Prefix : menu.Bool[(int)MenuBool.True].Prefix;
            menuValue.Suffix = menuItem.Data[index] == 0 ? menu.Bool[(int)MenuBool.False].Suffix : menu.Bool[(int)MenuBool.True].Suffix;

            return menuValue.ToString();
        }

        private static string FormatSelector(MenuBase menu, MenuItem menuItem, MenuItem selectedItem, MenuCursor selector)
        {
            if (menuItem.Type is MenuItemType.Button or MenuItemType.ChoiceBool && menuItem != selectedItem)
                return "";

            return menu.Selector[(int)selector].ToString();
        }

        private static string FormatSlider(MenuBase menu, MenuItem menuItem)
        {
            var html = "";

            html += menu.Slider[(int)MenuSlider.Left].ToString();

            for (var i = 0; i < 11; i++)
                html += $"{(i == menuItem.Data[0] ? menu.Slider[(int)MenuSlider.Selected] : menu.Slider[(int)MenuSlider.Spacer])}{(i != 10 ? " " : "")}";

            html += menu.Slider[(int)MenuSlider.Right].ToString();

            return html;
        }

        private static string FormatInput(MenuBase menu, MenuItem menuItem, MenuItem selectedItem)
        {
            var html = "";

            if (menu.AcceptInput && menuItem == selectedItem)
                html += menu.Selector[(int)MenuCursor.Left].ToString();

            if (menuItem.DataString.Length == 0)
                html += menu.Input.Value;
            else
                html += $"{menu.Input.Prefix}{menuItem.DataString}{menu.Input.Suffix}";

            if (menu.AcceptInput && menuItem == selectedItem)
                html += menu.Selector[(int)MenuCursor.Right].ToString();

            return html;
        }

        private static string FormatBool(MenuBase menu, MenuItem menuItem)
        {
            return menuItem.Data[0] == 0 ? menu.Bool[(int)MenuBool.False].ToString() : menu.Bool[(int)MenuBool.True].ToString();
        }

        public void ShowMenu(CCSPlayerController controller, MenuBase menu, Action<MenuButtons, MenuBase, MenuItem?> callback, bool isSubmenu = false, bool freezePlayer = false)
        {
            var menus = Menus.GetOrAdd(controller, _ => new Stack<MenuBase>());

            menu.Callback = callback;
            menu.RequiresFreeze = freezePlayer;

            if (!isSubmenu)
            {
                menus.Clear();
            }

            List<MenuItem> filterList = menu.Items.Any(item => item.Type is not (MenuItemType.Spacer or MenuItemType.Text)) ? menu.Items.Where(item => item.Type is not (MenuItemType.Spacer or MenuItemType.Text)).ToList() : menu.Items;

            menu.Option = menu.Items.IndexOf(filterList.First());

            menus.Push(menu);

            InitiatePlayerFreeze(controller, freezePlayer);
        }

        public void ShowScrollableMenu(CCSPlayerController controller, string title, List<MenuItem> items, Action<MenuButtons, MenuBase, MenuItem?>? callback, bool isSubmenu = false, bool freezePlayer = false, int visibleItems = 5, Dictionary<int, object>? defaultValues = null, bool disableDeveloper = false)
        {
            MenuBase menu = null!;
            List<MenuItem> allItems = [.. items];
            List<MenuItem> filterList = items.Any(item => item.Type is not (MenuItemType.Spacer or MenuItemType.Text)) ? items.Where(item => item.Type is not (MenuItemType.Spacer or MenuItemType.Text)).ToList() : allItems;
            int currentIndex = 0;
            int startIndex = 0;

            void CreateMenu()
            {
                menu = new MenuBase(new MenuValue($"{title}{(allItems.Count > visibleItems ? $" <font class=\"fontSize-s\" color=\"#FFFFFF\">{Translator.GetTranslation("Items")} {currentIndex + 1}/{filterList.Count}</font>" : "")}")
                {
                    Prefix = "<font class=\"fontSize-m\" color=\"#ff3333\">",
                    Suffix = "</font>"
                });

                UpdateMenuView();

                void wrappedCallback(MenuButtons button, MenuBase m, MenuItem? selected)
                {
                    if (button == _config.GetDownButton())
                    {
                        if (currentIndex < filterList.Count - 1)
                        {
                            currentIndex++;
                            if (currentIndex >= startIndex + visibleItems)
                            {
                                startIndex = Math.Min(startIndex + 1, filterList.Count - visibleItems);
                            }
                            UpdateMenuView();
                        }
                    }
                    else if (button == _config.GetUpButton())
                    {
                        if (currentIndex > 0)
                        {
                            currentIndex--;
                            if (currentIndex < startIndex)
                            {
                                startIndex = Math.Max(0, currentIndex);
                            }
                            UpdateMenuView();
                        }
                    }
                    else
                    {
                        if (filterList.Count > 0)
                        {
                            if (currentIndex >= 0 && currentIndex < filterList.Count)
                            {
                                int preservedIndex = menu.Option;

                                MenuItem? selectedItem = filterList[currentIndex];
                                menu.Option = allItems.IndexOf(selectedItem);

                                if (selected?.Values != null &&
                                    selected.Values.Count > selected.Option &&
                                    selected.Values[selected.Option] is MenuButtonCallback customButton)
                                {
                                    if (callback is null)
                                    {
                                        customButton.Callback?.Invoke(controller, customButton.Data);
                                    }
                                    else
                                    {
                                        callback(_config.GetButtonValue(button), m, selectedItem);
                                    }
                                }
                                else
                                {
                                    callback?.Invoke(_config.GetButtonValue(button), m, selectedItem);
                                }

                                menu.Option = preservedIndex;
                            }
                        }
                        else
                        {
                            if (selected?.Values != null &&
                                selected.Values.Count > selected.Option &&
                                selected.Values[selected.Option] is MenuButtonCallback customButton)
                            {
                                if (callback is null)
                                {
                                    customButton.Callback?.Invoke(controller, customButton.Data);
                                }
                                else
                                {
                                    callback(_config.GetButtonValue(button), m, null);
                                }
                            }
                            else
                            {
                                callback?.Invoke(_config.GetButtonValue(button), m, null);
                            }
                        }
                    }
                }

                if (defaultValues != null)
                {
                    foreach (var kvp in defaultValues)
                    {
                        int index = kvp.Key;
                        object value = kvp.Value;

                        if (index >= 0 && index < items.Count)
                        {
                            if (value is bool boolValue)
                            {
                                items[index].Data[0] = boolValue ? 1 : 0;
                            }
                            else if (value is string stringValue)
                            {
                                items[index].DataString = stringValue;
                            }
                            else if (value is int intValue)
                            {
                                items[index].DataString = intValue.ToString();
                            }
                        }
                    }
                }

                ShowMenu(controller, menu, wrappedCallback, isSubmenu, freezePlayer);
            }

            void UpdateMenuView()
            {
                menu.Items.Clear();
                menu.Title = new MenuValue($"{title}{(allItems.Count > visibleItems ? $" <font class=\"fontSize-s\" color=\"#FFFFFF\">{Translator.GetTranslation("Items")} {currentIndex + 1}/{filterList.Count}</font>" : "")}")
                {
                    Prefix = "<font class=\"fontSize-m\" color=\"#ff3333\">",
                    Suffix = "<font color=\"#FFFFFF\" class=\"fontSize-sm\">"
                };

                int visibleCount = 0;
                for (int i = startIndex; i < allItems.Count && visibleCount < visibleItems; i++)
                {
                    var item = allItems[i];
                    if (item.Values?.Count > 0 && item.Values[0] is MenuValue menuValue)
                    {
                        menuValue.Prefix = menuValue.OriginalPrefix ?? "";
                        menuValue.Suffix = menuValue.OriginalSuffix ?? "";

                        bool isDisabled = menuValue is MenuButtonCallback customButton && customButton.Disabled;
                        if (isDisabled)
                        {
                            menuValue.Prefix += "<font color=\"#8f3b3b\">";
                            menuValue.Suffix = "</font>" + menuValue.Suffix;
                        }
                    }

                    menu.AddItem(item);
                    visibleCount++;
                }

                if (filterList.Count == 0)
                {
                    menu.AddItem(new MenuItem(MenuItemType.Text, new MenuValue(Translator.GetTranslation("EmptyMenu")) { Prefix = "<font color=\"#8f3b3b\" class=\"fontSize-m\">", Suffix = "<font color=\"#FFFFFF\">" }));
                }

                menu.AddItem(new MenuItem(MenuItemType.Spacer));

                if (!isSubmenu && !disableDeveloper)
                    menu.AddItem(new MenuItem(MenuItemType.Text, new MenuValue($"Developed by <font color=\"#f5a142\">KitsuneLab</font>") { Prefix = "<font color=\"#FFFFFF\" class=\"fontSize-s\">", Suffix = "</font>" }));

                menu.AddItem(new MenuItem(MenuItemType.Text, new MenuValue(isSubmenu ? Translator.GetTranslation("FooterSubMenu") : Translator.GetTranslation("FooterMain")) { Prefix = "<font color=\"#ff3333\" class=\"fontSize-s\">", Suffix = "<font color=\"#FFFFFF\">" }));

                MenuItem? selectedItem = null;
                if (filterList.Count > 0)
                {
                    selectedItem = filterList[currentIndex];
                    menu.Option = menu.Items.IndexOf(selectedItem);
                }
                else
                {
                    menu.Option = 0;
                }

                DrawMenu(controller, menu, selectedItem);
            }

            CreateMenu();
        }

        public void ClearMenus(CCSPlayerController controller)
        {
            if (Menus.TryRemove(controller, out _))
            {
                UpdatePlayerFreeze(controller);
            }
        }

        public void PopMenu(CCSPlayerController controller, MenuBase? menu = null)
        {
            if (!Menus.TryGetValue(controller, out var value))
                return;

            if (menu != null && value.Peek() != menu)
                return;

            value.Pop();

            if (value.Count == 0)
            {
                Menus.TryRemove(controller, out _);
                InitiatePlayerFreeze(controller, false);
            }
            else
            {
                UpdatePlayerFreeze(controller);
            }
        }

        public bool IsCurrentMenu(CCSPlayerController controller, MenuBase menu)
        {
            if (!Menus.TryGetValue(controller, out var value))
                return false;

            return value.Peek() == menu;
        }

        public Stack<MenuBase>? GetMenus(CCSPlayerController controller)
        {
            return Menus.TryGetValue(controller, out var value) ? value : null;
        }

        public void SetMenus(CCSPlayerController controller, Stack<MenuBase> menus)
        {
            Menus[controller] = menus;
            UpdatePlayerFreeze(controller);
        }

        private static void InitiatePlayerFreeze(CCSPlayerController controller, bool shouldFreeze)
        {
            if (controller.IsValid && controller.PlayerPawn.IsValid)
            {
                if (shouldFreeze && !FrozenPlayers.Contains(controller))
                {
                    PendingFreeze.Add(controller);
                }
                else if (FrozenPlayers.Remove(controller) || PendingFreeze.Remove(controller))
                {
                    Freeze(controller, false);
                }
            }
            else
            {
                FrozenPlayers.Remove(controller);
                PendingFreeze.Remove(controller);
            }
        }

        private static void UpdatePlayerFreeze(CCSPlayerController controller)
        {
            if (!controller.IsValid || !controller.PlayerPawn.IsValid || controller.PlayerPawn.Value?.Health <= 0)
            {
                FrozenPlayers.Remove(controller);
                PendingFreeze.Remove(controller);
                return;
            }

            bool shouldFreeze = false;
            if (Menus.TryGetValue(controller, out var menuStack))
            {
                foreach (var menu in menuStack)
                {
                    if (menu.RequiresFreeze)
                    {
                        shouldFreeze = true;
                        break;
                    }
                }
            }

            if (shouldFreeze && !FrozenPlayers.Contains(controller))
            {
                var playerPawn = controller.PlayerPawn.Value;
                if (playerPawn?.MoveType == MoveType_t.MOVETYPE_WALK && playerPawn?.OnGroundLastTick == true)
                {
                    Freeze(controller, true);
                    FrozenPlayers.Add(controller);
                    PendingFreeze.Remove(controller);
                }
                else
                {
                    PendingFreeze.Add(controller);
                }
            }
            else if (!shouldFreeze && FrozenPlayers.Contains(controller))
            {
                Freeze(controller, false);
                FrozenPlayers.Remove(controller);
            }
        }

        public static MenuButtons HandleObserverMode(CCSPlayerController controller)
        {
            var playerPawn = controller.PlayerPawn.Value;
            if (playerPawn == null || !playerPawn.IsValid)
                return 0;

            if (controller.Team <= CsTeam.Spectator || (LifeState_t)playerPawn.LifeState != LifeState_t.LIFE_ALIVE)
            {
                var observerPawn = controller.ObserverPawn?.Value?.ObserverServices;
                if (observerPawn == null)
                    return 0;

                bool forceCamera = ConVar.Find("mp_forcecamera")?.GetPrimitiveValue<int>() == 1;

                if (!ObserverMode.TryGetValue(controller, out var obsMode))
                {
                    obsMode = (observerPawn.ObserverMode, false);
                    ObserverMode[controller] = obsMode;

                    if (forceCamera)
                    {
                        ObserverMode[controller] = ((int)ObserverMode_t.OBS_MODE_CHASE, true);
                        return 0;
                    }
                }

                float deathTime = Server.CurrentTime - playerPawn.DeathTime;

                if (obsMode.ObserverMode != observerPawn.ObserverMode)
                {
                    observerPawn.ObserverMode = (byte)obsMode.ObserverMode;
                    if (!obsMode.BlockNext && !(forceCamera && deathTime > 4.0f && deathTime < 4.5f))
                        return _config.GetSelectButton();

                    ObserverMode[controller] = (obsMode.ObserverMode, false);
                }
            }
            return 0;
        }

        public static void SetMoveType(CCSPlayerController player, MoveType_t moveType)
        {
            var pawn = player.PlayerPawn.Value;
            if (pawn == null)
                return;

            pawn.MoveType = moveType;
            Utilities.SetStateChanged(pawn, "CBaseEntity", "m_MoveType");
            Schema.GetRef<MoveType_t>(pawn.Handle, "CBaseEntity", "m_nActualMoveType") = moveType;
        }

        public static void Freeze(CCSPlayerController player, bool freeze = true)
        {
            SetMoveType(player, freeze ? MoveType_t.MOVETYPE_OBSOLETE : MoveType_t.MOVETYPE_WALK);
        }
    }
}