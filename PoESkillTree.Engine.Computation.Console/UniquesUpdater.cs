﻿using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NLog;
using PoESkillTree.Engine.GameModel.Items;
using PoESkillTree.Engine.Utils;
using PoESkillTree.Engine.Utils.Extensions;
using PoESkillTree.Engine.Utils.WikiApi;
using static MoreLinq.Extensions.DistinctByExtension;
using static PoESkillTree.Engine.Utils.WikiApi.CargoConstants;

namespace PoESkillTree.Engine.Computation.Console
{
    internal class UniquesUpdater
    {
        private static readonly ILogger Log = LogManager.GetCurrentClassLogger();

        private static readonly IReadOnlyList<string> Fields = new[]
        {
            Name, BaseItemId, RequiredLevel, DropEnabled
        };

        private static readonly IReadOnlyList<string> JewelFields = Fields.Concat(new[]
        {
            JewelLimit
        }).ToList();

        private static readonly IReadOnlyList<string> RelevantWikiClasses = new[]
        {
            "One Hand Axes", "Two Hand Axes", "Bows", "Claws", "Daggers", "Rune Dagger",
            "One Hand Maces", "Sceptres", "Two Hand Maces", "Staves", "Warstaff",
            "One Hand Swords", "Thrusting One Hand Swords", "Two Hand Swords", "Wands",
            "Amulets", "Belts", "Quivers", "Rings",
            "Body Armours", "Boots", "Helmets", "Gloves", "Shields",
            "Life Flasks", "Mana Flasks", "Hybrid Flasks", "Utility Flasks", "Critical Utility Flasks",
        };

        private static readonly IReadOnlyList<string> Blacklist = new[] { "Band of the Victor" };

        private const string JewelClass = "Jewel";

        private readonly ApiAccessor _apiAccessor;
        private readonly string _savePath;

        public UniquesUpdater(HttpClient httpClient, string basePath)
        {
            _apiAccessor = new ApiAccessor(httpClient);
            _savePath = basePath + "PoESkillTree.Engine.GameModel/Data/Uniques.xml";
        }

        public async Task UpdateAsync()
        {
            var mods = await LoadMods();
            var uniqueTasks = RelevantWikiClasses.Select(c => LoadAsync(c, mods))
                .Append(LoadJewelsAsync(mods));
            var results = await Task.WhenAll(uniqueTasks).ConfigureAwait(false);
            var uniqueList = new XmlUniqueList
            {
                Uniques = results.Flatten().ToArray()
            };
            XmlSerializationUtils.SerializeToFile(uniqueList, _savePath);
        }

        private async Task<ILookup<string, string>> LoadMods()
        {
            var results = await QueryApiForModsAsync().ConfigureAwait(false);
            Log.Info($"Retrieved {results.Count} unique item mods.");
            return results.ToLookup(
                j => j.Value<string>(ApiAccessor.GetPageNameFieldAlias(ItemTableName)),
                j => j.Value<string>(ModId));
        }

        private async Task<IEnumerable<XmlUnique>> LoadAsync(string wikiClass, ILookup<string, string> mods)
        {
            var results = await QueryApiForUniquesAsync(wikiClass).ConfigureAwait(false);
            return ReadJson(wikiClass, results, mods);
        }

        private async Task<IEnumerable<XmlUnique>> LoadJewelsAsync(ILookup<string, string> mods)
        {
            var results = await QueryApiForJewelsAsync().ConfigureAwait(false);
            return ReadJson(JewelClass, results, mods);
        }

        private Task<IReadOnlyList<JToken>> QueryApiForModsAsync()
        {
            string[] tables = { ItemTableName, ItemModTableName };
            string[] fields = { ModId };
            var where = $"{Rarity}='Unique' AND is_implicit=false AND is_random=false AND {ModId} != ''";
            var joinOn = $"{ItemTableName}.{PageName}={ItemModTableName}.{PageName}";
            return _apiAccessor.CargoQueryAsync(tables, fields, where, joinOn);
        }

        private Task<IReadOnlyList<JToken>> QueryApiForUniquesAsync(string wikiClass)
        {
            string[] tables = { ItemTableName };
            var where = GetWhereClause(wikiClass);
            return _apiAccessor.CargoQueryAsync(tables, Fields, where);
        }

        private Task<IReadOnlyList<JToken>> QueryApiForJewelsAsync()
        {
            string[] tables = { ItemTableName, JewelTableName };
            var where = GetWhereClause(JewelClass);
            var joinOn = $"{ItemTableName}.{PageName}={JewelTableName}.{PageName}";
            return _apiAccessor.CargoQueryAsync(tables, JewelFields, where, joinOn);
        }

        private static string GetWhereClause(string wikiClass)
        {
            return $"{Rarity}='Unique' AND {CargoConstants.ItemClass}='{wikiClass}'";
        }

        private static IEnumerable<XmlUnique> ReadJson(
            string wikiClass, IEnumerable<JToken> results, ILookup<string, string> mods)
        {
            results = results.DistinctBy(j => j.Value<string>(ApiAccessor.GetPageNameFieldAlias(ItemTableName)));
            List<XmlUnique> uniques = (
                from result in results
                let unique = PrintoutsToUnique(result, mods)
                where !Blacklist.Contains(unique.Name)
                orderby unique.Name
                select unique
            ).ToList();
            Log.Info($"Retrieved {uniques.Count} uniques of class {wikiClass}.");
            return uniques;
        }

        private static XmlUnique PrintoutsToUnique(JToken printouts, ILookup<string, string> mods)
        {
            var properties = new List<string>();
            if (printouts[JewelLimit]?.Value<string>() is string itemLimit && itemLimit.Length > 0)
            {
                properties.Add($"Limited to: {itemLimit}");
            }

            return new XmlUnique
            {
                Level = int.Parse(printouts.Value<string>(RequiredLevel)),
                Name = printouts.Value<string>(Name),
                DropDisabled = printouts.Value<string>(DropEnabled) == "0",
                BaseMetadataId = printouts.Value<string>(BaseItemId),
                Explicit = mods[printouts.Value<string>("items_page_name")].ToArray(),
                Properties = properties.ToArray()
            };
        }
    }
}