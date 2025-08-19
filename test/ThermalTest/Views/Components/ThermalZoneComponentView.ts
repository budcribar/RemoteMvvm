import { ThermalZoneState, TestSettingsState } from '../../ViewModels/generated/HP3LSThermalTestViewModelRemoteClient';
import { GaugeComponentView } from './GaugeComponentView';

export enum ThermalStateEnum {
    Unknown,
    MaybeRunningHot,
    MaybeOk,
    RunningHot,
    Ok,
    StressLevelExceeded,
    Pass,
    Fail,
    MaybePass,
    MaybeFail,
    CheckInProgress,
    Reset
}

const StateDescriptions: Record<number, string> = {
    [ThermalStateEnum.Unknown]: 'Initializing',
    [ThermalStateEnum.MaybeRunningHot]: 'Unsupported processor. The unit may be running hot.',
    [ThermalStateEnum.MaybeOk]: 'Unsupported processor. The thermal mechanism appears functional.',
    [ThermalStateEnum.RunningHot]: 'The unit may be running hot.',
    [ThermalStateEnum.Ok]: 'The thermal mechanism appears functional.',
    [ThermalStateEnum.StressLevelExceeded]: 'The operational conditions for the test are outside the allowed boundaries. Try closing some applications to reduce the processor load.',
    [ThermalStateEnum.Pass]: 'The thermal mechanism appears functional.',
    [ThermalStateEnum.Fail]: 'The unit is running hot. Please check the processor cooling solution.',
    [ThermalStateEnum.MaybePass]: 'Unsupported processor. The thermal mechanism appears functional.',
    [ThermalStateEnum.MaybeFail]: 'Unsupported processor. The unit may be running hot.',
    [ThermalStateEnum.CheckInProgress]: 'Please wait for the test to complete.',
    [ThermalStateEnum.Reset]: 'The operational conditions for the test are outside the allowed boundaries. Try closing some applications to reduce the processor load. The test will resume once operational conditions are met.'
};

export class ThermalZoneComponentView {
    constructor(
        private readonly state: ThermalZoneState,
        private readonly settings: TestSettingsState | undefined,
        private readonly processorLoadName: string
    ) {}

    render(): HTMLElement {
        const container = document.createElement('div');
        container.className = 'thermal-zone-container';
        container.style.background = this.state.background;

        if (this.state.status === ThermalStateEnum.CheckInProgress) {
            const progress = document.createElement('div');
            progress.className = 'progress-bar';
            progress.setAttribute('data-label', `${this.state.progress}%`);
            const value = document.createElement('span');
            value.className = 'value';
            value.style.width = `${this.state.progress}%`;
            progress.appendChild(value);
            container.appendChild(progress);
        }

        const runtimeProps = document.createElement('div');
        runtimeProps.className = `runtime-properties-container state-${this.state.state}`;
        runtimeProps.appendChild(this.runtimeProperty(this.state.status === ThermalStateEnum.CheckInProgress ? this.state.state.toString() : this.state.status.toString()));
        runtimeProps.appendChild(this.runtimeProperty(`Max Temp: ${this.getTemperatureThreshold()}° C`));
        runtimeProps.appendChild(this.runtimeProperty(`${this.state.zone}`));
        runtimeProps.appendChild(this.runtimeProperty(`${this.state.fanSpeed} RPM`));
        container.appendChild(runtimeProps);

        const gauges = document.createElement('div');
        gauges.className = 'gauges-container';
        const loadGauge = new GaugeComponentView(this.processorLoadName, this.state.processorLoad, this.settings?.cpuLoadThreshold ?? 0, '%');
        gauges.appendChild(loadGauge.render());
        const tempGauge = new GaugeComponentView(this.state.deviceName, this.state.temperature, this.getTemperatureThreshold(), '° C');
        gauges.appendChild(tempGauge.render());
        container.appendChild(gauges);

        const details = document.createElement('div');
        details.className = 'runtime-details';
        details.textContent = StateDescriptions[this.state.status] ?? '';
        container.appendChild(details);

        return container;
    }

    private runtimeProperty(text: string): HTMLElement {
        const div = document.createElement('div');
        div.className = 'runtime-property';
        div.textContent = text;
        return div;
    }

    private getTemperatureThreshold(): number {
        return this.settings?.cpuTemperatureThreshold ?? 0;
    }
}

