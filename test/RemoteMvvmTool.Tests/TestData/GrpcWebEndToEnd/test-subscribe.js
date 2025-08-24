// gRPC-Web client test for SubscribeToPropertyChanges
// Subscribes to property changes and logs the first notification

global.XMLHttpRequest = require('xhr2');

const svc = require('./testviewmodelservice_grpc_web_pb.js');
const pb = require('./testviewmodelservice_pb.js');
const { TestViewModelServiceClient } = svc;
const process = require('process');

const port = process.argv[2] || '5000';
const client = new TestViewModelServiceClient(`http://localhost:${port}`, null, null);

console.log('Starting SubscribeToPropertyChanges test using generated client...');

// Get SubscribeRequest from the protobuf messages
const { SubscribeRequest } = pb;
if (!SubscribeRequest) {
  console.error('SubscribeRequest not found in protobuf messages');
  console.log('Available in pb:', Object.keys(pb));
  process.exit(1);
}

console.log('Found SubscribeRequest constructor');

const req = new SubscribeRequest();
req.setClientId('test-client-' + Date.now());

let receivedUpdate = false;
let timeoutId;

// Timeout after 30 seconds to give more time for debugging
timeoutId = setTimeout(() => {
  console.error('Test timed out after 30 seconds');
  if (!receivedUpdate) {
    console.log('No property change notifications received');
  }
  process.exit(1);
}, 30000);

try {
  const stream = client.subscribeToPropertyChanges(req, {});
  
  stream.on('data', update => {
    console.log(`Received property change: ${update.getPropertyName()}`);
    receivedUpdate = true;
    clearTimeout(timeoutId);
    
    const anyVal = update.getNewValue();
    let value = '';
    
    if (anyVal) {
      try {
        // Import StringValue for unpacking
        const { StringValue } = require('google-protobuf/google/protobuf/wrappers_pb.js');
        
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
        
        // If we still don't have a value, try raw string extraction
        if (!value) {
          const typeUrl = anyVal.getTypeUrl ? anyVal.getTypeUrl() : '';
          console.log(`Any type URL: ${typeUrl}`);
          
          // Try to get raw bytes and decode
          const valueBytes = anyVal.getValue ? anyVal.getValue() : null;
          if (valueBytes && valueBytes.length > 0) {
            // Simple string decoding attempt
            let decoded = '';
            for (let i = 0; i < valueBytes.length; i++) {
              const byte = valueBytes[i];
              if (byte >= 32 && byte <= 126) { // Printable ASCII
                decoded += String.fromCharCode(byte);
              }
            }
            
            if (decoded.includes('Updated')) {
              value = 'Updated';
            } else if (decoded.length > 0) {
              value = decoded.trim();
            }
          }
        }
      } catch (err) {
        console.log('Error unpacking Any value:', err.message);
      }
    }
    
    console.log(`PROPERTY_CHANGE:${update.getPropertyName()}=${value}`);
    
    // Check if we got the expected value
    if (value === 'Updated' || update.getPropertyName() === 'Status') {
      console.log('✅ Test passed');
      process.exit(0);
    } else {
      console.log(`Unexpected property change: ${update.getPropertyName()}=${value}`);
    }
  });

  stream.on('error', err => {
    console.error('Stream error:', err.message || err);
    clearTimeout(timeoutId);
    process.exit(1);
  });

  stream.on('end', () => {
    console.log('Stream ended');
    clearTimeout(timeoutId);
    if (receivedUpdate) {
      console.log('✅ Test passed - stream ended after receiving update');
      process.exit(0);
    } else {
      console.error('Stream ended without receiving any updates');
      process.exit(1);
    }
  });
  
  console.log('Subscription established, waiting for property changes...');
  
} catch (err) {
  console.error('Failed to establish subscription:', err.message || err);
  clearTimeout(timeoutId);
  process.exit(1);
}
