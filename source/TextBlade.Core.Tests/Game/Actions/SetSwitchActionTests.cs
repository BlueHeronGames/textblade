using NUnit.Framework;
using TextBlade.Core.Game;
using TextBlade.Core.Game.Actions;

namespace TextBlade.Core.Tests.Game.Actions;

[TestFixture]
public class SetSwitchActionTests
{
    [Test]
    public void Execute_SetsSwitchToTrue()
    {
        // Arrange
        GameSwitches.Switches = new GameSwitches();
        var action = new SetSwitchAction("TestSwitch", true);

        // Act
        action.Execute();

        // Assert
        Assert.That(GameSwitches.Switches.Has("TestSwitch"), Is.True);
        Assert.That(GameSwitches.Switches.Get("TestSwitch"), Is.True);
    }

    [Test]
    public void Execute_SetsSwitchToFalse()
    {
        // Arrange
        GameSwitches.Switches = new GameSwitches();
        var action = new SetSwitchAction("TestSwitch", false);

        // Act
        action.Execute();

        // Assert
        Assert.That(GameSwitches.Switches.Has("TestSwitch"), Is.True);
        Assert.That(GameSwitches.Switches.Get("TestSwitch"), Is.False);
    }
}
