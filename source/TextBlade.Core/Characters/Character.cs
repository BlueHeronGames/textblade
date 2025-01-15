using TextBlade.Core.Battle;
using TextBlade.Core.Characters.PartyManagement;
using TextBlade.Core.Inv;
using TextBlade.Core.IO;

namespace TextBlade.Core.Characters;

public class Character : Entity
{
    public const int EquipmentStrengthMultiplier = 2; // 2 => equipment strength adds 2x to damage 
    public Dictionary<ItemType, Equipment> Equipment { get; set; } = new(); // Needs to be public for serialization
    public int ExperiencePoints { get; internal set; } = 0;
    public int Level { get; set; } = 1;
    public List<Tuple<string, int>> SkillsLearnedAtLevel { get; set; } = new();

    internal bool IsDefending { get; private set; }

    public Character(string name, int health, int strength, int toughness, int special, int specialDefense, int skillPoints, int experiencePoints = 0)
    : base(name, health, strength, toughness, special, specialDefense, skillPoints)
    {
        this.Special = special;
        this.SpecialDefense = specialDefense;
        this.ExperiencePoints = experiencePoints;
    }

    public void FullyHeal()
    {
        this.CurrentHealth = this.TotalHealth;
        this.CurrentSkillPoints = this.TotalSkillPoints;
    }

    public void Revive()
    {
        if (CurrentHealth <= 0)
        {
            this.CurrentHealth = 1;
        }
    }

    /// <summary>
    /// Total strength: character strength + all equipment strength (including, potentially, all armour).
    /// </summary>
    public int TotalStrength { get { return this.Strength + EquipmentStrengthMultiplier * this.Equipment.Sum(e => e.Value.GetStatsModifier(CharacterStats.Strength)); } }

    /// <summary>
    /// Total toughness: character toughness + all equipment toughness (including, potentially, your weapon).
    /// </summary>
    public int TotalToughness { get { return this.Toughness + this.Equipment.Sum(e => e.Value.GetStatsModifier(CharacterStats.Toughness)); } }

    internal void GainExperiencePoints(IConsole console, int experiencePoints)
    {
        if (this.CurrentHealth <= 0)
        {
            return;
        }

        var manager = new LevelManager(console);
        this.ExperiencePoints += experiencePoints;

        if (manager.CanLevelUp(this))
        {
            manager.LevelUp(this);
        }

        // It's a small list, it's fast to iterate, it's not a big deal that it's redundant. I hope.
        foreach (var tuple in SkillsLearnedAtLevel)
        {
            var skillName = tuple.Item1;
            
            if (Skills.Any(s => s.Name.ToUpperInvariant() == skillName.ToUpperInvariant()))
            {
                continue;
            }

            var learnedAtLevel = tuple.Item2;
            if (learnedAtLevel <= Level)
            {
                this.Skills.Add(Skill.GetSkill(skillName));
                console.WriteLine($"{Name} learned the skill [{Colours.Highlight}]{skillName}[/]!");
            }
        }
    }

    internal Equipment? EquippedOn(ItemType slot)
    {
        Equipment? currentlyEquipped;
        if (Equipment.TryGetValue(slot, out currentlyEquipped))
        {
            return currentlyEquipped;
        }
        
        return null; // nothing equipped
    }

    public override void OnRoundComplete(IConsole console)
    {
        this.IsDefending = false;
        base.OnRoundComplete(console);
    }

    internal void Defend(IConsole console)
    {
        this.IsDefending = true;
        console.WriteLine($"{Name} defends!");
    }

    public override string ToString()
    {
        return $"{this.Name}: {this.CurrentHealth}/{this.TotalHealth} health, {this.CurrentSkillPoints}/{this.TotalSkillPoints} skill points";
    }

    public string GetStats()
    {
        return $"{Strength} strength, {Toughness} toughness, {Special} special, {SpecialDefense} special defense";
    }
}
