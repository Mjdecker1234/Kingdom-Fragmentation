using System;
using System.Collections.Generic;
using System.Linq;
using KingdomFragmentation.Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Settlements;

namespace KingdomFragmentation.Logic
{
    /// <summary>
    /// Post-fragmentation pass that verifies every town, castle, and bound village
    /// reports the correct owning kingdom and owning clan after redistribution.
    /// Any mismatch is repaired using the safest available action API.
    /// </summary>
    public static class SettlementReassigner
    {
        /// <summary>
        /// Verifies and repairs settlement ownership for all kingdoms in
        /// <paramref name="kingdoms"/>.  Call this after fiefs have been
        /// distributed but before diplomacy is applied.
        /// </summary>
        public static void Verify(IEnumerable<Kingdom> kingdoms)
        {
            if (kingdoms == null) return;

            int repaired = 0;
            int verified = 0;

            foreach (var kingdom in kingdoms)
            {
                if (kingdom == null || kingdom.IsEliminated) continue;

                foreach (var clan in kingdom.Clans.ToList())
                {
                    if (clan == null || clan.IsEliminated || clan.Leader == null) continue;

                    foreach (var settlement in clan.Settlements.ToList())
                    {
                        if (settlement == null) continue;

                        if (!settlement.IsTown && !settlement.IsCastle && !settlement.IsVillage)
                            continue;

                        verified++;

                        // Check kingdom affiliation — should match the clan's kingdom
                        if (settlement.MapFaction != kingdom)
                        {
                            if (TryRepairSettlement(settlement, clan, kingdom))
                                repaired++;
                            else
                                LogHelper.Warn("SettlementReassigner: could not repair '"
                                    + settlement.Name + "' (owner clan: " + clan.Name
                                    + ", expected kingdom: " + kingdom.Name + ").");
                        }
                    }
                }

                // Also check towns/castles whose OwnerClan is in this kingdom
                // but MapFaction differs — can happen with direct Town.OwnerClan assignment
                foreach (var settlement in Settlement.All)
                {
                    if (settlement == null) continue;
                    if (!settlement.IsTown && !settlement.IsCastle) continue;

                    var ownerClan = settlement.OwnerClan;
                    if (ownerClan == null) continue;
                    if (ownerClan.Kingdom != kingdom) continue;
                    if (settlement.MapFaction == kingdom) continue;

                    verified++;
                    if (TryRepairSettlement(settlement, ownerClan, kingdom))
                        repaired++;
                    else
                        LogHelper.Warn("SettlementReassigner: MapFaction mismatch on '"
                            + settlement.Name + "', kingdom=" + kingdom.Name + ".");
                }

                // Ensure bound villages follow their parent fief
                RepairBoundVillages(kingdom);
            }

            if (repaired > 0)
                LogHelper.Info("SettlementReassigner: verified " + verified
                    + " settlement(s), repaired " + repaired + " mismatch(es).");
            else
                LogHelper.Info("SettlementReassigner: verified " + verified
                    + " settlement(s) — no mismatches found.");
        }

        // =====================================================================

        private static bool TryRepairSettlement(Settlement settlement, Clan targetClan, Kingdom kingdom)
        {
            // Try the proper API first
            try
            {
                if (targetClan.Leader != null)
                {
                    ChangeOwnerOfSettlementAction.ApplyByDefault(targetClan.Leader, settlement);
                    if (settlement.MapFaction == kingdom)
                        return true;
                }
            }
            catch (Exception ex)
            {
                LogHelper.Debug("SettlementReassigner: ApplyByDefault failed for '"
                    + settlement.Name + "': " + ex.Message);
            }

            // KingDecision fallback
            try
            {
                if (targetClan.Leader != null)
                {
                    ChangeOwnerOfSettlementAction.ApplyByKingDecision(targetClan.Leader, settlement);
                    if (settlement.MapFaction == kingdom)
                        return true;
                }
            }
            catch (Exception ex)
            {
                LogHelper.Debug("SettlementReassigner: ApplyByKingDecision failed for '"
                    + settlement.Name + "': " + ex.Message);
            }

            // Direct assignment as a last resort — sets OwnerClan which cascades MapFaction
            try
            {
                if (settlement.Town != null)
                {
                    settlement.Town.OwnerClan = targetClan;
                    return settlement.MapFaction == kingdom;
                }
            }
            catch (Exception ex)
            {
                LogHelper.Debug("SettlementReassigner: direct OwnerClan set failed for '"
                    + settlement.Name + "': " + ex.Message);
            }

            return false;
        }

        private static void RepairBoundVillages(Kingdom kingdom)
        {
            foreach (var settlement in Settlement.All)
            {
                if (settlement == null || !settlement.IsTown && !settlement.IsCastle) continue;
                if (settlement.MapFaction != kingdom) continue;
                if (settlement.BoundVillages == null) continue;

                foreach (var village in settlement.BoundVillages)
                {
                    if (village?.Settlement == null) continue;
                    if (village.Settlement.MapFaction == kingdom) continue;

                    try
                    {
                        // Villages should follow their bound parent — use direct set
                        if (settlement.OwnerClan?.Leader != null)
                            ChangeOwnerOfSettlementAction.ApplyByDefault(
                                settlement.OwnerClan.Leader, village.Settlement);
                    }
                    catch (Exception ex)
                    {
                        LogHelper.Debug("SettlementReassigner: village repair failed for '"
                            + village.Settlement.Name + "': " + ex.Message);
                    }
                }
            }
        }
    }
}
