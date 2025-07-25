// Auto-generated TypeScript client for GameViewModel
import { GameViewModelServiceClient } from './generated/GameViewModelServiceServiceClientPb.js';
import { GameViewModelState, UpdatePropertyValueRequest, SubscribeRequest, PropertyChangeNotification, ConnectionStatusResponse, ConnectionStatus, AttackMonsterRequest, SpecialAttackAsyncRequest, ResetGameRequest } from './generated/GameViewModelService_pb.js';
import * as grpcWeb from 'grpc-web';
import { Empty } from 'google-protobuf/google/protobuf/empty_pb';
import { Any } from 'google-protobuf/google/protobuf/any_pb';
import { StringValue, Int32Value, BoolValue } from 'google-protobuf/google/protobuf/wrappers_pb';

export class GameViewModelRemoteClient {
    private readonly grpcClient: GameViewModelServiceClient;
    private propertyStream?: grpcWeb.ClientReadableStream<PropertyChangeNotification>;
    private pingIntervalId?: any;
    private changeCallbacks: Array<() => void> = [];

    monsterName: any;
    monsterMaxHealth: any;
    monsterCurrentHealth: any;
    playerDamage: any;
    gameMessage: any;
    isMonsterDefeated: any;
    canUseSpecialAttack: any;
    isSpecialAttackOnCooldown: any;
    connectionStatus: string = 'Unknown';

    addChangeListener(cb: () => void): void {
        this.changeCallbacks.push(cb);
    }

    private notifyChange(): void {
        this.changeCallbacks.forEach(cb => cb());
    }

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
        this.notifyChange();
        this.startListeningToPropertyChanges();
        this.startPingLoop();
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
        this.notifyChange();
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

    private startPingLoop(): void {
        if (this.pingIntervalId) return;
        this.pingIntervalId = setInterval(async () => {
            try {
                const resp: ConnectionStatusResponse = await this.grpcClient.ping(new Empty());
                if (resp.getStatus() === ConnectionStatus.CONNECTED) {
                    if (this.connectionStatus !== 'Connected') {
                        await this.refreshState();
                    }
                    this.connectionStatus = 'Connected';
                } else {
                    this.connectionStatus = 'Disconnected';
                }
            } catch {
                this.connectionStatus = 'Disconnected';
            }
            this.notifyChange();
        }, 5000);
    }

    private startListeningToPropertyChanges(): void {
        const req = new SubscribeRequest();
        req.setClientId(Math.random().toString());
        this.propertyStream = this.grpcClient.subscribeToPropertyChanges(req);
        this.propertyStream.on('data', (update: PropertyChangeNotification) => {
            const anyVal = update.getNewValue();
            switch (update.getPropertyName()) {
                case 'MonsterName':
                    this.monsterName = anyVal?.unpack(StringValue.deserializeBinary, 'google.protobuf.StringValue')?.getValue();
                    break;
                case 'MonsterMaxHealth':
                    this.monsterMaxHealth = anyVal?.unpack(Int32Value.deserializeBinary, 'google.protobuf.Int32Value')?.getValue();
                    break;
                case 'MonsterCurrentHealth':
                    this.monsterCurrentHealth = anyVal?.unpack(Int32Value.deserializeBinary, 'google.protobuf.Int32Value')?.getValue();
                    break;
                case 'PlayerDamage':
                    this.playerDamage = anyVal?.unpack(Int32Value.deserializeBinary, 'google.protobuf.Int32Value')?.getValue();
                    break;
                case 'GameMessage':
                    this.gameMessage = anyVal?.unpack(StringValue.deserializeBinary, 'google.protobuf.StringValue')?.getValue();
                    break;
                case 'IsMonsterDefeated':
                    this.isMonsterDefeated = anyVal?.unpack(BoolValue.deserializeBinary, 'google.protobuf.BoolValue')?.getValue();
                    break;
                case 'CanUseSpecialAttack':
                    this.canUseSpecialAttack = anyVal?.unpack(BoolValue.deserializeBinary, 'google.protobuf.BoolValue')?.getValue();
                    break;
                case 'IsSpecialAttackOnCooldown':
                    this.isSpecialAttackOnCooldown = anyVal?.unpack(BoolValue.deserializeBinary, 'google.protobuf.BoolValue')?.getValue();
                    break;
            }
            this.notifyChange();
        });
        this.propertyStream.on('error', () => {
            this.propertyStream = undefined;
            setTimeout(() => this.startListeningToPropertyChanges(), 1000);
        });
        this.propertyStream.on('end', () => {
            this.propertyStream = undefined;
            setTimeout(() => this.startListeningToPropertyChanges(), 1000);
        });
    }

    dispose(): void {
        if (this.propertyStream) {
            this.propertyStream.cancel();
            this.propertyStream = undefined;
        }
        if (this.pingIntervalId) {
            clearInterval(this.pingIntervalId);
            this.pingIntervalId = undefined;
        }
    }
}
