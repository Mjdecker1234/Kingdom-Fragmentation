using System;
using System.Collections.Generic;

namespace KingdomFragmentation.Data
{
    public static class ClanPresetCatalog
    {
        private static readonly Dictionary<string, (string leader, string[] members)> ArchetypesByTheme =
            new Dictionary<string, (string, string[])>
            {
                { "martial", ("warlord", new[] { "marshal", "champion", "tactician", "raider" }) },
                { "trade", ("merchant_prince", new[] { "steward", "diplomat", "engineer", "tactician" }) },
                { "frontier", ("ranger_lord", new[] { "outrider", "archer_captain", "raider", "marshal" }) },
                { "court", ("diplomat", new[] { "steward", "marshal", "tactician", "champion" }) },
                { "faith", ("zealot_lord", new[] { "marshal", "steward", "champion", "tactician" }) },
                { "nomad", ("steppe_khan", new[] { "outrider", "raider", "archer_captain", "marshal" }) },
                { "coastal", ("merchant_prince", new[] { "archer_captain", "diplomat", "raider", "steward" }) },
                { "mountain", ("marshal", new[] { "champion", "ranger_lord", "tactician", "steward" }) },
                { "river", ("steward", new[] { "diplomat", "tactician", "ranger_lord", "marshal" }) },
                { "highland", ("ranger_lord", new[] { "champion", "marshal", "archer_captain", "tactician" }) },
                { "desert", ("zealot_lord", new[] { "raider", "merchant_prince", "outrider", "steward" }) },
                { "steppe", ("steppe_khan", new[] { "outrider", "raider", "archer_captain", "marshal" }) },
                { "forest", ("ranger_lord", new[] { "archer_captain", "steward", "marshal", "tactician" }) },
            };

        private static readonly Dictionary<string, string[]> ThemeCycleByCulture =
            new Dictionary<string, string[]>
            {
                { "empire",   new[] { "martial", "trade", "court", "faith", "river", "mountain", "coastal", "frontier", "highland", "forest" } },
                { "sturgia",  new[] { "martial", "highland", "frontier", "coastal", "river", "mountain", "faith", "trade", "forest", "court" } },
                { "aserai",   new[] { "trade", "court", "faith", "desert", "river", "coastal", "frontier", "mountain", "martial", "highland" } },
                { "vlandia",  new[] { "court", "martial", "trade", "coastal", "river", "mountain", "frontier", "faith", "highland", "forest" } },
                { "battania", new[] { "forest", "highland", "frontier", "martial", "faith", "river", "coastal", "court", "trade", "mountain" } },
                { "khuzait",  new[] { "steppe", "nomad", "martial", "frontier", "trade", "coastal", "river", "mountain", "highland", "faith" } },
            };

        private static readonly Dictionary<string, (uint primary, uint secondary)[]> PaletteByCulture =
            new Dictionary<string, (uint, uint)[]>
            {
                {
                    "empire",
                    new[]
                    {
                        (0xFF7A1F28u, 0xFFD8C178u), (0xFF1F3C73u, 0xFFE4DFCFu), (0xFF4C2C1Fu, 0xFFDEC59Fu),
                        (0xFF2A2A35u, 0xFFC7C8D3u), (0xFF5A3042u, 0xFFE5D2DEu), (0xFF1F4E5Au, 0xFFC8DDE0u),
                        (0xFF3E3E48u, 0xFFDADBE5u), (0xFF6A4A2Bu, 0xFFE8D9B6u),
                    }
                },
                {
                    "sturgia",
                    new[]
                    {
                        (0xFF1F3557u, 0xFF9FB6D4u), (0xFF263842u, 0xFFD9D2C4u), (0xFF30435Bu, 0xFFC8D6E7u),
                        (0xFF3E2A2Au, 0xFFE2D4BFu), (0xFF24505Au, 0xFFB8D7DBu), (0xFF2E2E3Au, 0xFFD5D8E2u),
                        (0xFF4D3B2Au, 0xFFE7D6C0u), (0xFF1F4650u, 0xFFC4DCE2u),
                    }
                },
                {
                    "aserai",
                    new[]
                    {
                        (0xFF6B3A14u, 0xFFE4B661u), (0xFF5A1F1Fu, 0xFFF0D7A1u), (0xFF7A2A1Eu, 0xFFF4DFAEu),
                        (0xFF3C3A2Du, 0xFFCFB58Au), (0xFF2F4A4Au, 0xFFC9DED8u), (0xFF7B4A1Cu, 0xFFEFC98Au),
                        (0xFF4A2F1Fu, 0xFFE7D8B8u), (0xFF2D4742u, 0xFFD1E5D9u),
                    }
                },
                {
                    "vlandia",
                    new[]
                    {
                        (0xFF8A2326u, 0xFFE9D6B8u), (0xFF1F3158u, 0xFFD9E1F2u), (0xFF304C2Fu, 0xFFE2D9C7u),
                        (0xFF5A3B1Fu, 0xFFF0DAA5u), (0xFF4A2F48u, 0xFFE8CFE7u), (0xFF2B4A62u, 0xFFD1DFF0u),
                        (0xFF6A4E27u, 0xFFEBDAB5u), (0xFF283F35u, 0xFFDAE4D8u),
                    }
                },
                {
                    "battania",
                    new[]
                    {
                        (0xFF1E4A2Bu, 0xFFBFD8A9u), (0xFF36512Bu, 0xFFE3D2B5u), (0xFF273B3Au, 0xFFCED9D4u),
                        (0xFF4C2235u, 0xFFE6C8D7u), (0xFF2D5A3Fu, 0xFFCDE2B9u), (0xFF2A4735u, 0xFFD2E1CBu),
                        (0xFF385840u, 0xFFE2DCC9u), (0xFF304040u, 0xFFD5DEDAu),
                    }
                },
                {
                    "khuzait",
                    new[]
                    {
                        (0xFF16486Du, 0xFFD9C279u), (0xFF2D3A4Du, 0xFFCAD8E7u), (0xFF2A4255u, 0xFFE7D8BAu),
                        (0xFF4D341Fu, 0xFFE9C98Fu), (0xFF2E5A4Fu, 0xFFC5E1D6u), (0xFF3C4E63u, 0xFFDBD0B2u),
                        (0xFF5A4126u, 0xFFEED49Fu), (0xFF244F59u, 0xFFD4E5D8u),
                    }
                },
            };

        private static readonly IReadOnlyList<ClanPreset> Presets = Build();

        public static IReadOnlyList<ClanPreset> GetAll()
        {
            return Presets;
        }

        private static IReadOnlyList<ClanPreset> Build()
        {
            var result = new List<ClanPreset>(300);

            AddGeneratedCulturePresets(result, "empire", 50,
                new[] { "Var", "Alec", "Dra", "Cass", "Pel", "Them", "Arg", "Nyr", "Val", "Ser" },
                new[] { "ion", "aros", "ellus", "yros", "andar", "emos", "anth", "eron", "aris", "imar" });

            AddGeneratedCulturePresets(result, "sturgia", 50,
                new[] { "Frost", "Bear", "Winter", "Red", "Stone", "Ice", "Wolf", "Rime", "Iron", "North" },
                new[] { "vein", "cliff", "oak", "fjord", "helm", "fang", "mar", "guard", "tide", "brow" });

            AddGeneratedCulturePresets(result, "aserai", 50,
                new[] { "Sah", "Maz", "Qad", "Rash", "Nas", "Had", "Zaf", "Mir", "Far", "Saf" },
                new[] { "ir", "im", "id", "ad", "an", "un", "iq", "ah", "imr", "iqa" });

            AddGeneratedCulturePresets(result, "vlandia", 50,
                new[] { "Ro", "Mar", "Cas", "Vau", "Mon", "Bel", "Cor", "Tal", "Giv", "Lor" },
                new[] { "en", "cen", "ter", "x", "tric", "lac", "ben", "mont", "ran", "ne" });

            AddGeneratedCulturePresets(result, "battania", 50,
                new[] { "Briar", "Oak", "Thorn", "Crow", "Moss", "Wolf", "Red", "Green", "Stag", "Black" },
                new[] { "hart", "shield", "vale", "fern", "river", "root", "rowan", "mantle", "briar", "ash" });

            AddGeneratedCulturePresets(result, "khuzait", 50,
                new[] { "Urum", "Saru", "Tor", "Noya", "Qul", "Alta", "Bor", "Yel", "Kara", "Tem" },
                new[] { "chi", "qa", "gut", "kar", "an", "nar", "chu", "gin", "bek", "jin" });

            return result;
        }

        private static void AddGeneratedCulturePresets(
            List<ClanPreset> result,
            string cultureId,
            int count,
            string[] rootsA,
            string[] rootsB)
        {
            if (!PaletteByCulture.TryGetValue(cultureId, out var palette) || palette.Length == 0)
                palette = new[] { (0xFF3A3A3Au, 0xFFD9D9D9u) };

            if (!ThemeCycleByCulture.TryGetValue(cultureId, out var themes) || themes.Length == 0)
                themes = new[] { "martial", "trade", "frontier", "court", "faith" };

            for (int i = 0; i < count; i++)
            {
                int a = i % rootsA.Length;
                int b = (i / rootsA.Length) % rootsB.Length;
                string token = (rootsA[a] + rootsB[b]).Trim();
                string displayName = BuildDisplayName(cultureId, token);
                string theme = themes[i % themes.Length];

                if (!ArchetypesByTheme.TryGetValue(theme, out var archetypes))
                    archetypes = ArchetypesByTheme["martial"];

                var colors = palette[i % palette.Length];
                string id = "clan_" + cultureId + "_" + (i + 1).ToString("00");

                result.Add(new ClanPreset(
                    id,
                    cultureId,
                    displayName,
                    BuildInformalName(displayName),
                    theme,
                    archetypes.leader,
                    archetypes.members,
                    colors.primary,
                    colors.secondary));
            }
        }

        private static string BuildDisplayName(string cultureId, string token)
        {
            switch (cultureId)
            {
                case "empire":
                    return "House " + token;
                case "sturgia":
                    return "Clan " + token;
                case "aserai":
                    return "Banu al-" + token;
                case "vlandia":
                    return "House de " + token;
                case "battania":
                    return "Clan " + token;
                case "khuzait":
                    return "Clan " + token;
                default:
                    return "House " + token;
            }
        }

        private static string BuildInformalName(string displayName)
        {
            if (string.IsNullOrWhiteSpace(displayName)) return "House";

            string[] prefixes =
            {
                "House de ", "House ", "Clan ", "Banu al-", "Banu ", "de ", "al-", "al "
            };

            foreach (var prefix in prefixes)
            {
                if (displayName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    return displayName.Substring(prefix.Length).Trim();
            }

            return displayName;
        }
    }
}
