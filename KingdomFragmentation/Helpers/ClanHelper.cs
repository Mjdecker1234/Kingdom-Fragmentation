using System.Linq;
using TaleWorlds.CampaignSystem;

namespace KingdomFragmentation.Helpers
{
    /// <summary>
    /// Utility methods for querying and validating <see cref="Clan"/> objects.
    /// </summary>
    public static class ClanHelper
    {
        /// <summary>
        /// Returns <c>true</c> when the clan is non-null, not destroyed / eliminated,
        /// has a living leader, and is part of a kingdom.
        /// </summary>
        public static bool IsValid(Clan? clan)
        {
            if (clan == null)                    return false;
            if (clan.IsEliminated)               return false;
            if (clan.Leader == null)             return false;
            if (clan.Leader.IsDead)              return false;
            if (clan.Kingdom == null)            return false;
            if (clan.Kingdom.IsEliminated)       return false;
            return true;
        }

        /// <summary>
        /// Returns <c>true</c> when the clan is classified as a mercenary clan.
        /// </summary>
        public static bool IsMercenary(Clan clan)
        {
            if (clan == null) return false;
            // In Bannerlord a mercenary clan has IsMercenary == true; in some versions
            // the field is IsUnderMercenaryService or checked via Kingdom service.
            // We check the most common path.
            return clan.IsUnderMercenaryService;
        }

        /// <summary>
        /// Returns <c>true</c> when the clan appears to be a rebel / bandit clan.
        /// Bannerlord does not have a first-class "rebel" flag, so we use a heuristic:
        /// the clan name or string-id contains "rebel" or "outlaw".
        /// </summary>
        public static bool IsRebel(Clan clan)
        {
            if (clan == null) return false;
            string id   = clan.StringId?.ToLowerInvariant() ?? "";
            string name = clan.Name?.ToString()?.ToLowerInvariant() ?? "";
            return id.Contains("rebel") || id.Contains("outlaw")
                || name.Contains("rebel") || name.Contains("outlaw");
        }

        /// <summary>
        /// Returns <c>true</c> when the clan owns at least one town or castle.
        /// </summary>
        public static bool HasFief(Clan clan)
        {
            if (clan == null) return false;
            return clan.Settlements.Any(s => s.IsTown || s.IsCastle);
        }
    }
}
