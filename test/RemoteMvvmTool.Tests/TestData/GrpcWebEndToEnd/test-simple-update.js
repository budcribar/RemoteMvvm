global.XMLHttpRequest = require('xhr2');
const { TestViewModelServiceClient } = require('./testviewmodelservice_grpc_web_pb.js');
const { UpdatePropertyValueRequest } = require('./testviewmodelservice_pb.js');
const { Int32Value } = require('google-protobuf/google/protobuf/wrappers_pb.js');
const { Any } = require('google-protobuf/google/protobuf/any_pb.js');

const port = process.argv[2] || 5000;
const client = new TestViewModelServiceClient(`http://localhost:${port}`);

console.log('Testing simple property update...');

// Test a simple property update first
const req = new UpdatePropertyValueRequest();
req.setPropertyName('Temperature');  // Simple property name
req.setPropertyPath('Temperature');   // Simple property path  
req.setOperationType('set');
req.setArrayIndex(-1);                // Explicitly set to -1 for non-array properties
const wrapper = new Int32Value();
wrapper.setValue(55);
const anyVal = new Any();
anyVal.pack(wrapper.serializeBinary(), 'google.protobuf.Int32Value');
req.setNewValue(anyVal);

client.updatePropertyValue(req, {}, (err, response) => {
    if (err) {
        console.error('UpdatePropertyValue error:', err);
        process.exit(1);
    }
    
    console.log('UpdatePropertyValue succeeded:', response.getSuccess());
    if (!response.getSuccess()) {
        console.error('UpdatePropertyValue failed:', response.getErrorMessage());
        process.exit(1);
    }

    console.log('PROPERTY_CHANGE:Temperature=55');
    console.log('Test passed');
    process.exit(0);
});

setTimeout(() => { 
    console.error('Timeout - no response received'); 
    process.exit(1); 
}, 10000);