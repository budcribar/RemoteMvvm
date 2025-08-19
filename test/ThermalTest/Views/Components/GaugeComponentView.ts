export class GaugeComponentView {
    constructor(
        private readonly title: string,
        private readonly currentValue: number,
        private readonly maxValue: number,
        private readonly unitOfMeasure: string
    ) {}

    render(): HTMLElement {
        const container = document.createElement('div');
        container.className = 'gauge-container';

        const titleDiv = document.createElement('div');
        titleDiv.className = 'gauge-title';
        titleDiv.textContent = this.title;
        container.appendChild(titleDiv);

        const bodyDiv = document.createElement('div');
        bodyDiv.className = 'gauge-body';

        const filler = document.createElement('div');
        filler.className = 'gauge-filler';
        filler.style.background = this.background();
        filler.style.transform = this.transform();
        bodyDiv.appendChild(filler);

        const cover = document.createElement('div');
        cover.className = 'gauge-cover';
        const coverText = document.createElement('div');
        coverText.textContent = this.text();
        cover.appendChild(coverText);
        bodyDiv.appendChild(cover);

        container.appendChild(bodyDiv);
        return container;
    }

    private transform(): string {
        if (this.maxValue === 0) return 'rotate(0turn)';
        const rotation = Math.min(this.currentValue / this.maxValue, 1) * 0.5;
        return `rotate(${rotation}turn)`;
    }

    private background(): string {
        if (this.maxValue === 0) return '#FFFFFF';
        if (this.currentValue >= this.maxValue) return '#FF0000';
        const perc = (100 * this.currentValue) / this.maxValue;
        const red = Math.round((perc * 255) / 100);
        const green = 255 - red;
        return `#${red.toString(16).padStart(2, '0')}${green
            .toString(16)
            .padStart(2, '0')}00`;
    }

    private text(): string {
        if (this.maxValue === 0) return '-';
        if (this.currentValue >= this.maxValue) return '+';
        return `${this.currentValue}${this.unitOfMeasure}`;
    }
}

