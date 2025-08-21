// A lightweight Web Component for the gauge markup provided.
// Usage:
// <x-gauge title="My Gauge" text="65%" background="#4caf50" transform="scaleX(0.65)"></x-gauge>

type AttrMap = {
    title?: string;
    text?: string;
    background?: string;
    transform?: string;
};

export class GaugeElement extends HTMLElement {
    static get observedAttributes() {
        return ['title', 'text', 'background', 'transform'];
    }

    private root: ShadowRoot;
    private els: {
        title: HTMLSpanElement;
        filler: HTMLDivElement;
        text: HTMLDivElement;
    };

    constructor() {
        super();
        this.root = this.attachShadow({ mode: 'open' });
        this.root.innerHTML = `
            <style>
                :host { display: inline-block; font-family: system-ui, Segoe UI, Roboto, Arial, sans-serif; }
                .gauge-container { min-width: 220px; }
                .gauge-title { font-weight: 600; margin-bottom: 6px; color: #222; }
                .gauge-title span { font-size: 0.95rem; }
                .gauge-body { position: relative; height: 24px; background: #f2f2f2; border-radius: 12px; overflow: hidden; box-shadow: inset 0 0 0 1px rgba(0,0,0,0.06); }
                .gauge-filler { position: absolute; inset: 0 auto 0 0; width: 100%; transform-origin: left center; background: #4caf50; }
                .gauge-cover { position: absolute; inset: 0; display: grid; place-items: center; color: #111; font-weight: 600; font-size: 0.9rem; text-shadow: 0 1px 0 rgba(255,255,255,0.6); pointer-events: none; }
            </style>
            <div class="gauge-container">
                <div class="gauge-title">
                    <span id="titleSpan"></span>
                </div>
                <div class="gauge-body">
                    <div id="filler" class="gauge-filler"></div>
                    <div class="gauge-cover">
                        <div id="textDiv"></div>
                    </div>
                </div>
            </div>
        `;

        this.els = {
            title: this.root.getElementById('titleSpan') as HTMLSpanElement,
            filler: this.root.getElementById('filler') as HTMLDivElement,
            text: this.root.getElementById('textDiv') as HTMLDivElement,
        };
    }

    connectedCallback() {
        // Initialize with current attributes
        this.syncAllFromAttributes();
    }

    attributeChangedCallback(name: keyof AttrMap, _old: string | null, _val: string | null) {
        this.applyAttribute(name, _val ?? undefined);
    }

    // Property accessors for convenience
    get titleText() { return this.getAttribute('title') ?? ''; }
    set titleText(v: string) { this.setAttribute('title', v ?? ''); }

    get text() { return this.getAttribute('text') ?? ''; }
    set text(v: string) { this.setAttribute('text', v ?? ''); }

    get background() { return this.getAttribute('background') ?? ''; }
    set background(v: string) { this.setAttribute('background', v ?? ''); }

    get transformStyle() { return this.getAttribute('transform') ?? ''; }
    set transformStyle(v: string) { this.setAttribute('transform', v ?? ''); }

    private syncAllFromAttributes() {
        (['title', 'text', 'background', 'transform'] as const).forEach(k =>
            this.applyAttribute(k, this.getAttribute(k) ?? undefined)
        );
    }

    private applyAttribute(name: keyof AttrMap, value?: string) {
        switch (name) {
            case 'title':
                this.els.title.textContent = value ?? '';
                break;
            case 'text':
                this.els.text.textContent = value ?? '';
                break;
            case 'background':
                if (value) this.els.filler.style.background = value; else this.els.filler.style.removeProperty('background');
                break;
            case 'transform':
                if (value) this.els.filler.style.transform = value; else this.els.filler.style.removeProperty('transform');
                break;
        }
    }
}

customElements.define('x-gauge', GaugeElement);
