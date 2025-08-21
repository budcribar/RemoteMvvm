// Web Component to render the provided README content.
// <x-readme show-previous="true|false"></x-readme>

export class ReadmeElement extends HTMLElement {
  static get observedAttributes() { return ['show-previous']; }
  private root: ShadowRoot;

  constructor() {
    super();
    this.root = this.attachShadow({ mode: 'open' });
    this.root.innerHTML = `
      <style>
        :host { display: block; font-family: system-ui, Segoe UI, Roboto, Arial, sans-serif; line-height: 1.4; color: #1a1a1a; }
        .readme-container {
          display: flex;
          flex-flow: column wrap;
          justify-content: space-around;
          align-content: space-around;
          gap: .5em;
          padding: 12px 0;
        }
        .readme-paragraph {
          text-align: left;
          width: 95%;
        }
        .reference-name { font-weight: bolder; }
        .previous-content { text-decoration: line-through; display: none; }
        :host([show-previous="true"]) .previous-content { display: inline; }
        .emphasis { animation: blinker 2s linear infinite; }
        @keyframes blinker { 50% { opacity: 0.2; } }
        a { color: #0a6cff; text-decoration: none; }
        a:hover { text-decoration: underline; }
      </style>
      <div class="readme-container">
        <div class="readme-paragraph">
          The <span class="reference-name">HP 3LS thermal tool</span> requires administrator permissions.
          <span class="previous-content">To use: Run BlazorCorrosionTest.exe.Requires administrator permissions.</span>
        </div>
        <div class="readme-paragraph">
          This software is designed to be run with no or minimal other applications running.
        </div>
        <div class="readme-paragraph">
          Running the test alongside other applications may skew results.<span class="previous-content">, or the test may flat-out refuse to run.</span>
        </div>
        <div class="readme-paragraph">
          <span class="reference-name">Temperature threshold to DTS</span>, <span class="reference-name">Processor maximum load</span>, and <span class="reference-name">Monitoring period</span> are all adjustable, but are set to default values based on testing performed on known good/failing systems (90%, 15%, 60s, respectively).
        </div>
        <div class="readme-paragraph">
          The host platform, cooler disposition, system environment, software in use, Operating System and CPU SKU will also affect results and may necessitate additional tweaking.
        </div>
        <div class="readme-paragraph">
          There is a known issue where the software will fail to detect a failure using the Balanced power plan.
          If possible, <span class="emphasis">ensure that the Windows OS Power Plan is set to <span class="reference-name">Ultimate Performance</span></span>.
        </div>
        <div class="readme-paragraph">
          On supported CPUs for Z4 G5, Z6 G5, and Z8 Fury G5, the software uses the DTS Max temperatures published by Intel for Max Temperature.
          For other systems this information is not included, and so the default of 100C is used.
          For a more accurate result, look up the installed CPU on <a href="https://ark.intel.com/content/www/us/en/ark.html" target="_blank" rel="noopener noreferrer">Intel ARK</a>, and set the 'Temperature threshold to DTS' slider to a value that is ~90% of the reported DTS Max.
          This will work for other unsupported processors like Core-i series as well, although that is out of scope for this tool's intended use.
        </div>
        <div class="readme-paragraph">
          Literally no assertions, warranties, or guarantees are made or claimed about this software.
          It is completely experimental and not meant for distribution outside of HP. Use at your own risk.
        </div>
        <div class="readme-paragraph">
          The tool was developed in collaboration with the <a href="https://www.microsoft.com/store/apps/9P4PNDG7L782" target="_blank" rel="noopener noreferrer">HP PC Hardware Diagnostics Windows</a> development team.
          <span class="previous-content">I (Max) didn't write this code (Guy did - thanks Guy!), but I requested it, tested it, wrote its README, and probably gave it to you in the first place.
          So I'm the point of contact if you need anything.</span>
        </div>
        <div class="readme-paragraph">
          Contact 3LS Engineer Max Knaver (CW) - max.knaver@hp.com for feedback or questions.
        </div>
      </div>
    `;
  }

  attributeChangedCallback(name: string, _oldVal: string | null, newVal: string | null) {
    if (name === 'show-previous') {
      // boolean attribute support: if present without value, interpret as true
      if (newVal === '' || newVal === null) {
        // reflect a default false unless explicitly set to "true"
        // No-op: styling is controlled via attribute value, kept as-is
      }
    }
  }
}

customElements.define('x-readme', ReadmeElement);
