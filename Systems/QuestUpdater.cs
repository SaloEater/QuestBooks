using System.Linq;

namespace QuestBooks.Systems
{
    internal class QuestUpdater : ModSystem
    {
        // Update the active quest log style. The log is client-only, so this needs the
        // local player's quests rather than the world's.
        public override void UpdateUI(GameTime gameTime)
        {
            if (!QuestLoader.PlayerQuestsLoaded)
                return;

            foreach (var questBook in QuestManager.QuestBooks)
                questBook.Update();

            QuestLogDrawer.ActiveStyle.UpdateLog();
        }

        // Loop through and check quest completion post-update.
        public override void PostUpdateEverything()
        {
            if (!QuestLoader.WorldQuestsLoaded)
                return;

            var allQuests = QuestManager.ActiveQuests.Values.ToArray();

            foreach (var quest in allQuests)
                quest.Update();

            // World quests are updated in singleplayer and on the server.
            // Not on multiplayer clients.
            if (Main.netMode != NetmodeID.MultiplayerClient)
                UpdateIncompleteQuests(QuestManager.IncompleteWorldQuests);

            // Player quests are updated in singleplayer and on multiplayer clients.
            // Not on the server.
            if (!Main.dedServ && QuestLoader.PlayerQuestsLoaded)
                UpdateIncompleteQuests(QuestManager.IncompletePlayerQuests);
        }

        public static void UpdateIncompleteQuests(string[] incompleteQuests)
        {
            foreach (var questName in incompleteQuests)
            {
                var quest = QuestManager.GetQuest(questName);

                if (quest.CheckCompletion())
                    QuestManager.CompleteQuest(quest);
            }
        }
    }
}
