# TypeScript Monster Clicker

This sample demonstrates a TypeScript client using gRPC‑Web to communicate with the MonsterClicker server.

The `generated` folder contains the service stubs and message types produced from `GameViewModelService.proto`. If `GameViewModelService_pb.js` is missing you can regenerate the files with `protoc`:

```bash
protoc \
  --proto_path=../MonsterClicker/protos \
  --proto_path=/usr/include \
  --js_out=import_style=commonjs,binary:generated \
  --grpc-web_out=import_style=commonjs,mode=grpcwebtext:generated \
  ../MonsterClicker/protos/GameViewModelService.proto
```

The command requires both `protoc` and the `protoc-gen-grpc-web` plugin to be installed and on your `PATH`.
