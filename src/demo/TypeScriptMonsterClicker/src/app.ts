import { GameViewModelServiceClient } from './generated/GameViewModelServiceServiceClientPb';
import { GameViewModelRemoteClient } from './GameViewModelRemoteClient';

const grpcHost = 'http://localhost:50052';
const grpcClient = new GameViewModelServiceClient(grpcHost);
const vm = new GameViewModelRemoteClient(grpcClient);

async function render() {
    (document.getElementById('monster-name') as HTMLElement).textContent = vm.monsterName;
    const health = document.getElementById('monster-health') as HTMLProgressElement;
    health.max = vm.monsterMaxHealth;
    health.value = vm.monsterCurrentHealth;
    (document.getElementById('health-text') as HTMLElement).textContent = `${vm.monsterCurrentHealth} / ${vm.monsterMaxHealth}`;
    (document.getElementById('game-message') as HTMLElement).textContent = vm.gameMessage;
    (document.getElementById('connection-status') as HTMLElement).textContent = vm.connectionStatus;
    const attackBtn = document.getElementById('attack-btn') as HTMLButtonElement;
    attackBtn.disabled = vm.isMonsterDefeated;
    const specialBtn = document.getElementById('special-btn') as HTMLButtonElement;
    specialBtn.disabled = !vm.canUseSpecialAttack || vm.isMonsterDefeated || vm.isSpecialAttackOnCooldown;
    const cd = document.getElementById('cooldown-text') as HTMLElement;
    cd.style.display = vm.isSpecialAttackOnCooldown ? 'block' : 'none';
}

async function init() {
    try {
        await vm.initializeRemote();
        vm.addChangeListener(render);
        document.getElementById('loading')!.style.display = 'none';
        document.getElementById('game-container')!.style.display = 'block';
        await render();
    } catch (err) {
        document.getElementById('loading')!.textContent = 'Failed to initialize.';
        console.error(err);
    }
}

document.addEventListener('DOMContentLoaded', () => {
    init();
    (document.getElementById('attack-btn') as HTMLButtonElement).addEventListener('click', async () => {
        await vm.attackMonster();
        await vm.refreshState();
        await render();
    });
    (document.getElementById('special-btn') as HTMLButtonElement).addEventListener('click', async () => {
        await vm.specialAttackAsync();
        // Special attack executes asynchronously on the server. Give it a
        // moment to update the ViewModel before refreshing the client state.
        await new Promise(r => setTimeout(r, 800));
        await vm.refreshState();
        await render();
    });
    (document.getElementById('newgame-btn') as HTMLButtonElement).addEventListener('click', async () => {
        await vm.resetGame();
        await vm.refreshState();
        await render();
    });
});
