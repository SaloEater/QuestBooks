using Terraria.ModLoader.IO;

namespace QuestBooks.Quests.VanillaQuests.Book3.Chapter3;

public class RunMarathon : CounterQuest
{
    /// <summary>
    ///     The number of tiles the player must run in order to complete the quest.
    /// </summary>
    public const int TargetTiles = 69200;

    public const string TagKey = "PlayerPixelsMoved";

    /// <summary>
    ///     Gets the total number of pixels the player has walked through.
    /// </summary>
    public float PixelsTravelled { get; private set; }

    /// <summary>
    ///     Gets the total number of tiles the player has walked through.
    /// </summary>
    /// <value>
    ///     <see cref="PixelsTravelled"/> divided by 16.
    /// </value>
    public int TilesTravelled => (int)(PixelsTravelled / 16f);

    /// <summary>
    ///     Gets the total number of miles the player has walked through.
    /// </summary>
    /// <value>
    ///     <see cref="TilesTravelled"/> divided by 2640.
    /// </value>
    public float MilesTravelled => TilesTravelled / 2640f;

    public override QuestType QuestType => QuestType.Player;

    // Cached player position last frame
    private Vector2? position;

    public override void Update()
    {
        if (Main.dedServ)
            return;

        position ??= Main.LocalPlayer.position;
        var distance = Vector2.Distance(position.Value, Main.LocalPlayer.position);
        PixelsTravelled += distance;
        position = Main.LocalPlayer.position;
    }

    // Progress is stored as a float in pixels rather than as the base class' int, so both the
    // count and its persistence are overridden here.
    public override int Count
    {
        get => TilesTravelled;
        protected set => PixelsTravelled = value * 16f;
    }

    public override int Goal => TargetTiles;

    public override void SaveProgress(TagCompound tag) => tag[TagKey] = PixelsTravelled;
    public override void LoadProgress(TagCompound tag) => PixelsTravelled = tag.GetFloat(TagKey);
}