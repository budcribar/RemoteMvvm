// Setup XMLHttpRequest polyfill for Node.js environment
global.XMLHttpRequest = require('xhr2');

function loadGenerated(modulePathLower, modulePathUpper) {
  try {
    return require(modulePathLower);
  } catch {
    return require(modulePathUpper);
  }
}

const svc = loadGenerated('./testviewmodelservice_grpc_web_pb.js', './TestViewModelService_grpc_web_pb.js');
const pb = loadGenerated('./testviewmodelservice_pb.js', './TestViewModelService_pb.js');
const { TestViewModelServiceClient } = svc;
const { Empty } = require('google-protobuf/google/protobuf/empty_pb.js');
const { StringValue } = require('google-protobuf/google/protobuf/wrappers_pb.js');
const { Any } = require('google-protobuf/google/protobuf/any_pb.js');
const process = require('process');

const port = process.argv[2] || '5000';
const client = new TestViewModelServiceClient(`http://localhost:${port}`, null, null);

console.log('Starting manual SubscribeToPropertyChanges test...');

// Enhanced SubscribeRequest detection
let SubscribeRequest;
let foundSubscribeRequest = false;

const possibleLocations = [
  () => pb.SubscribeRequest,
  () => svc.SubscribeRequest,
  () => pb.Test?.Protos?.SubscribeRequest,
  () => global.proto?.test_protos?.SubscribeRequest,
];

for (const getRequest of possibleLocations) {
  try {
    const req = getRequest();
    if (req && typeof req === 'function') {
      SubscribeRequest = req;
      foundSubscribeRequest = true;
      console.log('Found SubscribeRequest constructor');
      break;
    }
  } catch (e) {
    // Continue to next possibility
  }
}

if (!foundSubscribeRequest) {
  console.error('SubscribeRequest not found - using fallback');
  SubscribeRequest = function() {
    this.client_id = '';
    this.setClientId = function(id) { this.client_id = id; };
    this.getClientId = function() { return this.client_id; };
  };
}

async function testManualSubscription() {
    let receivedUpdate = false;
    let timeoutId;

    try {
        // First establish subscription
        console.log('Establishing subscription...');
        const req = new SubscribeRequest();
        req.setClientId('manual-test-client-' + Date.now());

        const stream = client.subscribeToPropertyChanges(req, {});

        // Set up event handlers
        stream.on('data', update => {
            console.log(`?? Received property change: ${update.getPropertyName()}`);
            receivedUpdate = true;
            clearTimeout(timeoutId);
            
            let value = '';
            const anyVal = update.getNewValue();
            
            if (anyVal) {
                try {
                    // Try unpacking as StringValue
                    if (anyVal.is && typeof anyVal.is === 'function') {
                        if (anyVal.is(StringValue.getDescriptor())) {
                            const str = anyVal.unpack(StringValue.deserializeBinary, 'google.protobuf.StringValue');
                            if (str && str.getValue) {
                                value = str.getValue();
                            }
                        }
                    } else {
                        // Fallback: try direct unpacking
                        try {
                            const str = StringValue.deserializeBinary(anyVal.getValue());
                            value = str.getValue();
                        } catch (directErr) {
                            console.log('Direct unpacking failed:', directErr.message);
                        }
                    }
                } catch (err) {
                    console.log('Error unpacking value:', err.message);
                    
                    // Raw byte decoding fallback
                    const valueBytes = anyVal.getValue ? anyVal.getValue() : null;
                    if (valueBytes && valueBytes.length > 0) {
                        let decoded = '';
                        for (let i = 0; i < valueBytes.length; i++) {
                            const byte = valueBytes[i];
                            if (byte >= 32 && byte <= 126) {
                                decoded += String.fromCharCode(byte);
                            }
                        }
                        if (decoded.includes('Updated')) {
                            value = 'Updated';
                        }
                    }
                }
            }
            
            console.log(`PROPERTY_CHANGE:${update.getPropertyName()}=${value}`);
            
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
        
        // Try using UpdatePropertyValue if available
        try {
            const { UpdatePropertyValueRequest } = require('./testviewmodelservice_pb.js');
            
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
            
        } catch (err) {
            console.error('? Failed to trigger update:', err.message);
            console.log('This may be expected if UpdatePropertyValue is not available');
        }
        
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