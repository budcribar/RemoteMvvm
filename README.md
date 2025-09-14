# RemoteMvvm

This repository contains the RemoteMvvm tools and sample projects, including a TypeScript demo.

## Partial Change Hooks

Generated clients expose partial `On<Property>Changed` methods so you can react to property updates with custom UI logic.

## Building the TypeScript Demo

The TypeScript demo is located in `src/demo/TypeScriptMonsterClicker`.

### Prerequisites

- Node.js and npm installed

### Install dependencies

```bash
cd src/demo/TypeScriptMonsterClicker
npm install
```

### Build the project

Compile the TypeScript sources and bundle the application:

```bash
npm run build:production
```

This command also runs `npm run protoc` to generate gRPC-Web stubs.

### Start in development mode

Launch the development server with:

```bash
npm start
```

The application will be available at `http://localhost:3000`.

## Unsupported Types

The `RemoteMvvmTool` currently supports basic scalar types, enums, arrays, lists, and dictionaries whose keys are simple scalars (`int32`, `int64`, `uint32`, `uint64`, `bool`, or `string`).

The following constructs are **not supported** and will cause code generation to fail with a clear error message:

- `HashSet<T>` collections
- `Tuple` types (for example `Tuple<int,string>`)
- Dictionaries with non-scalar keys such as `DateTime` or `object`

Other complex or framework types may also be rejected if they cannot be mapped to a protobuf type.
