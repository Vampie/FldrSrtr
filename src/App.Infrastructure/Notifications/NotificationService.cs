using System.Drawing;
using System.Windows.Forms;

namespace App.Infrastructure.Notifications
{
    /// <summary>
    /// Fire-and-forget balloon tip after a manual run — no persistent tray icon per the
    /// architecture decision (no background process, everything is user-triggered).
    /// </summary>
    public class NotificationService
    {
        public void ShowBalloonTip(string title, string message)
        {
            using (var icon = new NotifyIcon())
            {
                icon.Icon = SystemIcons.Information;
                icon.Visible = true;
                icon.BalloonTipTitle = title;
                icon.BalloonTipText = message;
                icon.ShowBalloonTip(4000);

                // Give Windows a moment to render the balloon before the icon (and its handle) is disposed.
                System.Threading.Thread.Sleep(300);
            }
        }
    }
}
