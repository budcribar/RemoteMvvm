// Simple notification/modal component with backdrop and slot
// Usage: <x-notification open><div>content</div></x-notification>
// Methods: show(), hide()

export class NotificationElement extends HTMLElement {
  static get observedAttributes() { return ['open', 'title']; }
  private root: ShadowRoot;

  constructor() {
    super();
    this.root = this.attachShadow({ mode: 'open' });
    this.root.innerHTML = `
      <style>
        :host { display: contents; }
        .backdrop { position: fixed; inset: 0; background: rgba(0,0,0,0.35); display: none; z-index: 9999; }
        .panel { position: fixed; left: 50%; top: 10%; transform: translateX(-50%);
                 background: #fff; color: #111; border-radius: 8px; box-shadow: 0 10px 30px rgba(0,0,0,0.2);
                 min-width: 320px; max-width: 90vw; max-height: 80vh; overflow: auto; display: none; z-index: 10000; }
        header { display: flex; align-items: center; justify-content: space-between; padding: 8px 12px; border-bottom: 1px solid #eee; font-weight: 700; }
        .content { padding: 12px; }
        button.close { appearance: none; background: transparent; border: 0; font-size: 1.2rem; line-height: 1; cursor: pointer; }
        :host([open]) .backdrop, :host([open]) .panel { display: block; }
      </style>
      <div class="backdrop" id="backdrop"></div>
      <div class="panel" role="dialog" aria-modal="true" aria-live="polite">
        <header>
          <div id="title"></div>
          <button id="btnClose" class="close" aria-label="Close">×</button>
        </header>
        <div class="content"><slot></slot></div>
      </div>
    `;
  }

  connectedCallback() {
    const $ = (id: string) => this.root.getElementById(id)!;
    $('backdrop').addEventListener('click', () => this.hide());
    $('btnClose').addEventListener('click', () => this.hide());
    this.syncTitle();
  }

  attributeChangedCallback(name: string) {
    if (name === 'title') this.syncTitle();
  }

  private syncTitle() {
    const el = this.root.getElementById('title');
    if (el) el.textContent = this.getAttribute('title') ?? '';
  }

  show() { this.setAttribute('open', ''); }
  hide() { this.removeAttribute('open'); this.dispatchEvent(new CustomEvent('close')); }
}

customElements.define('x-notification', NotificationElement);
