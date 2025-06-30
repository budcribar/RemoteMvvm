// package: monsterclicker_viewmodels_protos
// file: GameViewModelService.proto

var GameViewModelService_pb = require("./GameViewModelService_pb");
var google_protobuf_empty_pb = require("google-protobuf/google/protobuf/empty_pb");
var grpc = require("@improbable-eng/grpc-web").grpc;

var GameViewModelService = (function () {
  function GameViewModelService() {}
  GameViewModelService.serviceName = "monsterclicker_viewmodels_protos.GameViewModelService";
  return GameViewModelService;
}());

GameViewModelService.GetState = {
  methodName: "GetState",
  service: GameViewModelService,
  requestStream: false,
  responseStream: false,
  requestType: google_protobuf_empty_pb.Empty,
  responseType: GameViewModelService_pb.GameViewModelState
};

GameViewModelService.SubscribeToPropertyChanges = {
  methodName: "SubscribeToPropertyChanges",
  service: GameViewModelService,
  requestStream: false,
  responseStream: true,
  requestType: GameViewModelService_pb.SubscribeRequest,
  responseType: GameViewModelService_pb.PropertyChangeNotification
};

GameViewModelService.UpdatePropertyValue = {
  methodName: "UpdatePropertyValue",
  service: GameViewModelService,
  requestStream: false,
  responseStream: false,
  requestType: GameViewModelService_pb.UpdatePropertyValueRequest,
  responseType: google_protobuf_empty_pb.Empty
};

GameViewModelService.AttackMonster = {
  methodName: "AttackMonster",
  service: GameViewModelService,
  requestStream: false,
  responseStream: false,
  requestType: GameViewModelService_pb.AttackMonsterRequest,
  responseType: GameViewModelService_pb.AttackMonsterResponse
};

GameViewModelService.ResetGame = {
  methodName: "ResetGame",
  service: GameViewModelService,
  requestStream: false,
  responseStream: false,
  requestType: GameViewModelService_pb.ResetGameRequest,
  responseType: GameViewModelService_pb.ResetGameResponse
};

GameViewModelService.SpecialAttackAsync = {
  methodName: "SpecialAttackAsync",
  service: GameViewModelService,
  requestStream: false,
  responseStream: false,
  requestType: GameViewModelService_pb.SpecialAttackAsyncRequest,
  responseType: GameViewModelService_pb.SpecialAttackAsyncResponse
};

GameViewModelService.Ping = {
  methodName: "Ping",
  service: GameViewModelService,
  requestStream: false,
  responseStream: false,
  requestType: google_protobuf_empty_pb.Empty,
  responseType: GameViewModelService_pb.ConnectionStatusResponse
};

exports.GameViewModelService = GameViewModelService;

function GameViewModelServiceClient(serviceHost, options) {
  this.serviceHost = serviceHost;
  this.options = options || {};
}

GameViewModelServiceClient.prototype.getState = function getState(requestMessage, metadata, callback) {
  if (arguments.length === 2) {
    callback = arguments[1];
  }
  var client = grpc.unary(GameViewModelService.GetState, {
    request: requestMessage,
    host: this.serviceHost,
    metadata: metadata,
    transport: this.options.transport,
    debug: this.options.debug,
    onEnd: function (response) {
      if (callback) {
        if (response.status !== grpc.Code.OK) {
          var err = new Error(response.statusMessage);
          err.code = response.status;
          err.metadata = response.trailers;
          callback(err, null);
        } else {
          callback(null, response.message);
        }
      }
    }
  });
  return {
    cancel: function () {
      callback = null;
      client.close();
    }
  };
};

GameViewModelServiceClient.prototype.subscribeToPropertyChanges = function subscribeToPropertyChanges(requestMessage, metadata) {
  var listeners = {
    data: [],
    end: [],
    status: []
  };
  var client = grpc.invoke(GameViewModelService.SubscribeToPropertyChanges, {
    request: requestMessage,
    host: this.serviceHost,
    metadata: metadata,
    transport: this.options.transport,
    debug: this.options.debug,
    onMessage: function (responseMessage) {
      listeners.data.forEach(function (handler) {
        handler(responseMessage);
      });
    },
    onEnd: function (status, statusMessage, trailers) {
      listeners.status.forEach(function (handler) {
        handler({ code: status, details: statusMessage, metadata: trailers });
      });
      listeners.end.forEach(function (handler) {
        handler({ code: status, details: statusMessage, metadata: trailers });
      });
      listeners = null;
    }
  });
  return {
    on: function (type, handler) {
      listeners[type].push(handler);
      return this;
    },
    cancel: function () {
      listeners = null;
      client.close();
    }
  };
};

GameViewModelServiceClient.prototype.updatePropertyValue = function updatePropertyValue(requestMessage, metadata, callback) {
  if (arguments.length === 2) {
    callback = arguments[1];
  }
  var client = grpc.unary(GameViewModelService.UpdatePropertyValue, {
    request: requestMessage,
    host: this.serviceHost,
    metadata: metadata,
    transport: this.options.transport,
    debug: this.options.debug,
    onEnd: function (response) {
      if (callback) {
        if (response.status !== grpc.Code.OK) {
          var err = new Error(response.statusMessage);
          err.code = response.status;
          err.metadata = response.trailers;
          callback(err, null);
        } else {
          callback(null, response.message);
        }
      }
    }
  });
  return {
    cancel: function () {
      callback = null;
      client.close();
    }
  };
};

GameViewModelServiceClient.prototype.attackMonster = function attackMonster(requestMessage, metadata, callback) {
  if (arguments.length === 2) {
    callback = arguments[1];
  }
  var client = grpc.unary(GameViewModelService.AttackMonster, {
    request: requestMessage,
    host: this.serviceHost,
    metadata: metadata,
    transport: this.options.transport,
    debug: this.options.debug,
    onEnd: function (response) {
      if (callback) {
        if (response.status !== grpc.Code.OK) {
          var err = new Error(response.statusMessage);
          err.code = response.status;
          err.metadata = response.trailers;
          callback(err, null);
        } else {
          callback(null, response.message);
        }
      }
    }
  });
  return {
    cancel: function () {
      callback = null;
      client.close();
    }
  };
};

GameViewModelServiceClient.prototype.resetGame = function resetGame(requestMessage, metadata, callback) {
  if (arguments.length === 2) {
    callback = arguments[1];
  }
  var client = grpc.unary(GameViewModelService.ResetGame, {
    request: requestMessage,
    host: this.serviceHost,
    metadata: metadata,
    transport: this.options.transport,
    debug: this.options.debug,
    onEnd: function (response) {
      if (callback) {
        if (response.status !== grpc.Code.OK) {
          var err = new Error(response.statusMessage);
          err.code = response.status;
          err.metadata = response.trailers;
          callback(err, null);
        } else {
          callback(null, response.message);
        }
      }
    }
  });
  return {
    cancel: function () {
      callback = null;
      client.close();
    }
  };
};

GameViewModelServiceClient.prototype.specialAttackAsync = function specialAttackAsync(requestMessage, metadata, callback) {
  if (arguments.length === 2) {
    callback = arguments[1];
  }
  var client = grpc.unary(GameViewModelService.SpecialAttackAsync, {
    request: requestMessage,
    host: this.serviceHost,
    metadata: metadata,
    transport: this.options.transport,
    debug: this.options.debug,
    onEnd: function (response) {
      if (callback) {
        if (response.status !== grpc.Code.OK) {
          var err = new Error(response.statusMessage);
          err.code = response.status;
          err.metadata = response.trailers;
          callback(err, null);
        } else {
          callback(null, response.message);
        }
      }
    }
  });
  return {
    cancel: function () {
      callback = null;
      client.close();
    }
  };
};

GameViewModelServiceClient.prototype.ping = function ping(requestMessage, metadata, callback) {
  if (arguments.length === 2) {
    callback = arguments[1];
  }
  var client = grpc.unary(GameViewModelService.Ping, {
    request: requestMessage,
    host: this.serviceHost,
    metadata: metadata,
    transport: this.options.transport,
    debug: this.options.debug,
    onEnd: function (response) {
      if (callback) {
        if (response.status !== grpc.Code.OK) {
          var err = new Error(response.statusMessage);
          err.code = response.status;
          err.metadata = response.trailers;
          callback(err, null);
        } else {
          callback(null, response.message);
        }
      }
    }
  });
  return {
    cancel: function () {
      callback = null;
      client.close();
    }
  };
};

exports.GameViewModelServiceClient = GameViewModelServiceClient;

