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
class GameViewModelRemoteClient extends EventTarget {
    constructor() {
        super(...arguments);
        this.monsterName = 'Grumpy Goblin';
        this.monsterMaxHealth = 100;
        this.monsterCurrentHealth = 100;
        this.playerDamage = 10;
        this.gameMessage = 'Click the monster to attack!';
        this.isMonsterDefeated = false;
        this.canUseSpecialAttack = true;
        this.isSpecialAttackOnCooldown = false;
        this.connectionStatus = 'Disconnected';
    }
    initializeRemote() {
        return __awaiter(this, void 0, void 0, function* () {
            this.resetGame();
            this.connectionStatus = 'Connected';
            this.dispatchAll();
        });
    }
    attackMonster() {
        if (this.isMonsterDefeated)
            return;
        this.monsterCurrentHealth -= this.playerDamage;
        if (this.monsterCurrentHealth <= 0) {
            this.monsterCurrentHealth = 0;
            this.gameMessage = `${this.monsterName} defeated! Well done!`;
            this.isMonsterDefeated = true;
        }
        else {
            this.gameMessage = `Hit ${this.monsterName} for ${this.playerDamage} damage!`;
        }
        this.dispatchAll();
    }
    specialAttackAsync() {
        return __awaiter(this, void 0, void 0, function* () {
            if (!this.canUseSpecialAttack || this.isMonsterDefeated || this.isSpecialAttackOnCooldown) {
                return;
            }
            this.isSpecialAttackOnCooldown = true;
            this.gameMessage = 'Charging special attack...';
            this.dispatchAll();
            yield this.delay(750);
            const specialDamage = this.playerDamage * 3;
            this.monsterCurrentHealth -= specialDamage;
            if (this.monsterCurrentHealth <= 0) {
                this.monsterCurrentHealth = 0;
                this.gameMessage = `Critical Hit! ${this.monsterName} obliterated for ${specialDamage} damage!`;
                this.isMonsterDefeated = true;
            }
            else {
                this.gameMessage = `Special Attack hit ${this.monsterName} for ${specialDamage} damage!`;
            }
            this.dispatchAll();
            this.gameMessage = 'Special Attack on cooldown for 5 seconds...';
            this.dispatch('gameMessage');
            yield this.delay(5000);
            this.isSpecialAttackOnCooldown = false;
            this.gameMessage = 'Special Attack ready!';
            this.dispatchAll();
        });
    }
    resetGame() {
        this.monsterName = 'Grumpy Goblin';
        this.monsterMaxHealth = 100;
        this.monsterCurrentHealth = this.monsterMaxHealth;
        this.playerDamage = 10;
        this.gameMessage = 'A new monster appears! Click it!';
        this.isMonsterDefeated = false;
        this.isSpecialAttackOnCooldown = false;
        this.canUseSpecialAttack = true;
        this.dispatchAll();
    }
    dispatch(prop) {
        this.dispatchEvent(new CustomEvent('propertyChanged', { detail: prop }));
    }
    dispatchAll() {
        this.dispatch('monsterName');
        this.dispatch('monsterMaxHealth');
        this.dispatch('monsterCurrentHealth');
        this.dispatch('playerDamage');
        this.dispatch('gameMessage');
        this.dispatch('isMonsterDefeated');
        this.dispatch('canUseSpecialAttack');
        this.dispatch('isSpecialAttackOnCooldown');
    }
    delay(ms) {
        return new Promise(resolve => setTimeout(resolve, ms));
    }
}
exports.GameViewModelRemoteClient = GameViewModelRemoteClient;
//# sourceMappingURL=GameViewModelRemoteClient.js.map
