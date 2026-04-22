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
        /// Called early in campaign initialisation — before the campaign system
        /// calls <c>RegisterEvents</c> on all behaviours.  This guarantees that
        /// <see cref="KingdomFragmentationBehavior.RegisterEvents"/> runs in
        /// time to wire <c>OnNewGameCreatedPartialFollowUpEndEvent</c>.
        /// </summary>
        protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
        {
            base.OnGameStart(game, gameStarterObject);

            if (game.GameType is Campaign)
            {
                var starter = gameStarterObject as CampaignGameStarter;
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
