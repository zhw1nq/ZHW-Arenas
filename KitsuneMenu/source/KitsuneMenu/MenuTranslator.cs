using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using Menu.Enums;

namespace Menu
{
	public class MenuTranslator
	{
		// jsonc for texts in the menu
		private const string TRANSLATION_FILE = "menu_translations.jsonc";
		private string _translationPath = string.Empty;
		public static readonly Dictionary<string, string> MenuTranslations = [];

		private static readonly JsonSerializerOptions _jsonOptions = new()
		{
			WriteIndented = true,
			ReadCommentHandling = JsonCommentHandling.Skip,
			AllowTrailingCommas = true,
			PropertyNameCaseInsensitive = true,
		};

		public MenuTranslator() { }

		public void Initialize()
		{
			_translationPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, TRANSLATION_FILE);
			LoadTranslations();
		}

		private void LoadTranslations()
		{
			if (!File.Exists(_translationPath))
			{
				CreateDefaultTranslations();
				return;
			}

			try
			{
				var json = File.ReadAllText(_translationPath);
				var translations = JsonSerializer.Deserialize<Dictionary<string, string>>(json, _jsonOptions);

				if (translations == null)
				{
					Console.ForegroundColor = ConsoleColor.Red;
					Console.WriteLine($"Failed to parse menu translations file: {_translationPath}");
					Console.ResetColor();
					return;
				}

				MenuTranslations.Clear();

				foreach (var translation in translations)
				{
					MenuTranslations[translation.Key] = translation.Value;
				}
			}
			catch (Exception ex)
			{
				Console.ForegroundColor = ConsoleColor.Red;
				Console.WriteLine($"Failed to load menu translations: {ex.Message}");
				Console.ResetColor();
			}
		}

		private void CreateDefaultTranslations()
		{
			var translations = new Dictionary<string, string>
			{
				{ "EmptyMenu", "The menu is empty." },
				{ "FooterMain", "Move: <font color=\"#f5a142\">WASD <font color=\"#FFFFFF\">| <font color=\"#ff3333\">Select: <font color=\"#f5a142\">Jump <font color=\"#FFFFFF\">| <font color=\"#ff3333\">Exit: <font color=\"#f5a142\">TAB" },
				{ "FooterSubMenu", "Move: <font color=\"#f5a142\">WASD <font color=\"#FFFFFF\">| <font color=\"#ff3333\">Select: <font color=\"#f5a142\">Jump <font color=\"#FFFFFF\">| <font color=\"#ff3333\">Back: <font color=\"#f5a142\">Sprint <font color=\"#FFFFFF\">| <font color=\"#ff3333\">Exit: <font color=\"#f5a142\">TAB" },
				{ "Items", "Item:" }
			};

			var jsonOptions = new JsonSerializerOptions(_jsonOptions)
			{
				Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
			};

			var json = JsonSerializer.Serialize(translations, jsonOptions);
			File.WriteAllText(_translationPath, json);
			LoadTranslations();
		}

		public string GetTranslation(string key)
		{
			if (MenuTranslations.TryGetValue(key, out var translation))
				return translation;

			return key;
		}
	}
}