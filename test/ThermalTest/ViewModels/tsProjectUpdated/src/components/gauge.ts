// A lightweight Web Component for the gauge markup provided.
// Usage:
// <x-gauge title="My Gauge" text="65%" background="#4caf50" transform="scaleX(0.65)"></x-gauge>

type AttrMap = {
    title?: string;
    text?: string;
    background?: string;
    transform?: string;
    current?: string; // numeric string
    max?: string;     // numeric string
    unit?: string;
};

export class GaugeElement extends HTMLElement {
    static get observedAttributes() {
        return ['title', 'text', 'background', 'transform', 'current', 'max', 'unit'];
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
        (['title', 'text', 'background', 'transform', 'current', 'max', 'unit'] as const).forEach(k =>
            this.applyAttribute(k, this.getAttribute(k) ?? undefined)
        );
    }

    private applyAttribute(name: keyof AttrMap, value?: string) {
        // Always update title immediately
        if (name === 'title') {
            this.els.title.textContent = value ?? '';
        }

        // If both current and max are present (and numeric), compute derived values.
        const curAttr = this.getAttribute('current');
        const maxAttr = this.getAttribute('max');
        const hasNumeric = (s: string | null) => s != null && s.trim() !== '' && !Number.isNaN(Number(s));
        const canCompute = hasNumeric(curAttr) && hasNumeric(maxAttr);

        if (canCompute) {
            // Compute from current/max/unit like the Blazor GaugeComponent
            const current = Math.max(0, Number(curAttr));
            const max = Math.max(0, Number(maxAttr));
            const unit = this.getAttribute('unit') ?? '';

            // Text
            let text: string;
            if (max === 0) text = '-';
            else if (current >= max) text = '+';
            else text = `${current}${unit}`;
            this.els.text.textContent = text;

            // Transform using rotate(turn) like the Blazor component
            let turn = 0;
            if (max === 0) {
                turn = 0;
            } else if (current >= max) {
                turn = 0.5; // half turn (180deg)
            } else if (current === 0) {
                turn = 0.01; // minimal sliver
            } else {
                const perc = Math.floor((current * 50) / max); // 0..50
                // Compose 0.xx in turns; ensure two digits
                const percStr = String(perc).padStart(2, '0');
                // rotate(0.xxturn)
                this.els.filler.style.transform = `rotate(0.${percStr}turn)`;
                // Background will be applied below; return early to avoid overwrite
                // Note: For current<max and >0 we already set transform
                // so skip the generic setter at the end of this block
            }
            if (current >= max || max === 0 || current === 0) {
                this.els.filler.style.transform = `rotate(${turn}turn)`;
            }

            // Background gradient from green->red
            let bg = '#FFFFFF';
            if (max === 0) {
                bg = '#FFFFFF';
            } else if (current >= max) {
                bg = '#FF0000';
            } else {
                const perc = Math.max(0, Math.min(100, Math.floor((100 * current) / max)));
                const red = Math.round((perc * 255) / 100);
                const green = 255 - red;
                const toHex2 = (n: number) => n.toString(16).toUpperCase().padStart(2, '0');
                bg = `#${toHex2(red)}${toHex2(green)}00`;
            }
            this.els.filler.style.background = bg;
            return; // computed values take precedence
        }

        // Fallback: accept direct text/background/transform inputs
        switch (name) {
            case 'text':
                this.els.text.textContent = value ?? '';
                break;
            case 'background':
                if (value) this.els.filler.style.background = value; else this.els.filler.style.removeProperty('background');
                break;
            case 'transform':
                if (value) this.els.filler.style.transform = value; else this.els.filler.style.removeProperty('transform');
                break;
            case 'unit':
            case 'current':
            case 'max':
                // If only one of the pair changed and we can't compute yet, do nothing here.
                break;
        }
    }
}

customElements.define('x-gauge', GaugeElement);
