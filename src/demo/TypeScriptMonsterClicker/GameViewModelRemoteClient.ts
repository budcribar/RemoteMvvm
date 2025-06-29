export class GameViewModelRemoteClient extends EventTarget {
    monsterName: string = 'Grumpy Goblin';
    monsterMaxHealth: number = 100;
    monsterCurrentHealth: number = 100;
    playerDamage: number = 10;
    gameMessage: string = 'Click the monster to attack!';
    isMonsterDefeated: boolean = false;
    canUseSpecialAttack: boolean = true;
    isSpecialAttackOnCooldown: boolean = false;
    connectionStatus: string = 'Disconnected';

    async initializeRemote(): Promise<void> {
        this.resetGame();
        this.connectionStatus = 'Connected';
        this.dispatchAll();
    }

    attackMonster(): void {
        if (this.isMonsterDefeated) return;
        this.monsterCurrentHealth -= this.playerDamage;
        if (this.monsterCurrentHealth <= 0) {
            this.monsterCurrentHealth = 0;
            this.gameMessage = `${this.monsterName} defeated! Well done!`;
            this.isMonsterDefeated = true;
        } else {
            this.gameMessage = `Hit ${this.monsterName} for ${this.playerDamage} damage!`;
        }
        this.dispatchAll();
    }

    async specialAttackAsync(): Promise<void> {
        if (!this.canUseSpecialAttack || this.isMonsterDefeated || this.isSpecialAttackOnCooldown) {
            return;
        }
        this.isSpecialAttackOnCooldown = true;
        this.gameMessage = 'Charging special attack...';
        this.dispatchAll();
        await this.delay(750);
        const specialDamage = this.playerDamage * 3;
        this.monsterCurrentHealth -= specialDamage;
        if (this.monsterCurrentHealth <= 0) {
            this.monsterCurrentHealth = 0;
            this.gameMessage = `Critical Hit! ${this.monsterName} obliterated for ${specialDamage} damage!`;
            this.isMonsterDefeated = true;
        } else {
            this.gameMessage = `Special Attack hit ${this.monsterName} for ${specialDamage} damage!`;
        }
        this.dispatchAll();
        this.gameMessage = 'Special Attack on cooldown for 5 seconds...';
        this.dispatch('gameMessage');
        await this.delay(5000);
        this.isSpecialAttackOnCooldown = false;
        this.gameMessage = 'Special Attack ready!';
        this.dispatchAll();
    }

    resetGame(): void {
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

    private dispatch(prop: string) {
        this.dispatchEvent(new CustomEvent('propertyChanged', { detail: prop }));
    }

    private dispatchAll() {
        this.dispatch('monsterName');
        this.dispatch('monsterMaxHealth');
        this.dispatch('monsterCurrentHealth');
        this.dispatch('playerDamage');
        this.dispatch('gameMessage');
        this.dispatch('isMonsterDefeated');
        this.dispatch('canUseSpecialAttack');
        this.dispatch('isSpecialAttackOnCooldown');
    }

    private delay(ms: number) {
        return new Promise(resolve => setTimeout(resolve, ms));
    }
}
