using CounterStrikeSharp.API.Core;

namespace ZHWArenaSharedApi
{
	public interface IZHWArenaSharedApi
	{
		public int AddSpecialRound(string name, int teamSize, bool enabledByDefault, Action<List<CCSPlayerController>?, List<CCSPlayerController>?> startFunction, Action<List<CCSPlayerController>?, List<CCSPlayerController>?> endFunction);
		public void RemoveSpecialRound(int id);
		public int GetArenaPlacement(CCSPlayerController player);
		public string GetArenaName(CCSPlayerController player);
		public void PerformAFKAction(CCSPlayerController player, bool afk);
	}
}
