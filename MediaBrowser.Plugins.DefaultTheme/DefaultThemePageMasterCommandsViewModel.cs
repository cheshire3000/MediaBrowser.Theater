﻿using System;
using System.Windows.Input;
using System.Xml.Serialization;
using MediaBrowser.Common;
using MediaBrowser.Model.ApiClient;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Logging;
using MediaBrowser.Plugins.DefaultTheme.ListPage;
using MediaBrowser.Plugins.DefaultTheme.NowPlayingMenu;
using MediaBrowser.Plugins.DefaultTheme.UserProfileMenu;
using MediaBrowser.Theater.Interfaces;
using MediaBrowser.Theater.Interfaces.Playback;
using MediaBrowser.Theater.Interfaces.Presentation;
using MediaBrowser.Theater.Interfaces.ViewModels;
using MediaBrowser.Theater.Interfaces.Navigation;
using MediaBrowser.Theater.Interfaces.Session;
using MediaBrowser.Plugins.DefaultTheme.SystemOptionsMenu;
using MediaBrowser.Theater.Presentation.Pages;

namespace MediaBrowser.Plugins.DefaultTheme
{
    public class DefaultThemePageMasterCommandsViewModel : MasterCommandsViewModel
    {
        protected readonly IImageManager ImageManager;
        protected readonly IPlaybackManager PlaybackManager;

        public ICommand UserCommand { get; private set; }
        public ICommand LogoutCommand { get; private set; }
        public ICommand NowPlayingCommand { get; private set; }

        private bool _displayPreferencesEnabled;
        public bool DisplayPreferencesEnabled
        {
            get { return _displayPreferencesEnabled; }

            set
            {
                var changed = _displayPreferencesEnabled != value;

                _displayPreferencesEnabled = value;
                if (changed)
                {
                    OnPropertyChanged("DisplayPreferencesEnabled");
                }
            }
        }

        private bool _sortEnabled;
        public bool SortEnabled
        {
            get { return _sortEnabled; }

            set
            {
                var changed = _sortEnabled != value;

                _sortEnabled = value;
                if (changed)
                {
                    OnPropertyChanged("SortEnabled");
                }
            }
        }

        private bool _powerOptionsEnabled;
        public bool PowerOptionsEnabled
        {
            get { return _powerOptionsEnabled; }

            set
            {
                var changed = _powerOptionsEnabled != value;

                _powerOptionsEnabled = value;
                if (changed)
                {
                    OnPropertyChanged("PowerOptionsEnabled");
                }
            }
        }

        public DefaultThemePageMasterCommandsViewModel(INavigationService navigationService, ISessionManager sessionManager, IPresentationManager presentationManager, 
            IApiClient apiClient, ILogger logger, ITheaterApplicationHost appHost, IServerEvents serverEvents, IImageManager imageManager, IPlaybackManager playbackManager) 
            : base(navigationService, sessionManager, presentationManager, apiClient, logger, appHost, serverEvents)
        {
            ImageManager = imageManager;
            PlaybackManager = playbackManager;

            UserCommand = new RelayCommand(i => ShowUserMenu());
            LogoutCommand = new RelayCommand(i => Logout());
            NowPlayingCommand = new RelayCommand(i => ShowNowPlaying());

            PowerOptionsEnabled = true;
        }

        public virtual void ShowSystemOptions()
        {     
            var systemOptionsWindow = new SystemOptionsWindow(this);
            systemOptionsWindow.Closed += systemOptionsWindow_Closed;

            systemOptionsWindow.ShowModal(PresentationManager.Window);
        }

        protected virtual void ShowUserMenu()
        {
            var page = NavigationService.CurrentPage as IHasDisplayPreferences;
            DisplayPreferences displayPreferences = null;
            ListPageConfig options = null;
            if (page != null)
            {
                displayPreferences = page.GetDisplayPreferences();
                options = page.GetListPageConfig();
            }

            var userProfileWindow = new UserProfileWindow(this, SessionManager, PresentationManager, ImageManager,
                ApiClient, displayPreferences, options);
            userProfileWindow.Closed += userProfileWindow_Closed;

            userProfileWindow.ShowModal(PresentationManager.Window);
        }

        protected virtual void ShowNowPlaying()
        {
            var nowPlayingWindow = new NowPlayingWindow(PlaybackManager);
            nowPlayingWindow.ShowModal(PresentationManager.Window);
        }

        protected async void Logout()
        {
            if (SessionManager.CurrentUser == null)
            {
                throw new InvalidOperationException("The user is not logged in.");
            }

            await SessionManager.Logout();

            OnPageNavigated(this, new EventArgs());
        }

        protected override void Dispose(bool dispose)
        {
            if (dispose)
            {
                
            }

            base.Dispose(dispose);
        }

        //Try and re-focus off the top bar when the side bar has closed
        private void userProfileWindow_Closed(object sender, EventArgs e)
        {
            BasePage currentPage = NavigationService.CurrentPage as BasePage;

            if (currentPage != null)
            {
                currentPage.FocusOnFirstLoad();
            }
        }

        private void systemOptionsWindow_Closed(object sender, EventArgs e)
        {
            BasePage currentPage = NavigationService.CurrentPage as BasePage;

            if (currentPage != null)
            {
                currentPage.FocusOnFirstLoad();
            }
        }
    }
}
