using System.Collections.Generic;
using System.Linq;
using KingdomFragmentation.Helpers;
using KingdomFragmentation.Settings;
using TaleWorlds.CampaignSystem;

namespace KingdomFragmentation.Logic
{
    /// <summary>
    /// Top-level orchestrator.  Collects eligible clans and delegates to the
    /// specialised sub-systems (kingdom creation, settlement reassignment,
    /// diplomacy).
    /// </summary>
    public sealed class FragmentationEngine
    {
        private readonly KingdomFragmentationSettings? _settings;

        public FragmentationEngine(KingdomFragmentationSettings? settings)
        {
            _settings = settings;
        }

        // -----------------------------------------------------------------------
        // Public API
        // -----------------------------------------------------------------------

        /// <summary>
        /// Execute (or simulate when <paramref name="dryRun"/> is <c>true</c>) the
        /// full fragmentation pipeline.
        /// </summary>
        public void Run(bool dryRun = false)
        {
            LogHelper.Info("FragmentationEngine: collecting eligible clans…");

            // Snapshot: kingdoms that existed BEFORE we start changing things
            var originalKingdoms = Kingdom.All
                .Where(k => !k.IsEliminated)
                .ToList();

            LogHelper.Debug("  Original kingdoms: " + originalKingdoms.Count);

            // Collect all eligible clans
            var eligibleClans = CollectEligibleClans(originalKingdoms);

            // Apply OneSettlement filter when in settlement-based mode
            bool oneClanOneKingdom   = _settings?.OneClanOneKingdom      ?? true;
            bool oneSettlementMode   = _settings?.OneSettlementOneKingdom ?? false;

            if (!oneClanOneKingdom && oneSettlementMode)
            {
                // One-settlement mode: a kingdom must represent exactly one fief,
                // so we only include clans that currently own exactly one town or
                // castle.  Multi-fief clans are skipped because Bannerlord does not
                // allow splitting a clan across multiple kingdoms.
                eligibleClans = eligibleClans
                    .Where(c => c.Settlements.Count(s => s.IsTown || s.IsCastle) == 1)
                    .ToList();
                LogHelper.Info("One-Settlement mode: filtered to "
                    + eligibleClans.Count + " single-fief clan(s).");
            }

            LogHelper.Info("FragmentationEngine: " + eligibleClans.Count + " eligible clan(s) to process.");

            if (eligibleClans.Count == 0)
            {
                LogHelper.Warn("No eligible clans found — nothing to fragment.");
                return;
            }

            // ----------------------------------------------------------------
            // Capture pre-fragmentation clan → original kingdom mapping BEFORE
            // any ChangeKingdomAction calls mutate live clan membership.
            // This snapshot is required for correct RivalryBased diplomacy.
            // ----------------------------------------------------------------
            var clanOriginSnapshot = new Dictionary<Clan, Kingdom>(eligibleClans.Count);
            foreach (var clan in eligibleClans)
            {
                if (clan.Kingdom != null)
                    clanOriginSnapshot[clan] = clan.Kingdom;
            }

            var creator       = new KingdomCreator(_settings);
            var reassigner    = new SettlementReassigner(_settings);
            var diplomacyInit = new DiplomacyInitializer(_settings);

            // Track newly created kingdoms so we can set up diplomacy afterwards
            var newKingdoms = new List<Kingdom>();

            foreach (var clan in eligibleClans)
            {
                LogHelper.Debug("  Processing clan: " + clan.Name + " (tier " + clan.Tier + ")");

                if (dryRun)
                {
                    LogHelper.Info("  [DRY RUN] Would create kingdom for clan '" + clan.Name + "'.");
                    continue;
                }

                bool safeFallback = _settings?.ErrorSafeFallback ?? true;

                try
                {
                    // Skip if the clan is already a sole-kingdom ruling clan
                    if (clan.Kingdom != null && clan.Kingdom.Clans.Count == 1
                        && clan.Kingdom.RulingClan == clan)
                    {
                        LogHelper.Debug("  Clan '" + clan.Name + "' is already a sole kingdom — skipping creation.");
                        newKingdoms.Add(clan.Kingdom);
                        continue;
                    }

                    var newKingdom = creator.CreateForClan(clan);
                    if (newKingdom != null)
                    {
                        newKingdoms.Add(newKingdom);
                        reassigner.Reassign(clan, newKingdom);
                    }
                }
                catch (System.Exception ex)
                {
                    LogHelper.Error("  Error processing clan '" + clan.Name + "': " + ex.Message);
                    if (!safeFallback)
                        throw;
                }
            }

            if (!dryRun)
            {
                diplomacyInit.Initialize(newKingdoms, originalKingdoms, clanOriginSnapshot);
                LogHelper.Info("FragmentationEngine: finished. " + newKingdoms.Count + " kingdom(s) in play.");
            }
        }

        // -----------------------------------------------------------------------
        // Clan collection
        // -----------------------------------------------------------------------

        private List<Clan> CollectEligibleClans(List<Kingdom> originalKingdoms)
        {
            bool debug         = _settings?.DebugLogging     ?? false;
            bool majorOnly     = _settings?.MajorNobleClanOnly ?? true;
            bool includeRuling = _settings?.IncludeRulingClans ?? true;
            bool includeMinor  = _settings?.IncludeMinorFactions ?? false;
            bool includeMerc   = _settings?.IncludeMercenaries   ?? false;
            bool includeRebel  = _settings?.IncludeRebelClans    ?? false;
            bool skipLandless  = _settings?.SkipLandlessClans    ?? false;
            int  minTier       = _settings?.MinClanTier          ?? 1;

            var result = new List<Clan>();

            foreach (var kingdom in originalKingdoms)
            {
                foreach (var clan in kingdom.Clans.ToList())
                {
                    if (!ClanHelper.IsValid(clan)) continue;

                    // --- Ruling clan ---
                    bool isRuling = kingdom.RulingClan == clan;
                    if (isRuling && !includeRuling)
                    {
                        if (debug) LogHelper.Debug("    Skipping ruling clan '" + clan.Name + "'.");
                        continue;
                    }

                    // --- Minor faction ---
                    if (clan.IsMinorFaction && !includeMinor)
                    {
                        if (debug) LogHelper.Debug("    Skipping minor faction '" + clan.Name + "'.");
                        continue;
                    }

                    // --- Mercenary ---
                    if (ClanHelper.IsMercenary(clan) && !includeMerc)
                    {
                        if (debug) LogHelper.Debug("    Skipping mercenary clan '" + clan.Name + "'.");
                        continue;
                    }

                    // --- Rebel ---
                    if (ClanHelper.IsRebel(clan) && !includeRebel)
                    {
                        if (debug) LogHelper.Debug("    Skipping rebel clan '" + clan.Name + "'.");
                        continue;
                    }

                    // --- Tier threshold ---
                    if (majorOnly && clan.Tier < minTier)
                    {
                        if (debug) LogHelper.Debug("    Skipping low-tier clan '" + clan.Name
                            + "' (tier " + clan.Tier + " < " + minTier + ").");
                        continue;
                    }

                    // --- Landless skip ---
                    if (skipLandless && !ClanHelper.HasFief(clan))
                    {
                        if (debug) LogHelper.Debug("    Skipping landless clan '" + clan.Name + "'.");
                        continue;
                    }

                    result.Add(clan);
                }
            }

            return result;
        }
    }
}
