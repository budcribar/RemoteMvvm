// gRPC-Web client test using protoc generated stubs
// This script is executed from the C# test to verify that the generated
// protobuf and service files can communicate with the running server.
global.XMLHttpRequest = require('xhr2');
const svc = require('./testviewmodelservice_grpc_web_pb.js');
const pb = require('./testviewmodelservice_pb.js');
const { TestViewModelServiceClient } = svc;
const { Empty } = require('google-protobuf/google/protobuf/empty_pb.js');
const process = require('process');
const port = process.argv[2] || '5000';
const client = new TestViewModelServiceClient(`http://localhost:${port}`, null, null);
console.log('Starting gRPC-Web test using generated client...');
// Generic function to traverse and extract data from protobuf response
function extractDataFromResponse(response) {
    const responseData = {};
    // Get all methods on the response object
    const methods = Object.getOwnPropertyNames(Object.getPrototypeOf(response))
        .filter(name => name.startsWith('get') && typeof response[name] === 'function');
    methods.forEach(methodName => {
        try {
            const value = response[methodName]();
            if (value !== undefined && value !== null) {
                // Extract property name from getter method (e.g., 'getCounter' -> 'counter')
                let propName = methodName.substring(3).toLowerCase();
                // Special handling for protobuf map getters that end with "Map"
                if (methodName.endsWith('Map') && propName.endsWith('map')) {
                    // For methods like getStatusMapMap(), the property should be statusmap, not statusmapmap
                    propName = propName.substring(0, propName.length - 3); // Remove the extra "map"
                }
                // Handle different types of values
                if (typeof value === 'object' && value !== null) {
                    if (Array.isArray(value)) {
                        // Handle arrays/lists
                        responseData[propName] = extractArrayData(value);
                    }
                    else if (value.constructor && (value.constructor.name.includes('Map') || methodName.endsWith('Map'))) {
                        // Handle protobuf maps - iterate through entries
                        const mapData = {};
                        if (typeof value.forEach === 'function') {
                            value.forEach((mapValue, mapKey) => {
                                mapData[mapKey] = extractValue(mapValue);
                            });
                        }
                        else if (typeof value.entrySet === 'function') {
                            // Alternative map iteration approach
                            const entries = value.entrySet();
                            entries.forEach(entry => {
                                if (entry && entry.getKey && entry.getValue) {
                                    mapData[entry.getKey()] = extractValue(entry.getValue());
                                }
                            });
                        }
                        else {
                            // Try to extract as an object with numeric/string properties
                            Object.keys(value).forEach(key => {
                                if (!isNaN(key) || typeof key === 'string') {
                                    mapData[key] = extractValue(value[key]);
                                }
                            });
                        }
                        responseData[propName] = mapData;
                    }
                    else if (value.constructor && value.constructor.name && value.constructor.name.toLowerCase().includes('list')) {
                        // Handle special list objects
                        if (typeof value.array === 'function') {
                            responseData[propName] = extractArrayData(value.array());
                        }
                        else if (value.length !== undefined) {
                            responseData[propName] = extractArrayData(Array.from(value));
                        }
                        else {
                            responseData[propName] = value;
                        }
                    }
                    else {
                        // Handle nested objects
                        responseData[propName] = extractDataFromResponse(value);
                    }
                }
                else {
                    // Handle primitive values
                    responseData[propName] = value;
                }
            }
        }
        catch (err) {
            console.error(`Error extracting ${methodName}:`, err.message);
        }
    });
    return responseData;
}
function extractArrayData(array) {
    if (!Array.isArray(array))
        return array;
    return array.map(item => {
        if (typeof item === 'object' && item !== null) {
            // Check if it's a protobuf object with getter methods
            if (Object.getOwnPropertyNames(Object.getPrototypeOf(item)).some(name => name.startsWith('get'))) {
                return extractDataFromResponse(item);
            }
            else {
                return item;
            }
        }
        else {
            return item;
        }
    });
}
function extractValue(value) {
    if (typeof value === 'object' && value !== null) {
        if (Array.isArray(value)) {
            return extractArrayData(value);
        }
        else if (Object.getOwnPropertyNames(Object.getPrototypeOf(value)).some(name => name.startsWith('get'))) {
            return extractDataFromResponse(value);
        }
        else {
            return value;
        }
    }
    else {
        return value;
    }
}
client.getState(new Empty(), {}, (err, response) => {
    if (err) {
        console.error('gRPC error:', err);
        process.exit(1);
    }
    console.log('=== TestViewModel Data Extraction ===');
    try {
        // Extract all data from the response using reflection
        const extractedData = extractDataFromResponse(response);
        console.log('RESPONSE_DATA:', JSON.stringify(extractedData, null, 2));
        // Create a flat structure for easier numeric extraction
        const flatData = {};
        function flattenData(obj, prefix = '') {
            Object.keys(obj).forEach(key => {
                const fullKey = prefix ? `${prefix}.${key}` : key;
                const value = obj[key];
                if (typeof value === 'object' && value !== null && !Array.isArray(value)) {
                    // For dictionary/map objects, extract both keys AND values
                    // Enhanced check for dictionary indicators including 'statistics' for StatType enums
                    if ((prefix && (prefix.includes('map') || prefix.includes('dict') || prefix.includes('status') || prefix.includes('statistics'))) ||
                        (key && (key.includes('map') || key.includes('dict') || key.includes('status') || key.includes('statistics')))) {
                        // For each key in the dictionary, extract the key as a number
                        Object.keys(value).forEach(dictKey => {
                            const keyAsNumber = parseFloat(dictKey);
                            if (!isNaN(keyAsNumber)) {
                                flatData[`${fullKey}_key_${dictKey}`] = keyAsNumber;
                            }
                        });
                    }
                    flattenData(value, fullKey);
                }
                else if (Array.isArray(value)) {
                    // For arrays, add each element with an index
                    value.forEach((item, index) => {
                        if (typeof item === 'object' && item !== null) {
                            flattenData(item, `${fullKey}[${index}]`);
                        }
                        else {
                            flatData[`${fullKey}[${index}]`] = item;
                            // Also try to extract as number if it's a string that looks like a number
                            if (typeof item === 'string' && !isNaN(parseFloat(item)) && isFinite(item)) {
                                flatData[`${fullKey}[${index}]`] = parseFloat(item);
                            }
                        }
                    });
                }
                else {
                    flatData[fullKey] = value;
                    // For string values that look like numbers, also store as numbers for validation
                    if (typeof value === 'string' && !isNaN(parseFloat(value)) && isFinite(value)) {
                        flatData[fullKey] = parseFloat(value);
                    }
                    // Skip extracting numbers from timestamp-related fields to avoid DateTime/Guid noise
                    // But allow legitimate timestamp values like lastupdate.seconds and lastupdate.nanos
                    if (typeof value === 'number' &&
                        (fullKey.toLowerCase().includes('starttime') ||
                            fullKey.toLowerCase().includes('sessionid'))) {
                        // Only exclude known noisy timestamp fields, but allow legitimate datetime fields like lastupdate
                        delete flatData[fullKey];
                    }
                }
            });
        }
        flattenData(extractedData);
        console.log('FLAT_DATA:', JSON.stringify(flatData));
        // Count total extracted values for verification
        const totalValues = Object.keys(flatData).length;
        console.log(`📊 Total extracted properties: ${totalValues}`);
        console.log('✅ Test passed');
        process.exit(0);
    }
    catch (extractError) {
        console.error('Error during data extraction:', extractError.message);
        console.error('Raw response type:', typeof response);
        console.error('Raw response constructor:', response.constructor.name);
        // Fallback: try direct method calls for known types
        try {
            if (response.getZoneListList && typeof response.getZoneListList === 'function') {
                const zones = response.getZoneListList();
                console.log('Received zones:', zones.length);
                if (zones.length >= 2) {
                    const t0 = zones[0].getTemperature();
                    const t1 = zones[1].getTemperature();
                    console.log('Temperatures:', t0, t1);
                    if (t0 === 42 && t1 === 43) {
                        console.log('FLAT_DATA:', JSON.stringify({
                            'zones[0].temperature': t0,
                            'zones[1].temperature': t1,
                            'zones[0].zone': zones[0].getZone ? zones[0].getZone() : 0,
                            'zones[1].zone': zones[1].getZone ? zones[1].getZone() : 1
                        }));
                        console.log('✅ Test passed! Successfully retrieved collection from server using grpc-web client');
                        process.exit(0);
                    }
                }
            }
        }
        catch (fallbackError) {
            console.error('Fallback method also failed:', fallbackError.message);
        }
        console.error('❌ Failed to extract meaningful data from response');
        process.exit(1);
    }
});
// Timeout after 10 seconds
setTimeout(() => {
    console.error('Test timed out after 10 seconds');
    process.exit(1);
}, 10000);
