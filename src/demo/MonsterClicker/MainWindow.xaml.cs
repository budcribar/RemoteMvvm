using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.ComponentModel;

public static class AppModeUtil
{
    public enum AppMode
    {
        Local,
        Server,
        Client
    }
}

namespace MonsterClicker
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        public bool IsServerMode => _mode == AppModeUtil.AppMode.Server;
        public bool IsClientMode => _mode == AppModeUtil.AppMode.Client;
        public bool IsFooterVisible => _mode != AppModeUtil.AppMode.Local;
        private AppModeUtil.AppMode _mode;

        public MainWindow(AppModeUtil.AppMode mode = AppModeUtil.AppMode.Local)
        {
            _mode = mode;
            InitializeComponent();
            this.DataContextChanged += MainWindow_DataContextChanged;
            if (IsServerMode)
            {
               GameViewModelGrpcServiceImpl.ClientCountChanged += OnClientCountChanged;
                // Set initial value on startup
                ConnectedClients = GameViewModelGrpcServiceImpl.ClientCount;
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            if (IsServerMode)
                GameViewModelGrpcServiceImpl.ClientCountChanged -= OnClientCountChanged;
        }

        public int ConnectedClients
        {
            get => _connectedClients;
            set
            {
                if (_connectedClients != value)
                {
                    _connectedClients = value;
                    OnPropertyChanged(nameof(ConnectedClients));
                }
            }
        }
        private int _connectedClients;

        public string ConnectionStatus
        {
            get
            {
                var prop = DataContext?.GetType().GetProperty("ConnectionStatus");
                if (prop != null)
                {
                    return prop.GetValue(DataContext)?.ToString() ?? "Unknown";
                }
                return IsClientMode ? "Unknown" : "Local";
            }
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            SetFooterVisibility();
        }

        private void SetFooterVisibility()
        {
            if (this.FindName("ServerStatusBarItem") is System.Windows.FrameworkElement serverItem)
                serverItem.Visibility = IsServerMode ? Visibility.Visible : Visibility.Collapsed;
            if (this.FindName("ClientStatusBarItem") is System.Windows.FrameworkElement clientItem)
                clientItem.Visibility = IsClientMode ? Visibility.Visible : Visibility.Collapsed;
        }

        private void MainWindow_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            OnPropertyChanged(nameof(ConnectionStatus));
            OnPropertyChanged(nameof(ConnectedClients));
            SetFooterVisibility();
        }

        private void OnClientCountChanged(object? sender, int count)
        {
            Dispatcher.Invoke(() => ConnectedClients = count);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
