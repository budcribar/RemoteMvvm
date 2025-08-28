/******/ (() => { // webpackBootstrap
/*!********************!*\
  !*** ./src/app.ts ***!
  \********************/
async function render() {
    document.getElementById('instructions').value = vm.instructions;
    document.getElementById('cpuTemperatureThreshold').value = String(vm.cpuTemperatureThreshold);
    document.getElementById('cpuLoadThreshold').value = String(vm.cpuLoadThreshold);
    document.getElementById('cpuLoadTimeSpan').value = String(vm.cpuLoadTimeSpan);
    const zoneListEl = document.getElementById('zoneList');
    const zoneListRootOpen = zoneListEl.querySelector('details[data-root]')?.open ?? true;
    const zoneListItemOpen = Array.from(zoneListEl.querySelectorAll('details[data-index]')).map(d => d.open);
    zoneListEl.innerHTML = '';
    const zoneListDetails = document.createElement('details');
    zoneListDetails.setAttribute('data-root', '');
    zoneListDetails.open = zoneListRootOpen;
    const zoneListSummary = document.createElement('summary');
    zoneListSummary.textContent = 'ZoneList';
    zoneListDetails.appendChild(zoneListSummary);
    vm.zoneList.forEach((item, index) => {
        const itemDetails = document.createElement('details');
        itemDetails.setAttribute('data-index', String(index));
        itemDetails.open = zoneListItemOpen[index] ?? false;
        const itemSummary = document.createElement('summary');
        itemSummary.textContent = `ZoneList[${index}]`;
        itemDetails.appendChild(itemSummary);
        const container = document.createElement('div');
        Object.entries(item).forEach(([key, value]) => {
            const field = document.createElement('div');
            field.className = 'field';
            const label = document.createElement('span');
            label.textContent = key;
            const input = document.createElement('input');
            input.value = typeof value === 'object' ? JSON.stringify(value, null, 2) : String(value);
            input.addEventListener('change', async (e) => {
                const newVal = e.target.value;
                let parsed;
                if (typeof value === 'number')
                    parsed = Number(newVal);
                else if (typeof value === 'boolean')
                    parsed = newVal.toLowerCase() === 'true';
                else {
                    try {
                        parsed = JSON.parse(newVal);
                    }
                    catch {
                        parsed = newVal;
                    }
                }
                const newCollection = vm.zoneList.map((z) => Object.assign({}, z));
                if (JSON.stringify(newCollection[index][key]) !== JSON.stringify(parsed)) {
                    newCollection[index][key] = parsed;
                    try {
                        await vm.updatePropertyValueDebounced('ZoneList', newCollection);
                    }
                    catch (err) {
                        handleError(err, 'Update ZoneList');
                    }
                }
            });
            field.appendChild(label);
            field.appendChild(input);
            container.appendChild(field);
        });
        itemDetails.appendChild(container);
        zoneListDetails.appendChild(itemDetails);
    });
    zoneListEl.appendChild(zoneListDetails);
    const testSettingsEl = document.getElementById('testSettings');
    const testSettingsRootOpen = testSettingsEl.querySelector('details[data-root]')?.open ?? true;
    testSettingsEl.innerHTML = '';
    const testSettingsDetails = document.createElement('details');
    testSettingsDetails.setAttribute('data-root', '');
    testSettingsDetails.open = testSettingsRootOpen;
    const testSettingsSummary = document.createElement('summary');
    testSettingsSummary.textContent = 'TestSettings';
    testSettingsDetails.appendChild(testSettingsSummary);
    const container = document.createElement('div');
    Object.entries(vm.testSettings).forEach(([key, value]) => {
        const field = document.createElement('div');
        field.className = 'field';
        const label = document.createElement('span');
        label.textContent = key;
        const input = document.createElement('input');
        input.value = typeof value === 'object' ? JSON.stringify(value, null, 2) : String(value);
        input.addEventListener('change', async (e) => {
            const newVal = e.target.value;
            let parsed;
            if (typeof value === 'number')
                parsed = Number(newVal);
            else if (typeof value === 'boolean')
                parsed = newVal.toLowerCase() === 'true';
            else {
                try {
                    parsed = JSON.parse(newVal);
                }
                catch {
                    parsed = newVal;
                }
            }
            const newObj = Object.assign({}, vm.testSettings);
            if (JSON.stringify(newObj[key]) !== JSON.stringify(parsed)) {
                newObj[key] = parsed;
                try {
                    await vm.updatePropertyValueDebounced('TestSettings', newObj);
                }
                catch (err) {
                    handleError(err, 'Update TestSettings');
                }
            }
        });
        field.appendChild(label);
        field.appendChild(input);
        container.appendChild(field);
    });
    testSettingsDetails.appendChild(container);
    testSettingsEl.appendChild(testSettingsDetails);
    document.getElementById('showDescription').value = String(vm.showDescription);
    document.getElementById('showReadme').value = String(vm.showReadme);
    document.getElementById('connection-status').textContent = vm.connectionStatus;
}
async function init() {
    try {
        await vm.initializeRemote();
        vm.addChangeListener(render);
        await render();
    }
    catch (err) {
        handleError(err, 'Initialize remote');
    }
}
document.addEventListener('DOMContentLoaded', () => {
    init();
    document.getElementById('instructions').addEventListener('change', async (e) => {
        const newValue = e.target.value;
        const currentValue = vm.instructions;
        // Only update if value actually changed
        if (newValue !== currentValue) {
            try {
                await vm.updatePropertyValueDebounced('Instructions', newValue);
            }
            catch (err) {
                handleError(err, 'Update Instructions');
            }
        }
    });
    document.getElementById('cpuTemperatureThreshold').addEventListener('change', async (e) => {
        const newValue = e.target.value;
        const currentValue = vm.cpuTemperatureThreshold;
        // Only update if value actually changed
        if (Number(newValue) !== currentValue) {
            try {
                await vm.updatePropertyValueDebounced('CpuTemperatureThreshold', Number(newValue));
            }
            catch (err) {
                handleError(err, 'Update CpuTemperatureThreshold');
            }
        }
    });
    document.getElementById('cpuLoadThreshold').addEventListener('change', async (e) => {
        const newValue = e.target.value;
        const currentValue = vm.cpuLoadThreshold;
        // Only update if value actually changed
        if (Number(newValue) !== currentValue) {
            try {
                await vm.updatePropertyValueDebounced('CpuLoadThreshold', Number(newValue));
            }
            catch (err) {
                handleError(err, 'Update CpuLoadThreshold');
            }
        }
    });
    document.getElementById('cpuLoadTimeSpan').addEventListener('change', async (e) => {
        const newValue = e.target.value;
        const currentValue = vm.cpuLoadTimeSpan;
        // Only update if value actually changed
        if (Number(newValue) !== currentValue) {
            try {
                await vm.updatePropertyValueDebounced('CpuLoadTimeSpan', Number(newValue));
            }
            catch (err) {
                handleError(err, 'Update CpuLoadTimeSpan');
            }
        }
    });
    document.getElementById('showDescription').addEventListener('change', async (e) => {
        const newValue = e.target.value;
        const currentValue = vm.showDescription;
        // Only update if value actually changed
        if (Boolean(newValue.toLowerCase() === 'true') !== currentValue) {
            try {
                await vm.updatePropertyValueDebounced('ShowDescription', newValue.toLowerCase() === 'true');
            }
            catch (err) {
                handleError(err, 'Update ShowDescription');
            }
        }
    });
    document.getElementById('showReadme').addEventListener('change', async (e) => {
        const newValue = e.target.value;
        const currentValue = vm.showReadme;
        // Only update if value actually changed
        if (Boolean(newValue.toLowerCase() === 'true') !== currentValue) {
            try {
                await vm.updatePropertyValueDebounced('ShowReadme', newValue.toLowerCase() === 'true');
            }
            catch (err) {
                handleError(err, 'Update ShowReadme');
            }
        }
    });
    document.getElementById('stateChanged-btn').addEventListener('click', async () => {
        try {
            await vm.stateChanged(undefined);
        }
        catch (err) {
            handleError(err, 'Execute StateChanged');
        }
    });
    document.getElementById('cancelTest-btn').addEventListener('click', async () => {
        try {
            await vm.cancelTest();
        }
        catch (err) {
            handleError(err, 'Execute CancelTest');
        }
    });
});

/******/ })()
;
//# sourceMappingURL=bundle.js.map