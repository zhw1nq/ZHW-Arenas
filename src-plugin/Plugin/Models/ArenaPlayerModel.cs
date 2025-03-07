using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Entities.Constants;
using CounterStrikeSharp.API.Modules.Menu;
using CounterStrikeSharp.API.Modules.Utils;
using Menu;
using Menu.Enums;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;

namespace ZHWArenas.Models;

public class ArenaPlayer
{
	//** ? Main */
	private readonly Plugin Plugin;
	public readonly IStringLocalizer Localizer;
	public readonly PluginConfig Config;

	//** ? Player */
	public readonly CCSPlayerController Controller;
	public ChallengeModel? Challenge = null;
	public readonly ulong SteamID;
	public SpawnPoint? SpawnPoint;
	public bool PlayerIsSafe;
	public ushort MVPs = 0;
	public bool Loaded = false;
	public string ArenaTag = string.Empty;

	//** ? Settings */
	public bool AFK = false;
	public Dictionary<WeaponType, CsItem?> WeaponPreferences = new Dictionary<WeaponType, CsItem?>
	{
		{ WeaponType.Rifle, null },
		{ WeaponType.Sniper, null },
		{ WeaponType.Pistol, null }
	};

	public List<RoundType> RoundPreferences = RoundType.RoundTypes.ToList();

	public ArenaPlayer(Plugin plugin, CCSPlayerController playerController)
	{
		Plugin = plugin;
		Localizer = Plugin.Localizer;
		Config = Plugin.Config;

		Controller = playerController;
		SteamID = playerController.SteamID;
		PlayerIsSafe = false;
	}

	public bool IsValid
		=> Controller?.IsValid == true && Controller.PlayerPawn?.IsValid == true && Controller.Connected == PlayerConnectedState.PlayerConnected;

	public bool IsAlive
		=> Controller.PlayerPawn?.Value?.Health > 0;

	public void SetupWeapons(RoundType roundType)
	{
		if (!this.IsValid || Controller.PlayerPawn.Value == null)
		{
			Plugin.Logger.LogWarning($"Cannot setup weapons for invalid player or null pawn: {Controller.PlayerName}");
			return;
		}

		Controller.RemoveWeapons();

		if (Config.CompatibilitySettings.GiveKnifeByDefault)
			PlayerGiveNamedItem(Controller, CsItem.Knife);

		if (roundType.PrimaryPreference == WeaponType.Unknown) // Warmup or Random round types
		{
			PlayerGiveNamedItem(Controller, WeaponModel.GetRandomWeapon(WeaponType.Unknown));
			PlayerGiveNamedItem(Controller, WeaponModel.GetRandomWeapon(WeaponType.Pistol));
		}
		else
		{
			if (roundType.PrimaryWeapon != null)
			{
				PlayerGiveNamedItem(Controller, (CsItem)roundType.PrimaryWeapon);
			}
			else if (roundType.UsePreferredPrimary && roundType.PrimaryPreference != null && WeaponPreferences != null)
			{
				WeaponType primaryPreferenceType = (WeaponType)roundType.PrimaryPreference;
				CsItem? primaryPreference = WeaponPreferences.GetValueOrDefault(primaryPreferenceType) ?? WeaponModel.GetRandomWeapon(primaryPreferenceType);
				PlayerGiveNamedItem(Controller, (CsItem)primaryPreference);
			}

			if (roundType.SecondaryWeapon != null)
			{
				PlayerGiveNamedItem(Controller, (CsItem)roundType.SecondaryWeapon);
			}
			else if (roundType.UsePreferredSecondary && WeaponPreferences != null)
			{
				CsItem? secondaryPreference = WeaponPreferences.GetValueOrDefault(WeaponType.Pistol) ?? WeaponModel.GetRandomWeapon(WeaponType.Pistol);
				PlayerGiveNamedItem(Controller, (CsItem)secondaryPreference);
			}
		}

		Server.NextWorldUpdate(() =>
		{
			if (Controller.PlayerPawn.Value != null)
			{
				CCSPlayerPawn playerPawn = Controller.PlayerPawn.Value;

				playerPawn.ArmorValue = roundType.Armor ? 100 : 0;
				Utilities.SetStateChanged(playerPawn, "CCSPlayerPawn", "m_ArmorValue");

				if (playerPawn.ItemServices != null)
				{
					CCSPlayer_ItemServices itemService = new CCSPlayer_ItemServices(playerPawn.ItemServices.Handle)
					{
						HasHelmet = roundType.Helmet
					};

					Utilities.SetStateChanged(playerPawn, "CBasePlayerPawn", "m_pItemServices");
				}
				else
				{
					Plugin.Logger.LogWarning($"ItemServices is null for player: {Controller.PlayerName}");
				}
			}
			else
			{
				Plugin.Logger.LogWarning($"PlayerPawn is null for player: {Controller.PlayerName}");
			}
		});
	}

	public void ShowRoundPreferenceMenu()
	{
		if (Plugin.Config.CommandSettings.CenterMenuMode)
		{
			ShowCenterRoundPreferenceMenu();
		}
		else
		{
			ShowChatRoundPreferenceMenu();
		}
	}

	private void ShowChatRoundPreferenceMenu()
	{
		ChatMenu roundPreferenceMenu = new ChatMenu(Localizer["zhw.menu.roundpref.title"]);
		foreach (RoundType roundType in RoundType.RoundTypes)
		{
			bool isRoundTypeEnabled = RoundPreferences.Contains(roundType);
			roundPreferenceMenu.AddMenuOption(isRoundTypeEnabled ? Localizer["zhw.menu.roundpref.item_enabled", Localizer[roundType.Name]] : Localizer["zhw.menu.roundpref.item_disabled", Localizer[roundType.Name]],
				(player, option) =>
				{
					ToggleRoundPreference(roundType);
				}
			);
		}
		MenuManager.OpenChatMenu(Controller, roundPreferenceMenu);
	}

	private void ShowCenterRoundPreferenceMenu()
	{
		var items = new List<MenuItem>();
		var defaultValues = new Dictionary<int, object>();

		for (int i = 0; i < RoundType.RoundTypes.Count; i++)
		{
			RoundType roundType = RoundType.RoundTypes[i];
			bool isRoundTypeEnabled = RoundPreferences.Contains(roundType);
			items.Add(new MenuItem(MenuItemType.Bool, new MenuValue($"{Localizer[roundType.Name]}: ")));
			defaultValues[i] = isRoundTypeEnabled;
		}

		Plugin.Menu?.ShowScrollableMenu(Controller, Localizer["zhw.menu.roundpref.title"], items, (buttons, menu, selected) =>
		{
			if (selected == null) return;
			if (buttons == MenuButtons.Select)
			{
				RoundType roundType = RoundType.RoundTypes[menu.Option];
				bool newValue = selected.Data[0] == 1;
				if (newValue != RoundPreferences.Contains(roundType))
				{
					ToggleRoundPreference(roundType);
				}
			}
		}, false, Config.CommandSettings.FreezeInMenu, 5, defaultValues, Config.CommandSettings.ShowMenuCredits);
	}

	private void ToggleRoundPreference(RoundType roundType)
	{
		bool isRoundTypeEnabled = RoundPreferences.Contains(roundType);
		if (isRoundTypeEnabled)
		{
			if (RoundPreferences.Count == 1)
			{
				Controller.PrintToChat($" {Localizer["zhw.general.prefix"]} {Localizer["zhw.chat.round_preferences_atleastone"]}");
			}
			else
			{
				RoundPreferences.Remove(roundType);
				Controller.PrintToChat($" {Localizer["zhw.general.prefix"]} {Localizer["zhw.chat.round_preferences_removed", Localizer[roundType.Name]]}");
			}
		}
		else
		{
			RoundPreferences.Add(roundType);
			Controller.PrintToChat($" {Localizer["zhw.general.prefix"]} {Localizer["zhw.chat.round_preferences_added", Localizer[roundType.Name]]}");
		}

		if (!Config.CommandSettings.CenterMenuMode)
			ShowRoundPreferenceMenu();
	}

	public void ShowWeaponPreferenceMenu()
	{
		if (Plugin.Config.CommandSettings.CenterMenuMode)
		{
			ShowCenterWeaponPreferenceMenu();
		}
		else
		{
			ShowChatWeaponPreferenceMenu();
		}
	}

	private void ShowChatWeaponPreferenceMenu()
	{
		ChatMenu weaponPreferenceMenu = new ChatMenu(Localizer["zhw.menu.weaponpref.title"]);
		foreach (WeaponType weaponType in Enum.GetValues(typeof(WeaponType)))
		{
			if (weaponType == WeaponType.Unknown)
				continue;
			weaponPreferenceMenu.AddMenuOption(Localizer[$"zhw.rounds.{weaponType.ToString().ToLower()}"],
				(player, option) =>
				{
					ShowWeaponSubPreferenceMenu(weaponType);
				}
			);
		}
		MenuManager.OpenChatMenu(Controller, weaponPreferenceMenu);
	}

	private void ShowCenterWeaponPreferenceMenu()
	{
		var items = new List<MenuItem>();
		foreach (WeaponType weaponType in Enum.GetValues(typeof(WeaponType)))
		{
			if (weaponType == WeaponType.Unknown)
				continue;
			items.Add(new MenuItem(MenuItemType.Button, [new MenuValue($"{Localizer[$"zhw.rounds.{weaponType.ToString().ToLower()}"]}")]));
		}

		Plugin.Menu?.ShowScrollableMenu(Controller, Localizer["zhw.menu.weaponpref.title"], items, (buttons, menu, selected) =>
		{
			if (selected == null) return;
			if (buttons == MenuButtons.Select)
			{
				WeaponType selectedWeaponType = (WeaponType)(menu.Option);
				ShowWeaponSubPreferenceMenu(selectedWeaponType);
			}
		}, false, Config.CommandSettings.FreezeInMenu, disableDeveloper: Config.CommandSettings.ShowMenuCredits);
	}

	public void ShowWeaponSubPreferenceMenu(WeaponType weaponType)
	{
		if (Plugin.Config.CommandSettings.CenterMenuMode)
		{
			ShowCenterWeaponSubPreferenceMenu(weaponType);
		}
		else
		{
			ShowChatWeaponSubPreferenceMenu(weaponType);
		}
	}

	private void ShowChatWeaponSubPreferenceMenu(WeaponType weaponType)
	{
		ChatMenu primaryPreferenceMenu = new ChatMenu(Localizer["zhw.menu.weaponpref.title"]);
		AddWeaponOptions(primaryPreferenceMenu, weaponType);
		MenuManager.OpenChatMenu(Controller, primaryPreferenceMenu);
	}

	private void ShowCenterWeaponSubPreferenceMenu(WeaponType weaponType)
	{
		var items = new List<MenuItem>();
		var defaultValues = new Dictionary<int, object>();

		items.Add(new MenuItem(MenuItemType.Bool, new MenuValue($"{Localizer["zhw.general.random"]}: ")));
		defaultValues[0] = WeaponPreferences[weaponType] == null;

		List<CsItem> possibleItems = WeaponModel.GetWeaponList(weaponType);
		for (int i = 0; i < possibleItems.Count; i++)
		{
			CsItem item = possibleItems[i];
			if (WeaponModel.GetWeaponType(item) != weaponType)
				continue;
			items.Add(new MenuItem(MenuItemType.Bool, new MenuValue($"{Localizer[item.ToString()]}: ")));
			defaultValues[i + 1] = WeaponPreferences[weaponType] == item;
		}

		Plugin.Menu?.ShowScrollableMenu(Controller, Localizer["zhw.menu.weaponpref.title"], items, (buttons, menu, selected) =>
		{
			if (selected == null) return;
			if (buttons == MenuButtons.Select)
			{
				SetWeaponPreference(weaponType, menu.Option == 0 ? null : possibleItems[menu.Option - 1]);

				Plugin.Menu.ClearMenus(Controller);
				ShowCenterWeaponPreferenceMenu();
				ShowCenterWeaponSubPreferenceMenu(weaponType);
			}
		}, true, Config.CommandSettings.FreezeInMenu, 5, defaultValues, Config.CommandSettings.ShowMenuCredits);
	}

	private void AddWeaponOptions(ChatMenu menu, WeaponType weaponType)
	{
		menu.AddMenuOption(WeaponPreferences[weaponType] is null ? Localizer["zhw.menu.weaponpref.item_enabled", Localizer["zhw.general.random"]] : Localizer["zhw.menu.weaponpref.item_disabled", Localizer["zhw.general.random"]],
			(player, option) =>
			{
				SetWeaponPreference(weaponType, null);
			}
		);

		List<CsItem> possibleItems = WeaponModel.GetWeaponList(weaponType);
		foreach (CsItem item in possibleItems)
		{
			if (WeaponModel.GetWeaponType(item) != weaponType)
				continue;
			bool isItemEnabled = WeaponPreferences[weaponType] == item;
			menu.AddMenuOption(isItemEnabled ? Localizer["zhw.menu.weaponpref.item_enabled", Localizer[item.ToString()]] : Localizer["zhw.menu.weaponpref.item_disabled", Localizer[item.ToString()]],
				(player, option) =>
				{
					SetWeaponPreference(weaponType, item);
				}
			);
		}
	}

	private void SetWeaponPreference(WeaponType weaponType, CsItem? item)
	{
		WeaponPreferences[weaponType] = item;
		Controller.PrintToChat($" {Localizer["zhw.general.prefix"]} {Localizer["zhw.chat.weapon_preferences_added", Localizer[item?.ToString() ?? "zhw.general.random"]]}");
	}

	public void PlayerGiveNamedItem(CCSPlayerController player, CsItem item)
	{
		if (!player.PlayerPawn.IsValid || player.PlayerPawn.Value == null || !player.PlayerPawn.Value.IsValid || player.PlayerPawn.Value.ItemServices == null)
			return;

		if (!Plugin.Config.CompatibilitySettings.MetamodSkinchanger || Plugin.GiveNamedItem2 is null)
		{
			player.GiveNamedItem(item);
			return;
		}

		string? itemName = EnumUtils.GetEnumMemberAttributeValue(item);

		if (itemName is null)
			return;

		try
		{
			Plugin.GiveNamedItem2.Invoke(player.PlayerPawn.Value.ItemServices.Handle, itemName, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
		}
		catch (Exception e)
		{
			Plugin.Logger.LogError("Failed to give named item. It is recommended to disable 'metamod-skinchanger-compatibility' on this server. Error: " + e.Message);
			player.GiveNamedItem(item);
		}
	}
}