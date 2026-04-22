using System.Collections.Generic;

namespace KingdomFragmentation.Data
{
    public static class HeroArchetypeCatalog
    {
        private static readonly IReadOnlyList<HeroArchetypePreset> Archetypes =
            new List<HeroArchetypePreset>
            {
                new HeroArchetypePreset(
                    "warlord", "any", "Warlord", "heavy",
                    170, 155, 110, 90, 70,
                    145, 110, 160, 60, 40, 135, 115, 90, 70, 60,
                    1, 3, -1, 2, -2, 1, -1, 1, 0, 3),

                new HeroArchetypePreset(
                    "marshal", "any", "Marshal", "heavy",
                    150, 165, 100, 95, 70,
                    130, 90, 150, 70, 50, 130, 110, 105, 65, 70,
                    1, 2, 0, 2, -1, 1, -1, 1, 1, 3),

                new HeroArchetypePreset(
                    "tactician", "any", "Tactician", "support",
                    140, 175, 105, 110, 90,
                    100, 80, 120, 80, 65, 105, 95, 130, 70, 120,
                    0, 2, -1, 2, -1, 1, -1, 1, 2, 3),

                new HeroArchetypePreset(
                    "steward", "any", "Steward", "support",
                    115, 105, 120, 180, 130,
                    80, 70, 90, 60, 60, 85, 90, 95, 55, 110,
                    -1, 1, 0, 2, 0, 2, 1, 3, 1, 3),

                new HeroArchetypePreset(
                    "diplomat", "any", "Diplomat", "support",
                    120, 110, 185, 125, 120,
                    75, 65, 85, 55, 55, 80, 85, 95, 50, 90,
                    -1, 1, 1, 3, 0, 2, 1, 3, 1, 3),

                new HeroArchetypePreset(
                    "merchant_prince", "any", "Merchant Prince", "support",
                    120, 110, 150, 155, 190,
                    80, 65, 90, 60, 60, 90, 85, 100, 70, 105,
                    -1, 1, 0, 2, -1, 2, 1, 3, 1, 3),

                new HeroArchetypePreset(
                    "champion", "any", "Champion", "heavy",
                    130, 120, 90, 80, 55,
                    170, 150, 165, 50, 40, 120, 140, 80, 60, 55,
                    2, 3, -2, 0, -2, 0, -2, 1, 0, 2),

                new HeroArchetypePreset(
                    "raider", "any", "Raider", "mounted",
                    105, 115, 85, 70, 60,
                    120, 95, 130, 135, 70, 150, 120, 145, 145, 65,
                    1, 3, -2, 0, -3, -1, -2, 0, 1, 3),

                new HeroArchetypePreset(
                    "outrider", "any", "Outrider", "mounted",
                    110, 125, 90, 85, 75,
                    115, 90, 125, 140, 70, 170, 115, 155, 110, 70,
                    1, 2, -1, 1, -2, 0, -1, 1, 1, 3),

                new HeroArchetypePreset(
                    "archer_captain", "any", "Archer Captain", "ranged",
                    120, 130, 95, 90, 70,
                    95, 75, 110, 170, 115, 120, 110, 130, 80, 75,
                    0, 2, -1, 1, -1, 1, -1, 1, 1, 3),

                new HeroArchetypePreset(
                    "engineer", "any", "Engineer", "support",
                    100, 130, 95, 120, 100,
                    70, 60, 80, 70, 65, 75, 85, 105, 60, 190,
                    -1, 1, -1, 1, 0, 2, 0, 2, 2, 3),

                new HeroArchetypePreset(
                    "zealot_lord", "any", "Zealot Lord", "heavy",
                    145, 120, 110, 100, 60,
                    130, 105, 145, 75, 55, 120, 125, 90, 55, 75,
                    1, 3, 1, 3, -3, -1, -2, 0, 0, 2),

                new HeroArchetypePreset(
                    "ranger_lord", "any", "Ranger Lord", "ranged",
                    130, 135, 105, 95, 85,
                    110, 90, 120, 155, 85, 135, 125, 150, 90, 80,
                    1, 2, 0, 2, -1, 1, -1, 1, 1, 3),

                new HeroArchetypePreset(
                    "steppe_khan", "khuzait", "Steppe Khan", "mounted",
                    165, 150, 95, 90, 70,
                    130, 90, 155, 150, 70, 185, 110, 145, 110, 75,
                    2, 3, -2, 0, -2, 0, -2, 0, 1, 3),

                new HeroArchetypePreset(
                    "coastal_reaver", "any", "Coastal Reaver", "mounted",
                    120, 125, 95, 85, 95,
                    125, 95, 135, 140, 80, 145, 120, 125, 135, 70,
                    1, 3, -2, 0, -2, 0, -2, 0, 1, 3),

                new HeroArchetypePreset(
                    "mountain_guard", "any", "Mountain Guard", "heavy",
                    140, 145, 90, 100, 70,
                    145, 120, 160, 85, 65, 105, 150, 110, 75, 80,
                    1, 3, -1, 1, -1, 1, -1, 1, 1, 3),

                new HeroArchetypePreset(
                    "river_warden", "any", "River Warden", "ranged",
                    130, 140, 100, 110, 90,
                    110, 85, 120, 150, 90, 120, 120, 145, 85, 90,
                    0, 2, 0, 2, -1, 1, -1, 1, 1, 3),

                new HeroArchetypePreset(
                    "highland_chief", "any", "Highland Chief", "heavy",
                    150, 140, 95, 95, 70,
                    150, 130, 160, 90, 65, 125, 145, 120, 80, 75,
                    1, 3, -1, 1, -2, 0, -1, 1, 1, 3),

                new HeroArchetypePreset(
                    "desert_lancer", "aserai", "Desert Lancer", "mounted",
                    135, 130, 90, 85, 80,
                    130, 95, 145, 125, 75, 175, 115, 135, 105, 70,
                    1, 3, -1, 1, -2, 0, -1, 1, 1, 3),

                new HeroArchetypePreset(
                    "forest_ranger", "battania", "Forest Ranger", "ranged",
                    135, 140, 100, 100, 85,
                    115, 95, 125, 165, 85, 130, 130, 160, 90, 80,
                    1, 2, 0, 2, -1, 1, -1, 1, 1, 3),
            };

        public static IReadOnlyList<HeroArchetypePreset> GetAll()
        {
            return Archetypes;
        }
    }
}
