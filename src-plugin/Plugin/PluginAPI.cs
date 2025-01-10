namespace ZHWArenas
{
	using CounterStrikeSharp.API;
	using CounterStrikeSharp.API.Core;
	using CounterStrikeSharp.API.Core.Capabilities;
	using CounterStrikeSharp.API.Modules.Utils;
	using ZHWArenas.Models;
	using ZHWArenaSharedApi;

	public sealed partial class Plugin : BasePlugin
	{
		public static PluginCapability<IZHWArenaSharedApi> Capability_SharedAPI { get; } = new("zhw-arenas:sharedapi");

		public void Initialize_API()
		{
			Capabilities.RegisterPluginCapability(Capability_SharedAPI, () => new ArenaAPIHandler(this));
		}
	}

	public class ArenaAPIHandler : IZHWArenaSharedApi
	{
		public Plugin plugin { get; set; }
		public ArenaAPIHandler(Plugin plugin)
		{
			this.plugin = plugin;
		}

		public int AddSpecialRound(string name, int teamSize, bool enabledByDefault, Action<List<CCSPlayerController>?, List<CCSPlayerController>?> startFunction, Action<List<CCSPlayerController>?, List<CCSPlayerController>?> endFunction)
		{
			return RoundType.AddSpecialRoundType(name, teamSize, enabledByDefault, startFunction, endFunction);
		}

		public void RemoveSpecialRound(int id)
		{
			RoundType.RemoveSpecialRoundType(id);
		}

		public int GetArenaPlacement(CCSPlayerController player)
		{
			var arenaPlayer = plugin.Arenas?.FindPlayer(player);
			return arenaPlayer is not null ? plugin.GetPlayerArenaID(arenaPlayer) : -1;
		}

		public string GetArenaName(CCSPlayerController player)
		{
			var arenaPlayer = plugin.Arenas?.FindPlayer(player);
			if (arenaPlayer is not null)
			{
				string arenaTag = arenaPlayer.ArenaTag;
				if (arenaTag.EndsWith(" |"))
					arenaTag = arenaTag.Substring(0, arenaTag.Length - 2);

				return arenaTag;
			}
			return string.Empty;
		}

		public void PerformAFKAction(CCSPlayerController player, bool afk)
		{
			ArenaPlayer? arenaPlayer = plugin.Arenas?.FindPlayer(player!);

			if (arenaPlayer is null)
				return;

			arenaPlayer!.AFK = afk;

			if (afk)
			{
				arenaPlayer!.AFK = true;
				player!.ChangeTeam(CsTeam.Spectator);
				arenaPlayer.ArenaTag = $"{plugin.Localizer["zhw.general.afk"]} |";

				if (!plugin.Config.CompatibilitySettings.DisableClantags)
				{
					player.Clan = arenaPlayer.ArenaTag;
					Utilities.SetStateChanged(player, "CCSPlayerController", "m_szClan");
				}
			}
			else
			{
				arenaPlayer.ArenaTag = $"{plugin.Localizer["zhw.general.waiting"]} |";

				if (!plugin.Config.CompatibilitySettings.DisableClantags)
				{
					arenaPlayer.Controller.Clan = arenaPlayer.ArenaTag;
					Utilities.SetStateChanged(arenaPlayer.Controller, "CCSPlayerController", "m_szClan");
				}
			}
		}
	}
}