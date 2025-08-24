// Simple gRPC-Web client test for SubscribeToPropertyChanges
// This is a simplified version for basic testing

global.XMLHttpRequest = require('xhr2');

const svc = require('./testviewmodelservice_grpc_web_pb.js');
const pb = require('./testviewmodelservice_pb.js');
const { TestViewModelServiceClient } = svc;
const process = require('process');

const port = process.argv[2] || '5000';
const client = new TestViewModelServiceClient(`http://localhost:${port}`, null, null);

console.log('Starting simple SubscribeToPropertyChanges test...');

// Get SubscribeRequest from protobuf messages
const { SubscribeRequest } = pb;
if (!SubscribeRequest) {
    console.error('? SubscribeRequest not found in protobuf messages');
    console.log('Available in pb:', Object.keys(pb));
    process.exit(1);
}

console.log('? Found SubscribeRequest constructor');

const req = new SubscribeRequest();
req.setClientId('simple-test-' + Date.now());

let receivedUpdate = false;
let timeoutId;

// Timeout after 20 seconds  
timeoutId = setTimeout(() => {
    console.error('? Test timed out after 20 seconds');
    if (!receivedUpdate) {
        console.log('No property change notifications received');
    }
    process.exit(1);
}, 20000);

try {
    const stream = client.subscribeToPropertyChanges(req, {});
    
    stream.on('data', update => {
        console.log(`?? Received property change: ${update.getPropertyName()}`);
        receivedUpdate = true;
        clearTimeout(timeoutId);
        
        const anyVal = update.getNewValue();
        let value = '';
        
        if (anyVal) {
            try {
                const { StringValue } = require('google-protobuf/google/protobuf/wrappers_pb.js');
                
                if (anyVal.is && typeof anyVal.is === 'function') {
                    if (anyVal.is(StringValue.getDescriptor())) {
                        const str = anyVal.unpack(StringValue.deserializeBinary, 'google.protobuf.StringValue');
                        if (str && str.getValue) {
                            value = str.getValue();
                        }
                    }
                }
            } catch (err) {
                console.log('Error unpacking Any value:', err.message);
            }
        }
        
        console.log(`PROPERTY_CHANGE:${update.getPropertyName()}=${value}`);
        console.log('? Test passed - received property change notification');
        process.exit(0);
    });

    stream.on('error', err => {
        console.error('? Stream error:', err.message || err);
        clearTimeout(timeoutId);
        process.exit(1);
    });

    stream.on('end', () => {
        console.log('Stream ended');
        clearTimeout(timeoutId);
        if (receivedUpdate) {
            console.log('? Test passed - stream ended after receiving update');
            process.exit(0);
        } else {
            console.error('? Stream ended without receiving updates');
            process.exit(1);
        }
    });
    
    console.log('? Subscription established, waiting for property changes...');
    
} catch (err) {
    console.error('? Failed to establish subscription:', err.message || err);
    clearTimeout(timeoutId);
    process.exit(1);
}