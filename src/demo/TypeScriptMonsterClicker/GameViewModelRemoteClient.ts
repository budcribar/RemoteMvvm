// Auto-generated TypeScript client for GameViewModel
import { GameViewModelServiceClient } from './generated/GameViewModelService_pb_service';
import { GameViewModelState, UpdatePropertyValueRequest, SubscribeRequest } from './generated/GameViewModelService_pb';
import { Empty } from 'google-protobuf/google/protobuf/empty_pb';

export class GameViewModelRemoteClient {
    private readonly grpcClient: GameViewModelServiceClient;

    monsterName: any;
    monsterMaxHealth: any;
    monsterCurrentHealth: any;
    playerDamage: any;
    gameMessage: any;
    isMonsterDefeated: any;
    canUseSpecialAttack: any;
    isSpecialAttackOnCooldown: any;
    connectionStatus: string = 'Unknown';

    constructor(grpcClient: GameViewModelServiceClient) {
        this.grpcClient = grpcClient;
    }

    async initializeRemote(): Promise<void> {
        const state = await new Promise<GameViewModelState>((resolve, reject) => {
            this.grpcClient.getState(new Empty(), (err, res) => {
                if (err) reject(err); else resolve(res!);
            });
        });
        this.monsterName = (state as any)['monster_name'];
        this.monsterMaxHealth = (state as any)['monster_max_health'];
        this.monsterCurrentHealth = (state as any)['monster_current_health'];
        this.playerDamage = (state as any)['player_damage'];
        this.gameMessage = (state as any)['game_message'];
        this.isMonsterDefeated = (state as any)['is_monster_defeated'];
        this.canUseSpecialAttack = (state as any)['can_use_special_attack'];
        this.isSpecialAttackOnCooldown = (state as any)['is_special_attack_on_cooldown'];
        this.connectionStatus = 'Connected';
    }

    async updatePropertyValue(propertyName: string, value: any): Promise<void> {
        const req: UpdatePropertyValueRequest = { propertyName, newValue: value }; 
        await new Promise<void>((resolve, reject) => {
            this.grpcClient.updatePropertyValue(req, (err) => {
                if (err) reject(err); else resolve();
            });
        });
    }

}
