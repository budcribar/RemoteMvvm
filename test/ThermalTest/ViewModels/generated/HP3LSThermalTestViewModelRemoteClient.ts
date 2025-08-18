// TypeScript client for HP3LSThermalTestViewModel
import { HP3LSThermalTestServiceClient } from './generated/HP3LSThermalTestServiceServiceClientPb';
import { HP3LSThermalTestViewModelState, UpdatePropertyValueRequest } from './generated/HP3LSThermalTestService_pb';
import { Empty } from 'google-protobuf/google/protobuf/empty_pb';
import { Any } from 'google-protobuf/google/protobuf/any_pb';
import { StringValue, Int32Value, BoolValue } from 'google-protobuf/google/protobuf/wrappers_pb';

export interface ThermalZoneState {
    zone: number;
    isActive: boolean;
    deviceName: string;
    temperature: number;
    processorLoad: number;
    fanSpeed: number;
    secondsInState: number;
    firstSeenInState?: any;
    progress: number;
    background: string;
    status: number;
    state: number;
}

export interface TestSettingsState {
    cpuTemperatureThreshold: number;
    cpuLoadThreshold: number;
    cpuLoadTimeSpan: number;
}

export class HP3LSThermalTestViewModelRemoteClient {
    private readonly grpcClient: HP3LSThermalTestServiceClient;
    private changeCallbacks: Array<() => void> = [];

    zones: Map<number, ThermalZoneState> = new Map();
    testSettings?: TestSettingsState;
    showDescription = false;
    showReadme = false;

    constructor(grpcClient: HP3LSThermalTestServiceClient) {
        this.grpcClient = grpcClient;
    }

    addChangeListener(cb: () => void): void {
        this.changeCallbacks.push(cb);
    }

    private notifyChange(): void {
        this.changeCallbacks.forEach(cb => cb());
    }

    private applyState(state: HP3LSThermalTestViewModelState): void {
        const zonesMap = state.getZonesMap();
        this.zones.clear();
        zonesMap.forEach((value, key) => {
            this.zones.set(key, value.toObject() as ThermalZoneState);
        });
        this.testSettings = state.getTestSettings()?.toObject() as TestSettingsState;
        this.showDescription = state.getShowDescription();
        this.showReadme = state.getShowReadme();
        this.notifyChange();
    }

    async initializeRemote(): Promise<void> {
        const state = await this.grpcClient.getState(new Empty());
        this.applyState(state);
    }

    async refreshState(): Promise<void> {
        const state = await this.grpcClient.getState(new Empty());
        this.applyState(state);
    }

    async updatePropertyValue(propertyName: string, value: any): Promise<void> {
        const req = new UpdatePropertyValueRequest();
        req.setPropertyName(propertyName);
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
        req.setNewValue(anyVal);
        await this.grpcClient.updatePropertyValue(req);
        await this.refreshState();
    }
}

