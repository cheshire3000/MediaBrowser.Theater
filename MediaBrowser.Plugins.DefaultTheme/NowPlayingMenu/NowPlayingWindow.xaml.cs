using MediaBrowser.Plugins.DefaultTheme.PlaylistViewer;
using MediaBrowser.Theater.Presentation.Controls;

namespace MediaBrowser.Plugins.DefaultTheme.NowPlayingMenu
{
    public partial class NowPlayingWindow : BaseModalWindow
    {
        public NowPlayingWindow()
        {
            InitializeComponent();
            DataContext = new PlayListViewerWindowViewModel();
        }
    }
}
