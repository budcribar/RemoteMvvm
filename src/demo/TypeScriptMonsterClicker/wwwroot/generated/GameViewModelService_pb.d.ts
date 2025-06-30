// package: monsterclicker_viewmodels_protos
// file: GameViewModelService.proto

import * as jspb from "google-protobuf";
import * as google_protobuf_any_pb from "google-protobuf/google/protobuf/any_pb";
import * as google_protobuf_empty_pb from "google-protobuf/google/protobuf/empty_pb";

export class GameViewModelState extends jspb.Message {
  getCanUseSpecialAttack(): boolean;
  setCanUseSpecialAttack(value: boolean): void;

  getGameMessage(): string;
  setGameMessage(value: string): void;

  getIsMonsterDefeated(): boolean;
  setIsMonsterDefeated(value: boolean): void;

  getIsSpecialAttackOnCooldown(): boolean;
  setIsSpecialAttackOnCooldown(value: boolean): void;

  getMonsterCurrentHealth(): number;
  setMonsterCurrentHealth(value: number): void;

  getMonsterMaxHealth(): number;
  setMonsterMaxHealth(value: number): void;

  getMonsterName(): string;
  setMonsterName(value: string): void;

  getPlayerDamage(): number;
  setPlayerDamage(value: number): void;

  serializeBinary(): Uint8Array;
  toObject(includeInstance?: boolean): GameViewModelState.AsObject;
  static toObject(includeInstance: boolean, msg: GameViewModelState): GameViewModelState.AsObject;
  static extensions: {[key: number]: jspb.ExtensionFieldInfo<jspb.Message>};
  static extensionsBinary: {[key: number]: jspb.ExtensionFieldBinaryInfo<jspb.Message>};
  static serializeBinaryToWriter(message: GameViewModelState, writer: jspb.BinaryWriter): void;
  static deserializeBinary(bytes: Uint8Array): GameViewModelState;
  static deserializeBinaryFromReader(message: GameViewModelState, reader: jspb.BinaryReader): GameViewModelState;
}

export namespace GameViewModelState {
  export type AsObject = {
    canUseSpecialAttack: boolean,
    gameMessage: string,
    isMonsterDefeated: boolean,
    isSpecialAttackOnCooldown: boolean,
    monsterCurrentHealth: number,
    monsterMaxHealth: number,
    monsterName: string,
    playerDamage: number,
  }
}

export class PropertyChangeNotification extends jspb.Message {
  getPropertyName(): string;
  setPropertyName(value: string): void;

  hasNewValue(): boolean;
  clearNewValue(): void;
  getNewValue(): google_protobuf_any_pb.Any | undefined;
  setNewValue(value?: google_protobuf_any_pb.Any): void;

  serializeBinary(): Uint8Array;
  toObject(includeInstance?: boolean): PropertyChangeNotification.AsObject;
  static toObject(includeInstance: boolean, msg: PropertyChangeNotification): PropertyChangeNotification.AsObject;
  static extensions: {[key: number]: jspb.ExtensionFieldInfo<jspb.Message>};
  static extensionsBinary: {[key: number]: jspb.ExtensionFieldBinaryInfo<jspb.Message>};
  static serializeBinaryToWriter(message: PropertyChangeNotification, writer: jspb.BinaryWriter): void;
  static deserializeBinary(bytes: Uint8Array): PropertyChangeNotification;
  static deserializeBinaryFromReader(message: PropertyChangeNotification, reader: jspb.BinaryReader): PropertyChangeNotification;
}

export namespace PropertyChangeNotification {
  export type AsObject = {
    propertyName: string,
    newValue?: google_protobuf_any_pb.Any.AsObject,
  }
}

export class UpdatePropertyValueRequest extends jspb.Message {
  getPropertyName(): string;
  setPropertyName(value: string): void;

  hasNewValue(): boolean;
  clearNewValue(): void;
  getNewValue(): google_protobuf_any_pb.Any | undefined;
  setNewValue(value?: google_protobuf_any_pb.Any): void;

  serializeBinary(): Uint8Array;
  toObject(includeInstance?: boolean): UpdatePropertyValueRequest.AsObject;
  static toObject(includeInstance: boolean, msg: UpdatePropertyValueRequest): UpdatePropertyValueRequest.AsObject;
  static extensions: {[key: number]: jspb.ExtensionFieldInfo<jspb.Message>};
  static extensionsBinary: {[key: number]: jspb.ExtensionFieldBinaryInfo<jspb.Message>};
  static serializeBinaryToWriter(message: UpdatePropertyValueRequest, writer: jspb.BinaryWriter): void;
  static deserializeBinary(bytes: Uint8Array): UpdatePropertyValueRequest;
  static deserializeBinaryFromReader(message: UpdatePropertyValueRequest, reader: jspb.BinaryReader): UpdatePropertyValueRequest;
}

export namespace UpdatePropertyValueRequest {
  export type AsObject = {
    propertyName: string,
    newValue?: google_protobuf_any_pb.Any.AsObject,
  }
}

export class AttackMonsterRequest extends jspb.Message {
  serializeBinary(): Uint8Array;
  toObject(includeInstance?: boolean): AttackMonsterRequest.AsObject;
  static toObject(includeInstance: boolean, msg: AttackMonsterRequest): AttackMonsterRequest.AsObject;
  static extensions: {[key: number]: jspb.ExtensionFieldInfo<jspb.Message>};
  static extensionsBinary: {[key: number]: jspb.ExtensionFieldBinaryInfo<jspb.Message>};
  static serializeBinaryToWriter(message: AttackMonsterRequest, writer: jspb.BinaryWriter): void;
  static deserializeBinary(bytes: Uint8Array): AttackMonsterRequest;
  static deserializeBinaryFromReader(message: AttackMonsterRequest, reader: jspb.BinaryReader): AttackMonsterRequest;
}

export namespace AttackMonsterRequest {
  export type AsObject = {
  }
}

export class AttackMonsterResponse extends jspb.Message {
  serializeBinary(): Uint8Array;
  toObject(includeInstance?: boolean): AttackMonsterResponse.AsObject;
  static toObject(includeInstance: boolean, msg: AttackMonsterResponse): AttackMonsterResponse.AsObject;
  static extensions: {[key: number]: jspb.ExtensionFieldInfo<jspb.Message>};
  static extensionsBinary: {[key: number]: jspb.ExtensionFieldBinaryInfo<jspb.Message>};
  static serializeBinaryToWriter(message: AttackMonsterResponse, writer: jspb.BinaryWriter): void;
  static deserializeBinary(bytes: Uint8Array): AttackMonsterResponse;
  static deserializeBinaryFromReader(message: AttackMonsterResponse, reader: jspb.BinaryReader): AttackMonsterResponse;
}

export namespace AttackMonsterResponse {
  export type AsObject = {
  }
}

export class ResetGameRequest extends jspb.Message {
  serializeBinary(): Uint8Array;
  toObject(includeInstance?: boolean): ResetGameRequest.AsObject;
  static toObject(includeInstance: boolean, msg: ResetGameRequest): ResetGameRequest.AsObject;
  static extensions: {[key: number]: jspb.ExtensionFieldInfo<jspb.Message>};
  static extensionsBinary: {[key: number]: jspb.ExtensionFieldBinaryInfo<jspb.Message>};
  static serializeBinaryToWriter(message: ResetGameRequest, writer: jspb.BinaryWriter): void;
  static deserializeBinary(bytes: Uint8Array): ResetGameRequest;
  static deserializeBinaryFromReader(message: ResetGameRequest, reader: jspb.BinaryReader): ResetGameRequest;
}

export namespace ResetGameRequest {
  export type AsObject = {
  }
}

export class ResetGameResponse extends jspb.Message {
  serializeBinary(): Uint8Array;
  toObject(includeInstance?: boolean): ResetGameResponse.AsObject;
  static toObject(includeInstance: boolean, msg: ResetGameResponse): ResetGameResponse.AsObject;
  static extensions: {[key: number]: jspb.ExtensionFieldInfo<jspb.Message>};
  static extensionsBinary: {[key: number]: jspb.ExtensionFieldBinaryInfo<jspb.Message>};
  static serializeBinaryToWriter(message: ResetGameResponse, writer: jspb.BinaryWriter): void;
  static deserializeBinary(bytes: Uint8Array): ResetGameResponse;
  static deserializeBinaryFromReader(message: ResetGameResponse, reader: jspb.BinaryReader): ResetGameResponse;
}

export namespace ResetGameResponse {
  export type AsObject = {
  }
}

export class SpecialAttackAsyncRequest extends jspb.Message {
  serializeBinary(): Uint8Array;
  toObject(includeInstance?: boolean): SpecialAttackAsyncRequest.AsObject;
  static toObject(includeInstance: boolean, msg: SpecialAttackAsyncRequest): SpecialAttackAsyncRequest.AsObject;
  static extensions: {[key: number]: jspb.ExtensionFieldInfo<jspb.Message>};
  static extensionsBinary: {[key: number]: jspb.ExtensionFieldBinaryInfo<jspb.Message>};
  static serializeBinaryToWriter(message: SpecialAttackAsyncRequest, writer: jspb.BinaryWriter): void;
  static deserializeBinary(bytes: Uint8Array): SpecialAttackAsyncRequest;
  static deserializeBinaryFromReader(message: SpecialAttackAsyncRequest, reader: jspb.BinaryReader): SpecialAttackAsyncRequest;
}

export namespace SpecialAttackAsyncRequest {
  export type AsObject = {
  }
}

export class SpecialAttackAsyncResponse extends jspb.Message {
  serializeBinary(): Uint8Array;
  toObject(includeInstance?: boolean): SpecialAttackAsyncResponse.AsObject;
  static toObject(includeInstance: boolean, msg: SpecialAttackAsyncResponse): SpecialAttackAsyncResponse.AsObject;
  static extensions: {[key: number]: jspb.ExtensionFieldInfo<jspb.Message>};
  static extensionsBinary: {[key: number]: jspb.ExtensionFieldBinaryInfo<jspb.Message>};
  static serializeBinaryToWriter(message: SpecialAttackAsyncResponse, writer: jspb.BinaryWriter): void;
  static deserializeBinary(bytes: Uint8Array): SpecialAttackAsyncResponse;
  static deserializeBinaryFromReader(message: SpecialAttackAsyncResponse, reader: jspb.BinaryReader): SpecialAttackAsyncResponse;
}

export namespace SpecialAttackAsyncResponse {
  export type AsObject = {
  }
}

export class SubscribeRequest extends jspb.Message {
  getClientId(): string;
  setClientId(value: string): void;

  serializeBinary(): Uint8Array;
  toObject(includeInstance?: boolean): SubscribeRequest.AsObject;
  static toObject(includeInstance: boolean, msg: SubscribeRequest): SubscribeRequest.AsObject;
  static extensions: {[key: number]: jspb.ExtensionFieldInfo<jspb.Message>};
  static extensionsBinary: {[key: number]: jspb.ExtensionFieldBinaryInfo<jspb.Message>};
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
  getStatus(): ConnectionStatusMap[keyof ConnectionStatusMap];
  setStatus(value: ConnectionStatusMap[keyof ConnectionStatusMap]): void;

  serializeBinary(): Uint8Array;
  toObject(includeInstance?: boolean): ConnectionStatusResponse.AsObject;
  static toObject(includeInstance: boolean, msg: ConnectionStatusResponse): ConnectionStatusResponse.AsObject;
  static extensions: {[key: number]: jspb.ExtensionFieldInfo<jspb.Message>};
  static extensionsBinary: {[key: number]: jspb.ExtensionFieldBinaryInfo<jspb.Message>};
  static serializeBinaryToWriter(message: ConnectionStatusResponse, writer: jspb.BinaryWriter): void;
  static deserializeBinary(bytes: Uint8Array): ConnectionStatusResponse;
  static deserializeBinaryFromReader(message: ConnectionStatusResponse, reader: jspb.BinaryReader): ConnectionStatusResponse;
}

export namespace ConnectionStatusResponse {
  export type AsObject = {
    status: ConnectionStatusMap[keyof ConnectionStatusMap],
  }
}

export interface ConnectionStatusMap {
  UNKNOWN: 0;
  CONNECTED: 1;
  DISCONNECTED: 2;
}

export const ConnectionStatus: ConnectionStatusMap;

