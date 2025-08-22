const process = require('process');

(async () => {
  console.log('Starting gRPC-Web test with protoc-generated protobuf parsing...');
  
  try {
    // Try to require the generated protobuf files
    let TestViewModelState;
    let ThermalZoneComponentViewModelState;
    
    try {
      // Import generated protobuf classes
      const pb = require('./testviewmodelservice_pb.js');
      TestViewModelState = pb.TestViewModelState;
      ThermalZoneComponentViewModelState = pb.ThermalZoneComponentViewModelState;
      console.log('Successfully loaded protoc-generated protobuf classes');
    } catch (importError) {
      console.warn('Could not load generated protobuf classes:', importError.message);
      throw importError;
    }
    
    console.log('Making gRPC-web call...');
    
    const port = process.argv[2] || '5000';
    const response = await fetch(`http://localhost:${port}/test_protos.TestViewModelService/GetState`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/grpc-web+proto' },
      body: new Uint8Array([0,0,0,0,0]) // Empty protobuf message for GetState
    });
    
    if (!response.ok) {
      throw new Error(`HTTP ${response.status}: ${response.statusText}`);
    }
    
    const buffer = await response.arrayBuffer();
    const bytes = new Uint8Array(buffer);
    
    console.log('Response received:', bytes.length, 'bytes');
    console.log('First 20 bytes:', Array.from(bytes.slice(0, 20)));
    
    if (bytes.length < 5) {
      throw new Error('Response too short');
    }
    
    // Parse gRPC-web response format: [compressed_flag][message_length][message_data]
    const messageLength = (bytes[1] << 24) | (bytes[2] << 16) | (bytes[3] << 8) | bytes[4];
    console.log('Message length from header:', messageLength);
    
    if (messageLength === 0) {
      console.error('Server returned empty message - this suggests a server-side issue');
      console.log('Full response bytes:', Array.from(bytes.slice(0, 100)));
      throw new Error('Empty message returned from server');
    }
    
    const messageBytes = bytes.slice(5, 5 + messageLength);
    console.log('Protobuf message bytes:', messageBytes.length, 'bytes');
    
    // Use the protoc-generated classes to deserialize
    const state = TestViewModelState.deserializeBinary(messageBytes);
    console.log('Deserialized state using protoc-generated protobuf:', state);
    
    const zoneList = state.getZoneListList ? state.getZoneListList() : [];
    console.log('Zone list from protobuf:', zoneList.length, 'items');
    
    const zones = zoneList.map((zone, index) => ({
      zone: zone.getZone ? zone.getZone() : index,
      temperature: zone.getTemperature ? zone.getTemperature() : 0
    }));
    
    console.log('Final parsed zones:', zones);
    
    if (zones.length < 2) {
      throw new Error(`Expected at least 2 zones, got ${zones.length}`);
    }
    
    if (zones[0].temperature !== 42 || zones[1].temperature !== 43) {
      throw new Error(`Expected temperatures [42, 43], got [${zones[0].temperature}, ${zones[1].temperature}]`);
    }
    
    console.log('? Test passed! Successfully retrieved collection from server using protoc-generated protobuf parsing');
    console.log(`Zone 1: Zone=${zones[0].zone}, Temperature=${zones[0].temperature}`);
    console.log(`Zone 2: Zone=${zones[1].zone}, Temperature=${zones[1].temperature}`);
    
  } catch (error) {
    console.error('? Test failed:', error);
    throw error;
  }
})().catch(e => { 
  console.error('Unhandled error:', e); 
  process.exit(1); 
});