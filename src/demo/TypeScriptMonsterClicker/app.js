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
const GameViewModelRemoteClient_1 = require("./GameViewModelRemoteClient");
const vm = new GameViewModelRemoteClient_1.GameViewModelRemoteClient();
const initMsg = document.getElementById('init-msg');
const gameContainer = document.getElementById('game');
const monsterNameEl = document.getElementById('monsterName');
const healthBar = document.getElementById('healthBar');
const healthText = document.getElementById('healthText');
const gameMessageEl = document.getElementById('gameMessage');
const attackBtn = document.getElementById('attackBtn');
const specialBtn = document.getElementById('specialAttackBtn');
const resetBtn = document.getElementById('resetBtn');
const cooldownMsg = document.getElementById('cooldownMessage');
function updateUI() {
    monsterNameEl.textContent = vm.monsterName;
    healthBar.max = vm.monsterMaxHealth;
    healthBar.value = vm.monsterCurrentHealth;
    healthText.textContent = `${vm.monsterCurrentHealth} / ${vm.monsterMaxHealth}`;
    gameMessageEl.textContent = vm.gameMessage;
    attackBtn.disabled = vm.isMonsterDefeated;
    specialBtn.disabled = !(vm.canUseSpecialAttack && !vm.isMonsterDefeated && !vm.isSpecialAttackOnCooldown);
    cooldownMsg.style.display = vm.isSpecialAttackOnCooldown ? 'block' : 'none';
}
vm.addEventListener('propertyChanged', updateUI);
attackBtn.addEventListener('click', () => vm.attackMonster());
specialBtn.addEventListener('click', () => vm.specialAttackAsync());
resetBtn.addEventListener('click', () => vm.resetGame());
function start() {
    return __awaiter(this, void 0, void 0, function* () {
        try {
            yield vm.initializeRemote();
            if (initMsg)
                initMsg.style.display = 'none';
            if (gameContainer)
                gameContainer.classList.remove('hidden');
            updateUI();
        }
        catch (err) {
            if (initMsg)
                initMsg.textContent = `Failed to initialize game: ${err}`;
        }
    });
}
start();
//# sourceMappingURL=app.js.map
