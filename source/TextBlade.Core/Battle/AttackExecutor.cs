using System.Text;
using TextBlade.Core.Characters;
using TextBlade.Core.IO;

namespace TextBlade.Core.Battle;

internal class AttackExecutor
{
    /// <summary>
    /// Calculates damage attacker inflicts on defender.
    /// Takes into account attacker's weapon and defender's weakness.
    /// </summary>
    public static int CalculateBaseDamage(Entity attacker, Entity defender)
    {
        // total strength? or strength? vs. toughness
        var strength = attacker is Character c1 ? c1.TotalStrength : attacker.Strength;
        var toughness = defender is Character c2 ? c2.TotalToughness : defender.Toughness;

        return Math.Max(0, strength - toughness);
    }

    public static bool IsSuperEffective(string attackType, Monster targetMonster)
    {
       return attackType  == targetMonster.Weakness;
    }

    /// <summary>
    /// If character's weapon's damage type is target's weakness, double damage.
    /// </summary>
    public static float BoostDamageIfDamageTypesMatch(float damage, string attackDamageType, Monster targetMonster)
    {
        if (attackDamageType == targetMonster.Weakness)
        {
            return damage * 2.0f;
        }
        
        return damage;
    }

    private readonly IConsole _console;

    public AttackExecutor(IConsole console)
    {
        _console = console;
    }

    /// <summary>
    /// A basic melee attack between a character and a monster.
    /// Takes into account equipment, and damage type weaknesses.
    /// </summary>
    public void Attack(Character character, Monster targetMonster)
    {
        ArgumentNullException.ThrowIfNull(character);
        ArgumentNullException.ThrowIfNull(targetMonster);

        // Assume target number is legit
        var message = new StringBuilder();
        message.Append($"{character.Name} attacks {targetMonster.Name}! ");
        
        float damage = CalculateBaseDamage(character, targetMonster);
        damage = BoostDamageIfDamageTypesMatch(damage, character.EquippedOn(Inv.ItemType.Weapon)?.DamageType, targetMonster);
        var effectiveMessage = IsSuperEffective(character.EquippedOn(Inv.ItemType.Weapon)?.DamageType, targetMonster) ? "[#f80]Super effective![/]" : string.Empty; 

        targetMonster.Damage((int)damage);
        
        var damageAmount = damage <= 0 ? "NO" : damage.ToString();
        message.Append($"[{Colours.Highlight}]{damageAmount}[/] damage! {effectiveMessage}");
        if (targetMonster.CurrentHealth <= 0)
        {
            message.Append($"{targetMonster.Name} DIES!");
        }
        
        _console.WriteLine(message.ToString());
    }
}
