// Web Component: <x-thermal-zone>
// Mirrors the Razor fragment for a thermal zone with progress, properties, gauges, and details.
// Attributes:
// - active: "true" | "false" (if falsey, the component hides itself)
// - background: CSS color string for container background
// - status: string (e.g., CheckInProgress)
// - state: string
// - progress: number 0-100
// - zone: string
// - fan-speed: number (RPM)
// - device-name: string
// - temperature: number (current temp)
// - max-temp: number (threshold)
// - processor-load-name: string (title for CPU load gauge)
// - processor-load: number (current CPU load percent)
// - cpu-load-threshold: number (max for CPU load)
// - state-descriptions: JSON string mapping { [key: string]: string }
//
// Uses the existing <x-gauge> component for the gauges.

import './gauge';

type Descriptions = Record<string, string>;

export class ThermalZoneElement extends HTMLElement {
  static get observedAttributes() {
    return [
      'active', 'background', 'status', 'state', 'progress', 'zone', 'fan-speed',
      'device-name', 'temperature', 'max-temp', 'processor-load-name',
      'processor-load', 'cpu-load-threshold', 'state-descriptions'
    ];
  }

  private root: ShadowRoot;
  private desc: Descriptions = {};

  constructor() {
    super();
    this.root = this.attachShadow({ mode: 'open' });
    this.root.innerHTML = `
      <style>
        :host { display: block; font-family: system-ui, Segoe UI, Roboto, Arial, sans-serif; color: #1a1a1a; }
        .thermal-zone-container { border-radius: 8px; padding: 12px; background: #fafafa; box-shadow: inset 0 0 0 1px rgba(0,0,0,0.06); }
        .progress-bar { position: relative; height: 14px; border-radius: 7px; background: #e9e9e9; overflow: hidden; margin-bottom: 10px; }
        .progress-bar .value { display: block; height: 100%; background: linear-gradient(90deg,#4caf50,#81c784); width: 0; transition: width 200ms ease; }
        .progress-bar::after { content: attr(data-label); position: absolute; inset: 0; display: grid; place-items: center; font-size: 12px; color: #222; text-shadow: 0 1px 0 rgba(255,255,255,0.5); }

        .runtime-properties-container { display: grid; grid-template-columns: repeat(4, minmax(0, 1fr)); gap: 8px; margin: 8px 0; }
        .runtime-property { background: #fff; border-radius: 6px; padding: 8px; box-shadow: 0 1px 2px rgba(0,0,0,0.06); text-align: center; font-weight: 600; }

        .gauges-container { display: grid; grid-template-columns: repeat(2, minmax(220px, 1fr)); gap: 12px; align-items: center; margin: 8px 0; }
        .runtime-details { margin-top: 8px; color: #333; }
      </style>
      <div id="container" class="thermal-zone-container">
        <div id="progressWrap" class="progress-wrap" hidden>
          <div id="progress" class="progress-bar" data-label="0%">
            <span id="progressValue" class="value" style="width: 0%"></span>
          </div>
        </div>

        <div id="runtimeProps" class="runtime-properties-container">
          <div id="propPrimary" class="runtime-property"></div>
          <div id="propMaxTemp" class="runtime-property"></div>
          <div id="propZone" class="runtime-property"></div>
          <div id="propFan" class="runtime-property"></div>
        </div>

        <div class="gauges-container">
          <x-gauge id="gaugeCpu" title="" text="" background="#2196f3" transform="scaleX(0)"></x-gauge>
          <x-gauge id="gaugeTemp" title="" text="" background="#e53935" transform="scaleX(0)"></x-gauge>
        </div>

        <div id="details" class="runtime-details"></div>
      </div>
    `;
  }

  connectedCallback() {
    this.renderAll();
  }

  attributeChangedCallback() {
    if (!this.isConnected) return;
    this.renderAll();
  }

  // Helpers to read attributes safely
  private str(name: string, def = ''): string {
    const v = this.getAttribute(name);
    return v == null ? def : v;
  }
  private num(name: string, def = 0): number {
    const v = this.getAttribute(name);
    const n = v == null ? NaN : Number(v);
    return Number.isFinite(n) ? n : def;
  }
  private bool(name: string, def = false): boolean {
    const v = this.getAttribute(name);
    if (v == null) return def;
    return v === '' || v.toLowerCase() === 'true' || v === '1';
  }

  private renderAll() {
    const container = this.root.getElementById('container') as HTMLDivElement;
    const active = this.bool('active', true);
    if (!active) {
      container.style.display = 'none';
      return;
    }
    container.style.display = '';

    // Background
    container.style.background = this.str('background', '#fafafa');

    // Progress visibility
  const status = this.str('status');
  const state = this.str('state');
  const progress = this.num('progress');
  const showProgress = status === 'CheckInProgress' || progress > 0;
    const progressWrap = this.root.getElementById('progressWrap') as HTMLDivElement;
    const progressBar = this.root.getElementById('progress') as HTMLDivElement;
    const progressValue = this.root.getElementById('progressValue') as HTMLSpanElement;
    progressWrap.hidden = !showProgress;
    if (showProgress) {
      const pct = Math.max(0, Math.min(100, progress));
      progressBar.setAttribute('data-label', `${pct}%`);
      progressValue.style.width = `${pct}%`;
    }

    // Runtime props
    const propPrimary = this.root.getElementById('propPrimary') as HTMLDivElement;
    const propMaxTemp = this.root.getElementById('propMaxTemp') as HTMLDivElement;
    const propZone = this.root.getElementById('propZone') as HTMLDivElement;
    const propFan = this.root.getElementById('propFan') as HTMLDivElement;

    const maxTemp = this.num('max-temp');
    const zone = this.str('zone');
    const fan = this.num('fan-speed');
    const primary = status === 'CheckInProgress' ? state : status;
    propPrimary.textContent = primary;
    propMaxTemp.textContent = `Max Temp: ${maxTemp}\u00B0 C`;
    propZone.textContent = zone;
    propFan.textContent = `${fan} RPM`;

    // Parse descriptions JSON lazily
    try {
      const descAttr = this.getAttribute('state-descriptions');
      if (descAttr) this.desc = JSON.parse(descAttr) as Descriptions;
    } catch {
      // ignore bad JSON
    }

    // Gauges
    const gaugeCpu = this.root.getElementById('gaugeCpu') as HTMLElement;
    const gaugeTemp = this.root.getElementById('gaugeTemp') as HTMLElement;

    const cpuTitle = this.str('processor-load-name');
    const cpuVal = this.num('processor-load');
    const cpuMax = this.num('cpu-load-threshold');
    const cpuRatio = cpuMax > 0 ? Math.max(0, Math.min(1, cpuVal / cpuMax)) : 0;
    gaugeCpu.setAttribute('title', cpuTitle);
    gaugeCpu.setAttribute('text', `${cpuVal}%`);
    gaugeCpu.setAttribute('transform', `scaleX(${cpuRatio.toFixed(3)})`);

    const deviceName = this.str('device-name');
    const tempVal = this.num('temperature');
    const tempMax = maxTemp;
    const tempRatio = tempMax > 0 ? Math.max(0, Math.min(1, tempVal / tempMax)) : 0;
    gaugeTemp.setAttribute('title', deviceName);
    gaugeTemp.setAttribute('text', `${tempVal}\u00B0 C`);
    gaugeTemp.setAttribute('transform', `scaleX(${tempRatio.toFixed(3)})`);

    // Details
    const details = this.root.getElementById('details') as HTMLDivElement;
    const sdStatus = this.desc[status] ?? status;
    const sdState = this.desc[state] ?? state;
    const detailText = status === 'CheckInProgress' ? `${sdStatus} ${sdState}`.trim() : sdStatus;
    details.textContent = detailText;
  }
}

customElements.define('x-thermal-zone', ThermalZoneElement);
