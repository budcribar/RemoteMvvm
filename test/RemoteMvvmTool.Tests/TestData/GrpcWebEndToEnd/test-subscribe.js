// gRPC-Web client test for SubscribeToPropertyChanges
// Subscribes to property changes and logs the first notification

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
const { SubscribeRequest } = pb;
const { StringValue } = require('google-protobuf/google/protobuf/wrappers_pb.js');
const process = require('process');

const port = process.argv[2] || '5000';
const client = new TestViewModelServiceClient(`http://localhost:${port}`, null, null);

console.log('Starting SubscribeToPropertyChanges test using generated client...');

const req = new SubscribeRequest();
req.setClientId('test-client');

const stream = client.subscribeToPropertyChanges(req, {});
stream.on('data', update => {
  const anyVal = update.getNewValue();
  let value = '';
  if (anyVal) {
    const str = anyVal.unpack(StringValue.deserializeBinary, 'google.protobuf.StringValue');
    if (str) {
      value = str.getValue();
    }
  }
  console.log(`PROPERTY_CHANGE:${update.getPropertyName()}=${value}`);
  console.log('✅ Test passed');
  process.exit(0);
});
stream.on('error', err => {
  console.error('Stream error:', err);
  process.exit(1);
});

setTimeout(() => {
  console.error('Test timed out after 10 seconds');
  process.exit(1);
}, 10000);
