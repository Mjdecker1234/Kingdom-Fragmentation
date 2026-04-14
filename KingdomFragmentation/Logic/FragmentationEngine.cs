using System;
using System.Collections.Generic;
using System.Linq;
using KingdomFragmentation.Helpers;
using KingdomFragmentation.Settings;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Settlements;

namespace KingdomFragmentation.Logic
{
    /// <summary>
    /// Top-level orchestrator.  Counts map fiefs, creates kingdoms, distributes
    /// clans and fiefs among them.
    /// </summary>
    public sealed class FragmentationEngine
    {
        private readonly KingdomFragmentationSettings? _settings;

        /// <summary>
        /// After <see cref="Run"/> completes this contains every
        /// clan → kingdom assignment so the behaviour can persist and enforce it.
        /// </summary>
        public Dictionary<string, string> ClanKingdomAssignments { get; }
            = new Dictionary<string, string>();

        public FragmentationEngine(KingdomFragmentationSettings? settings)
        {
            _settings = settings;
        }

        /// <summary>
        /// Execute the full fragmentation pipeline.
        /// </summary>
        public void Run()
        {
            // ---- Step 1: Count map fiefs ------------------------------------
            var towns = Settlement.All
                .Where(s => s != null && s.IsTown)
                .ToList();
            var castles = Settlement.All
                .Where(s => s != null && s.IsCastle)
                .ToList();
            int totalFiefs = towns.Count + castles.Count;

            LogHelper.Info("Map has " + towns.Count + " towns, "
                + castles.Count + " castles (" + totalFiefs + " total fiefs).");

            if (totalFiefs == 0)
            {
                LogHelper.Error("No towns or castles on the map — nothing to do.");
                return;
            }

            // ---- Step 2: Determine actual kingdom count ---------------------
            int desired = _settings?.DesiredKingdomCount ?? 10;
            int fiefsPerKingdom = _settings?.FiefsPerKingdom ?? 5;
            int clansPerKingdom = _settings?.ClansPerKingdom ?? 3;
            int heroesPerClan   = _settings?.HeroesPerClan   ?? 5;

            int actualKingdoms = desired;
            if (fiefsPerKingdom > 0)
                actualKingdoms = Math.Min(desired, totalFiefs / fiefsPerKingdom);
            actualKingdoms = Math.Max(1, Math.Min(actualKingdoms, totalFiefs));

            LogHelper.Info("Desired kingdoms: " + desired
                + ", fiefs/kingdom: " + (fiefsPerKingdom > 0 ? fiefsPerKingdom.ToString() : "auto")
                + ", actual kingdoms: " + actualKingdoms + ".");

            // ---- Step 3: Collect existing noble clans -----------------------
            var existingClans = CollectNobleClans();
            LogHelper.Info("Found " + existingClans.Count + " existing noble clans.");

            if (existingClans.Count == 0)
            {
                LogHelper.Error("No eligible clans found on the map — cannot create kingdoms.");
                return;
            }

            // ---- Step 4: Pick leader clans ----------------------------------
            // Sort: highest tier first, then most fiefs, then most renown
            var sorted = existingClans
                .OrderByDescending(c => c.Tier)
                .ThenByDescending(c => c.Settlements.Count(s => s.IsTown || s.IsCastle))
                .ThenByDescending(c => c.Renown)
                .ToList();

            int leaderCount = Math.Min(actualKingdoms, sorted.Count);
            var leaderClans = sorted.Take(leaderCount).ToList();
            var remainingClans = sorted.Skip(leaderCount).ToList();

            LogHelper.Info("Selected " + leaderClans.Count + " leader clans, "
                + remainingClans.Count + " remaining clans for distribution.");

            // ---- Step 5: Create kingdoms ------------------------------------
            var creator = new KingdomCreator(_settings);
            var newKingdoms = new List<Kingdom>();

            foreach (var clan in leaderClans)
            {
                try
                {
                    // If the clan already has a sole kingdom, reuse it
                    if (clan.Kingdom != null && clan.Kingdom.Clans.Count == 1
                        && clan.Kingdom.RulingClan == clan)
                    {
                        LogHelper.Debug("Clan '" + clan.Name + "' already leads a solo kingdom — reusing.");
                        newKingdoms.Add(clan.Kingdom);
                        RecordAssignment(clan, clan.Kingdom);
                        continue;
                    }

                    var kingdom = creator.CreateForClan(clan);
                    if (kingdom != null)
                    {
                        newKingdoms.Add(kingdom);
                        RecordAssignment(clan, kingdom);
                    }
                    else
                    {
                        LogHelper.Warn("Failed to create kingdom for clan '" + clan.Name + "'.");
                    }
                }
                catch (Exception ex)
                {
                    LogHelper.Error("Error creating kingdom for '" + clan.Name + "': " + ex.Message);
                }
            }

            if (newKingdoms.Count == 0)
            {
                LogHelper.Error("No kingdoms were created — aborting.");
                return;
            }

            LogHelper.Info("Created " + newKingdoms.Count + " kingdoms.");

            // ---- Step 6: Distribute remaining clans -------------------------
            int clanSlots = (clansPerKingdom - 1) * newKingdoms.Count; // leaders already placed
            int clansNeeded = Math.Max(0, clanSlots - remainingClans.Count);

            // Generate extra clans if we don't have enough existing ones
            if (clansNeeded > 0)
            {
                LogHelper.Info("Need " + clansNeeded + " additional clans — generating.");
                var generator = new ProceduralGenerationEngine();
                var extraClans = generator.GenerateClans(
                    clansNeeded, heroesPerClan, existingClans);
                remainingClans.AddRange(extraClans);
                LogHelper.Info("Generated " + extraClans.Count + " supplemental clans.");
            }

            // Assign clans round-robin to kingdoms
            int clanIndex = 0;
            for (int round = 0; round < clansPerKingdom - 1; round++)
            {
                foreach (var kingdom in newKingdoms)
                {
                    if (clanIndex >= remainingClans.Count) break;
                    var clan = remainingClans[clanIndex++];
                    AssignClanToKingdom(clan, kingdom);
                }
            }

            // Any leftover clans → round-robin
            while (clanIndex < remainingClans.Count)
            {
                var kingdom = newKingdoms[clanIndex % newKingdoms.Count];
                var clan = remainingClans[clanIndex++];
                AssignClanToKingdom(clan, kingdom);
            }

            // Top up heroes in all clans if needed
            if (heroesPerClan > 1)
            {
                var generator = new ProceduralGenerationEngine();
                foreach (var kingdom in newKingdoms)
                {
                    foreach (var clan in kingdom.Clans.ToList())
                    {
                        int currentHeroes = clan.Heroes.Count(h => h != null && h.IsAlive);
                        int needed = heroesPerClan - currentHeroes;
                        if (needed > 0)
                        {
                            generator.GenerateHeroesForClan(clan, needed);
                        }
                    }
                }
            }

            // ---- Step 7: Distribute fiefs -----------------------------------
            var allFiefs = new List<Settlement>();
            // Towns first (more valuable), then castles
            allFiefs.AddRange(towns.OrderBy(t => t.StringId));
            allFiefs.AddRange(castles.OrderBy(c => c.StringId));

            int perKingdom = fiefsPerKingdom > 0
                ? fiefsPerKingdom
                : (int)Math.Ceiling((double)totalFiefs / newKingdoms.Count);

            int fiefIndex = 0;
            foreach (var kingdom in newKingdoms)
            {
                int assigned = 0;
                while (assigned < perKingdom && fiefIndex < allFiefs.Count)
                {
                    var fief = allFiefs[fiefIndex++];
                    AssignFiefToKingdom(fief, kingdom);
                    assigned++;
                }
                LogHelper.Debug("Assigned " + assigned + " fiefs to '" + kingdom.Name + "'.");
            }

            // Remaining fiefs → round-robin
            int kingdomIdx = 0;
            while (fiefIndex < allFiefs.Count)
            {
                var kingdom = newKingdoms[kingdomIdx % newKingdoms.Count];
                var fief = allFiefs[fiefIndex++];
                AssignFiefToKingdom(fief, kingdom);
                kingdomIdx++;
            }

            // ---- Step 8: Diplomacy — AllPeace -------------------------------
            ApplyAllPeace(newKingdoms);

            // ---- Done -------------------------------------------------------
            LogSummary(newKingdoms);
        }

        // =====================================================================
        // Helpers
        // =====================================================================

        private List<Clan> CollectNobleClans()
        {
            var result = new List<Clan>();
            foreach (var clan in Clan.All)
            {
                if (clan == null || clan.IsEliminated) continue;
                if (clan.Leader == null || clan.Leader.IsDead) continue;
                if (clan == Clan.PlayerClan) continue;
                if (clan.IsMinorFaction) continue;
                if (clan.IsUnderMercenaryService) continue;
                if (clan.IsBanditFaction) continue;
                if (!clan.IsNoble) continue;
                result.Add(clan);
            }
            return result;
        }

        private void AssignClanToKingdom(Clan clan, Kingdom kingdom)
        {
            try
            {
                if (clan.Kingdom == kingdom)
                {
                    RecordAssignment(clan, kingdom);
                    return;
                }

                // Try the proper action first
                try
                {
                    ChangeKingdomAction.ApplyByJoinToKingdom(clan, kingdom, CampaignTime.Now, false);
                }
                catch
                {
                    // Fallback: direct assignment
                    clan.Kingdom = kingdom;
                }

                RecordAssignment(clan, kingdom);
                LogHelper.Debug("Assigned clan '" + clan.Name + "' to kingdom '" + kingdom.Name + "'.");
            }
            catch (Exception ex)
            {
                LogHelper.Error("Failed to assign clan '" + clan.Name + "' to '"
                    + kingdom.Name + "': " + ex.Message);
            }
        }

        private static void AssignFiefToKingdom(Settlement fief, Kingdom kingdom)
        {
            if (fief == null || kingdom == null) return;
            var rulingClan = kingdom.RulingClan;
            if (rulingClan?.Leader == null)
            {
                LogHelper.Warn("Kingdom '" + kingdom.Name + "' has no ruling clan/leader — cannot assign fief.");
                return;
            }

            // If the fief already belongs to a clan in this kingdom, leave it
            if (fief.OwnerClan?.Kingdom == kingdom)
                return;

            try
            {
                ChangeOwnerOfSettlementAction.ApplyByDefault(rulingClan.Leader, fief);
            }
            catch (Exception ex)
            {
                LogHelper.Warn("Could not reassign fief '" + fief.Name + "': " + ex.Message
                    + " — trying ApplyByKingDecision.");
                try
                {
                    ChangeOwnerOfSettlementAction.ApplyByKingDecision(rulingClan.Leader, fief);
                }
                catch (Exception ex2)
                {
                    LogHelper.Error("ApplyByKingDecision also failed for '" + fief.Name
                        + "': " + ex2.Message);
                }
            }
        }

        private static void ApplyAllPeace(List<Kingdom> kingdoms)
        {
            for (int i = 0; i < kingdoms.Count; i++)
            {
                for (int j = i + 1; j < kingdoms.Count; j++)
                {
                    var a = kingdoms[i];
                    var b = kingdoms[j];
                    if (a == null || b == null) continue;
                    if (FactionManager.IsAtWarAgainstFaction(a, b))
                    {
                        try
                        {
                            MakePeaceAction.Apply(a, b);
                        }
                        catch (Exception ex)
                        {
                            LogHelper.Warn("Could not make peace between '"
                                + a.Name + "' and '" + b.Name + "': " + ex.Message);
                        }
                    }
                }
            }
            LogHelper.Info("Applied AllPeace between " + kingdoms.Count + " kingdoms.");
        }

        private void LogSummary(List<Kingdom> newKingdoms)
        {
            int totalAssignedFiefs = 0;
            int totalClans = 0;
            foreach (var kingdom in newKingdoms)
            {
                int fiefs = kingdom.Clans.Sum(c =>
                    c.Settlements.Count(s => s.IsTown || s.IsCastle));
                totalAssignedFiefs += fiefs;
                totalClans += kingdom.Clans.Count;
                LogHelper.Info("Kingdom '" + kingdom.Name + "': "
                    + kingdom.Clans.Count + " clans, " + fiefs + " fiefs.");
            }
            LogHelper.Info("Fragmentation complete: " + newKingdoms.Count + " kingdoms, "
                + totalClans + " clans, " + totalAssignedFiefs + " fiefs assigned.");
        }

        private void RecordAssignment(Clan clan, Kingdom kingdom)
        {
            if (clan?.StringId != null && kingdom?.StringId != null)
                ClanKingdomAssignments[clan.StringId] = kingdom.StringId;
        }
    }
}
