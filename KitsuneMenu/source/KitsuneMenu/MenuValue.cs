using CounterStrikeSharp.API.Core;

namespace Menu
{
    public class MenuValue(string value) : IMenuFormat
    {
        public string Value { get; set; } = value;
        private string _prefix = "";
        private string _suffix = "";
        public string? OriginalPrefix { get; private set; }
        public string? OriginalSuffix { get; private set; }

        public string Prefix
        {
            get => _prefix;
            set
            {
                if (OriginalPrefix == null)
                    OriginalPrefix = value;
                _prefix = value;
            }
        }

        public string Suffix
        {
            get => _suffix;
            set
            {
                if (OriginalSuffix == null)
                    OriginalSuffix = value;
                _suffix = value;
            }
        }

        public override string ToString()
        {
            return $"{Prefix}{Value}{Suffix}";
        }

        public MenuValue Copy()
        {
            return new(Value) { Prefix = Prefix, Suffix = Suffix };
        }
    }

    public class MenuButtonCallback : MenuValue
    {
        private static readonly int MaxLength = 26;

        public MenuButtonCallback(string value, string data, Action<CCSPlayerController, string> callback, bool disabled = false, bool trimValue = false)
            : base(trimValue ? TrimValue(value) : value)
        {
            Callback = callback;
            Data = data;
            Disabled = disabled;
        }

        public Action<CCSPlayerController, string> Callback { get; }
        public string Data { get; }
        public bool Disabled { get; }

        private static string TrimValue(string value) => value.Length > MaxLength ? value.Substring(0, MaxLength) + "..." : value;

        public new MenuButtonCallback Copy()
        {
            return new(Value, Data, Callback, Disabled, true) { Prefix = Prefix, Suffix = Suffix };
        }
    }
}