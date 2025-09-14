using System.IO;
using System.Linq;
using Generated.Protos;

namespace SimpleViewModelTest.ViewModels;

public partial class MainViewModel
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
        Devices = state.Devices.Select(ProtoStateConverters.FromProto).ToList();
    }
}
