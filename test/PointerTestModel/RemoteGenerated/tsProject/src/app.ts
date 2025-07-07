import { PointerViewModelServiceClient } from './generated/PointerViewModelServiceServiceClientPb';
import { PointerViewModelRemoteClient } from './PointerViewModelRemoteClient';

const grpcHost = 'http://localhost:50052';
const grpcClient = new PointerViewModelServiceClient(grpcHost);
const vm = new PointerViewModelRemoteClient(grpcClient);

async function render() {
    (document.getElementById('connection-status') as HTMLElement).textContent = vm.connectionStatus;
}

async function init() {
    await vm.initializeRemote();
    vm.addChangeListener(render);
    await render();
}

document.addEventListener('DOMContentLoaded', () => {
    init();
});
