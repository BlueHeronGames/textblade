using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TextBlade.Core.Audio;
using TextBlade.Core.Battle;
using TextBlade.Core.Characters;
using TextBlade.Core.IO;
using TextBlade.Core.Locations;

namespace TextBlade.Core.Commands;

/// <summary>
/// Unlike most of the other commands, handles all the (fighting) logic and user input/response internally.
/// </summary>
public class TurnBasedBattleSystem : IBattleSystem
{
    private static JObject s_allMonstersData; // name => stats
 
    public const string VictoryMessage = "Victory! You gained {0} gold and {1} experience points!";
    public const string DefeatMessage = "Defeat!";
    private const string CommentsInJsonRegex = @"(//.*)";

    private readonly ISerialSoundPlayer _serialSoundPlayer;
    private readonly ISoundPlayer _soundPlayer;
    private readonly List<Monster> _monsters = new();
    private readonly IConsole _console;
    private SaveData _saveData; // not available at construction time, but available in Execute
    private readonly Dungeon _dungeon;

    private readonly List<string> _loot;

    static TurnBasedBattleSystem()
    {
        // By convention for now
        var jsonPath = Path.Join("Content", "Data", "Monsters.json");
        if (!File.Exists(jsonPath))
        {
            throw new FileNotFoundException($"{jsonPath} doesn't seem to exist.");
        }

        // Remove comments...
        var rawLines = File.ReadAllLines(jsonPath);
        var commentlessText = new StringBuilder();
        foreach (var line in rawLines)
        {
            commentlessText.Append(Regex.Replace(line, CommentsInJsonRegex, string.Empty, RegexOptions.None, TimeSpan.FromSeconds(1)));
        }

        var jsonContent = commentlessText.ToString();
        var jsonData = JsonConvert.DeserializeObject(jsonContent);
        if (jsonData == null)
        {
            throw new InvalidOperationException($"{jsonPath} failed to deserialize.");
        }

        s_allMonstersData = jsonData as JObject;
        if (s_allMonstersData == null)
        {
            throw new InvalidOperationException($"{jsonPath} doesn't seem to be valid JSON.");
        }
    }

    public TurnBasedBattleSystem(IConsole console, ISerialSoundPlayer serialSoundPlayer, ISoundPlayer soundPlayer, Dungeon dungeon, List<string> monsterNames, List<string> loot)
    {
        ArgumentNullException.ThrowIfNull(console);
        ArgumentNullException.ThrowIfNull(serialSoundPlayer);
        ArgumentNullException.ThrowIfNull(soundPlayer);
        ArgumentNullException.ThrowIfNull(dungeon);
        ArgumentNullException.ThrowIfNull(monsterNames);
        ArgumentNullException.ThrowIfNull(loot);

        _console = console;
        _serialSoundPlayer = serialSoundPlayer;
        _soundPlayer = soundPlayer;
        _dungeon = dungeon;
        _loot = loot;

        PopulateMonsters(monsterNames);
    }

    private void PopulateMonsters(List<string> monsterNames)
    {
        foreach (var name in monsterNames)
        {
            var data = s_allMonstersData[name];
            if (data == null)
            {
                throw new ArgumentException($"Can't find monster data for {name}");
            }

            var health = data.Value<int>("Health");
            var strength = data.Value<int>("Strength");
            var toughness = data.Value<int>("Toughness");
            var special = data.Value<int?>("Special") ?? 0;
            var specialDefense = data.Value<int?>("SpecialDefense") ?? 0;
            var skillPoints = data.Value<int?>("SkillPoints") ?? 0;
            var weakness = data.Value<string?>("Weakness") ?? string.Empty;
            var gold = data.Value<int>("Gold");
            var experiencePoints = data.Value<int?>("ExperiencePoints") ?? 0;
            
            var skillNames = data.Value<JArray>("SkillNames");
            var skillProbabilities = new Dictionary<string, double>();

            var skills = new List<Skill>();
            if (skillNames != null)
            {
                skills = new();
                foreach (var token in skillNames)
                {
                    var skillName = token.Value<string>("Name") ?? throw new InvalidOperationException("Skill has no 'Name' key");
                    var probability = token.Value<double>("Probability");
                    var skill = Skill.GetSkill(skillName);
                    skills.Add(skill);
                    skillProbabilities[skillName] = probability;
                }
            }

            var monster = new Monster(name, health, strength, toughness, special, specialDefense, skillPoints, experiencePoints, gold, weakness, skills, skillProbabilities);
            _monsters.Add(monster);
        }
    }

    public Spoils Execute(SaveData saveData, Location currentLocation)
    {
        ArgumentNullException.ThrowIfNull(saveData);
        _saveData = saveData;
        
        var isPartyWipedOut = () => _saveData.Party.TrueForAll(p => p.CurrentHealth <= 0);
        var areMonstersDefeated = () => _monsters.TrueForAll(m => m.CurrentHealth <= 0);
        var isBattleOver = () => isPartyWipedOut() || areMonstersDefeated();
        var characterTurnProcessor = new CharacterTurnProcessor(_console, _soundPlayer, _saveData, _monsters, currentLocation);

        while (!isBattleOver())
        {
            ShowMonsterStats();
            ShowCharacterStats();

            foreach (var character in _saveData.Party)
            {
                if (character.CurrentHealth <= 0)
                {
                    continue;
                }

                if (_monsters.All(m => m.CurrentHealth <= 0))
                {
                    continue;
                }

                if (character.StatusStacks.ContainsKey("Paralyze"))
                {
                    _console.WriteLine($"[{Colours.Paralyze}]{character.Name} is paralyzed[/] and can't move!");
                    continue;
                }

                var isProcessed = false;
                while (!isProcessed)
                {
                    isProcessed = characterTurnProcessor.ProcessTurnFor(character);
                }

                // User request: tell me statuses again after each turn
                ShowMonsterStats();

            }

            // If they're not done by here, stop and clear the queue.
            _serialSoundPlayer.Stop();
            var anyMonstersActed = false;

            foreach (var monster in _monsters)
            {
                if (monster.CurrentHealth <= 0)
                {
                    continue;
                }

                new BasicMonsterAi(_console, _serialSoundPlayer, _saveData.Party).ProcessTurnFor(monster);
                anyMonstersActed = true;
            }

            if (anyMonstersActed)
            {
                // Wait for any skill sounds to finish, then play monster sounds, so there's no overlap.
                Thread.Sleep(500);
                _serialSoundPlayer.Play();
            }
            
            ApplyRoundCompletion();
        }

        if (isPartyWipedOut())
        {
            _console.WriteLine(DefeatMessage);
            // No soup for you! Well, I guess we could give partial xp/gold/loot if we wanted to.
            return new Spoils() { IsVictory = false };
        }
        else if (areMonstersDefeated())
        {
            _dungeon.OnVictory();
            
            var spoils = new Spoils()
            {
                IsVictory = true,
                ExperiencePointsGained = _monsters.Sum(m => m.ExperiencePoints),
                GoldGained = _monsters.Sum(m => m.Gold),
                Loot = _loot,
            };

            _console.WriteLine(string.Format(VictoryMessage, spoils.GoldGained, spoils.ExperiencePointsGained));

            return spoils;
        }
        else
        {
            throw new InvalidOperationException("Undeterminable battle status!");
        }
    }

    private void ApplyRoundCompletion()
    {
        foreach (var monster in _monsters)
        {
            if (monster.CurrentHealth <= 0)
            {
                continue;
            }

            monster.OnRoundComplete(_console);
        }

        foreach (var e in _saveData.Party)
        {
            if (e.CurrentHealth <= 0)
            {
                continue;
            }

            e.OnRoundComplete(_console);
        }
    }

    private void ShowCharacterStats()
    {
        var partyStatus = string.Join(", ", _saveData.Party);
        _console.WriteLine($"Your party: [{Colours.Highlight}]{partyStatus}[/]");
    }

    private void ShowMonsterStats()
    {
        var monstersStatus = string.Join(", ", _monsters.Where(m => m.CurrentHealth > 0).Select(m => $"{m.Name}: {m.CurrentHealth}/{m.TotalHealth} health"));
        _console.WriteLine($"You face: [{Colours.Highlight}]{monstersStatus}[/]");
    }
}
