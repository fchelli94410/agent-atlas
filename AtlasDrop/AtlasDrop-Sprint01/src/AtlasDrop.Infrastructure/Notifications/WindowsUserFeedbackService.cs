using System.Runtime.InteropServices;
using AtlasDrop.Core.Notifications;

namespace AtlasDrop.Infrastructure.Notifications;

public sealed class WindowsUserFeedbackService
    : IUserFeedbackService
{
    private const uint MbIconAsterisk = 0x00000040;
    private const uint MbIconExclamation = 0x00000030;
    private const uint MbOk = 0x00000000;

    public void PlayLaunchSound()
    {
        _ = MessageBeep(MbIconAsterisk);
    }

    public void PlaySuccessSound()
    {
        _ = MessageBeep(MbIconExclamation);
    }

    public void PlayReminderSound()
    {
        _ = MessageBeep(MbOk);
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool MessageBeep(uint uType);
}
