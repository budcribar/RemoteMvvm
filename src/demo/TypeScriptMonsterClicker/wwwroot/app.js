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
const GameViewModelServiceServiceClientPb_1 = require("./generated/GameViewModelServiceServiceClientPb");
const GameViewModelRemoteClient_1 = require("./GameViewModelRemoteClient");
const grpcHost = 'http://localhost:50052';
const grpcClient = new GameViewModelServiceServiceClientPb_1.GameViewModelServiceClient(grpcHost);
const vm = new GameViewModelRemoteClient_1.GameViewModelRemoteClient(grpcClient);
function render() {
    return __awaiter(this, void 0, void 0, function* () {
        document.getElementById('monster-name').textContent = vm.monsterName;
        const health = document.getElementById('monster-health');
        health.max = vm.monsterMaxHealth;
        health.value = vm.monsterCurrentHealth;
        document.getElementById('health-text').textContent = `${vm.monsterCurrentHealth} / ${vm.monsterMaxHealth}`;
        document.getElementById('game-message').textContent = vm.gameMessage;
        document.getElementById('connection-status').textContent = vm.connectionStatus;
        const attackBtn = document.getElementById('attack-btn');
        attackBtn.disabled = vm.isMonsterDefeated;
        const specialBtn = document.getElementById('special-btn');
        specialBtn.disabled = !vm.canUseSpecialAttack || vm.isMonsterDefeated || vm.isSpecialAttackOnCooldown;
        const cd = document.getElementById('cooldown-text');
        cd.style.display = vm.isSpecialAttackOnCooldown ? 'block' : 'none';
    });
}
function init() {
    return __awaiter(this, void 0, void 0, function* () {
        try {
            yield vm.initializeRemote();
            document.getElementById('loading').style.display = 'none';
            document.getElementById('game-container').style.display = 'block';
            yield render();
        }
        catch (err) {
            document.getElementById('loading').textContent = 'Failed to initialize.';
            console.error(err);
        }
    });
}
document.addEventListener('DOMContentLoaded', () => {
    init();
    document.getElementById('attack-btn').addEventListener('click', () => __awaiter(void 0, void 0, void 0, function* () {
        yield vm.attackMonster();
        yield vm.refreshState();
        yield render();
    }));
    document.getElementById('special-btn').addEventListener('click', () => __awaiter(void 0, void 0, void 0, function* () {
        yield vm.specialAttackAsync();
        yield vm.refreshState();
        yield render();
    }));
    document.getElementById('newgame-btn').addEventListener('click', () => __awaiter(void 0, void 0, void 0, function* () {
        yield vm.resetGame();
        yield vm.refreshState();
        yield render();
    }));
});
//# sourceMappingURL=app.js.map