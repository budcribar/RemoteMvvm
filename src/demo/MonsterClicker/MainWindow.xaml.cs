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

namespace MonsterClicker
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        public MainWindow()
        {
            InitializeComponent();
            this.DataContextChanged += MainWindow_DataContextChanged;
        }

        public bool IsClientMode
        {
            get
            {
                // DataContext is set to GameViewModelRemoteClient in client mode
                return DataContext?.GetType().Name == "GameViewModelRemoteClient";
            }
        }

        public string ConnectionStatus
        {
            get
            {
                // If DataContext has a ConnectionStatus property, return it
                var prop = DataContext?.GetType().GetProperty("ConnectionStatus");
                if (prop != null)
                {
                    return prop.GetValue(DataContext)?.ToString() ?? "Unknown";
                }
                return IsClientMode ? "Unknown" : "Local";
            }
        }

        private void MainWindow_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is INotifyPropertyChanged oldInpc)
                oldInpc.PropertyChanged -= DataContext_PropertyChanged;
            if (e.NewValue is INotifyPropertyChanged newInpc)
                newInpc.PropertyChanged += DataContext_PropertyChanged;
            // Notify UI to update ConnectionStatus
            OnPropertyChanged(nameof(ConnectionStatus));
        }

        private void DataContext_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "ConnectionStatus")
            {
                OnPropertyChanged(nameof(ConnectionStatus));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
