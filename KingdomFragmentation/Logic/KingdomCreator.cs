using System;
using System.Collections.Generic;
using System.Linq;
using KingdomFragmentation.Helpers;
using KingdomFragmentation.Settings;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;

namespace KingdomFragmentation.Logic
{
    public sealed class KingdomCreator
    {
        private readonly KingdomFragmentationSettings? _settings;
        private static int _idCounter = 0;

        public KingdomCreator(KingdomFragmentationSettings? settings)
        {
            _settings = settings;
        }

        /// <summary>
        /// Creates a new kingdom for <paramref name="clan"/> and moves the clan into it.
        /// </summary>
        public Kingdom? CreateForClan(Clan clan)
        {
            if (clan?.Leader == null)
            {
                LogHelper.Warn("KingdomCreator: Clan has no leader — skipping.");
                return null;
            }

            // Find a settlement anchor for InitializeKingdom
            var anchor = clan.Settlements
                .FirstOrDefault(s => s != null && (s.IsTown || s.IsCastle))
                ?? clan.Settlements.FirstOrDefault(s => s != null)
                ?? Settlement.All.FirstOrDefault(s => s != null && s.IsTown);

            if (anchor == null)
            {
                LogHelper.Warn("KingdomCreator: No settlement anchor found for clan '"
                    + clan.Name + "'.");
                return null;
            }

            string kingdomId = BuildKingdomId(clan);
            var (name, informal) = BuildKingdomNames(clan, anchor);
            var culture = clan.Culture ?? clan.Kingdom?.Culture;
            var banner = Banner.CreateRandomBanner();
            var (color1, color2) = ResolveColors(clan, kingdomId);

            LogHelper.Debug("KingdomCreator: creating '" + name + "' (id=" + kingdomId
                + ") for clan '" + clan.Name + "'.");

            // Create the kingdom object
            Kingdom? kingdom;
            try
            {
                kingdom = MBObjectManager.Instance.CreateObject<Kingdom>(kingdomId);
            }
            catch (Exception ex)
            {
                LogHelper.Error("MBObjectManager threw creating '" + kingdomId + "': " + ex.Message);
                return null;
            }

            if (kingdom == null)
            {
                LogHelper.Error("MBObjectManager returned null for '" + kingdomId + "'.");
                return null;
            }

            // Initialize the kingdom
            try
            {
                kingdom.InitializeKingdom(
                    new TextObject(name),
                    new TextObject(informal),
                    culture,
                    banner,
                    color1,
                    color2,
                    anchor,
                    new TextObject(""),
                    new TextObject(""),
                    new TextObject(""));
            }
            catch (Exception ex)
            {
                LogHelper.Error("InitializeKingdom failed for '" + kingdomId + "': " + ex.Message);
                return null;
            }

            // Move clan into the kingdom — cascading fallback
            bool moved = false;

            try
            {
                ChangeKingdomAction.ApplyByCreateKingdom(clan, kingdom, false);
                moved = clan.Kingdom == kingdom;
            }
            catch (Exception ex)
            {
                LogHelper.Warn("ApplyByCreateKingdom failed: " + ex.Message + " — trying join.");
            }

            if (!moved)
            {
                try
                {
                    ChangeKingdomAction.ApplyByJoinToKingdom(clan, kingdom, CampaignTime.Now, false);
                    moved = clan.Kingdom == kingdom;
                }
                catch (Exception ex)
                {
                    LogHelper.Warn("ApplyByJoinToKingdom failed: " + ex.Message + " — trying direct.");
                }
            }

            if (!moved)
            {
                try
                {
                    clan.Kingdom = kingdom;
                    if (kingdom.RulingClan == null)
                        kingdom.RulingClan = clan;
                    moved = clan.Kingdom == kingdom;
                }
                catch (Exception ex)
                {
                    LogHelper.Error("Direct assignment failed: " + ex.Message);
                }
            }

            if (!moved)
            {
                LogHelper.Error("Could not move clan '" + clan.Name
                    + "' into kingdom '" + name + "' by any method.");
                return null;
            }

            if (kingdom.RulingClan != clan)
            {
                try { kingdom.RulingClan = clan; } catch { }
            }

            LogHelper.Info("Created kingdom '" + name + "' led by '" + clan.Leader.Name + "'.");
            return kingdom;
        }

        // =====================================================================
        // Naming
        // =====================================================================

        private (string name, string informal) BuildKingdomNames(Clan clan, Settlement anchor)
        {
            string prefix = _settings?.KingdomNamePrefix ?? "Kingdom of ";
            KingdomNamingModeOption mode = _settings?.KingdomNamingMode
                ?? KingdomNamingModeOption.ClanBased;

            string baseName;
            switch (mode)
            {
                case KingdomNamingModeOption.SettlementBased:
                    baseName = anchor.Name?.ToString() ?? clan.Name.ToString();
                    break;
                case KingdomNamingModeOption.CultureBased:
                    baseName = clan.Culture?.Name?.ToString() ?? clan.Name.ToString();
                    break;
                default:
                    baseName = clan.Name.ToString();
                    break;
            }

            return ((prefix + baseName).Trim(), baseName.Trim());
        }

        // =====================================================================
        // Helpers
        // =====================================================================

        private static string BuildKingdomId(Clan clan)
        {
            _idCounter++;
            string safe = (clan.StringId ?? clan.Name.ToString())
                .Replace(" ", "_").ToLowerInvariant();
            return "kf_" + safe + "_" + _idCounter;
        }

        private (uint, uint) ResolveColors(Clan clan, string kingdomId)
        {
            BannerColorModeOption mode = _settings?.BannerColorMode
                ?? BannerColorModeOption.UniquePerKingdom;

            if (mode == BannerColorModeOption.CultureBased && clan.Culture != null)
                return (clan.Culture.Color, clan.Culture.Color2);

            // Unique: seeded random
            int seed = StableHash(kingdomId + "|" + (clan.StringId ?? ""));
            return (RandomColor(seed, 0x31), RandomColor(seed, 0x7B));
        }

        private static int StableHash(string value)
        {
            unchecked
            {
                int hash = 23;
                for (int i = 0; i < value.Length; i++)
                    hash = (hash * 31) + value[i];
                return hash;
            }
        }

        private static uint RandomColor(int seed, int salt)
        {
            unchecked
            {
                var rng = new Random(seed ^ salt);
                int r = rng.Next(48, 224);
                int g = rng.Next(48, 224);
                int b = rng.Next(48, 224);
                return (uint)((255 << 24) | (r << 16) | (g << 8) | b);
            }
        }
    }
}
