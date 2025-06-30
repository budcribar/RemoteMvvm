"use strict";
var __awaiter = (this && this.__awaiter) || function (thisArg, _arguments, P, generator) {
    function adopt(value) { return value instanceof P ? value : new P(function (resolve) { resolve(value); }); }
    return new (P || (P = Promise))(function (resolve, reject) {
        function fulfilled(value) { try { step(generator.next(value)); } catch (e) { reject(e); } }
        function rejected(value) { try { step(generator["throw"](value)); } catch (e) { reject(e); } }
        function step(result) { result.done ? resolve(result.value) : adopt(result.value).then(fulfilled, rejected); }
        step((generator = generator.apply(thisArg, _arguments || [])).next());
    });
};
Object.defineProperty(exports, "__esModule", { value: true });
exports.GameViewModelRemoteClient = void 0;
const GameViewModelService_pb_1 = require("./generated/GameViewModelService_pb");
const empty_pb_1 = require("google-protobuf/google/protobuf/empty_pb");
const any_pb_1 = require("google-protobuf/google/protobuf/any_pb");
const wrappers_pb_1 = require("google-protobuf/google/protobuf/wrappers_pb");
class GameViewModelRemoteClient {
    constructor(grpcClient) {
        this.connectionStatus = 'Unknown';
        this.grpcClient = grpcClient;
    }
    initializeRemote() {
        return __awaiter(this, void 0, void 0, function* () {
            const state = yield new Promise((resolve, reject) => {
                this.grpcClient.getState(new empty_pb_1.Empty(), (err, res) => {
                    if (err)
                        reject(err);
                    else
                        resolve(res);
                });
            });
            this.monsterName = state['monster_name'];
            this.monsterMaxHealth = state['monster_max_health'];
            this.monsterCurrentHealth = state['monster_current_health'];
            this.playerDamage = state['player_damage'];
            this.gameMessage = state['game_message'];
            this.isMonsterDefeated = state['is_monster_defeated'];
            this.canUseSpecialAttack = state['can_use_special_attack'];
            this.isSpecialAttackOnCooldown = state['is_special_attack_on_cooldown'];
            this.connectionStatus = 'Connected';
        });
    }
    refreshState() {
        return __awaiter(this, void 0, void 0, function* () {
            const state = yield new Promise((resolve, reject) => {
                this.grpcClient.getState(new empty_pb_1.Empty(), (err, res) => {
                    if (err)
                        reject(err);
                    else
                        resolve(res);
                });
            });
            this.monsterName = state['monster_name'];
            this.monsterMaxHealth = state['monster_max_health'];
            this.monsterCurrentHealth = state['monster_current_health'];
            this.playerDamage = state['player_damage'];
            this.gameMessage = state['game_message'];
            this.isMonsterDefeated = state['is_monster_defeated'];
            this.canUseSpecialAttack = state['can_use_special_attack'];
            this.isSpecialAttackOnCooldown = state['is_special_attack_on_cooldown'];
        });
    }
    updatePropertyValue(propertyName, value) {
        return __awaiter(this, void 0, void 0, function* () {
            const req = new GameViewModelService_pb_1.UpdatePropertyValueRequest();
            req.setPropertyName(propertyName);
            req.setNewValue(this.createAnyValue(value));
            yield new Promise((resolve, reject) => {
                this.grpcClient.updatePropertyValue(req, (err) => {
                    if (err)
                        reject(err);
                    else
                        resolve();
                });
            });
        });
    }
    createAnyValue(value) {
        const anyVal = new any_pb_1.Any();
        if (typeof value === 'string') {
            const wrapper = new wrappers_pb_1.StringValue();
            wrapper.setValue(value);
            anyVal.pack(wrapper.serializeBinary(), 'google.protobuf.StringValue');
        }
        else if (typeof value === 'number' && Number.isInteger(value)) {
            const wrapper = new wrappers_pb_1.Int32Value();
            wrapper.setValue(value);
            anyVal.pack(wrapper.serializeBinary(), 'google.protobuf.Int32Value');
        }
        else if (typeof value === 'boolean') {
            const wrapper = new wrappers_pb_1.BoolValue();
            wrapper.setValue(value);
            anyVal.pack(wrapper.serializeBinary(), 'google.protobuf.BoolValue');
        }
        else {
            throw new Error('Unsupported value type');
        }
        return anyVal;
    }
    attackMonster() {
        return __awaiter(this, void 0, void 0, function* () {
            const req = new GameViewModelService_pb_1.AttackMonsterRequest();
            yield new Promise((resolve, reject) => {
                this.grpcClient.attackMonster(req, (err) => {
                    if (err)
                        reject(err);
                    else
                        resolve();
                });
            });
        });
    }
    specialAttackAsync() {
        return __awaiter(this, void 0, void 0, function* () {
            const req = new GameViewModelService_pb_1.SpecialAttackAsyncRequest();
            yield new Promise((resolve, reject) => {
                this.grpcClient.specialAttackAsync(req, (err) => {
                    if (err)
                        reject(err);
                    else
                        resolve();
                });
            });
        });
    }
    resetGame() {
        return __awaiter(this, void 0, void 0, function* () {
            const req = new GameViewModelService_pb_1.ResetGameRequest();
            yield new Promise((resolve, reject) => {
                this.grpcClient.resetGame(req, (err) => {
                    if (err)
                        reject(err);
                    else
                        resolve();
                });
            });
        });
    }
}
exports.GameViewModelRemoteClient = GameViewModelRemoteClient;
//# sourceMappingURL=GameViewModelRemoteClient.js.map