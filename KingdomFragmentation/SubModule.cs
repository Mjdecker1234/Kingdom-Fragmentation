using KingdomFragmentation.Behaviors;
using KingdomFragmentation.Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace KingdomFragmentation
{
    /// <summary>
    /// Bannerlord sub-module entry point for Kingdom Fragmentation.
    /// Registers the <see cref="KingdomFragmentationBehavior"/> into every campaign
    /// (new game and loaded save) so that events are wired before the campaign
    /// system invokes <c>RegisterEvents</c> on all behaviours.
    /// </summary>
    public sealed class SubModule : MBSubModuleBase
    {
        // -----------------------------------------------------------------------
        // MBSubModuleBase overrides
        // -----------------------------------------------------------------------

        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();
            LogHelper.Info("Kingdom Fragmentation sub-module loaded.");
        }

        protected override void OnBeforeInitialModuleScreenSetAsRoot()
        {
            base.OnBeforeInitialModuleScreenSetAsRoot();
            LogHelper.Info("Kingdom Fragmentation: main menu ready.");
        }

        /// <summary>
        /// Called for every campaign start — both new games and loaded saves.
        /// We register the behaviour here (not inside an event callback) so that
        /// <see cref="KingdomFragmentationBehavior.RegisterEvents"/> is invoked
        /// during the normal campaign-system initialisation pass, guaranteeing
        /// that <c>OnNewGameCreatedPartialFollowUpEndEvent</c> fires correctly
        /// for Sandbox and Story Mode alike.
        /// </summary>
        public override void OnCampaignStart(Game game, object starterObject)
        {
            base.OnCampaignStart(game, starterObject);

            if (game.GameType is Campaign)
            {
                var starter = starterObject as CampaignGameStarter;
                if (starter != null)
                {
                    LogHelper.Info("Kingdom Fragmentation: adding campaign behavior.");
                    starter.AddBehavior(new KingdomFragmentationBehavior());
                }
                else
                {
                    LogHelper.Warn("Kingdom Fragmentation: could not obtain CampaignGameStarter.");
                }
            }
        }
    }
}
