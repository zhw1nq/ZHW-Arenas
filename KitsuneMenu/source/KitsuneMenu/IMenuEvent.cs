﻿using CounterStrikeSharp.API.Core;

namespace Menu
{
    public interface IMenuEvent
    {
    }

    public class MenuEvent(CCSPlayerController controller, MenuBase menu, MenuItem? selectedItem, ulong button) : IMenuEvent
    {
        public CCSPlayerController Controller { get; set; } = controller;
        public MenuBase Menu { get; set; } = menu;
        public MenuItem? SelectedItem { get; set; } = selectedItem;
        public ulong Button { get; set; } = button;
    }
}