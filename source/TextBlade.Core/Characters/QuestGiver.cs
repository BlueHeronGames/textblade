using TextBlade.Core.Game;
using TextBlade.Core.Game.Actions;

namespace TextBlade.Core.Characters;

public class QuestGiver : Npc
{
    /// <summary>
    /// The texts this NPC says after the quest is done.
    /// </summary>
    public string[] PostQuestTexts { get; }

    /// <summary>
    /// Which switch indicates that this quest is complete? It must be a boolean switch, and set to true, to indicate completion.
    /// </summary>
    public string QuestCompleteSwitchName { get; }

    // Texts are pre-quest texts
    public QuestGiver(string name, string[] texts, string[] postQuestTexts, string questCompleteSwitchName) : base(name, texts)
    {
        PostQuestTexts = postQuestTexts;
        QuestCompleteSwitchName = questCompleteSwitchName;
        
        // Auto-set OnTalk to set TalkedTo_ switch if not already provided
        if (OnTalk == null)
        {
            OnTalk = new SetSwitchAction(GameSwitches.GetTalkedToSwitchForQuestGiver(name), true);
        }
    }

    public override string Speak()
    {
        if (GameSwitches.Switches.Has(QuestCompleteSwitchName) && GameSwitches.Switches.Get(QuestCompleteSwitchName) == true)
        {
            var message = PostQuestTexts[_readNextIndex];
            _readNextIndex = (_readNextIndex + 1) % PostQuestTexts.Length;
            return message;
        }

        // Call base.Speak() which will handle OnTalk execution
        return base.Speak();
    }
}
