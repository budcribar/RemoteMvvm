// Setup XMLHttpRequest polyfill for Node.js environment  
global.XMLHttpRequest = require('xhr2');

const svc = require('./testviewmodelservice_grpc_web_pb.js');
const pb = require('./testviewmodelservice_pb.js');
const { TestViewModelServiceClient } = svc;
const process = require('process');

const port = process.argv[2] || '5000';
const client = new TestViewModelServiceClient(`http://localhost:${port}`, null, null);

console.log('Starting pure streaming test (no UpdatePropertyValue calls)...');

// Get SubscribeRequest from protobuf messages
const { SubscribeRequest } = pb;
if (!SubscribeRequest) {
    console.error('? SubscribeRequest not found in protobuf messages');
    console.log('Available in pb:', Object.keys(pb));
    process.exit(1);
}

console.log('? Found SubscribeRequest constructor');

let receivedUpdate = false;
let timeoutId;

try {
  console.log('?? Establishing subscription...');
  const req = new SubscribeRequest();
  req.setClientId('streaming-test-' + Date.now());

  const stream = client.subscribeToPropertyChanges(req, {});
  
  stream.on('data', update => {
    console.log(`?? Received property change: ${update.getPropertyName()}`);
    receivedUpdate = true;
    clearTimeout(timeoutId);
    
    let value = '';
    const anyVal = update.getNewValue();
    
    if (anyVal) {
      try {
        // Try to extract string value
        const { StringValue } = require('google-protobuf/google/protobuf/wrappers_pb.js');
        
        if (anyVal.is && typeof anyVal.is === 'function') {
          if (anyVal.is(StringValue.getDescriptor())) {
            const str = anyVal.unpack(StringValue.deserializeBinary, 'google.protobuf.StringValue');
            if (str && str.getValue) {
              value = str.getValue();
            }
          }
        }
      } catch (err) {
        console.log('Error unpacking Any value:', err.message);
      }
    }
    
    console.log(`PROPERTY_CHANGE:${update.getPropertyName()}=${value}`);
    
    if (value === 'Updated' || update.getPropertyName() === 'Status') {
      console.log('? Test passed - received expected property change');
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
    clearTimeout(timeoutId);
    if (receivedUpdate) {
      console.log('? Test passed - stream ended after receiving update');
      process.exit(0);
    } else {
      console.error('? Stream ended without receiving any updates');
      process.exit(1);
    }
  });

  // Timeout after 30 seconds
  timeoutId = setTimeout(() => {
    console.error('? Test timed out after 30 seconds');
    if (!receivedUpdate) {
      console.log('No property change notifications received');
    }
    process.exit(1);
  }, 30000);

} catch (err) {
  console.error('? Failed to establish subscription:', err.message || err);
  process.exit(1);
}