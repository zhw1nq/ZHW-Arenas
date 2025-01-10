using Menu.Enums;

namespace Menu
{
    public class MenuBase(MenuValue title)
    {
        public Action<MenuButtons, MenuBase, MenuItem?>? Callback;
        public MenuValue Title { get; set; } = title;
        public List<MenuItem> Items { get; set; } = [];
        public int Option { get; set; } = 0;

        public bool RequiresFreeze { get; set; } = false;
        public bool AcceptButtons { get; set; } = false;
        public bool AcceptInput { get; set; } = false;

        public MenuValue[] Cursor =
        [
            new MenuValue("►") { Prefix = "<font color=\"#ff3333\">", Suffix = "</font>" },
            new MenuValue("◄") { Prefix = "<font color=\"#ff3333\">", Suffix = "</font>" },
        ];

        public MenuValue[] Selector =
        [
            new MenuValue("[ ") { Prefix = "<font color=\"#ff3333\">", Suffix = "</font>" },
            new MenuValue(" ]") { Prefix = "<font color=\"#ff3333\">", Suffix = "</font>" },
        ];

        public MenuValue[] Bool =
        [
            new MenuValue("✘") { Prefix = "<font color=\"#FF0000\">", Suffix = "</font>" },
            new MenuValue("✔") { Prefix = "<font color=\"#008000\">", Suffix = "</font>" },
        ];

        public MenuValue[] Slider =
        [
            new MenuValue("(") { Prefix = "<font color=\"#FFFFFF\">", Suffix = "</font>" },
            new MenuValue(")") { Prefix = "<font color=\"#FFFFFF\">", Suffix = "</font>" },
            new MenuValue("-") { Prefix = "<font color=\"#FFFFFF\">", Suffix = "</font>" },
            new MenuValue("|") { Prefix = "<font color=\"#FFFFFF\">", Suffix = "</font>" },
        ];

        public MenuValue Input = new("________") { Prefix = "<font color=\"#FFFFFF\">", Suffix = "</font>" };
        public MenuValue Separator = new(" - ") { Prefix = "<font color=\"#FFFFFF\">", Suffix = "</font>" };

        public void AddItem(MenuItem item)
        {
            Items.Add(item);
        }
    }
}