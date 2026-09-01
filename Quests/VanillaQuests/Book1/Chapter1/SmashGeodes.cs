using QuestBooks.Systems;

namespace QuestBooks.Quests.VanillaQuests.Book1.Chapter1;

public class SmashGeodes : CounterQuest
{
    /// <summary>
    ///     The number of geodes the player must smash in order to complete the quest.
    /// </summary>
    public override int Goal => 10;

    public override QuestType QuestType => QuestType.Player;

    protected override string ProgressTag => "SmashedGeodesCount";

    public class SmashGeodesCheck : GlobalItem
    {
        public override bool AppliesToEntity(Item entity, bool lateInstantiation) => entity.type == ItemID.Geode;

        public override void OnConsumeItem(Item item, Player player)
        {
            if (QuestManager.TryGetQuest<SmashGeodes>(out var quest) && !quest.Completed) quest.Count++;
        }
    }
}