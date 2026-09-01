using QuestBooks.Quests.QuestSystems;
using QuestBooks.Systems;

namespace QuestBooks.Quests.VanillaQuests.Book4.Chapter1;

public class BuyTeleporter : CounterQuest
{
    public override QuestType QuestType => QuestType.Player;

    public override int Goal => 2;

    public class BuyTeleporterCheck() : BuyItemHook(item => Match(item, ItemID.Teleporter), static (_, _) => QuestManager.GetQuest<BuyTeleporter>().Count++);
}