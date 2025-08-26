using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Generated.ViewModels
{
    public partial class TestViewModel : ObservableObject 
    { 
        public TestViewModel() 
        {
            ScoreList.Add(100);
            ScoreList.Add(200);
            ScoreList.Add(300);
            PlayerLevel = 15;
            HasBonus = true;
            BonusMultiplier = 2.5; // Will be converted to 2.5 as a double
            Status = GameStatus.Playing;
        }

        [ObservableProperty]
        private ObservableCollection<int> _scoreList = new();
        
        [ObservableProperty]
        private int _playerLevel = 1;

        [ObservableProperty]
        private bool _hasBonus = true;

        [ObservableProperty]
        private double _bonusMultiplier = 1.0;

        [ObservableProperty]
        private GameStatus _status = GameStatus.Menu;
    }

    public enum GameStatus
    {
        Menu = 10,
        Playing = 20, 
        Paused = 30,
        GameOver = 40
    }
}