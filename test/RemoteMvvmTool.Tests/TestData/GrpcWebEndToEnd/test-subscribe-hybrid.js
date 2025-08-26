// Hybrid SubscribeToPropertyChanges test - tries both HTTP and HTTPS for streaming
// This approach tests both compatibility (HTTP) and proper streaming (HTTPS) support

global.XMLHttpRequest = require('xhr2');

const svc = require('./testviewmodelservice_grpc_web_pb.js');
const pb = require('./testviewmodelservice_pb.js');
const { TestViewModelServiceClient } = svc;
const { SubscribeRequest } = pb;
const { StringValue } = require('google-protobuf/google/protobuf/wrappers_pb.js');
const process = require('process');

const port = process.argv[2] || '5000';
const httpPort = parseInt(port);
const httpsPort = httpPort + 1000; // HTTPS port is HTTP port + 1000

console.log('Starting SubscribeToPropertyChanges hybrid test...');
console.log(`HTTP endpoint: http://localhost:${httpPort}`);
console.log(`HTTPS endpoint: https://localhost:${httpsPort}`);

let successCount = 0;
let totalTests = 2;
let receivedPropertyChange = false;

// Test timeout
const testTimeoutMs = 10000;
const timeoutId = setTimeout(() => {
    if (receivedPropertyChange) {
        console.log('? Test passed - property change detected via hybrid approach');
        process.exit(0);
    } else {
        console.error('? Test timed out - no property changes detected');
        process.exit(1);
    }
}, testTimeoutMs);

async function testEndpoint(baseUrl, endpointName) {
    return new Promise((resolve, reject) => {
        console.log(`\n?? Testing ${endpointName} endpoint: ${baseUrl}`);
        
        let client;
        try {
            client = new TestViewModelServiceClient(baseUrl);
            console.log(`? ${endpointName} client initialized`);
        } catch (initError) {
            console.log(`?? ${endpointName} client initialization failed: ${initError.message}`);
            resolve(false);
            return;
        }

        try {
            const req = new SubscribeRequest();
            req.setClientId(`hybrid-test-${endpointName.toLowerCase()}-${Date.now()}`);

            console.log(`?? Creating ${endpointName} subscription...`);
            const stream = client.subscribeToPropertyChanges(req, {});

            // Set up event handlers
            stream.on('data', (update) => {
                try {
                    console.log(`?? ${endpointName} STREAMING: Received property change: ${update.getPropertyName()}`);
                    receivedPropertyChange = true;
                    
                    let value = '';
                    const anyVal = update.getNewValue();
                    
                    if (anyVal) {
                        try {
                            if (anyVal.is && typeof anyVal.is === 'function' && anyVal.is(StringValue.getDescriptor())) {
                                const str = anyVal.unpack(StringValue.deserializeBinary, 'google.protobuf.StringValue');
                                if (str && str.getValue) {
                                    value = str.getValue();
                                }
                            }
                        } catch (err) {
                            console.log(`${endpointName} error unpacking value:`, err.message);
                        }
                    }
                    
                    console.log(`PROPERTY_CHANGE:${update.getPropertyName()}=${value}`);
                    
                    console.log(`? ${endpointName} streaming successful!`);
                    clearTimeout(timeoutId);
                    console.log('? Test passed - property change detected via hybrid approach');
                    process.exit(0);
                    
                } catch (dataError) {
                    console.error(`${endpointName} error processing data:`, dataError.message);
                }
            });

            stream.on('error', (err) => {
                console.log(`?? ${endpointName} stream error: ${err.message || err}`);
                resolve(false);
            });

            stream.on('end', () => {
                console.log(`${endpointName} stream ended`);
                resolve(false);
            });
            
            console.log(`? ${endpointName} subscription established, waiting for property changes...`);
            
            // Give this endpoint some time to work
            setTimeout(() => {
                if (!receivedPropertyChange) {
                    console.log(`?? ${endpointName} endpoint did not receive property changes within timeout`);
                    resolve(false);
                }
            }, 5000);
            
        } catch (streamError) {
            console.log(`?? ${endpointName} streaming failed: ${streamError.message}`);
            resolve(false);
        }
    });
}

async function runHybridTest() {
    try {
        // Test both endpoints simultaneously
        console.log('\n?? Starting hybrid endpoint test...');
        
        const httpPromise = testEndpoint(`http://localhost:${httpPort}`, 'HTTP');
        const httpsPromise = testEndpoint(`https://localhost:${httpsPort}`, 'HTTPS');
        
        // Wait for either endpoint to succeed
        const results = await Promise.allSettled([httpPromise, httpsPromise]);
        
        const httpResult = results[0];
        const httpsResult = results[1];
        
        console.log(`\nHTTP result: ${httpResult.status === 'fulfilled' ? httpResult.value : 'rejected'}`);
        console.log(`HTTPS result: ${httpsResult.status === 'fulfilled' ? httpsResult.value : 'rejected'}`);
        
        if (receivedPropertyChange) {
            console.log('? Test passed - property change detected via hybrid approach');
            clearTimeout(timeoutId);
            process.exit(0);
        } else {
            console.log('?? Neither endpoint successfully received property changes');
            // Let timeout handle the final result
        }
        
    } catch (error) {
        console.error('? Hybrid test failed:', error.message);
        clearTimeout(timeoutId);
        process.exit(1);
    }
}

// Start the test
runHybridTest().catch(err => {
    console.error('? Test execution failed:', err.message);
    clearTimeout(timeoutId);
    process.exit(1);
});