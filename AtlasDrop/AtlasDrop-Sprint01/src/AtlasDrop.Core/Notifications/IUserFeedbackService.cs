namespace AtlasDrop.Core.Notifications;

public interface IUserFeedbackService
{
    void PlayLaunchSound();

    void PlaySuccessSound();

    void PlayReminderSound();
}
