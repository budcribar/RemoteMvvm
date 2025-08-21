// Auto-generated TypeScript client for PointerViewModel
import { PointerViewModelServiceClient } from './generated/PointerViewModelServiceServiceClientPb';
import { PointerViewModelState, UpdatePropertyValueRequest, SubscribeRequest, PropertyChangeNotification, ConnectionStatusResponse, ConnectionStatus, InitializeRequest, OnCursorTestRequest, OnClickTestRequest, OnSelectDeviceRequest, OnSelectNumButtonsRequest, GetClicksWithoutNotificationRequest, ResetClicksRequest, CancelTestRequest, FinishTestRequest } from './generated/PointerViewModelService_pb.js';
import * as grpcWeb from 'grpc-web';
import { Empty } from 'google-protobuf/google/protobuf/empty_pb';
import { Any } from 'google-protobuf/google/protobuf/any_pb';
import { StringValue, Int32Value, BoolValue, DoubleValue } from 'google-protobuf/google/protobuf/wrappers_pb';

export class PointerViewModelRemoteClient {
    private readonly grpcClient: PointerViewModelServiceClient;
    private propertyStream?: grpcWeb.ClientReadableStream<PropertyChangeNotification>;
    private pingIntervalId?: any;
    private changeCallbacks: Array<() => void> = [];

    show: any;
    showSpinner: any;
    clicksToPass: any;
    is3Btn: any;
    testTimeoutSec: any;
    instructions: any;
    showCursorTest: any;
    showConfigSelection: any;
    showClickInstructions: any;
    showTimer: any;
    showBottom: any;
    timerText: any;
    selectedDevice: any;
    lastClickCount: any;
    connectionStatus: string = 'Unknown';

    addChangeListener(cb: () => void): void {
        this.changeCallbacks.push(cb);
    }

    private notifyChange(): void {
        this.changeCallbacks.forEach(cb => cb());
    }

    constructor(grpcClient: PointerViewModelServiceClient) {
        this.grpcClient = grpcClient;
    }

    async initializeRemote(): Promise<void> {
        const state = await this.grpcClient.getState(new Empty());
        this.show = (state as any).getShow();
        this.showSpinner = (state as any).getShowSpinner();
        this.clicksToPass = (state as any).getClicksToPass();
        this.is3Btn = (state as any).getIs3Btn();
        this.testTimeoutSec = (state as any).getTestTimeoutSec();
        this.instructions = (state as any).getInstructions();
        this.showCursorTest = (state as any).getShowCursorTest();
        this.showConfigSelection = (state as any).getShowConfigSelection();
        this.showClickInstructions = (state as any).getShowClickInstructions();
        this.showTimer = (state as any).getShowTimer();
        this.showBottom = (state as any).getShowBottom();
        this.timerText = (state as any).getTimerText();
        this.selectedDevice = (state as any).getSelectedDevice();
        this.lastClickCount = (state as any).getLastClickCount();
        this.connectionStatus = 'Connected';
        this.notifyChange();
        this.startListeningToPropertyChanges();
        this.startPingLoop();
    }

    async refreshState(): Promise<void> {
        const state = await this.grpcClient.getState(new Empty());
        this.show = (state as any).getShow();
        this.showSpinner = (state as any).getShowSpinner();
        this.clicksToPass = (state as any).getClicksToPass();
        this.is3Btn = (state as any).getIs3Btn();
        this.testTimeoutSec = (state as any).getTestTimeoutSec();
        this.instructions = (state as any).getInstructions();
        this.showCursorTest = (state as any).getShowCursorTest();
        this.showConfigSelection = (state as any).getShowConfigSelection();
        this.showClickInstructions = (state as any).getShowClickInstructions();
        this.showTimer = (state as any).getShowTimer();
        this.showBottom = (state as any).getShowBottom();
        this.timerText = (state as any).getTimerText();
        this.selectedDevice = (state as any).getSelectedDevice();
        this.lastClickCount = (state as any).getLastClickCount();
        this.notifyChange();
    }

    async updatePropertyValue(propertyName: string, value: any): Promise<void> {
        const req = new UpdatePropertyValueRequest();
        req.setPropertyName(propertyName);
        req.setNewValue(this.createAnyValue(value));
        await this.grpcClient.updatePropertyValue(req);
    }

    private createAnyValue(value: any): Any {
        const anyVal = new Any();
        if (typeof value === 'string') {
            const wrapper = new StringValue();
            wrapper.setValue(value);
            anyVal.pack(wrapper.serializeBinary(), 'google.protobuf.StringValue');
        } else if (typeof value === 'number') {
            if (Number.isInteger(value)) {
                const wrapper = new Int32Value();
                wrapper.setValue(value);
                anyVal.pack(wrapper.serializeBinary(), 'google.protobuf.Int32Value');
            } else {
                const wrapper = new DoubleValue();
                wrapper.setValue(value);
                anyVal.pack(wrapper.serializeBinary(), 'google.protobuf.DoubleValue');
            }
        } else if (typeof value === 'boolean') {
            const wrapper = new BoolValue();
            wrapper.setValue(value);
            anyVal.pack(wrapper.serializeBinary(), 'google.protobuf.BoolValue');
        } else {
            throw new Error('Unsupported value type');
        }
        return anyVal;
    }

    async initialize(): Promise<void> {
        const req = new InitializeRequest();
        await this.grpcClient.initialize(req);
    }
    async onCursorTest(): Promise<void> {
        const req = new OnCursorTestRequest();
        await this.grpcClient.onCursorTest(req);
    }
    async onClickTest(button: any): Promise<void> {
        const req = new OnClickTestRequest();
        req.setButton(button);
        await this.grpcClient.onClickTest(req);
    }
    async onSelectDevice(device: any): Promise<void> {
        const req = new OnSelectDeviceRequest();
        req.setDevice(device);
        await this.grpcClient.onSelectDevice(req);
    }
    async onSelectNumButtons(btnCount: any): Promise<void> {
        const req = new OnSelectNumButtonsRequest();
        req.setBtnCount(btnCount);
        await this.grpcClient.onSelectNumButtons(req);
    }
    async getClicksWithoutNotification(button: any): Promise<void> {
        const req = new GetClicksWithoutNotificationRequest();
        req.setButton(button);
        await this.grpcClient.getClicksWithoutNotification(req);
    }
    async resetClicks(): Promise<void> {
        const req = new ResetClicksRequest();
        await this.grpcClient.resetClicks(req);
    }
    async cancelTest(): Promise<void> {
        const req = new CancelTestRequest();
        await this.grpcClient.cancelTest(req);
    }
    async finishTest(): Promise<void> {
        const req = new FinishTestRequest();
        await this.grpcClient.finishTest(req);
    }

    private startPingLoop(): void {
        if (this.pingIntervalId) return;
        this.pingIntervalId = setInterval(async () => {
            try {
                const resp: ConnectionStatusResponse = await this.grpcClient.ping(new Empty());
                if (resp.getStatus() === ConnectionStatus.CONNECTED) {
                    if (this.connectionStatus !== 'Connected') {
                        await this.refreshState();
                    }
                    this.connectionStatus = 'Connected';
                } else {
                    this.connectionStatus = 'Disconnected';
                }
            } catch {
                this.connectionStatus = 'Disconnected';
            }
            this.notifyChange();
        }, 5000);
    }

    private startListeningToPropertyChanges(): void {
        const req = new SubscribeRequest();
        req.setClientId(Math.random().toString());
        this.propertyStream = this.grpcClient.subscribeToPropertyChanges(req);
        this.propertyStream.on('data', (update: PropertyChangeNotification) => {
            const anyVal = update.getNewValue();
            switch (update.getPropertyName()) {
                case 'Show':
                    this.show = anyVal?.unpack(BoolValue.deserializeBinary, 'google.protobuf.BoolValue')?.getValue();
                    break;
                case 'ShowSpinner':
                    this.showSpinner = anyVal?.unpack(BoolValue.deserializeBinary, 'google.protobuf.BoolValue')?.getValue();
                    break;
                case 'ClicksToPass':
                    this.clicksToPass = anyVal?.unpack(Int32Value.deserializeBinary, 'google.protobuf.Int32Value')?.getValue();
                    break;
                case 'Is3Btn':
                    this.is3Btn = anyVal?.unpack(BoolValue.deserializeBinary, 'google.protobuf.BoolValue')?.getValue();
                    break;
                case 'TestTimeoutSec':
                    this.testTimeoutSec = anyVal?.unpack(Int32Value.deserializeBinary, 'google.protobuf.Int32Value')?.getValue();
                    break;
                case 'Instructions':
                    this.instructions = anyVal?.unpack(StringValue.deserializeBinary, 'google.protobuf.StringValue')?.getValue();
                    break;
                case 'ShowCursorTest':
                    this.showCursorTest = anyVal?.unpack(BoolValue.deserializeBinary, 'google.protobuf.BoolValue')?.getValue();
                    break;
                case 'ShowConfigSelection':
                    this.showConfigSelection = anyVal?.unpack(BoolValue.deserializeBinary, 'google.protobuf.BoolValue')?.getValue();
                    break;
                case 'ShowClickInstructions':
                    this.showClickInstructions = anyVal?.unpack(BoolValue.deserializeBinary, 'google.protobuf.BoolValue')?.getValue();
                    break;
                case 'ShowTimer':
                    this.showTimer = anyVal?.unpack(BoolValue.deserializeBinary, 'google.protobuf.BoolValue')?.getValue();
                    break;
                case 'ShowBottom':
                    this.showBottom = anyVal?.unpack(BoolValue.deserializeBinary, 'google.protobuf.BoolValue')?.getValue();
                    break;
                case 'TimerText':
                    this.timerText = anyVal?.unpack(StringValue.deserializeBinary, 'google.protobuf.StringValue')?.getValue();
                    break;
                case 'SelectedDevice':
                    this.selectedDevice = anyVal?.unpack(StringValue.deserializeBinary, 'google.protobuf.StringValue')?.getValue();
                    break;
                case 'LastClickCount':
                    this.lastClickCount = anyVal?.unpack(Int32Value.deserializeBinary, 'google.protobuf.Int32Value')?.getValue();
                    break;
            }
            this.notifyChange();
        });
        this.propertyStream.on('error', () => {
            this.propertyStream = undefined;
            setTimeout(() => this.startListeningToPropertyChanges(), 1000);
        });
        this.propertyStream.on('end', () => {
            this.propertyStream = undefined;
            setTimeout(() => this.startListeningToPropertyChanges(), 1000);
        });
    }

    dispose(): void {
        if (this.propertyStream) {
            this.propertyStream.cancel();
            this.propertyStream = undefined;
        }
        if (this.pingIntervalId) {
            clearInterval(this.pingIntervalId);
            this.pingIntervalId = undefined;
        }
    }
}
