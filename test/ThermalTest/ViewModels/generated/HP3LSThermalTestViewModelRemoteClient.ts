// Auto-generated TypeScript client for HP3LSThermalTestViewModel
import { HP3LSThermalTestViewModelServiceClient } from './generated/HP3LSThermalTestViewModelServiceServiceClientPb';
import { HP3LSThermalTestViewModelState, UpdatePropertyValueRequest, SubscribeRequest, PropertyChangeNotification, ConnectionStatusResponse, ConnectionStatus, StateChangedRequest, CancelTestRequest } from './generated/HP3LSThermalTestViewModelService_pb.js';
import * as grpcWeb from 'grpc-web';
import { Empty } from 'google-protobuf/google/protobuf/empty_pb';
import { Any } from 'google-protobuf/google/protobuf/any_pb';
import { StringValue, Int32Value, BoolValue } from 'google-protobuf/google/protobuf/wrappers_pb';

export class HP3LSThermalTestViewModelRemoteClient {
    private readonly grpcClient: HP3LSThermalTestViewModelServiceClient;
    private propertyStream?: grpcWeb.ClientReadableStream<PropertyChangeNotification>;
    private pingIntervalId?: any;
    private changeCallbacks: Array<() => void> = [];

    zone: any;
    isActive: any;
    deviceName: any;
    temperature: any;
    processorLoad: any;
    fanSpeed: any;
    secondsInState: any;
    firstSeenInState: any;
    progress: any;
    background: any;
    status: any;
    state: any;
    connectionStatus: string = 'Unknown';

    addChangeListener(cb: () => void): void {
        this.changeCallbacks.push(cb);
    }

    private notifyChange(): void {
        this.changeCallbacks.forEach(cb => cb());
    }

    constructor(grpcClient: HP3LSThermalTestViewModelServiceClient) {
        this.grpcClient = grpcClient;
    }

    async initializeRemote(): Promise<void> {
        const state = await this.grpcClient.getState(new Empty());
        this.zone = (state as any).getZone();
        this.isActive = (state as any).getIsActive();
        this.deviceName = (state as any).getDeviceName();
        this.temperature = (state as any).getTemperature();
        this.processorLoad = (state as any).getProcessorLoad();
        this.fanSpeed = (state as any).getFanSpeed();
        this.secondsInState = (state as any).getSecondsInState();
        this.firstSeenInState = (state as any).getFirstSeenInState();
        this.progress = (state as any).getProgress();
        this.background = (state as any).getBackground();
        this.status = (state as any).getStatus();
        this.state = (state as any).getState();
        this.connectionStatus = 'Connected';
        this.notifyChange();
        this.startListeningToPropertyChanges();
        this.startPingLoop();
    }

    async refreshState(): Promise<void> {
        const state = await this.grpcClient.getState(new Empty());
        this.zone = (state as any).getZone();
        this.isActive = (state as any).getIsActive();
        this.deviceName = (state as any).getDeviceName();
        this.temperature = (state as any).getTemperature();
        this.processorLoad = (state as any).getProcessorLoad();
        this.fanSpeed = (state as any).getFanSpeed();
        this.secondsInState = (state as any).getSecondsInState();
        this.firstSeenInState = (state as any).getFirstSeenInState();
        this.progress = (state as any).getProgress();
        this.background = (state as any).getBackground();
        this.status = (state as any).getStatus();
        this.state = (state as any).getState();
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
        } else if (typeof value === 'number' && Number.isInteger(value)) {
            const wrapper = new Int32Value();
            wrapper.setValue(value);
            anyVal.pack(wrapper.serializeBinary(), 'google.protobuf.Int32Value');
        } else if (typeof value === 'boolean') {
            const wrapper = new BoolValue();
            wrapper.setValue(value);
            anyVal.pack(wrapper.serializeBinary(), 'google.protobuf.BoolValue');
        } else {
            throw new Error('Unsupported value type');
        }
        return anyVal;
    }

    async stateChanged(state: any): Promise<void> {
        const req = new StateChangedRequest();
        req.setState(state);
        await this.grpcClient.stateChanged(req);
    }
    async cancelTest(): Promise<void> {
        const req = new CancelTestRequest();
        await this.grpcClient.cancelTest(req);
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
                case 'IsActive':
                    this.isActive = anyVal?.unpack(BoolValue.deserializeBinary, 'google.protobuf.BoolValue')?.getValue();
                    break;
                case 'DeviceName':
                    this.deviceName = anyVal?.unpack(StringValue.deserializeBinary, 'google.protobuf.StringValue')?.getValue();
                    break;
                case 'Temperature':
                    this.temperature = anyVal?.unpack(Int32Value.deserializeBinary, 'google.protobuf.Int32Value')?.getValue();
                    break;
                case 'ProcessorLoad':
                    this.processorLoad = anyVal?.unpack(Int32Value.deserializeBinary, 'google.protobuf.Int32Value')?.getValue();
                    break;
                case 'FanSpeed':
                    this.fanSpeed = anyVal?.unpack(Int32Value.deserializeBinary, 'google.protobuf.Int32Value')?.getValue();
                    break;
                case 'SecondsInState':
                    this.secondsInState = anyVal?.unpack(Int32Value.deserializeBinary, 'google.protobuf.Int32Value')?.getValue();
                    break;
                case 'Progress':
                    this.progress = anyVal?.unpack(Int32Value.deserializeBinary, 'google.protobuf.Int32Value')?.getValue();
                    break;
                case 'Background':
                    this.background = anyVal?.unpack(StringValue.deserializeBinary, 'google.protobuf.StringValue')?.getValue();
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
