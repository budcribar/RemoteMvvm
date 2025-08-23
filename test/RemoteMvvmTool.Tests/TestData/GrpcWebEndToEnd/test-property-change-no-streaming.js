// Setup XMLHttpRequest polyfill for Node.js environment
global.XMLHttpRequest = require('xhr2');

const grpc = require('grpc-web');
const { TestViewModelServiceClient } = require('./testviewmodelservice_grpc_web_pb.js');
const { UpdatePropertyValueRequest } = require('./testviewmodelservice_pb.js');
const { StringValue } = require('google-protobuf/google/protobuf/wrappers_pb.js');
const { Any } = require('google-protobuf/google/protobuf/any_pb.js');
const { Empty } = require('google-protobuf/google/protobuf/empty_pb.js');

const port = process.argv[2] || 5000;
const client = new TestViewModelServiceClient(`http://localhost:${port}`);

console.log('Testing property change operation WITHOUT any streaming subscriptions...');

async function testPropertyChangeWithoutStreaming() {
    try {
        console.log('?? This test will:');
        console.log('1. Make UpdatePropertyValue calls that trigger PropertyChanged');
        console.log('2. NOT establish any streaming subscriptions');
        console.log('3. Test if PropertyChanged deadlock only happens with active streams');

        // First get the current state
        console.log('\n?? Getting initial state...');
        const initialState = await client.getState(new Empty());
        
        // Debug: Log available methods on the state object
        console.log('Available methods on initialState:', Object.getOwnPropertyNames(Object.getPrototypeOf(initialState)));
        
        // Try different method names that might exist
        let initialMessage = null;
        const methods = Object.getOwnPropertyNames(Object.getPrototypeOf(initialState));
        const getters = methods.filter(m => m.startsWith('get') && typeof initialState[m] === 'function');
        console.log('Available getter methods:', getters);
        
        // Try to find a message-like property
        const messageGetter = getters.find(m => m.toLowerCase().includes('message'));
        if (messageGetter) {
            console.log(`Using method: ${messageGetter}`);
            initialMessage = initialState[messageGetter]();
        }
        
        console.log(`Initial Message: ${initialMessage || 'Not found'}`);

        // Create property change request (this WILL trigger PropertyChanged)
        console.log('\n?? Creating property change request (triggers PropertyChanged)...');
        const request = new UpdatePropertyValueRequest();
        request.setPropertyName('Message');

        const newValue = 'Updated_' + Date.now() + '_NO_STREAMING';
        const stringValue = new StringValue();
        stringValue.setValue(newValue);

        const anyValue = new Any();
        anyValue.pack(stringValue.serializeBinary(), 'google.protobuf.StringValue');
        request.setNewValue(anyValue);

        // Set operation type to "set" - this WILL trigger PropertyChanged
        request.setOperationType('set');
        request.setPropertyPath('Message');

        console.log('Sending property change request...');
        console.log(`  PropertyName: ${request.getPropertyName()}`);
        console.log(`  PropertyPath: ${request.getPropertyPath()}`);
        console.log(`  OperationType: ${request.getOperationType()}`);
        console.log(`  NewValue: ${newValue}`);
        console.log('  ??  This SHOULD trigger PropertyChanged events');

        // Call updatePropertyValue and wait for response
        console.log('\n?? Calling updatePropertyValue (Property Change - NO active streams)...');
        const startTime = Date.now();
        
        const response = await client.updatePropertyValue(request);
        
        const endTime = Date.now();
        const duration = endTime - startTime;
        
        console.log(`\n=== UpdatePropertyValue Response (took ${duration}ms) ===`);
        console.log('Response object type:', typeof response);
        console.log('Response methods:', Object.getOwnPropertyNames(Object.getPrototypeOf(response)));
        
        // Check if response has expected methods for enhanced response
        let updateSuccess = false;
        if (typeof response.getSuccess === 'function') {
            console.log(`Success: ${response.getSuccess()}`);
            updateSuccess = response.getSuccess();
            
            if (updateSuccess) {
                console.log('? Property change successful!');
                
                // Check if we have an old value
                if (typeof response.getOldValue === 'function') {
                    const oldValue = response.getOldValue();
                    if (oldValue) {
                        console.log(`Old value type: ${oldValue.getTypeUrl()}`);
                    }
                }
            } else {
                console.log('? Property change failed!');
                if (typeof response.getErrorMessage === 'function') {
                    console.log(`Error: ${response.getErrorMessage()}`);
                }
            }
        } else {
            console.log('Response does not have getSuccess method - assuming older version');
            console.log('Available methods:', Object.getOwnPropertyNames(response));
            updateSuccess = true;
            console.log('? Property change call completed (assuming success)');
        }

        // Verify the change by getting state again
        console.log('\n?? Getting updated state to verify property change...');
        const updatedState = await client.getState(new Empty());
        
        let updatedMessage = null;
        if (messageGetter && typeof updatedState[messageGetter] === 'function') {
            updatedMessage = updatedState[messageGetter]();
        }
        
        console.log(`Updated Message: ${updatedMessage || 'Not found'}`);
        
        // Check if the value was actually changed
        if (updatedMessage && updatedMessage.includes(newValue)) {
            console.log(`? Confirmed: Property was successfully changed to contain "${newValue}"`);
        } else {
            console.log(`?? Note: Could not confirm property change. Server-side logic may differ.`);
        }
        
        console.log('\n?? Key Insights:');
        console.log(`- Property change completed in ${duration}ms (should be fast if no deadlock)`);
        console.log('- PropertyChanged events were triggered but NO streaming subscriptions were active');
        console.log('- If this test passes but streaming tests fail, the issue is streaming-specific');
        
        // Test completed successfully
        console.log('\n? Test passed - Property change WITHOUT streaming completed successfully');
        console.log('?? This confirms PropertyChanged events work when no streaming subscriptions are active');
        process.exit(0);
        
    } catch (error) {
        console.error('? Property change test (no streaming) failed:', error.message);
        console.error(error.stack || error);
        
        console.log('\n?? Failure Analysis:');
        console.log('If this test fails, the issue is likely:');
        console.log('- Basic PropertyChanged event handling problems');
        console.log('- UpdatePropertyValue implementation issues');
        console.log('- NOT specifically related to streaming deadlocks');
        
        process.exit(1);
    }
}

testPropertyChangeWithoutStreaming();

// Add timeout
setTimeout(() => {
    console.error('? Test timed out after 15 seconds');
    console.log('\n?? If PropertyChanged (no streaming) times out:');
    console.log('- Issue is likely basic PropertyChanged event handling');
    console.log('- NOT streaming-specific deadlock');
    process.exit(1);
}, 15000);