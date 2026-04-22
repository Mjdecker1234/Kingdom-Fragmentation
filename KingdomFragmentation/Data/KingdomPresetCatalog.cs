using System;
using System.Collections.Generic;

namespace KingdomFragmentation.Data
{
    public static class KingdomPresetCatalog
    {
        private static readonly IReadOnlyList<KingdomPreset> Presets = Build();

        public static IReadOnlyList<KingdomPreset> GetAll()
        {
            return Presets;
        }

        private static IReadOnlyList<KingdomPreset> Build()
        {
            var result = new List<KingdomPreset>(48);

            AddCulture(result,
                "empire",
                new[] { "Aurelian", "Sapphire", "Argent", "Velorian", "Marble", "Sunspire", "Ivory", "Bronze" },
                new[] { "Imperium", "Senate", "Basilica", "Principate", "Dominion", "Diadem", "Legate", "Tribunate" },
                new[] { "imperial", "trade", "faith", "frontier", "coastal", "river", "mountain", "court" },
                new[]
                {
                    (0xFF7A1F28u, 0xFFD8C178u),
                    (0xFF1F3C73u, 0xFFE4DFCFu),
                    (0xFF2A2A35u, 0xFFC7C8D3u),
                    (0xFF4C2C1Fu, 0xFFDEC59Fu),
                    (0xFF1F4E5Au, 0xFFC8DDE0u),
                    (0xFF5A3042u, 0xFFE5D2DEu),
                    (0xFF3E3E48u, 0xFFDADBE5u),
                    (0xFF6A4A2Bu, 0xFFE8D9B6u),
                });

            AddCulture(result,
                "sturgia",
                new[] { "Frostwolf", "Stonefjord", "Rimecrown", "Northwatch", "Ironpine", "Deepwinter", "Wolfshore", "Skall" },
                new[] { "Jarldom", "Realm", "League", "Tsardom", "March", "Holdfast", "Coast", "Duchy" },
                new[] { "martial", "frontier", "trade", "imperial", "highland", "mountain", "coastal", "river" },
                new[]
                {
                    (0xFF1F3557u, 0xFF9FB6D4u),
                    (0xFF263842u, 0xFFD9D2C4u),
                    (0xFF30435Bu, 0xFFC8D6E7u),
                    (0xFF3E2A2Au, 0xFFE2D4BFu),
                    (0xFF24505Au, 0xFFB8D7DBu),
                    (0xFF2E2E3Au, 0xFFD5D8E2u),
                    (0xFF4D3B2Au, 0xFFE7D6C0u),
                    (0xFF1F4650u, 0xFFC4DCE2u),
                });

            AddCulture(result,
                "aserai",
                new[] { "Golden Dune", "Qasiran", "Sunfire", "Mirage", "Amber", "Sirocco", "Palmveil", "Jade Oasis" },
                new[] { "Sultanate", "Emirate", "Caliphate", "Court", "Dominion", "March", "League", "Circles" },
                new[] { "trade", "court", "faith", "frontier", "desert", "coastal", "river", "mountain" },
                new[]
                {
                    (0xFF6B3A14u, 0xFFE4B661u),
                    (0xFF5A1F1Fu, 0xFFF0D7A1u),
                    (0xFF7A2A1Eu, 0xFFF4DFAEu),
                    (0xFF3C3A2Du, 0xFFCFB58Au),
                    (0xFF2F4A4Au, 0xFFC9DED8u),
                    (0xFF7B4A1Cu, 0xFFEFC98Au),
                    (0xFF4A2F1Fu, 0xFFE7D8B8u),
                    (0xFF2D4742u, 0xFFD1E5D9u),
                });

            AddCulture(result,
                "vlandia",
                new[] { "Iron Rose", "Highwall", "Westmark", "Lionspire", "Red Banner", "Silverford", "Goldmarch", "Ravenkeep" },
                new[] { "Kingdom", "Crown", "Principality", "Realm", "Duchy", "March", "Domain", "Throne" },
                new[] { "court", "martial", "trade", "imperial", "coastal", "river", "mountain", "frontier" },
                new[]
                {
                    (0xFF8A2326u, 0xFFE9D6B8u),
                    (0xFF1F3158u, 0xFFD9E1F2u),
                    (0xFF304C2Fu, 0xFFE2D9C7u),
                    (0xFF5A3B1Fu, 0xFFF0DAA5u),
                    (0xFF4A2F48u, 0xFFE8CFE7u),
                    (0xFF2B4A62u, 0xFFD1DFF0u),
                    (0xFF6A4E27u, 0xFFEBDAB5u),
                    (0xFF283F35u, 0xFFDAE4D8u),
                });

            AddCulture(result,
                "battania",
                new[] { "Greenthorn", "Oakheart", "Mistwood", "Ravenbloom", "Stonegrove", "Briarwatch", "Wildfern", "Whiteoak" },
                new[] { "Confederacy", "High Kingdom", "Circle", "Realm", "Pact", "March", "League", "Domain" },
                new[] { "frontier", "martial", "faith", "court", "forest", "highland", "river", "mountain" },
                new[]
                {
                    (0xFF1E4A2Bu, 0xFFBFD8A9u),
                    (0xFF36512Bu, 0xFFE3D2B5u),
                    (0xFF273B3Au, 0xFFCED9D4u),
                    (0xFF4C2235u, 0xFFE6C8D7u),
                    (0xFF2D5A3Fu, 0xFFCDE2B9u),
                    (0xFF2A4735u, 0xFFD2E1CBu),
                    (0xFF385840u, 0xFFE2DCC9u),
                    (0xFF304040u, 0xFFD5DEDAu),
                });

            AddCulture(result,
                "khuzait",
                new[] { "Skysteppe", "Thunderhoof", "Moonwind", "Sable Arrow", "Blue Dune", "Stormmane", "Falconstep", "Golden Yurt" },
                new[] { "Khaganate", "Horde", "Khanate", "Host", "March", "Confederacy", "Dominion", "Pact" },
                new[] { "nomad", "martial", "frontier", "trade", "steppe", "coastal", "river", "mountain" },
                new[]
                {
                    (0xFF16486Du, 0xFFD9C279u),
                    (0xFF2D3A4Du, 0xFFCAD8E7u),
                    (0xFF2A4255u, 0xFFE7D8BAu),
                    (0xFF4D341Fu, 0xFFE9C98Fu),
                    (0xFF2E5A4Fu, 0xFFC5E1D6u),
                    (0xFF3C4E63u, 0xFFDBD0B2u),
                    (0xFF5A4126u, 0xFFEED49Fu),
                    (0xFF244F59u, 0xFFD4E5D8u),
                });

            return result;
        }

        private static void AddCulture(
            List<KingdomPreset> result,
            string cultureId,
            string[] descriptors,
            string[] titles,
            string[] themes,
            (uint primary, uint secondary)[] palette)
        {
            int count = Math.Min(Math.Min(descriptors.Length, titles.Length), Math.Min(themes.Length, palette.Length));
            for (int i = 0; i < count; i++)
            {
                string id = "kg_" + cultureId + "_" + (i + 1).ToString("00");
                string formal = (descriptors[i] + " " + titles[i]).Trim();
                string informal = BuildInformalName(descriptors[i], titles[i]);
                var colors = palette[i];

                result.Add(new KingdomPreset(
                    id,
                    cultureId,
                    formal,
                    informal,
                    themes[i],
                    colors.primary,
                    colors.secondary));
            }
        }

        private static string BuildInformalName(string descriptor, string title)
        {
            if (string.IsNullOrWhiteSpace(descriptor))
                return title;

            if (title.IndexOf("Court", StringComparison.OrdinalIgnoreCase) >= 0
                || title.IndexOf("Circle", StringComparison.OrdinalIgnoreCase) >= 0
                || title.IndexOf("League", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return descriptor + " " + title;
            }

            return descriptor;
        }
    }
}
