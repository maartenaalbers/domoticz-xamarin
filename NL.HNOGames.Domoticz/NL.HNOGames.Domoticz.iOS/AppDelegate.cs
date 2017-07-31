﻿
using FFImageLoading.Forms.Touch;
using Foundation;
using UIKit;

using System;
using MTiRate;
using PushNotification.Plugin;
using NL.HNOGames.Domoticz.Helpers;
using UserNotifications;
using Firebase.CloudMessaging;
using System.Diagnostics;
using Firebase.InstanceID;
using System.Net;
using Plugin.LocalNotifications;

namespace NL.HNOGames.Domoticz.iOS
{
    [Register("AppDelegate")]
    public partial class AppDelegate : global::Xamarin.Forms.Platform.iOS.FormsApplicationDelegate, IUNUserNotificationCenterDelegate, IMessagingDelegate
    {
        public override bool FinishedLaunching(UIApplication app, NSDictionary options)
        {
            global::Xamarin.Forms.Forms.Init();
            UIApplication.SharedApplication.SetStatusBarStyle(UIStatusBarStyle.LightContent, false);
            UIApplication.SharedApplication.SetStatusBarHidden(false, false);

            UINavigationBar.Appearance.SetTitleTextAttributes(new UITextAttributes()
            {
                Font = UIFont.FromName("HelveticaNeue-Light", (nfloat)20f),
                TextColor = UIColor.White
            });

            iRate.SharedInstance.DaysUntilPrompt = 10;
            iRate.SharedInstance.UsesUntilPrompt = 20;

            System.Net.ServicePointManager.ServerCertificateValidationCallback += (o, certificate, chain, errors) => true;

            CachedImageRenderer.Init();
            SlideOverKit.iOS.SlideOverKit.Init();
            OxyPlot.Xamarin.Forms.Platform.iOS.PlotViewRenderer.Init();

            LoadApplication(new App(SetupFirebase));
            CrossPushNotification.Initialize<CrossPushNotificationListener>();

            // Register your app for remote notifications.
            if (UIDevice.CurrentDevice.CheckSystemVersion(10, 0))
            {
                var authOptions = UNAuthorizationOptions.Alert | UNAuthorizationOptions.Badge | UNAuthorizationOptions.Sound;
                UNUserNotificationCenter.Current.RequestAuthorization(authOptions, (granted, error) =>
                {
                    App.AddLog("GC Authorization granted");
                });

                UNUserNotificationCenter.Current.Delegate = this;
                Messaging.SharedInstance.RemoteMessageDelegate = this;
            }
            else
            {
                var allNotificationTypes = UIUserNotificationType.Alert | UIUserNotificationType.Badge | UIUserNotificationType.Sound;
                var settings = UIUserNotificationSettings.GetSettingsForTypes(allNotificationTypes, null);
                UIApplication.SharedApplication.RegisterUserNotificationSettings(settings);
            }

            UIApplication.SharedApplication.RegisterForRemoteNotifications();
            SetupFirebase();

            return base.FinishedLaunching(app, options);
        }

        /// <summary>
        /// Setup Firebase
        /// </summary>
        public void SetupFirebase()
        {

            // Firebase component initialize
            Firebase.Analytics.App.Configure();
            Firebase.InstanceID.InstanceId.Notifications.ObserveTokenRefresh((sender, e) =>
            {
                var refreshedToken = Firebase.InstanceID.InstanceId.SharedInstance.Token;
                tokenUploaded = false;
                SaveToken();
            });

            connectFCM();
        }

        public override void DidEnterBackground(UIApplication application)
        {
            Messaging.SharedInstance.Disconnect();
            Console.WriteLine("Disconnected from FCM");
        }

        private async void registerAsync(String token)
        {
            tokenUploaded = true;
            App.AddLog(string.Format("GCM: Push Notification - Device Registered - Token : {0}", token));
            String Id = Helpers.UsefulBits.GetDeviceID();
            bool bSuccess = await App.ApiService.RegisterDevice(Id, token);
            if (bSuccess)
                App.AddLog("GCM: Device registered on Domoticz");
            else
                App.AddLog("GCM: Device not registered on Domoticz");
        }

        private Boolean tokenUploaded = false;
        private void connectFCM()
        {
            Messaging.SharedInstance.Connect((error) =>
            {
                if (error == null)
                    Messaging.SharedInstance.Subscribe("/topics/all");

                App.AddLog(error != null ? "GCM: error occured: " + error.Description : "GCM: connect success");
                if (error != null && !tokenUploaded)
                    SaveToken();
            });
        }

        /// <summary>
        /// Save token to domoticz
        /// </summary>
        private void SaveToken()
        {
            var token = Firebase.InstanceID.InstanceId.SharedInstance.Token;
            if (token != null)
                registerAsync(token);
            else
                App.AddLog("GCM: No token available");
            tokenUploaded = true;
        }

        public override void OnActivated(UIApplication uiApplication)
        {
            base.OnActivated(uiApplication);
            connectFCM();
        }

        // iOS 9 <=, fire when recieve notification foreground
        public override void DidReceiveRemoteNotification(UIApplication application, NSDictionary userInfo, Action<UIBackgroundFetchResult> completionHandler)
        {
            if (App.AppSettings.EnableNotifications)
            {
                App.AddLog("GCM: Notification received");
                App.AddLog(userInfo.ToString());
                var body = WebUtility.UrlDecode(userInfo["gcm.notification.message"] as NSString);
                var title = WebUtility.UrlDecode(userInfo["gcm.notification.title"] as NSString);
                if (String.IsNullOrEmpty(title))
                    title = WebUtility.UrlDecode(userInfo["gcm.notification.subject"] as NSString);
                if (String.Compare(title, body, true) == 0)
                    title = "Domoticz";
                if (application.ApplicationState == UIApplicationState.Active)
                    debugAlert(title, body);
                
                //else if (App.AppSettings.EnableNotifications)
                //    CrossLocalNotifications.Current.Show(title, body);
            }
        }

        // iOS 10, fire when recieve notification foreground
        [Export("userNotificationCenter:willPresentNotification:withCompletionHandler:")]
        public void WillPresentNotification(UNUserNotificationCenter center, UNNotification notification, Action<UNNotificationPresentationOptions> completionHandler)
        {
            if (App.AppSettings.EnableNotifications)
            {
                App.AddLog("GCM: Notification received");
                var title = WebUtility.UrlDecode(notification.Request.Content.Title);
                var body = WebUtility.UrlDecode(notification.Request.Content.Body);
                debugAlert(title, body);
            }
        }

        private void debugAlert(string title, string message)
        {
            var alert = new UIAlertView(title ?? "Title", message ?? "Message", null, "Cancel", "OK");
            alert.Show();
        }

        // Receive data message on iOS 10 devices.
        public void ApplicationReceivedRemoteMessage(RemoteMessage remoteMessage)
        {
            App.AddLog("GCM: Notification received");
            App.AddLog(remoteMessage.AppData.ToString());
        }
    }
}
