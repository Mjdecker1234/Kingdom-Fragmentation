using System;
using System.Collections.Generic;

namespace KingdomFragmentation.Data
{
    public sealed class KingdomPreset
    {
        public string Id { get; }
        public string CultureId { get; }
        public string FormalName { get; }
        public string InformalName { get; }
        public string Theme { get; }
        public uint PrimaryColor { get; }
        public uint SecondaryColor { get; }

        public KingdomPreset(
            string id,
            string cultureId,
            string formalName,
            string informalName,
            string theme,
            uint primaryColor,
            uint secondaryColor)
        {
            Id = id;
            CultureId = cultureId;
            FormalName = formalName;
            InformalName = informalName;
            Theme = theme;
            PrimaryColor = primaryColor;
            SecondaryColor = secondaryColor;
        }
    }

    public sealed class ClanPreset
    {
        public string Id { get; }
        public string CultureId { get; }
        public string DisplayName { get; }
        public string InformalName { get; }
        public string Theme { get; }
        public string LeaderArchetypeId { get; }
        public IReadOnlyList<string> MemberArchetypeIds { get; }
        public uint PrimaryColor { get; }
        public uint SecondaryColor { get; }

        public ClanPreset(
            string id,
            string cultureId,
            string displayName,
            string informalName,
            string theme,
            string leaderArchetypeId,
            IReadOnlyList<string> memberArchetypeIds,
            uint primaryColor,
            uint secondaryColor)
        {
            Id = id;
            CultureId = cultureId;
            DisplayName = displayName;
            InformalName = informalName;
            Theme = theme;
            LeaderArchetypeId = leaderArchetypeId;
            MemberArchetypeIds = memberArchetypeIds;
            PrimaryColor = primaryColor;
            SecondaryColor = secondaryColor;
        }
    }

    public sealed class HeroArchetypePreset
    {
        public string Id { get; }
        public string CultureId { get; }
        public string DisplayName { get; }
        public string EquipmentStyle { get; }

        public int Leadership { get; }
        public int Tactics { get; }
        public int Charm { get; }
        public int Steward { get; }
        public int Trade { get; }
        public int OneHanded { get; }
        public int TwoHanded { get; }
        public int Polearm { get; }
        public int Bow { get; }
        public int Crossbow { get; }
        public int Riding { get; }
        public int Athletics { get; }
        public int Scouting { get; }
        public int Roguery { get; }
        public int Engineering { get; }

        public int ValorMin { get; }
        public int ValorMax { get; }
        public int HonorMin { get; }
        public int HonorMax { get; }
        public int MercyMin { get; }
        public int MercyMax { get; }
        public int GenerosityMin { get; }
        public int GenerosityMax { get; }
        public int CalculatingMin { get; }
        public int CalculatingMax { get; }

        public HeroArchetypePreset(
            string id,
            string cultureId,
            string displayName,
            string equipmentStyle,
            int leadership,
            int tactics,
            int charm,
            int steward,
            int trade,
            int oneHanded,
            int twoHanded,
            int polearm,
            int bow,
            int crossbow,
            int riding,
            int athletics,
            int scouting,
            int roguery,
            int engineering,
            int valorMin,
            int valorMax,
            int honorMin,
            int honorMax,
            int mercyMin,
            int mercyMax,
            int generosityMin,
            int generosityMax,
            int calculatingMin,
            int calculatingMax)
        {
            Id = id;
            CultureId = cultureId;
            DisplayName = displayName;
            EquipmentStyle = equipmentStyle;
            Leadership = leadership;
            Tactics = tactics;
            Charm = charm;
            Steward = steward;
            Trade = trade;
            OneHanded = oneHanded;
            TwoHanded = twoHanded;
            Polearm = polearm;
            Bow = bow;
            Crossbow = crossbow;
            Riding = riding;
            Athletics = athletics;
            Scouting = scouting;
            Roguery = roguery;
            Engineering = engineering;
            ValorMin = valorMin;
            ValorMax = valorMax;
            HonorMin = honorMin;
            HonorMax = honorMax;
            MercyMin = mercyMin;
            MercyMax = mercyMax;
            GenerosityMin = generosityMin;
            GenerosityMax = generosityMax;
            CalculatingMin = calculatingMin;
            CalculatingMax = calculatingMax;
        }

        public int PickTraitValue(Random rng, string traitKey)
        {
            switch (traitKey)
            {
                case "valor":
                    return rng.Next(ValorMin, ValorMax + 1);
                case "honor":
                    return rng.Next(HonorMin, HonorMax + 1);
                case "mercy":
                    return rng.Next(MercyMin, MercyMax + 1);
                case "generosity":
                    return rng.Next(GenerosityMin, GenerosityMax + 1);
                default:
                    return rng.Next(CalculatingMin, CalculatingMax + 1);
            }
        }
    }
}
