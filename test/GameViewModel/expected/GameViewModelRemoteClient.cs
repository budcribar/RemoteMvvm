using CommunityToolkit.Mvvm.ComponentModel;
using Grpc.Net.Client;
using MonsterClicker.ViewModels.Protos;

public partial class GameViewModelRemoteClient : ObservableObject
{
  public string MonsterName { get; private set; }
  public int MonsterMaxHealth { get; private set; }
  public int MonsterCurrentHealth { get; private set; }
  public int PlayerDamage { get; private set; }
  public string GameMessage { get; private set; }
  public bool IsMonsterDefeated { get; private set; }
  public bool CanUseSpecialAttack { get; private set; }
  public bool IsSpecialAttackOnCooldown { get; private set; }
  public GameViewModelRemoteClient(GameViewModelService.GameViewModelServiceClient client) {}
}
