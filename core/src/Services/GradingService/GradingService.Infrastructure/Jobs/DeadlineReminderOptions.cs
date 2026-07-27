namespace GradingService.Infrastructure.Jobs;

public class DeadlineReminderOptions
{
    public const string SectionName = "DeadlineReminder";

    /// <summary>How often the sweep runs.</summary>
    public int IntervalHours { get; set; } = 6;

    /// <summary>A subject's exam deadline counts as "approaching" within this many days (or already past).</summary>
    public int ReminderWindowDays { get; set; } = 3;
}
