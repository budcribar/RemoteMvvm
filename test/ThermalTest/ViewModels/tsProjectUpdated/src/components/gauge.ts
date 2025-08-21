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
                .gauge-title { font-weight: bold; height: 2.5em; text-align: center; color: #222; }
                .gauge-container {
                    /* display: flex;
                    flex-flow: column nowrap; */
                    align-items: center;
                    width: 200px;
                    gap: 1em;
                }
                .gauge-body {
                    width: 100%;
                    height: 0;
                    padding-bottom: 50%;
                    background: #b4c0be;
                    position: relative;
                    border-top-left-radius: 100% 200%;
                    border-top-right-radius: 100% 200%;
                    overflow: hidden;
                }
                .gauge-filler {
                    position: absolute;
                    top: 100%;
                    left: 0;
                    width: inherit;
                    height: 100%;
                    transform-origin: center top;
                    transition: transform 0.2s ease-out;
                    background: #4caf50;
                }
                .gauge-cover {
                    width: 75%;
                    height: 150%;
                    position: absolute;
                    background: #ffffff;
                    border-radius: 50%;
                    top: 25%;
                    left: 50%;
                    transform: translateX(-50%);
                    display: flex;
                    align-items: center;
                    justify-content: center;
                    padding-bottom: 25%;
                    box-sizing: border-box;
                    color: #111;
                    font-weight: 600;
                    font-size: 0.9rem;
                    text-shadow: 0 1px 0 rgba(255,255,255,0.6);
                    pointer-events: none;
                }
            </style>
            <div class="gauge-container">
                <div class="gauge-title"><span id="titleSpan"></span></div>
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
