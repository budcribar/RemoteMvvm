// Auto-generated TypeScript client for GameViewModel
import { GameViewModelServiceClient } from './generated/GameViewModelServiceServiceClientPb';
import { GameViewModelState, UpdatePropertyValueRequest, SubscribeRequest, AttackMonsterRequest, SpecialAttackAsyncRequest, ResetGameRequest } from './generated/GameViewModelService_pb';
import { Empty } from 'google-protobuf/google/protobuf/empty_pb';
import { Any } from 'google-protobuf/google/protobuf/any_pb';
import { StringValue, Int32Value, BoolValue } from 'google-protobuf/google/protobuf/wrappers_pb';

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
        const state = await this.grpcClient.getState(new Empty());
        this.monsterName = (state as any).getMonsterName();
        this.monsterMaxHealth = (state as any).getMonsterMaxHealth();
        this.monsterCurrentHealth = (state as any).getMonsterCurrentHealth();
        this.playerDamage = (state as any).getPlayerDamage();
        this.gameMessage = (state as any).getGameMessage();
        this.isMonsterDefeated = (state as any).getIsMonsterDefeated();
        this.canUseSpecialAttack = (state as any).getCanUseSpecialAttack();
        this.isSpecialAttackOnCooldown = (state as any).getIsSpecialAttackOnCooldown();
        this.connectionStatus = 'Connected';
    }

    async refreshState(): Promise<void> {
        const state = await this.grpcClient.getState(new Empty());
        this.monsterName = (state as any).getMonsterName();
        this.monsterMaxHealth = (state as any).getMonsterMaxHealth();
        this.monsterCurrentHealth = (state as any).getMonsterCurrentHealth();
        this.playerDamage = (state as any).getPlayerDamage();
        this.gameMessage = (state as any).getGameMessage();
        this.isMonsterDefeated = (state as any).getIsMonsterDefeated();
        this.canUseSpecialAttack = (state as any).getCanUseSpecialAttack();
        this.isSpecialAttackOnCooldown = (state as any).getIsSpecialAttackOnCooldown();
    }

    async updatePropertyValue(propertyName: string, value: any): Promise<void> {
        const req = new UpdatePropertyValueRequest();
        req.setPropertyName(propertyName);
        req.setNewValue(this.createAnyValue(value));
        await this.grpcClient.updatePropertyValue(req);
    }

    private createAnyValue(value: any): Any {
        const anyVal = new Any();
        if (typeof value === 'string') {
            const wrapper = new StringValue();
            wrapper.setValue(value);
            anyVal.pack(wrapper.serializeBinary(), 'google.protobuf.StringValue');
        } else if (typeof value === 'number' && Number.isInteger(value)) {
            const wrapper = new Int32Value();
            wrapper.setValue(value);
            anyVal.pack(wrapper.serializeBinary(), 'google.protobuf.Int32Value');
        } else if (typeof value === 'boolean') {
            const wrapper = new BoolValue();
            wrapper.setValue(value);
            anyVal.pack(wrapper.serializeBinary(), 'google.protobuf.BoolValue');
        } else {
            throw new Error('Unsupported value type');
        }
        return anyVal;
    }

    async attackMonster(): Promise<void> {
        const req = new AttackMonsterRequest();
        await this.grpcClient.attackMonster(req);
    }
    async specialAttackAsync(): Promise<void> {
        const req = new SpecialAttackAsyncRequest();
        await this.grpcClient.specialAttackAsync(req);
    }
    async resetGame(): Promise<void> {
        const req = new ResetGameRequest();
        await this.grpcClient.resetGame(req);
    }
}
