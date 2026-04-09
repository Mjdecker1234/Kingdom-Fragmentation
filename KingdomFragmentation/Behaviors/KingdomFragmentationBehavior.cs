using KingdomFragmentation.Helpers;
using KingdomFragmentation.Logic;
using KingdomFragmentation.Settings;
using TaleWorlds.CampaignSystem;

namespace KingdomFragmentation.Behaviors
{
    /// <summary>
    /// Campaign behavior that triggers kingdom fragmentation exactly once on a new
    /// campaign.  Uses <see cref="OnNewGameCreatedPartialFollowUpEnd"/> so that all
    /// vanilla initialization (settlements, clans, heroes) has already completed
    /// before we start moving clans around.
    /// </summary>
    public sealed class KingdomFragmentationBehavior : CampaignBehaviorBase
    {
        // -----------------------------------------------------------------------
        // State — persisted in the save so we only run once per campaign
        // -----------------------------------------------------------------------

        private bool _fragmentationApplied = false;

        // -----------------------------------------------------------------------
        // CampaignBehaviorBase
        // -----------------------------------------------------------------------

        public override void RegisterEvents()
        {
            // Fires after all partial follow-ups on NEW campaign creation.
            // This is the safest hook: vanilla data is fully initialised, no
            // tutorial screens have been shown yet.
            CampaignEvents.OnNewGameCreatedPartialFollowUpEndEvent.AddNonSerializedListener(
                this,
                new System.Action<CampaignGameStarter>(OnNewGameCreatedPartialFollowUpEnd));
        }

        public override void SyncData(IDataStore dataStore)
        {
            // Persist the "already applied" flag so that loading a save does not
            // re-run fragmentation.
            dataStore.SyncData("KF_FragmentationApplied", ref _fragmentationApplied);
        }

        // -----------------------------------------------------------------------
        // Event handler
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

            try
            {
                var engine = new FragmentationEngine(settings);
                engine.Run(isDryRun);

                if (!isDryRun)
                {
                    _fragmentationApplied = true;
                    LogHelper.Info("Kingdom Fragmentation: process complete.");
                }
                else
                {
                    LogHelper.Info("[DRY RUN] Fragmentation simulation complete. See log for details.");
                }
            }
            catch (System.Exception ex)
            {
                bool safeFallback = settings?.ErrorSafeFallback ?? true;
                LogHelper.Error($"Kingdom Fragmentation encountered an error: {ex.Message}");
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
    }
}
