// gRPC-Web client test using protoc generated stubs
// This script is executed from the C# test to verify that the generated
// protobuf and service files can communicate with the running server.
global.XMLHttpRequest = require('xhr2');
function loadGenerated(modulePathLower, modulePathUpper) {
    try {
        return require(modulePathLower);
    }
    catch {
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
    const zones = response.getZoneListList();
    console.log('Received zones:', zones.length);
    if (zones.length < 2) {
        console.error('Expected at least 2 zones, got', zones.length);
        process.exit(1);
    }
    const t0 = zones[0].getTemperature();
    const t1 = zones[1].getTemperature();
    console.log('Temperatures:', t0, t1);
    if (t0 !== 42 || t1 !== 43) {
        console.error(`Unexpected temperatures [${t0}, ${t1}]`);
        process.exit(1);
    }
    console.log('✅ Test passed! Successfully retrieved collection from server using grpc-web client');
    process.exit(0);
});
