// Basic gRPC-Web connectivity test
// This script validates that the gRPC-Web client can connect to the server
// without any complex streaming or property update operations

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
const { Empty } = require('google-protobuf/google/protobuf/empty_pb.js');
const process = require('process');

const port = process.argv[2] || '5000';

console.log('?? Basic gRPC-Web Connectivity Test');
console.log(`Connecting to: http://localhost:${port}`);

// Initialize client with fallback approaches
let client;
try {
    client = new TestViewModelServiceClient(`http://localhost:${port}`);
    console.log('? Client initialized (standard approach)');
} catch (initError) {
    console.log('Standard initialization failed, trying with null parameters...');
    try {
        client = new TestViewModelServiceClient(`http://localhost:${port}`, null, null);
        console.log('? Client initialized (alternative approach)');
    } catch (altError) {
        console.error('? Both client initialization approaches failed');
        console.error('Standard error:', initError.message);
        console.error('Alternative error:', altError.message);
        process.exit(1);
    }
}

// Test basic GetState call
console.log('?? Testing GetState call...');

// Use callback style to avoid Promise-related issues
client.getState(new Empty(), {}, (err, response) => {
    if (err) {
        console.error('? GetState failed:', err.message);
        console.error('Error code:', err.code);
        console.error('Error details:', err.details);
        process.exit(1);
    }
    
    console.log('? GetState succeeded!');
    console.log('Response type:', typeof response);
    console.log('Response methods:', Object.getOwnPropertyNames(Object.getPrototypeOf(response)));
    
    // Try to extract some basic information
    let hasData = false;
    
    // Look for getter methods
    const methods = Object.getOwnPropertyNames(Object.getPrototypeOf(response));
    const getters = methods.filter(m => m.startsWith('get') && typeof response[m] === 'function');
    
    console.log('Available getters:', getters);
    
    // Try a few common getters to see if we have data
    getters.forEach(getter => {
        try {
            const value = response[getter]();
            if (value !== undefined && value !== null && value !== '') {
                console.log(`${getter}():`, value);
                hasData = true;
            }
        } catch (getterError) {
            // Ignore getter errors
        }
    });
    
    if (hasData) {
        console.log('? Server returned data successfully');
    } else {
        console.log('?? Server responded but no data was extracted');
    }
    
    console.log('?? Basic connectivity test passed!');
    process.exit(0);
});

// Timeout after 15 seconds
setTimeout(() => {
    console.error('? Test timed out after 15 seconds');
    console.log('This suggests either:');
    console.log('1. Server is not running');
    console.log('2. Network connectivity issues');
    console.log('3. gRPC-Web configuration problems');
    process.exit(1);
}, 15000);

// Global error handlers
process.on('unhandledRejection', (reason, promise) => {
    console.error('? Unhandled Promise Rejection:', reason);
    process.exit(1);
});

process.on('uncaughtException', (error) => {
    console.error('? Uncaught Exception:', error.message);
    process.exit(1);
});