namespace TextBlade.Core.Game.Actions;

/// <summary>
/// Represents an action that can be executed in response to game events.
/// </summary>
public interface IAction
{
    /// <summary>
    /// Executes the action.
    /// </summary>
    void Execute();
}
