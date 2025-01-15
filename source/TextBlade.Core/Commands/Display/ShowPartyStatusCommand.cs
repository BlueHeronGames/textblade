using TextBlade.Core.IO;
using TextBlade.Core.Locations;

namespace TextBlade.Core.Commands.Display;

public class ShowPartyStatusCommand : ICommand
{
    public bool Execute(IConsole console, Location currentLocation, SaveData saveData)
    {
        console.WriteLine("Party status:");

        foreach (var member in saveData.Party)
        {
            var equipment = string.Join(", ", member.Equipment.Values.Select(e => $"{e.Name}: {e}"));

            console.WriteLine($"    {member}");
            console.WriteLine($"        Stats: {member.GetStats()}");
            console.WriteLine($"        Equipment: {(string.IsNullOrWhiteSpace(equipment) ? "nothing" : equipment)}");
        }

        return true;
    }
}
