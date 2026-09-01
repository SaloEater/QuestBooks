using Terraria.ModLoader.IO;

namespace QuestBooks.Quests.VanillaQuests;

/// <summary>
/// Intended for internal QuestBooks use.<br/>
/// Shorthands the localization category based on namespace instead of <c>QuestBooks</c>.
/// </summary>

public abstract class VanillaQuest : Quest
{
    private string _localizationCategory = null;

    public override string TextureCategory => $"{Mod.Name}/Assets/Textures/Quests/InfoPages";

    public override string LocalizationCategory
    {
        get
        {
            _localizationCategory ??= GetType().Namespace[GetType().Namespace.LastIndexOf("Book")..];
            return _localizationCategory;
        }
    }
}

/// <summary>
/// Intended for internal QuestBooks use.<br/>
/// Acts as a quest that is marked as completed once it has been opened.
/// </summary>
public abstract class InfoQuest : VanillaQuest
{
    public override QuestType QuestType => QuestType.Player;

    public bool Read { get; set; } = false;

    public override void MakeSimpleInfoPage(out string title, out string contents, out Texture2D texture)
    {
        base.MakeSimpleInfoPage(out title, out contents, out texture);
        Read = true;
    }

    public override bool CheckCompletion() => Read;
}

/// <summary>
/// Intended for internal QuestBooks use.<br/>
/// Acts as a quest that is completed once a running count reaches a goal.<br/>
/// Owns the count and its persistence, and appends a "count / goal" line to the bottom of the
/// quest's info page, so implementations only have to supply a <see cref="Goal"/>.
/// </summary>
public abstract class CounterQuest : VanillaQuest
{
    // The count ramps toward the goal color.
    // A direct red-to-green lerp turns to brown at the midpoint, so it passes through the yellow.
    private static readonly Color EmptyColor = Color.Red;
    private static readonly Color PartialColor = Color.Yellow;
    private static readonly Color GoalColor = Color.Green;

    /// <summary>
    /// How far along this quest currently is, measured in the same unit as <see cref="Goal"/>
    /// </summary>
    public virtual int Count { get; protected set; }

    /// <summary>
    /// The value <see cref="Count"/> has to reach for this quest to be complete.
    /// </summary>
    public abstract int Goal { get; }

    /// <summary>
    /// The key <see cref="Count"/> is saved under. This lives inside this quest's own progress
    /// compound, so it only has to be distinct from any other key this same quest saves.
    /// </summary>
    protected virtual string ProgressTag => "Count";

    /// <summary>
    /// <see cref="Count"/> as a fraction of <see cref="Goal"/>, clamped between 0f and 1f.
    /// </summary>
    public float Progress => Goal <= 0 ? 1f : MathHelper.Clamp(Count / (float)Goal, 0f, 1f);

    /// <summary>
    /// The color the count is drawn in, ramping from red through yellow to green as
    /// <see cref="Progress"/> approaches 1f.
    /// </summary>
    public Color CountColor => Progress < 0.5f
        ? Color.Lerp(EmptyColor, PartialColor, Progress * 2f)
        : Color.Lerp(PartialColor, GoalColor, (Progress - 0.5f) * 2f);

    public override bool CheckCompletion() => Count >= Goal;

    public override void SaveProgress(TagCompound tag) => tag[ProgressTag] = Count;

    public override void LoadProgress(TagCompound tag) => Count = tag.GetInt(ProgressTag);

    public override void MakeSimpleInfoPage(out string title, out string contents, out Texture2D texture)
    {
        base.MakeSimpleInfoPage(out title, out contents, out texture);
        contents += $"\n\n[c/{CountColor.Hex3()}:{Count}] / [c/{GoalColor.Hex3()}:{Goal}]";
    }
}

internal class Placeholder : Quest
{
    public override bool CheckCompletion() => true;

    public override string LocalizationCategory => "Tooltips";
}