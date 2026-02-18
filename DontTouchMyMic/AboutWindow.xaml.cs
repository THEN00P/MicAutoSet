using System;
using Windows.Graphics;
using Windows.System;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using DontTouchMyMic.Utils;

namespace DontTouchMyMic
{
    public sealed partial class AboutWindow : Window
    {
        private static readonly Uri ReleasesUri = new("https://github.com/THEN00P/dont-touch-my-mic/releases");
        private static readonly SizeInt32 LogicalWindowSize = new(480, 264);

        public string InstalledVersionText { get; }
        public string BuildDateText { get; }
        public string BuildBranchText { get; }
        public string BuildCommitText { get; }

        public AboutWindow()
        {
            InitializeComponent();

            Title = string.Empty;
            ExtendsContentIntoTitleBar = true;
            SetTitleBar(TitleBarDragRegion);
            SystemBackdrop = new MicaBackdrop();

            if (AppWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.IsResizable = false;
                presenter.IsMaximizable = false;
                presenter.IsMinimizable = false;
            }

            InstalledVersionText = AppMetadata.GetInstalledVersion();
            BuildDateText = AppMetadata.GetBuildDate();
            BuildBranchText = AppMetadata.GetBuildBranch();
            BuildCommitText = AppMetadata.GetBuildCommit();

            ResizeForCurrentScale();
            CenterOnScreen();
        }

        public void CenterOnScreen()
        {
            ResizeForCurrentScale();

            var displayArea = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary);
            var workArea = displayArea.WorkArea;

            var width = AppWindow.Size.Width;
            var height = AppWindow.Size.Height;

            var x = workArea.X + Math.Max(0, (workArea.Width - width) / 2);
            var y = workArea.Y + Math.Max(0, (workArea.Height - height) / 2);

            AppWindow.Move(new PointInt32(x, y));
        }

        private void ResizeForCurrentScale()
        {
            AppWindow.Resize(WindowScaleHelper.ScaleSizeForWindow(this, LogicalWindowSize));
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private async void CheckForUpdates_Click(object sender, RoutedEventArgs e)
        {
            await Launcher.LaunchUriAsync(ReleasesUri);
        }
    }
}
