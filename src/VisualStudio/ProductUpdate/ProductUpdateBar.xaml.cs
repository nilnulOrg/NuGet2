﻿using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace NuGet.VisualStudio
{
    public partial class ProductUpdateBar : UserControl
    {
        private readonly IProductUpdateService _productUpdateService;

        public event EventHandler UpdateStarting = delegate { };

        public ProductUpdateBar(IProductUpdateService productUpdateService)
        {
            InitializeComponent();

            if (productUpdateService == null)
            {
                throw new ArgumentNullException("productUpdateService");
            }

            _productUpdateService = productUpdateService;
            _productUpdateService.UpdateAvailable += OnUpdateAvailable;
        }

        private void OnUpdateAvailable(object sender, ProductUpdateAvailableEventArgs e)
        {
            // this event handler will be invoked on background thread. Has to use Dispatcher to show update bar.
            Dispatcher.BeginInvoke(new Action<Version, Version>(ShowUpdateBar), e.CurrentVersion, e.NewVersion);
        }

        private void OnUpdateLinkClick(object sender, RoutedEventArgs e)
        {
            HideUpdateBar();

            UpdateStarting(this, EventArgs.Empty);

            // invoke with priority as Background so that our window is closed first before the Update method is called.
            Dispatcher.BeginInvoke(new Action(_productUpdateService.Update), DispatcherPriority.Background);
        }

        private void OnDeclineUpdateLinkClick(object sender, RoutedEventArgs e)
        {
            HideUpdateBar();
            _productUpdateService.DeclineUpdate(false);
        }

        private void OnDeclineUpdateLinkClickNoRemind(object sender, RoutedEventArgs e)
        {
            HideUpdateBar();
            _productUpdateService.DeclineUpdate(true);
        }

        public void ShowUpdateBar(Version currentVersion, Version newVersion)
        {
            if (IsVisible)
            {
                UpdateBar.Visibility = Visibility.Visible;
            }
        }

        private void HideUpdateBar()
        {
            UpdateBar.Visibility = Visibility.Collapsed;
        }
    }
}