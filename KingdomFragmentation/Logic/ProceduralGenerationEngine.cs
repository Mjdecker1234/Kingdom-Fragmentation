using System;
using System.Collections.Generic;
using System.Linq;
using KingdomFragmentation.Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace KingdomFragmentation.Logic
{
    /// <summary>
    /// Generates supplemental clans and heroes using wanderers or template cloning.
    /// Stateless — no settings dependency.
    /// </summary>
    public sealed class ProceduralGenerationEngine
    {
        private int _clanCounter;
        private int _heroCounter;

        // =================================================================
        // Public API
        // =================================================================

        /// <summary>
        /// Generate <paramref name="count"/> new clans, each with
        /// <paramref name="heroesPerClan"/> heroes.  Uses cultures and
        /// names from <paramref name="sourceClans"/> as templates.
        /// </summary>
        public List<Clan> GenerateClans(int count, int heroesPerClan,
            List<Clan> sourceClans)
        {
            if (count <= 0 || sourceClans == null || sourceClans.Count == 0)
                return new List<Clan>();

            var settlements = Settlement.All
                .Where(s => s != null && (s.IsTown || s.IsCastle))
                .ToList();
            if (settlements.Count == 0)
            {
                LogHelper.Warn("ProceduralGen: No town/castle settlements on map — cannot generate clans.");
                return new List<Clan>();
            }

            var wanderers = Hero.AllAliveHeroes
                .Where(IsAvailableWanderer)
                .ToList();

            var usedSettlements = new HashSet<Settlement>();
            var result = new List<Clan>();

            for (int i = 0; i < count; i++)
            {
                var template = sourceClans[i % sourceClans.Count];
                var anchor = PickAnchor(template, settlements, usedSettlements);

                Clan? clan = TryCreateFromWanderer(template, anchor, wanderers)
                          ?? TryCreateFromTemplate(template, anchor);

                if (clan == null)
                {
                    LogHelper.Warn("ProceduralGen: Could not create clan #" + (i + 1));
                    continue;
                }

                // Generate additional heroes (leader already counts as 1)
                int extraHeroes = Math.Max(0, heroesPerClan - 1);
                if (extraHeroes > 0)
                    GenerateHeroesForClan(clan, extraHeroes);

                usedSettlements.Add(anchor);
                result.Add(clan);
            }

            LogHelper.Info("ProceduralGen: created " + result.Count + " clan(s).");
            return result;
        }

        /// <summary>
        /// Add <paramref name="count"/> heroes to an existing clan.
        /// </summary>
        public void GenerateHeroesForClan(Clan clan, int count)
        {
            if (clan == null || count <= 0) return;

            var wanderers = Hero.AllAliveHeroes
                .Where(w => IsAvailableWanderer(w) && w.Culture == clan.Culture)
                .ToList();
            if (wanderers.Count == 0)
                wanderers = Hero.AllAliveHeroes.Where(IsAvailableWanderer).ToList();

            var anchor = clan.HomeSettlement
                ?? clan.Settlements.FirstOrDefault(s => s.IsTown || s.IsCastle)
                ?? Settlement.All.FirstOrDefault(s => s != null && s.IsTown);

            var templateHero = clan.Leader;

            for (int i = 0; i < count; i++)
            {
                // Prefer converting a wanderer
                var wanderer = wanderers.FirstOrDefault();
                if (wanderer != null)
                {
                    wanderers.Remove(wanderer);
                    AdoptWanderer(wanderer, clan, anchor, templateHero);
                    continue;
                }

                // Clone from template
                if (templateHero?.CharacterObject != null)
                    CloneHero(templateHero, clan, anchor);
            }
        }

        // =================================================================
        // Clan creation helpers
        // =================================================================

        private Clan? TryCreateFromWanderer(Clan template, Settlement anchor,
            List<Hero> wanderers)
        {
            var wanderer = wanderers.FirstOrDefault(w => w.Culture == template.Culture)
                        ?? wanderers.FirstOrDefault();
            if (wanderer == null) return null;

            wanderers.Remove(wanderer);

            string clanName = BuildClanName(template, anchor);

            Clan clan;
            try
            {
                clan = Clan.CreateCompanionToLordClan(
                    wanderer,
                    anchor,
                    new TextObject(clanName),
                    Math.Max(2, template.Tier));
            }
            catch (Exception ex)
            {
                LogHelper.Warn("CreateCompanionToLordClan failed: " + ex.Message);
                return null;
            }

            ConfigureClan(clan, template, anchor, clanName);
            AdoptWanderer(wanderer, clan, anchor, template.Leader);
            clan.SetLeader(wanderer);
            _clanCounter++;
            return clan;
        }

        private Clan? TryCreateFromTemplate(Clan template, Settlement anchor)
        {
            if (template.Leader?.CharacterObject == null) return null;

            _clanCounter++;
            string clanId = "kf_gen_clan_" + _clanCounter;
            string clanName = BuildClanName(template, anchor);

            Clan clan;
            try
            {
                clan = Clan.CreateClan(clanId);
            }
            catch (Exception ex)
            {
                LogHelper.Warn("Clan.CreateClan failed: " + ex.Message);
                return null;
            }

            ConfigureClan(clan, template, anchor, clanName);

            // Create leader hero
            var leader = CloneHero(template.Leader, clan, anchor);
            if (leader != null)
                clan.SetLeader(leader);

            return clan;
        }

        // =================================================================
        // Hero helpers
        // =================================================================

        private void AdoptWanderer(Hero wanderer, Clan clan, Settlement? anchor,
            Hero? templateHero)
        {
            wanderer.Clan = clan;
            wanderer.CompanionOf = clan;
            wanderer.SetNewOccupation(Occupation.Lord);
            if (anchor != null)
            {
                wanderer.BornSettlement = anchor;
                wanderer.StayingInSettlement = anchor;
            }
            wanderer.Gold = Math.Max(1000, templateHero?.Gold / 2 ?? 1000);
        }

        private Hero? CloneHero(Hero template, Clan clan, Settlement? anchor)
        {
            if (template?.CharacterObject == null) return null;

            _heroCounter++;
            string heroId = "kf_gen_hero_" + _heroCounter;
            string heroName = BuildHeroName(template);

            try
            {
                var hero = new Hero(heroId, template.CharacterObject,
                    CampaignTime.YearsFromNow(-(22f + (_heroCounter % 12))));
                hero.SetName(new TextObject(heroName), new TextObject(heroName));
                hero.Clan = clan;
                hero.CompanionOf = clan;
                hero.SetNewOccupation(Occupation.Lord);
                if (anchor != null)
                {
                    hero.BornSettlement = anchor;
                    hero.StayingInSettlement = anchor;
                }
                hero.Gold = Math.Max(1000, template.Gold / 2);
                return hero;
            }
            catch (Exception ex)
            {
                LogHelper.Warn("CloneHero failed: " + ex.Message);
                return null;
            }
        }

        // =================================================================
        // Configuration
        // =================================================================

        private static void ConfigureClan(Clan clan, Clan template,
            Settlement anchor, string name)
        {
            clan.ChangeClanName(new TextObject(name), new TextObject(name));
            clan.Culture = template.Culture;
            clan.IsNoble = true;
            clan.Renown = Math.Max(150f, template.Renown * 0.8f);
            clan.Banner = Banner.CreateRandomBanner();
            clan.Color = template.Color;
            clan.Color2 = template.Color2;
            clan.SetInitialHomeSettlement(anchor);
            try { clan.ConsiderAndUpdateHomeSettlement(); } catch { }
            clan.Influence = 0f;
        }

        // =================================================================
        // Naming
        // =================================================================

        private string BuildClanName(Clan template, Settlement anchor)
        {
            try
            {
                if (NameGenerator.Current != null && template.Culture != null)
                {
                    var generated = NameGenerator.Current.GenerateClanName(
                        template.Culture, anchor);
                    string name = generated?.ToString();
                    if (!string.IsNullOrWhiteSpace(name))
                        return name;
                }
            }
            catch (Exception ex)
            {
                LogHelper.Warn("NameGenerator.GenerateClanName failed: " + ex.Message);
            }

            // Fallback: culture-based name
            string baseName = anchor?.Name?.ToString()
                ?? template.Culture?.Name?.ToString()
                ?? "March";
            return "House of " + baseName;
        }

        private string BuildHeroName(Hero template)
        {
            try
            {
                if (NameGenerator.Current != null && template != null)
                {
                    var firstName = NameGenerator.Current.GenerateHeroFirstName(template);
                    string name = firstName?.ToString();
                    if (!string.IsNullOrWhiteSpace(name))
                        return name;
                }
            }
            catch (Exception ex)
            {
                LogHelper.Warn("NameGenerator.GenerateHeroFirstName failed: " + ex.Message);
            }

            return template?.Name?.ToString() ?? "Lord";
        }

        // =================================================================
        // Utilities
        // =================================================================

        private static bool IsAvailableWanderer(Hero hero)
        {
            if (hero == null || !hero.IsAlive) return false;
            if (hero == Hero.MainHero) return false;
            return hero.Occupation == Occupation.Wanderer
                && hero.Clan == null
                && hero.CompanionOf == null;
        }

        private static Settlement PickAnchor(Clan template,
            List<Settlement> settlements, HashSet<Settlement> used)
        {
            var preferred = template.Settlements
                .FirstOrDefault(s => s != null && (s.IsTown || s.IsCastle));

            var anchor = preferred ?? settlements.First();

            // Try to find an unused settlement near the anchor
            var pick = settlements
                .Where(s => !used.Contains(s))
                .OrderBy(s => anchor.GatePosition.Distance(s.GatePosition))
                .FirstOrDefault();

            return pick ?? anchor;
        }
    }
}
