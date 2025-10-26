using NUnit.Framework;
using TextBlade.Core.Characters;
using TextBlade.Core.Game;
using TextBlade.Core.Game.Actions;

namespace TextBlade.Core.Tests.Characters;

[TestFixture]
public class NpcTests
{
    [Test]
    public void Speak_ReturnsTheOnlyMessage()
    {
        // Arrange
        var expected = "Spawn more overlords!";
        var npc = new Npc("Overlord 1", [expected]);

        // Act/Assert
        for (int i = 0; i < 10; i++)
        {
            var actual = npc.Speak();
            Assert.That(actual, Is.EqualTo(expected));
        }
    }

    [Test]
    public void Speak_CyclesThroughMessages()
    {
        var expected = new string[] {
            "Message 1",
            "Message 2",
            "Message 3"
        };

        var npc = new Npc("Speaker for the Speakers", expected);

        // Act/Assert
        Assert.That(npc.Speak(), Is.EqualTo(expected[0]));
        Assert.That(npc.Speak(), Is.EqualTo(expected[1]));
        Assert.That(npc.Speak(), Is.EqualTo(expected[2]));
        
        Assert.That(npc.Speak(), Is.EqualTo(expected[0]));
        Assert.That(npc.Speak(), Is.EqualTo(expected[1]));
        Assert.That(npc.Speak(), Is.EqualTo(expected[2]));
    }

    [Test]
    public void Speak_ExecutesOnTalkAction_OnFirstCall()
    {
        // Arrange
        GameSwitches.Switches = new GameSwitches();
        var npc = new Npc("Test NPC", ["Hello"])
        {
            OnTalk = new SetSwitchAction("TestSwitch", true)
        };

        // Act
        npc.Speak();

        // Assert
        Assert.That(GameSwitches.Switches.Has("TestSwitch"), Is.True);
        Assert.That(GameSwitches.Switches.Get("TestSwitch"), Is.True);
    }

    [Test]
    public void Speak_ExecutesOnTalkActionOnlyOnce()
    {
        // Arrange
        GameSwitches.Switches = new GameSwitches();
        var counter = 0;
        var npc = new Npc("Test NPC", ["Hello", "Hi"])
        {
            OnTalk = new TestAction(() => counter++)
        };

        // Act
        npc.Speak(); // Should execute OnTalk
        npc.Speak(); // Should NOT execute OnTalk

        // Assert
        Assert.That(counter, Is.EqualTo(1));
    }
}

// Helper class for testing
class TestAction : IAction
{
    private readonly Action _action;
    public TestAction(Action action) => _action = action;
    public void Execute() => _action();
}
