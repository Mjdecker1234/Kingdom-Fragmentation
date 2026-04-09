using System.Linq;
using KingdomFragmentation.Helpers;
using KingdomFragmentation.Settings;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;

namespace KingdomFragmentation.Logic
{
    /// <summary>
    /// Ensures that all settlements belonging to a clan are correctly associated
    /// with the clan's newly created kingdom.
    /// </summary>
    public sealed class SettlementReassigner
    {
        private readonly KingdomFragmentationSettings? _settings;

        public SettlementReassigner(KingdomFragmentationSettings? settings)
        {
            _settings = settings;
        }

        // -----------------------------------------------------------------------
        // Public API
        // -----------------------------------------------------------------------

        /// <summary>
        /// Transfers ownership / kingdom affiliation of every settlement that
        /// belongs to <paramref name="clan"/> to <paramref name="newKingdom"/>.
        /// </summary>
        public void Reassign(Clan clan, Kingdom newKingdom)
        {
            if (!(_settings?.PreserveSettlementOwnership ?? true))
            {
                LogHelper.Debug("  SettlementReassigner: preserve-ownership disabled — skipping.");
                return;
            }

            if (clan == null || newKingdom == null)
                return;

            var ownedSettlements = clan.Settlements.ToList();

            if (ownedSettlements.Count == 0)
            {
                LogHelper.Debug("  SettlementReassigner: clan '" + clan.Name + "' has no settlements.");
                return;
            }

            LogHelper.Debug(
                "  SettlementReassigner: processing " + ownedSettlements.Count
                + " settlement(s) for clan '" + clan.Name + "'.");

            foreach (var settlement in ownedSettlements)
            {
                if (settlement == null || settlement.IsVillage)
                {
                    // Villages are bound to a town/castle — they follow automatically.
                    continue;
                }

                // The settlement should already belong to this clan after the clan
                // was moved into the new kingdom via ChangeKingdomAction.  We verify
                // and fix any edge cases where the owning map-faction differs.
                if (settlement.MapFaction != newKingdom)
                {
                    LogHelper.Debug(
                        "    Fixing settlement '" + settlement.Name
                        + "' — map faction was '" + settlement.MapFaction?.Name + "'.");

                    try
                    {
                        ChangeOwnerOfSettlementAction.ApplyByKingdomDecision(
                            clan.Leader,
                            settlement);
                    }
                    catch (System.Exception ex)
                    {
                        LogHelper.Warn(
                            "    Could not reassign settlement '" + settlement.Name
                            + "': " + ex.Message);
                    }
                }
                else
                {
                    LogHelper.Debug(
                        "    Settlement '" + settlement.Name + "' already in correct kingdom.");
                }
            }
        }
    }
}
