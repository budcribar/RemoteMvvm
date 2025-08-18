import { HP3LSThermalTestViewModelRemoteClient, ThermalZoneState } from '../ViewModels/generated/HP3LSThermalTestViewModelRemoteClient';
import { ThermalZoneComponentView } from './ThermalZoneComponentView';

export class HP3LSThermalTestView {
    constructor(private readonly vm: HP3LSThermalTestViewModelRemoteClient) {}

    async init(): Promise<void> {
        await this.vm.initializeRemote();
        this.vm.addChangeListener(() => this.render());
        this.render();
    }

    render(): void {
        console.log('Show description:', this.vm.showDescription);
        console.log('Show readme:', this.vm.showReadme);
        this.vm.zones.forEach((zone: ThermalZoneState, key: number) => {
            const view = new ThermalZoneComponentView(zone);
            view.render();
        });
    }
}

