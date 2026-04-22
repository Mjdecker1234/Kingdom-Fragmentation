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
    /// clans and fiefs among them — while preserving vanilla kingdoms.
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
            // Reset banner generator tracking for this run
            BannerGenerator.Reset();
            bool usePresetWorld = _settings?.UsePresetWorld ?? true;
            bool strictPresetCulture = _settings?.PresetStrictCulture ?? false;
            int archetypeAggressiveness = _settings?.ArchetypeAggressivenessPercent ?? 120;
            bool presetDebugReport = _settings?.PresetDebugReport ?? true;

            PresetAssignmentEngine? presetEngine = usePresetWorld
                ? new PresetAssignmentEngine(
                    strictPresetCulture,
                    archetypeAggressiveness,
                    presetDebugReport)
                : null;

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

            // ---- Step 2: Read settings --------------------------------------
            int desired = _settings?.DesiredKingdomCount ?? 10;
            int fiefsPerKingdom = _settings?.FiefsPerKingdom ?? 5;
            int clansPerKingdom = _settings?.ClansPerKingdom ?? 3;
            int heroesPerClan   = _settings?.HeroesPerClan   ?? 5;
            int retentionPercent = _settings?.OriginalKingdomRetentionPercent ?? 40;
            bool protectPlayerFiefs = _settings?.ProtectPlayerClanFiefs ?? true;
            bool cultureAware = _settings?.CultureAwareClanAssignment ?? true;
            bool geoFiefs = _settings?.GeographicFiefDistribution ?? true;
            bool distributeToVassals = _settings?.DistributeFiefsToVassals ?? true;
            int minFiefsPerOriginal = _settings?.MinFiefsPerOriginalKingdom ?? 3;
            int relationsBonus = _settings?.InitialRelationsBonus ?? 20;

            // ---- Step 3: Partition clans — protect vanilla kingdoms ----------
            var (availableClans, protectedClans, protectedFiefs) =
                PartitionNobleClans(retentionPercent, minFiefsPerOriginal, protectPlayerFiefs);

            LogHelper.Info("Partitioned clans: " + availableClans.Count + " available, "
                + protectedClans.Count + " protected in vanilla kingdoms.");

            if (availableClans.Count == 0)
            {
                LogHelper.Error("No eligible clans available for fragmentation — cannot create kingdoms.");
                return;
            }

            // ---- Step 4: Determine actual kingdom count ---------------------
            int actualKingdoms = desired;
            if (fiefsPerKingdom > 0)
            {
                int availableFiefCount = totalFiefs - protectedFiefs.Count;
                actualKingdoms = Math.Min(desired, availableFiefCount / Math.Max(1, fiefsPerKingdom));
            }
            actualKingdoms = Math.Max(1, Math.Min(actualKingdoms, availableClans.Count));

            LogHelper.Info("Desired kingdoms: " + desired
                + ", fiefs/kingdom: " + (fiefsPerKingdom > 0 ? fiefsPerKingdom.ToString() : "auto")
                + ", actual kingdoms: " + actualKingdoms + ".");

            // ---- Step 5: Pick leader clans ----------------------------------
            var sorted = availableClans
                .OrderByDescending(c => c.Tier)
                .ThenByDescending(c => c.Settlements.Count(s => s.IsTown || s.IsCastle))
                .ThenByDescending(c => c.Renown)
                .ToList();

            int leaderCount = Math.Min(actualKingdoms, sorted.Count);
            var leaderClans = sorted.Take(leaderCount).ToList();
            var remainingClans = sorted.Skip(leaderCount).ToList();

            LogHelper.Info("Selected " + leaderClans.Count + " leader clans, "
                + remainingClans.Count + " remaining clans for distribution.");

            // ---- Step 6: Create kingdoms ------------------------------------
            var creator = new KingdomCreator(_settings, presetEngine);
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

            // ---- Step 7: Distribute remaining clans -------------------------
            int clanSlots = (clansPerKingdom - 1) * newKingdoms.Count;
            int clansNeeded = Math.Max(0, clanSlots - remainingClans.Count);

            if (clansNeeded > 0)
            {
                LogHelper.Info("Need " + clansNeeded + " additional clans — generating.");
                var generator = new ProceduralGenerationEngine(presetEngine);
                var extraClans = generator.GenerateClans(
                    clansNeeded, heroesPerClan, availableClans);
                remainingClans.AddRange(extraClans);
                LogHelper.Info("Generated " + extraClans.Count + " supplemental clans.");
            }

            if (cultureAware)
            {
                DistributeClansWithCulture(remainingClans, newKingdoms, clansPerKingdom);
            }
            else
            {
                DistributeClansRoundRobin(remainingClans, newKingdoms, clansPerKingdom);
            }

            // Apply curated clan identities to all newly fragmented kingdoms.
            ApplyClanPresetsToNewKingdoms(newKingdoms, presetEngine);

            // Top up heroes in all clans if needed
            if (heroesPerClan > 1)
            {
                var generator = new ProceduralGenerationEngine(presetEngine);
                foreach (var kingdom in newKingdoms)
                {
                    foreach (var clan in kingdom.Clans.ToList())
                    {
                        int currentHeroes = clan.Heroes.Count(h => h != null && h.IsAlive);
                        int needed = heroesPerClan - currentHeroes;
                        if (needed > 0)
                            generator.GenerateHeroesForClan(clan, needed);
                    }
                }
            }

            // ---- Step 8: Distribute fiefs -----------------------------------
            var allFiefs = new List<Settlement>();
            allFiefs.AddRange(towns);
            allFiefs.AddRange(castles);

            // Remove protected fiefs from the redistribution pool
            var availableFiefs = allFiefs
                .Where(f => !protectedFiefs.Contains(f))
                .ToList();

            LogHelper.Info("Fiefs for distribution: " + availableFiefs.Count
                + " (protected: " + protectedFiefs.Count + ").");

            int perKingdom = fiefsPerKingdom > 0
                ? fiefsPerKingdom
                : (int)Math.Ceiling((double)availableFiefs.Count / newKingdoms.Count);

            if (geoFiefs)
                DistributeFiefsGeographic(availableFiefs, newKingdoms, perKingdom, distributeToVassals);
            else
                DistributeFiefsRoundRobin(availableFiefs, newKingdoms, perKingdom, distributeToVassals);

            // ---- Step 9: Settlement verification/repair --------------------
            SettlementReassigner.Verify(newKingdoms);

            // ---- Step 10: Diplomacy -----------------------------------------
            ApplyAllPeace(newKingdoms);

            // Apply initial relations bonus between same-culture kingdoms
            if (relationsBonus > 0)
                ApplyRelationsBonus(newKingdoms, relationsBonus);

            presetEngine?.EmitRunReport();

            // ---- Done -------------------------------------------------------
            LogSummary(newKingdoms);
        }

        // =====================================================================
        // Clan partitioning — protects vanilla kingdoms
        // =====================================================================

        private (List<Clan> available, HashSet<Clan> protected_, HashSet<Settlement> protectedFiefs)
            PartitionNobleClans(int retentionPercent, int minFiefsPerOriginal, bool protectPlayerFiefs)
        {
            var available = new List<Clan>();
            var protectedClans = new HashSet<Clan>();
            var protectedFiefs = new HashSet<Settlement>();

            // Identify vanilla (non-kf_) kingdoms
            var vanillaKingdoms = Kingdom.All
                .Where(k => k != null && !k.IsEliminated
                    && k.StringId != null
                    && !k.StringId.StartsWith("kf_", StringComparison.OrdinalIgnoreCase))
                .ToList();

            LogHelper.Info("Found " + vanillaKingdoms.Count + " vanilla kingdoms to preserve.");

            foreach (var kingdom in vanillaKingdoms)
            {
                var kingdomClans = kingdom.Clans
                    .Where(c => c != null && !c.IsEliminated
                        && c.Leader != null && !c.Leader.IsDead
                        && !c.IsMinorFaction && !c.IsBanditFaction
                        && !c.IsUnderMercenaryService)
                    .ToList();

                if (kingdomClans.Count == 0) continue;

                // Always protect the ruling clan
                var rulingClan = kingdom.RulingClan;
                int protectCount;

                if (retentionPercent >= 100)
                {
                    protectCount = kingdomClans.Count;
                }
                else if (retentionPercent <= 0)
                {
                    // Even at 0%, keep the ruling clan
                    protectCount = 1;
                }
                else
                {
                    protectCount = Math.Max(1,
                        (int)Math.Floor(kingdomClans.Count * retentionPercent / 100.0));
                }

                // Sort non-ruling clans by tier+renown to keep the best ones
                var sortedClans = kingdomClans
                    .Where(c => c != rulingClan)
                    .OrderByDescending(c => c.Tier)
                    .ThenByDescending(c => c.Renown)
                    .ToList();

                // Protect ruling clan + top (protectCount - 1) others
                if (rulingClan != null && kingdomClans.Contains(rulingClan))
                    protectedClans.Add(rulingClan);

                for (int i = 0; i < protectCount - 1 && i < sortedClans.Count; i++)
                    protectedClans.Add(sortedClans[i]);

                // Remaining clans go to the available pool
                foreach (var clan in kingdomClans)
                {
                    if (!protectedClans.Contains(clan) && clan != Clan.PlayerClan)
                        available.Add(clan);
                }

                LogHelper.Debug("Kingdom '" + kingdom.Name + "': "
                    + kingdomClans.Count + " clans, protecting "
                    + protectedClans.Count(c => kingdomClans.Contains(c)) + ".");
            }

            // Protect fiefs owned by protected clans
            foreach (var clan in protectedClans)
            {
                foreach (var s in clan.Settlements)
                {
                    if (s != null && (s.IsTown || s.IsCastle))
                        protectedFiefs.Add(s);
                }
            }

            // Ensure each vanilla kingdom has at least minFiefsPerOriginal protected fiefs
            foreach (var kingdom in vanillaKingdoms)
            {
                int kFiefs = protectedFiefs.Count(f =>
                    f.OwnerClan?.Kingdom == kingdom);
                if (kFiefs < minFiefsPerOriginal)
                {
                    // Find additional fiefs in this kingdom to protect
                    var extraFiefs = Settlement.All
                        .Where(s => s != null && (s.IsTown || s.IsCastle)
                            && s.OwnerClan?.Kingdom == kingdom
                            && !protectedFiefs.Contains(s))
                        .Take(minFiefsPerOriginal - kFiefs)
                        .ToList();
                    foreach (var f in extraFiefs)
                        protectedFiefs.Add(f);
                }
            }

            // Protect player clan fiefs if setting is on
            if (protectPlayerFiefs && Clan.PlayerClan != null)
            {
                foreach (var s in Clan.PlayerClan.Settlements)
                {
                    if (s != null && (s.IsTown || s.IsCastle))
                        protectedFiefs.Add(s);
                }
            }

            // Also collect unaffiliated noble clans (clanless kingdoms, etc.)
            foreach (var clan in Clan.All)
            {
                if (clan == null || clan.IsEliminated) continue;
                if (clan.Leader == null || clan.Leader.IsDead) continue;
                if (clan == Clan.PlayerClan) continue;
                if (clan.IsMinorFaction || clan.IsBanditFaction) continue;
                if (clan.IsUnderMercenaryService) continue;
                if (!clan.IsNoble) continue;
                if (protectedClans.Contains(clan)) continue;
                if (available.Contains(clan)) continue;
                // Clan not in any vanilla kingdom — add to available
                available.Add(clan);
            }

            return (available, protectedClans, protectedFiefs);
        }

        // =====================================================================
        // Clan distribution
        // =====================================================================

        private void DistributeClansWithCulture(List<Clan> clans, List<Kingdom> kingdoms, int clansPerKingdom)
        {
            var clansByCulture = new Dictionary<string, List<Clan>>();
            foreach (var clan in clans)
            {
                string cultureId = clan.Culture?.StringId ?? "unknown";
                if (!clansByCulture.ContainsKey(cultureId))
                    clansByCulture[cultureId] = new List<Clan>();
                clansByCulture[cultureId].Add(clan);
            }

            var unassigned = new List<Clan>(clans);

            // First pass: same-culture clans
            foreach (var kingdom in kingdoms)
            {
                string kingdomCultureId = kingdom.Culture?.StringId ?? "unknown";
                int slotsLeft = clansPerKingdom - kingdom.Clans.Count;

                if (slotsLeft > 0 && clansByCulture.ContainsKey(kingdomCultureId))
                {
                    var pool = clansByCulture[kingdomCultureId];
                    int toAssign = Math.Min(slotsLeft, pool.Count);
                    for (int i = 0; i < toAssign; i++)
                    {
                        var clan = pool[0];
                        pool.RemoveAt(0);
                        unassigned.Remove(clan);
                        AssignClanToKingdom(clan, kingdom);
                    }
                }
            }

            // Second pass: fill remaining round-robin
            DistributeClansRoundRobin(unassigned, kingdoms, clansPerKingdom);
        }

        private void DistributeClansRoundRobin(List<Clan> clans, List<Kingdom> kingdoms, int clansPerKingdom)
        {
            // Pass 1: respect the per-kingdom cap.
            int idx = 0;
            bool anyAssigned = true;
            while (idx < clans.Count && anyAssigned)
            {
                anyAssigned = false;
                foreach (var kingdom in kingdoms)
                {
                    if (idx >= clans.Count) break;
                    // Skip kingdoms that have already hit the cap.
                    if (clansPerKingdom > 0 && kingdom.Clans.Count >= clansPerKingdom) continue;
                    AssignClanToKingdom(clans[idx++], kingdom);
                    anyAssigned = true;
                }
            }

            // Pass 2: overflow — all kingdoms hit cap but clans remain.
            // Distribute round-robin without enforcing cap to avoid orphaned clans.
            if (idx < clans.Count)
            {
                LogHelper.Info("Clan overflow: " + (clans.Count - idx)
                    + " clan(s) exceed the per-kingdom target, distributing round-robin.");
                int kIdx = 0;
                while (idx < clans.Count)
                {
                    AssignClanToKingdom(clans[idx++], kingdoms[kIdx % kingdoms.Count]);
                    kIdx++;
                }
            }
        }

        // =====================================================================
        // Fief distribution
        // =====================================================================

        /// <summary>
        /// Town-first geographic distribution with farthest-first anchor seeding:
        ///  Phase 0: Seed anchors by spreading kingdoms across the map
        ///  Phase 1: Cluster-grow TOWNS only — each kingdom grabs nearest town to its centroid
        ///  Phase 2: Castles assigned to whichever kingdom owns the nearest town
        ///  Phase 3: Villages follow their bound parent (town/castle)
        /// </summary>
        private void DistributeFiefsGeographic(List<Settlement> fiefs, List<Kingdom> kingdoms,
            int perKingdom, bool distributeToVassals)
        {
            // Split fiefs into towns and castles
            var availableTowns = new List<Settlement>(fiefs.Where(f => f.IsTown));
            var availableCastles = new List<Settlement>(fiefs.Where(f => f.IsCastle));

            LogHelper.Info("Town-first distribution: " + availableTowns.Count + " towns, "
                + availableCastles.Count + " castles to distribute among "
                + kingdoms.Count + " kingdoms.");

            if (availableTowns.Count == 0)
            {
                LogHelper.Warn("No towns to distribute — using round-robin fallback.");
                DistributeFiefsRoundRobin(fiefs, kingdoms, perKingdom, distributeToVassals);
                return;
            }

            // ---- Phase 0: Farthest-first anchor seeding ----
            // Spread kingdom anchors across available towns for maximum geographic spacing.
            // This prevents all anchors from clustering in one map area.
            var clusters = new Dictionary<Kingdom, List<Settlement>>();
            var anchorTowns = new List<Settlement>();
            var townPool = new List<Settlement>(availableTowns);

            foreach (var kingdom in kingdoms)
                clusters[kingdom] = new List<Settlement>();

            for (int k = 0; k < kingdoms.Count && townPool.Count > 0; k++)
            {
                Settlement pick;
                if (k == 0)
                {
                    // First anchor: pick a central-ish town (median position)
                    pick = PickCentralTown(townPool);
                }
                else
                {
                    // Subsequent anchors: pick the town farthest from all existing anchors
                    pick = PickFarthestTown(townPool, anchorTowns);
                }

                anchorTowns.Add(pick);
                townPool.Remove(pick);
                availableTowns.Remove(pick);
                clusters[kingdoms[k]].Add(pick);
            }

            LogHelper.Info("Seeded " + anchorTowns.Count + " anchor towns across the map.");

            // ---- Phase 1: Cluster-grow with TOWNS only ----
            int townsPerKingdom = availableTowns.Count > 0
                ? (int)Math.Ceiling((double)(availableTowns.Count + anchorTowns.Count) / kingdoms.Count)
                : 1;
            townsPerKingdom = Math.Max(1, townsPerKingdom);

            bool progress = true;
            while (progress && availableTowns.Count > 0)
            {
                progress = false;
                foreach (var kingdom in kingdoms)
                {
                    if (clusters[kingdom].Count(s => s.IsTown) >= townsPerKingdom) continue;
                    if (availableTowns.Count == 0) break;

                    // Find the nearest available town to this kingdom's cluster
                    var centroidSettlement = ComputeCentroidSettlement(clusters[kingdom]);
                    float bestDist = float.MaxValue;
                    Settlement? best = null;

                    foreach (var town in availableTowns)
                    {
                        float dist = centroidSettlement != null
                            ? town.GatePosition.Distance(centroidSettlement.GatePosition)
                            : float.MaxValue;
                        // Culture tiebreaker: 10% bonus for matching culture
                        if (kingdom.Culture?.StringId != null
                            && town.Culture?.StringId == kingdom.Culture.StringId)
                        {
                            dist *= 0.9f;
                        }
                        if (dist < bestDist)
                        {
                            bestDist = dist;
                            best = town;
                        }
                    }

                    if (best != null)
                    {
                        availableTowns.Remove(best);
                        clusters[kingdom].Add(best);
                        progress = true;
                    }
                }
            }

            // Any remaining unassigned towns go to nearest kingdom by centroid
            foreach (var town in availableTowns.ToList())
            {
                float bestDist = float.MaxValue;
                Kingdom? bestKingdom = null;
                foreach (var kingdom in kingdoms)
                {
                    if (clusters[kingdom].Count == 0) continue;
                    var cs = ComputeCentroidSettlement(clusters[kingdom]);
                    if (cs == null) continue;
                    float dist = town.GatePosition.Distance(cs.GatePosition);
                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        bestKingdom = kingdom;
                    }
                }
                if (bestKingdom != null)
                    clusters[bestKingdom].Add(town);
            }

            // ---- Phase 2: Castles follow nearest owned TOWN ----
            foreach (var castle in availableCastles)
            {
                float bestDist = float.MaxValue;
                Kingdom? bestKingdom = null;

                foreach (var kingdom in kingdoms)
                {
                    foreach (var ownedTown in clusters[kingdom].Where(s => s.IsTown))
                    {
                        float dist = castle.GatePosition.Distance(ownedTown.GatePosition);
                        if (dist < bestDist)
                        {
                            bestDist = dist;
                            bestKingdom = kingdom;
                        }
                    }
                }

                if (bestKingdom != null)
                    clusters[bestKingdom].Add(castle);
            }

            // ---- Assign ownership from clusters ----
            foreach (var kingdom in kingdoms)
            {
                var kingdomClans = kingdom.Clans.ToList();
                int clanIdx = 0;
                foreach (var fief in clusters[kingdom])
                {
                    if (distributeToVassals && kingdomClans.Count > 0)
                    {
                        AssignFiefToClan(fief, kingdomClans[clanIdx % kingdomClans.Count], kingdom);
                        clanIdx++;
                    }
                    else
                    {
                        AssignFiefToKingdom(fief, kingdom);
                    }
                }

                int townCount = clusters[kingdom].Count(s => s.IsTown);
                int castleCount = clusters[kingdom].Count(s => s.IsCastle);
                LogHelper.Info("Kingdom '" + kingdom.Name + "': "
                    + townCount + " towns, " + castleCount + " castles assigned.");
            }

            // ---- Phase 3: Villages follow parent ----
            AssignBoundVillages(clusters, kingdoms);
        }

        /// <summary>
        /// Pick the most central town (closest to the median X/Y of all towns).
        /// </summary>
        private static Settlement PickCentralTown(List<Settlement> towns)
        {
            if (towns.Count <= 1) return towns[0];

            // Find the town closest to the average position of all towns
            // Use pairwise distances to find the most central one
            float bestTotalDist = float.MaxValue;
            Settlement best = towns[0];

            foreach (var candidate in towns)
            {
                float totalDist = 0;
                foreach (var other in towns)
                {
                    if (other == candidate) continue;
                    totalDist += candidate.GatePosition.Distance(other.GatePosition);
                }
                if (totalDist < bestTotalDist)
                {
                    bestTotalDist = totalDist;
                    best = candidate;
                }
            }
            return best;
        }

        /// <summary>
        /// Pick the town that is farthest from all existing anchors (Euclidean).
        /// This spreads kingdom starting points across the map.
        /// </summary>
        private static Settlement PickFarthestTown(List<Settlement> pool, List<Settlement> anchors)
        {
            float bestMinDist = -1f;
            Settlement best = pool[0];

            foreach (var candidate in pool)
            {
                float minDist = float.MaxValue;
                foreach (var anchor in anchors)
                {
                    float dist = candidate.GatePosition.Distance(anchor.GatePosition);
                    if (dist < minDist) minDist = dist;
                }
                if (minDist > bestMinDist)
                {
                    bestMinDist = minDist;
                    best = candidate;
                }
            }
            return best;
        }

        /// <summary>
        /// Compute the centroid (average position) of a list of settlements.
        /// Returns the settlement closest to the centroid.
        /// </summary>
        private static Settlement? ComputeCentroidSettlement(List<Settlement> settlements)
        {
            if (settlements.Count == 0) return null;
            if (settlements.Count == 1) return settlements[0];

            // Find the settlement with the minimum total distance to all others
            float bestTotal = float.MaxValue;
            Settlement best = settlements[0];
            foreach (var candidate in settlements)
            {
                float totalDist = 0;
                foreach (var other in settlements)
                {
                    if (other == candidate) continue;
                    totalDist += candidate.GatePosition.Distance(other.GatePosition);
                }
                if (totalDist < bestTotal)
                {
                    bestTotal = totalDist;
                    best = candidate;
                }
            }
            return best;
        }

        /// <summary>
        /// After fortification assignment, ensure bound villages follow their parent.
        /// </summary>
        private static void AssignBoundVillages(
            Dictionary<Kingdom, List<Settlement>> clusters, List<Kingdom> kingdoms)
        {
            foreach (var kingdom in kingdoms)
            {
                foreach (var fief in clusters[kingdom])
                {
                    if (fief.BoundVillages == null) continue;
                    foreach (var village in fief.BoundVillages)
                    {
                        if (village?.Settlement == null) continue;
                        if (village.Settlement.OwnerClan?.Kingdom == kingdom) continue;
                        try
                        {
                            AssignFiefToKingdom(village.Settlement, kingdom);
                        }
                        catch (Exception ex)
                        {
                            LogHelper.Debug("Could not reassign village '"
                                + village.Settlement.Name + "': " + ex.Message);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Euclidean fallback when MapDistanceModel is unavailable.
        /// </summary>
        private void DistributeFiefsEuclidean(List<Settlement> fiefs, List<Kingdom> kingdoms,
            int perKingdom, bool distributeToVassals)
        {
            var available = new List<Settlement>(fiefs);

            foreach (var kingdom in kingdoms)
            {
                if (available.Count == 0) break;

                var anchor = kingdom.RulingClan?.HomeSettlement
                    ?? kingdom.RulingClan?.Settlements?.FirstOrDefault(s => s.IsTown || s.IsCastle);

                var sorted = anchor != null
                    ? available.OrderBy(f => f.GatePosition.DistanceSquared(anchor.GatePosition)).ToList()
                    : available;

                int toAssign = Math.Min(perKingdom, sorted.Count);
                var kingdomClans = kingdom.Clans.ToList();
                int clanIdx = 0;

                for (int i = 0; i < toAssign; i++)
                {
                    var fief = sorted[i];
                    available.Remove(fief);
                    if (distributeToVassals && kingdomClans.Count > 0)
                    {
                        AssignFiefToClan(fief, kingdomClans[clanIdx % kingdomClans.Count], kingdom);
                        clanIdx++;
                    }
                    else
                    {
                        AssignFiefToKingdom(fief, kingdom);
                    }
                }
            }

            // Remaining → round-robin
            int kIdx = 0;
            foreach (var fief in available)
            {
                AssignFiefToKingdom(fief, kingdoms[kIdx % kingdoms.Count]);
                kIdx++;
            }
        }

        private void DistributeFiefsRoundRobin(List<Settlement> fiefs, List<Kingdom> kingdoms,
            int perKingdom, bool distributeToVassals)
        {
            int fiefIdx = 0;
            foreach (var kingdom in kingdoms)
            {
                var kingdomClans = kingdom.Clans.ToList();
                int clanIdx = 0;

                for (int i = 0; i < perKingdom && fiefIdx < fiefs.Count; i++, fiefIdx++)
                {
                    var fief = fiefs[fiefIdx];

                    if (distributeToVassals && kingdomClans.Count > 0)
                    {
                        var targetClan = kingdomClans[clanIdx % kingdomClans.Count];
                        clanIdx++;
                        AssignFiefToClan(fief, targetClan, kingdom);
                    }
                    else
                    {
                        AssignFiefToKingdom(fief, kingdom);
                    }
                }
            }

            // Remaining fiefs → round-robin to kingdoms
            int kIdx = 0;
            while (fiefIdx < fiefs.Count)
            {
                AssignFiefToKingdom(fiefs[fiefIdx], kingdoms[kIdx % kingdoms.Count]);
                fiefIdx++;
                kIdx++;
            }
        }

        // =====================================================================
        // Relations
        // =====================================================================

        private static void ApplyRelationsBonus(List<Kingdom> kingdoms, int bonus)
        {
            for (int i = 0; i < kingdoms.Count; i++)
            {
                for (int j = i + 1; j < kingdoms.Count; j++)
                {
                    var a = kingdoms[i];
                    var b = kingdoms[j];
                    if (a?.Culture == null || b?.Culture == null) continue;
                    if (a.Culture.StringId != b.Culture.StringId) continue;

                    // Apply relation bonus between ruling clans of same-culture kingdoms
                    try
                    {
                        if (a.RulingClan?.Leader != null && b.RulingClan?.Leader != null)
                        {
                            ChangeRelationAction.ApplyRelationChangeBetweenHeroes(
                                a.RulingClan.Leader, b.RulingClan.Leader, bonus);
                            LogHelper.Debug("Relations +" + bonus + " between '"
                                + a.Name + "' and '" + b.Name + "' (same culture).");
                        }
                    }
                    catch (Exception ex)
                    {
                        LogHelper.Warn("Could not apply relations bonus: " + ex.Message);
                    }
                }
            }
        }

        // =====================================================================
        // Helpers
        // =====================================================================

        private void AssignClanToKingdom(Clan clan, Kingdom kingdom)
        {
            try
            {
                if (clan.Kingdom == kingdom)
                {
                    RecordAssignment(clan, kingdom);
                    return;
                }

                try
                {
                    ChangeKingdomAction.ApplyByJoinToKingdom(clan, kingdom, CampaignTime.Now, false);
                }
                catch
                {
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

            if (fief.OwnerClan?.Kingdom == kingdom)
                return;

            try
            {
                ChangeOwnerOfSettlementAction.ApplyByDefault(rulingClan.Leader, fief);
            }
            catch
            {
                try
                {
                    ChangeOwnerOfSettlementAction.ApplyByKingDecision(rulingClan.Leader, fief);
                }
                catch
                {
                    try
                    {
                        if (fief.Town != null)
                            fief.Town.OwnerClan = rulingClan;
                        LogHelper.Debug("Fief '" + fief.Name + "' assigned via direct Town.OwnerClan.");
                    }
                    catch (Exception ex3)
                    {
                        LogHelper.Error("All fief assignment methods failed for '" + fief.Name
                            + "': " + ex3.Message);
                    }
                }
            }
        }

        private static void AssignFiefToClan(Settlement fief, Clan clan, Kingdom kingdom)
        {
            if (fief == null || clan == null) return;
            if (clan.Leader == null)
            {
                // Fall back to ruling clan
                AssignFiefToKingdom(fief, kingdom);
                return;
            }

            if (fief.OwnerClan == clan)
                return;

            try
            {
                ChangeOwnerOfSettlementAction.ApplyByDefault(clan.Leader, fief);
            }
            catch
            {
                try
                {
                    if (fief.Town != null)
                        fief.Town.OwnerClan = clan;
                }
                catch (Exception ex)
                {
                    LogHelper.Warn("Could not assign fief '" + fief.Name
                        + "' to clan '" + clan.Name + "': " + ex.Message);
                    // Fall back to ruling clan
                    AssignFiefToKingdom(fief, kingdom);
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

        private static void ApplyClanPresetsToNewKingdoms(
            List<Kingdom> kingdoms,
            PresetAssignmentEngine? presetEngine)
        {
            if (kingdoms == null || presetEngine == null) return;

            int assigned = 0;
            foreach (var kingdom in kingdoms)
            {
                if (kingdom == null) continue;
                foreach (var clan in kingdom.Clans.ToList())
                {
                    if (clan == null || clan == Clan.PlayerClan) continue;
                    if (presetEngine.ApplyClanIdentity(clan) != null)
                        assigned++;
                }
            }

            LogHelper.Info("Preset identities applied to " + assigned + " clan(s).");
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
