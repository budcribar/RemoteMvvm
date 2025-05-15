using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PeakSWC.Mvvm.Remote;
using System;
using System.Threading.Tasks;
using System.Windows.Threading; // For DispatcherTimer if used for game loop/cooldowns

namespace MonsterClicker.ViewModels
{
    // Attribute to mark this ViewModel for gRPC remote generation
    [GenerateGrpcRemoteAttribute("MonsterClicker.Protos.Game", "GameService",
        ServerImplNamespace = "MonsterClicker.GrpcServices",
        ClientProxyNamespace = "MonsterClicker.RemoteClients")]
    public partial class GameViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _monsterName = "Grumpy Goblin";

        [ObservableProperty]
        private int _monsterMaxHealth = 100;

        [ObservableProperty]
        private int _monsterCurrentHealth = 100;

        [ObservableProperty]
        private int _playerDamage = 10;

        [ObservableProperty]
        private string _gameMessage = "Click the monster to attack!";

        [ObservableProperty]
        private bool _isMonsterDefeated;

        [ObservableProperty]
        private bool _canUseSpecialAttack = true;

        private DispatcherTimer? _specialAttackCooldownTimer;
        private const int SpecialAttackCooldownSeconds = 5;

        public GameViewModel()
        {
            ResetGame();
        }

        [RelayCommand]
        private void AttackMonster()
        {
            if (IsMonsterDefeated) return;

            MonsterCurrentHealth -= PlayerDamage;
            if (MonsterCurrentHealth <= 0)
            {
                MonsterCurrentHealth = 0;
                GameMessage = $"{MonsterName} defeated! Well done!";
                IsMonsterDefeated = true;
            }
            else
            {
                GameMessage = $"Hit {MonsterName} for {PlayerDamage} damage!";
            }
        }

        [RelayCommand(CanExecute = nameof(CanExecuteSpecialAttack))]
        private async Task SpecialAttackAsync()
        {
            if (IsMonsterDefeated || !CanUseSpecialAttack) return;

            CanUseSpecialAttack = false; // Disable button
            SpecialAttackCommand.NotifyCanExecuteChanged(); // Notify UI to update button state

            GameMessage = "Charging special attack...";
            await Task.Delay(750); // Simulate charge time

            int specialDamage = PlayerDamage * 3;
            MonsterCurrentHealth -= specialDamage;

            if (MonsterCurrentHealth <= 0)
            {
                MonsterCurrentHealth = 0;
                GameMessage = $"Critical Hit! {MonsterName} obliterated for {specialDamage} damage!";
                IsMonsterDefeated = true;
            }
            else
            {
                GameMessage = $"Special Attack hit {MonsterName} for {specialDamage} damage!";
            }

            // Start cooldown
            _specialAttackCooldownTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(SpecialAttackCooldownSeconds)
            };
            _specialAttackCooldownTimer.Tick += (s, e) =>
            {
                CanUseSpecialAttack = true;
                SpecialAttackCommand.NotifyCanExecuteChanged(); // Re-enable button
                _specialAttackCooldownTimer?.Stop();
                GameMessage = "Special Attack ready!";
            };
            _specialAttackCooldownTimer.Start();
        }

        private bool CanExecuteSpecialAttack() => CanUseSpecialAttack && !IsMonsterDefeated;

        [RelayCommand]
        private void ResetGame()
        {
            MonsterName = "Grumpy Goblin"; // Or randomize
            MonsterMaxHealth = 100;
            MonsterCurrentHealth = MonsterMaxHealth;
            PlayerDamage = 10;
            GameMessage = "A new monster appears! Click it!";
            IsMonsterDefeated = false;

            _specialAttackCooldownTimer?.Stop();
            CanUseSpecialAttack = true;
            SpecialAttackCommand?.NotifyCanExecuteChanged(); // Ensure command state is updated
        }
    }
}
