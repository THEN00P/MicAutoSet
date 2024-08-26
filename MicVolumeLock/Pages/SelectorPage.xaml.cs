using Windows.Graphics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using CoreAudio;

namespace MicVolumeLock.Pages
{
    public sealed partial class SelectorPage : Page
    {
        public static readonly SizeInt32 PageSize = new(375, 375);
        
        public SelectorPage()
        {
            InitializeComponent();

            // App.Enumerator.RegisterEndpointNotificationCallback();
            foreach (var device in App.Enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active))
            {
                DeviceList.Items.Add(device.FriendlyName);
            }
            
            DeviceList.SelectedIndex = 0;
        }
        
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if (App.MainWindow != null)
            {
                int windowY = App.MainWindow.AppWindow.Position.Y + App.MainWindow.AppWindow.Size.Height -
                              PageSize.Height;
                App.MainWindow.AppWindow.Resize(new SizeInt32(PageSize.Width, PageSize.Height));
                App.MainWindow.AppWindow.Move(new PointInt32(App.MainWindow.AppWindow.Position.X, windowY));
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            App.MainWindow.NavigateBack();
        }
    }
}
