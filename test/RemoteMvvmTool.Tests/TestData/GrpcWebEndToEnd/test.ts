declare var process: any;

(async () => {
  console.log('Starting gRPC-Web test with generated protobuf parsing...');
  
  try {
    // Try to require the generated protobuf files
    let TestViewModelState: any;
    let ThermalZoneComponentViewModelState: any;
    
    try {
      // Import generated protobuf classes
      const pb = require('./testviewmodelservice_pb.js');
      TestViewModelState = pb.TestViewModelState;
      ThermalZoneComponentViewModelState = pb.ThermalZoneComponentViewModelState;
      console.log('Successfully loaded generated protobuf classes');
    } catch (importError: any) {
      console.warn('Could not load generated protobuf classes, using fallback parsing:', importError.message);
      // Fall back to manual parsing if protobuf classes not available
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
    console.log('Message data:', Array.from(messageBytes.slice(0, 50)));
    
    let zones: any[] = [];
    
    if (TestViewModelState && ThermalZoneComponentViewModelState) {
      try {
        // Use generated protobuf classes to deserialize
        const state = TestViewModelState.deserializeBinary(messageBytes);
        console.log('Deserialized state using protobuf:', state);
        
        const zoneList = state.getZoneListList ? state.getZoneListList() : [];
        console.log('Zone list from protobuf:', zoneList.length, 'items');
        
        zones = zoneList.map((zone: any, index: number) => ({
          zone: zone.getZone ? zone.getZone() : index,
          temperature: zone.getTemperature ? zone.getTemperature() : 0
        }));
      } catch (pbError: any) {
        console.warn('Protobuf deserialization failed:', pbError.message);
        console.log('Falling back to manual parsing...');
        zones = parseZonesFromProtobuf(messageBytes);
      }
    } else {
      console.log('Using manual protobuf parsing...');
      zones = parseZonesFromProtobuf(messageBytes);
    }
    
    console.log('Final parsed zones:', zones);
    
    if (zones.length < 2) {
      throw new Error(`Expected at least 2 zones, got ${zones.length}`);
    }
    
    if (zones[0].temperature !== 42 || zones[1].temperature !== 43) {
      throw new Error(`Expected temperatures [42, 43], got [${zones[0].temperature}, ${zones[1].temperature}]`);
    }
    
    console.log('? Test passed! Successfully retrieved collection from server using protobuf parsing');
    console.log(`Zone 1: Zone=${zones[0].zone}, Temperature=${zones[0].temperature}`);
    console.log(`Zone 2: Zone=${zones[1].zone}, Temperature=${zones[1].temperature}`);
    
  } catch (error: any) {
    console.error('? Test failed:', error);
    throw error;
  }
})().catch(e => { 
  console.error('Unhandled error:', e); 
  process.exit(1); 
});

// Fallback manual parsing functions
function readVarint(bytes: Uint8Array, pos: number): { value: number; newPos: number } | null {
  if (pos >= bytes.length) return null;
  
  let value = 0;
  let shift = 0;
  let currentPos = pos;
  
  while (currentPos < bytes.length && shift < 64) {
    const byte = bytes[currentPos++];
    value |= (byte & 0x7F) << shift;
    
    if ((byte & 0x80) === 0) {
      return { value, newPos: currentPos };
    }
    
    shift += 7;
  }
  
  return null;
}

function parseZonesFromProtobuf(bytes: Uint8Array): any[] {
  const zones: any[] = [];
  let pos = 0;
  
  console.log('Manual parsing protobuf message of', bytes.length, 'bytes');
  
  while (pos < bytes.length) {
    const tagResult = readVarint(bytes, pos);
    if (!tagResult) break;
    
    const tag = tagResult.value;
    pos = tagResult.newPos;
    
    const fieldNumber = tag >> 3;
    const wireType = tag & 0x7;
    
    console.log(`Field ${fieldNumber}, wire type ${wireType}, pos ${pos}`);
    
    if (fieldNumber === 1 && wireType === 2) { // repeated zone_list field  
      const lengthResult = readVarint(bytes, pos);
      if (!lengthResult) break;
      
      const length = lengthResult.value;
      pos = lengthResult.newPos;
      
      console.log(`Reading zone of length ${length} at pos ${pos}`);
      
      if (pos + length <= bytes.length) {
        const zoneBytes = bytes.slice(pos, pos + length);
        const zone = parseZone(zoneBytes);
        zones.push(zone);
        pos += length;
        console.log('Parsed zone:', zone);
      } else {
        console.warn('Zone data exceeds buffer');
        break;
      }
    } else {
      console.log(`Skipping unknown field ${fieldNumber}, wire type ${wireType}`);
      // Skip unknown field by reading the next varint (if wire type 0) or length-delimited data (if wire type 2)
      if (wireType === 0) {
        const skipResult = readVarint(bytes, pos);
        if (skipResult) pos = skipResult.newPos;
        else break;
      } else if (wireType === 2) {
        const lenResult = readVarint(bytes, pos);
        if (lenResult) {
          pos = lenResult.newPos + lenResult.value;
        } else break;
      } else {
        break; // Unknown wire type
      }
    }
  }
  
  return zones;
}

function parseZone(bytes: Uint8Array): any {
  const zone: any = { zone: 0, temperature: 0 };
  let pos = 0;
  
  console.log('Parsing zone from', bytes.length, 'bytes:', Array.from(bytes));
  
  while (pos < bytes.length) {
    const tagResult = readVarint(bytes, pos);
    if (!tagResult) break;
    
    const tag = tagResult.value;
    pos = tagResult.newPos;
    
    const fieldNumber = tag >> 3;
    console.log(`Zone field ${fieldNumber} at pos ${pos}`);
    
    if (fieldNumber === 1) { // zone field
      const valueResult = readVarint(bytes, pos);
      if (!valueResult) break;
      zone.zone = valueResult.value;
      pos = valueResult.newPos;
      console.log('Zone value:', zone.zone);
    } else if (fieldNumber === 2) { // temperature field  
      const valueResult = readVarint(bytes, pos);
      if (!valueResult) break;
      zone.temperature = valueResult.value;
      pos = valueResult.newPos;
      console.log('Temperature value:', zone.temperature);
    } else {
      console.log('Unknown zone field:', fieldNumber);
      // Skip unknown field
      const skipResult = readVarint(bytes, pos);
      if (skipResult) pos = skipResult.newPos;
      else break;
    }
  }
  
  return zone;
}
