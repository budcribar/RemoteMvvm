import { ThermalZoneState } from '../ViewModels/generated/HP3LSThermalTestViewModelRemoteClient';

export class ThermalZoneComponentView {
    constructor(private readonly state: ThermalZoneState) {}

    render(): void {
        console.log(`Zone ${this.state.zone}: ${this.state.temperature}°C, load ${this.state.processorLoad}%`);
    }
}

