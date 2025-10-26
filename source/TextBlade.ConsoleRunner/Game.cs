using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TestBlade.ConsoleRunner.Audio;
using TextBlade.ConsoleRunner.IO;
using TextBlade.Core.Audio;
using TextBlade.Core.Battle;
using TextBlade.Core.Commands;
using TextBlade.Core.Commands.Display;
using TextBlade.Core.Game;
using TextBlade.Core.IO;
using TextBlade.Core.Locations;

namespace TextBlade.ConsoleRunner;


/// <summary>
/// Your basic game class. Keeps track of the current location, party members, etc. in save data.
/// Handles some basic parsing: showing output, reading input, and processing it (delegation).
/// </summary>
public class Game : IGame
{
    public static IGame Current { get; private set; } = null!;

    private const int AutoSaveIntervalMinutes = 1;
    private const float BackgroundAudioVolume = 0.30f;

    protected SaveData _saveData = new();
    protected Location? _currentLocation;

    private readonly bool _isRunning = true;
    private readonly LocationDisplayer _locationDisplayer;
    private DateTime _lastSaveOn = DateTime.UtcNow;

    private readonly IConsole _console;

    private readonly ISerialSoundPlayer _serialSoundPlayer;
    private readonly ISoundPlayer _oneShotBattleSoundsPlayer = new AudioPlayer();
    private readonly ISoundPlayer _battleThemeSoundPlayer = new AudioPlayer(true);
    private readonly List<AudioPlayer> _backgroundAudiosPlayers = []; 

    public Game(IConsole console, ISerialSoundPlayer serialSoundPlayer, ISoundPlayer soundPlayer)
    {
        ArgumentNullException.ThrowIfNull(console);
        ArgumentNullException.ThrowIfNull(serialSoundPlayer);
        ArgumentNullException.ThrowIfNull(soundPlayer);

        _console = console;
        _serialSoundPlayer = serialSoundPlayer;
        _oneShotBattleSoundsPlayer = soundPlayer;
        _locationDisplayer = new(_console);

        Current = this;
    }

    /// <summary>
    /// Called whenever a location changes. Sleeping in an inn, descending a dungeon, do NOT trigger this.
    /// </summary>
    public void SetLocation(Location location)
    {
        if (location.LocationId == _saveData.LocationSpecificDataLocationId)
        {
            location.SetStateBasedOnCustomSaveData(_saveData.LocationSpecificData);
        }
        
        // If a current location requires saving when you change away from it, save it here.
        if (_currentLocation?.GetCustomSaveData() != null)
        {
            SaveGame();
        }

        _currentLocation = location;
        PlayBackgroundAudio();
        AutoSaveIfItsBeenAWhile();
    }

    public void Run()
    {
        try
        {
            LoadGameOrStartNewGame();
            if (_currentLocation is null)
            {
                throw new InvalidOperationException("No location has been configured for game start");
            }

            // Don't execute code if we stay in the same location, e.g. press enter or "help" - only execute code
            // if the location changed. Fixes a bug where spamming enter keeps adding the same location over and over ...
            Location? previousLocation = null;

            while (_isRunning)
            {
                if (previousLocation != _currentLocation)
                {
                    CodeBehindRunner.ExecuteLocationCode(_currentLocation);
                    _locationDisplayer.ShowLocation(_currentLocation);
                }

                var command = new InputProcessor(this, _console, _serialSoundPlayer, _oneShotBattleSoundsPlayer).PromptForAction(_currentLocation);
                previousLocation = _currentLocation;

                // Special case for battles
                if (command is FightCommand)
                {
                    FadeOutAudios();
                    _battleThemeSoundPlayer.Play(Path.Combine("Content", "Audio", "bgm", "battle.ogg"));
                }

                var isExecuted = command.Execute(_console, _currentLocation, _saveData);
                if (!isExecuted)
                {
                    continue;
                }
                
                // Command processing is done, e.g. battle is over.
                switch (command)
                {
                    case ManuallySaveCommand:
                        SaveGame();
                        break;
                    case LookCommand:
                        _locationDisplayer.ShowLocation(_currentLocation);
                        break;
                    case FightCommand:
                        _battleThemeSoundPlayer.Stop(); // TODO: fade out
                        FadeInAudios();
                        break;
                    default:
                        AutoSaveIfItsBeenAWhile();
                        break;
                }
            }
        }
        catch (Exception ex)
        {
            string[] crashFiles = [@"SaveData\default.save", "crash.txt"];
            _console.WriteLine("[red]Oh no! The game crashed![/]");
            _console.WriteLine("Please reach out to the developers and let them know about this, so that they can look into it.");
            _console.WriteLine($"Send them these files from your game directory, along with a description of what you were doing in-game: [green]{string.Join(", ", crashFiles)}[/]");
            File.WriteAllText("crash.txt", ex.ToString());
            
            #if DEBUG
                throw;
            #endif
        }
    }

    protected void SaveGame()
    {
        if (_currentLocation == null)
            throw new InvalidOperationException("Game has not been started yet"); 
        // Save location-specific data, favouring the current location's specific data. e.g. if you have save data from dungeon A, but are now in dungeon B,
        // you save dungeon B's data.  But if the current location data is null, albeit previously saved, preserve that data. (e.g. if you're now in town,
        // which saves nothing, but were previously in a dungeon, preserve that dungeon's data.)
        var locationSpecificData = _currentLocation.GetCustomSaveData();
        var locationSpecificDataLocationId = _currentLocation.LocationId;
        
        if (locationSpecificData == null)
        {
            locationSpecificData = _saveData.LocationSpecificData;
            locationSpecificDataLocationId = _saveData.LocationSpecificDataLocationId;
        }

        // Update SaveData, so we don't end up with a mismatch between in-game and what's on disk
        _saveData.LocationSpecificData = locationSpecificData;
        _saveData.LocationSpecificDataLocationId = locationSpecificDataLocationId;

        SaveGameManager.SaveGame(SaveGameManager.CurrentGameSlot, _currentLocation.LocationId, _saveData.Party, _saveData.Inventory, _saveData.Gold, locationSpecificDataLocationId, locationSpecificData);
        _lastSaveOn = DateTime.UtcNow;

        _console.WriteLine("[green]Game saved.[/]");
    }

    private void FadeOutAudios()
    {
        // One day, perhaps I will figure out how to fade out.
        foreach (var audio in _backgroundAudiosPlayers)
        {
            audio.Stop();
        }
    }

    private void FadeInAudios()
    {
        // One day...
        foreach (var audio in _backgroundAudiosPlayers)
        {
            audio.Dispose();
        }

        foreach (var sound in _currentLocation.BackgroundAudios)
        {
            PlayBackgroundAudio(sound);
        }
    }

    private void LoadGameOrStartNewGame()
    {
        var gameJson = ShowGameIntro();

        if (SaveGameManager.HasSave(SaveGameManager.CurrentGameSlot))
        {
            LoadGame();
        }
        else
        {
            StartNewGame(gameJson);
        }
    }

    private void StartNewGame(JObject gameJson)
    {
        var runner = new NewGameRunner(gameJson);
        _saveData = new();
        _saveData.Party = runner.CreateParty();
        RefreshSkillsData();
        _saveData.Inventory = new();

        var startLocationId = runner.GetStartingLocationId();
        new ChangeLocationCommand(this, startLocationId).Execute(_console, _currentLocation, _saveData);
        _console.WriteLine("New game started. For help, type \"help\"");
    }


    private void LoadGame()
    {
        _saveData = SaveGameManager.LoadGame(SaveGameManager.CurrentGameSlot);

        GameSwitches.Switches = _saveData.Switches;
        new ChangeLocationCommand(this, _saveData.CurrentLocationId).Execute(_console, _currentLocation, _saveData);

        if (_saveData.LocationSpecificDataLocationId == _currentLocation?.LocationId)
        {
            _currentLocation.SetStateBasedOnCustomSaveData(_saveData.LocationSpecificData);
        }

        RefreshSkillsData();

        _console.WriteLine("Save game loaded. For help, type \"help\"");
    }

    private void RefreshSkillsData()
    {
        foreach (var character in _saveData.Party)
        {
            character.Skills = new();
            foreach (var tuple in character.SkillsLearnedAtLevel)
            {
                var skillName = tuple.Item1;
                var levelLearned = tuple.Item2;
                if (character.Level >= levelLearned)
                {
                    var skill = Skill.GetSkill(skillName);
                    character.Skills.Add(skill);
                }
            }
        }
    }

    private JObject ShowGameIntro()
    {
        var gameJsonPath = Path.Join("Content", "game.json");

        if (!File.Exists(gameJsonPath))
        {
            throw new InvalidOperationException("Content/game.json file is missing!");
        }

        var gameJsonContents = File.ReadAllText(gameJsonPath);
        if (JsonConvert.DeserializeObject(gameJsonContents) is not JObject gameJson)
        {
            throw new Exception("game.json is not a valid JSON object!");
        }

        var version = File.ReadAllText("version.txt").Trim();
        var gameName = gameJson["GameName"];
        _console.WriteLine($"[white]Welcome to[/] [red]{gameName}[/] version [white]{version}[/]!");

        // Keeps things DRY
        return gameJson;
    }

    private void PlayBackgroundAudio()
    {
        foreach (var audioPlayer in _backgroundAudiosPlayers)
        {
            // Fade out and stop
            audioPlayer.Stop();
        }
        
        _backgroundAudiosPlayers.Clear();

        if (string.IsNullOrWhiteSpace(_currentLocation?.BackgroundAudio) && _currentLocation?.BackgroundAudios.Count() == 0)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(_currentLocation?.BackgroundAudio))
        {
            PlayBackgroundAudio(_currentLocation.BackgroundAudio);
            return;
        }

        // Array of audios.
        PlayAudiosFor(_currentLocation?.BackgroundAudios);

        // Make them repeat.
        foreach (var audio in _backgroundAudiosPlayers)
        {
            audio.LoopPlayback = true;
        }
    }

    private void PlayAudiosFor(string[] audios)
    {
        // Fade out
        // OnFadeOutComplete () =>
        {
            _backgroundAudiosPlayers.Clear();
            foreach (var audio in audios)
            {
                PlayBackgroundAudio(audio);
            }
        }        
    }

    private void PlayBackgroundAudio(string audioFile)
    {   
        var audioPlayer = new AudioPlayer(true);
        audioPlayer.Play(Path.Join("Content", "Audio", $"{audioFile}.ogg"));
        audioPlayer.Volume = BackgroundAudioVolume;
        _backgroundAudiosPlayers.Add(audioPlayer);
    }

    private void AutoSaveIfItsBeenAWhile()
    {
        var elapsed = DateTime.UtcNow - _lastSaveOn;
        if (elapsed.TotalMinutes < AutoSaveIntervalMinutes)
        {
            return;
        }
        
        _lastSaveOn = DateTime.UtcNow;
        SaveGame();
    }
}
