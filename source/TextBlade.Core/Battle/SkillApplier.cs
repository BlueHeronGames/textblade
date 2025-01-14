using TextBlade.Core.Characters;
using TextBlade.Core.IO;

namespace TextBlade.Core.Battle;

public class SkillApplier
{
    private readonly IConsole _console;

    public SkillApplier(IConsole console)
    {
        _console = console;
    }
    
    internal void Apply(Entity user, Skill skill, IEnumerable<Entity> targets)
    {
        foreach (var target in targets)
        {
            ApplyDamage(user, skill, target);
            InflictStatuses(user, skill, target);
        }

        user.CurrentSkillPoints -= skill.Cost;
    }

    private void ApplyDamage(Entity user, Skill skill, Entity target)
    {
        /////// TODO: REFACTOR so this method is not polymorphic: healing *and* damage.
        
        ArgumentNullException.ThrowIfNull(target);
        float damage = 0;
        var hitWeakness = false;

        if (user.GetType() != target.GetType())
        {
            var totalStrength = user is Character character ? character.TotalStrength : user.Strength;
            damage = (totalStrength - target.Toughness) * skill.DamageMultiplier;
            if (target is Monster m && m.Weakness == skill.DamageType)
            {
                // Targeting their weakness? 2x damage!
                damage *= 2;
                hitWeakness = true;
            }
        }
        else if (user.GetType() == target.GetType())
        {
            var skillPower = user.Special;
            // If you're healing, heal for 2x
            damage = (int)Math.Ceiling(skillPower * -skill.DamageMultiplier * 2);
        }

        var roundedDamage = (int)damage;
        target.Damage(roundedDamage);
        // TODO: DRY the 2x damage part with CharacterTurnProcessor
        var damageMessage = damage > 0 ? $"{roundedDamage} damage" : $"healed for [green]{-roundedDamage}[/]";
        var effectiveMessage = hitWeakness ? "[#f80]Super effective![/]" : "";

        var finalMessage = ($"{user.Name} uses [#faa]{skill.Name} on {target.Name}[/]!");
        if (damage != 0)
        {
            finalMessage = $"{finalMessage} {effectiveMessage} {damageMessage}!";
        }
        _console.WriteLine(finalMessage);
    }
    
    private void InflictStatuses(Entity user, Skill skill, Entity target)
    {
        if (string.IsNullOrWhiteSpace(skill.StatusInflicted))
        {
            return;
        }

        var status = skill.StatusInflicted;
        var stacks = skill.StatusStacks;
        target.InflictStatus(status, stacks);

        _console.WriteLine($"{user.Name} inflicts [{Colours.Highlight}]{skill.StatusInflicted} x{skill.StatusStacks}[/] on {target.Name}!");
    }
}
