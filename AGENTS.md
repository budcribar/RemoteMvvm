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

Files under `test/**/actual/` and `test/**/actual2/` are generated during test runs. Revert any changes to these directories before committing.

Here is a hint of how to set up the TypeScript project:

var tsconfig = @"{
  ""compilerOptions"": {
    ""target"": ""es2018"",
    ""module"": ""commonjs"",
    ""strict"": false,
    ""esModuleInterop"": true,
    ""lib"": [""es2018"", ""dom""],
    ""outDir"": ""dist"",
    ""allowJs"": true
  },
  ""include"": [""**/*.ts"", ""**/*.js""]
}";
