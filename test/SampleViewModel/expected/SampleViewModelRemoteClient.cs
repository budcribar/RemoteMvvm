using CommunityToolkit.Mvvm.ComponentModel;
using Grpc.Net.Client;
using SampleApp.ViewModels.Protos;

public partial class SampleViewModelRemoteClient : ObservableObject
{
  public string Name { get; private set; }
  public int Count { get; private set; }
  public SampleViewModelRemoteClient(CounterService.CounterServiceClient client) {}
}
