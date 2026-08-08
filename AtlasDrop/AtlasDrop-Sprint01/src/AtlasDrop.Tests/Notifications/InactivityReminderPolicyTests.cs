using Xunit;
using AtlasDrop.Core.Notifications;

namespace AtlasDrop.Tests.Notifications;

public sealed class InactivityReminderPolicyTests
{
    [Fact]
    public void Reminder_is_shown_after_delay()
    {
        var policy = new InactivityReminderPolicy(
            TimeSpan.FromSeconds(30));

        var now = DateTime.UtcNow;

        Assert.True(
            policy.ShouldRemind(
                now,
                now.AddSeconds(-31),
                isUserTyping: false,
                reminderAlreadyShown: false));
    }

    [Fact]
    public void Reminder_is_not_shown_while_typing()
    {
        var policy = new InactivityReminderPolicy(
            TimeSpan.FromSeconds(30));

        var now = DateTime.UtcNow;

        Assert.False(
            policy.ShouldRemind(
                now,
                now.AddMinutes(-5),
                isUserTyping: true,
                reminderAlreadyShown: false));
    }

    [Fact]
    public void Reminder_is_not_repeated_until_new_activity()
    {
        var policy = new InactivityReminderPolicy(
            TimeSpan.FromSeconds(30));

        var now = DateTime.UtcNow;

        Assert.False(
            policy.ShouldRemind(
                now,
                now.AddMinutes(-5),
                isUserTyping: false,
                reminderAlreadyShown: true));
    }

    [Fact]
    public void Reminder_is_not_shown_before_delay()
    {
        var policy = new InactivityReminderPolicy(
            TimeSpan.FromSeconds(30));

        var now = DateTime.UtcNow;

        Assert.False(
            policy.ShouldRemind(
                now,
                now.AddSeconds(-29),
                isUserTyping: false,
                reminderAlreadyShown: false));
    }
}
