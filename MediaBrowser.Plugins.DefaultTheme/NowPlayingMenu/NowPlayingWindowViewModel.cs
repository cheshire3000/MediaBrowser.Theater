using System;
using System.Windows.Input;
using MediaBrowser.Theater.Interfaces.Playback;
using MediaBrowser.Theater.Interfaces.ViewModels;

namespace MediaBrowser.Plugins.DefaultTheme.NowPlayingMenu
{
    public class NowPlayingWindowViewModel : BaseViewModel
    {
        private readonly IPlaybackManager _playbackManager;

        public ICommand CloseCommand { get; private set; }

        public event Action CloseDialogRequested;

        public NowPlayingWindowViewModel(IPlaybackManager playbackManager)
        {
            _playbackManager = playbackManager;

            CloseCommand = new RelayCommand(CloseCommandHandler);
        }

        private void CloseCommandHandler(object obj)
        {
            CloseDialogRequested();
        }
    }
}
