using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PeakSWC.Mvvm.Remote;
using System;
using System.Threading.Tasks;
// using System.Windows.Threading; // No longer needed for DispatcherTimer

namespace MonsterClicker.ViewModels
{
    // Attribute to mark this ViewModel for gRPC remote generation
    [GenerateGrpcRemote("MonsterClicker.Protos.Game", "GameService",
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
        [NotifyCanExecuteChangedFor(nameof(SpecialAttackCommand))] // Ensures CanExecute is re-evaluated when this changes
        private bool _canUseSpecialAttack = true;

        // private DispatcherTimer? _specialAttackCooldownTimer; // Removed
        private const int SpecialAttackCooldownSeconds = 5;
        private bool _isSpecialAttackOnCooldown = false; // New flag to manage cooldown state internally

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
                // SpecialAttackCommand.NotifyCanExecuteChanged(); // CanUseSpecialAttack will trigger this
            }
            else
            {
                GameMessage = $"Hit {MonsterName} for {PlayerDamage} damage!";
            }
        }

        // Updated CanExecute condition for SpecialAttackCommand
        private bool CanExecuteSpecialAttack() => CanUseSpecialAttack && !IsMonsterDefeated && !_isSpecialAttackOnCooldown;

        [RelayCommand(CanExecute = nameof(CanExecuteSpecialAttack))]
        private async Task SpecialAttackAsync()
        {
            // Redundant check, CanExecute should prevent this, but good for safety
            if (IsMonsterDefeated || _isSpecialAttackOnCooldown) return;

            _isSpecialAttackOnCooldown = true; // Set cooldown flag
            // CanUseSpecialAttack remains true, but CanExecuteSpecialAttack will now be false due to _isSpecialAttackOnCooldown
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
                // SpecialAttackCommand.NotifyCanExecuteChanged(); // CanUseSpecialAttack will trigger this
            }
            else
            {
                GameMessage = $"Special Attack hit {MonsterName} for {specialDamage} damage!";
            }

            // Start cooldown using Task.Delay
            GameMessage = $"Special Attack on cooldown for {SpecialAttackCooldownSeconds} seconds..."; // Update message
            await Task.Delay(TimeSpan.FromSeconds(SpecialAttackCooldownSeconds));

            _isSpecialAttackOnCooldown = false; // Reset cooldown flag
            // CanUseSpecialAttack is still true (unless changed by another mechanism)
            SpecialAttackCommand.NotifyCanExecuteChanged(); // Re-enable button
            GameMessage = "Special Attack ready!";
        }


        [RelayCommand]
        private void ResetGame()
        {
            MonsterName = "Grumpy Goblin";
            MonsterMaxHealth = 100;
            MonsterCurrentHealth = MonsterMaxHealth;
            PlayerDamage = 10;
            GameMessage = "A new monster appears! Click it!";
            IsMonsterDefeated = false;

            // _specialAttackCooldownTimer?.Stop(); // Removed
            _isSpecialAttackOnCooldown = false; // Reset cooldown state
            CanUseSpecialAttack = true; // Ensure it's true on reset
            // SpecialAttackCommand.NotifyCanExecuteChanged(); // CanUseSpecialAttack [NotifyCanExecuteChangedFor] handles this
        }
    }
}
