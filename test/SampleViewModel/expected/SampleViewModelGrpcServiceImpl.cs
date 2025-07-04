using Grpc.Core;
using SampleApp.ViewModels.Protos;
using Google.Protobuf.WellKnownTypes;

public class SampleViewModelGrpcServiceImpl : CounterService.CounterServiceBase
{
  private readonly SampleViewModel _vm;
  public SampleViewModelGrpcServiceImpl(SampleViewModel vm) => _vm = vm;
  public override Task<SampleViewModelState> GetState(Empty request, ServerCallContext context)
  {
    var state = new SampleViewModelState();
    state.Name = _vm.Name;
    state.Count = _vm.Count;
    return Task.FromResult(state);
  }
}
