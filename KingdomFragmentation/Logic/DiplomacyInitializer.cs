using System.Collections.Generic;
using System.Linq;
using KingdomFragmentation.Helpers;
using KingdomFragmentation.Settings;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;

namespace KingdomFragmentation.Logic
{
    /// <summary>
    /// Sets up the initial diplomatic state between all freshly created kingdoms.
    /// </summary>
    public sealed class DiplomacyInitializer
    {
        private readonly KingdomFragmentationSettings? _settings;

        public DiplomacyInitializer(KingdomFragmentationSettings? settings)
        {
            _settings = settings;
        }

        // -----------------------------------------------------------------------
        // Public API
        // -----------------------------------------------------------------------

        /// <param name="clanOriginSnapshot">
        /// A snapshot of <c>Clan → original Kingdom</c> captured BEFORE any
        /// <see cref="ChangeKingdomAction"/> calls, so that rivalry resolution
        /// works correctly even though clans have since moved.
        /// </param>
        public void Initialize(
            IReadOnlyList<Kingdom> newKingdoms,
            IReadOnlyList<Kingdom> originalKingdoms,
            IReadOnlyDictionary<Clan, Kingdom> clanOriginSnapshot)
        {
            if (newKingdoms == null || newKingdoms.Count == 0)
                return;

            string mode = _settings?.DiplomacyMode?.SelectedValue ?? "AllPeace";

            LogHelper.Info("DiplomacyInitializer: mode = " + mode
                + ", kingdoms = " + newKingdoms.Count);

            switch (mode)
            {
                case "AllWar":
                    ApplyAllWar(newKingdoms);
                    break;

                case "RivalryBased":
                    ApplyRivalryBased(newKingdoms, clanOriginSnapshot);
                    break;

                default: // AllPeace — ensure no accidental wars
                    ApplyAllPeace(newKingdoms);
                    break;
            }

            // Apply starting truce period (not applicable when AllWar is selected,
            // since you can't have war and a forced-peace truce simultaneously).
            int truceDays = _settings?.StartingTruceDays ?? 0;
            if (truceDays > 0 && mode != "AllWar")
            {
                LogHelper.Info("DiplomacyInitializer: truce of " + truceDays
                    + " day(s) will be enforced via daily tick.");
            }
        }

        // -----------------------------------------------------------------------
        // Diplomacy modes
        // -----------------------------------------------------------------------

        private void ApplyAllPeace(IReadOnlyList<Kingdom> kingdoms)
        {
            LogHelper.Debug("  DiplomacyInitializer: ensuring peace between all kingdoms.");
            var list = kingdoms.ToList();
            for (int i = 0; i < list.Count; i++)
            {
                for (int j = i + 1; j < list.Count; j++)
                {
                    var a = list[i];
                    var b = list[j];
                    if (a == null || b == null) continue;

                    if (FactionManager.IsAtWarAgainstFaction(a, b))
                    {
                        try
                        {
                            MakePeaceAction.Apply(a, b, showNotification: false);
                            LogHelper.Debug("    Peace: " + a.Name + " <-> " + b.Name);
                        }
                        catch (System.Exception ex)
                        {
                            LogHelper.Warn("    Could not make peace between '"
                                + a.Name + "' and '" + b.Name + "': " + ex.Message);
                        }
                    }
                }
            }
        }

        private void ApplyAllWar(IReadOnlyList<Kingdom> kingdoms)
        {
            LogHelper.Debug("  DiplomacyInitializer: declaring war between all kingdoms.");
            var list = kingdoms.ToList();
            for (int i = 0; i < list.Count; i++)
            {
                for (int j = i + 1; j < list.Count; j++)
                {
                    var a = list[i];
                    var b = list[j];
                    if (a == null || b == null) continue;

                    if (!FactionManager.IsAtWarAgainstFaction(a, b))
                    {
                        try
                        {
                            DeclareWarAction.ApplyByDefault(a, b);
                            LogHelper.Debug("    War: " + a.Name + " vs " + b.Name);
                        }
                        catch (System.Exception ex)
                        {
                            LogHelper.Warn("    Could not declare war between '"
                                + a.Name + "' and '" + b.Name + "': " + ex.Message);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Declares wars only between kingdoms whose ruling clans came from
        /// original kingdoms that were already at war, using the pre-fragmentation
        /// snapshot so that post-move clan membership does not cause the lookup
        /// to produce empty results.
        /// </summary>
        private void ApplyRivalryBased(
            IReadOnlyList<Kingdom> newKingdoms,
            IReadOnlyDictionary<Clan, Kingdom> clanOriginSnapshot)
        {
            // Build a lookup: new Kingdom → the pre-fragmentation kingdom its
            // ruling clan came from.  We use the pre-captured snapshot rather than
            // live clan membership to avoid the case where the clan has already
            // moved and ok.Clans.Contains(clan) returns false.
            var originMap = new Dictionary<Kingdom, Kingdom>();
            foreach (var nk in newKingdoms)
            {
                if (nk?.RulingClan == null) continue;
                if (clanOriginSnapshot.TryGetValue(nk.RulingClan, out var origin) && origin != null)
                    originMap[nk] = origin;
            }

            // Kingdoms whose clans came from originally-at-war kingdoms start at war
            var newList = newKingdoms.ToList();
            for (int i = 0; i < newList.Count; i++)
            {
                for (int j = i + 1; j < newList.Count; j++)
                {
                    var a = newList[i];
                    var b = newList[j];
                    if (a == null || b == null) continue;

                    originMap.TryGetValue(a, out var originA);
                    originMap.TryGetValue(b, out var originB);

                    // Same original kingdom → peace (no action needed)
                    if (originA != null && originA == originB)
                        continue;

                    // Different original kingdoms that were at war → declare war
                    bool wereAtWar = originA != null && originB != null
                        && FactionManager.IsAtWarAgainstFaction(originA, originB);

                    if (wereAtWar)
                    {
                        try
                        {
                            DeclareWarAction.ApplyByDefault(a, b);
                            LogHelper.Debug("    RivalryWar: " + a.Name + " vs " + b.Name);
                        }
                        catch (System.Exception ex)
                        {
                            LogHelper.Warn("    Could not declare rivalry war: " + ex.Message);
                        }
                    }
                }
            }
        }
    }
}
