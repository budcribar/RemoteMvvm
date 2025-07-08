// newapp.ts - Demo UI script using PointerViewModelRemoteClient
import { PointerViewModelServiceClient } from './generated/PointerViewModelServiceServiceClientPb';
import { PointerViewModelRemoteClient } from './PointerViewModelRemoteClient';

const grpcHost = 'http://localhost:50052';
const grpcClient = new PointerViewModelServiceClient(grpcHost);
const vm = new PointerViewModelRemoteClient(grpcClient);

async function getButtonColor(button: string): Promise<string> {
    await vm.getClicksWithoutNotification(button);
    const clicks = vm.lastClickCount;
    if (clicks === 0) return 'white';
    if (clicks === 1) return 'grey';
    if (clicks >= 2) return 'green';
    return 'white';
}

async function coloredMouseSvg(btnCount: number): Promise<string> {
    const left = await getButtonColor('left');
    const right = await getButtonColor('right');
    const center = await getButtonColor('center');
    if (btnCount === 2) {
        return `<svg class='configuration btns-2' data-btn-count='2' style='width: 18em; height:18em;' viewBox='15 12 135 260'>
            <path class='body' fill='#FFFFFF' d='M14.731,109.632v84.879c0,37.369,30.402,67.771,67.772,67.771s67.772-30.402,67.771-67.772 v-84.877c-20.932,10.435-44.215,15.925-67.773,15.925C58.947,125.558,35.663,120.067,14.731,109.632z'/>
            <path class='button-right' fill='${right}' d='M85.578,33.804v85.562c22.587-0.477,44.818-6.2,64.697-16.65v-1.214 C150.275,65.164,121.525,35.417,85.578,33.804z'/>
            <path class='button-left' fill='${left}' d='M79.424,33.804c-35.945,1.615-64.693,31.362-64.693,67.699v1.213 c19.879,10.451,42.109,16.175,64.693,16.651V33.804z'/>
            <path class='Outline' d='M82.503,24.5C40.043,24.5,5.5,59.043,5.5,101.502v93.007c0,42.46,34.543,77.003,77.003,77.003 s77.002-34.544,77.002-77.003v-93.007C159.505,59.043,124.962,24.5,82.503,24.5z M150.275,101.502v1.214 c-19.879,10.45-42.11,16.173-64.697,16.65V33.804C121.525,35.417,150.275,65.164,150.275,101.502z M79.424,33.804v85.563 c-22.584-0.476-44.814-6.2-64.693-16.651v-1.213C14.731,65.166,43.479,35.419,79.424,33.804z M82.503,262.282 c-37.37,0-67.772-30.402-67.772-67.771v-84.879c20.932,10.435,44.216,15.926,67.769,15.926c23.559,0,46.842-5.49,67.773-15.925 v84.877C150.275,231.88,119.873,262.282,82.503,262.282z'/>
        </svg>`;
    }
    return `<svg class='configuration btns-3' data-btn-count='3' style='width: 18em; height:18em;' viewBox='15 10 135 260'>
            <path class='body' fill='#FFFFFF' d='M13.063,103.63v84.879c0,37.369,30.402,67.771,67.772,67.771 c37.369,0,67.771-30.402,67.77-67.772v-84.877c-20.932,10.435-44.215,15.925-67.773,15.925 C57.279,119.556,33.996,114.065,13.063,103.63z'/>
            <path class='button-right' fill='${right}' d='M83.91,27.802v12.212c6.514,1.414,11.408,7.221,11.408,14.152v19.303 c0,6.931-4.895,12.738-11.408,14.152v25.743c22.588-0.477,44.818-6.2,64.697-16.65V95.5 C148.607,59.162,119.857,29.415,83.91,27.802z'/>
            <path class='button-center' fill='${center}' d='M72.505,54.166v19.303c0,4.593,3.736,8.33,8.331,8.33c4.594,0,8.33-3.737,8.33-8.33V54.166 c0-4.593-3.738-8.33-8.33-8.33C76.244,45.836,72.505,49.573,72.505,54.166z'/>
            <path class='button-left' fill='${left}' d='M77.756,87.62c-6.511-1.416-11.404-7.222-11.404-14.151V54.166 c0-6.929,4.893-12.735,11.404-14.151V27.802c-35.945,1.615-64.693,31.362-64.693,67.699v1.213 c19.879,10.451,42.109,16.175,64.693,16.652V87.62z'/>
            <path class='Outline' d='M80.836,18.498c-42.46,0-77.003,34.543-77.003,77.002v93.007c0,42.46,34.543,77.003,77.003,77.003 c42.459,0,77.002-34.544,77.002-77.003V95.5C157.838,53.041,123.295,18.498,80.836,18.498z M148.607,95.5v1.214 c-19.879,10.45-42.109,16.173-64.697,16.65V87.621c6.514-1.414,11.408-7.221,11.408-14.152V54.166 c0-6.931-4.895-12.738-11.408-14.152V27.802C119.857,29.415,148.607,59.162,148.607,95.5z M80.836,45.836 c4.592,0,8.33,3.737,8.33,8.33v19.303c0,4.593-3.736,8.33-8.33,8.33c-4.594,0-8.331-3.737-8.331-8.33V54.166 C72.505,49.573,76.244,45.836,80.836,45.836L80.836,45.836z M77.756,27.802v12.213c-6.511,1.416-11.404,7.222-11.404,14.151 v19.303c0,6.929,4.893,12.735,11.404,14.151v25.746c-22.584-0.477-44.814-6.201-64.693-16.652v-1.213 C13.063,59.164,41.812,29.417,77.756,27.802z M80.836,256.28c-37.371,0-67.772-30.402-67.772-67.771V103.63 c20.932,10.435,44.216,15.926,67.769,15.926c23.559,0,46.842-5.49,67.773-15.925v84.877 C148.607,225.878,118.205,256.28,80.836,256.28z'/>
        </svg>`;
}

async function coloredTouchpadSvg(btnCount: number): Promise<string> {
    const left = await getButtonColor('left');
    const right = await getButtonColor('right');
    const center = await getButtonColor('center');
    if (btnCount === 2) {
        return `<svg class='configuration btns-2' data-btn-count='2' style='width: 18em; height:18em;' viewBox='0 -10 200 200'>
            <path class='button-right' fill='${right}' stroke='#000000' stroke-width='2.5' d='M101.762,185.114h4.829h17.216h57.273 c8.521,0,15.396-6.875,15.396-15.397v-16.875v-22.783h-94.715V185.114z'/>
            <path class='button-left' fill='${left}' stroke='#000000' stroke-width='2.5' d='M1.25,152.841v16.875c0,8.522,6.932,15.397,15.455,15.397 H73.92h22.045v-55.057H1.25V152.841z'/>
            <path class='right-dotted' fill='none' stroke='#FFFFFF' stroke-width='1.5' stroke-dasharray='2' d='M101.979,129.965l94.446,0.219 l0.437,41.742c0,0-2.182,13.768-15.303,13.768s-80.017-0.437-80.017-0.437L101.979,129.965L101.979,129.965z'/>
            <path class='left-dotted' fill='none' stroke='#FFFFFF' stroke-width='1.5' stroke-dasharray='2' d='M1.058,129.965 c0,0,95.536,0,95.535-0.054c0.001,0.054-0.659,55.353-0.659,55.353H13.577c0,0-12.299-1.226-12.299-16.479 C1.278,153.527,1.058,129.965,1.058,129.965z'/>
            <path class='body' fill='#FFFFFF' stroke='#000000' stroke-width='2.5' d='M181.081,1.25H16.705C8.182,1.25,1.25,8.125,1.25,16.647 v86.364v21.25h94.716h5.796h94.715v-21.25V16.647C196.477,8.125,189.604,1.25,181.081,1.25z'/>
        </svg>`;
    }
    return `<svg class='configuration btns-3' data-btn-count='3' style='width: 18em; height:18em;' viewBox='0 -10 200 200'>
            <path class='body' fill='#FFFFFF' stroke='#000000' stroke-width='2.5' d='M196.477,103.011V16.647 c0-8.522-6.873-15.397-15.396-15.397H16.705C8.182,1.25,1.25,8.125,1.25,16.647v86.364v21.25h94.716h5.796h94.714V103.011z'/>
            <path class='button-right' fill='${right}' stroke='#000000' stroke-width='2.5' d='M133.35,131.059v55.056h3.242h11.559h38.457 c5.722,0,10.336-6.875,10.336-15.397v-16.875v-22.783H133.35z'/>
            <path class='right-dotted' fill='none' stroke='#FFFFFF' stroke-width='1.5' stroke-dasharray='2' d='M133.496,130.965l63.413,0.219 l0.294,41.742c0,0-1.467,13.768-10.275,13.768c-8.81,0-53.725-0.437-53.725-0.437L133.496,130.965L133.496,130.965z'/>
            <polygon class='button-center' fill='${center}' stroke='#000000' stroke-width='2.5' points='71.273,131.057 71.188,186.111 127.008,186.111  127.008,131.057'/>
            <path class='center-dotted' fill='none' stroke='#FFFFFF' stroke-width='1.5' stroke-dasharray='2' d='M71.188,186.111l-0.029-55.146 c0,0,56.222,0,56.219-0.055c0.003,0.055-0.386,55.354-0.386,55.354L71.188,186.111z'/>
            <path class='button-left' fill='${left}' stroke='#000000' stroke-width='2.5' d='M1.31,153.841v16.875c0,8.522,4.644,15.397,10.353,15.397 h38.33h14.768v-55.057H1.31V153.841z'/>
            <path class='left-dotted' fill='none' stroke='#FFFFFF' stroke-width='1.5' stroke-dasharray='2' d='M1.182,130.965 c0,0,64.001,0,64-0.054c0.001,0.054-0.441,55.353-0.441,55.353H9.568c0,0-8.239-1.226-8.239-16.479 C1.329,154.527,1.182,130.965,1.182,130.965z'/>
        </svg>`;
}

async function updateTestArea(): Promise<void> {
    const area = document.getElementById('test-area') as HTMLElement;
    if (!area) return;
    if (vm.showCursorTest) {
        area.innerHTML = `<div style="display: flex; justify-content: center; align-items: center; width: 100%;">
            <div id="cursor-test" style="width:18em; height:18em; border:.5em solid #0096d6; border-radius:1em;" tabindex="0" role="button" aria-label="Move cursor here to continue"></div>
        </div>`;
        const cursorDiv = document.getElementById('cursor-test');
        cursorDiv?.addEventListener('mouseover', () => vm.onCursorTest());
        cursorDiv?.addEventListener('keydown', (e) => { if ((e as KeyboardEvent).key === 'Enter') vm.onCursorTest(); });
        return;
    }
    if (vm.showConfigSelection) {
        const m2 = await coloredMouseSvg(2);
        const m3 = await coloredMouseSvg(3);
        const t2 = await coloredTouchpadSvg(2);
        const t3 = await coloredTouchpadSvg(3);
        area.innerHTML = `<div style="display: flex; justify-content: center; align-items: center; width: 100%;">
            <div id="pointer-images">
                <button class="configuration-btn" aria-label="Select 2-button mouse"><span>${m2}</span></button>
                <button class="configuration-btn" aria-label="Select 3-button mouse"><span>${m3}</span></button>
                <button class="configuration-btn" aria-label="Select 2-button touchpad"><span>${t2}</span></button>
                <button class="configuration-btn" aria-label="Select 3-button touchpad"><span>${t3}</span></button>
            </div>
        </div>`;
        const btns = area.querySelectorAll('.configuration-btn');
        if (btns[0]) btns[0].addEventListener('click', () => { vm.onSelectDevice('mouse'); vm.onSelectNumButtons(2); });
        if (btns[1]) btns[1].addEventListener('click', () => { vm.onSelectDevice('mouse'); vm.onSelectNumButtons(3); });
        if (btns[2]) btns[2].addEventListener('click', () => { vm.onSelectDevice('touchpad'); vm.onSelectNumButtons(2); });
        if (btns[3]) btns[3].addEventListener('click', () => { vm.onSelectDevice('touchpad'); vm.onSelectNumButtons(3); });
        return;
    }
    if (vm.showClickInstructions) {
        const deviceSvg = vm.selectedDevice === 'mouse'
            ? await coloredMouseSvg(vm.is3Btn ? 3 : 2)
            : await coloredTouchpadSvg(vm.is3Btn ? 3 : 2);
        area.innerHTML = `<div id="click-instructions" aria-live="polite"></div>
            <div id="device-area" role="application"></div>`;
        const deviceArea = document.getElementById('device-area');
        if (deviceArea) {
            deviceArea.innerHTML = deviceSvg;
            deviceArea.addEventListener('mousedown', (e) => vm.onClickTest((e as MouseEvent).button));
        }
        return;
    }
    area.innerHTML = '';
}

async function render() {
    (document.getElementById('mouse-pointer-test') as HTMLElement).style.display = vm.show ? 'block' : 'none';
    const spinner = document.getElementById('initializing-spinner') as HTMLElement;
    if (spinner) spinner.style.display = vm.showSpinner ? 'block' : 'none';
    const instr = document.getElementById('pointer-test-instructions') as HTMLElement;
    if (instr) {
        instr.style.display = vm.showCursorTest ? 'flex' : 'none';
        instr.textContent = vm.instructions;
    }
    const timer = document.getElementById('timer') as HTMLElement;
    if (timer) {
        timer.style.display = vm.showTimer ? 'block' : 'none';
        timer.textContent = vm.timerText;
    }
    const bottom = document.querySelector('.bottom') as HTMLElement;
    if (bottom) bottom.style.display = vm.showBottom ? 'flex' : 'none';
    const status = document.getElementById('connection-status');
    if (status) status.textContent = vm.connectionStatus;
    await updateTestArea();
}

async function init() {
    await vm.initializeRemote();
    vm.addChangeListener(render);
    await render();
}

document.addEventListener('DOMContentLoaded', () => {
    init();
    const cancelBtn = document.getElementById('btn-cancel');
    cancelBtn?.addEventListener('click', () => vm.cancelTest());
    const finishBtn = document.getElementById('btn-finish');
    finishBtn?.addEventListener('click', () => { vm.finishTest(); });
});
