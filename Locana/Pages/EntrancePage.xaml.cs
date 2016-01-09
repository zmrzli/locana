﻿using Locana.DataModel;
using Locana.Network;
using Locana.Utility;
using Naotaco.Nfc;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Networking.Proximity;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace Locana.Pages
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class EntrancePage : Page
    {
        public EntrancePage()
        {
            this.InitializeComponent();

            appMenuGroup.Add(new EntrancePanel("Find QR Code",
                Symbol.WebCam, () =>
                {
                    Frame.Navigate(typeof(QrCodePage));
                }));
            appMenuGroup.Add(new EntrancePanel(SystemUtil.GetStringResource("WifiSettingLauncherButtonText"),
                Symbol.ThreeBars, async () =>
                {
                    await Launcher.LaunchUriAsync(new Uri("ms-settings-wifi:"));
                }));
            appMenuGroup.Add(new EntrancePanel(SystemUtil.GetStringResource("AppSettings"),
                Symbol.Setting, () =>
                {
                    Frame.Navigate(typeof(AppSettingPage));
                }));

            panelSource.Add(devicesGroup);
            panelSource.Add(appMenuGroup);
        }

        private EntrancePanelGroupCollection panelSource = new EntrancePanelGroupCollection();

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            NetworkObserver.INSTANCE.CameraDiscovered += NetworkObserver_Discovered;
            NetworkObserver.INSTANCE.DevicesCleared += NetworkObserver_DevicesCleared;
            NetworkObserver.INSTANCE.ForceRestart();

            if (e.Parameter != null && (e.Parameter as string).Length > 1)
            {
                DebugUtil.Log("found data from QR code: " + e.Parameter as string);
                try
                {
                    var data = SonyQrDataParser.ParseData(e.Parameter as string);
                    await OnConnectionInfoFound(data.SSID, data.Password);
                }
                catch (FormatException ex)
                {
                    DebugUtil.Log("QR data parse error: " + ex.Message);
                }
            }

            SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility =
                Frame.CanGoBack
                ? AppViewBackButtonVisibility.Visible
                : AppViewBackButtonVisibility.Collapsed;
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            NetworkObserver.INSTANCE.CameraDiscovered -= NetworkObserver_Discovered;
            NetworkObserver.INSTANCE.DevicesCleared -= NetworkObserver_DevicesCleared;
            NetworkObserver.INSTANCE.Stop();

            base.OnNavigatedFrom(e);
        }

        private EntrancePanelGroup appMenuGroup = new EntrancePanelGroup(SystemUtil.GetStringResource("PanelGroup_AppMenu"));

        private EntrancePanelGroup devicesGroup = new EntrancePanelGroup(SystemUtil.GetStringResource("PanelGroup_Devices"));

        private void EntranceGrid_Loaded(object sender, RoutedEventArgs e)
        {
            devicesGroup.Clear();
            foreach (var device in NetworkObserver.INSTANCE.CameraDevices)
            {
                devicesGroup.Add(new DevicePanel(device));
            }

            PanelSources.Source = panelSource;
        }

        private void EntranceGrid_Unloaded(object sender, RoutedEventArgs e)
        {
            PanelSources.Source = null;
        }

        private void NetworkObserver_Discovered(object sender, CameraDeviceEventArgs e)
        {
            var task = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                devicesGroup.Add(new DevicePanel(e.CameraDevice));
            });
        }

        private void NetworkObserver_DevicesCleared(object sender, EventArgs e)
        {
            var task = Dispatcher.RunAsync(CoreDispatcherPriority.High, () =>
            {
                devicesGroup.Clear();
            });
        }

        private void PanelHolder_Tapped(object sender, Windows.UI.Xaml.Input.TappedRoutedEventArgs e)
        {
            var grid = sender as Grid;
            var content = grid.DataContext as EntrancePanel;
            content.OnClick();
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            InitializeProximityDevice();
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            StopProximityDevice();
        }

        ProximityDevice _ProximityDevice;
        long ProximitySubscribeId;

        private void InitializeProximityDevice()
        {
            StopProximityDevice();

            try
            {
                _ProximityDevice = ProximityDevice.GetDefault();
            }
            catch (FileNotFoundException)
            {
                _ProximityDevice = null;
                DebugUtil.Log("Caught ununderstandable exception. ");
                return;
            }
            catch (COMException)
            {
                _ProximityDevice = null;
                DebugUtil.Log("Caught ununderstandable exception. ");
                return;
            }

            if (_ProximityDevice == null)
            {
                DebugUtil.Log("It seems this is not NFC available device");
                return;
            }

            try
            {
                ProximitySubscribeId = _ProximityDevice.SubscribeForMessage("NDEF", ProximityMessageReceivedHandler);
            }
            catch (Exception e)
            {
                _ProximityDevice = null;
                DebugUtil.Log("Caught ununderstandable exception. " + e.Message + e.StackTrace);
                return;
            }
        }

        private void StopProximityDevice()
        {
            if (_ProximityDevice != null)
            {
                _ProximityDevice.StopSubscribingForMessage(ProximitySubscribeId);
                _ProximityDevice = null;
            }
        }

        private async void ProximityMessageReceivedHandler(ProximityDevice sender, ProximityMessage message)
        {
            var parser = new NdefParser(message);
            var ndefRecords = new List<NdefRecord>();

            var err = "";

            try { ndefRecords = parser.Parse(); }
            catch (NoSonyNdefRecordException) { err = SystemUtil.GetStringResource("ErrorMessage_CantFindSonyRecord"); }
            catch (NoNdefRecordException) { err = SystemUtil.GetStringResource("ErrorMessage_ParseNFC"); }
            catch (NdefParseException) { err = SystemUtil.GetStringResource("ErrorMessage_ParseNFC"); }
            catch (Exception) { err = SystemUtil.GetStringResource("ErrorMessage_fatal"); }

            if (err != "")
            {
                DebugUtil.Log("Failed to read NFC: " + err);
                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => { PageHelper.ShowErrorToast(err); });
                return;
            }

            foreach (NdefRecord r in ndefRecords)
            {
                if (r.SSID.Length > 0 && r.Password.Length > 0)
                {
                    await OnConnectionInfoFound(r.SSID, r.Password);
                    break;
                }
            }
        }

        private async Task OnConnectionInfoFound(string SSID, string Password)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                var sb = new StringBuilder();
                sb.Append(SystemUtil.GetStringResource("Message_NFC_succeed"));
                sb.Append(Environment.NewLine);
                sb.Append(Environment.NewLine);
                sb.Append("SSID: ");
                sb.Append(SSID);
                sb.Append(Environment.NewLine);
                sb.Append("Password: ");
                sb.Append(Password);
                sb.Append(Environment.NewLine);

                PutToClipBoard(Password);

                var dialog = new MessageDialog(sb.ToString());
                try
                {
                    await dialog.ShowAsync();
                }
                catch (UnauthorizedAccessException) {/* Duplicated message dialog */}
            });
        }

        void PutToClipBoard(string s)
        {
            var package = new DataPackage();
            package.RequestedOperation = DataPackageOperation.Copy;
            package.SetText(s);
            Clipboard.SetContent(package);
        }
    }
}
