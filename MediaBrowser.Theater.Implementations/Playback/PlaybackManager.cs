﻿using System.Diagnostics;
using System.Threading;
using CoreAudioApi;
using MediaBrowser.Common.Events;
using MediaBrowser.Model.ApiClient;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Library;
using MediaBrowser.Model.Logging;
using MediaBrowser.Theater.Interfaces.Configuration;
using MediaBrowser.Theater.Interfaces.Navigation;
using MediaBrowser.Theater.Interfaces.Playback;
using MediaBrowser.Theater.Interfaces.Presentation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using MediaBrowser.Theater.Interfaces.Theming;

namespace MediaBrowser.Theater.Implementations.Playback
{
    public class PlaybackManager : IPlaybackManager
    {
        private readonly ITheaterConfigurationManager _configurationManager;
        private readonly ILogger _logger;
        private readonly IApiClient _apiClient;
        private readonly INavigationService _nav;
        private readonly IPresentationManager _presentationManager;
        private int _isStarting;

        public event EventHandler<PlaybackStartEventArgs> PlaybackStarted;

        public event EventHandler<PlaybackStopEventArgs> PlaybackCompleted;

        public void AddParts(IEnumerable<IMediaPlayer> mediaPlayers)
        {
            _mediaPlayers.AddRange(mediaPlayers);
        }

        private readonly List<IMediaPlayer> _mediaPlayers = new List<IMediaPlayer>();

        public PlaybackManager(ITheaterConfigurationManager configurationManager, ILogger logger, IApiClient apiClient, INavigationService nav, IPresentationManager presentationManager)
        {
            _configurationManager = configurationManager;
            _logger = logger;
            _apiClient = apiClient;
            _nav = nav;
            _presentationManager = presentationManager;
        }

        public IEnumerable<IMediaPlayer> MediaPlayers
        {
            get { return _mediaPlayers; }
        }

        public IMediaPlayer CurrentMediaPlayer { get; private set; }

        /// <summary>
        /// Plays the specified options.
        /// </summary>
        /// <param name="options">The options.</param>
        /// <returns>Task.</returns>
        /// <exception cref="System.ArgumentNullException">
        /// options
        /// or
        /// options
        /// </exception>
        /// <exception cref="System.InvalidOperationException">There are no available players.</exception>
        public async Task Play(PlayOptions options)
        {
            if (options == null)
            {
                throw new ArgumentNullException("options");
            }

            if (options.Items == null || options.Items.Count(i => i.LocationType != LocationType.Offline) == 0)
            {
                throw new ArgumentException("At least one item must be supplied.");
            }

            PlayerConfiguration config;
            var player = GetPlayer(options.Items, options.EnableCustomPlayers, out config);

            if (player == null)
            {
                throw new InvalidOperationException("There are no available players.");
            }

            StopAllPlayback();

            await Task.Run(() => Play(player, options, config));
        }

        /// <summary>
        /// Plays the specified player.
        /// </summary>
        /// <param name="player">The player.</param>
        /// <param name="options">The options.</param>
        /// <param name="configuration">The configuration.</param>
        /// <returns>Task.</returns>
        private async Task Play(IMediaPlayer player, PlayOptions options, PlayerConfiguration configuration)
        {
            if (options.Items[0].IsPlaceHolder??false)
            {
                // play a physical disk in the CD-ROM drive
                // Will be re-entrant call, so has to be made before the interlocked.CompareExchange below
                await PlayExternalDisc(true);
                return;
            }

            if (Interlocked.CompareExchange(ref _isStarting, 1, 0) == 0) // prevent race conditions, thread safe check we are not already starting to play an item
            {
                try
                {
                    if (options.Shuffle)
                    {
                        options.Items = options.Items.OrderBy(i => Guid.NewGuid()).ToList();
                    }

                    var firstItem = options.Items[0];

                  
                    if (options.StartPositionTicks == 0 && player.SupportsMultiFilePlayback && firstItem.IsVideo && firstItem.LocationType == LocationType.FileSystem && options.GoFullScreen)
                    {
                        try
                        {
                            var intros = await _apiClient.GetIntrosAsync(firstItem.Id, _apiClient.CurrentUserId);

                            options.Items.InsertRange(0, intros.Items);
                        }
                        catch (Exception ex)
                        {
                            _logger.ErrorException("Error retrieving intros", ex);
                        }
                    }


                    options.Configuration = configuration;

                    await player.Play(options);

                    if (player is IInternalMediaPlayer && player is IVideoPlayer && firstItem.IsVideo)
                    {
                        await _presentationManager.Window.Dispatcher.InvokeAsync(() => _presentationManager.WindowOverlay.SetResourceReference(FrameworkElement.StyleProperty, "WindowBackgroundContentDuringPlayback"));

                        if (options.GoFullScreen)
                        {
                            await _nav.NavigateToInternalPlayerPage();
                        }
                    }
                    OnPlaybackStarted(player, options);
                    
                }
                finally
                {
                    Interlocked.Exchange(ref _isStarting, 0);
                }
            }
        }

        /// <summary>
        /// Called when [playback started].
        /// </summary>
        /// <param name="player">The player.</param>
        /// <param name="options">The options.</param>
        private async void OnPlaybackStarted(IMediaPlayer player, PlayOptions options)
        {
            CurrentMediaPlayer = player;

            EventHelper.QueueEventIfNotNull(PlaybackStarted, this, new PlaybackStartEventArgs
            {
                Options = options,
                Player = player
            }, _logger);

           await new PlaybackProgressReporter(_apiClient, player, _logger, this).Start().ConfigureAwait(false);
        }

        /// <summary>
        /// Reports the playback completed.
        /// </summary>
        /// <param name="eventArgs">The <see cref="PlaybackStopEventArgs"/> instance containing the event data.</param>
        public async void ReportPlaybackCompleted(PlaybackStopEventArgs eventArgs)
        {
            CurrentMediaPlayer = null;

            await _presentationManager.Window.Dispatcher.InvokeAsync(() => _presentationManager.WindowOverlay.SetResourceReference(FrameworkElement.StyleProperty, "WindowBackgroundContent"));

            EventHelper.QueueEventIfNotNull(PlaybackCompleted, this, eventArgs, _logger);
        }

        /// <summary>
        /// Stops all playback.
        /// </summary>
        /// <returns>Task.</returns>
        public void StopAllPlayback()
        {
            var players = MediaPlayers
                .Where(p => p.PlayState == PlayState.Playing || p.PlayState == PlayState.Paused)
                .ToList();

            foreach (var player in players)
            {
                player.Stop();
            }

            CurrentMediaPlayer = null;
        }

        private async Task PlayDisc(DriveInfo drive)
        {
            string path = String.Empty;
            VideoType videoType;

            // check if there is a DVD
            if (Directory.Exists(drive.Name + @"\VIDEO_TS"))
            {
                path = drive.Name + @"\VIDEO_TS";
                videoType = VideoType.Dvd;
            }
            else if (Directory.Exists(drive.Name + @"\BDMV"))
            {
                path = drive.Name + @"\BDMV";
                videoType = VideoType.BluRay;
            }
            else
            {
                throw new ApplicationException("The disc in the player contains neither a DVD or BLueray directory");
            }

            var item = new BaseItemDto()
            {
                Id = null,
                Type = "movie",
                VideoType = videoType,
                Path = path,
                Name = drive.VolumeLabel ?? String.Empty,
                MediaType = "Video"
            };

            await Play(new PlayOptions(item));
        }

        /// <summary>
        /// Plays a DVD or Blueray disc in an external disk drive
        /// will ask for the disc to be inserted if it is not ready
        /// </summary>
        public async Task PlayExternalDisc(bool forceAskToInsertDisc)
        {
            var drive = DriveInfo.GetDrives().FirstOrDefault(d => d.DriveType == DriveType.CDRom);
            if (drive == null)
                return;

            var msgResult = MessageBoxResult.OK;
            while ((!drive.IsReady && msgResult != MessageBoxResult.Cancel) || forceAskToInsertDisc)
            {
                msgResult = _presentationManager.ShowMessage(new MessageBoxInfo
                {
                    Button = MessageBoxButton.OKCancel,
                    Caption = "Insert disc",
                    Icon = MessageBoxIcon.Warning,
                    Text = "Insert a disc into the cd/dvd player and hit ok when ready "
                });
                forceAskToInsertDisc = false;
            }

            if (msgResult == MessageBoxResult.Cancel)
                return;

            if (drive.IsReady)
            {
                await PlayDisc(drive);
            }
        }

        /// <summary>
        /// Gets the playable item.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <returns>BaseItemDto.</returns>
        private BaseItemDto GetPlayableItem(string path)
        {
            return new BaseItemDto
            {
                Path = path,
                Name = Path.GetFileName(path),
                Type = "Video",
                MediaType = "Video",
                VideoType = VideoType.VideoFile,
                IsFolder = false
            };
        }

        /// <summary>
        /// Gets the playable item.
        /// </summary>
        /// <param name="uri">The URI.</param>
        /// <param name="name">The name.</param>
        /// <returns>BaseItemDto.</returns>
        private BaseItemDto GetPlayableItem(Uri uri, string name)
        {
            return new BaseItemDto
            {
                Path = uri.ToString(),
                Name = name,
                Type = "Video",
                MediaType = "Video",
                VideoType = VideoType.VideoFile,
                IsFolder = false,
                LocationType = LocationType.Remote
            };
        }

        /// <summary>
        /// Gets the player.
        /// </summary>
        /// <param name="items">The items.</param>
        /// <param name="enableCustomPlayers">if set to <c>true</c> [enable custom players].</param>
        /// <param name="configuration">The configuration.</param>
        /// <returns>IMediaPlayer.</returns>
        private IMediaPlayer GetPlayer(IEnumerable<BaseItemDto> items, bool enableCustomPlayers, out PlayerConfiguration configuration)
        {
            if (enableCustomPlayers)
            {
                var configuredPlayer = _configurationManager.Configuration.MediaPlayers
                    .FirstOrDefault(p => IsConfiguredToPlay(p, items));

                if (configuredPlayer != null)
                {
                    var player = MediaPlayers.FirstOrDefault(i => string.Equals(i.Name, configuredPlayer.PlayerName, StringComparison.OrdinalIgnoreCase));

                    if (player != null)
                    {
                        configuration = configuredPlayer;
                        return player;
                    }
                }
            }

            configuration = null;

            // If there's no explicit configuration just find the first matching player who says they can play it
            return MediaPlayers.FirstOrDefault(p => items.All(p.CanPlayByDefault));
        }

        /// <summary>
        /// Determines whether [is configured to play] [the specified configuration].
        /// </summary>
        /// <param name="configuration">The configuration.</param>
        /// <param name="items">The items.</param>
        /// <returns><c>true</c> if [is configured to play] [the specified configuration]; otherwise, <c>false</c>.</returns>
        private bool IsConfiguredToPlay(PlayerConfiguration configuration, IEnumerable<BaseItemDto> items)
        {
            return items.All(i => IsConfiguredToPlay(configuration, i));
        }

        /// <summary>
        /// Determines whether [is configured to play] [the specified configuration].
        /// </summary>
        /// <param name="configuration">The configuration.</param>
        /// <param name="item">The item.</param>
        /// <returns><c>true</c> if [is configured to play] [the specified configuration]; otherwise, <c>false</c>.</returns>
        private bool IsConfiguredToPlay(PlayerConfiguration configuration, BaseItemDto item)
        {
            // Make this configurable if/when needed
            if (item.LocationType != LocationType.FileSystem)
            {
                return false;
            }

            // If it's configured for specific item types
            if (!string.Equals(configuration.MediaType, item.MediaType, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (string.Equals(configuration.MediaType, MediaType.Video))
            {
                if (!item.VideoType.HasValue)
                {
                    return false;
                }

                if (item.VideoType.Value == VideoType.VideoFile)
                {
                    // If it's configured for specific file extensions
                    if (!IsConfiguredForFileExtension(configuration, item.Path))
                    {
                        return false;
                    }
                }

                if (item.VideoType.Value == VideoType.BluRay && !configuration.PlayBluray)
                {
                    return false;
                }
                if (item.VideoType.Value == VideoType.Dvd && !configuration.PlayDvd)
                {
                    return false;
                }

                if (!configuration.Play3DVideo && item.Video3DFormat.HasValue)
                {
                    return false;
                }

                if (item.VideoType.Value == VideoType.Iso & configuration.IsoMethod == IsoConfiguration.None)
                {
                    return false;
                }
            }

            else if (string.Equals(configuration.MediaType, MediaType.Book))
            {
                // If it's configured for specific file extensions
                if (!IsConfiguredForFileExtension(configuration, item.Path))
                {
                    return false;
                }
            }

            else if (string.Equals(configuration.MediaType, MediaType.Audio))
            {
                // If it's configured for specific file extensions
                if (!IsConfiguredForFileExtension(configuration, item.Path))
                {
                    return false;
                }
            }

            else if (string.Equals(configuration.MediaType, MediaType.Game))
            {
                // If it's configured for specific file extensions
                if (!string.Equals(item.GameSystem, configuration.GameSystem, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            return true;
        }

        private bool IsConfiguredForFileExtension(PlayerConfiguration configuration, string path)
        {
            var extensions = configuration.FileExtensions.Select(i => i.TrimStart('.'));

            return extensions.Contains(Path.GetExtension(path).TrimStart('.'), StringComparer.OrdinalIgnoreCase);
        }

        private event EventHandler _volumeChanged;

        public event EventHandler VolumeChanged
        {
            add
            {
                EnsureAudioDevice();
                _volumeChanged += value;
            }
            remove
            {
                _volumeChanged -= value;
            }
        }

        private bool _lastMuteValue;
        private float _lastVolumeValue;

        private void EnsureAudioDevice()
        {
            if (_audioDevice == null)
            {
                var devEnum = new MMDeviceEnumerator();

                try
                {
                    _audioDevice = devEnum.GetDefaultAudioEndpoint(EDataFlow.eRender, ERole.eMultimedia);
                }
                catch (Exception ex)
                {
                    _logger.ErrorException("Error attempting to discover default audio device", ex);
                    return;
                }

                _audioDevice.AudioEndpointVolume.OnVolumeNotification += AudioEndpointVolume_OnVolumeNotification;

                _lastMuteValue = _audioDevice.AudioEndpointVolume.Mute;
                _lastVolumeValue = _audioDevice.AudioEndpointVolume.MasterVolumeLevelScalar;
            }
        }

        private MMDevice _audioDevice;
        private MMDevice AudioDevice
        {
            get
            {
                EnsureAudioDevice();
                return _audioDevice;
            }
        }

        void AudioEndpointVolume_OnVolumeNotification(AudioVolumeNotificationData data)
        {
            OnVolumeChanged();
        }

        public bool IsMuted
        {
            get
            {
                var audioDevice = AudioDevice;

                if (audioDevice != null)
                {
                    return audioDevice.AudioEndpointVolume.Mute;
                }

                return false;
            }
        }

        public void Mute()
        {
            var audioDevice = AudioDevice;

            if (audioDevice != null)
            {
                audioDevice.AudioEndpointVolume.Mute = true;
            }
        }

        public void UnMute()
        {
            var audioDevice = AudioDevice;

            if (audioDevice != null)
            {
                audioDevice.AudioEndpointVolume.Mute = false;
            }
        }

        public void ToggleMute()
        {
            if (IsMuted)
            {
                UnMute();
            }
            else
            {
                Mute();
            }
        }

        public float Volume
        {
            get
            {
                var audioDevice = AudioDevice;

                if (audioDevice != null)
                {
                    return audioDevice.AudioEndpointVolume.MasterVolumeLevelScalar * 100;
                }

                return 0;
            }
        }

        public void SetVolume(float volume)
        {
            var audioDevice = AudioDevice;

            if (audioDevice != null)
            {
                audioDevice.AudioEndpointVolume.MasterVolumeLevelScalar = volume / 100;
            }
        }

        private void OnVolumeChanged()
        {
            if (_lastMuteValue != _audioDevice.AudioEndpointVolume.Mute)
            {
                _lastMuteValue = _audioDevice.AudioEndpointVolume.Mute;
                EventHelper.FireEventIfNotNull(_volumeChanged, this, EventArgs.Empty, _logger);
            }
            else if (!_lastVolumeValue.Equals(_audioDevice.AudioEndpointVolume.MasterVolumeLevelScalar))
            {
                _lastVolumeValue = _audioDevice.AudioEndpointVolume.MasterVolumeLevelScalar;
                EventHelper.FireEventIfNotNull(_volumeChanged, this, EventArgs.Empty, _logger);
            }
        }

        public void VolumeStepUp()
        {
            var audioDevice = AudioDevice;

            if (audioDevice != null)
            {
                audioDevice.AudioEndpointVolume.VolumeStepUp();
            }
        }

        public void VolumeStepDown()
        {
            var audioDevice = AudioDevice;

            if (audioDevice != null)
            {
                audioDevice.AudioEndpointVolume.VolumeStepDown();
            }
        }

        public void SetAudioStreamIndex(int audioStreamIndex)
        {
            var player  =MediaPlayers.Where(p =>(p.PlayState == PlayState.Playing || p.PlayState == PlayState.Paused) &&p.CanSetAudioStreamIndex).FirstOrDefault();
            if (player != null)
            {
                player.SetAudioStreamIndex(audioStreamIndex);
            }

        }

        public void NextAudioStream()
        {
            var player = MediaPlayers.Where(p => (p.PlayState == PlayState.Playing || p.PlayState == PlayState.Paused) && p.CanSetAudioStreamIndex).FirstOrDefault();
            if (player != null)
            {
                player.NextAudioStream();
            }

        }

        public void SetSubtitleStreamIndex(int subitleStreamIndex)
        {
            var player = MediaPlayers.Where(p => (p.PlayState == PlayState.Playing || p.PlayState == PlayState.Paused) && p.CanSetAudioStreamIndex).FirstOrDefault();
            if (player != null)
            {
                player.SetSubtitleStreamIndex(subitleStreamIndex);
            }

        }

        public void NextSubtitleStream()
        {
            var player = MediaPlayers.Where(p =>(p.PlayState == PlayState.Playing || p.PlayState == PlayState.Paused) && p.CanSetSubtitleStreamIndex).FirstOrDefault();
            if (player != null)
            {
                player.NextSubtitleStream();
            }

        }

        /// <summary>
        /// Determines whether this instance can play the specified item.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <returns><c>true</c> if this instance can play the specified item; otherwise, <c>false</c>.</returns>
        public bool CanPlay(BaseItemDto item)
        {
            if (item.IsFolder)
            {
                return false;
            }

            if (string.IsNullOrEmpty(item.MediaType))
            {
                return false;
            }

            if (item.LocationType == LocationType.Virtual)
            {
                return false;
            }
           

            if (item.PlayAccess != PlayAccess.Full)
            {
                return false;
            }

            PlayerConfiguration config;
            var player = GetPlayer(new[] { item }, true, out config);

            return player != null;
        }
    }
}
