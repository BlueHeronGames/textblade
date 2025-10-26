namespace TextBlade.Core.Game.Actions;

/// <summary>
/// Action that sets a game switch to a specified value.
/// </summary>
public class SetSwitchAction : IAction
{
    public string SwitchName { get; set; }
    public bool Value { get; set; }

    public SetSwitchAction(string switchName, bool value)
    {
        SwitchName = switchName;
        Value = value;
    }

    public void Execute()
    {
        GameSwitches.Switches?.Set(SwitchName, Value);
    }
}
