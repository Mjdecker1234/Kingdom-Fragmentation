using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using MCM.Abstractions.Base.Global;

namespace KingdomFragmentation.Settings
{
    /// <summary>
    /// All user-facing MCM settings for the Kingdom Fragmentation mod.
    /// Groups mirror the problem-statement categories.
    /// </summary>
    public sealed class KingdomFragmentationSettings
        : AttributeGlobalSettings<KingdomFragmentationSettings>
    {
        // ------------------------------------------------------------------
        // MCM identity
        // ------------------------------------------------------------------
        public override string Id          => "KingdomFragmentation_v1";
        public override string DisplayName => "Kingdom Fragmentation";
        public override string FolderName  => "KingdomFragmentation";
        public override string FormatType  => "json";

        // ==================================================================
        // GROUP 1 — Fragmentation Scope
        // ==================================================================

        [SettingsPropertyGroupAttribute("1. Fragmentation Scope", GroupOrder = 1)]
        [SettingsPropertyBoolAttribute(
            "Enable Kingdom Fragmentation",
            RequireRestart = false,
            HintText = "Master switch. When off, the mod does nothing on campaign start.")]
        public bool EnableFragmentation
        {
            get => _enableFragmentation;
            set { _enableFragmentation = value; OnPropertyChanged(); }
        }
        private bool _enableFragmentation = true;

        [SettingsPropertyGroupAttribute("1. Fragmentation Scope", GroupOrder = 1)]
        [SettingsPropertyBoolAttribute(
            "Major Noble Clans Only",
            RequireRestart = false,
            HintText = "Only split clans that meet the minimum tier requirement. "
                     + "If off, all eligible clans are considered.")]
        public bool MajorNobleClanOnly
        {
            get => _majorNobleClanOnly;
            set { _majorNobleClanOnly = value; OnPropertyChanged(); }
        }
        private bool _majorNobleClanOnly = true;

        [SettingsPropertyGroupAttribute("1. Fragmentation Scope", GroupOrder = 1)]
        [SettingsPropertyBoolAttribute(
            "Include Ruling Clans",
            RequireRestart = false,
            HintText = "When enabled, the ruling clan of each original kingdom is also "
                     + "split into its own kingdom.")]
        public bool IncludeRulingClans
        {
            get => _includeRulingClans;
            set { _includeRulingClans = value; OnPropertyChanged(); }
        }
        private bool _includeRulingClans = true;

        [SettingsPropertyGroupAttribute("1. Fragmentation Scope", GroupOrder = 1)]
        [SettingsPropertyBoolAttribute(
            "Include Minor Factions",
            RequireRestart = false,
            HintText = "Allows minor-faction clans to become independent kingdoms. "
                     + "Can destabilise the map if enabled.")]
        public bool IncludeMinorFactions
        {
            get => _includeMinorFactions;
            set { _includeMinorFactions = value; OnPropertyChanged(); }
        }
        private bool _includeMinorFactions = false;

        [SettingsPropertyGroupAttribute("1. Fragmentation Scope", GroupOrder = 1)]
        [SettingsPropertyBoolAttribute(
            "Include Mercenary Clans",
            RequireRestart = false,
            HintText = "Allows mercenary clans to form independent kingdoms.")]
        public bool IncludeMercenaries
        {
            get => _includeMercenaries;
            set { _includeMercenaries = value; OnPropertyChanged(); }
        }
        private bool _includeMercenaries = false;

        [SettingsPropertyGroupAttribute("1. Fragmentation Scope", GroupOrder = 1)]
        [SettingsPropertyBoolAttribute(
            "Include Rebel Clans",
            RequireRestart = false,
            HintText = "Allows rebel clans to form independent kingdoms.")]
        public bool IncludeRebelClans
        {
            get => _includeRebelClans;
            set { _includeRebelClans = value; OnPropertyChanged(); }
        }
        private bool _includeRebelClans = false;

        [SettingsPropertyGroupAttribute("1. Fragmentation Scope", GroupOrder = 1)]
        [SettingsPropertyIntegerAttribute(
            "Minimum Clan Tier",
            1, 6,
            RequireRestart = false,
            HintText = "Clans below this tier are skipped. Default 1 includes almost every noble clan.")]
        public int MinClanTier
        {
            get => _minClanTier;
            set { _minClanTier = value; OnPropertyChanged(); }
        }
        private int _minClanTier = 1;

        // ==================================================================
        // GROUP 2 — New Kingdom Creation Rules
        // ==================================================================

        [SettingsPropertyGroupAttribute("2. Kingdom Creation", GroupOrder = 2)]
        [SettingsPropertyIntegerAttribute(
            "Target Number of Kingdoms",
            0, 200,
            RequireRestart = false,
            HintText = "How many kingdoms to generate. 0 = one kingdom per eligible clan (default). "
                     + "When > 0, the best N clans become kingdom leaders and remaining clans are distributed among them. "
                     + "Takes precedence over 'One Clan = One Kingdom' when set.")]
        public int TargetKingdomCount
        {
            get => _targetKingdomCount;
            set { _targetKingdomCount = value; OnPropertyChanged(); }
        }
        private int _targetKingdomCount = 0;

        [SettingsPropertyGroupAttribute("2. Kingdom Creation", GroupOrder = 2)]
        [SettingsPropertyBoolAttribute(
            "One Clan = One Kingdom",
            RequireRestart = false,
            HintText = "Each eligible clan becomes its own independent kingdom. "
                     + "Disable to use the one-settlement mode instead.")]
        public bool OneClanOneKingdom
        {
            get => _oneClanOneKingdom;
            set { _oneClanOneKingdom = value; OnPropertyChanged(); }
        }
        private bool _oneClanOneKingdom = true;

        [SettingsPropertyGroupAttribute("2. Kingdom Creation", GroupOrder = 2)]
        [SettingsPropertyBoolAttribute(
            "One Settlement = One Kingdom (experimental)",
            RequireRestart = false,
            HintText = "Creates a separate kingdom for every individual town/castle. "
                     + "Very fragmented map. Only active when 'One Clan = One Kingdom' is disabled.")]
        public bool OneSettlementOneKingdom
        {
            get => _oneSettlementOneKingdom;
            set { _oneSettlementOneKingdom = value; OnPropertyChanged(); }
        }
        private bool _oneSettlementOneKingdom = false;

        [SettingsPropertyGroupAttribute("2. Kingdom Creation", GroupOrder = 2)]
        [SettingsPropertyBoolAttribute(
            "Preserve Original Culture",
            RequireRestart = false,
            HintText = "New kingdoms inherit the culture of the clan's original kingdom.")]
        public bool PreserveCulture
        {
            get => _preserveCulture;
            set { _preserveCulture = value; OnPropertyChanged(); }
        }
        private bool _preserveCulture = true;

        [SettingsPropertyGroupAttribute("2. Kingdom Creation", GroupOrder = 2)]
        [SettingsPropertyDropdownAttribute(
            "Kingdom Naming Mode",
            RequireRestart = false,
            HintText = "How new kingdom names are generated: "
                     + "ClanBased = 'Kingdom of [ClanName]'; "
                     + "SettlementBased = named after primary settlement; "
                     + "CultureBased = generic culture prefix.")]
        public MCM.Abstractions.Ref.DropdownDefault<string> KingdomNamingMode
        {
            get => _kingdomNamingMode;
            set { _kingdomNamingMode = value; OnPropertyChanged(); }
        }
        private MCM.Abstractions.Ref.DropdownDefault<string> _kingdomNamingMode =
            new MCM.Abstractions.Ref.DropdownDefault<string>(
                new System.Collections.Generic.List<string>
                {
                    "ClanBased",
                    "SettlementBased",
                    "CultureBased"
                },
                0);

        [SettingsPropertyGroupAttribute("2. Kingdom Creation", GroupOrder = 2)]
        [SettingsPropertyTextAttribute(
            "Kingdom Name Prefix",
            RequireRestart = false,
            HintText = "Optional text added before every generated kingdom name, e.g. 'Kingdom of '.")]
        public string KingdomNamePrefix
        {
            get => _kingdomNamePrefix;
            set { _kingdomNamePrefix = value; OnPropertyChanged(); }
        }
        private string _kingdomNamePrefix = "Kingdom of ";

        [SettingsPropertyGroupAttribute("2. Kingdom Creation", GroupOrder = 2)]
        [SettingsPropertyTextAttribute(
            "Kingdom Name Suffix",
            RequireRestart = false,
            HintText = "Optional text appended after every generated kingdom name.")]
        public string KingdomNameSuffix
        {
            get => _kingdomNameSuffix;
            set { _kingdomNameSuffix = value; OnPropertyChanged(); }
        }
        private string _kingdomNameSuffix = "";

        [SettingsPropertyGroupAttribute("2. Kingdom Creation", GroupOrder = 2)]
        [SettingsPropertyDropdownAttribute(
            "Banner Assignment Mode",
            RequireRestart = false,
            HintText = "KeepClan = new kingdom keeps the clan's existing banner; "
                     + "Randomize = assign a random banner; "
                     + "CultureBased = derive from culture colours.")]
        public MCM.Abstractions.Ref.DropdownDefault<string> BannerMode
        {
            get => _bannerMode;
            set { _bannerMode = value; OnPropertyChanged(); }
        }
        private MCM.Abstractions.Ref.DropdownDefault<string> _bannerMode =
            new MCM.Abstractions.Ref.DropdownDefault<string>(
                new System.Collections.Generic.List<string>
                {
                    "KeepClan",
                    "Randomize",
                    "CultureBased"
                },
                0);

        // ==================================================================
        // GROUP 3 — Diplomacy Setup
        // ==================================================================

        [SettingsPropertyGroupAttribute("3. Diplomacy Setup", GroupOrder = 3)]
        [SettingsPropertyDropdownAttribute(
            "Starting Diplomatic State",
            RequireRestart = false,
            HintText = "AllPeace = every new kingdom starts at peace; "
                     + "AllWar = every kingdom starts at war; "
                     + "RivalryBased = wars follow original kingdom rivalries.")]
        public MCM.Abstractions.Ref.DropdownDefault<string> DiplomacyMode
        {
            get => _diplomacyMode;
            set { _diplomacyMode = value; OnPropertyChanged(); }
        }
        private MCM.Abstractions.Ref.DropdownDefault<string> _diplomacyMode =
            new MCM.Abstractions.Ref.DropdownDefault<string>(
                new System.Collections.Generic.List<string>
                {
                    "AllPeace",
                    "AllWar",
                    "RivalryBased"
                },
                0);

        [SettingsPropertyGroupAttribute("3. Diplomacy Setup", GroupOrder = 3)]
        [SettingsPropertyIntegerAttribute(
            "Starting Truce Duration (days)",
            0, 365,
            RequireRestart = false,
            HintText = "If > 0, a truce is applied for this many days between all kingdoms at start.")]
        public int StartingTruceDays
        {
            get => _startingTruceDays;
            set { _startingTruceDays = value; OnPropertyChanged(); }
        }
        private int _startingTruceDays = 0;

        [SettingsPropertyGroupAttribute("3. Diplomacy Setup", GroupOrder = 3)]
        [SettingsPropertyIntegerAttribute(
            "Grace Period — No Joining (days)",
            0, 365,
            RequireRestart = false,
            HintText = "Clans cannot join other kingdoms for this many days after fragmentation. "
                     + "Enforced via daily clan loyalty check (uses the longer of this and Defection Lockout). "
                     + "The player's clan is exempt.")]
        public int JoinGracePeriodDays
        {
            get => _joinGracePeriodDays;
            set { _joinGracePeriodDays = value; OnPropertyChanged(); }
        }
        private int _joinGracePeriodDays = 30;

        [SettingsPropertyGroupAttribute("3. Diplomacy Setup", GroupOrder = 3)]
        [SettingsPropertyIntegerAttribute(
            "Defection Lockout Period (days)",
            0, 365,
            RequireRestart = false,
            HintText = "Clans are forced to stay in their assigned kingdom for this many days. "
                     + "Prevents instant reunification after fragmentation. The player's clan is exempt.")]
        public int DefectionLockoutDays
        {
            get => _defectionLockoutDays;
            set { _defectionLockoutDays = value; OnPropertyChanged(); }
        }
        private int _defectionLockoutDays = 60;

        // ==================================================================
        // GROUP 4 — Ownership & Settlements
        // ==================================================================

        [SettingsPropertyGroupAttribute("4. Ownership & Settlements", GroupOrder = 4)]
        [SettingsPropertyBoolAttribute(
            "Preserve Settlement Ownership",
            RequireRestart = false,
            HintText = "When enabled, clans keep ownership of their original settlements "
                     + "after fragmentation.")]
        public bool PreserveSettlementOwnership
        {
            get => _preserveSettlementOwnership;
            set { _preserveSettlementOwnership = value; OnPropertyChanged(); }
        }
        private bool _preserveSettlementOwnership = true;

        [SettingsPropertyGroupAttribute("4. Ownership & Settlements", GroupOrder = 4)]
        [SettingsPropertyBoolAttribute(
            "Skip Landless Clans",
            RequireRestart = false,
            HintText = "Clans with no fief (town/castle) do not become kingdoms. "
                     + "Prevents empty kingdom shells.")]
        public bool SkipLandlessClans
        {
            get => _skipLandlessClans;
            set { _skipLandlessClans = value; OnPropertyChanged(); }
        }
        private bool _skipLandlessClans = false;

        // ==================================================================
        // GROUP 5 — Stability / Balance
        // ==================================================================

        [SettingsPropertyGroupAttribute("5. Stability & Balance", GroupOrder = 5)]
        [SettingsPropertyIntegerAttribute(
            "Starting Treasury per Kingdom",
            0, 1000000,
            RequireRestart = false,
            HintText = "Gold added to each new kingdom's treasury on creation.")]
        public int StartingTreasury
        {
            get => _startingTreasury;
            set { _startingTreasury = value; OnPropertyChanged(); }
        }
        private int _startingTreasury = 50000;

        [SettingsPropertyGroupAttribute("5. Stability & Balance", GroupOrder = 5)]
        [SettingsPropertyIntegerAttribute(
            "Starting Influence per Kingdom",
            0, 10000,
            RequireRestart = false,
            HintText = "Influence granted to each new kingdom leader at start.")]
        public int StartingInfluence
        {
            get => _startingInfluence;
            set { _startingInfluence = value; OnPropertyChanged(); }
        }
        private int _startingInfluence = 200;

        [SettingsPropertyGroupAttribute("5. Stability & Balance", GroupOrder = 5)]
        [SettingsPropertyIntegerAttribute(
            "Anti-Collapse Protection (days)",
            0, 365,
            RequireRestart = false,
            HintText = "New kingdoms are actively protected from elimination for this many days. "
                     + "If a kingdom loses all its clans, the mod will move a clan back to revive it.")]
        public int AntiCollapseProtectionDays
        {
            get => _antiCollapseProtectionDays;
            set { _antiCollapseProtectionDays = value; OnPropertyChanged(); }
        }
        private int _antiCollapseProtectionDays = 14;

        [SettingsPropertyGroupAttribute("5. Stability & Balance", GroupOrder = 5)]
        [SettingsPropertyIntegerAttribute(
            "Disable Diplomacy Actions (days)",
            0, 180,
            RequireRestart = false,
            HintText = "Advisory: AI should suppress automatic diplomatic actions for this many days. "
                     + "Logged at campaign start. AI action suppression requires an optional Harmony patch.")]
        public int DisableDiplomacyDays
        {
            get => _disableDiplomacyDays;
            set { _disableDiplomacyDays = value; OnPropertyChanged(); }
        }
        private int _disableDiplomacyDays = 0;

        // ==================================================================
        // GROUP 6 — Compatibility & Debug
        // ==================================================================

        [SettingsPropertyGroupAttribute("6. Compatibility & Debug", GroupOrder = 6)]
        [SettingsPropertyBoolAttribute(
            "New Campaigns Only",
            RequireRestart = false,
            HintText = "Fragmentation only runs on freshly created campaigns, "
                     + "never when loading an existing save.")]
        public bool NewCampaignsOnly
        {
            get => _newCampaignsOnly;
            set { _newCampaignsOnly = value; OnPropertyChanged(); }
        }
        private bool _newCampaignsOnly = true;

        [SettingsPropertyGroupAttribute("6. Compatibility & Debug", GroupOrder = 6)]
        [SettingsPropertyBoolAttribute(
            "Debug Logging",
            RequireRestart = false,
            HintText = "Outputs detailed information to the game log. "
                     + "Useful for troubleshooting.")]
        public bool DebugLogging
        {
            get => _debugLogging;
            set { _debugLogging = value; OnPropertyChanged(); }
        }
        private bool _debugLogging = false;

        [SettingsPropertyGroupAttribute("6. Compatibility & Debug", GroupOrder = 6)]
        [SettingsPropertyBoolAttribute(
            "Dry-Run Mode",
            RequireRestart = false,
            HintText = "Simulates fragmentation and logs what would happen without "
                     + "making any actual changes.")]
        public bool DryRunMode
        {
            get => _dryRunMode;
            set { _dryRunMode = value; OnPropertyChanged(); }
        }
        private bool _dryRunMode = false;

        [SettingsPropertyGroupAttribute("6. Compatibility & Debug", GroupOrder = 6)]
        [SettingsPropertyBoolAttribute(
            "Error-Safe Fallback",
            RequireRestart = false,
            HintText = "If a clan cannot be converted, skip it and continue instead of aborting.")]
        public bool ErrorSafeFallback
        {
            get => _errorSafeFallback;
            set { _errorSafeFallback = value; OnPropertyChanged(); }
        }
        private bool _errorSafeFallback = true;
    }
}
