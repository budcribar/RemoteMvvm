import * as jspb from 'google-protobuf'

import * as google_protobuf_any_pb from 'google-protobuf/google/protobuf/any_pb'; // proto import: "google/protobuf/any.proto"
import * as google_protobuf_empty_pb from 'google-protobuf/google/protobuf/empty_pb'; // proto import: "google/protobuf/empty.proto"
import * as google_protobuf_timestamp_pb from 'google-protobuf/google/protobuf/timestamp_pb'; // proto import: "google/protobuf/timestamp.proto"


export class HP3LSThermalTestViewModelState extends jspb.Message {
  getCpuTemperatureThreshold(): number;
  setCpuTemperatureThreshold(value: number): HP3LSThermalTestViewModelState;

  getCpuLoadThreshold(): number;
  setCpuLoadThreshold(value: number): HP3LSThermalTestViewModelState;

  getCpuLoadTimeSpan(): number;
  setCpuLoadTimeSpan(value: number): HP3LSThermalTestViewModelState;

  getZoneListList(): Array<ThermalZoneComponentViewModelState>;
  setZoneListList(value: Array<ThermalZoneComponentViewModelState>): HP3LSThermalTestViewModelState;
  clearZoneListList(): HP3LSThermalTestViewModelState;
  addZoneList(value?: ThermalZoneComponentViewModelState, index?: number): ThermalZoneComponentViewModelState;

  getTestSettings(): TestSettingsModelState | undefined;
  setTestSettings(value?: TestSettingsModelState): HP3LSThermalTestViewModelState;
  hasTestSettings(): boolean;
  clearTestSettings(): HP3LSThermalTestViewModelState;

  getShowDescription(): boolean;
  setShowDescription(value: boolean): HP3LSThermalTestViewModelState;

  getShowReadme(): boolean;
  setShowReadme(value: boolean): HP3LSThermalTestViewModelState;

  serializeBinary(): Uint8Array;
  toObject(includeInstance?: boolean): HP3LSThermalTestViewModelState.AsObject;
  static toObject(includeInstance: boolean, msg: HP3LSThermalTestViewModelState): HP3LSThermalTestViewModelState.AsObject;
  static serializeBinaryToWriter(message: HP3LSThermalTestViewModelState, writer: jspb.BinaryWriter): void;
  static deserializeBinary(bytes: Uint8Array): HP3LSThermalTestViewModelState;
  static deserializeBinaryFromReader(message: HP3LSThermalTestViewModelState, reader: jspb.BinaryReader): HP3LSThermalTestViewModelState;
}

export namespace HP3LSThermalTestViewModelState {
  export type AsObject = {
    cpuTemperatureThreshold: number,
    cpuLoadThreshold: number,
    cpuLoadTimeSpan: number,
    zoneListList: Array<ThermalZoneComponentViewModelState.AsObject>,
    testSettings?: TestSettingsModelState.AsObject,
    showDescription: boolean,
    showReadme: boolean,
  }
}

export class ThermalZoneComponentViewModelState extends jspb.Message {
  getZone(): number;
  setZone(value: number): ThermalZoneComponentViewModelState;

  getIsActive(): boolean;
  setIsActive(value: boolean): ThermalZoneComponentViewModelState;

  getDeviceName(): string;
  setDeviceName(value: string): ThermalZoneComponentViewModelState;

  getTemperature(): number;
  setTemperature(value: number): ThermalZoneComponentViewModelState;

  getProcessorLoad(): number;
  setProcessorLoad(value: number): ThermalZoneComponentViewModelState;

  getFanSpeed(): number;
  setFanSpeed(value: number): ThermalZoneComponentViewModelState;

  getSecondsInState(): number;
  setSecondsInState(value: number): ThermalZoneComponentViewModelState;

  getFirstSeenInState(): google_protobuf_timestamp_pb.Timestamp | undefined;
  setFirstSeenInState(value?: google_protobuf_timestamp_pb.Timestamp): ThermalZoneComponentViewModelState;
  hasFirstSeenInState(): boolean;
  clearFirstSeenInState(): ThermalZoneComponentViewModelState;

  getProgress(): number;
  setProgress(value: number): ThermalZoneComponentViewModelState;

  getBackground(): string;
  setBackground(value: string): ThermalZoneComponentViewModelState;

  getStatus(): number;
  setStatus(value: number): ThermalZoneComponentViewModelState;

  getState(): number;
  setState(value: number): ThermalZoneComponentViewModelState;

  serializeBinary(): Uint8Array;
  toObject(includeInstance?: boolean): ThermalZoneComponentViewModelState.AsObject;
  static toObject(includeInstance: boolean, msg: ThermalZoneComponentViewModelState): ThermalZoneComponentViewModelState.AsObject;
  static serializeBinaryToWriter(message: ThermalZoneComponentViewModelState, writer: jspb.BinaryWriter): void;
  static deserializeBinary(bytes: Uint8Array): ThermalZoneComponentViewModelState;
  static deserializeBinaryFromReader(message: ThermalZoneComponentViewModelState, reader: jspb.BinaryReader): ThermalZoneComponentViewModelState;
}

export namespace ThermalZoneComponentViewModelState {
  export type AsObject = {
    zone: number,
    isActive: boolean,
    deviceName: string,
    temperature: number,
    processorLoad: number,
    fanSpeed: number,
    secondsInState: number,
    firstSeenInState?: google_protobuf_timestamp_pb.Timestamp.AsObject,
    progress: number,
    background: string,
    status: number,
    state: number,
  }
}

export class TestSettingsModelState extends jspb.Message {
  getCpuTemperatureThreshold(): number;
  setCpuTemperatureThreshold(value: number): TestSettingsModelState;

  getCpuLoadThreshold(): number;
  setCpuLoadThreshold(value: number): TestSettingsModelState;

  getCpuLoadTimeSpan(): number;
  setCpuLoadTimeSpan(value: number): TestSettingsModelState;

  serializeBinary(): Uint8Array;
  toObject(includeInstance?: boolean): TestSettingsModelState.AsObject;
  static toObject(includeInstance: boolean, msg: TestSettingsModelState): TestSettingsModelState.AsObject;
  static serializeBinaryToWriter(message: TestSettingsModelState, writer: jspb.BinaryWriter): void;
  static deserializeBinary(bytes: Uint8Array): TestSettingsModelState;
  static deserializeBinaryFromReader(message: TestSettingsModelState, reader: jspb.BinaryReader): TestSettingsModelState;
}

export namespace TestSettingsModelState {
  export type AsObject = {
    cpuTemperatureThreshold: number,
    cpuLoadThreshold: number,
    cpuLoadTimeSpan: number,
  }
}

export class UpdatePropertyValueRequest extends jspb.Message {
  getPropertyName(): string;
  setPropertyName(value: string): UpdatePropertyValueRequest;

  getNewValue(): google_protobuf_any_pb.Any | undefined;
  setNewValue(value?: google_protobuf_any_pb.Any): UpdatePropertyValueRequest;
  hasNewValue(): boolean;
  clearNewValue(): UpdatePropertyValueRequest;

  getPropertyPath(): string;
  setPropertyPath(value: string): UpdatePropertyValueRequest;

  getCollectionKey(): string;
  setCollectionKey(value: string): UpdatePropertyValueRequest;

  getArrayIndex(): number;
  setArrayIndex(value: number): UpdatePropertyValueRequest;

  getOperationType(): string;
  setOperationType(value: string): UpdatePropertyValueRequest;

  serializeBinary(): Uint8Array;
  toObject(includeInstance?: boolean): UpdatePropertyValueRequest.AsObject;
  static toObject(includeInstance: boolean, msg: UpdatePropertyValueRequest): UpdatePropertyValueRequest.AsObject;
  static serializeBinaryToWriter(message: UpdatePropertyValueRequest, writer: jspb.BinaryWriter): void;
  static deserializeBinary(bytes: Uint8Array): UpdatePropertyValueRequest;
  static deserializeBinaryFromReader(message: UpdatePropertyValueRequest, reader: jspb.BinaryReader): UpdatePropertyValueRequest;
}

export namespace UpdatePropertyValueRequest {
  export type AsObject = {
    propertyName: string,
    newValue?: google_protobuf_any_pb.Any.AsObject,
    propertyPath: string,
    collectionKey: string,
    arrayIndex: number,
    operationType: string,
  }
}

export class UpdatePropertyValueResponse extends jspb.Message {
  getSuccess(): boolean;
  setSuccess(value: boolean): UpdatePropertyValueResponse;

  getErrorMessage(): string;
  setErrorMessage(value: string): UpdatePropertyValueResponse;

  getValidationErrors(): string;
  setValidationErrors(value: string): UpdatePropertyValueResponse;

  getOldValue(): google_protobuf_any_pb.Any | undefined;
  setOldValue(value?: google_protobuf_any_pb.Any): UpdatePropertyValueResponse;
  hasOldValue(): boolean;
  clearOldValue(): UpdatePropertyValueResponse;

  serializeBinary(): Uint8Array;
  toObject(includeInstance?: boolean): UpdatePropertyValueResponse.AsObject;
  static toObject(includeInstance: boolean, msg: UpdatePropertyValueResponse): UpdatePropertyValueResponse.AsObject;
  static serializeBinaryToWriter(message: UpdatePropertyValueResponse, writer: jspb.BinaryWriter): void;
  static deserializeBinary(bytes: Uint8Array): UpdatePropertyValueResponse;
  static deserializeBinaryFromReader(message: UpdatePropertyValueResponse, reader: jspb.BinaryReader): UpdatePropertyValueResponse;
}

export namespace UpdatePropertyValueResponse {
  export type AsObject = {
    success: boolean,
    errorMessage: string,
    validationErrors: string,
    oldValue?: google_protobuf_any_pb.Any.AsObject,
  }
}

export class PropertyChangeNotification extends jspb.Message {
  getPropertyName(): string;
  setPropertyName(value: string): PropertyChangeNotification;

  getNewValue(): google_protobuf_any_pb.Any | undefined;
  setNewValue(value?: google_protobuf_any_pb.Any): PropertyChangeNotification;
  hasNewValue(): boolean;
  clearNewValue(): PropertyChangeNotification;

  getPropertyPath(): string;
  setPropertyPath(value: string): PropertyChangeNotification;

  getChangeType(): string;
  setChangeType(value: string): PropertyChangeNotification;

  getOldValue(): google_protobuf_any_pb.Any | undefined;
  setOldValue(value?: google_protobuf_any_pb.Any): PropertyChangeNotification;
  hasOldValue(): boolean;
  clearOldValue(): PropertyChangeNotification;

  serializeBinary(): Uint8Array;
  toObject(includeInstance?: boolean): PropertyChangeNotification.AsObject;
  static toObject(includeInstance: boolean, msg: PropertyChangeNotification): PropertyChangeNotification.AsObject;
  static serializeBinaryToWriter(message: PropertyChangeNotification, writer: jspb.BinaryWriter): void;
  static deserializeBinary(bytes: Uint8Array): PropertyChangeNotification;
  static deserializeBinaryFromReader(message: PropertyChangeNotification, reader: jspb.BinaryReader): PropertyChangeNotification;
}

export namespace PropertyChangeNotification {
  export type AsObject = {
    propertyName: string,
    newValue?: google_protobuf_any_pb.Any.AsObject,
    propertyPath: string,
    changeType: string,
    oldValue?: google_protobuf_any_pb.Any.AsObject,
  }
}

export class StateChangedRequest extends jspb.Message {
  getState(): number;
  setState(value: number): StateChangedRequest;

  serializeBinary(): Uint8Array;
  toObject(includeInstance?: boolean): StateChangedRequest.AsObject;
  static toObject(includeInstance: boolean, msg: StateChangedRequest): StateChangedRequest.AsObject;
  static serializeBinaryToWriter(message: StateChangedRequest, writer: jspb.BinaryWriter): void;
  static deserializeBinary(bytes: Uint8Array): StateChangedRequest;
  static deserializeBinaryFromReader(message: StateChangedRequest, reader: jspb.BinaryReader): StateChangedRequest;
}

export namespace StateChangedRequest {
  export type AsObject = {
    state: number,
  }
}

export class StateChangedResponse extends jspb.Message {
  serializeBinary(): Uint8Array;
  toObject(includeInstance?: boolean): StateChangedResponse.AsObject;
  static toObject(includeInstance: boolean, msg: StateChangedResponse): StateChangedResponse.AsObject;
  static serializeBinaryToWriter(message: StateChangedResponse, writer: jspb.BinaryWriter): void;
  static deserializeBinary(bytes: Uint8Array): StateChangedResponse;
  static deserializeBinaryFromReader(message: StateChangedResponse, reader: jspb.BinaryReader): StateChangedResponse;
}

export namespace StateChangedResponse {
  export type AsObject = {
  }
}

export class CancelTestRequest extends jspb.Message {
  serializeBinary(): Uint8Array;
  toObject(includeInstance?: boolean): CancelTestRequest.AsObject;
  static toObject(includeInstance: boolean, msg: CancelTestRequest): CancelTestRequest.AsObject;
  static serializeBinaryToWriter(message: CancelTestRequest, writer: jspb.BinaryWriter): void;
  static deserializeBinary(bytes: Uint8Array): CancelTestRequest;
  static deserializeBinaryFromReader(message: CancelTestRequest, reader: jspb.BinaryReader): CancelTestRequest;
}

export namespace CancelTestRequest {
  export type AsObject = {
  }
}

export class CancelTestResponse extends jspb.Message {
  serializeBinary(): Uint8Array;
  toObject(includeInstance?: boolean): CancelTestResponse.AsObject;
  static toObject(includeInstance: boolean, msg: CancelTestResponse): CancelTestResponse.AsObject;
  static serializeBinaryToWriter(message: CancelTestResponse, writer: jspb.BinaryWriter): void;
  static deserializeBinary(bytes: Uint8Array): CancelTestResponse;
  static deserializeBinaryFromReader(message: CancelTestResponse, reader: jspb.BinaryReader): CancelTestResponse;
}

export namespace CancelTestResponse {
  export type AsObject = {
  }
}

export class SubscribeRequest extends jspb.Message {
  getClientId(): string;
  setClientId(value: string): SubscribeRequest;

  serializeBinary(): Uint8Array;
  toObject(includeInstance?: boolean): SubscribeRequest.AsObject;
  static toObject(includeInstance: boolean, msg: SubscribeRequest): SubscribeRequest.AsObject;
  static serializeBinaryToWriter(message: SubscribeRequest, writer: jspb.BinaryWriter): void;
  static deserializeBinary(bytes: Uint8Array): SubscribeRequest;
  static deserializeBinaryFromReader(message: SubscribeRequest, reader: jspb.BinaryReader): SubscribeRequest;
}

export namespace SubscribeRequest {
  export type AsObject = {
    clientId: string,
  }
}

export class ConnectionStatusResponse extends jspb.Message {
  getStatus(): ConnectionStatus;
  setStatus(value: ConnectionStatus): ConnectionStatusResponse;

  serializeBinary(): Uint8Array;
  toObject(includeInstance?: boolean): ConnectionStatusResponse.AsObject;
  static toObject(includeInstance: boolean, msg: ConnectionStatusResponse): ConnectionStatusResponse.AsObject;
  static serializeBinaryToWriter(message: ConnectionStatusResponse, writer: jspb.BinaryWriter): void;
  static deserializeBinary(bytes: Uint8Array): ConnectionStatusResponse;
  static deserializeBinaryFromReader(message: ConnectionStatusResponse, reader: jspb.BinaryReader): ConnectionStatusResponse;
}

export namespace ConnectionStatusResponse {
  export type AsObject = {
    status: ConnectionStatus,
  }
}

export enum ConnectionStatus { 
  UNKNOWN = 0,
  CONNECTED = 1,
  DISCONNECTED = 2,
}
