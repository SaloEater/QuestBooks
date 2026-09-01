using Terraria.GameContent.Achievements;

namespace QuestBooks.Quests.VanillaQuests.Book3.Chapter3;

public class Break10kTiles : CounterQuest
{
    /// <summary>
    ///     The amount of tiles the player must break to complete the quest.
    /// </summary>
    public override int Goal => 10000;

    public override QuestType QuestType => QuestType.Player;

    public override void Load() => AchievementsHelper.OnTileDestroyed += Check;

    public override void Unload() => AchievementsHelper.OnTileDestroyed -= Check;

    private static void Check(Player player, ushort tileId)
    {
        if (player.whoAmI != Main.myPlayer)
            return;

        if (QuestBooksMod.TryGetQuest<Break10kTiles>(out var quest) && !quest.Completed)
            quest.Count++;
    }
}