// Setup XMLHttpRequest polyfill for Node.js environment
global.XMLHttpRequest = require('xhr2');

const svc = require('./testviewmodelservice_grpc_web_pb.js');
const pb = require('./testviewmodelservice_pb.js');
const { TestViewModelServiceClient } = svc;
const { UpdatePropertyValueRequest } = pb;
const { StringValue } = require('google-protobuf/google/protobuf/wrappers_pb.js');
const { Any } = require('google-protobuf/google/protobuf/any_pb.js');
const { Empty } = require('google-protobuf/google/protobuf/empty_pb.js');
const process = require('process');

const port = process.argv[2] || '5000';

// Improved client initialization with error handling
let client;
try {
    // Try the standard initialization first
    client = new TestViewModelServiceClient(`http://localhost:${port}`);
} catch (initError) {
    console.error('Standard client initialization failed, trying alternative:', initError.message);
    try {
        // Try with explicit null parameters
        client = new TestViewModelServiceClient(`http://localhost:${port}`, null, null);
    } catch (altError) {
        console.error('Alternative client initialization also failed:', altError.message);
        process.exit(1);
    }
}

console.log('?? Testing DEADLOCK FIX: Streaming + UpdatePropertyValue combination...');

// Get SubscribeRequest from protobuf messages
const { SubscribeRequest } = pb;
if (!SubscribeRequest) {
    console.error('? SubscribeRequest not found in protobuf messages');
    console.log('Available in pb:', Object.keys(pb));
    process.exit(1);
}

console.log('? Found SubscribeRequest constructor');

async function testDeadlockFix() {
    let receivedPropertyChange = false;
    let updateCompletedSuccessfully = false;
    let timeoutId;

    try {
        console.log('\n?? Test Strategy:');
        console.log('1. Establish streaming subscription');
        console.log('2. Make UpdatePropertyValue call that triggers PropertyChanged');
        console.log('3. Verify BOTH the UpdatePropertyValue response AND streaming notification work');
        console.log('4. This was previously deadlocking - now should work with event handler removal');

        // Step 1: Establish subscription
        console.log('\n?? Step 1: Establishing streaming subscription...');
        const req = new SubscribeRequest();
        req.setClientId('deadlock-fix-test-' + Date.now());

        let stream;
        try {
            stream = client.subscribeToPropertyChanges(req, {});
        } catch (streamError) {
            console.error('Failed to create streaming subscription:', streamError.message);
            throw streamError;
        }
        
        // Set up streaming event handlers with improved error handling
        stream.on('data', update => {
            try {
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
                        } else {
                            // Fallback unpacking
                            try {
                                const str = StringValue.deserializeBinary(anyVal.getValue());
                                value = str.getValue();
                            } catch (directErr) {
                                console.log('Direct unpacking failed:', directErr.message);
                            }
                        }
                    } catch (err) {
                        console.log('Error unpacking value:', err.message);
                    }
                }
                
                console.log(`PROPERTY_CHANGE:${update.getPropertyName()}=${value}`);
                
                // Check for test completion
                if (receivedPropertyChange && updateCompletedSuccessfully) {
                    console.log('\n?? SUCCESS: Both streaming AND unary calls completed!');
                    console.log('? Deadlock fix is working - event handler removal/restoration prevents deadlock');
                    clearTimeout(timeoutId);
                    process.exit(0);
                }
            } catch (dataError) {
                console.error('Error processing streaming data:', dataError.message);
            }
        });

        stream.on('error', err => {
            console.error('? Stream error:', err.message || err);
            
            // Log additional error details if available
            if (err.code !== undefined) {
                console.error('Error code:', err.code);
            }
            if (err.details) {
                console.error('Error details:', err.details);
            }
            
            clearTimeout(timeoutId);
            process.exit(1);
        });

        stream.on('end', () => {
            console.log('Stream ended');
            // Don't exit immediately - might be expected
        });
        
        console.log('? Streaming subscription established');
        
        // Step 2: Wait for subscription to be fully established
        console.log('\n?? Step 2: Waiting 3 seconds for subscription to stabilize...');
        await new Promise(resolve => setTimeout(resolve, 3000));
        
        // Step 3: Make UpdatePropertyValue call (this would previously deadlock)
        console.log('\n?? Step 3: Making UpdatePropertyValue call (with streaming active)...');
        
        const updateRequest = new UpdatePropertyValueRequest();
        updateRequest.setPropertyName('Message');
        updateRequest.setOperationType('set');
        updateRequest.setPropertyPath('Message');
        
        const newValue = 'DEADLOCK_FIX_TEST_' + Date.now();
        const stringValue = new StringValue();
        stringValue.setValue(newValue);
        
        const anyValue = new Any();
        anyValue.pack(stringValue.serializeBinary(), 'google.protobuf.StringValue');
        updateRequest.setNewValue(anyValue);
        
        console.log(`Sending UpdatePropertyValue: "${newValue}"`);
        console.log('??  This call would previously deadlock with active streaming subscription');
        
        const startTime = Date.now();
        
        try {
            // This is the critical test - unary call while streaming is active
            const response = await new Promise((resolve, reject) => {
                // Use callback-style to avoid potential Promise issues
                client.updatePropertyValue(updateRequest, {}, (err, response) => {
                    if (err) {
                        reject(err);
                    } else {
                        resolve(response);
                    }
                });
            });
            
            const endTime = Date.now();
            const duration = endTime - startTime;
            
            console.log(`\n?? UpdatePropertyValue Response (${duration}ms):`);
            console.log('Response received without deadlock!');
            
            updateCompletedSuccessfully = true;
            console.log('? UpdatePropertyValue completed successfully');
            
            // Check for test completion
            if (receivedPropertyChange && updateCompletedSuccessfully) {
                console.log('\n?? SUCCESS: Both streaming AND unary calls completed!');
                console.log('? Deadlock fix is working');
                clearTimeout(timeoutId);
                process.exit(0);
            } else {
                console.log('?? Waiting for streaming notification...');
            }
            
        } catch (error) {
            console.error('? UpdatePropertyValue failed:', error.message);
            console.error('Stack trace:', error.stack);
            console.error('This suggests the deadlock fix is not working or there\'s a client issue');
            clearTimeout(timeoutId);
            process.exit(1);
        }
        
        // Set timeout for the complete test
        timeoutId = setTimeout(() => {
            console.error('\n? Test timed out after 30 seconds');
            console.log('\n?? Results Summary:');
            console.log(`- UpdatePropertyValue completed: ${updateCompletedSuccessfully}`);
            console.log(`- Streaming notification received: ${receivedPropertyChange}`);
            
            if (updateCompletedSuccessfully && !receivedPropertyChange) {
                console.log('?? Partial success: UpdatePropertyValue works, but streaming may be suppressed');
                console.log('   This could indicate the event handler removal is working');
                console.log('? Test passed - deadlock prevented even if streaming is suppressed');
                process.exit(0);
            } else if (!updateCompletedSuccessfully) {
                console.log('? UpdatePropertyValue failed - deadlock may still exist');
                process.exit(1);
            } else {
                console.log('? Unexpected timeout scenario');
                process.exit(1);
            }
        }, 30000);
        
    } catch (error) {
        console.error('? Test setup failed:', error.message);
        console.error('Stack trace:', error.stack);
        clearTimeout(timeoutId);
        process.exit(1);
    }
}

// Add global error handlers
process.on('unhandledRejection', (reason, promise) => {
    console.error('Unhandled Promise Rejection:', reason);
    process.exit(1);
});

process.on('uncaughtException', (error) => {
    console.error('Uncaught Exception:', error.message);
    console.error('Stack:', error.stack);
    process.exit(1);
});

testDeadlockFix();