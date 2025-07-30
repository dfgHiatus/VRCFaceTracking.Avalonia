using System;
using System.Linq;
using System.Threading;
using Avalonia;
using DesktopNotifications;
using DesktopNotifications.Avalonia;
using VRCFaceTracking.Models;
using Velopack;

namespace VRCFaceTracking.Avalonia.Desktop;

sealed class Program
{
    internal static INotificationManager NotificationManager;

    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static int Main(string[] args)
    {
        VelopackApp.Build().Run();
        var builder = BuildAvaloniaApp();

        App.SendNotification += NotificationRequested;
        NotificationManager.NotificationActivated += OnNotificationActivated;
        NotificationManager.NotificationDismissed += OnNotificationDismissed;

        return builder.StartWithClassicDesktopLifetime(args);
    }

    private static void NotificationRequested(NotificationModel notificationModel)
    {
        Notification notification = new()
        {
            Title = notificationModel.Title,
            Body = notificationModel.Body,
            BodyImagePath = notificationModel.BodyImagePath,
            BodyImageAltText = notificationModel.BodyAltText!
        };

        if (notificationModel.ActionButtons is not null)
        {
            if (notificationModel.ActionButtons.Count != 0)
                notification.Buttons.AddRange(notificationModel.ActionButtons.
                    Where(x => x.HasValue).
                    Select(x => x!.Value));
        }

        if (notificationModel is { OptionalScheduledTime: not null, OptionalExpirationTime: not null })
            NotificationManager.ScheduleNotification(notification, notificationModel.OptionalScheduledTime.Value, notificationModel.OptionalExpirationTime.Value);
        else if (notificationModel.OptionalScheduledTime.HasValue)
            NotificationManager.ScheduleNotification(notification, notificationModel.OptionalScheduledTime.Value);
        else
            NotificationManager.ShowNotification(notification);
    }

    private static void OnNotificationActivated(object? sender, NotificationActivatedEventArgs e)
    {

    }

    private static void OnNotificationDismissed(object? sender, NotificationDismissedEventArgs e)
    {

    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace()
            .SetupDesktopNotifications(out NotificationManager);
}
