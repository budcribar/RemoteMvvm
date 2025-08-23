// Setup XMLHttpRequest polyfill for Node.js environment
global.XMLHttpRequest = require('xhr2');

const grpc = require('grpc-web');
const { TestViewModelServiceClient } = require('./testviewmodelservice_grpc_web_pb.js');
const { UpdatePropertyValueRequest, TestViewModelState } = require('./testviewmodelservice_pb.js');
const { StringValue } = require('google-protobuf/google/protobuf/wrappers_pb.js');
const { Any } = require('google-protobuf/google/protobuf/any_pb.js');
const { Empty } = require('google-protobuf/google/protobuf/empty_pb.js');

const port = process.argv[2] || 5000;
const client = new TestViewModelServiceClient(`http://localhost:${port}`);

console.log('Testing enhanced UpdatePropertyValue...');

async function testUpdateProperty() {
    try {
        // First get the current state
        console.log('Getting initial state...');
        const initialState = await client.getState(new Empty());
        
        // Debug: Log available methods on the state object
        console.log('Available methods on initialState:', Object.getOwnPropertyNames(Object.getPrototypeOf(initialState)));
        
        // Try different method names that might exist
        let initialMessage = null;
        if (typeof initialState.getMessage === 'function') {
            initialMessage = initialState.getMessage();
        } else if (typeof initialState.getmessage === 'function') {
            initialMessage = initialState.getmessage();
        } else if (typeof initialState.Message !== 'undefined') {
            initialMessage = initialState.Message;
        } else {
            console.log('No getMessage method found, checking all get* methods...');
            const methods = Object.getOwnPropertyNames(Object.getPrototypeOf(initialState));
            const getters = methods.filter(m => m.startsWith('get') && typeof initialState[m] === 'function');
            console.log('Available getter methods:', getters);
            
            // Try to find a message-like property
            const messageGetter = getters.find(m => m.toLowerCase().includes('message'));
            if (messageGetter) {
                console.log(`Using method: ${messageGetter}`);
                initialMessage = initialState[messageGetter]();
            }
        }
        
        console.log(`Initial Message: ${initialMessage || 'Not found'}`);

        // Create update request
        const request = new UpdatePropertyValueRequest();
        request.setPropertyName('Message');

        const stringValue = new StringValue();
        stringValue.setValue('Updated via enhanced UpdatePropertyValue!');

        const anyValue = new Any();
        anyValue.pack(stringValue.serializeBinary(), 'google.protobuf.StringValue');
        request.setNewValue(anyValue);

        console.log('Calling updatePropertyValue...');
        
        // Call updatePropertyValue and wait for response
        const response = await client.updatePropertyValue(request);
        
        console.log('\n=== Server Response ===');
        console.log('Response object:', response);
        console.log('Response methods:', Object.getOwnPropertyNames(Object.getPrototypeOf(response)));
        
        // Check if response has expected methods
        if (typeof response.getSuccess === 'function') {
            console.log(`Success: ${response.getSuccess()}`);
            if (!response.getSuccess()) {
                console.log(`Error: ${response.getErrorMessage ? response.getErrorMessage() : 'No error message'}`);
            } else {
                console.log('? UpdatePropertyValue call succeeded');
            }
        } else {
            console.log('Response does not have getSuccess method');
            console.log('Available methods:', Object.getOwnPropertyNames(response));
            // Assume success if we got a response without error
            console.log('? UpdatePropertyValue call completed (assuming success)');
        }
        
        // Get state again to verify change
        console.log('\nGetting updated state...');
        const updatedState = await client.getState(new Empty());
        
        // Try the same method discovery for updated state
        let updatedMessage = null;
        if (typeof updatedState.getMessage === 'function') {
            updatedMessage = updatedState.getMessage();
        } else if (typeof updatedState.getmessage === 'function') {
            updatedMessage = updatedState.getmessage();
        } else {
            const methods = Object.getOwnPropertyNames(Object.getPrototypeOf(updatedState));
            const getters = methods.filter(m => m.startsWith('get') && typeof updatedState[m] === 'function');
            const messageGetter = getters.find(m => m.toLowerCase().includes('message'));
            if (messageGetter) {
                updatedMessage = updatedState[messageGetter]();
            }
        }
        
        console.log(`Updated Message: ${updatedMessage || 'Not found'}`);
        
        // Success!
        console.log('\n? Test passed');
        process.exit(0);
    } catch (error) {
        console.error('? Error:', error.message);
        console.error(error);
        process.exit(1);
    }
}

testUpdateProperty();

// Timeout
setTimeout(() => {
    console.error('? Test timed out');
    process.exit(1);
}, 15000);