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

        public void Initialize(
            IReadOnlyList<Kingdom> newKingdoms,
            IReadOnlyList<Kingdom> originalKingdoms)
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
                    ApplyRivalryBased(newKingdoms, originalKingdoms);
                    break;

                default: // AllPeace — ensure no accidental wars
                    ApplyAllPeace(newKingdoms);
                    break;
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

        private void ApplyRivalryBased(
            IReadOnlyList<Kingdom> newKingdoms,
            IReadOnlyList<Kingdom> originalKingdoms)
        {
            // Build a lookup: for each new kingdom, which original kingdom did its
            // ruling clan belong to?
            var originMap = new Dictionary<Kingdom, Kingdom>();
            foreach (var nk in newKingdoms)
            {
                if (nk?.RulingClan == null) continue;
                var clan = nk.RulingClan;
                // Find the original kingdom that contained this clan before fragmentation
                var origin = originalKingdoms
                    .FirstOrDefault(ok => ok.Clans.Contains(clan));
                if (origin != null)
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

                    // Same original kingdom -> peace
                    if (originA != null && originA == originB)
                        continue;

                    // Different original kingdoms that were at war -> declare war
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
