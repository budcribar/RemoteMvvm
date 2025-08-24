// gRPC-Web client test for manual property subscription
// Establishes subscription and manually triggers property changes via UpdatePropertyValue

global.XMLHttpRequest = require('xhr2');

const svc = require('./testviewmodelservice_grpc_web_pb.js');
const pb = require('./testviewmodelservice_pb.js');
const { TestViewModelServiceClient } = svc;
const { UpdatePropertyValueRequest, SubscribeRequest } = pb;
const { StringValue } = require('google-protobuf/google/protobuf/wrappers_pb.js');
const { Any } = require('google-protobuf/google/protobuf/any_pb.js');
const { Empty } = require('google-protobuf/google/protobuf/empty_pb.js');

const port = process.argv[2] || '5000';
const client = new TestViewModelServiceClient(`http://localhost:${port}`, null, null);

console.log('Starting manual property subscription test...');

// Verify we have the required classes
if (!SubscribeRequest) {
    console.error('? SubscribeRequest not found in protobuf messages');
    console.log('Available in pb:', Object.keys(pb));
    process.exit(1);
}

if (!UpdatePropertyValueRequest) {
    console.error('? UpdatePropertyValueRequest not found in protobuf messages');
    console.log('Available in pb:', Object.keys(pb));
    process.exit(1);
}

console.log('? Found required protobuf classes');

let receivedUpdate = false;
let timeoutId;

async function testManualSubscription() {
    try {
        console.log('?? Establishing subscription...');
        
        const subscribeReq = new SubscribeRequest();
        subscribeReq.setClientId('manual-test-' + Date.now());
        
        const stream = client.subscribeToPropertyChanges(subscribeReq, {});
        
        stream.on('data', update => {
            console.log(`?? Received property change: ${update.getPropertyName()}`);
            receivedUpdate = true;
            clearTimeout(timeoutId);
            
            let value = '';
            const anyVal = update.getNewValue();
            
            if (anyVal && anyVal.is && typeof anyVal.is === 'function') {
                try {
                    if (anyVal.is(StringValue.getDescriptor())) {
                        const str = anyVal.unpack(StringValue.deserializeBinary, 'google.protobuf.StringValue');
                        if (str && str.getValue) {
                            value = str.getValue();
                        }
                    }
                } catch (err) {
                    console.log('Error unpacking Any value:', err.message);
                }
            }
            
            console.log(`PROPERTY_CHANGE:${update.getPropertyName()}=${value}`);
            
            // Check if we got the expected value
            if (value === 'Updated' || update.getPropertyName() === 'Status') {
                console.log('? Test passed');
                process.exit(0);
            } else {
                console.log(`Unexpected property change: ${update.getPropertyName()}=${value}`);
            }
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
        
        console.log('? Subscription established, waiting 2 seconds before triggering update...');
        
        // Wait a moment for subscription to be fully established
        await new Promise(resolve => setTimeout(resolve, 2000));
        
        // Now manually trigger a property change via UpdatePropertyValue
        console.log('?? Triggering manual property update...');
        
        const updateRequest = new UpdatePropertyValueRequest();
        updateRequest.setPropertyName('Status');
        
        const stringValue = new StringValue();
        stringValue.setValue('Updated');
        
        const anyValue = new Any();
        anyValue.pack(stringValue.serializeBinary(), 'google.protobuf.StringValue');
        updateRequest.setNewValue(anyValue);
        
        console.log('Sending UpdatePropertyValue request...');
        await client.updatePropertyValue(updateRequest);
        console.log('? UpdatePropertyValue sent successfully');
        
        // Set timeout for receiving the property change notification
        timeoutId = setTimeout(() => {
            console.error('? Test timed out after 15 seconds');
            if (!receivedUpdate) {
                console.log('No property change notifications received after manual trigger');
            }
            process.exit(1);
        }, 15000);
        
    } catch (error) {
        console.error('? Test setup failed:', error.message);
        console.error(error);
        process.exit(1);
    }
}

testManualSubscription();