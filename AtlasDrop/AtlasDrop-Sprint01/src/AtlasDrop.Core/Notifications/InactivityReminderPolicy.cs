namespace AtlasDrop.Core.Notifications;

public sealed class InactivityReminderPolicy
{
    public TimeSpan ReminderDelay { get; }

    public InactivityReminderPolicy(
        TimeSpan? reminderDelay = null)
    {
        ReminderDelay = reminderDelay
            ?? TimeSpan.FromSeconds(45);
    }

    public bool ShouldRemind(
        DateTime nowUtc,
        DateTime lastActivityUtc,
        bool isUserTyping,
        bool reminderAlreadyShown)
    {
        if (isUserTyping || reminderAlreadyShown)
            return false;

        return nowUtc - lastActivityUtc >= ReminderDelay;
    }
}
