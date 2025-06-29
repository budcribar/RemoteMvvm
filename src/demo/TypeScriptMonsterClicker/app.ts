import { GameViewModelRemoteClient } from './GameViewModelRemoteClient';

const vm = new GameViewModelRemoteClient();

const initMsg = document.getElementById('init-msg') as HTMLElement;
const gameContainer = document.getElementById('game') as HTMLElement;
const monsterNameEl = document.getElementById('monsterName') as HTMLElement;
const healthBar = document.getElementById('healthBar') as HTMLProgressElement;
const healthText = document.getElementById('healthText') as HTMLElement;
const gameMessageEl = document.getElementById('gameMessage') as HTMLElement;
const attackBtn = document.getElementById('attackBtn') as HTMLButtonElement;
const specialBtn = document.getElementById('specialAttackBtn') as HTMLButtonElement;
const resetBtn = document.getElementById('resetBtn') as HTMLButtonElement;
const cooldownMsg = document.getElementById('cooldownMessage') as HTMLElement;

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

async function start() {
    try {
        await vm.initializeRemote();
        if (initMsg) initMsg.style.display = 'none';
        if (gameContainer) gameContainer.classList.remove('hidden');
        updateUI();
    } catch (err) {
        if (initMsg) initMsg.textContent = `Failed to initialize game: ${err}`;
    }
}

start();
