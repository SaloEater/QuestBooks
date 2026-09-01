using MonoMod.Utils;
using Newtonsoft.Json;
using QuestBooks.QuestLog;
using QuestBooks.Quests;
using QuestBooks.Quests.VanillaQuests;
using QuestBooks.Utilities;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Terraria.ModLoader.Core;
using Terraria.ModLoader.IO;

namespace QuestBooks.Systems
{
    internal class QuestLoader : ModSystem
    {
        private const string TagKey = "QuestBooks:CompletedQuests";
        private const string ProgressTagKey = "QuestBooks:QuestProgress";

        // We use a separate dictionary for loading and accessing to
        // improve performance and ensure that quests are not modified post-setup.
        private static readonly HashSet<Assembly> checkedAssemblies = [];
        internal static readonly Dictionary<Type, string> loadingQuests = [];

        /// <summary>
        /// Whether or not the quest completion status' have been loaded for a selected world/character.
        /// </summary>
        public static bool QuestsLoaded { get; private set; } = false;

        public static Dictionary<Type, Mod> QuestMods { get; } = [];
        public static FrozenDictionary<Type, string> QuestKeys { get; internal set; }

        public static QuestLogStyle ExclusiveOverrideStyle { get; internal set; } = null;
        public static Dictionary<Mod, List<QuestLogStyle>> LogStyleRegistry { get; } = [];

        internal static void LoadQuests(Mod mod)
        {
            var loadingAssembly = mod.Code;

            if (checkedAssemblies.Contains(loadingAssembly))
                return;

            checkedAssemblies.Add(loadingAssembly);
            var types = AssemblyManager.GetLoadableTypes(loadingAssembly);

            QuestManager.AvailableQuestBookTypes.AddRange(types.Where(t => !t.IsAbstract && t.IsAssignableTo(typeof(QuestBook))).OrderBy(t => t.Name));
            QuestManager.AvailableQuestLineTypes.AddRange(types.Where(t => !t.IsAbstract && t.IsAssignableTo(typeof(QuestChapter))).OrderBy(t => t.Name));

            QuestManager.AvailableQuestElementTypes.AddRange(types.Where(t => !t.IsAbstract && t.IsAssignableTo(typeof(QuestLogElement)))
                .Select(t => new KeyValuePair<Type, QuestLogElement>(t, (QuestLogElement)Activator.CreateInstance(t)))
                .Select(kvp => { kvp.Value.TemplateInstance = true; return kvp; })
                .OrderBy(kvp => kvp.Key.Name)
                .ToDictionary());

            // "Quest" objects are registered via ModType functionality and do not need manual registering here.
        }

        // Mods should load their quest logs in PostSetupContent().
        // Following that, we freeze the dictionary and reset the loading.
        public override void PostAddRecipes()
        {
            QuestKeys = loadingQuests.ToFrozenDictionary();
            loadingQuests.Clear();
            checkedAssemblies.Clear();

            //if (QuestManager.QuestBooks.Count == 0)
            //    VanillaQuests.AddVanillaQuests();

            QuestLogDrawer.QuestLogStyles = LogStyleRegistry
                .SelectMany(kvp => kvp.Value)
                .Select(q => new KeyValuePair<string, QuestLogStyle>(q.Key, q))
                .ToDictionary();
        }

        public override void PostSetupRecipes()
        {
            QuestManager.SelectQuestLog(QuestManager.AvailableQuestLogs.First().Key);
            QuestLogDrawer.SelectLogStyle(ExclusiveOverrideStyle ?? QuestLogDrawer.QuestLogStyles.First().Value);
        }

        #region Quest Loading

        public override void LoadWorldData(TagCompound tag)
        {
            // Attempt to fetch completed quest data from world.
            // If it does not exist, leave all quests as incomplete.
            if (tag.TryGet(TagKey, out string[] quests))
            {
                LoadCompletedQuests(quests, out var unloaded);

                foreach (string quest in unloaded)
                    QuestManager.UnloadedCompletedWorldQuests.Add(quest);
            }

            // Progress is restored before the completion sweep below, so that a quest whose
            // restored progress already meets its goal is marked complete right away.
            LoadQuestProgress(QuestManager.WorldQuests, tag);

            // Immediately mark any quests that should have previously been completed
            foreach (var quest in QuestManager.IncompleteWorldQuests.Select(QuestManager.GetQuest).ToArray())
            {
                if (quest.CheckCompletion())
                    QuestManager.MarkComplete(quest);
            }

            QuestLogDrawer.ActiveStyle.LoadWorldData(tag);
        }

        public partial class PlayerQuestLoader : ModPlayer
        {
            // Cache the tag compound as we only want to read it for the actual player
            // entering the world.
            private TagCompound tagCompound = new();

            public override void LoadData(TagCompound tag) => tagCompound = tag;

            public override void OnEnterWorld()
            {
                // Attempt to fetch completed quest data from player.
                // If it does not exist, leave all player quests as incomplete.
                if (tagCompound?.TryGet(TagKey, out string[] quests) ?? false)
                {
                    LoadCompletedQuests(quests, out var unloaded);

                    foreach (string quest in unloaded)
                        QuestManager.UnloadedCompletedPlayerQuests.Add(quest);
                }

                // Progress is restored before the completion sweep below, so that a quest whose
                // restored progress already meets its goal is marked complete right away.
                LoadQuestProgress(QuestManager.PlayerQuests, tagCompound);

                foreach (var quest in QuestManager.IncompletePlayerQuests.Select(QuestManager.GetQuest).ToArray())
                {
                    if (quest.CheckCompletion())
                        QuestManager.MarkComplete(quest);
                }

                QuestLogDrawer.ActiveStyle.LoadPlayerData(tagCompound);

                QuestsLoaded = true;
            }
        }

        #endregion

        #region Quest Saving

        public override void ClearWorld() => QuestsLoaded = false;

        public override void SaveWorldData(TagCompound tag)
        {
            var worldQuests = QuestManager.CompletedWorldQuests?.Concat(QuestManager.UnloadedCompletedWorldQuests) ?? QuestManager.UnloadedCompletedWorldQuests;
            tag[TagKey] = worldQuests.ToArray();

            SaveQuestProgress(QuestManager.WorldQuests, tag);

            QuestLogDrawer.ActiveStyle.SaveWorldData(tag);
        }

        public partial class PlayerQuestLoader : ModPlayer
        {
            public override void SaveData(TagCompound tag)
            {
                var playerQuests = QuestManager.CompletedPlayerQuests?.Concat(QuestManager.UnloadedCompletedPlayerQuests) ?? QuestManager.UnloadedCompletedPlayerQuests;
                tag[TagKey] = playerQuests.ToArray();

                SaveQuestProgress(QuestManager.PlayerQuests, tag);

                QuestLogDrawer.ActiveStyle.SavePlayerData(tag);
            }
        }

        // Each quest writes into its own TagCompound, stored by quest key under a single
        // "progress" compound. Quests never share a compound, so their keys cannot collide.
        private static void SaveQuestProgress(FrozenDictionary<string, Quest> quests, TagCompound tag)
        {
            if (quests is null || tag is null)
                return;

            TagCompound allProgress = new();

            foreach (var quest in quests.Values)
            {
                TagCompound questProgress = new();
                quest.SaveProgress(questProgress);

                if (questProgress.Count > 0)
                    allProgress[quest.Key] = questProgress;
            }

            if (allProgress.Count > 0)
                tag[ProgressTagKey] = allProgress;
        }

        private static void LoadQuestProgress(FrozenDictionary<string, Quest> quests, TagCompound tag)
        {
            if (quests is null || tag is null)
                return;

            if (!tag.TryGet(ProgressTagKey, out TagCompound allProgress))
                allProgress = new TagCompound();

            // Quests with no stored progress are still called, with an empty compound,
            // so that they can reset any state left over from a previous world.
            foreach (var quest in quests.Values)
                quest.LoadProgress(allProgress.TryGet(quest.Key, out TagCompound questProgress) ? questProgress : new TagCompound());
        }

        #endregion

        // This is the first loading related method called on world entry.
        // We use it to reset which quests are considered "complete".
        public override void OnWorldLoad() => QuestManager.ResetActiveQuests();

        public override void OnWorldUnload() => QuestManager.UnloadActiveQuests();

        public static void LoadCompletedQuests(IEnumerable<string> completedQuests, out IEnumerable<string> unloadedQuests)
        {
            List<string> unloaded = [];

            foreach (string key in completedQuests)
            {
                if (QuestManager.TryGetQuest(key, out var quest))
                    QuestManager.MarkComplete(quest);

                else
                    unloaded.Add(key);
            }

            unloadedQuests = unloaded;
        }

        public static void SaveQuestLog(IList<QuestBook> questLog, string filePath)
        {
            string serialized = JsonConvert.SerializeObject(questLog, Formatting.None, JsonTypeResolverFix.Settings);
            File.WriteAllText(filePath, serialized);
        }

        public static List<QuestBook> LoadQuestLog(string filePath)
        {
            string serialized = File.ReadAllText(filePath);

            try
            {
                List<QuestBook> book = JsonConvert.DeserializeObject<List<QuestBook>>(serialized, JsonTypeResolverFix.Settings);
                return book;
            }
            catch
            {
                return null;
            }
        }
    }
}
