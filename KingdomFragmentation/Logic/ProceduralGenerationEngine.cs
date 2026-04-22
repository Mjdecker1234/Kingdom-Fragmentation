using System;
using System.Collections.Generic;
using System.Linq;
using KingdomFragmentation.Data;
using KingdomFragmentation.Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;

namespace KingdomFragmentation.Logic
{
    /// <summary>
    /// Generates supplemental clans and heroes using wanderers or template cloning.
    /// Stateless — no settings dependency.
    /// </summary>
    public sealed class ProceduralGenerationEngine
    {
        private readonly PresetAssignmentEngine? _presetEngine;
        private int _clanCounter;
        private int _heroCounter;

        public ProceduralGenerationEngine(PresetAssignmentEngine? presetEngine = null)
        {
            _presetEngine = presetEngine;
        }

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
                var clanPreset = _presetEngine?.DrawClanPreset(template.Culture?.StringId);

                Clan? clan = TryCreateFromWanderer(template, anchor, wanderers, clanPreset)
                          ?? TryCreateFromTemplate(template, anchor, clanPreset);

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
            var clanPreset = _presetEngine?.GetOrAssignClanPreset(clan);

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
                var archetype = _presetEngine?.DrawHeroArchetype(
                    clan.Culture?.StringId,
                    clanPreset,
                    false,
                    i + 1);

                // Prefer converting a wanderer
                var wanderer = wanderers.FirstOrDefault();
                if (wanderer != null)
                {
                    wanderers.Remove(wanderer);
                    AdoptWanderer(
                        wanderer,
                        clan,
                        anchor,
                        templateHero,
                        archetype,
                        false,
                        i + 1,
                        "topup_wanderer");
                    continue;
                }

                // Clone from template
                if (templateHero?.CharacterObject != null)
                    CloneHero(
                        templateHero,
                        clan,
                        anchor,
                        archetype,
                        false,
                        i + 1,
                        "topup_clone");
            }
        }

        // =================================================================
        // Clan creation helpers
        // =================================================================

        private Clan? TryCreateFromWanderer(
            Clan template,
            Settlement anchor,
            List<Hero> wanderers,
            ClanPreset? clanPreset)
        {
            var wanderer = wanderers.FirstOrDefault(w => w.Culture == template.Culture)
                        ?? wanderers.FirstOrDefault();
            if (wanderer == null) return null;

            wanderers.Remove(wanderer);

            string clanName = BuildClanName(template, anchor, clanPreset);

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

            ConfigureClan(clan, template, anchor, clanName, clanPreset);
            if (clanPreset != null)
                _presetEngine?.RegisterClanPreset(clan, clanPreset, "generated_wanderer");

            var leaderArchetype = _presetEngine?.DrawHeroArchetype(
                clan.Culture?.StringId,
                clanPreset,
                true,
                0);
            AdoptWanderer(
                wanderer,
                clan,
                anchor,
                template.Leader,
                leaderArchetype,
                true,
                0,
                "generated_leader_wanderer");
            clan.SetLeader(wanderer);
            _clanCounter++;
            return clan;
        }

        private Clan? TryCreateFromTemplate(
            Clan template,
            Settlement anchor,
            ClanPreset? clanPreset)
        {
            if (template.Leader?.CharacterObject == null) return null;

            _clanCounter++;
            string clanId = "kf_gen_clan_" + _clanCounter;
            string clanName = BuildClanName(template, anchor, clanPreset);

            Clan clan;
            try
            {
                clan = MBObjectManager.Instance.CreateObject<Clan>(clanId);
            }
            catch (Exception ex)
            {
                LogHelper.Warn("MBObjectManager.CreateObject<Clan> failed: " + ex.Message);
                return null;
            }

            ConfigureClan(clan, template, anchor, clanName, clanPreset);
            if (clanPreset != null)
                _presetEngine?.RegisterClanPreset(clan, clanPreset, "generated_template");

            // Create leader hero
            var leaderArchetype = _presetEngine?.DrawHeroArchetype(
                clan.Culture?.StringId,
                clanPreset,
                true,
                0);
            var leader = CloneHero(
                template.Leader,
                clan,
                anchor,
                leaderArchetype,
                true,
                0,
                "generated_leader_clone");
            if (leader != null)
                clan.SetLeader(leader);

            return clan;
        }

        // =================================================================
        // Hero helpers
        // =================================================================

        private void AdoptWanderer(
            Hero wanderer,
            Clan clan,
            Settlement? anchor,
            Hero? templateHero,
            HeroArchetypePreset? archetype,
            bool isLeader,
            int slotIndex,
            string source)
        {
            wanderer.CompanionOf = null;
            wanderer.Clan = clan;
            wanderer.SetNewOccupation(Occupation.Lord);
            if (anchor != null)
            {
                wanderer.BornSettlement = anchor;
                wanderer.StayingInSettlement = anchor;
            }
            wanderer.Gold = Math.Max(1000, templateHero?.Gold / 2 ?? 1000);
            FinalizeHero(wanderer, clan, anchor, archetype);
            _presetEngine?.RecordHeroArchetypeAssignment(
                clan,
                wanderer,
                archetype,
                isLeader,
                slotIndex,
                source);
        }

        private Hero? CloneHero(
            Hero template,
            Clan clan,
            Settlement? anchor,
            HeroArchetypePreset? archetype,
            bool isLeader,
            int slotIndex,
            string source)
        {
            if (template?.CharacterObject == null) return null;

            _heroCounter++;
            string heroName = BuildHeroName(template);

            try
            {
                var hero = HeroCreator.CreateSpecialHero(
                    template.CharacterObject,
                    anchor,
                    clan,
                    null,
                    22 + (_heroCounter % 12));
                hero.SetName(new TextObject(heroName), new TextObject(heroName));
                hero.Clan = clan;
                hero.SetNewOccupation(Occupation.Lord);
                hero.Gold = Math.Max(1000, template.Gold / 2);
                FinalizeHero(hero, clan, anchor, archetype);
                _presetEngine?.RecordHeroArchetypeAssignment(
                    clan,
                    hero,
                    archetype,
                    isLeader,
                    slotIndex,
                    source);
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

        private static void ConfigureClan(
            Clan clan,
            Clan template,
            Settlement anchor,
            string name,
            ClanPreset? clanPreset)
        {
            string informal = clanPreset?.InformalName ?? name;
            clan.ChangeClanName(new TextObject(name), new TextObject(informal));
            clan.Culture = template.Culture;
            clan.IsNoble = true;
            clan.Renown = Math.Max(150f, template.Renown * 0.8f);
            string bannerSeed = clanPreset?.Id ?? (clan.StringId ?? name);
            clan.Banner = BannerGenerator.GenerateUniqueBanner("clan_" + bannerSeed);
            clan.Color = clanPreset?.PrimaryColor ?? template.Color;
            clan.Color2 = clanPreset?.SecondaryColor ?? template.Color2;
            clan.SetInitialHomeSettlement(anchor);
            try { clan.ConsiderAndUpdateHomeSettlement(); } catch { }
            clan.Influence = 0f;
        }

        // =================================================================
        // Naming
        // =================================================================

        private string BuildClanName(
            Clan template,
            Settlement anchor,
            ClanPreset? clanPreset)
        {
            if (clanPreset != null && !string.IsNullOrWhiteSpace(clanPreset.DisplayName))
                return clanPreset.DisplayName;

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
        // Hero finalization — traits, skills, equipment, state, bio
        // =================================================================

        /// <summary>
        /// Complete a generated hero with traits, skills, equipment, state,
        /// and encyclopedia text so they function like a proper lord.
        /// </summary>
        private void FinalizeHero(
            Hero hero,
            Clan clan,
            Settlement? anchor,
            HeroArchetypePreset? archetype)
        {
            if (hero == null) return;

            EnsureActiveState(hero);
            if (archetype != null)
            {
                ApplyArchetypeTraits(hero, archetype);
                ApplyArchetypeSkills(hero, archetype);
                AssignLordEquipment(hero, clan, archetype.EquipmentStyle);
                SetEncyclopediaText(hero, clan, archetype);
                return;
            }

            SetRandomTraits(hero);
            BoostLordSkillsRandom(hero);
            AssignLordEquipment(hero, clan, "heavy");
            SetEncyclopediaText(hero, clan, null);
        }

        private static void EnsureActiveState(Hero hero)
        {
            try
            {
                if (hero.HeroState != Hero.CharacterStates.Active)
                    hero.ChangeState(Hero.CharacterStates.Active);
            }
            catch (Exception ex)
            {
                LogHelper.Debug("Could not set hero to Active: " + ex.Message);
            }
        }

        private static void SetRandomTraits(Hero hero)
        {
            int seed = StableHash(hero.StringId ?? hero.Name?.ToString() ?? "hero");
            var rng = new Random(seed);

            try
            {
                hero.SetTraitLevel(DefaultTraits.Valor, rng.Next(-2, 3));
                hero.SetTraitLevel(DefaultTraits.Honor, rng.Next(-2, 3));
                hero.SetTraitLevel(DefaultTraits.Mercy, rng.Next(-2, 3));
                hero.SetTraitLevel(DefaultTraits.Generosity, rng.Next(-2, 3));
                hero.SetTraitLevel(DefaultTraits.Calculating, rng.Next(-2, 3));
            }
            catch (Exception ex)
            {
                LogHelper.Debug("SetRandomTraits failed: " + ex.Message);
            }
        }

        private static void BoostLordSkillsRandom(Hero hero)
        {
            int seed = StableHash((hero.StringId ?? "skills") + "_boost");
            var rng = new Random(seed);

            // Leadership skills — lords need these at decent levels
            var lordSkills = new[]
            {
                DefaultSkills.Leadership,
                DefaultSkills.Tactics,
                DefaultSkills.Charm,
                DefaultSkills.Steward,
                DefaultSkills.Trade
            };

            foreach (var skill in lordSkills)
            {
                try
                {
                    int current = hero.GetSkillValue(skill);
                    if (current < 80)
                    {
                        float targetLevel = 100 + rng.Next(80);
                        float xpToAdd = (targetLevel - current) * 1500f;
                        if (xpToAdd > 0)
                            hero.AddSkillXp(skill, xpToAdd);
                    }
                }
                catch { }
            }

            // Combat skills — lords should be competent fighters
            var combatSkills = new[]
            {
                DefaultSkills.OneHanded,
                DefaultSkills.Riding,
                DefaultSkills.Athletics,
                DefaultSkills.Polearm
            };

            foreach (var skill in combatSkills)
            {
                try
                {
                    int current = hero.GetSkillValue(skill);
                    if (current < 60)
                    {
                        float targetLevel = 60 + rng.Next(60);
                        float xpToAdd = (targetLevel - current) * 1200f;
                        if (xpToAdd > 0)
                            hero.AddSkillXp(skill, xpToAdd);
                    }
                }
                catch { }
            }
        }

        private static void ApplyArchetypeTraits(Hero hero, HeroArchetypePreset archetype)
        {
            int seed = StableHash((hero.StringId ?? hero.Name?.ToString() ?? "hero") + "|traits|" + archetype.Id);
            var rng = new Random(seed);
            try
            {
                hero.SetTraitLevel(DefaultTraits.Valor, archetype.PickTraitValue(rng, "valor"));
                hero.SetTraitLevel(DefaultTraits.Honor, archetype.PickTraitValue(rng, "honor"));
                hero.SetTraitLevel(DefaultTraits.Mercy, archetype.PickTraitValue(rng, "mercy"));
                hero.SetTraitLevel(DefaultTraits.Generosity, archetype.PickTraitValue(rng, "generosity"));
                hero.SetTraitLevel(DefaultTraits.Calculating, archetype.PickTraitValue(rng, "calculating"));
            }
            catch (Exception ex)
            {
                LogHelper.Debug("ApplyArchetypeTraits failed: " + ex.Message);
            }
        }

        private void ApplyArchetypeSkills(Hero hero, HeroArchetypePreset archetype)
        {
            RaiseSkillTowardTarget(
                hero,
                DefaultSkills.Leadership,
                ScaleArchetypeTarget(archetype.Leadership, "command"),
                1500f);
            RaiseSkillTowardTarget(
                hero,
                DefaultSkills.Tactics,
                ScaleArchetypeTarget(archetype.Tactics, "command"),
                1500f);
            RaiseSkillTowardTarget(
                hero,
                DefaultSkills.Charm,
                ScaleArchetypeTarget(archetype.Charm, "command"),
                1450f);
            RaiseSkillTowardTarget(
                hero,
                DefaultSkills.Steward,
                ScaleArchetypeTarget(archetype.Steward, "civil"),
                1500f);
            RaiseSkillTowardTarget(
                hero,
                DefaultSkills.Trade,
                ScaleArchetypeTarget(archetype.Trade, "civil"),
                1500f);

            RaiseSkillTowardTarget(
                hero,
                DefaultSkills.OneHanded,
                ScaleArchetypeTarget(archetype.OneHanded, "combat"),
                1300f);
            RaiseSkillTowardTarget(
                hero,
                DefaultSkills.TwoHanded,
                ScaleArchetypeTarget(archetype.TwoHanded, "combat"),
                1300f);
            RaiseSkillTowardTarget(
                hero,
                DefaultSkills.Polearm,
                ScaleArchetypeTarget(archetype.Polearm, "combat"),
                1300f);
            RaiseSkillTowardTarget(
                hero,
                DefaultSkills.Bow,
                ScaleArchetypeTarget(archetype.Bow, "combat"),
                1300f);
            RaiseSkillTowardTarget(
                hero,
                DefaultSkills.Crossbow,
                ScaleArchetypeTarget(archetype.Crossbow, "combat"),
                1300f);
            RaiseSkillTowardTarget(
                hero,
                DefaultSkills.Riding,
                ScaleArchetypeTarget(archetype.Riding, "combat"),
                1200f);
            RaiseSkillTowardTarget(
                hero,
                DefaultSkills.Athletics,
                ScaleArchetypeTarget(archetype.Athletics, "combat"),
                1200f);
            RaiseSkillTowardTarget(
                hero,
                DefaultSkills.Scouting,
                ScaleArchetypeTarget(archetype.Scouting, "combat"),
                1250f);
            RaiseSkillTowardTarget(
                hero,
                DefaultSkills.Roguery,
                ScaleArchetypeTarget(archetype.Roguery, "combat"),
                1250f);
            RaiseSkillTowardTarget(
                hero,
                DefaultSkills.Engineering,
                ScaleArchetypeTarget(archetype.Engineering, "civil"),
                1400f);
        }

        private int ScaleArchetypeTarget(int baseValue, string category)
        {
            if (_presetEngine == null)
                return baseValue;

            return _presetEngine.ScaleArchetypeSkillTarget(baseValue, category);
        }

        private static void RaiseSkillTowardTarget(
            Hero hero,
            SkillObject skill,
            int target,
            float xpPerLevel)
        {
            try
            {
                int current = hero.GetSkillValue(skill);
                if (current >= target) return;
                float xpToAdd = (target - current) * xpPerLevel;
                if (xpToAdd > 0f)
                    hero.AddSkillXp(skill, xpToAdd);
            }
            catch { }
        }

        // Culture → equipment template mapping
        private static readonly Dictionary<string, (string battle, string civilian, string lady)>
            CultureEquipmentMap = new Dictionary<string, (string, string, string)>
        {
            { "empire",   ("emp_bat_template_heavy", "emp_civ_template_default", "emp_bat_template_lady") },
            { "sturgia",  ("stu_bat_template_heavy", "stu_civ_template_default", "stu_bat_template_lady") },
            { "aserai",   ("ase_bat_template_heavy", "ase_civ_template_default", "ase_bat_template_lady") },
            { "vlandia",  ("vla_bat_template_heavy", "vla_civ_template_default", "vla_bat_template_lady") },
            { "battania", ("bat_bat_template_heavy", "bat_civ_template_default", "bat_bat_template_lady") },
            { "khuzait",  ("khu_bat_template_heavy", "khu_civ_template_default", "khu_bat_template_lady") },
        };

        private static void AssignLordEquipment(Hero hero, Clan clan, string? equipmentStyle)
        {
            string cultureId = clan?.Culture?.StringId?.ToLowerInvariant() ?? "empire";
            if (!CultureEquipmentMap.TryGetValue(cultureId, out var templates))
                templates = CultureEquipmentMap["empire"];

            string style = string.IsNullOrWhiteSpace(equipmentStyle)
                ? "heavy"
                : (equipmentStyle ?? "heavy").ToLowerInvariant();
            string civilianId = templates.civilian;

            // Battle equipment
            try
            {
                if (!TryFillFromRosters(hero.BattleEquipment, GetBattleRosterCandidates(cultureId, style, hero.IsFemale, templates)))
                    LogHelper.Debug("AssignLordEquipment battle: no matching roster found for style '" + style + "'.");
            }
            catch (Exception ex)
            {
                LogHelper.Debug("AssignLordEquipment battle failed: " + ex.Message);
            }

            // Civilian equipment
            try
            {
                var civRoster = MBObjectManager.Instance.GetObject<MBEquipmentRoster>(civilianId);
                if (civRoster?.DefaultEquipment != null)
                {
                    hero.CivilianEquipment.FillFrom(civRoster.DefaultEquipment);
                }
                else if (civRoster?.AllEquipments?.Count > 0)
                {
                    hero.CivilianEquipment.FillFrom(civRoster.AllEquipments[0]);
                }
            }
            catch (Exception ex)
            {
                LogHelper.Debug("AssignLordEquipment civilian failed for '" + civilianId + "': " + ex.Message);
            }
        }

        private static string[] GetBattleRosterCandidates(
            string cultureId,
            string style,
            bool isFemale,
            (string battle, string civilian, string lady) fallbackTemplates)
        {
            if (isFemale)
                return new[] { fallbackTemplates.lady, fallbackTemplates.battle };

            string prefix = cultureId switch
            {
                "empire" => "emp",
                "sturgia" => "stu",
                "aserai" => "ase",
                "vlandia" => "vla",
                "battania" => "bat",
                "khuzait" => "khu",
                _ => "emp"
            };

            if (style == "mounted")
            {
                return new[]
                {
                    prefix + "_bat_template_cavalry",
                    prefix + "_bat_template_horseman",
                    prefix + "_bat_template_knight",
                    fallbackTemplates.battle
                };
            }

            if (style == "ranged")
            {
                return new[]
                {
                    prefix + "_bat_template_archer",
                    prefix + "_bat_template_crossbow",
                    prefix + "_bat_template_ranged",
                    fallbackTemplates.battle
                };
            }

            if (style == "support")
            {
                return new[]
                {
                    prefix + "_bat_template_infantry",
                    prefix + "_bat_template_light",
                    fallbackTemplates.battle
                };
            }

            return new[] { fallbackTemplates.battle };
        }

        private static bool TryFillFromRosters(
            Equipment equipment,
            IEnumerable<string> rosterIds)
        {
            foreach (var rosterId in rosterIds)
            {
                if (string.IsNullOrWhiteSpace(rosterId))
                    continue;

                var roster = MBObjectManager.Instance.GetObject<MBEquipmentRoster>(rosterId);
                if (roster?.DefaultEquipment != null)
                {
                    equipment.FillFrom(roster.DefaultEquipment);
                    return true;
                }
                if (roster?.AllEquipments?.Count > 0)
                {
                    equipment.FillFrom(roster.AllEquipments[0]);
                    return true;
                }
            }
            return false;
        }

        private static void SetEncyclopediaText(
            Hero hero,
            Clan clan,
            HeroArchetypePreset? archetype)
        {
            try
            {
                string cultureName = clan?.Culture?.Name?.ToString() ?? "the realm";
                string clanName = clan?.Name?.ToString() ?? "a noble house";
                string roleText = archetype != null
                    ? " A noted " + archetype.DisplayName.ToLowerInvariant() + ","
                    : string.Empty;
                hero.EncyclopediaText = new TextObject(
                    hero.Name + " is a lord of " + clanName + "." + roleText
                    + " hailing from the lands of " + cultureName + ".");
            }
            catch (Exception ex)
            {
                LogHelper.Debug("SetEncyclopediaText failed: " + ex.Message);
            }
        }

        private static int StableHash(string value)
        {
            unchecked
            {
                int hash = 17;
                for (int i = 0; i < value.Length; i++)
                    hash = (hash * 31) + value[i];
                return hash;
            }
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
