using TextBlade.Core.Game.Actions;

namespace TextBlade.Core.Characters;

public class Npc
{
    protected int _readNextIndex = 0;
    private bool _hasSpoken = false;
    private string[] _texts = [];
    
    public string Name { get; }
    public string[] Texts
    {
        get { return _texts; }
        set {
            _texts = value;
            _readNextIndex = 0;
        }
    }

    /// <summary>
    /// Action to execute when this NPC is talked to. Executed once per conversation.
    /// </summary>
    public IAction? OnTalk { get; set; }

    public Npc(string name, string[] texts)
    {
        this.Name = name;
        this.Texts = texts;
    }

    public virtual string Speak()
    {
        // Execute OnTalk action on first talk only
        if (!_hasSpoken && OnTalk != null)
        {
            OnTalk.Execute();
            _hasSpoken = true;
        }
        
        var toReturn = _texts[_readNextIndex];
        _readNextIndex = (_readNextIndex + 1) % Texts.Length;
        
        return toReturn;
    }
}
