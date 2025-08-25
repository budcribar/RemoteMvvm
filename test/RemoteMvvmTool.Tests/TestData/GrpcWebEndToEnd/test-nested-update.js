global.XMLHttpRequest = require('xhr2');
const { TestViewModelServiceClient } = require('./testviewmodelservice_grpc_web_pb.js');
const { SubscribeRequest, UpdatePropertyValueRequest } = require('./testviewmodelservice_pb.js');
const { Int32Value } = require('google-protobuf/google/protobuf/wrappers_pb.js');
const { Any } = require('google-protobuf/google/protobuf/any_pb.js');

const port = process.argv[2] || 5000;
const client = new TestViewModelServiceClient(`http://localhost:${port}`);

const subReq = new SubscribeRequest();
subReq.setClientId('nested-test');
const stream = client.subscribeToPropertyChanges(subReq);

stream.on('data', update => {
    const anyVal = update.getNewValue();
    const val = anyVal.unpack(Int32Value.deserializeBinary, 'google.protobuf.Int32Value').getValue();
    console.log(`PROPERTY_CHANGE:${update.getPropertyPath()}=${val}`);
    console.log('Test passed');
    process.exit(0);
});

stream.on('error', err => {
    console.error('Stream error', err);
    process.exit(1);
});

const req = new UpdatePropertyValueRequest();
req.setPropertyName('ZoneList');
req.setPropertyPath('ZoneList[0].Temperature');
req.setOperationType('set');
const wrapper = new Int32Value();
wrapper.setValue(55);
const anyVal = new Any();
anyVal.pack(wrapper.serializeBinary(), 'google.protobuf.Int32Value');
req.setNewValue(anyVal);
client.updatePropertyValue(req);

setTimeout(() => { console.error('No update received'); process.exit(1); }, 10000);
