# RemoteMvvm

This repository contains the RemoteMvvm tools and sample projects, including a TypeScript demo.

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
