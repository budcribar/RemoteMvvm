// Auto-generated TypeScript client for SampleViewModel
import { CounterServiceClient } from './generated/CounterServiceServiceClientPb.js';
import { SampleViewModelState, UpdatePropertyValueRequest, SubscribeRequest, PropertyChangeNotification, ConnectionStatusResponse, ConnectionStatus, IncrementCountRequest, DelayedIncrementAsyncRequest, SetNameToValueRequest } from './generated/CounterService_pb.js';
import * as grpcWeb from 'grpc-web';
import { Empty } from 'google-protobuf/google/protobuf/empty_pb';
import { Any } from 'google-protobuf/google/protobuf/any_pb';
import { StringValue, Int32Value, BoolValue } from 'google-protobuf/google/protobuf/wrappers_pb';

export class SampleViewModelRemoteClient {
    private readonly grpcClient: CounterServiceClient;
    private propertyStream?: grpcWeb.ClientReadableStream<PropertyChangeNotification>;
    private pingIntervalId?: any;
    private changeCallbacks: Array<() => void> = [];

    name: any;
    count: any;
    connectionStatus: string = 'Unknown';

    addChangeListener(cb: () => void): void {
        this.changeCallbacks.push(cb);
    }

    private notifyChange(): void {
        this.changeCallbacks.forEach(cb => cb());
    }

    constructor(grpcClient: CounterServiceClient) {
        this.grpcClient = grpcClient;
    }

    async initializeRemote(): Promise<void> {
        const state = await this.grpcClient.getState(new Empty());
        this.name = (state as any).getName();
        this.count = (state as any).getCount();
        this.connectionStatus = 'Connected';
        this.notifyChange();
        this.startListeningToPropertyChanges();
        this.startPingLoop();
    }

    async refreshState(): Promise<void> {
        const state = await this.grpcClient.getState(new Empty());
        this.name = (state as any).getName();
        this.count = (state as any).getCount();
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

    async incrementCount(): Promise<void> {
        const req = new IncrementCountRequest();
        await this.grpcClient.incrementCount(req);
    }
    async delayedIncrementAsync(delayMilliseconds: any): Promise<void> {
        const req = new DelayedIncrementAsyncRequest();
        req.setDelayMilliseconds(delayMilliseconds);
        await this.grpcClient.delayedIncrementAsync(req);
    }
    async setNameToValue(value: any): Promise<void> {
        const req = new SetNameToValueRequest();
        req.setValue(value);
        await this.grpcClient.setNameToValue(req);
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
                case 'Name':
                    this.name = anyVal?.unpack(StringValue.deserializeBinary, 'google.protobuf.StringValue')?.getValue();
                    break;
                case 'Count':
                    this.count = anyVal?.unpack(Int32Value.deserializeBinary, 'google.protobuf.Int32Value')?.getValue();
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
