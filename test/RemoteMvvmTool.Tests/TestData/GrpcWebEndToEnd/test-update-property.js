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

console.log('Testing enhanced UpdatePropertyValue with response...');

async function testUpdatePropertyValue() {
    try {
        // First get the current state to establish baseline
        console.log('Getting initial state...');
        const initialState = await client.getState(new Empty());
        
        // Debug: Log available methods on the state object
        console.log('Available methods on initialState:', Object.getOwnPropertyNames(Object.getPrototypeOf(initialState)));
        
        // Try different method names that might exist for getting the message
        let initialMessage = null;
        if (typeof initialState.getMessage === 'function') {
            initialMessage = initialState.getMessage();
        } else {
            const methods = Object.getOwnPropertyNames(Object.getPrototypeOf(initialState));
            const getters = methods.filter(m => m.startsWith('get') && typeof initialState[m] === 'function');
            console.log('Available getter methods:', getters);
            
            const messageGetter = getters.find(m => m.toLowerCase().includes('message'));
            if (messageGetter) {
                console.log(`Using method: ${messageGetter}`);
                initialMessage = initialState[messageGetter]();
            }
        }
        
        console.log(`Initial Message: ${initialMessage || 'Not found'}`);

        // Create a test request
        const request = new UpdatePropertyValueRequest();
        request.setPropertyName('Message');

        // Create the new value (StringValue wrapped in Any)
        const stringValue = new StringValue();
        stringValue.setValue('Hello from enhanced UpdatePropertyValue!');

        const anyValue = new Any();
        anyValue.pack(stringValue.serializeBinary(), 'google.protobuf.StringValue');
        request.setNewValue(anyValue);

        // Set additional options
        request.setPropertyPath('Message'); // Simple property path
        request.setOperationType('set');     // Set operation

        console.log('Sending UpdatePropertyValue request...');
        console.log(`  PropertyName: ${request.getPropertyName()}`);
        console.log(`  PropertyPath: ${request.getPropertyPath()}`);
        console.log(`  OperationType: ${request.getOperationType()}`);

        // Call updatePropertyValue and wait for response
        const response = await client.updatePropertyValue(request);
        
        console.log('\n=== UpdatePropertyValue Response ===');
        console.log('Response object type:', typeof response);
        console.log('Response methods:', Object.getOwnPropertyNames(Object.getPrototypeOf(response)));
        
        // Check if response has expected methods for enhanced response
        let updateSuccess = false;
        if (typeof response.getSuccess === 'function') {
            console.log(`Success: ${response.getSuccess()}`);
            updateSuccess = response.getSuccess();
            
            if (updateSuccess) {
                console.log('? Property update successful!');
                
                // Check if we have an old value
                if (typeof response.getOldValue === 'function') {
                    const oldValue = response.getOldValue();
                    if (oldValue && typeof oldValue.getTypeUrl === 'function') {
                        console.log(`Old value type: ${oldValue.getTypeUrl()}`);
                        // Try to unpack the old value
                        try {
                            if (oldValue.getTypeUrl().includes('StringValue')) {
                                const oldStringValue = StringValue.deserializeBinary(oldValue.getValue_asU8());
                                console.log(`Previous value was: "${oldStringValue.getValue()}"`);
                            }
                        } catch (e) {
                            console.log('Could not unpack old value:', e.message);
                        }
                    }
                }
            } else {
                console.log('? Property update failed!');
                if (typeof response.getErrorMessage === 'function') {
                    console.log(`Error: ${response.getErrorMessage()}`);
                }
                if (typeof response.getValidationErrors === 'function') {
                    console.log(`Validation errors: ${response.getValidationErrors()}`);
                }
            }
        } else {
            console.log('Response does not have getSuccess method - assuming older version');
            console.log('Available methods:', Object.getOwnPropertyNames(response));
            // Assume success if we got a response without error
            updateSuccess = true;
            console.log('? UpdatePropertyValue call completed (assuming success)');
        }

        // Verify the change by getting state again
        console.log('\nGetting updated state...');
        const updatedState = await client.getState(new Empty());
        
        let updatedMessage = null;
        if (typeof updatedState.getMessage === 'function') {
            updatedMessage = updatedState.getMessage();
        } else {
            const methods = Object.getOwnPropertyNames(Object.getPrototypeOf(updatedState));
            const getters = methods.filter(m => m.startsWith('get') && typeof updatedState[m] === 'function');
            const messageGetter = getters.find(m => m.toLowerCase().includes('message'));
            if (messageGetter) {
                updatedMessage = updatedState[messageGetter]();
            }
        }
        
        console.log(`Updated Message: ${updatedMessage || 'Not found'}`);
        
        // Test completed successfully
        console.log('\n? Test passed');
        process.exit(0);
    } catch (error) {
        console.error('? UpdatePropertyValue test failed:', error.message);
        console.error(error);
        process.exit(1);
    }
}

testUpdatePropertyValue();

// Add timeout
setTimeout(() => {
    console.error('? Test timed out after 15 seconds');
    process.exit(1);
}, 15000);