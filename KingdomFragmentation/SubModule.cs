using KingdomFragmentation.Behaviors;
using KingdomFragmentation.Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace KingdomFragmentation
{
    /// <summary>
    /// Bannerlord sub-module entry point for Kingdom Fragmentation.
    /// Registers the <see cref="KingdomFragmentationBehavior"/> into every new campaign.
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

        public override void OnCampaignStart(Game game, object starterObject)
        {
            base.OnCampaignStart(game, starterObject);

            if (game.GameType is Campaign)
            {
                LogHelper.Info("Kingdom Fragmentation: registering campaign behavior.");
                CampaignEvents.OnNewGameCreatedEvent.AddNonSerializedListener(
                    this,
                    new System.Action<CampaignGameStarter>(OnNewGameCreated));
            }
        }

        // -----------------------------------------------------------------------
        // Internal helpers
        // -----------------------------------------------------------------------

        private static void OnNewGameCreated(CampaignGameStarter starter)
        {
            starter.AddBehavior(new KingdomFragmentationBehavior());
        }
    }
}
