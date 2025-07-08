# AGENTS Instructions

This repository contains both .NET and TypeScript sources. If you modify any code files under `src/` or `test/`, run the following checks before creating a pull request:

1. Execute the test suite:
   ```bash
   dotnet test
   ```
2. If you changed anything inside `src/demo/TypeScriptMonsterClicker`, build the TypeScript project:
   ```bash
   cd src/demo/TypeScriptMonsterClicker
   npm run build:production
   ```

Add the output of these commands to the **Testing** section of your pull request description. If the commands fail because of missing dependencies or network restrictions, mention that in the Testing section.
