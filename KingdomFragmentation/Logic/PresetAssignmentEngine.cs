using System;
using System.Collections.Generic;
using System.Linq;
using KingdomFragmentation.Data;
using KingdomFragmentation.Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Localization;

namespace KingdomFragmentation.Logic
{
    /// <summary>
    /// Draws kingdom/clan/hero presets from curated catalogs and applies identities.
    /// Decks are sampled without replacement for each campaign run.
    /// </summary>
    public sealed class PresetAssignmentEngine
    {
        private readonly Random _rng;
        private readonly bool _strictCulture;
        private readonly int _archetypeAggressivenessPercent;
        private readonly bool _reportEnabled;

        private readonly List<KingdomPreset> _kingdomPresets;
        private readonly List<ClanPreset> _clanPresets;
        private readonly List<HeroArchetypePreset> _heroArchetypes;
        private readonly Dictionary<string, HeroArchetypePreset> _archetypesById;

        private readonly HashSet<string> _usedKingdomPresetIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _usedClanPresetIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, ClanPreset> _assignedClanPresetsByClanKey =
            new Dictionary<string, ClanPreset>(StringComparer.OrdinalIgnoreCase);

        private readonly List<string> _kingdomReportLines = new List<string>();
        private readonly List<string> _clanReportLines = new List<string>();
        private readonly List<string> _heroReportLines = new List<string>();
        private readonly HashSet<string> _loggedKingdomKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _loggedClanKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _loggedHeroKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private static readonly Dictionary<string, string[]> ThemeFallbackArchetypes =
            new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                { "martial", new[] { "warlord", "marshal", "champion", "raider" } },
                { "trade", new[] { "merchant_prince", "steward", "diplomat", "engineer" } },
                { "frontier", new[] { "ranger_lord", "outrider", "archer_captain", "raider" } },
                { "court", new[] { "diplomat", "steward", "marshal", "tactician" } },
                { "faith", new[] { "zealot_lord", "marshal", "champion", "steward" } },
                { "nomad", new[] { "steppe_khan", "outrider", "raider", "archer_captain" } },
                { "coastal", new[] { "merchant_prince", "archer_captain", "diplomat", "raider" } },
                { "mountain", new[] { "mountain_guard", "champion", "marshal", "highland_chief" } },
                { "river", new[] { "river_warden", "steward", "diplomat", "ranger_lord" } },
                { "highland", new[] { "highland_chief", "ranger_lord", "champion", "marshal" } },
                { "desert", new[] { "desert_lancer", "zealot_lord", "raider", "merchant_prince" } },
                { "steppe", new[] { "steppe_khan", "outrider", "raider", "archer_captain" } },
                { "forest", new[] { "forest_ranger", "ranger_lord", "archer_captain", "steward" } },
            };

        public PresetAssignmentEngine(
            bool strictCulture,
            int archetypeAggressivenessPercent,
            bool reportEnabled)
        {
            _rng = CreateRandom();
            _strictCulture = strictCulture;
            _archetypeAggressivenessPercent = Clamp(archetypeAggressivenessPercent, 50, 200);
            _reportEnabled = reportEnabled;

            _kingdomPresets = KingdomPresetCatalog.GetAll().ToList();
            _clanPresets = ClanPresetCatalog.GetAll().ToList();
            _heroArchetypes = HeroArchetypeCatalog.GetAll().ToList();

            _archetypesById = new Dictionary<string, HeroArchetypePreset>(StringComparer.OrdinalIgnoreCase);
            foreach (var archetype in _heroArchetypes)
            {
                if (!_archetypesById.ContainsKey(archetype.Id))
                    _archetypesById[archetype.Id] = archetype;
            }
        }

        public int ArchetypeAggressivenessPercent => _archetypeAggressivenessPercent;

        public KingdomPreset? DrawKingdomPreset(string? cultureId)
        {
            return DrawPreset(_kingdomPresets,
                p => p.Id,
                p => p.CultureId,
                cultureId,
                _usedKingdomPresetIds);
        }

        public ClanPreset? DrawClanPreset(string? cultureId)
        {
            return DrawPreset(_clanPresets,
                p => p.Id,
                p => p.CultureId,
                cultureId,
                _usedClanPresetIds);
        }

        public void RegisterKingdomPreset(Kingdom kingdom, Clan leaderClan, KingdomPreset preset)
        {
            if (!_reportEnabled || kingdom == null || preset == null) return;

            string key = (kingdom.StringId ?? kingdom.Name?.ToString() ?? "kingdom") + "|" + preset.Id;
            if (!_loggedKingdomKeys.Add(key))
                return;

            string leaderName = leaderClan?.Name?.ToString() ?? "Unknown";
            _kingdomReportLines.Add(
                "KINGDOM | id=" + (kingdom.StringId ?? "n/a")
                + " | name=" + kingdom.Name
                + " | culture=" + (kingdom.Culture?.StringId ?? "unknown")
                + " | preset=" + preset.Id
                + " | theme=" + preset.Theme
                + " | leader=" + leaderName);
        }

        public void RegisterClanPreset(Clan clan, ClanPreset preset, string source = "assign")
        {
            var key = GetClanKey(clan);
            if (string.IsNullOrEmpty(key) || preset == null) return;
            _assignedClanPresetsByClanKey[key] = preset;

            if (!_reportEnabled) return;
            string logKey = key + "|" + preset.Id;
            if (!_loggedClanKeys.Add(logKey))
                return;

            _clanReportLines.Add(
                "CLAN    | id=" + (clan.StringId ?? "n/a")
                + " | name=" + clan.Name
                + " | culture=" + (clan.Culture?.StringId ?? "unknown")
                + " | preset=" + preset.Id
                + " | theme=" + preset.Theme
                + " | leader_archetype=" + preset.LeaderArchetypeId
                + " | source=" + source);
        }

        public ClanPreset? GetAssignedClanPreset(Clan clan)
        {
            var key = GetClanKey(clan);
            if (string.IsNullOrEmpty(key)) return null;

            _assignedClanPresetsByClanKey.TryGetValue(key, out var preset);
            return preset;
        }

        public ClanPreset? GetOrAssignClanPreset(Clan clan)
        {
            var preset = GetAssignedClanPreset(clan);
            if (preset != null) return preset;

            preset = DrawClanPreset(clan?.Culture?.StringId);
            if (preset != null && clan != null)
                RegisterClanPreset(clan, preset, "draw");

            return preset;
        }

        /// <summary>
        /// Applies preset identity (name/colors/banner) to a clan.
        /// </summary>
        public ClanPreset? ApplyClanIdentity(Clan clan)
        {
            if (clan == null || clan == Clan.PlayerClan) return null;

            var preset = GetOrAssignClanPreset(clan);
            if (preset == null) return null;
            uint primaryColor = clan.Kingdom?.Color ?? preset.PrimaryColor;
            uint secondaryColor = clan.Kingdom?.Color2 ?? preset.SecondaryColor;

            try
            {
                clan.ChangeClanName(new TextObject(preset.DisplayName), new TextObject(preset.InformalName));
            }
            catch (Exception ex)
            {
                LogHelper.Debug("PresetEngine: could not rename clan '" + clan.Name + "': " + ex.Message);
            }

            try
            {
                clan.Banner = BannerGenerator.GenerateKingdomStyledBanner(
                    "clan_preset_" + preset.Id + "_" + (clan.StringId ?? preset.DisplayName),
                    primaryColor,
                    secondaryColor);
            }
            catch (Exception ex)
            {
                LogHelper.Debug("PresetEngine: could not set banner for clan '" + clan.Name + "': " + ex.Message);
            }

            try
            {
                clan.Color = primaryColor;
                clan.Color2 = secondaryColor;
            }
            catch (Exception ex)
            {
                LogHelper.Debug("PresetEngine: could not set colors for clan '" + clan.Name + "': " + ex.Message);
            }

            RegisterClanPreset(clan, preset, "identity_apply");
            return preset;
        }

        public HeroArchetypePreset? DrawHeroArchetype(
            string? cultureId,
            ClanPreset? clanPreset,
            bool isLeader,
            int slotIndex)
        {
            if (clanPreset != null)
            {
                if (isLeader && !string.IsNullOrWhiteSpace(clanPreset.LeaderArchetypeId)
                    && _archetypesById.TryGetValue(clanPreset.LeaderArchetypeId, out var leaderArchetype))
                {
                    return leaderArchetype;
                }

                if (clanPreset.MemberArchetypeIds != null && clanPreset.MemberArchetypeIds.Count > 0)
                {
                    int idx = PositiveMod(slotIndex, clanPreset.MemberArchetypeIds.Count);
                    string requested = clanPreset.MemberArchetypeIds[idx];
                    if (_archetypesById.TryGetValue(requested, out var memberArchetype))
                        return memberArchetype;
                }

                if (!string.IsNullOrWhiteSpace(clanPreset.Theme)
                    && ThemeFallbackArchetypes.TryGetValue(clanPreset.Theme, out var themedIds)
                    && themedIds.Length > 0)
                {
                    string themedId = themedIds[PositiveMod(slotIndex, themedIds.Length)];
                    if (_archetypesById.TryGetValue(themedId, out var themedArchetype))
                        return themedArchetype;
                }
            }

            string normalizedCulture = NormalizeCultureId(cultureId);
            var candidates = _heroArchetypes
                .Where(a => a.CultureId.Equals("any", StringComparison.OrdinalIgnoreCase)
                    || a.CultureId.Equals(normalizedCulture, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (candidates.Count == 0)
            {
                if (_strictCulture)
                    return null;
                candidates = _heroArchetypes;
            }

            return candidates.Count > 0
                ? candidates[_rng.Next(candidates.Count)]
                : null;
        }

        public void RecordHeroArchetypeAssignment(
            Clan clan,
            Hero hero,
            HeroArchetypePreset? archetype,
            bool isLeader,
            int slotIndex,
            string source)
        {
            if (!_reportEnabled || clan == null || hero == null || archetype == null)
                return;

            string heroKey = (hero.StringId ?? hero.Name?.ToString() ?? "hero") + "|" + archetype.Id + "|" + slotIndex;
            if (!_loggedHeroKeys.Add(heroKey))
                return;

            _heroReportLines.Add(
                "HERO    | id=" + (hero.StringId ?? "n/a")
                + " | name=" + hero.Name
                + " | clan=" + clan.Name
                + " | culture=" + (clan.Culture?.StringId ?? "unknown")
                + " | archetype=" + archetype.Id
                + " | role=" + (isLeader ? "leader" : "member")
                + " | slot=" + slotIndex
                + " | source=" + source);
        }

        public int ScaleArchetypeSkillTarget(int baseValue, string category)
        {
            double aggressiveness = _archetypeAggressivenessPercent / 100.0;
            double factor;

            switch ((category ?? string.Empty).ToLowerInvariant())
            {
                case "combat":
                    factor = aggressiveness;
                    break;
                case "command":
                    factor = 1.0 + ((aggressiveness - 1.0) * 0.45);
                    break;
                case "civil":
                    factor = 1.0 + ((aggressiveness - 1.0) * 0.20);
                    break;
                default:
                    factor = 1.0;
                    break;
            }

            int scaled = (int)Math.Round(baseValue * factor);
            return Clamp(scaled, 10, 330);
        }

        public void EmitRunReport()
        {
            if (!_reportEnabled)
                return;

            LogHelper.Info(
                "Preset Report Summary: "
                + "strictCulture=" + _strictCulture
                + ", aggressiveness=" + _archetypeAggressivenessPercent + "%"
                + ", catalog_kingdoms=" + _kingdomPresets.Count
                + ", catalog_clans=" + _clanPresets.Count
                + ", catalog_archetypes=" + _heroArchetypes.Count
                + ", kingdom_picks=" + _kingdomReportLines.Count
                + ", clan_picks=" + _clanReportLines.Count
                + ", hero_picks=" + _heroReportLines.Count);

            foreach (var line in _kingdomReportLines)
                LogHelper.Info("PresetReport " + line);

            foreach (var line in _clanReportLines)
                LogHelper.Info("PresetReport " + line);

            foreach (var line in _heroReportLines)
                LogHelper.Info("PresetReport " + line);
        }

        private T? DrawPreset<T>(
            List<T> source,
            Func<T, string> idSelector,
            Func<T, string> cultureSelector,
            string? cultureId,
            HashSet<string> usedSet)
            where T : class
        {
            if (source.Count == 0) return null;

            string normalizedCulture = NormalizeCultureId(cultureId);

            // Phase 1: same-culture, not yet used this run.
            var cultureCandidates = source
                .Where(p => !usedSet.Contains(idSelector(p))
                    && cultureSelector(p).Equals(normalizedCulture, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (cultureCandidates.Count > 0)
            {
                var picked = cultureCandidates[_rng.Next(cultureCandidates.Count)];
                usedSet.Add(idSelector(picked));
                return picked;
            }

            // Phase 2: same-culture exhausted — borrow from any other culture.
            // (Applies regardless of _strictCulture; hero archetype path enforces
            //  strict culture separately through DrawHeroArchetype.)
            var anyCandidates = source
                .Where(p => !usedSet.Contains(idSelector(p)))
                .ToList();

            if (anyCandidates.Count > 0)
            {
                var picked = anyCandidates[_rng.Next(anyCandidates.Count)];
                usedSet.Add(idSelector(picked));
                return picked;
            }

            // Phase 3: entire deck used — recycle same-culture without clearing the
            // used-set so we don't lose uniqueness tracking for other draw paths.
            var recycleCulture = source
                .Where(p => cultureSelector(p).Equals(normalizedCulture, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (recycleCulture.Count > 0)
                return recycleCulture[_rng.Next(recycleCulture.Count)];

            // Phase 4: last resort — any entry from the source list.
            return source[_rng.Next(source.Count)];
        }

        private static string GetClanKey(Clan clan)
        {
            if (clan == null) return string.Empty;
            if (!string.IsNullOrWhiteSpace(clan.StringId)) return clan.StringId;
            string clanName = clan.Name?.ToString() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(clanName)) return clanName;
            return "clan_" + clan.GetHashCode();
        }

        private static string NormalizeCultureId(string? cultureId)
        {
            if (string.IsNullOrWhiteSpace(cultureId))
                return "unknown";

            string value = cultureId ?? string.Empty;
            return value.Trim().ToLowerInvariant();
        }

        private static Random CreateRandom()
        {
            unchecked
            {
                int seed = Environment.TickCount;
                seed ^= (int)DateTime.UtcNow.Ticks;
                seed ^= Guid.NewGuid().GetHashCode();
                return new Random(seed);
            }
        }

        private static int PositiveMod(int value, int modulo)
        {
            int rem = value % modulo;
            return rem < 0 ? rem + modulo : rem;
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }
    }
}
