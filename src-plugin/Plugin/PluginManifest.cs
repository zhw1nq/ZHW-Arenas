namespace ZHWArenas
{
    using CounterStrikeSharp.API.Core;

    public sealed partial class Plugin : BasePlugin
    {
        public override string ModuleName => "ZHW-Arenas";

        public override string ModuleDescription => "An arena plugin for Counter-Strike2";

        public override string ModuleAuthor => "ZHWryuu";

        public override string ModuleVersion => "1.5.4 " +
#if RELEASE
            "(release)";
#else
            "(debug)";
#endif
    }
}