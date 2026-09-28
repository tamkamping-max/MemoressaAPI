namespace Memoressa.Application.Common;

/// <summary>Calendar constraints parsed from natural-language Agent queries.</summary>
public readonly record struct AgentCalendarFilter(int? Year, int? Month, int? Day)
{
    public bool HasAny => Year.HasValue || Month.HasValue || Day.HasValue;

    public bool Matches(DateTime? takenAt)
    {
        if (!takenAt.HasValue)
        {
            return false;
        }

        var dt = takenAt.Value;
        if (Year.HasValue && dt.Year != Year.Value)
        {
            return false;
        }

        if (Month.HasValue && dt.Month != Month.Value)
        {
            return false;
        }

        if (Day.HasValue && dt.Day != Day.Value)
        {
            return false;
        }

        return true;
    }
}
