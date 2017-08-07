﻿using System;

using NL.HNOGames.Domoticz.ViewModels;
using Xamarin.Forms;
using System.Threading.Tasks;
using System.Collections.Generic;
using Plugin.Share;
using NL.HNOGames.Domoticz.Resources;
using ZXing.Mobile;
using ZXing.Net.Mobile.Forms;
using System.Linq;
using NL.HNOGames.Domoticz.Models;
using Acr.UserDialogs;
using Rg.Plugins.Popup.Services;
using NL.HNOGames.Domoticz.Views.Dialog;

namespace NL.HNOGames.Domoticz.Views.Settings
{
    public partial class QRCodeSettingsPage : ContentPage
    {
        private List<QRCodeModel> oListSource = new List<QRCodeModel>();

        /// <summary>
        /// Constructor of QRCode page
        /// </summary>
        public QRCodeSettingsPage()
        {
            InitializeComponent();

            App.ShowToast(AppResources.qrcode_register);
            swEnableQRCode.IsToggled = App.AppSettings.QRCodeEnabled;
            swEnableQRCode.Toggled += (sender, args) =>
            {
                App.AppSettings.QRCodeEnabled = swEnableQRCode.IsToggled;
            };

            oListSource = App.AppSettings.QRCodes;
            if (oListSource != null)
                listView.ItemsSource = oListSource;
        }
        
        /// <summary>
        /// Add new qr code to system
        /// </summary>
        private async Task ToolbarItem_Activated(object sender, EventArgs e)
        {
            if (!App.AppSettings.QRCodeEnabled)
                return;
            
            var expectedFormat = ZXing.BarcodeFormat.QR_CODE;
            var opts = new ZXing.Mobile.MobileBarcodeScanningOptions
            {
                PossibleFormats = new List<ZXing.BarcodeFormat> { expectedFormat }
            };
            System.Diagnostics.Debug.WriteLine("Scanning " + expectedFormat);

            var scanPage = new ZXingScannerPage(opts);
            scanPage.OnScanResult += (result) => {
                scanPage.IsScanning = false;

                Xamarin.Forms.Device.BeginInvokeOnMainThread(async () =>
                {
                    await Navigation.PopAsync();
                    try
                    {
                        String QRCodeID = result.Text.ToString().GetHashCode() + "";
                        if (oListSource.Any(o => String.Compare(o.Id, QRCodeID, StringComparison.OrdinalIgnoreCase) == 0))
                            App.ShowToast(AppResources.qrcode_exists);
                        else
                            AddNewRecord(result, QRCodeID);
                    }
                    catch (Exception ex)
                    {
                        App.AddLog(ex.Message);
                    }
                });
            };

            await Navigation.PushAsync(scanPage);
        }

        /// <summary>
        /// Create new QR Code object
        /// </summary>
        private void AddNewRecord(ZXing.Result result, string QRCodeID)
        {
            Xamarin.Forms.Device.BeginInvokeOnMainThread(async () =>
            {
                var r = await UserDialogs.Instance.PromptAsync(AppResources.qrcode_name, inputType: InputType.Name);
                await Task.Delay(500);

                if (r.Ok)
                {
                    var name = r.Text;
                    if (!String.IsNullOrEmpty(name))
                    {
                        App.ShowToast(AppResources.qrcode_saved + " " + name);
                        QRCodeModel QRCode = new QRCodeModel()
                        {
                            Id = QRCodeID,
                            Name = name,
                            Enabled = true,
                        };
                        oListSource.Add(QRCode);
                        SaveAndRefresh();
                    }
                }
            });
        }

        /// <summary>
        /// Delete a QR Code from the list
        /// </summary>
        private void btnDeleteButton_Clicked(object sender, EventArgs e)
        {
            QRCodeModel oQRCode = (QRCodeModel)((Button)sender).BindingContext;
            App.ShowToast(AppResources.something_deleted.Replace("%1$s", oQRCode.Name));
            oListSource.Remove(oQRCode);
            SaveAndRefresh();
        }

        /// <summary>
        /// Save and refresh the list of QR Codes
        /// </summary>
        private void SaveAndRefresh()
        {
            App.AppSettings.QRCodes = oListSource;
            listView.ItemsSource = null;
            listView.ItemsSource = oListSource;
        }

        private async Task btnConnect_Clicked(object sender, EventArgs e)
        {
            await PopupNavigation.PushAsync(new SwitchPopup());
        }
    }
}