// Web Component: <x-thermal-main>
// Encapsulates the main CPU Thermal Test page UI translated from the Razor snippet.
// Attributes:
// - title: string (default "CPU Thermal Test")
// - show-description: boolean
// - instructions: string (text content for description)
// - show-readme: boolean
// - label-temp-threshold, label-processor-max-load, label-monitoring-period: strings
// - temp-threshold: number (50-100)
// - cpu-load-threshold: number (0-100)
// - cpu-load-time: number (seconds 30-600)
// - readme-show-previous: boolean (passes to <x-readme>)
// - label-show-readme, label-hide-readme, label-show-description, label-hide-description, label-cancel: strings
// - zones: JSON string array of zone objects to render using <x-thermal-zone>
//
// Events dispatched:
// - change-temp-threshold { detail: { value: number } }
// - change-cpu-load-threshold { detail: { value: number } }
// - change-cpu-load-time { detail: { value: number } }
// - toggle-readme { detail: { value: boolean } }
// - toggle-description { detail: { value: boolean } }
// - cancel { detail: {} }

import './thermal-zone';
import './readme';

export class ThermalMainElement extends HTMLElement {
  private sliderDragging: { [key: string]: boolean } = {};
  static get observedAttributes() {
    return [
      'title', 'show-description', 'instructions', 'show-readme',
      'label-temp-threshold', 'label-processor-max-load', 'label-monitoring-period',
      'temp-threshold', 'cpu-load-threshold', 'cpu-load-time',
      'readme-show-previous',
      'label-show-readme', 'label-hide-readme', 'label-show-description', 'label-hide-description', 'label-cancel',
      'zones'
    ];
  }

  private root: ShadowRoot;

  constructor() {
    super();
    this.root = this.attachShadow({ mode: 'open' });
    this.root.innerHTML = `
      <style>
        :host { display: block; font-family: system-ui, Segoe UI, Roboto, Arial, sans-serif; color: #1a1a1a; }
        /* Provided layout styles */
        .container {
          display: flex;
          justify-content: space-evenly;
          flex-flow: row wrap;
          align-items: center;
          justify-items: center;
        }
        .main-page-container {
          display: flex;
          flex-flow: column wrap;
          justify-content: space-around;
          align-content: space-around;
          gap: 1em;
        }
        .test-title {
          text-align: center;
          width: 100%;
          font-weight: bolder;
          font-size: 1.1rem;
        }
        .test-description { /* keep default block; styling optional */ }
        .test-parameters {
          display: flex;
          flex-flow: row wrap;
          justify-content: space-evenly;
        }
        .show-hide-options {
          display: flex;
          flex-flow: row wrap;
          justify-content: space-around;
          align-content: space-around;
        }
        .slider {
          display: flex;
          flex-flow: column nowrap;
          justify-content: space-evenly;
          align-self: center;
          font-size: .8rem;
          background: #fff;
          border-radius: 8px;
          padding: 10px 12px;
          box-shadow: 0 1px 2px rgba(0,0,0,0.06);
        }
        .slider-title {
          align-self: center;
          font-weight: bolder;
          margin-bottom: 8px;
        }
        .slider-content {
          display: flex;
          flex-flow: row nowrap;
          justify-content: space-evenly;
          align-items: center;
          gap: 12px;
        }
        .slider-input input[type=range] { width: 260px; max-width: 60vw; }
        .slider-label label { font-weight: 600; min-width: 48px; display: inline-block; text-align: right; }
        .thermal-zones-container {
          display: flex;
          flex-flow: column wrap;
          justify-content: space-around;
          align-items: center;
          gap: 12px;
        }

        /* Buttons and misc */
        button { appearance: none; border: 1px solid rgba(0,0,0,0.1); background: #f4f6f8; padding: 6px 10px; border-radius: 6px; cursor: pointer; }
        button:hover { background: #eef1f5; }
            .bottom { display: flex; justify-content: flex-end; }
            .center-cancel { justify-content: center !important; }
        .hidden { display: none; }
      </style>
      <div class="main-page-container">
        <div class="test-title" id="title"></div>
        <div id="descriptionWrap" class="test-description" hidden>
          <span id="instructions"></span>
        </div>

        <div class="test-parameters">
          <div class="slider">
            <div class="slider-title" id="lblTemp"></div>
            <div class="slider-content">
              <div class="slider-input"><input id="sliderTemp" type="range" min="50" max="100" step="5"></div>
              <div class="slider-label"><label id="valTemp" for="sliderTemp">0%</label></div>
            </div>
          </div>
          <div class="slider">
            <div class="slider-title" id="lblCpuLoad"></div>
            <div class="slider-content">
              <div class="slider-input"><input id="sliderCpuLoad" type="range" min="0" max="100" step="5"></div>
              <div class="slider-label"><label id="valCpuLoad" for="sliderCpuLoad">0%</label></div>
            </div>
          </div>
          <div class="slider">
            <div class="slider-title" id="lblMonitor"></div>
            <div class="slider-content">
              <div class="slider-input"><input id="sliderTime" type="range" min="30" max="600" step="10"></div>
              <div class="slider-label"><label id="valTime" for="sliderTime">0s</label></div>
            </div>
          </div>
        </div>

        <div id="zones" class="thermal-zones-container"></div>

        <div class="show-hide-options">
          <button id="btnReadme"></button>
          <button id="btnDesc"></button>
        </div>

            <div class="bottom full-width center-cancel" id="cancelBtnContainer">
              <button id="btnCancel" class="hp-btn secondary btn-margin">Cancel</button>
            </div>

        <div id="readmeWrap" hidden>
          <x-readme id="readme"></x-readme>
        </div>
      </div>
    `;
  }

  connectedCallback() {
    const $ = (id: string) => this.root.getElementById(id)!;
    // Wire slider events with drag tracking
    const sliders = [
      { id: 'sliderTemp', label: 'valTemp', event: 'change-temp-threshold', format: (v: number) => `${v}%` },
      { id: 'sliderCpuLoad', label: 'valCpuLoad', event: 'change-cpu-load-threshold', format: (v: number) => `${v}%` },
      { id: 'sliderTime', label: 'valTime', event: 'change-cpu-load-time', format: (v: number) => `${v}s` },
    ];
    for (const s of sliders) {
      const slider = $(s.id) as HTMLInputElement;
      const label = $(s.label) as HTMLLabelElement;
      this.sliderDragging[s.id] = false;
      slider.addEventListener('pointerdown', () => { this.sliderDragging[s.id] = true; });
      slider.addEventListener('pointerup', () => { this.sliderDragging[s.id] = false; });
      slider.addEventListener('pointercancel', () => { this.sliderDragging[s.id] = false; });
      slider.addEventListener('input', () => {
        const v = Number(slider.value);
        label.textContent = s.format(v);
        this.dispatchEvent(new CustomEvent(s.event, { detail: { value: v } }));
      });
    }

    // Toggle buttons
    $('btnReadme').addEventListener('click', () => {
      const curr = this.bool('show-readme');
      this.setAttribute('show-readme', String(!curr));
      this.dispatchEvent(new CustomEvent('toggle-readme', { detail: { value: !curr } }));
    });
    $('btnDesc').addEventListener('click', () => {
      const curr = this.bool('show-description');
      this.setAttribute('show-description', String(!curr));
      this.dispatchEvent(new CustomEvent('toggle-description', { detail: { value: !curr } }));
    });

    // Cancel button
    $('btnCancel').addEventListener('click', () => {
      this.dispatchEvent(new CustomEvent('cancel', { detail: {} }));
    });

    this.renderAll();
  }

  attributeChangedCallback() {
    if (!this.isConnected) return;
    this.renderAll();
  }

  // Helpers
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
    const $ = (id: string) => this.root.getElementById(id)!;

    // Title
    $('title').textContent = this.str('title', 'CPU Thermal Test');

  // Description: always hidden, now shown only in modal
  const descWrap = $('descriptionWrap');
  descWrap.toggleAttribute('hidden', true);
  $('instructions').textContent = this.str('instructions');

    // Labels
    $('lblTemp').textContent = this.str('label-temp-threshold', 'Temperature threshold to DTS');
    $('lblCpuLoad').textContent = this.str('label-processor-max-load', 'Processor maximum load');
    $('lblMonitor').textContent = this.str('label-monitoring-period', 'Monitoring period');

    // Slider values (respect provided current values)
    const temp = this.num('temp-threshold', 90);
    const cpuLoad = this.num('cpu-load-threshold', 15);
    const time = this.num('cpu-load-time', 60);
    // Only update slider values if not being dragged
    const sliders = [
      { id: 'sliderTemp', value: temp, label: 'valTemp', format: (v: number) => `${v}%` },
      { id: 'sliderCpuLoad', value: cpuLoad, label: 'valCpuLoad', format: (v: number) => `${v}%` },
      { id: 'sliderTime', value: time, label: 'valTime', format: (v: number) => `${v}s` },
    ];
    for (const s of sliders) {
      const slider = $(s.id) as HTMLInputElement;
      const label = $(s.label) as HTMLLabelElement;
      if (!this.sliderDragging[s.id] && slider.value !== String(s.value)) {
        slider.value = String(s.value);
        label.textContent = s.format(s.value);
      }
    }

  // Readme: keep inline hidden; modal is handled by app.ts via <x-notification>
  const showReadme = this.bool('show-readme');
  const readmeWrap = $('readmeWrap');
  readmeWrap.toggleAttribute('hidden', true);
  const readme = $('readme') as HTMLElement;
  if (this.bool('readme-show-previous')) readme.setAttribute('show-previous', 'true'); else readme.removeAttribute('show-previous');

    // Buttons
    const lblShowReadme = this.str('label-show-readme', 'Show README');
    const lblHideReadme = this.str('label-hide-readme', 'Hide README');
    ( $('btnReadme') as HTMLButtonElement ).textContent = showReadme ? lblHideReadme : lblShowReadme;

    const lblShowDesc = this.str('label-show-description', 'Show description');
    const lblHideDesc = this.str('label-hide-description', 'Hide description');
  ( $('btnDesc') as HTMLButtonElement ).textContent = this.bool('show-description') ? lblHideDesc : lblShowDesc;

  ( $('btnCancel') as HTMLButtonElement ).textContent = this.str('label-cancel', 'Cancel');

    // Zones
    const zonesHost = $('zones');
    zonesHost.innerHTML = '';
    try {
      const zonesAttr = this.getAttribute('zones');
      if (zonesAttr) {
        const zones = JSON.parse(zonesAttr) as Array<any>;
        for (const z of zones) {
          const el = document.createElement('x-thermal-zone');
          if (z.active !== undefined) el.setAttribute('active', String(!!z.active));
          if (z.background) el.setAttribute('background', z.background);
          if (z.status) el.setAttribute('status', z.status);
          if (z.state) el.setAttribute('state', z.state);
          if (z.progress !== undefined) el.setAttribute('progress', String(z.progress));
          if (z.zone) el.setAttribute('zone', String(z.zone));
          if (z.fanSpeed !== undefined) el.setAttribute('fan-speed', String(z.fanSpeed));
          if (z.deviceName) el.setAttribute('device-name', z.deviceName);
          if (z.temperature !== undefined) el.setAttribute('temperature', String(z.temperature));
          if (z.maxTemp !== undefined) el.setAttribute('max-temp', String(z.maxTemp));
          if (z.processorLoadName) el.setAttribute('processor-load-name', z.processorLoadName);
          if (z.processorLoad !== undefined) el.setAttribute('processor-load', String(z.processorLoad));
          if (z.cpuLoadThreshold !== undefined) el.setAttribute('cpu-load-threshold', String(z.cpuLoadThreshold));
          if (z.stateDescriptions) el.setAttribute('state-descriptions', JSON.stringify(z.stateDescriptions));
          zonesHost.appendChild(el);
        }
      }
    } catch {
      // ignore invalid JSON
    }
  }
}

customElements.define('x-thermal-main', ThermalMainElement);
