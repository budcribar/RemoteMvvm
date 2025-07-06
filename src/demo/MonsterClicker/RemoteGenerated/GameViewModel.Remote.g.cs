using Grpc.Core;
using Grpc.Net.Client;
using MonsterClicker.ViewModels.Protos;
using MonsterClicker.ViewModels.RemoteClients;
using System;
using System.Threading.Tasks;
using System.Windows.Threading;
using PeakSWC.Mvvm.Remote;

namespace MonsterClicker.ViewModels
{
    public partial class GameViewModel
    {
        private GameViewModelGrpcServiceImpl? _grpcService;
        private Grpc.Core.Server? _server;
        private GrpcChannel? _channel;
        private MonsterClicker.ViewModels.RemoteClients.GameViewModelRemoteClient? _remoteClient;

        public GameViewModel(ServerOptions options) : this()
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            _grpcService = new GameViewModelGrpcServiceImpl(this, Dispatcher.CurrentDispatcher);
            _server = new Grpc.Core.Server
            {
                Services = { GameViewModelService.BindService(_grpcService) },
                Ports = { new ServerPort("localhost", options.Port, ServerCredentials.Insecure) }
            };
            _server.Start();
        }

        public GameViewModel(ClientOptions options) : this()
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            _channel = GrpcChannel.ForAddress(options.Address);
            var client = new GameViewModelService.GameViewModelServiceClient(_channel);
            _remoteClient = new GameViewModelRemoteClient(client);
        }

        public async Task<GameViewModelRemoteClient> GetRemoteModel()
        {
            if (_remoteClient == null) throw new InvalidOperationException("Client options not provided");
            await _remoteClient.InitializeRemoteAsync();
            return _remoteClient;
        }
    }
}
