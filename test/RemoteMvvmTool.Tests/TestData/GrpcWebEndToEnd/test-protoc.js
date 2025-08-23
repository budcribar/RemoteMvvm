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

// Generic function to traverse and extract data from protobuf response
function extractDataFromResponse(response) {
  const responseData = {};
  
  // Get all methods on the response object
  const methods = Object.getOwnPropertyNames(Object.getPrototypeOf(response))
    .filter(name => name.startsWith('get') && typeof response[name] === 'function');
  
  methods.forEach(methodName => {
    try {
      const value = response[methodName]();
      if (value !== undefined && value !== null) {
        // Extract property name from getter method (e.g., 'getCounter' -> 'counter')
        let propName = methodName.substring(3).toLowerCase();
        
        // Special handling for protobuf map getters that end with "Map"
        if (methodName.endsWith('Map') && propName.endsWith('map')) {
          // For methods like getStatusMapMap(), the property should be statusmap, not statusmapmap
          propName = propName.substring(0, propName.length - 3); // Remove the extra "map"
        }
        
        // Handle different types of values
        if (typeof value === 'object' && value !== null) {
          if (Array.isArray(value)) {
            // Handle arrays/lists
            responseData[propName] = extractArrayData(value);
          } else if (value.constructor && (value.constructor.name.includes('Map') || methodName.endsWith('Map'))) {
            // Handle protobuf maps - iterate through entries
            const mapData = {};
            if (typeof value.forEach === 'function') {
              value.forEach((mapValue, mapKey) => {
                mapData[mapKey] = extractValue(mapValue);
              });
            } else if (typeof value.entrySet === 'function') {
              // Alternative map iteration approach
              const entries = value.entrySet();
              for (const entry of entries) {
                mapData[entry.getKey()] = extractValue(entry.getValue());
              }
            }
            responseData[propName] = mapData;
          } else if (typeof value.toArray === 'function') {
            // Handle repeated fields that have toArray method
            const arrayValue = value.toArray();
            responseData[propName] = extractArrayData(arrayValue);
          } else if (value.getEntriesMap && typeof value.getEntriesMap === 'function') {
            // Handle single dictionary objects with entries map
            const entriesMap = value.getEntriesMap();
            const entries = {};
            entriesMap.forEach((mapValue, mapKey) => {
              entries[mapKey] = extractValue(mapValue);
            });
            responseData[propName] = entries;
          } else if (typeof value.getMap === 'function') {
            // Handle other map-like objects
            const map = value.getMap();
            const mapData = {};
            map.forEach((mapValue, mapKey) => {
              mapData[mapKey] = extractValue(mapValue);
            });
            responseData[propName] = mapData;
          } else {
            // Handle complex objects - recursively extract data
            responseData[propName] = extractDataFromResponse(value);
          }
        } else {
          // Handle primitive values
          responseData[propName] = value;
        }
      }
    } catch (error) {
      // Skip methods that cause errors - they might not be actual getters
      console.log(`Skipping method ${methodName}: ${error.message}`);
    }
  });
  
  return responseData;
}

// Helper function to extract data from arrays
function extractArrayData(array) {
  return array.map(item => {
    if (typeof item === 'object' && item !== null) {
      if (item.getEntriesMap) {
        // Handle collection of dictionaries (like MetricsByRegion)
        const entriesMap = item.getEntriesMap();
        const entries = {};
        entriesMap.forEach((value, key) => {
          entries[key] = extractValue(value);
        });
        return entries;
      } else {
        // Handle other complex objects
        return extractDataFromResponse(item);
      }
    } else {
      return extractValue(item);
    }
  });
}

// Helper function to extract a single value
function extractValue(value) {
  if (typeof value === 'object' && value !== null) {
    if (Array.isArray(value)) {
      return extractArrayData(value);
    } else if (value.constructor && value.constructor.name.includes('Map')) {
      const mapData = {};
      value.forEach((mapValue, mapKey) => {
        mapData[mapKey] = extractValue(mapValue);
      });
      return mapData;
    } else {
      return extractDataFromResponse(value);
    }
  }
  return value;
}

client.getState(new Empty(), {}, (err, response) => {
  if (err) {
    console.error('gRPC error:', err);
    process.exit(1);
  }

  // Output the complete response for data validation
  console.log('=== TestViewModel Data Start ===');
  
  try {
    // Use generic extraction instead of hardcoded property checks
    const responseData = extractDataFromResponse(response);
    
    // Output stringified data for parsing by C# test
    const jsonData = JSON.stringify(responseData, null, 2);
    console.log('RESPONSE_DATA:', jsonData);
    
    // Also output a flattened version for easier parsing
    const flatData = JSON.stringify(responseData);
    console.log('FLAT_DATA:', flatData);
    
  } catch (parseError) {
    console.error('Error parsing response:', parseError);
    // Try to output the raw response object
    try {
      const rawResponse = response.toObject ? response.toObject() : response;
      console.log('RAW_RESPONSE:', JSON.stringify(rawResponse));
    } catch (rawError) {
      console.error('Could not serialize raw response either:', rawError);
    }
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
  
  // New: Check for MetricsByRegion specifically
  if (response.getMetricsByRegionList) {
    const metricsList = response.getMetricsByRegionList();
    console.log('MetricsByRegion count:', metricsList.length);
    metricsList.forEach((metricsMap, index) => {
      console.log(`Region ${index}:`, metricsMap.getEntriesMap());
    });
  }
  
  // Check for TotalRegions and IsAnalysisComplete
  if (response.getTotalRegions !== undefined) console.log('TotalRegions:', response.getTotalRegions());
  if (response.getIsAnalysisComplete !== undefined) console.log('IsAnalysisComplete:', response.getIsAnalysisComplete());
  
  console.log('✅ Test passed');
  process.exit(0);
});

// Timeout after 10 seconds
setTimeout(() => {
  console.error('Test timed out after 10 seconds');
  process.exit(1);
}, 10000);

