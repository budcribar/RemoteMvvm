using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Generated.ViewModels
{
    public partial class TestViewModel : ObservableObject 
    { 
        public TestViewModel() 
        {
            // Mix of different complex types that might interact poorly
            GameState = GameMode.Paused;
            Players = new ObservableCollection<Player>
            {
                new Player { Name = "Alice", Score = 1500.5f, Level = 15, IsActive = true }
            };
            
            Statistics = new Dictionary<StatType, List<double>>
            {
                { StatType.DamageDealt, new List<double> { 450.5, 623.2, 789.1 } },
                { StatType.HealingDone, new List<double> { 123.4, 234.5 } }
            };
            
            SessionId = Guid.Parse("00000000-0000-0000-0000-000000000222"); // Fixed GUID
            StartTime = new DateTime(121); // Fixed DateTime from ticks
            TotalSessions = 42;
        }

        [ObservableProperty]
        private GameMode _gameState = GameMode.Paused;

        [ObservableProperty]
        private ObservableCollection<Player> _players = new();

        [ObservableProperty]
        private Dictionary<StatType, List<double>> _statistics = new();

        [ObservableProperty]
        private Guid _sessionId;

        [ObservableProperty]
        private DateTime _startTime;

        [ObservableProperty]
        private int _totalSessions = 0;

        [RelayCommand]
        private void StartGame() => GameState = GameMode.Active;

        [RelayCommand]
        private async Task EndGameAsync()
        {
            await Task.Delay(100); // Simulate async work
            GameState = GameMode.Inactive;
        }

        [RelayCommand]
        private void AddPlayer(string? playerName)
        {
            if (!string.IsNullOrEmpty(playerName))
            {
                Players.Add(new Player { Name = playerName, Score = 0, Level = 1, IsActive = true });
            }
        }
    }

    public enum GameMode 
    { 
        Inactive = 0, 
        Active = 1, 
        Paused = 2 
    }

    public enum StatType 
    { 
        DamageDealt = 10, 
        HealingDone = 20 
    }

    public class Player
    {
        public string Name { get; set; } = "";
        public float Score { get; set; }
        public int Level { get; set; }
        public bool IsActive { get; set; }
    }
}