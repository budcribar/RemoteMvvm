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
const empty_pb_1 = require("google-protobuf/google/protobuf/empty_pb");
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
    updatePropertyValue(propertyName, value) {
        return __awaiter(this, void 0, void 0, function* () {
            const req = { propertyName, newValue: value };
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
}
exports.GameViewModelRemoteClient = GameViewModelRemoteClient;
//# sourceMappingURL=GameViewModelRemoteClient.js.map