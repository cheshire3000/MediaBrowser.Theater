using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Data;
using System.Windows.Input;
using MediaBrowser.Model.ApiClient;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Logging;
using MediaBrowser.Plugins.DefaultTheme.Home;
using MediaBrowser.Theater.Interfaces.Playback;
using MediaBrowser.Theater.Interfaces.Presentation;
using MediaBrowser.Theater.Interfaces.Session;
using MediaBrowser.Theater.Interfaces.ViewModels;
using MediaBrowser.Theater.Presentation.ViewModels;

namespace MediaBrowser.Plugins.DefaultTheme.PlaylistViewer
{
    public class NowPlayingWindowViewModel : BaseViewModel
    {
        private readonly IPlaybackManager _playbackManager;
        private readonly IApiClient _apiClient;
        private readonly IImageManager _imageManager;
        private readonly IPresentationManager _presentationManager;
        private readonly ILogger _logger;
        private readonly IServerEvents _serverEvents;
        private readonly ISessionManager _sessionManager;

        private ItemViewModel _currentItem;
        private ICollectionView _playlistItems;

        public ItemViewModel CurrentItem
        {
            get { return _currentItem; }
            set
            {
                _currentItem = value;
                OnPropertyChanged("CurrentItem");
            }
        }

        public ICollectionView PlaylistItems
        {
            get { return _playlistItems; }
            set
            {
                _playlistItems = value;
                OnPropertyChanged("PlaylistItems");
            }
        }

        public ICommand CloseCommand { get; private set; }

        public event Action CloseDialogRequested;

        public NowPlayingWindowViewModel(ILogger logger, IPresentationManager presentationManager, ISessionManager sessionManager, IPlaybackManager playbackManager, IApiClient apiClient, IImageManager imageManager, IServerEvents serverEvents)
        {
            _playbackManager = playbackManager;
            _logger = logger;
            _presentationManager = presentationManager;
            _apiClient = apiClient;
            _imageManager = imageManager;
            _serverEvents = serverEvents;
            _sessionManager = sessionManager;

            _playbackManager.PlaybackStarted += _playbackManager_PlaybackStarted;
            CloseCommand = new RelayCommand(CloseCommandHandler);

            SetupTestPlaylist();
        }

        private async void SetupTestPlaylist()
        {
            List<BaseItemDto> items = new List<BaseItemDto>();

            items.Add(await _apiClient.GetItemAsync("9c709573361566e761d214d271f37e1f", _sessionManager.CurrentUser.Id));
            items.Add(await _apiClient.GetItemAsync("de815dabc32cd08511eb057c99b61185", _sessionManager.CurrentUser.Id));
            items.Add(await _apiClient.GetItemAsync("0d3c6fbe8621b527571d31969c64ea02", _sessionManager.CurrentUser.Id));

            PlayOptions options = new PlayOptions {Items = items, Resume = true};

            await _playbackManager.Play(options);
        }

        void _playbackManager_PlaybackStarted(object sender, PlaybackStartEventArgs e)
        {
            var itemViewModel = new ItemViewModel(_apiClient, _imageManager, _playbackManager, _presentationManager,
                _logger, _serverEvents)
            {
                Item = _playbackManager.CurrentMediaPlayer.CurrentMedia,
                ImageWidth = 400,
                PreferredImageTypes = new[] {ImageType.Thumb, ImageType.Primary},
                DisplayName = HomePageViewModel.GetDisplayName(_playbackManager.CurrentMediaPlayer.CurrentMedia),
            };

            var playlist = (from playlistItem in _playbackManager.CurrentMediaPlayer.Playlist
                where playlistItem != _playbackManager.CurrentMediaPlayer.CurrentMedia
                select new ItemViewModel(_apiClient, _imageManager, _playbackManager, _presentationManager, _logger, _serverEvents)
                {
                    Item = _playbackManager.CurrentMediaPlayer.CurrentMedia,
                    ImageWidth = 400, PreferredImageTypes = new[] {ImageType.Thumb, ImageType.Primary}, 
                    DisplayName = HomePageViewModel.GetDisplayName(_playbackManager.CurrentMediaPlayer.CurrentMedia),
                }).ToList();

            PlaylistItems = CollectionViewSource.GetDefaultView(playlist);
            CurrentItem = itemViewModel;
        }

        private void CloseCommandHandler(object obj)
        {
            CloseDialogRequested();
        }
    }
}
