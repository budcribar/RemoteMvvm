import { HP3LSThermalTestViewModelRemoteClient, ThermalZoneState, TestSettingsState } from '../ViewModels/generated/HP3LSThermalTestViewModelRemoteClient';
import { ThermalZoneComponentView } from './Components/ThermalZoneComponentView';
import { ReadmeComponentView } from './Components/ReadmeComponentView';

export class HP3LSThermalTestView {
    private readonly root: HTMLElement;

    constructor(private readonly vm: HP3LSThermalTestViewModelRemoteClient, rootId: string) {
        const el = document.getElementById(rootId);
        if (!el) {
            throw new Error(`Element with id '${rootId}' not found`);
        }
        this.root = el;
    }

    async init(): Promise<void> {
        await this.vm.initializeRemote();
        this.vm.addChangeListener(() => this.render());
        this.render();
        setInterval(() => this.vm.refreshState(), 5000);
    }

    private createSlider(title: string, value: number, min: number, max: number, step: number, onChange: (v: number) => void): HTMLElement {
        const wrapper = document.createElement('div');
        wrapper.className = 'slider';

        const titleDiv = document.createElement('div');
        titleDiv.className = 'slider-title';
        titleDiv.textContent = title;
        wrapper.appendChild(titleDiv);

        const contentDiv = document.createElement('div');
        contentDiv.className = 'slider-content';

        const inputDiv = document.createElement('div');
        inputDiv.className = 'slider-input';
        const input = document.createElement('input');
        input.type = 'range';
        input.min = String(min);
        input.max = String(max);
        input.step = String(step);
        input.value = String(value);
        input.oninput = () => {
            const v = parseInt(input.value, 10);
            label.textContent = `${v}${title === 'Monitoring period' ? 's' : '%'}`;
            onChange(v);
        };
        inputDiv.appendChild(input);

        const labelDiv = document.createElement('div');
        labelDiv.className = 'slider-label';
        const label = document.createElement('label');
        label.textContent = `${value}${title === 'Monitoring period' ? 's' : '%'}`;
        labelDiv.appendChild(label);

        contentDiv.appendChild(inputDiv);
        contentDiv.appendChild(labelDiv);
        wrapper.appendChild(contentDiv);
        return wrapper;
    }

    private renderZones(settings: TestSettingsState | undefined): HTMLElement {
        const container = document.createElement('div');
        container.className = 'thermal-zones-container';
        this.vm.zones.forEach((zone: ThermalZoneState) => {
            const view = new ThermalZoneComponentView(zone, settings, 'Processor Load');
            container.appendChild(view.render());
        });
        return container;
    }

    render(): void {
        this.root.innerHTML = '';
        const container = document.createElement('div');
        container.className = 'main-page-container';

        const title = document.createElement('div');
        title.className = 'test-title';
        title.textContent = 'CPU Thermal Test';
        container.appendChild(title);

        if (this.vm.showDescription) {
            const desc = document.createElement('div');
            desc.className = 'test-description';
            desc.textContent = 'Instructions for running the thermal test.';
            container.appendChild(desc);
        }

        const settings = this.vm.testSettings;
        const params = document.createElement('div');
        params.className = 'test-parameters';
        params.appendChild(this.createSlider('Temperature threshold to DTS', settings?.cpuTemperatureThreshold ?? 0, 50, 100, 5, v => this.vm.updatePropertyValue('CpuTemperatureThreshold', v)));
        params.appendChild(this.createSlider('Processor maximum load', settings?.cpuLoadThreshold ?? 0, 0, 100, 5, v => this.vm.updatePropertyValue('CpuLoadThreshold', v)));
        params.appendChild(this.createSlider('Monitoring period', settings?.cpuLoadTimeSpan ?? 0, 30, 600, 10, v => this.vm.updatePropertyValue('CpuLoadTimeSpan', v)));
        container.appendChild(params);

        container.appendChild(this.renderZones(settings));

        const options = document.createElement('div');
        options.className = 'show-hide-options';
        const readmeBtn = document.createElement('button');
        readmeBtn.textContent = this.vm.showReadme ? 'Hide Readme' : 'Show Readme';
        readmeBtn.onclick = () => this.vm.updatePropertyValue('ShowReadme', !this.vm.showReadme);
        options.appendChild(readmeBtn);
        const descBtn = document.createElement('button');
        descBtn.textContent = this.vm.showDescription ? 'Hide Description' : 'Show Description';
        descBtn.onclick = () => this.vm.updatePropertyValue('ShowDescription', !this.vm.showDescription);
        options.appendChild(descBtn);
        container.appendChild(options);

        if (this.vm.showReadme) {
            const readme = new ReadmeComponentView();
            container.appendChild(readme.render());
        }

        const bottom = document.createElement('div');
        bottom.className = 'bottom full-width hidden';
        bottom.id = 'wirelessBtnContainer';
        const cancelBtn = document.createElement('button');
        cancelBtn.id = 'wirelessCancelBtn';
        cancelBtn.className = 'hp-btn secondary btn-margin';
        cancelBtn.textContent = 'Cancel';
        cancelBtn.onclick = () => console.log('Cancel test');
        bottom.appendChild(cancelBtn);
        container.appendChild(bottom);

        this.root.appendChild(container);
    }
}

