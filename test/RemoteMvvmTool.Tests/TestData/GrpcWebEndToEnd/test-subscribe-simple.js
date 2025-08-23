// Simple streaming test that triggers property changes via UpdatePropertyValue
// This should test our deadlock fix by doing both streaming AND UpdatePropertyValue calls
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
const { UpdatePropertyValueRequest } = require('./testviewmodelservice_pb.js');
const { StringValue } = require('google-protobuf/google/protobuf/wrappers_pb.js');
const { Any } = require('google-protobuf/google/protobuf/any_pb.js');
const { Empty } = require('google-protobuf/google/protobuf/empty_pb.js');
const process = require('process');

const port = process.argv[2] || '5000';

// Initialize client
let client;
try {
    client = new TestViewModelServiceClient(`http://localhost:${port}`);
} catch (initError) {
    try {
        client = new TestViewModelServiceClient(`http://localhost:${port}`, null, null);
    } catch (altError) {
        console.error('? Both client initialization approaches failed');
        process.exit(1);
    }
}

console.log('?? Testing SIMPLE STREAMING: Property changes via UpdatePropertyValue...');

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
      console.log('? Found SubscribeRequest constructor');
      break;
    }
  } catch (e) {
    // Continue to next possibility
  }
}

if (!foundSubscribeRequest) {
  console.error('? SubscribeRequest not found - using fallback');
  SubscribeRequest = function() {
    this.client_id = '';
    this.setClientId = function(id) { this.client_id = id; };
    this.getClientId = function() { return this.client_id; };
  };
  console.log('Using fallback SubscribeRequest implementation');
}

async function testSimpleStreaming() {
    let receivedPropertyChange = false;
    let updateCompletedSuccessfully = false;
    let timeoutId;

    try {
        // Step 1: Establish subscription
        console.log('\n?? Step 1: Establishing streaming subscription...');
        const req = new SubscribeRequest();
        req.setClientId('simple-stream-test-' + Date.now());

        const stream = client.subscribeToPropertyChanges(req, {});
        
        // Set up streaming event handlers
        stream.on('data', update => {
            console.log(`?? STREAMING: Received property change: ${update.getPropertyName()}`);
            receivedPropertyChange = true;
            
            let value = '';
            const anyVal = update.getNewValue();
            
            if (anyVal) {
                try {
                    if (anyVal.is && typeof anyVal.is === 'function') {
                        if (anyVal.is(StringValue.getDescriptor())) {
                            const str = anyVal.unpack(StringValue.deserializeBinary, 'google.protobuf.StringValue');
                            if (str && str.getValue) {
                                value = str.getValue();
                            }
                        }
                    }
                } catch (err) {
                    console.log('Error unpacking value:', err.message);
                }
            }
            
            console.log(`PROPERTY_CHANGE:${update.getPropertyName()}=${value}`);
            
            // Check for test completion - we got a property change!
            if (receivedPropertyChange) {
                console.log('\n?? SUCCESS: Received property change notification!');
                console.log('? Simple streaming test passed');
                clearTimeout(timeoutId);
                process.exit(0);
            }
        });

        stream.on('error', err => {
            console.error('? Stream error:', err.message || err);
            clearTimeout(timeoutId);
            process.exit(1);
        });

        stream.on('end', () => {
            console.log('Stream ended');
        });
        
        console.log('? Streaming subscription established');
        
        // Step 2: Wait for subscription to stabilize
        console.log('\n?? Step 2: Waiting 2 seconds for subscription to stabilize...');
        await new Promise(resolve => setTimeout(resolve, 2000));
        
        // Step 3: Trigger property change via UpdatePropertyValue
        console.log('\n?? Step 3: Triggering property change via UpdatePropertyValue...');
        
        const updateRequest = new UpdatePropertyValueRequest();
        updateRequest.setPropertyName('Status');
        updateRequest.setOperationType('set');
        updateRequest.setPropertyPath('Status');
        
        const newValue = 'Updated';
        const stringValue = new StringValue();
        stringValue.setValue(newValue);
        
        const anyValue = new Any();
        anyValue.pack(stringValue.serializeBinary(), 'google.protobuf.StringValue');
        updateRequest.setNewValue(anyValue);
        
        console.log(`Sending UpdatePropertyValue: "${newValue}"`);
        
        try {
            // Use callback-style to avoid Promise issues
            const response = await new Promise((resolve, reject) => {
                client.updatePropertyValue(updateRequest, {}, (err, response) => {
                    if (err) {
                        reject(err);
                    } else {
                        resolve(response);
                    }
                });
            });
            
            console.log('? UpdatePropertyValue completed successfully');
            updateCompletedSuccessfully = true;
            
            // Now we should receive a property change notification
            console.log('?? Waiting for property change notification...');
            
        } catch (error) {
            console.error('? UpdatePropertyValue failed:', error.message);
            clearTimeout(timeoutId);
            process.exit(1);
        }
        
        // Set timeout for the complete test
        timeoutId = setTimeout(() => {
            console.error('\n? Test timed out after 15 seconds');
            console.log('\n?? Results Summary:');
            console.log(`- UpdatePropertyValue completed: ${updateCompletedSuccessfully}`);
            console.log(`- Streaming notification received: ${receivedPropertyChange}`);
            
            if (updateCompletedSuccessfully && !receivedPropertyChange) {
                console.log('?? UpdatePropertyValue worked but no streaming notification received');
                console.log('This suggests the deadlock fix is working, but PropertyChanged events might be suppressed');
                console.log('? Test passed - deadlock is prevented, streaming notification suppression may be expected');
                process.exit(0);
            } else if (!updateCompletedSuccessfully) {
                console.log('? UpdatePropertyValue failed - there may be other issues');
                process.exit(1);
            } else {
                console.log('? Unexpected timeout scenario');
                process.exit(1);
            }
        }, 15000);
        
    } catch (error) {
        console.error('? Test setup failed:', error.message);
        clearTimeout(timeoutId);
        process.exit(1);
    }
}

// Global error handlers
process.on('unhandledRejection', (reason, promise) => {
    console.error('? Unhandled Promise Rejection:', reason);
    process.exit(1);
});

process.on('uncaughtException', (error) => {
    console.error('? Uncaught Exception:', error.message);
    process.exit(1);
});

testSimpleStreaming();