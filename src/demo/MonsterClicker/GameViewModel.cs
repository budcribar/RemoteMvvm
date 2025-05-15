using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PeakSWC.Mvvm.Remote;
using System;
using System.Threading.Tasks;

namespace MonsterClicker.ViewModels
{
    // Attribute to mark this ViewModel for gRPC remote generation
    // CORRECTED ARGUMENTS:
    // 1st: The C# namespace where Grpc.Tools will place generated types (from proto's csharp_namespace option)
    // 2nd: The service name as defined in the .proto file
    [GenerateGrpcRemote("MonsterClicker.ViewModels.Protos", "GameViewModelService",
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
        [NotifyCanExecuteChangedFor(nameof(SpecialAttackCommand))]
        private bool _canUseSpecialAttack = true;

        private const int SpecialAttackCooldownSeconds = 5;
        private bool _isSpecialAttackOnCooldown = false;

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

        private bool CanExecuteSpecialAttack() => CanUseSpecialAttack && !IsMonsterDefeated && !_isSpecialAttackOnCooldown;

        [RelayCommand(CanExecute = nameof(CanExecuteSpecialAttack))]
        private async Task SpecialAttackAsync() // Parameter removed in previous step
        {
            if (IsMonsterDefeated || _isSpecialAttackOnCooldown) return;

            _isSpecialAttackOnCooldown = true;
            SpecialAttackCommand.NotifyCanExecuteChanged();

            GameMessage = "Charging special attack...";
            await Task.Delay(750);

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

            GameMessage = $"Special Attack on cooldown for {SpecialAttackCooldownSeconds} seconds...";
            await Task.Delay(TimeSpan.FromSeconds(SpecialAttackCooldownSeconds));

            _isSpecialAttackOnCooldown = false;
            SpecialAttackCommand.NotifyCanExecuteChanged();
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

            _isSpecialAttackOnCooldown = false;
            CanUseSpecialAttack = true;
        }
    }
}
