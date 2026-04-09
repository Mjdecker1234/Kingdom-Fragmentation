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
    /// campaign.  Uses <see cref="CampaignEvents.OnNewGameCreatedPartialFollowUpEndEvent"/>
    /// so that all vanilla initialization (settlements, clans, heroes) has already
    /// completed before we start moving clans around.
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

        // -----------------------------------------------------------------------
        // CampaignBehaviorBase
        // -----------------------------------------------------------------------

        public override void RegisterEvents()
        {
            // Fires after all partial follow-ups on NEW campaign creation.
            // This is the safest hook: vanilla data is fully initialised and no
            // tutorial screens have been shown yet.
            CampaignEvents.OnNewGameCreatedPartialFollowUpEndEvent.AddNonSerializedListener(
                this,
                new System.Action<CampaignGameStarter>(OnNewGameCreatedPartialFollowUpEnd));

            // Daily tick: enforces time-gated settings such as StartingTruceDays.
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(
                this,
                new System.Action(OnDailyTick));
        }

        public override void SyncData(IDataStore dataStore)
        {
            dataStore.SyncData("KF_FragmentationApplied",    ref _fragmentationApplied);
            dataStore.SyncData("KF_FragmentationCampaignDay", ref _fragmentationCampaignDay);
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

            // NewCampaignsOnly guard:
            // This event hook (OnNewGameCreatedPartialFollowUpEnd) only fires during
            // new-game creation, never when loading a save.  When the setting is true
            // we enforce this at the hook level (it is already enforced by hook
            // selection).  When the setting is false, the _fragmentationApplied save
            // flag still prevents the engine from running more than once.
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
                    LogHelper.Info("Kingdom Fragmentation: process complete on campaign day "
                        + _fragmentationCampaignDay + ".");
                }
                else
                {
                    LogHelper.Info("[DRY RUN] Fragmentation simulation complete. See log for details.");
                }
            }
            catch (System.Exception ex)
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
            // Re-enforce peace between KF-created kingdoms for the configured
            // truce window.  AllWar mode is excluded; a truce contradicts war.
            int truceDays = settings.StartingTruceDays;
            string diplomacyMode = settings.DiplomacyMode?.SelectedValue ?? "AllPeace";

            if (truceDays > 0 && daysSince < truceDays && diplomacyMode != "AllWar")
            {
                EnforceTruceBetweenFragmentedKingdoms();
            }
        }

        /// <summary>
        /// Finds all KF-created kingdoms (identified by their "kf_" StringId prefix)
        /// and ensures no two of them are currently at war.
        /// </summary>
        private static void EnforceTruceBetweenFragmentedKingdoms()
        {
            var kfKingdoms = Kingdom.All
                .Where(k => !k.IsEliminated
                         && k.StringId != null
                         && k.StringId.StartsWith("kf_",
                            System.StringComparison.OrdinalIgnoreCase))
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
                        catch (System.Exception ex)
                        {
                            LogHelper.Warn("Truce enforcement failed between '"
                                + a.Name + "' and '" + b.Name + "': " + ex.Message);
                        }
                    }
                }
            }
        }

        // -----------------------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------------------

        private static void LogAdvisorySettings(KingdomFragmentationSettings? settings)
        {
            if (settings == null) return;

            const string harmonyNote = " (advisory — full enforcement requires an optional Harmony patch)";

            if (settings.JoinGracePeriodDays > 0)
                LogHelper.Info("JoinGracePeriodDays = " + settings.JoinGracePeriodDays + harmonyNote);

            if (settings.DefectionLockoutDays > 0)
                LogHelper.Info("DefectionLockoutDays = " + settings.DefectionLockoutDays + harmonyNote);

            if (settings.AntiCollapseProtectionDays > 0)
                LogHelper.Info("AntiCollapseProtectionDays = " + settings.AntiCollapseProtectionDays + harmonyNote);

            if (settings.DisableDiplomacyDays > 0)
                LogHelper.Info("DisableDiplomacyDays = " + settings.DisableDiplomacyDays + harmonyNote);
        }
    }
}
