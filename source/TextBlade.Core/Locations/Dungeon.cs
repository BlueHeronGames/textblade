using System.Text;
using TextBlade.Core.Commands;
using TextBlade.Core.Commands.Display;
using TextBlade.Core.Game;
using TextBlade.Core.IO;

namespace TextBlade.Core.Locations;

public class Dungeon : Location
{
    // Simple but complex. Can be "Iron Shield," can be listed twice to give me two.
    // Public because of serialization.
    // Floor (e.g. B2) => list of lootz
    public Dictionary<string, List<string>> FloorLoot { get; set; } = [];

    // It's a list, one entry per floor.
    // Each entry is a list of monsters.
    protected readonly List<List<string>> _floorMonsters = [];
    private int _currentFloorNumber  = 0;
    
    private string _currentFloorLootKey => $"B{_currentFloorNumber + 1}";

    public Dungeon(string name, string description, int numFloors, List<string> monsters, string boss, string? locationClass = null)
    : base(name, description, locationClass)
    {
        GenerateMonsters(numFloors, monsters, boss);
    }

    private void GenerateMonsters(int numFloors, List<string> monsters, string boss)
    {
        if (numFloors <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(numFloors));
        }

        if (monsters.Count == 0)
        {
            throw new ArgumentException(nameof(monsters));
        }

        // Iterate up to the second-last floor and generate monsters
        for (int i = 0; i < numFloors - 1; i++)
        {
            var currentFloorOccupants = new List<string>();
            
            // Assumes monsters are in order of difficulty, roughly.
            // Which monsters can we spawn on this floor? Up to (floor_number + 1) inclusive.
            // Always guarantee at least two monster types
            var firstMonsterIndex = Math.Min(i - 1, monsters.Count - 2);
            firstMonsterIndex = Math.Max(firstMonsterIndex, 0);
            var lastMonsterIndex = Math.Min(i + 2, monsters.Count);

            List<string> validMonsters = [];
            // Keep this at base zero, so we know the correct value of each monster.
            // If we change this to firstMonsterIndex, we'll end up thinking the first
            // entry in validMonsters is value 1, but it's real value might be 5.
            for (int j = 0; j < lastMonsterIndex; j++)
            {
                validMonsters.Add(monsters[j]);
            }
            
            // Intentionally imprecise, nor does it guarantee future floors are always harder than past floors.
            var pointsLeft = (i + 1) * 5;
            while (pointsLeft > 0)
            {
                var nextMonsterIndex = Random.Shared.Next(firstMonsterIndex, lastMonsterIndex);
                currentFloorOccupants.Add(validMonsters[nextMonsterIndex]);
                // Magic number of *2 here cuts the monster count roughly in half
                var pointCost = (nextMonsterIndex + 1) * 2;
                pointsLeft -= pointCost;
            }

            _floorMonsters.Add(currentFloorOccupants);
        }

        // Generate a boss on the last floor, with 2-3 of the strongest minion
        int numMinions = Random.Shared.Next(2, 4);
        var minion = monsters[monsters.Count - 1];
        var finalFloor = new List<string>();

        for (int i = 0; i < numMinions; i++)
        {
            finalFloor.Add(minion);
        }
        finalFloor.Add(boss);
        _floorMonsters.Add(finalFloor);
    }

    public override string GetExtraDescription()
    {
       var currentFloorData = _floorMonsters[_currentFloorNumber];
        var monstersMessage = new StringBuilder();
        if (currentFloorData.Count == 0)
        {
            monstersMessage.AppendLine("There are no monsters left.");
        }
        else
        {
            monstersMessage.AppendLine($"You see: {string.Join(", ", currentFloorData)}.");
        }

        var treasureMessage = "";
        if (FloorLoot.ContainsKey(_currentFloorLootKey) && currentFloorData.Any())
        {
            treasureMessage = "You see something shiny glitter behind the monsters.  ";
        }

        return $"You are on floor {_currentFloorNumber + 1}. {treasureMessage}{monstersMessage}";
    }

    public override string GetExtraMenuOptions()
    {
        var message = new StringBuilder();
        var currentFloorData = _floorMonsters[_currentFloorNumber];
        if (currentFloorData.Count == 0)
        {
            if (_currentFloorNumber < _floorMonsters.Count - 1)
            {
                message.AppendLine("You see stairs leading down. Type d/down/descend to go to the next floor.");
            }
        }
        else
        {
            message.Append(" Type f/fight to fight. ");
        }

        return message.ToString();
    }

    override public ICommand GetCommandFor(string input)
    {
        var currentFloorData = _floorMonsters[_currentFloorNumber];
        if ((input == "f" || input == "fight") && currentFloorData.Any())
        {
            return new FightCommand();
        }
        // Type d, down, descend, or >; current floor has no monsters; we're not at the bottom. 
        else if ((input == "d" || input == "down" || input == "descend" || input == ">") &&
        !currentFloorData.Any() && _currentFloorNumber < _floorMonsters.Count - 1)
        {
            // Valid descent
            _currentFloorNumber++;
            return new LookCommand();
        }

        return new DoNothingCommand();
    }

    public override string GetExtraOutputFor(string rawResponse)
    {
        var currentFloorData = _floorMonsters[_currentFloorNumber];
        if ((rawResponse == "f" || rawResponse == "fight") && !currentFloorData.Any())
        {
                return "There are no monsters to fight here!";
        }
        else if (rawResponse == "d" || rawResponse == "down" || rawResponse == "descend" || rawResponse == ">")
        {
            if (currentFloorData.Any())
            {
                return $"You can't descend while monsters are around! You can [#{Colours.Command}]fight[/] them, though.";
            }
            else if (_currentFloorNumber == _floorMonsters.Count - 1)
            {
                return "You're already at the bottom of the dungeon!";
            }
        }

        return string.Empty;
    }

    public bool IsCurrentFloorClear()
    {
        return _floorMonsters[_currentFloorNumber].Count == 0;
    }

    public override Dictionary<string, object>? GetCustomSaveData()
    {
        return new Dictionary<string, object>
        {
            { "CurrentFloor", this._currentFloorNumber },
            { "IsClear",  this.IsCurrentFloorClear() },
        };
    }

    public override void SetStateBasedOnCustomSaveData(Dictionary<string, object>? extraData)
    {
        if (extraData == null)
        {
            return;
        }
        
        var floorNumber = Convert.ToInt32(extraData["CurrentFloor"]);
        var isClear = (bool)extraData["IsClear"];
        
        if (floorNumber < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(floorNumber));
        }

        this._currentFloorNumber = floorNumber;
        if (!isClear)
        {
            return;
        }

        _floorMonsters[floorNumber].Clear();
        if (!FloorLoot.ContainsKey(_currentFloorLootKey))
        {
            return;
        }

        // Obvious, innit? If the current floor is clear, you auto-grabbed the loot already.
        ClearFloorAndSetSwitch();
    }

    public void OnVictory()
    {
        ClearFloorAndSetSwitch();
    }

    private void ClearFloorAndSetSwitch()
    {
        _floorMonsters[_currentFloorNumber].Clear();
        if (_currentFloorNumber >= _floorMonsters.Count - 1)
        {
            // You just wiped out the last floor of monsters. Set an automatic switch.
            GameSwitches.Switches.Set(GameSwitches.GetCompletionSwitchForDungeon(this.Name), true);
        }
    }

    public List<string> GetCurrentFloorData()
    {
        return _floorMonsters[_currentFloorNumber] ?? new();
    }

    public List<string> GetCurrentFloorLoot()
    {
        return FloorLoot.ContainsKey(_currentFloorLootKey) ? FloorLoot[_currentFloorLootKey] : new();
    }
}
