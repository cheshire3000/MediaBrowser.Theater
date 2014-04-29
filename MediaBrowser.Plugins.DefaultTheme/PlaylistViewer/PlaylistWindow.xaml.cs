using System;
using System.Windows.Media.Animation;
using MediaBrowser.Model.ApiClient;
using MediaBrowser.Model.Logging;
using MediaBrowser.Theater.Interfaces.Playback;
using MediaBrowser.Theater.Interfaces.Presentation;
using MediaBrowser.Theater.Interfaces.Session;
using MediaBrowser.Theater.Presentation.Controls;

namespace MediaBrowser.Plugins.DefaultTheme.PlaylistViewer
{
    public partial class PlaylistWindow : BaseModalWindow
    {
        private readonly NowPlayingWindowViewModel _viewModel;

        public PlaylistWindow(ILogger logger, IPresentationManager presentationManager, ISessionManager sessionManager, IPlaybackManager playbackManager, IApiClient apiClient, IImageManager imageManager, IServerEvents serverEvents)
        {
            InitializeComponent();
            _viewModel = new NowPlayingWindowViewModel(logger, presentationManager, sessionManager, playbackManager, apiClient,
                imageManager, serverEvents);
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
