using System;
using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using MCM.Abstractions.Base.Global;
using MCM.Common;

namespace KingdomFragmentation.Settings
{
    public enum KingdomNamingModeOption
    {
        ClanBased,
        SettlementBased,
        CultureBased
    }

    public enum BannerColorModeOption
    {
        UniquePerKingdom,
        CultureBased
    }

    public sealed class KingdomFragmentationSettings
        : AttributeGlobalSettings<KingdomFragmentationSettings>
    {
        public override string Id          => "KingdomFragmentation_v2";
        public override string DisplayName => "Kingdom Fragmentation";
        public override string FolderName  => "KingdomFragmentation";
        public override string FormatType  => "json";

        // ==================================================================
        // GROUP 1 — Kingdom Setup
        // ==================================================================

        [SettingPropertyGroupAttribute("1. Kingdom Setup", GroupOrder = 1)]
        [SettingPropertyBoolAttribute(
            "Enable Kingdom Fragmentation",
            RequireRestart = false,
            HintText = "Master switch. When off the mod does nothing.")]
        public bool EnableFragmentation
        {
            get => _enableFragmentation;
            set { _enableFragmentation = value; OnPropertyChanged(); }
        }
        private bool _enableFragmentation = true;

        [SettingPropertyGroupAttribute("1. Kingdom Setup", GroupOrder = 1)]
        [SettingPropertyIntegerAttribute(
            "Number of Kingdoms",
            1, 50,
            RequireRestart = false,
            HintText = "How many kingdoms the map should have. Limited by available fiefs.")]
        public int DesiredKingdomCount
        {
            get => _desiredKingdomCount;
            set { _desiredKingdomCount = value; OnPropertyChanged(); }
        }
        private int _desiredKingdomCount = 10;

        [SettingPropertyGroupAttribute("1. Kingdom Setup", GroupOrder = 1)]
        [SettingPropertyIntegerAttribute(
            "Fiefs per Kingdom",
            0, 50,
            RequireRestart = false,
            HintText = "Towns and castles per kingdom. 0 = distribute all fiefs evenly.")]
        public int FiefsPerKingdom
        {
            get => _fiefsPerKingdom;
            set { _fiefsPerKingdom = value; OnPropertyChanged(); }
        }
        private int _fiefsPerKingdom = 5;

        [SettingPropertyGroupAttribute("1. Kingdom Setup", GroupOrder = 1)]
        [SettingPropertyIntegerAttribute(
            "Clans per Kingdom",
            1, 20,
            RequireRestart = false,
            HintText = "Number of clans in each kingdom. Extra clans are generated if needed.")]
        public int ClansPerKingdom
        {
            get => _clansPerKingdom;
            set { _clansPerKingdom = value; OnPropertyChanged(); }
        }
        private int _clansPerKingdom = 3;

        [SettingPropertyGroupAttribute("1. Kingdom Setup", GroupOrder = 1)]
        [SettingPropertyIntegerAttribute(
            "Heroes per Clan",
            1, 30,
            RequireRestart = false,
            HintText = "Target heroes in each clan. Extra heroes are generated if needed.")]
        public int HeroesPerClan
        {
            get => _heroesPerClan;
            set { _heroesPerClan = value; OnPropertyChanged(); }
        }
        private int _heroesPerClan = 5;

        // ==================================================================
        // GROUP 2 — Kingdom Appearance
        // ==================================================================

        [SettingPropertyGroupAttribute("2. Kingdom Appearance", GroupOrder = 2)]
        [SettingPropertyDropdownAttribute(
            "Kingdom Naming Style",
            RequireRestart = false,
            HintText = "ClanBased = named after the ruling clan; SettlementBased = named after the capital; CultureBased = named after the culture.")]
        public Dropdown<string> KingdomNamingModeDropdown
        {
            get => _kingdomNamingModeDropdown;
            set { _kingdomNamingModeDropdown = value; OnPropertyChanged(); }
        }
        private Dropdown<string> _kingdomNamingModeDropdown = new Dropdown<string>(
            new string[] { "ClanBased", "SettlementBased", "CultureBased" }, 0);

        public KingdomNamingModeOption KingdomNamingMode =>
            ParseEnum<KingdomNamingModeOption>(KingdomNamingModeDropdown?.SelectedValue, KingdomNamingModeOption.ClanBased);

        [SettingPropertyGroupAttribute("2. Kingdom Appearance", GroupOrder = 2)]
        [SettingPropertyTextAttribute(
            "Kingdom Name Prefix",
            RequireRestart = false,
            HintText = "Text placed before the kingdom name, e.g. 'Kingdom of '.")]
        public string KingdomNamePrefix
        {
            get => _kingdomNamePrefix;
            set { _kingdomNamePrefix = value; OnPropertyChanged(); }
        }
        private string _kingdomNamePrefix = "Kingdom of ";

        [SettingPropertyGroupAttribute("2. Kingdom Appearance", GroupOrder = 2)]
        [SettingPropertyDropdownAttribute(
            "Kingdom Color Mode",
            RequireRestart = false,
            HintText = "UniquePerKingdom = distinct random colors; CultureBased = colors match culture.")]
        public Dropdown<string> BannerColorModeDropdown
        {
            get => _bannerColorModeDropdown;
            set { _bannerColorModeDropdown = value; OnPropertyChanged(); }
        }
        private Dropdown<string> _bannerColorModeDropdown = new Dropdown<string>(
            new string[] { "UniquePerKingdom", "CultureBased" }, 0);

        public BannerColorModeOption BannerColorMode =>
            ParseEnum<BannerColorModeOption>(BannerColorModeDropdown?.SelectedValue, BannerColorModeOption.UniquePerKingdom);

        // ==================================================================
        // GROUP 3 — Stability & Debug
        // ==================================================================

        [SettingPropertyGroupAttribute("3. Stability & Debug", GroupOrder = 3)]
        [SettingPropertyIntegerAttribute(
            "Starting Truce Days",
            0, 365,
            RequireRestart = false,
            HintText = "Days of enforced peace between all new kingdoms. 0 = no truce.")]
        public int StartingTruceDays
        {
            get => _startingTruceDays;
            set { _startingTruceDays = value; OnPropertyChanged(); }
        }
        private int _startingTruceDays = 30;

        [SettingPropertyGroupAttribute("3. Stability & Debug", GroupOrder = 3)]
        [SettingPropertyIntegerAttribute(
            "Defection Lockout Days",
            0, 365,
            RequireRestart = false,
            HintText = "Days where clans cannot leave their assigned kingdom.")]
        public int DefectionLockoutDays
        {
            get => _defectionLockoutDays;
            set { _defectionLockoutDays = value; OnPropertyChanged(); }
        }
        private int _defectionLockoutDays = 60;

        [SettingPropertyGroupAttribute("3. Stability & Debug", GroupOrder = 3)]
        [SettingPropertyBoolAttribute(
            "New Campaigns Only",
            RequireRestart = false,
            HintText = "When on, fragmentation only runs on new campaigns, not loaded saves.")]
        public bool NewCampaignsOnly
        {
            get => _newCampaignsOnly;
            set { _newCampaignsOnly = value; OnPropertyChanged(); }
        }
        private bool _newCampaignsOnly = true;

        [SettingPropertyGroupAttribute("3. Stability & Debug", GroupOrder = 3)]
        [SettingPropertyBoolAttribute(
            "Debug Logging",
            RequireRestart = false,
            HintText = "Enables detailed debug messages in the log file.")]
        public bool DebugLogging
        {
            get => _debugLogging;
            set { _debugLogging = value; OnPropertyChanged(); }
        }
        private bool _debugLogging = false;

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        private static T ParseEnum<T>(string value, T defaultValue) where T : struct
        {
            if (string.IsNullOrEmpty(value)) return defaultValue;
            return Enum.TryParse<T>(value, out var result) ? result : defaultValue;
        }
    }
}
