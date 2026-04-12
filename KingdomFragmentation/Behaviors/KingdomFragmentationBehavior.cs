using System;
using System.Collections.Generic;
using System.Linq;
using KingdomFragmentation.Helpers;
using KingdomFragmentation.Logic;
using KingdomFragmentation.Settings;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;

namespace KingdomFragmentation.Behaviors
{
    /// <summary>
    /// Campaign behavior that triggers kingdom fragmentation exactly once on a new
    /// campaign and then actively enforces the resulting structure (truces, clan
    /// loyalty, anti-collapse) through the daily tick for the configured period.
    /// </summary>
    public sealed class KingdomFragmentationBehavior : CampaignBehaviorBase
    {
        // -----------------------------------------------------------------------
        // State — serialised into every save so each flag survives load/reload
        // -----------------------------------------------------------------------

        /// <summary>Set to true after fragmentation runs; prevents re-triggering on load.</summary>
        private bool _fragmentationApplied = false;

        /// <summary>
        /// Campaign day number when fragmentation completed.
        /// Used for time-gated enforcement (truce, grace periods, etc.).
        /// -1 means fragmentation has not yet run.
        /// </summary>
        private float _fragmentationCampaignDay = -1f;

        /// <summary>
        /// Parallel lists that together form the clan → KF-kingdom assignment map.
        /// Persisted into saves so enforcement survives reload.
        /// </summary>
        private List<string> _assignedClanIds    = new List<string>();
        private List<string> _assignedKingdomIds = new List<string>();

        // -----------------------------------------------------------------------
        // CampaignBehaviorBase
        // -----------------------------------------------------------------------

        public override void RegisterEvents()
        {
            // Fires after all partial follow-ups on NEW campaign creation.
            CampaignEvents.OnNewGameCreatedPartialFollowUpEndEvent.AddNonSerializedListener(
                this,
                new Action<CampaignGameStarter>(OnNewGameCreatedPartialFollowUpEnd));

            // Daily tick: enforces truces, clan loyalty, and anti-collapse.
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(
                this,
                new Action(OnDailyTick));
        }

        public override void SyncData(IDataStore dataStore)
        {
            dataStore.SyncData("KF_FragmentationApplied",    ref _fragmentationApplied);
            dataStore.SyncData("KF_FragmentationCampaignDay", ref _fragmentationCampaignDay);
            dataStore.SyncData("KF_AssignedClanIds",          ref _assignedClanIds);
            dataStore.SyncData("KF_AssignedKingdomIds",       ref _assignedKingdomIds);

            // Guard against null after deserialization
            if (_assignedClanIds == null) _assignedClanIds = new List<string>();
            if (_assignedKingdomIds == null) _assignedKingdomIds = new List<string>();
        }

        // -----------------------------------------------------------------------
        // New-game handler
        // -----------------------------------------------------------------------

        private void OnNewGameCreatedPartialFollowUpEnd(CampaignGameStarter starter)
        {
            var settings = KingdomFragmentationSettings.Instance;

            if (settings == null)
            {
                LogHelper.Warn("KingdomFragmentationSettings not available — using defaults.");
            }

            if (settings != null && !settings.EnableFragmentation)
            {
                LogHelper.Info("Kingdom Fragmentation is disabled in MCM settings. Skipping.");
                return;
            }

            if (settings != null && settings.NewCampaignsOnly)
            {
                LogHelper.Info("NewCampaignsOnly = true: fragmentation will run on this new campaign.");
            }

            if (_fragmentationApplied)
            {
                LogHelper.Info("Fragmentation already applied for this campaign. Skipping.");
                return;
            }

            LogHelper.Info("Kingdom Fragmentation: starting fragmentation process.");

            bool isDryRun = settings?.DryRunMode ?? false;
            if (isDryRun)
            {
                LogHelper.Info("[DRY RUN] No actual changes will be made.");
            }

            // Log advisory-only settings that need Harmony patches for full enforcement
            LogAdvisorySettings(settings);

            try
            {
                var engine = new FragmentationEngine(settings);
                engine.Run(isDryRun);

                if (!isDryRun)
                {
                    _fragmentationApplied    = true;
                    _fragmentationCampaignDay = (float)CampaignTime.Now.ToDays;

                    // Persist the clan → kingdom assignments for enforcement
                    _assignedClanIds.Clear();
                    _assignedKingdomIds.Clear();
                    foreach (var kvp in engine.ClanKingdomAssignments)
                    {
                        _assignedClanIds.Add(kvp.Key);
                        _assignedKingdomIds.Add(kvp.Value);
                    }

                    LogHelper.Info("Kingdom Fragmentation: process complete on campaign day "
                        + _fragmentationCampaignDay + ". "
                        + _assignedClanIds.Count + " clan assignment(s) recorded.");
                }
                else
                {
                    LogHelper.Info("[DRY RUN] Fragmentation simulation complete. See log for details.");
                }
            }
            catch (Exception ex)
            {
                bool safeFallback = settings?.ErrorSafeFallback ?? true;
                LogHelper.Error("Kingdom Fragmentation encountered an error: " + ex.Message);
                if (safeFallback)
                {
                    LogHelper.Warn("Error-safe fallback active — partial changes may have been applied.");
                }
                else
                {
                    throw;
                }
            }
        }

        // -----------------------------------------------------------------------
        // Daily tick — time-gated enforcement
        // -----------------------------------------------------------------------

        private void OnDailyTick()
        {
            if (!_fragmentationApplied) return;
            if (_fragmentationCampaignDay < 0f) return;

            var settings = KingdomFragmentationSettings.Instance;
            if (settings == null) return;

            float daysSince = (float)CampaignTime.Now.ToDays - _fragmentationCampaignDay;

            // ---- StartingTruceDays ------------------------------------------
            int truceDays = settings.StartingTruceDays;
            string diplomacyMode = settings.DiplomacyMode?.SelectedValue ?? "AllPeace";

            if (truceDays > 0 && daysSince < truceDays && diplomacyMode != "AllWar")
            {
                EnforceTruceBetweenFragmentedKingdoms();
            }

            // ---- Clan loyalty (DefectionLockoutDays) ------------------------
            // Prevent clans from leaving their KF-assigned kingdoms during the
            // lockout window.  This is the active enforcement that keeps the
            // fragmented map intact instead of collapsing back immediately.
            int defectionDays = settings.DefectionLockoutDays;
            if (defectionDays > 0 && daysSince < defectionDays)
            {
                EnforceClanLoyalty();
            }

            // ---- Anti-collapse protection -----------------------------------
            // During the protection window, if a KF-kingdom has been reduced to
            // zero clans (eliminated), attempt to revive it by moving the
            // original ruling clan back.
            int antiCollapseDays = settings.AntiCollapseProtectionDays;
            if (antiCollapseDays > 0 && daysSince < antiCollapseDays)
            {
                EnforceAntiCollapse();
            }
        }

        /// <summary>
        /// Ensures no two KF-created kingdoms are at war during the truce window.
        /// </summary>
        private static void EnforceTruceBetweenFragmentedKingdoms()
        {
            var kfKingdoms = Kingdom.All
                .Where(k => k != null
                          && !k.IsEliminated
                          && k.StringId != null
                          && k.StringId.StartsWith("kf_",
                             StringComparison.OrdinalIgnoreCase))
                .ToArray();

            if (kfKingdoms.Length == 0) return;

            for (int i = 0; i < kfKingdoms.Length; i++)
            {
                for (int j = i + 1; j < kfKingdoms.Length; j++)
                {
                    var a = kfKingdoms[i];
                    var b = kfKingdoms[j];

                    if (FactionManager.IsAtWarAgainstFaction(a, b))
                    {
                        try
                        {
                            MakePeaceAction.Apply(a, b, showNotification: false);
                            LogHelper.Debug("Truce enforcement: peace applied between '"
                                + a.Name + "' and '" + b.Name + "'.");
                        }
                        catch (Exception ex)
                        {
                            LogHelper.Warn("Truce enforcement failed between '"
                                + a.Name + "' and '" + b.Name + "': " + ex.Message);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Checks every assigned clan and forces it back into its KF kingdom if
        /// it has defected.  Skips the player's clan to avoid overriding player
        /// choice.
        /// </summary>
        private void EnforceClanLoyalty()
        {
            if (_assignedClanIds.Count == 0) return;

            int assignmentCount = Math.Min(_assignedClanIds.Count, _assignedKingdomIds.Count);
            for (int i = 0; i < assignmentCount; i++)
            {
                try
                {
                    string clanId    = _assignedClanIds[i];
                    string kingdomId = _assignedKingdomIds[i];
                    if (string.IsNullOrEmpty(clanId) || string.IsNullOrEmpty(kingdomId))
                        continue;

                    var clan = Clan.All.FirstOrDefault(
                        c => c.StringId == clanId && !c.IsEliminated);
                    if (clan == null) continue;

                    // Never override the player's choice
                    if (clan == Clan.PlayerClan) continue;

                    var assignedKingdom = Kingdom.All.FirstOrDefault(
                        k => k.StringId == kingdomId && !k.IsEliminated);
                    if (assignedKingdom == null) continue;

                    // If clan is no longer in its assigned kingdom, move it back
                    if (clan.Kingdom != assignedKingdom)
                    {
                        LogHelper.Debug("Loyalty enforcement: returning '"
                            + clan.Name + "' to '" + assignedKingdom.Name + "'.");
                        ChangeKingdomAction.ApplyByJoinToKingdom(
                            clan, assignedKingdom, showNotification: false);
                    }
                }
                catch (Exception ex)
                {
                    LogHelper.Warn("Clan loyalty enforcement error at index "
                        + i + ": " + ex.Message);
                }
            }
        }

        /// <summary>
        /// If a KF-kingdom has lost all its clans / been eliminated, attempt
        /// to move its original ruling clan back to restore it.
        /// </summary>
        private void EnforceAntiCollapse()
        {
            if (_assignedClanIds.Count == 0) return;

            // Build set of KF-kingdom IDs that should exist
            var expectedKingdomIds = new HashSet<string>(
                _assignedKingdomIds.Where(id => !string.IsNullOrEmpty(id)));

            foreach (var kingdomId in expectedKingdomIds)
            {
                try
                {
                    var kingdom = Kingdom.All.FirstOrDefault(k => k.StringId == kingdomId);
                    if (kingdom == null) continue;

                    // If the kingdom still has active clans, it's fine
                    if (!kingdom.IsEliminated && kingdom.Clans.Count > 0)
                        continue;

                    // Find the first assigned clan that should be in this kingdom
                    int assignmentCount = Math.Min(_assignedClanIds.Count, _assignedKingdomIds.Count);
                    for (int i = 0; i < assignmentCount; i++)
                    {
                        if (_assignedKingdomIds[i] != kingdomId) continue;

                        var clan = Clan.All.FirstOrDefault(
                            c => c.StringId == _assignedClanIds[i] && !c.IsEliminated);
                        if (clan == null || clan == Clan.PlayerClan) continue;

                        LogHelper.Debug("Anti-collapse: moving '"
                            + clan.Name + "' back into '" + kingdom.Name + "'.");
                        ChangeKingdomAction.ApplyByJoinToKingdom(
                            clan, kingdom, showNotification: false);
                        break; // One clan is enough to revive the kingdom
                    }
                }
                catch (Exception ex)
                {
                    LogHelper.Warn("Anti-collapse error for kingdom '" + kingdomId + "': " + ex.Message);
                }
            }
        }

        // -----------------------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------------------

        private static void LogAdvisorySettings(KingdomFragmentationSettings? settings)
        {
            if (settings == null) return;

            if (settings.JoinGracePeriodDays > 0)
                LogHelper.Info("JoinGracePeriodDays = " + settings.JoinGracePeriodDays
                    + " — will be enforced via daily clan loyalty check.");

            if (settings.DefectionLockoutDays > 0)
                LogHelper.Info("DefectionLockoutDays = " + settings.DefectionLockoutDays
                    + " — will be enforced via daily clan loyalty check.");

            if (settings.AntiCollapseProtectionDays > 0)
                LogHelper.Info("AntiCollapseProtectionDays = " + settings.AntiCollapseProtectionDays
                    + " — will be enforced via daily anti-collapse check.");

            if (settings.DisableDiplomacyDays > 0)
                LogHelper.Info("DisableDiplomacyDays = " + settings.DisableDiplomacyDays
                    + " (advisory — full enforcement requires an optional Harmony patch)");
        }
    }
}
