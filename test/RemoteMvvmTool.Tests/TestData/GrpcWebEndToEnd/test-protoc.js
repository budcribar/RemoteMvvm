// gRPC-Web client test using protoc generated stubs
// This script is executed from the C# test to verify that the generated
// protobuf and service files can communicate with the running server.

global.XMLHttpRequest = require('xhr2');

function loadGenerated(modulePathLower, modulePathUpper) {
  try {
    return require(modulePathLower);
  } catch {
    return require(modulePathUpper);
  }
}

const svc = loadGenerated('./testviewmodelservice_grpc_web_pb.js', './TestViewModelService_grpc_web_pb.js');
loadGenerated('./testviewmodelservice_pb.js', './TestViewModelService_pb.js');
const { TestViewModelServiceClient } = svc;
const { Empty } = require('google-protobuf/google/protobuf/empty_pb.js');
const process = require('process');

const port = process.argv[2] || '5000';

const client = new TestViewModelServiceClient(`http://localhost:${port}`, null, null);

console.log('Starting gRPC-Web test using generated client...');

client.getState(new Empty(), {}, (err, response) => {
  if (err) {
    console.error('gRPC error:', err);
    process.exit(1);
  }

  // Output the complete response for data validation
  console.log('=== TestViewModel Data Start ===');
  
  try {
    // Get all the data from the response object
    const responseData = {};
    
    // Check for zone list
    if (response.getZoneListList) {
      const zones = response.getZoneListList();
      responseData.zoneList = [];
      zones.forEach(zone => {
        const zoneData = {};
        if (zone.getZone) zoneData.zone = zone.getZone();
        if (zone.getTemperature) zoneData.temperature = zone.getTemperature();
        responseData.zoneList.push(zoneData);
      });
    }
    
    // Check for simple properties
    if (response.getStatus) responseData.status = response.getStatus();
    if (response.getMessage) responseData.message = response.getMessage();
    if (response.getCounter !== undefined) responseData.counter = response.getCounter();
    if (response.getIsEnabled !== undefined) responseData.isEnabled = response.getIsEnabled();
    
    // Check for score list
    if (response.getScoreListList) {
      responseData.scoreList = response.getScoreListList();
    }
    
    // Check for other numeric properties
    if (response.getPlayerLevel !== undefined) responseData.playerLevel = response.getPlayerLevel();
    if (response.getHasBonus !== undefined) responseData.hasBonus = response.getHasBonus();
    if (response.getBonusMultiplier !== undefined) responseData.bonusMultiplier = response.getBonusMultiplier();
    if (response.getStatus !== undefined) responseData.gameStatus = response.getStatus();
    if (response.getCurrentStatus !== undefined) responseData.currentStatus = response.getCurrentStatus();
    
    // Check for dictionary/map
    if (response.getStatusMapMap) {
      const statusMap = response.getStatusMapMap();
      responseData.statusMap = {};
      statusMap.forEach((value, key) => {
        responseData.statusMap[key] = value;
      });
    }
    
    // Output stringified data for parsing by C# test
    const jsonData = JSON.stringify(responseData, null, 2);
    console.log('RESPONSE_DATA:', jsonData);
    
    // Also output a flattened version for easier parsing
    const flatData = JSON.stringify(responseData);
    console.log('FLAT_DATA:', flatData);
    
  } catch (parseError) {
    console.error('Error parsing response:', parseError);
    // Try to output the raw response object
    console.log('RAW_RESPONSE:', JSON.stringify(response.toObject ? response.toObject() : response));
  }
  
  console.log('=== TestViewModel Data End ===');
  
  // Legacy validation for backward compatibility - basic sanity check
  const zones = response.getZoneListList ? response.getZoneListList() : [];
  if (zones.length >= 2) {
    console.log('Received zones:', zones.length);
    const temperatures = zones.map(z => z.getTemperature ? z.getTemperature() : 0);
    console.log('Temperatures:', temperatures.join(' '));
  }
  
  // Simple properties check
  if (response.getMessage) console.log('Message:', response.getMessage());
  if (response.getCounter !== undefined) console.log('Counter:', response.getCounter());
  if (response.getIsEnabled !== undefined) console.log('IsEnabled:', response.getIsEnabled());
  
  console.log('✅ Test passed');
  process.exit(0);
});

// Timeout after 10 seconds
setTimeout(() => {
  console.error('Test timed out after 10 seconds');
  process.exit(1);
}, 10000);

