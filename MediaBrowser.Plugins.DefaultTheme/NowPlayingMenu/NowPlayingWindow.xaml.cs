using System;
using System.Windows.Media.Animation;
using MediaBrowser.Theater.Interfaces.Playback;
using MediaBrowser.Theater.Presentation.Controls;

namespace MediaBrowser.Plugins.DefaultTheme.NowPlayingMenu
{
    public partial class NowPlayingWindow : BaseModalWindow
    {
        private readonly NowPlayingWindowViewModel _viewModel;

        public NowPlayingWindow(IPlaybackManager playbackManager)
        {
            InitializeComponent();
            _viewModel = new NowPlayingWindowViewModel(playbackManager);
            _viewModel.CloseDialogRequested += ViewModel_CloseDialogRequested;
            DataContext = _viewModel;
        }

        private void ViewModel_CloseDialogRequested()
        {
            var closeModalStoryboard = (Storyboard)FindResource("ClosingModalStoryboard");
            closeModalStoryboard.Completed += closeModalStoryboard_Completed;
            closeModalStoryboard.Begin();
        }

        void closeModalStoryboard_Completed(object sender, EventArgs e)
        {
            base.CloseModal();
        }
    }
}
