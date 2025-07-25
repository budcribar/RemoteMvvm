import { PointerViewModelServiceClient } from './generated/PointerViewModelServiceServiceClientPb';
import { PointerViewModelRemoteClient } from './PointerViewModelRemoteClient';

const grpcHost = 'http://localhost:50052';
const grpcClient = new PointerViewModelServiceClient(grpcHost);
const vm = new PointerViewModelRemoteClient(grpcClient);

async function render() {
    (document.getElementById('show') as HTMLInputElement).value = vm.show;
    (document.getElementById('showSpinner') as HTMLInputElement).value = vm.showSpinner;
    (document.getElementById('clicksToPass') as HTMLInputElement).value = vm.clicksToPass;
    (document.getElementById('is3Btn') as HTMLInputElement).value = vm.is3Btn;
    (document.getElementById('testTimeoutSec') as HTMLInputElement).value = vm.testTimeoutSec;
    (document.getElementById('instructions') as HTMLInputElement).value = vm.instructions;
    (document.getElementById('showCursorTest') as HTMLInputElement).value = vm.showCursorTest;
    (document.getElementById('showConfigSelection') as HTMLInputElement).value = vm.showConfigSelection;
    (document.getElementById('showClickInstructions') as HTMLInputElement).value = vm.showClickInstructions;
    (document.getElementById('showTimer') as HTMLInputElement).value = vm.showTimer;
    (document.getElementById('showBottom') as HTMLInputElement).value = vm.showBottom;
    (document.getElementById('timerText') as HTMLInputElement).value = vm.timerText;
    (document.getElementById('selectedDevice') as HTMLInputElement).value = vm.selectedDevice;
    (document.getElementById('lastClickCount') as HTMLInputElement).value = vm.lastClickCount;
    (document.getElementById('connection-status') as HTMLElement).textContent = vm.connectionStatus;
}

async function init() {
    await vm.initializeRemote();
    vm.addChangeListener(render);
    await render();
}

document.addEventListener('DOMContentLoaded', () => {
    init();
    (document.getElementById('show') as HTMLInputElement).addEventListener('change', async () => {
        await vm.updatePropertyValue('Show', (document.getElementById('show') as HTMLInputElement).value);
    });
    (document.getElementById('showSpinner') as HTMLInputElement).addEventListener('change', async () => {
        await vm.updatePropertyValue('ShowSpinner', (document.getElementById('showSpinner') as HTMLInputElement).value);
    });
    (document.getElementById('clicksToPass') as HTMLInputElement).addEventListener('change', async () => {
        await vm.updatePropertyValue('ClicksToPass', (document.getElementById('clicksToPass') as HTMLInputElement).value);
    });
    (document.getElementById('is3Btn') as HTMLInputElement).addEventListener('change', async () => {
        await vm.updatePropertyValue('Is3Btn', (document.getElementById('is3Btn') as HTMLInputElement).value);
    });
    (document.getElementById('testTimeoutSec') as HTMLInputElement).addEventListener('change', async () => {
        await vm.updatePropertyValue('TestTimeoutSec', (document.getElementById('testTimeoutSec') as HTMLInputElement).value);
    });
    (document.getElementById('instructions') as HTMLInputElement).addEventListener('change', async () => {
        await vm.updatePropertyValue('Instructions', (document.getElementById('instructions') as HTMLInputElement).value);
    });
    (document.getElementById('showCursorTest') as HTMLInputElement).addEventListener('change', async () => {
        await vm.updatePropertyValue('ShowCursorTest', (document.getElementById('showCursorTest') as HTMLInputElement).value);
    });
    (document.getElementById('showConfigSelection') as HTMLInputElement).addEventListener('change', async () => {
        await vm.updatePropertyValue('ShowConfigSelection', (document.getElementById('showConfigSelection') as HTMLInputElement).value);
    });
    (document.getElementById('showClickInstructions') as HTMLInputElement).addEventListener('change', async () => {
        await vm.updatePropertyValue('ShowClickInstructions', (document.getElementById('showClickInstructions') as HTMLInputElement).value);
    });
    (document.getElementById('showTimer') as HTMLInputElement).addEventListener('change', async () => {
        await vm.updatePropertyValue('ShowTimer', (document.getElementById('showTimer') as HTMLInputElement).value);
    });
    (document.getElementById('showBottom') as HTMLInputElement).addEventListener('change', async () => {
        await vm.updatePropertyValue('ShowBottom', (document.getElementById('showBottom') as HTMLInputElement).value);
    });
    (document.getElementById('timerText') as HTMLInputElement).addEventListener('change', async () => {
        await vm.updatePropertyValue('TimerText', (document.getElementById('timerText') as HTMLInputElement).value);
    });
    (document.getElementById('selectedDevice') as HTMLInputElement).addEventListener('change', async () => {
        await vm.updatePropertyValue('SelectedDevice', (document.getElementById('selectedDevice') as HTMLInputElement).value);
    });
    (document.getElementById('lastClickCount') as HTMLInputElement).addEventListener('change', async () => {
        await vm.updatePropertyValue('LastClickCount', (document.getElementById('lastClickCount') as HTMLInputElement).value);
    });
    (document.getElementById('initialize-btn') as HTMLButtonElement).addEventListener('click', async () => {
        await vm.initialize();
    });
    (document.getElementById('onCursorTest-btn') as HTMLButtonElement).addEventListener('click', async () => {
        await vm.onCursorTest();
    });
    (document.getElementById('onClickTest-btn') as HTMLButtonElement).addEventListener('click', async () => {
        await vm.onClickTest(1);
    });
    (document.getElementById('onSelectDevice-btn') as HTMLButtonElement).addEventListener('click', async () => {
        await vm.onSelectDevice('sample');
    });
    (document.getElementById('onSelectNumButtons-btn') as HTMLButtonElement).addEventListener('click', async () => {
        await vm.onSelectNumButtons(3);
    });
    (document.getElementById('getClicksWithoutNotification-btn') as HTMLButtonElement).addEventListener('click', async () => {
        await vm.getClicksWithoutNotification('sample');
    });
    (document.getElementById('resetClicks-btn') as HTMLButtonElement).addEventListener('click', async () => {
        await vm.resetClicks();
    });
    (document.getElementById('cancelTest-btn') as HTMLButtonElement).addEventListener('click', async () => {
        await vm.cancelTest();
    });
    (document.getElementById('finishTest-btn') as HTMLButtonElement).addEventListener('click', async () => {
        await vm.finishTest();
    });
});
