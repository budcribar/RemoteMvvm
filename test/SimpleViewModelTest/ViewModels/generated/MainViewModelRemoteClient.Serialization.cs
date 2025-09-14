using System.IO;
using System.Linq;
using Generated.Protos;
using SimpleViewModelTest.ViewModels;

namespace SimpleViewModelTest.ViewModels.RemoteClients;

public partial class MainViewModelRemoteClient
{
    public void SaveToFile(string path)
    {
        var state = new MainViewModelState();
        if (Devices != null)
            state.Devices.AddRange(Devices.Where(d => d != null).Select(ProtoStateConverters.ToProto).Where(s => s != null));
        using var fs = File.Create(path);
        state.WriteTo(fs);
    }

    public void LoadFromFile(string path)
    {
        using var fs = File.OpenRead(path);
        var state = MainViewModelState.Parser.ParseFrom(fs);
        _suppressLocalUpdates = true;
        Devices = state.Devices.Select(ProtoStateConverters.FromProto).ToList();
        _suppressLocalUpdates = false;
        UpdatePropertyValueAsync(nameof(Devices), Devices).GetAwaiter().GetResult();
    }
}
