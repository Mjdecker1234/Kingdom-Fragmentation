using System.Collections.Generic;
using System.Linq;
using KingdomFragmentation.Helpers;
using KingdomFragmentation.Settings;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace KingdomFragmentation.Logic
{
    /// <summary>
    /// Responsible for creating a new <see cref="Kingdom"/> for a given <see cref="Clan"/>
    /// and moving the clan into that kingdom.
    /// </summary>
    public sealed class KingdomCreator
    {
        private readonly KingdomFragmentationSettings? _settings;
        private static int _idCounter = 0;

        public KingdomCreator(KingdomFragmentationSettings? settings)
        {
            _settings = settings;
        }

        // -----------------------------------------------------------------------
        // Public API
        // -----------------------------------------------------------------------

        /// <summary>
        /// Creates a new independent kingdom for <paramref name="clan"/> and moves
        /// the clan into it.
        /// </summary>
        /// <returns>The newly created kingdom, or <c>null</c> on failure.</returns>
        public Kingdom? CreateForClan(Clan clan, IReadOnlyList<Kingdom> originalKingdoms)
        {
            if (clan?.Leader == null)
            {
                LogHelper.Warn("KingdomCreator: Clan has no leader — skipping.");
                return null;
            }

            string kingdomId = BuildKingdomId(clan);
            var (kingdomName, informalName) = BuildKingdomNames(clan);
            var culture = ResolveCulture(clan);
            var banner  = ResolveBanner(clan);

            uint primaryColor   = clan.Color;
            uint secondaryColor = clan.Color2;

            LogHelper.Debug(
                "  KingdomCreator: creating kingdom '" + kingdomName + "' (id=" + kingdomId + ") "
                + "for clan '" + clan.Name + "'.");

            var kingdom = MBObjectManager.Instance.CreateObject<Kingdom>(kingdomId);
            if (kingdom == null)
            {
                LogHelper.Error("  MBObjectManager failed to create Kingdom '" + kingdomId + "'.");
                return null;
            }

            kingdom.InitializeKingdom(
                new TextObject(kingdomName),
                new TextObject(informalName),
                culture,
                banner,
                primaryColor,
                secondaryColor,
                clan.Leader);

            // Move the clan into the new kingdom
            ChangeKingdomAction.ApplyByJoinToKingdom(clan, kingdom, showNotification: false);

            ApplyStartingResources(clan, kingdom);

            LogHelper.Info("  Created kingdom '" + kingdomName + "' led by '" + clan.Leader.Name + "'.");
            return kingdom;
        }

        // -----------------------------------------------------------------------
        // Naming
        // -----------------------------------------------------------------------

        private (string name, string informalName) BuildKingdomNames(Clan clan)
        {
            string prefix = _settings?.KingdomNamePrefix ?? "Kingdom of ";
            string suffix = _settings?.KingdomNameSuffix ?? "";
            string mode   = _settings?.KingdomNamingMode?.SelectedValue ?? "ClanBased";

            string baseName;
            switch (mode)
            {
                case "SettlementBased":
                    var primary = clan.Settlements
                        .Where(s => s.IsTown || s.IsCastle)
                        .OrderByDescending(s => s.IsTown)
                        .FirstOrDefault();
                    baseName = primary != null
                        ? primary.Name.ToString()
                        : clan.Name.ToString();
                    break;

                case "CultureBased":
                    baseName = clan.Culture?.Name?.ToString() ?? clan.Name.ToString();
                    break;

                default: // ClanBased
                    baseName = clan.Name.ToString();
                    break;
            }

            string fullName    = (prefix + baseName + suffix).Trim();
            string informalStr = baseName.Trim();
            return (fullName, informalStr);
        }

        // -----------------------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------------------

        private static string BuildKingdomId(Clan clan)
        {
            _idCounter++;
            string safeName = (clan.StringId ?? clan.Name.ToString())
                .Replace(" ", "_")
                .ToLowerInvariant();
            return "kf_" + safeName + "_" + _idCounter;
        }

        private CultureObject? ResolveCulture(Clan clan)
        {
            // PreserveCulture=false still uses clan culture — there is no culture-override option
            return clan.Culture;
        }

        private Banner ResolveBanner(Clan clan)
        {
            string mode = _settings?.BannerMode?.SelectedValue ?? "KeepClan";

            switch (mode)
            {
                case "Randomize":
                    return Banner.CreateRandomBanner();

                case "CultureBased":
                    // Derive from culture colours using the clan's own banner as fallback
                    if (clan.Culture != null)
                    {
                        return new Banner(
                            clan.Banner?.BannerDataList?[0]?.MeshId.ToString()
                                ?? Banner.CreateRandomBanner().Serialize(),
                            clan.Culture.Color,
                            clan.Culture.Color2);
                    }
                    return clan.Banner ?? Banner.CreateRandomBanner();

                default: // KeepClan
                    return clan.Banner ?? Banner.CreateRandomBanner();
            }
        }

        private void ApplyStartingResources(Clan clan, Kingdom kingdom)
        {
            int treasury  = _settings?.StartingTreasury  ?? 50000;
            int influence = _settings?.StartingInfluence ?? 200;

            if (treasury > 0 && clan.Leader != null)
            {
                clan.Leader.Gold += treasury;
                LogHelper.Debug("  Applied treasury " + treasury + " to " + clan.Leader.Name);
            }

            if (influence > 0 && clan.Leader != null)
            {
                clan.Influence += influence;
                LogHelper.Debug("  Applied influence " + influence + " to clan " + clan.Name);
            }
        }
    }
}
