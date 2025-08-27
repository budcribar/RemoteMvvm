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
      'active', 'background', 'status', 'state', 'progress', 'zone', 'zone-index', 'fan-speed',
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
        /* Container */
        .thermal-zone-container {
          display: flex;
          flex-flow: column wrap;
          border: 1px solid #ccc;
          width: 90%;
          border-radius: 8px;
          padding: 12px;
          background: #fafafa;
        }

        /* Progress */
        .test-progress { width: 100%; }
        .progress-bar {
          height: 1.5em;
          width: 100%;
          background-color: #eee;
          position: relative;
          border-radius: 7px;
          overflow: hidden;
          margin-bottom: 10px;
        }
        .progress-bar:before {
          content: attr(data-label);
          font-size: 0.8em;
          position: absolute;
          text-align: center;
          top: 5px;
          left: 0; right: 0;
        }
        .progress-bar .value {
          background-color: #ccc;
          display: inline-block;
          height: 100%;
          width: 0;
          transition: width 200ms ease;
        }

        /* Runtime properties */
        .runtime-properties-container {
          display: flex;
          flex-flow: row wrap;
          justify-content: space-evenly;
          gap: 8px;
          margin: 8px 0;
        }
        .runtime-properties { text-align: center; }
        .runtime-property {
          background: #fff;
          border-radius: 6px;
          padding: 8px;
          box-shadow: 0 1px 2px rgba(0,0,0,0.06);
          text-align: center;
          font-weight: 600;
          min-width: 120px;
        }

        /* Gauges */
        .gauges-container {
          display: flex;
          flex-flow: row wrap;
          justify-content: space-evenly;
          align-items: center;
          margin: 8px 0;
          gap: 12px;
        }
        .thermal-gauge { width: 50%; }

        /* Details */
        .runtime-details { text-align: center; color: #333; margin-top: 8px; }

        /* State backgrounds */
        .state-RunningHot, .state-MaybeRunningHot, .state-Fail, .state-MaybeFail { background: #faa; }
        .state-Ok, .state-MaybeOk, .state-Pass, .state-MaybePass { background: #afa; }
        .state-Unknown, .state-StressLevelExceeded, .state-Reset { background: #ccc; }
        .state-CheckInProgress { background: #ffa; }

        /* Tables (not used by markup, included per spec) */
        .thermal-zone-table { border: 1px solid #ccc; width: 100%; }
        .thermal-parameter { text-align: center; font-size: .8rem; }
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
  const runtimeProps = this.root.getElementById('runtimeProps') as HTMLDivElement;

  const maxTemp = this.num('max-temp');
  const zoneLabel = this.getAttribute('zone-label') ?? '';
  const fan = this.num('fan-speed');
  const primary = status === 'CheckInProgress' ? state : status;
  propPrimary.textContent = primary;
  propMaxTemp.textContent = `Max Temp: ${maxTemp}\u00B0 C`;
  propZone.textContent = zoneLabel;
  propFan.textContent = `${fan} RPM`;

    // Apply state-based class styling (supports either textual status/state values)
    const sanitize = (s: string) => (s || '').toString().replace(/\s+/g, '').replace(/[^\w-]/g, '');
    const stateClassCandidates = [sanitize(state), sanitize(status)].filter(Boolean);
    // Reset to base class, then add the first matching textual class
    runtimeProps.className = 'runtime-properties-container';
    for (const cls of stateClassCandidates) {
      if (/\D/.test(cls)) { // has non-digit characters -> likely a name like CheckInProgress
        runtimeProps.classList.add(`state-${cls}`);
        break;
      }
    }

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
  gaugeCpu.setAttribute('title', cpuTitle);
  gaugeCpu.setAttribute('unit', '%');
  gaugeCpu.setAttribute('current', String(cpuVal));
  gaugeCpu.setAttribute('max', String(Math.max(0, cpuMax)));

  const deviceName = this.str('device-name');
  const tempVal = this.num('temperature');
  const tempMax = maxTemp;
  gaugeTemp.setAttribute('title', deviceName);
  gaugeTemp.setAttribute('unit', '\u00B0 C');
  gaugeTemp.setAttribute('current', String(tempVal));
  gaugeTemp.setAttribute('max', String(Math.max(0, tempMax)));

    // Details
    const details = this.root.getElementById('details') as HTMLDivElement;
    const sdStatus = this.desc[status] ?? status;
    const sdState = this.desc[state] ?? state;
    const detailText = status === 'CheckInProgress' ? `${sdStatus} ${sdState}`.trim() : sdStatus;
    details.textContent = detailText;
  }
}

customElements.define('x-thermal-zone', ThermalZoneElement);
