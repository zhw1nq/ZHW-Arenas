using CounterStrikeSharp.API.Modules.Entities.Constants;

namespace ZHWArenas.Models;

public enum WeaponType
{
	Rifle,
	Sniper,
	Pistol,
	Unknown
}

public struct WeaponModel
{
	public static List<CsItem> rifleItems = new List<CsItem>()
	{
		CsItem.AWP,
		CsItem.M4A1S,
		CsItem.M4A1,
	};

	public static List<CsItem> sniperItems = new List<CsItem>()
	{
		CsItem.AWP,
		CsItem.SSG08,
	};

	public static List<CsItem> pistolItems = new List<CsItem>()
	{
		CsItem.Deagle,
		CsItem.USPS,
		CsItem.Revolver
	};

	public static List<CsItem> GetWeaponList(WeaponType type)
	{
		List<CsItem> allWeapons = GetAllPrimaryWeapons();

		switch (type)
		{
			case WeaponType.Rifle:
				return rifleItems;
			case WeaponType.Sniper:
				return sniperItems;
			case WeaponType.Pistol:
				return pistolItems;
			default:
				return allWeapons;
		}
	}

	public static List<CsItem> GetAllPrimaryWeapons()
	{
		List<CsItem> allPrimaryWeapons =
		[
			.. rifleItems,
			.. sniperItems,
		];
		return allPrimaryWeapons;
	}

	public static CsItem GetRandomWeapon(WeaponType type)
	{
		List<CsItem> possibleItems = GetWeaponList(type);
		return possibleItems[Plugin.rng.Next(0, possibleItems.Count)];
	}

	public static WeaponType GetWeaponType(CsItem? weapon)
	{
		if (weapon is null)
			return WeaponType.Unknown;

		if (rifleItems.Contains(weapon.Value))
			return WeaponType.Rifle;
		if (sniperItems.Contains(weapon.Value))
			return WeaponType.Sniper;
		if (pistolItems.Contains(weapon.Value))
			return WeaponType.Pistol;

		return WeaponType.Unknown;
	}
}