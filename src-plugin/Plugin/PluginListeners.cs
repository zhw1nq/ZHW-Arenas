namespace ZHWArenas
{
	using CounterStrikeSharp.API;
	using CounterStrikeSharp.API.Core;
	using CounterStrikeSharp.API.Modules.Commands;
	using CounterStrikeSharp.API.Modules.Utils;
	using ZHWArenas.Models;
	using Microsoft.Extensions.Logging;

	public sealed partial class Plugin : BasePlugin
	{
		public void Initialize_Listeners()
		{
			AddCommandListener("jointeam", ListenerJoinTeam);
		}

		public HookResult ListenerJoinTeam(CCSPlayerController? player, CommandInfo info)
		{
			if (player?.IsValid == true && player.PlayerPawn?.IsValid == true)
			{
				ArenaPlayer? arenaPlayer = Arenas?.FindPlayer(player);
				if (arenaPlayer != null)
					arenaPlayer.PlayerIsSafe = true;

				if (player.Team != CsTeam.None)
				{
					if (arenaPlayer?.AFK == false && player.Team != CsTeam.Spectator && info.ArgByIndex(1) == "1")
					{
						arenaPlayer!.AFK = true;

						arenaPlayer.ArenaTag = $"{Localizer["zhw.general.afk"]} |";

						if (!Config.CompatibilitySettings.DisableClantags)
						{
							player.Clan = arenaPlayer.ArenaTag;
							Utilities.SetStateChanged(player, "CCSPlayerController", "m_szClan");
						}

						player!.ChangeTeam(CsTeam.Spectator);

						player.PrintToChat($" {Localizer["zhw.general.prefix"]} {string.Format(Localizer["zhw.chat.afk_enabled"], Config.CommandSettings.AFKCommands.FirstOrDefault("Missing"))}");
						return HookResult.Stop;
					}
					else if (arenaPlayer?.AFK == true && player.Team == CsTeam.Spectator && (info.ArgByIndex(1) == "2" || info.ArgByIndex(1) == "3"))
					{
						arenaPlayer!.AFK = false;

						arenaPlayer.ArenaTag = $"{Localizer["zhw.general.waiting"]} |";

						if (!Config.CompatibilitySettings.DisableClantags)
						{
							arenaPlayer.Controller.Clan = arenaPlayer.ArenaTag;
							Utilities.SetStateChanged(arenaPlayer.Controller, "CCSPlayerController", "m_szClan");
						}

						player.PrintToChat($" {Localizer["zhw.general.prefix"]} {Localizer["zhw.chat.afk_disabled"]}");
						return HookResult.Continue;
					}

					player.ExecuteClientCommand("play sounds/ui/weapon_cant_buy.vsnd_c");
					return HookResult.Stop;
				}
			}

			return HookResult.Continue;
		}
	}
}