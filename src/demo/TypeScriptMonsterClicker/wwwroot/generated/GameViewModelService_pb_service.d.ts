// package: monsterclicker_viewmodels_protos
// file: GameViewModelService.proto

import * as GameViewModelService_pb from "./GameViewModelService_pb";
import * as google_protobuf_empty_pb from "google-protobuf/google/protobuf/empty_pb";
import {grpc} from "@improbable-eng/grpc-web";

type GameViewModelServiceGetState = {
  readonly methodName: string;
  readonly service: typeof GameViewModelService;
  readonly requestStream: false;
  readonly responseStream: false;
  readonly requestType: typeof google_protobuf_empty_pb.Empty;
  readonly responseType: typeof GameViewModelService_pb.GameViewModelState;
};

type GameViewModelServiceSubscribeToPropertyChanges = {
  readonly methodName: string;
  readonly service: typeof GameViewModelService;
  readonly requestStream: false;
  readonly responseStream: true;
  readonly requestType: typeof GameViewModelService_pb.SubscribeRequest;
  readonly responseType: typeof GameViewModelService_pb.PropertyChangeNotification;
};

type GameViewModelServiceUpdatePropertyValue = {
  readonly methodName: string;
  readonly service: typeof GameViewModelService;
  readonly requestStream: false;
  readonly responseStream: false;
  readonly requestType: typeof GameViewModelService_pb.UpdatePropertyValueRequest;
  readonly responseType: typeof google_protobuf_empty_pb.Empty;
};

type GameViewModelServiceAttackMonster = {
  readonly methodName: string;
  readonly service: typeof GameViewModelService;
  readonly requestStream: false;
  readonly responseStream: false;
  readonly requestType: typeof GameViewModelService_pb.AttackMonsterRequest;
  readonly responseType: typeof GameViewModelService_pb.AttackMonsterResponse;
};

type GameViewModelServiceResetGame = {
  readonly methodName: string;
  readonly service: typeof GameViewModelService;
  readonly requestStream: false;
  readonly responseStream: false;
  readonly requestType: typeof GameViewModelService_pb.ResetGameRequest;
  readonly responseType: typeof GameViewModelService_pb.ResetGameResponse;
};

type GameViewModelServiceSpecialAttackAsync = {
  readonly methodName: string;
  readonly service: typeof GameViewModelService;
  readonly requestStream: false;
  readonly responseStream: false;
  readonly requestType: typeof GameViewModelService_pb.SpecialAttackAsyncRequest;
  readonly responseType: typeof GameViewModelService_pb.SpecialAttackAsyncResponse;
};

type GameViewModelServicePing = {
  readonly methodName: string;
  readonly service: typeof GameViewModelService;
  readonly requestStream: false;
  readonly responseStream: false;
  readonly requestType: typeof google_protobuf_empty_pb.Empty;
  readonly responseType: typeof GameViewModelService_pb.ConnectionStatusResponse;
};

export class GameViewModelService {
  static readonly serviceName: string;
  static readonly GetState: GameViewModelServiceGetState;
  static readonly SubscribeToPropertyChanges: GameViewModelServiceSubscribeToPropertyChanges;
  static readonly UpdatePropertyValue: GameViewModelServiceUpdatePropertyValue;
  static readonly AttackMonster: GameViewModelServiceAttackMonster;
  static readonly ResetGame: GameViewModelServiceResetGame;
  static readonly SpecialAttackAsync: GameViewModelServiceSpecialAttackAsync;
  static readonly Ping: GameViewModelServicePing;
}

export type ServiceError = { message: string, code: number; metadata: grpc.Metadata }
export type Status = { details: string, code: number; metadata: grpc.Metadata }

interface UnaryResponse {
  cancel(): void;
}
interface ResponseStream<T> {
  cancel(): void;
  on(type: 'data', handler: (message: T) => void): ResponseStream<T>;
  on(type: 'end', handler: (status?: Status) => void): ResponseStream<T>;
  on(type: 'status', handler: (status: Status) => void): ResponseStream<T>;
}
interface RequestStream<T> {
  write(message: T): RequestStream<T>;
  end(): void;
  cancel(): void;
  on(type: 'end', handler: (status?: Status) => void): RequestStream<T>;
  on(type: 'status', handler: (status: Status) => void): RequestStream<T>;
}
interface BidirectionalStream<ReqT, ResT> {
  write(message: ReqT): BidirectionalStream<ReqT, ResT>;
  end(): void;
  cancel(): void;
  on(type: 'data', handler: (message: ResT) => void): BidirectionalStream<ReqT, ResT>;
  on(type: 'end', handler: (status?: Status) => void): BidirectionalStream<ReqT, ResT>;
  on(type: 'status', handler: (status: Status) => void): BidirectionalStream<ReqT, ResT>;
}

export class GameViewModelServiceClient {
  readonly serviceHost: string;

  constructor(serviceHost: string, options?: grpc.RpcOptions);
  getState(
    requestMessage: google_protobuf_empty_pb.Empty,
    metadata: grpc.Metadata,
    callback: (error: ServiceError|null, responseMessage: GameViewModelService_pb.GameViewModelState|null) => void
  ): UnaryResponse;
  getState(
    requestMessage: google_protobuf_empty_pb.Empty,
    callback: (error: ServiceError|null, responseMessage: GameViewModelService_pb.GameViewModelState|null) => void
  ): UnaryResponse;
  subscribeToPropertyChanges(requestMessage: GameViewModelService_pb.SubscribeRequest, metadata?: grpc.Metadata): ResponseStream<GameViewModelService_pb.PropertyChangeNotification>;
  updatePropertyValue(
    requestMessage: GameViewModelService_pb.UpdatePropertyValueRequest,
    metadata: grpc.Metadata,
    callback: (error: ServiceError|null, responseMessage: google_protobuf_empty_pb.Empty|null) => void
  ): UnaryResponse;
  updatePropertyValue(
    requestMessage: GameViewModelService_pb.UpdatePropertyValueRequest,
    callback: (error: ServiceError|null, responseMessage: google_protobuf_empty_pb.Empty|null) => void
  ): UnaryResponse;
  attackMonster(
    requestMessage: GameViewModelService_pb.AttackMonsterRequest,
    metadata: grpc.Metadata,
    callback: (error: ServiceError|null, responseMessage: GameViewModelService_pb.AttackMonsterResponse|null) => void
  ): UnaryResponse;
  attackMonster(
    requestMessage: GameViewModelService_pb.AttackMonsterRequest,
    callback: (error: ServiceError|null, responseMessage: GameViewModelService_pb.AttackMonsterResponse|null) => void
  ): UnaryResponse;
  resetGame(
    requestMessage: GameViewModelService_pb.ResetGameRequest,
    metadata: grpc.Metadata,
    callback: (error: ServiceError|null, responseMessage: GameViewModelService_pb.ResetGameResponse|null) => void
  ): UnaryResponse;
  resetGame(
    requestMessage: GameViewModelService_pb.ResetGameRequest,
    callback: (error: ServiceError|null, responseMessage: GameViewModelService_pb.ResetGameResponse|null) => void
  ): UnaryResponse;
  specialAttackAsync(
    requestMessage: GameViewModelService_pb.SpecialAttackAsyncRequest,
    metadata: grpc.Metadata,
    callback: (error: ServiceError|null, responseMessage: GameViewModelService_pb.SpecialAttackAsyncResponse|null) => void
  ): UnaryResponse;
  specialAttackAsync(
    requestMessage: GameViewModelService_pb.SpecialAttackAsyncRequest,
    callback: (error: ServiceError|null, responseMessage: GameViewModelService_pb.SpecialAttackAsyncResponse|null) => void
  ): UnaryResponse;
  ping(
    requestMessage: google_protobuf_empty_pb.Empty,
    metadata: grpc.Metadata,
    callback: (error: ServiceError|null, responseMessage: GameViewModelService_pb.ConnectionStatusResponse|null) => void
  ): UnaryResponse;
  ping(
    requestMessage: google_protobuf_empty_pb.Empty,
    callback: (error: ServiceError|null, responseMessage: GameViewModelService_pb.ConnectionStatusResponse|null) => void
  ): UnaryResponse;
}

