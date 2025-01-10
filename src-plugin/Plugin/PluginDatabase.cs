
using System.Data;
using CounterStrikeSharp.API.Core;
using ZHWArenas.Models;
using MySqlConnector;
using Dapper;
using CounterStrikeSharp.API.Modules.Entities.Constants;
using Microsoft.Extensions.Logging;
using CounterStrikeSharp.API;

namespace ZHWArenas;

public sealed partial class Plugin : BasePlugin
{
	public MySqlConnection CreateConnection(PluginConfig config)
	{
		DatabaseSettings _settings = config.DatabaseSettings;

		MySqlConnectionStringBuilder builder = new MySqlConnectionStringBuilder
		{
			Server = _settings.Host,
			UserID = _settings.Username,
			Password = _settings.Password,
			Database = _settings.Database,
			Port = (uint)_settings.Port,
			SslMode = Enum.Parse<MySqlSslMode>(_settings.Sslmode, true),
		};

		return new MySqlConnection(builder.ToString());
	}

	public async Task CreateTableAsync()
	{
		string tablePrefix = Config.DatabaseSettings.TablePrefix;
		string tableQuery = @$"CREATE TABLE IF NOT EXISTS `{tablePrefix}zhw-arenas` (
			`steamid64` BIGINT UNIQUE,
			`rifle` INT,
			`sniper` INT,
			`pistol` INT,
			`rounds` VARCHAR(256) NOT NULL,
			`lastseen` TIMESTAMP NOT NULL
		);";

		using MySqlConnection connection = CreateConnection(Config);
		await connection.OpenAsync();

		await connection.ExecuteAsync(tableQuery);
	}

	public async Task LoadPlayerAsync(ulong SteamID)
	{
		string tablePrefix = Config.DatabaseSettings.TablePrefix;

		DefaultWeaponSettings dws = Config.DefaultWeaponSettings;

		string sqlInsertOrUpdate = $@"
			INSERT INTO `{tablePrefix}zhw-arenas` (`steamid64`, `lastseen`, `rifle`, `sniper`, `pistol`, `rounds`)
			VALUES (@SteamID, CURRENT_TIMESTAMP, @DefaultRifle, @DefaultSniper, @DefaultPistol, @Rounds)
			ON DUPLICATE KEY UPDATE `lastseen` = CURRENT_TIMESTAMP;";

		string sqlSelect = $@"
				SELECT `rifle`, `sniper`, `pistol`, `rounds`
				FROM `{tablePrefix}zhw-arenas` WHERE `steamid64` = @SteamID;";

		using MySqlConnection connection = CreateConnection(Config);
		await connection.OpenAsync();

		string rounds = string.Join(",", RoundType.RoundTypes.Where(r => r.EnabledByDefault).Select(x => x.ID.ToString()));
		await connection.ExecuteAsync(sqlInsertOrUpdate, new
		{
			SteamID,
			Rounds = rounds,
			DefaultRifle = FindEnumValueByEnumMemberValue(dws.DefaultRifle),
			DefaultSniper = FindEnumValueByEnumMemberValue(dws.DefaultSniper),
			DefaultPistol = FindEnumValueByEnumMemberValue(dws.DefaultPistol)
		});

		dynamic? result = await connection.QuerySingleOrDefaultAsync<dynamic>(sqlSelect, new { SteamID });
		if (result != null)
		{
			ArenaPlayer? arenaPlayer = Arenas?.FindPlayer(SteamID);

			if (arenaPlayer == null)
				return;

			arenaPlayer.WeaponPreferences = new Dictionary<WeaponType, CsItem?>
			{
				{ WeaponType.Rifle, (CsItem?)result.rifle },
				{ WeaponType.Sniper, (CsItem?)result.sniper },
				{ WeaponType.Pistol, (CsItem?)result.pistol }
			};

			if (!string.IsNullOrEmpty(result.rounds))
			{
				List<int> validRoundIds = new List<int>();
				string[] roundIds = result.rounds.Split(',');
				List<RoundType> roundPreferences = new List<RoundType>();

				foreach (string roundId in roundIds)
				{
					if (int.TryParse(roundId, out int id))
					{
						RoundType? roundType = RoundType.RoundTypes.FirstOrDefault(x => x.ID == id);
						if (roundType != null)
						{
							roundPreferences.Add((RoundType)roundType);
							validRoundIds.Add(id);
						}
					}
				}

				if (validRoundIds.Count != roundIds.Length)
				{
					string validRounds = string.Join(",", validRoundIds);
					string sqlUpdateRounds = $@"UPDATE `{tablePrefix}zhw-arenas`
						SET `rounds` = @ValidRounds
						WHERE `steamid64` = @SteamID;";

					await connection.ExecuteAsync(sqlUpdateRounds, new { SteamID, ValidRounds = validRounds });
				}

				arenaPlayer.RoundPreferences = roundPreferences;

				arenaPlayer.Loaded = true;
			}
		}
	}

	public async Task SavePlayerPreferencesAsync(List<ArenaPlayer> players)
	{
		using MySqlConnection connection = CreateConnection(Config);
		await connection.OpenAsync();
		using var transaction = await connection.BeginTransactionAsync();

		try
		{
			string sqlUpdate = $@"
            UPDATE `{Config.DatabaseSettings.TablePrefix}zhw-arenas`
            SET `rifle` = @Rifle, `sniper` = @Sniper, `pistol` = @Pistol, `rounds` = @Rounds
            WHERE `steamid64` = @SteamId;";

			foreach (ArenaPlayer player in players)
			{
				var weaponParameters = new
				{
					SteamId = player.SteamID,
					Rifle = player.WeaponPreferences.TryGetValue(WeaponType.Rifle, out CsItem? rifle) ? rifle : null,
					Sniper = player.WeaponPreferences.TryGetValue(WeaponType.Sniper, out CsItem? sniper) ? sniper : null,
					Pistol = player.WeaponPreferences.TryGetValue(WeaponType.Pistol, out CsItem? pistol) ? pistol : null,
					Rounds = string.Join(",", player.RoundPreferences.Select(r => r.ID))
				};

				await connection.ExecuteAsync(sqlUpdate, weaponParameters, transaction: transaction);
			}

			await transaction.CommitAsync();
		}
		catch (Exception ex)
		{
			await transaction.RollbackAsync();
			Server.NextWorldUpdate(() => Logger.LogError("Failed to save player preferences: {0}", ex.Message));
			throw;
		}
	}

	public async Task PurgeDatabaseAsync()
	{
		if (Config.DatabaseSettings.TablePurgeDays <= 0)
			return;

		string query = $@"
        DELETE FROM `{Config.DatabaseSettings.TablePrefix}zhw-arenas`
        WHERE `lastseen` < DATE_SUB(NOW(), INTERVAL @PurgeDays DAY);";

		using MySqlConnection connection = CreateConnection(Config);
		await connection.OpenAsync();

		await connection.ExecuteAsync(query, new { PurgeDays = Config.DatabaseSettings.TablePurgeDays });
	}

	public bool IsDatabaseConfigDefault(PluginConfig config)
	{
		DatabaseSettings _settings = config.DatabaseSettings;
		return _settings.Host == "localhost" &&
			_settings.Username == "root" &&
			_settings.Database == "database" &&
			_settings.Password == "password" &&
			_settings.Port == 3306 &&
			_settings.Sslmode == "none" &&
			_settings.TablePrefix == "" &&
			_settings.TablePurgeDays == 30;
	}

	private string GetColumnName(WeaponType weaponType)
	{
		switch (weaponType)
		{
			case WeaponType.Rifle:
				return "rifle";
			case WeaponType.Sniper:
				return "sniper";
			case WeaponType.Pistol:
				return "pistol";
			default:
				throw new ArgumentException("Invalid weapon type");
		}
	}
}