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

        [SettingPropertyGroupAttribute("1. Kingdom Setup", GroupOrder = 1)]
        [SettingPropertyIntegerAttribute(
            "Original Kingdom Retention %",
            0, 100,
            RequireRestart = false,
            HintText = "Percentage of each vanilla kingdom's clans that stay with it. 0 = take all clans, 100 = leave vanilla kingdoms untouched.")]
        public int OriginalKingdomRetentionPercent
        {
            get => _originalKingdomRetentionPercent;
            set { _originalKingdomRetentionPercent = value; OnPropertyChanged(); }
        }
        private int _originalKingdomRetentionPercent = 40;

        [SettingPropertyGroupAttribute("1. Kingdom Setup", GroupOrder = 1)]
        [SettingPropertyBoolAttribute(
            "Protect Player Clan Fiefs",
            RequireRestart = false,
            HintText = "When on, the player clan's fiefs are never redistributed during fragmentation.")]
        public bool ProtectPlayerClanFiefs
        {
            get => _protectPlayerClanFiefs;
            set { _protectPlayerClanFiefs = value; OnPropertyChanged(); }
        }
        private bool _protectPlayerClanFiefs = true;

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

        [SettingPropertyGroupAttribute("2. Kingdom Appearance", GroupOrder = 2)]
        [SettingPropertyBoolAttribute(
            "Use Preset World Identities",
            RequireRestart = false,
            HintText = "When on, kingdoms/clans/heroes use curated preset catalogs. When off, fallback procedural identity is used.")]
        public bool UsePresetWorld
        {
            get => _usePresetWorld;
            set { _usePresetWorld = value; OnPropertyChanged(); }
        }
        private bool _usePresetWorld = true;

        [SettingPropertyGroupAttribute("2. Kingdom Appearance", GroupOrder = 2)]
        [SettingPropertyBoolAttribute(
            "Preset Strict Culture Matching",
            RequireRestart = false,
            HintText = "When on, preset draws only use matching culture decks. If a culture deck is exhausted, fallback procedural identity is used.")]
        public bool PresetStrictCulture
        {
            get => _presetStrictCulture;
            set { _presetStrictCulture = value; OnPropertyChanged(); }
        }
        private bool _presetStrictCulture = false;

        [SettingPropertyGroupAttribute("2. Kingdom Appearance", GroupOrder = 2)]
        [SettingPropertyIntegerAttribute(
            "Archetype Aggressiveness %",
            50, 200,
            RequireRestart = false,
            HintText = "Scales combat-oriented archetype skills. 100 = baseline preset values.")]
        public int ArchetypeAggressivenessPercent
        {
            get => _archetypeAggressivenessPercent;
            set { _archetypeAggressivenessPercent = value; OnPropertyChanged(); }
        }
        private int _archetypeAggressivenessPercent = 120;

        // ==================================================================
        // GROUP 3 — Clan & Fief Distribution
        // ==================================================================

        [SettingPropertyGroupAttribute("3. Clan & Fief Distribution", GroupOrder = 3)]
        [SettingPropertyBoolAttribute(
            "Culture-Aware Clan Assignment",
            RequireRestart = false,
            HintText = "Prefer assigning clans to kingdoms of matching culture before filling remaining slots.")]
        public bool CultureAwareClanAssignment
        {
            get => _cultureAwareClanAssignment;
            set { _cultureAwareClanAssignment = value; OnPropertyChanged(); }
        }
        private bool _cultureAwareClanAssignment = true;

        [SettingPropertyGroupAttribute("3. Clan & Fief Distribution", GroupOrder = 3)]
        [SettingPropertyBoolAttribute(
            "Geographic Fief Distribution",
            RequireRestart = false,
            HintText = "Assign fiefs based on proximity to each kingdom's capital. When off, fiefs are distributed round-robin.")]
        public bool GeographicFiefDistribution
        {
            get => _geographicFiefDistribution;
            set { _geographicFiefDistribution = value; OnPropertyChanged(); }
        }
        private bool _geographicFiefDistribution = true;

        [SettingPropertyGroupAttribute("3. Clan & Fief Distribution", GroupOrder = 3)]
        [SettingPropertyBoolAttribute(
            "Distribute Fiefs to Vassals",
            RequireRestart = false,
            HintText = "Spread fiefs among vassal clans, not just the ruling clan.")]
        public bool DistributeFiefsToVassals
        {
            get => _distributeFiefsToVassals;
            set { _distributeFiefsToVassals = value; OnPropertyChanged(); }
        }
        private bool _distributeFiefsToVassals = true;

        [SettingPropertyGroupAttribute("3. Clan & Fief Distribution", GroupOrder = 3)]
        [SettingPropertyIntegerAttribute(
            "Min Fiefs per Original Kingdom",
            0, 20,
            RequireRestart = false,
            HintText = "Minimum number of fiefs each vanilla kingdom keeps regardless of retention %.")]
        public int MinFiefsPerOriginalKingdom
        {
            get => _minFiefsPerOriginalKingdom;
            set { _minFiefsPerOriginalKingdom = value; OnPropertyChanged(); }
        }
        private int _minFiefsPerOriginalKingdom = 3;

        // ==================================================================
        // GROUP 4 — Diplomacy & Relations
        // ==================================================================

        [SettingPropertyGroupAttribute("4. Diplomacy & Relations", GroupOrder = 4)]
        [SettingPropertyIntegerAttribute(
            "Initial Relations Bonus",
            0, 100,
            RequireRestart = false,
            HintText = "Starting relation bonus between kingdoms of the same culture. 0 = no bonus.")]
        public int InitialRelationsBonus
        {
            get => _initialRelationsBonus;
            set { _initialRelationsBonus = value; OnPropertyChanged(); }
        }
        private int _initialRelationsBonus = 20;

        [SettingPropertyGroupAttribute("4. Diplomacy & Relations", GroupOrder = 4)]
        [SettingPropertyBoolAttribute(
            "Truce Includes Vanilla Kingdoms",
            RequireRestart = false,
            HintText = "When on, the starting truce also prevents wars between new and vanilla kingdoms.")]
        public bool TruceIncludesVanillaKingdoms
        {
            get => _truceIncludesVanillaKingdoms;
            set { _truceIncludesVanillaKingdoms = value; OnPropertyChanged(); }
        }
        private bool _truceIncludesVanillaKingdoms = false;

        // ==================================================================
        // GROUP 5 — Stability & Debug
        // ==================================================================

        [SettingPropertyGroupAttribute("5. Stability & Debug", GroupOrder = 5)]
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

        [SettingPropertyGroupAttribute("5. Stability & Debug", GroupOrder = 5)]
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

        [SettingPropertyGroupAttribute("5. Stability & Debug", GroupOrder = 5)]
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

        [SettingPropertyGroupAttribute("5. Stability & Debug", GroupOrder = 5)]
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

        [SettingPropertyGroupAttribute("5. Stability & Debug", GroupOrder = 5)]
        [SettingPropertyBoolAttribute(
            "Preset Run Report",
            RequireRestart = false,
            HintText = "Logs selected kingdom/clan/hero presets each run into the mod log.")]
        public bool PresetDebugReport
        {
            get => _presetDebugReport;
            set { _presetDebugReport = value; OnPropertyChanged(); }
        }
        private bool _presetDebugReport = true;

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
