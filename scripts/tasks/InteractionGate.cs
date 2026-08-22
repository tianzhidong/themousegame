using System;

namespace NewGameProject.Tasks;

public sealed class InteractionGate
{
    public bool Matches(TaskDefinition task, InteractionReport interaction)
    {
        if (!EqualsIgnoreCase(task.Operation, interaction.Operation))
        {
            return false;
        }

        if (!MatchesOptional(task.TargetType, interaction.ObjectType))
        {
            return false;
        }

        if (!MatchesOptional(task.TargetColor, interaction.ObjectColor))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(task.TargetTag)
            && !interaction.ObjectTags.Contains(task.TargetTag))
        {
            return false;
        }

        if (task.TargetIds.Count > 0
            && !task.TargetIds.Exists(id => EqualsIgnoreCase(id, interaction.ObjectId)))
        {
            return false;
        }

        return true;
    }

    private static bool MatchesOptional(string expected, string actual)
    {
        return string.IsNullOrWhiteSpace(expected) || EqualsIgnoreCase(expected, actual);
    }

    private static bool EqualsIgnoreCase(string left, string right)
    {
        return string.Equals(left?.Trim(), right?.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}
