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
    /// campaign and then enforces truces and clan loyalty through the daily tick.
    /// </summary>
    public sealed class KingdomFragmentationBehavior : CampaignBehaviorBase
    {
        // -----------------------------------------------------------------------
        // State — serialised into every save
        // -----------------------------------------------------------------------

        private bool _fragmentationApplied = false;
        private float _fragmentationCampaignDay = -1f;
        private List<string> _assignedClanIds = new List<string>();
        private List<string> _assignedKingdomIds = new List<string>();

        // -----------------------------------------------------------------------
        // CampaignBehaviorBase
        // -----------------------------------------------------------------------

        public override void RegisterEvents()
        {
            LogHelper.Info("RegisterEvents called — wiring campaign event listeners.");

            CampaignEvents.OnNewGameCreatedPartialFollowUpEndEvent.AddNonSerializedListener(
                this,
                new Action<CampaignGameStarter>(OnNewGameCreatedPartialFollowUpEnd));

            CampaignEvents.OnGameLoadFinishedEvent.AddNonSerializedListener(
                this,
                new Action(OnGameLoadFinished));

            CampaignEvents.DailyTickEvent.AddNonSerializedListener(
                this,
                new Action(OnDailyTick));
        }

        public override void SyncData(IDataStore dataStore)
        {
            dataStore.SyncData("KF_FragmentationApplied", ref _fragmentationApplied);
            dataStore.SyncData("KF_FragmentationCampaignDay", ref _fragmentationCampaignDay);
            dataStore.SyncData("KF_AssignedClanIds", ref _assignedClanIds);
            dataStore.SyncData("KF_AssignedKingdomIds", ref _assignedKingdomIds);

            if (_assignedClanIds == null) _assignedClanIds = new List<string>();
            if (_assignedKingdomIds == null) _assignedKingdomIds = new List<string>();
        }

        // -----------------------------------------------------------------------
        // Event handlers
        // -----------------------------------------------------------------------

        private void OnNewGameCreatedPartialFollowUpEnd(CampaignGameStarter starter)
        {
            LogHelper.Info("OnNewGameCreatedPartialFollowUpEndEvent fired.");
            TryRunFragmentation("OnNewGameCreatedPartialFollowUpEnd");
        }

        private void OnGameLoadFinished()
        {
            LogHelper.Info("OnGameLoadFinishedEvent fired.");
            var settings = KingdomFragmentationSettings.Instance;
            if (settings != null && settings.NewCampaignsOnly)
            {
                LogHelper.Info("NewCampaignsOnly = true and this is a loaded save. Skipping.");
                return;
            }
            TryRunFragmentation("OnGameLoadFinished");
        }

        private void TryRunFragmentation(string trigger)
        {
            var settings = KingdomFragmentationSettings.Instance;

            if (settings == null)
                LogHelper.Warn("Settings not available — using defaults.");

            if (settings != null && !settings.EnableFragmentation)
            {
                LogHelper.Info("Kingdom Fragmentation disabled in MCM. Skipping.");
                return;
            }

            if (_fragmentationApplied)
            {
                LogHelper.Info("Fragmentation already applied. Skipping.");
                return;
            }

            LogHelper.Info("Starting fragmentation (trigger: " + trigger + ").");

            try
            {
                var engine = new FragmentationEngine(settings);
                engine.Run();

                _fragmentationApplied = true;
                _fragmentationCampaignDay = (float)CampaignTime.Now.ToDays;

                // Persist assignments for enforcement
                _assignedClanIds.Clear();
                _assignedKingdomIds.Clear();
                foreach (var kvp in engine.ClanKingdomAssignments)
                {
                    _assignedClanIds.Add(kvp.Key);
                    _assignedKingdomIds.Add(kvp.Value);
                }

                LogHelper.Info("Fragmentation complete on day "
                    + _fragmentationCampaignDay + ". "
                    + _assignedClanIds.Count + " assignment(s) recorded.");
            }
            catch (Exception ex)
            {
                LogHelper.Error("Fragmentation error: " + ex.Message);
                LogHelper.Warn("Partial changes may have been applied.");
            }
        }

        // -----------------------------------------------------------------------
        // Daily tick — truce + defection enforcement
        // -----------------------------------------------------------------------

        private void OnDailyTick()
        {
            if (!_fragmentationApplied || _fragmentationCampaignDay < 0f)
                return;

            var settings = KingdomFragmentationSettings.Instance;
            if (settings == null) return;

            float daysSince = (float)CampaignTime.Now.ToDays - _fragmentationCampaignDay;

            // Truce enforcement
            int truceDays = settings.StartingTruceDays;
            if (truceDays > 0 && daysSince < truceDays)
                EnforceTruce();

            // Defection lockout
            int lockoutDays = settings.DefectionLockoutDays;
            if (lockoutDays > 0 && daysSince < lockoutDays)
                EnforceClanLoyalty();
        }

        // -----------------------------------------------------------------------
        // Enforcement
        // -----------------------------------------------------------------------

        private static void EnforceTruce()
        {
            var kfKingdoms = Kingdom.All
                .Where(k => k != null && !k.IsEliminated
                    && k.StringId != null
                    && k.StringId.StartsWith("kf_", StringComparison.OrdinalIgnoreCase))
                .ToArray();

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
                            MakePeaceAction.Apply(a, b);
                            LogHelper.Debug("Truce: peace between '"
                                + a.Name + "' and '" + b.Name + "'.");
                        }
                        catch (Exception ex)
                        {
                            LogHelper.Warn("Truce failed: " + ex.Message);
                        }
                    }
                }
            }
        }

        private void EnforceClanLoyalty()
        {
            if (_assignedClanIds.Count == 0) return;

            var clanById = new Dictionary<string, Clan>();
            foreach (var c in Clan.All)
            {
                if (c != null && c.StringId != null && !c.IsEliminated)
                    clanById[c.StringId] = c;
            }

            var kingdomById = new Dictionary<string, Kingdom>();
            foreach (var k in Kingdom.All)
            {
                if (k != null && k.StringId != null && !k.IsEliminated)
                    kingdomById[k.StringId] = k;
            }

            int count = Math.Min(_assignedClanIds.Count, _assignedKingdomIds.Count);
            for (int i = 0; i < count; i++)
            {
                try
                {
                    string clanId = _assignedClanIds[i];
                    string kingdomId = _assignedKingdomIds[i];
                    if (string.IsNullOrEmpty(clanId) || string.IsNullOrEmpty(kingdomId))
                        continue;

                    if (!clanById.TryGetValue(clanId, out var clan)) continue;
                    if (clan == Clan.PlayerClan) continue;
                    if (!kingdomById.TryGetValue(kingdomId, out var kingdom)) continue;

                    if (clan.Kingdom != kingdom)
                    {
                        LogHelper.Debug("Loyalty: returning '" + clan.Name
                            + "' to '" + kingdom.Name + "'.");
                        try
                        {
                            ChangeKingdomAction.ApplyByJoinToKingdom(
                                clan, kingdom, CampaignTime.Now, false);
                        }
                        catch
                        {
                            clan.Kingdom = kingdom;
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogHelper.Warn("Loyalty enforcement error: " + ex.Message);
                }
            }
        }
    }
}
