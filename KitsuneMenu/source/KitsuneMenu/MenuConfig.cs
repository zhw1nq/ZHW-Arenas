using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using Menu.Enums;

namespace Menu
{
	public class MenuConfiguration
	{
		// Menu action constants
		public const ulong BUTTON_NONE = 0UL;
		public const ulong BUTTON_SELECT = (ulong)PlayerButtons.Jump;
		public const ulong BUTTON_BACK = (ulong)PlayerButtons.Speed;
		public const ulong BUTTON_UP = (ulong)PlayerButtons.Forward;
		public const ulong BUTTON_DOWN = (ulong)PlayerButtons.Back;
		public const ulong BUTTON_LEFT = (ulong)PlayerButtons.Moveleft;
		public const ulong BUTTON_RIGHT = (ulong)PlayerButtons.Moveright;
		public const ulong BUTTON_EXIT = 1UL << 33;  // Custom Scoreboard value
		public const ulong BUTTON_INPUT = 1UL << 63; // Special input mode

		private const string CONFIG_FILE = "menu_config.jsonc";
		private string _configPath = string.Empty;
		private static readonly JsonSerializerOptions _jsonOptions = new()
		{
			WriteIndented = true,
			ReadCommentHandling = JsonCommentHandling.Skip,
			AllowTrailingCommas = true,
			PropertyNameCaseInsensitive = true,
		};

		// Button names in config
		public string Select { get; set; } = "Jump";
		public string Back { get; set; } = "Speed";
		public string Up { get; set; } = "Forward";
		public string Down { get; set; } = "Back";
		public string Left { get; set; } = "Moveleft";
		public string Right { get; set; } = "Moveright";
		public string Exit { get; set; } = "Scoreboard";

		// Cached button values
		private ulong _selectButton;
		private ulong _backButton;
		private ulong _upButton;
		private ulong _downButton;
		private ulong _leftButton;
		private ulong _rightButton;
		private ulong _exitButton;

		public MenuConfiguration() { }

		public void Initialize()
		{
			_configPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, CONFIG_FILE);
			LoadConfig();
			ParseButtons();
		}

		private void LoadConfig()
		{
			if (!File.Exists(_configPath))
			{
				CreateDefaultConfig();
				return;
			}

			try
			{
				var jsonContent = File.ReadAllText(_configPath);
				var config = JsonSerializer.Deserialize<MenuConfiguration>(jsonContent, _jsonOptions);

				if (config != null)
				{
					Select = config.Select;
					Back = config.Back;
					Up = config.Up;
					Down = config.Down;
					Left = config.Left;
					Right = config.Right;
					Exit = config.Exit;
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error loading menu configuration: {ex.Message}");
				CreateDefaultConfig();
			}
		}

		private void ParseButtons()
		{
			_selectButton = ParseButtonByName(Select);
			_backButton = ParseButtonByName(Back);
			_upButton = ParseButtonByName(Up);
			_downButton = ParseButtonByName(Down);
			_leftButton = ParseButtonByName(Left);
			_rightButton = ParseButtonByName(Right);
			_exitButton = ParseButtonByName(Exit);
		}

		private static ulong ParseButtonByName(string buttonName)
		{
			if (buttonName == "Scoreboard")
				return 1UL << 33;

			if (Enum.TryParse<PlayerButtons>(buttonName, true, out var button))
			{
				return (ulong)button;
			}

			Console.ForegroundColor = ConsoleColor.Yellow;
			Console.WriteLine($"Warning: Invalid button name '{buttonName}', falling back to default");
			Console.ResetColor();
			return BUTTON_NONE;
		}

		private void CreateDefaultConfig()
		{
			var configContent = @"{
    /* Available buttons:
        Attack      - Primary attack button
        Jump        - Jump
        Duck        - Crouch
        Forward     - Move forward
        Back        - Move backward
        Use         - Use key
        Cancel      - Cancel action
        Left        - Turn left
        Right       - Turn right
        Moveleft    - Strafe left
        Moveright   - Strafe right
        Attack2     - Secondary attack
        Run         - Run
        Reload      - Reload weapon
        Alt1        - Alternative button 1
        Alt2        - Alternative button 2
        Speed       - Sprint/Fast movement
        Walk        - Walk
        Zoom        - Zoom view
        Weapon1     - Primary weapon
        Weapon2     - Secondary weapon
        Bullrush    - Bullrush
        Grenade1    - First grenade
        Grenade2    - Second grenade
        Attack3     - Third attack
        Scoreboard  - Show scoreboard (TAB)
    */

	// !!! To apply changes, restart the server as CSS only reload shared things when restarted !!!

    // Button to select menu items
    ""Select"": ""Jump"",

    // Button to go back in submenus
    ""Back"": ""Speed"",

    // Navigation buttons
    ""Up"": ""Forward"",
    ""Down"": ""Back"",
    ""Left"": ""Moveleft"",
    ""Right"": ""Moveright"",

    // Exit menu button
    ""Exit"": ""Scoreboard""
}";

			try
			{
				File.WriteAllText(_configPath, configContent);
				LoadConfig();
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error creating default configuration: {ex.Message}");
			}
		}

		public void SaveConfig()
		{
			try
			{
				var jsonContent = JsonSerializer.Serialize(this, _jsonOptions);
				File.WriteAllText(_configPath, jsonContent);
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error saving menu configuration: {ex.Message}");
			}
		}

		// Getter methods for buttons
		public MenuButtons GetSelectButton() => (MenuButtons)_selectButton;
		public MenuButtons GetBackButton() => (MenuButtons)_backButton;
		public MenuButtons GetUpButton() => (MenuButtons)_upButton;
		public MenuButtons GetDownButton() => (MenuButtons)_downButton;
		public MenuButtons GetLeftButton() => (MenuButtons)_leftButton;
		public MenuButtons GetRightButton() => (MenuButtons)_rightButton;
		public MenuButtons GetExitButton() => (MenuButtons)_exitButton;
		public MenuButtons GetInputButton() => (MenuButtons)BUTTON_INPUT;

		public MenuButtons GetButtonValue(MenuButtons button)
		{
			if ((ulong)button == _selectButton)
				return MenuButtons.Select;
			if ((ulong)button == _backButton)
				return MenuButtons.Back;
			if ((ulong)button == _upButton)
				return MenuButtons.Up;
			if ((ulong)button == _downButton)
				return MenuButtons.Down;
			if ((ulong)button == _leftButton)
				return MenuButtons.Left;
			if ((ulong)button == _rightButton)
				return MenuButtons.Right;
			if ((ulong)button == _exitButton)
				return MenuButtons.Exit;
			if ((ulong)button == BUTTON_INPUT)
				return MenuButtons.Input;
			return MenuButtons.None;
		}
	}
}