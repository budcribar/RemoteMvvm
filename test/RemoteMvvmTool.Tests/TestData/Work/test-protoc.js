const process = require('process');

(async () => {
  console.log('Starting gRPC-Web test with protoc-generated grpc-web client...');
  const svc = require('./src/generated/TestViewModelService_grpc_web_pb.js');
  const pb = require('./src/generated/TestViewModelService_pb.js');
  const empty = require('google-protobuf/google/protobuf/empty_pb.js');

  const port = process.argv[2] || '5000';
  const host = `http://localhost:${port}`;

  const client = new svc.TestViewModelServiceClient(host, null, null);

  console.log('Calling GetState via grpc-web client...');
  await new Promise((resolve, reject) => {
    client.getState(new empty.Empty(), { 'content-type': 'application/grpc-web+proto' }, (err, resp) => {
      if (err) return reject(err);
      try {
        console.log('Got response from grpc-web client');
        const zoneList = resp.getZoneListList ? resp.getZoneListList() : [];
        const zones = zoneList.map((z, i) => ({ zone: z.getZone ? z.getZone() : i, temperature: z.getTemperature ? z.getTemperature() : 0 }));
        console.log('Zones:', zones);
        if (zones.length < 2) return reject(new Error('Expected at least 2 zones'));
        if (zones[0].temperature !== 42 || zones[1].temperature !== 43)
          return reject(new Error(`Unexpected temperatures: ${zones[0].temperature}, ${zones[1].temperature}`));
        resolve();
      } catch (e) { reject(e); }
    });
  });
  console.log('? grpc-web client test passed');
})().catch(e => { console.error('Unhandled error:', e); process.exit(1); });
