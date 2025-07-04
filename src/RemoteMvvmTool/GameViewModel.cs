using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PeakSWC.Mvvm.Remote; // Your custom attribute's namespace
using System;
using System.Threading.Tasks;
// using System.Windows; // No longer directly using WPF dispatcher here

namespace MonsterClicker.ViewModels
{
    [GenerateGrpcRemoteAttribute("MonsterClicker.ViewModels.Protos","GameViewModelService",
            ServerImplNamespace = "MonsterClicker.GrpcServices",
            ClientProxyNamespace = "MonsterClicker.RemoteClients")]
    public partial class GameViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _monsterName = "Grumpy Goblin";

        [ObservableProperty]
        private int _monsterMaxHealth = 100;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(AttackMonsterCommand))] // Re-evaluate AttackMonster when health changes
        [NotifyCanExecuteChangedFor(nameof(SpecialAttackCommand))] // Re-evaluate SpecialAttack when health changes
        private int _monsterCurrentHealth = 100;

        [ObservableProperty]
        private int _playerDamage = 10;

        [ObservableProperty]
        private string _gameMessage = "Click the monster to attack!";

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(AttackMonsterCommand))] // Re-evaluate AttackMonster
        [NotifyCanExecuteChangedFor(nameof(SpecialAttackCommand))] // Re-evaluate SpecialAttack
        private bool _isMonsterDefeated;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SpecialAttackCommand))]
        private bool _canUseSpecialAttack = true;

        private const int SpecialAttackCooldownSeconds = 5;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SpecialAttackCommand))]
        private bool _isSpecialAttackOnCooldown = false;

        public GameViewModel()
        {
            // ResetGame will set initial values and trigger notifications
            ResetGame();
        }

        private bool CanAttackMonster() => !IsMonsterDefeated;

        [RelayCommand(CanExecute = nameof(CanAttackMonster))]
        private void AttackMonster()
        {
            // CanExecute guard is usually sufficient
            // if (IsMonsterDefeated) return; 

            MonsterCurrentHealth -= PlayerDamage; // Setter will trigger PropertyChanged & CanExecuteChanged
            if (MonsterCurrentHealth <= 0)
            {
                MonsterCurrentHealth = 0;
                GameMessage = $"{MonsterName} defeated! Well done!";
                IsMonsterDefeated = true; // Setter will trigger PropertyChanged & CanExecuteChanged
            }
            else
            {
                GameMessage = $"Hit {MonsterName} for {PlayerDamage} damage!";
            }
        }

        private bool CanExecuteSpecialAttack() => CanUseSpecialAttack && !IsMonsterDefeated && !IsSpecialAttackOnCooldown;

        [RelayCommand(CanExecute = nameof(CanExecuteSpecialAttack))]
        private async Task SpecialAttackAsync()
        {
            // if (!CanExecuteSpecialAttack()) return; // Guarded by CanExecute

            IsSpecialAttackOnCooldown = true; // Setter will trigger PropertyChanged & CanExecuteChanged

            try
            {
                GameMessage = "Charging special attack...";
                await Task.Delay(750);

                int specialDamage = PlayerDamage * 3;
                MonsterCurrentHealth -= specialDamage; // Setter will trigger PropertyChanged & CanExecuteChanged

                if (MonsterCurrentHealth <= 0)
                {
                    MonsterCurrentHealth = 0;
                    GameMessage = $"Critical Hit! {MonsterName} obliterated for {specialDamage} damage!";
                    IsMonsterDefeated = true; // Setter will trigger PropertyChanged & CanExecuteChanged
                }
                else
                {
                    GameMessage = $"Special Attack hit {MonsterName} for {specialDamage} damage!";
                }

                GameMessage = $"Special Attack on cooldown for {SpecialAttackCooldownSeconds} seconds...";
                await Task.Delay(TimeSpan.FromSeconds(SpecialAttackCooldownSeconds));
            }
            finally
            {
                IsSpecialAttackOnCooldown = false; // Setter will trigger PropertyChanged & CanExecuteChanged
                GameMessage = "Special Attack ready!";
            }
        }


        [RelayCommand]
        private void ResetGame()
        {
            MonsterName = "Grumpy Goblin";
            MonsterMaxHealth = 100;
            // Set properties which will trigger notifications and CanExecute updates
            MonsterCurrentHealth = MonsterMaxHealth;
            PlayerDamage = 10;
            GameMessage = "A new monster appears! Click it!";
            IsMonsterDefeated = false;
            IsSpecialAttackOnCooldown = false;
            CanUseSpecialAttack = true;
        }
    }
}
