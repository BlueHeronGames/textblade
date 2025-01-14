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

        if (user.GetType() != target.GetType())
        {
            damage = AttackExecutor.CalculateBaseDamage(user, target) * skill.DamageMultiplier;
            if (target is Monster m)
            {
                damage = AttackExecutor.BoostDamageIfDamageTypesMatch(damage, skill.DamageType, m);
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
        var damageMessage = damage > 0 ? $"{roundedDamage} damage" : $"healed for [green]{-roundedDamage}[/]";
        var effectiveMessage = AttackExecutor.IsSuperEffective(skill.DamageType, target as Monster) ? "[#f80]Super effective![/]" : string.Empty;

        var finalMessage = $"{user.Name} uses [#faa]{skill.Name} on {target.Name}[/]!";
        if (damage != 0)
        {
            finalMessage = $"{finalMessage} {effectiveMessage} {damageMessage}!";
        }
        if (target.CurrentHealth <= 0)
        {
            finalMessage = $"{finalMessage} {target.Name} DIES!";
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

        string statusColour = Colours.Highlight;
        switch (status)
        {
            case "Burn":
                statusColour = Colours.Fire;
                break;
            case "Poison":
                statusColour = Colours.Poison;
                break;
            case "Paralyze":
                statusColour = Colours.Paralyze;
                break;
        }

        _console.WriteLine($"{user.Name} inflicts [{statusColour}]{skill.StatusInflicted} x{skill.StatusStacks}[/] on {target.Name}!");
    }
}
