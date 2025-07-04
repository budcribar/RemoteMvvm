using Grpc.Core;
using MonsterClicker.ViewModels.Protos;
using Google.Protobuf.WellKnownTypes;

public class GameViewModelGrpcServiceImpl : GameViewModelService.GameViewModelServiceBase
{
  private readonly GameViewModel _vm;
  public GameViewModelGrpcServiceImpl(GameViewModel vm) => _vm = vm;
  public override Task<GameViewModelState> GetState(Empty request, ServerCallContext context)
  {
    var state = new GameViewModelState();
    state.MonsterName = _vm.MonsterName;
    state.MonsterMaxHealth = _vm.MonsterMaxHealth;
    state.MonsterCurrentHealth = _vm.MonsterCurrentHealth;
    state.PlayerDamage = _vm.PlayerDamage;
    state.GameMessage = _vm.GameMessage;
    state.IsMonsterDefeated = _vm.IsMonsterDefeated;
    state.CanUseSpecialAttack = _vm.CanUseSpecialAttack;
    state.IsSpecialAttackOnCooldown = _vm.IsSpecialAttackOnCooldown;
    return Task.FromResult(state);
  }
}
