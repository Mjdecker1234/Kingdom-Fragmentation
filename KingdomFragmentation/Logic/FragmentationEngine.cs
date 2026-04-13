using System;
using System.Collections.Generic;
using System.Linq;
using KingdomFragmentation.Helpers;
using KingdomFragmentation.Settings;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;

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

        /// <summary>
        /// After <see cref="Run"/> completes successfully this contains every
        /// clan → KF-kingdom assignment so the behaviour can persist and enforce it.
        /// </summary>
        public Dictionary<string, string> ClanKingdomAssignments { get; }
            = new Dictionary<string, string>();

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
                .Where(k => k != null && !k.IsEliminated)
                .ToList();

            LogHelper.Debug("  Original kingdoms: " + originalKingdoms.Count);

            // Collect all eligible clans
            var eligibleClans = CollectEligibleClans(originalKingdoms);

            // Apply OneSettlement filter when in settlement-based mode
            bool oneClanOneKingdom   = _settings?.OneClanOneKingdom      ?? true;
            bool oneSettlementMode   = _settings?.OneSettlementOneKingdom ?? false;

            if (!oneClanOneKingdom && oneSettlementMode)
            {
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
            // Target kingdom count: when configured, select the best N clans
            // to become kingdom leaders and distribute the rest among them.
            // ----------------------------------------------------------------
            int targetCount = _settings?.TargetKingdomCount ?? 0;
            List<Clan>? leaderClans = null;
            List<Clan>? followerClans = null;

            if (targetCount > 0)
            {
                // TargetKingdomCount takes precedence over OneClanOneKingdom
                if (oneClanOneKingdom)
                {
                    LogHelper.Info("TargetKingdomCount (" + targetCount
                        + ") overrides 'One Clan = One Kingdom' — clans will be grouped.");
                }

                // Sort by suitability: higher tier first, then clans with fiefs first
                var sorted = eligibleClans
                    .OrderByDescending(c => c.Tier)
                    .ThenByDescending(c => c.Settlements.Count(s => s.IsTown || s.IsCastle))
                    .ThenByDescending(c => c.Renown)
                    .ToList();

                int leaderCount = Math.Min(targetCount, sorted.Count);
                leaderClans   = sorted.Take(leaderCount).ToList();
                followerClans = sorted.Skip(leaderCount).ToList();

                LogHelper.Info("Target kingdom count: " + targetCount
                    + " → " + leaderClans.Count + " leader(s), "
                    + followerClans.Count + " follower(s) to distribute.");
            }
            else
            {
                // Default: every eligible clan becomes a kingdom leader
                leaderClans   = eligibleClans;
                followerClans = new List<Clan>();
            }

            // ----------------------------------------------------------------
            // Capture pre-fragmentation clan → original kingdom mapping BEFORE
            // any ChangeKingdomAction calls mutate live clan membership.
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

            // ---- Phase 1: Create kingdoms for leader clans ------------------
            foreach (var clan in leaderClans)
            {
                LogHelper.Debug("  Processing leader clan: " + clan.Name + " (tier " + clan.Tier + ")");

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
                        RecordAssignment(clan, clan.Kingdom);
                        continue;
                    }

                    var newKingdom = creator.CreateForClan(clan);
                    if (newKingdom != null)
                    {
                        newKingdoms.Add(newKingdom);
                        reassigner.Reassign(clan, newKingdom);
                        RecordAssignment(clan, newKingdom);
                    }
                }
                catch (Exception ex)
                {
                    LogHelper.Error("  Error processing leader clan '" + clan.Name + "': " + ex.Message);
                    if (!safeFallback)
                        throw;
                }
            }

            // ---- Phase 2: Distribute follower clans among new kingdoms ------
            if (!dryRun && followerClans.Count > 0 && newKingdoms.Count > 0)
            {
                DistributeFollowerClans(followerClans, newKingdoms,
                    clanOriginSnapshot, reassigner);
            }
            else if (dryRun && followerClans.Count > 0)
            {
                LogHelper.Info("  [DRY RUN] Would distribute " + followerClans.Count
                    + " follower clan(s) among " + newKingdoms.Count + " kingdom(s).");
            }

            if (!dryRun)
            {
                diplomacyInit.Initialize(newKingdoms, originalKingdoms, clanOriginSnapshot);
                LogHelper.Info("FragmentationEngine: finished. " + newKingdoms.Count + " kingdom(s) in play.");
            }
        }

        // -----------------------------------------------------------------------
        // Follower distribution
        // -----------------------------------------------------------------------

        /// <summary>
        /// Moves follower clans into existing new kingdoms.  Preference:
        /// 1. The new kingdom whose leader came from the same original kingdom.
        /// 2. The new kingdom with the same culture.
        /// 3. The new kingdom with the fewest clans (load-balance).
        /// </summary>
        private void DistributeFollowerClans(
            List<Clan> followers,
            List<Kingdom> newKingdoms,
            Dictionary<Clan, Kingdom> clanOriginSnapshot,
            SettlementReassigner reassigner)
        {
            // Build a map from original-kingdom → new kingdom (using the leader's origin)
            var originToNew = new Dictionary<Kingdom, Kingdom>();
            foreach (var nk in newKingdoms)
            {
                if (nk?.RulingClan == null) continue;
                if (clanOriginSnapshot.TryGetValue(nk.RulingClan, out var origin) && origin != null)
                {
                    // First match wins (multiple leaders may share an origin)
                    if (!originToNew.ContainsKey(origin))
                        originToNew[origin] = nk;
                }
            }

            bool safeFallback = _settings?.ErrorSafeFallback ?? true;

            foreach (var clan in followers)
            {
                try
                {
                    Kingdom? target = null;

                    // 1. Same original kingdom
                    if (clanOriginSnapshot.TryGetValue(clan, out var origin) && origin != null)
                        originToNew.TryGetValue(origin, out target);

                    // 2. Same culture
                    if (target == null && clan.Culture != null)
                    {
                        target = newKingdoms
                            .Where(k => k.Culture == clan.Culture)
                            .OrderBy(k => k.Clans.Count)
                            .FirstOrDefault();
                    }

                    // 3. Fewest clans (load-balance)
                    if (target == null)
                    {
                        target = newKingdoms
                            .OrderBy(k => k.Clans.Count)
                            .FirstOrDefault();
                    }

                    if (target == null)
                    {
                        LogHelper.Warn("  No target kingdom for follower clan '" + clan.Name + "' — skipping.");
                        continue;
                    }

                    LogHelper.Debug("  Assigning follower clan '" + clan.Name
                        + "' to kingdom '" + target.Name + "'.");

                    ChangeKingdomAction.ApplyByJoinToKingdom(clan, target, showNotification: false);
                    reassigner.Reassign(clan, target);
                    RecordAssignment(clan, target);
                }
                catch (Exception ex)
                {
                    LogHelper.Error("  Error distributing follower '" + clan.Name + "': " + ex.Message);
                    if (!safeFallback)
                        throw;
                }
            }
        }

        // -----------------------------------------------------------------------
        // Assignment tracking
        // -----------------------------------------------------------------------

        private void RecordAssignment(Clan clan, Kingdom kingdom)
        {
            if (clan?.StringId != null && kingdom?.StringId != null)
                ClanKingdomAssignments[clan.StringId] = kingdom.StringId;
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
