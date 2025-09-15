# RemoteMvvm Architecture Philosophy

## Guiding Principles
- **Do the heavy lifting at generation time.** We lean on Roslyn to understand the view-model surface area while we are compiling, not while the app is running. By the time a UI assembly ships, every element the runtime needs is already described in generated code.
- **Stay framework independent for as long as possible.** We generate neutral descriptions of both the property hierarchy and the user interface layout before handing them to a framework-specific translator.
- **Minimize runtime reflection.** Runtime work is limited to binding generated structures to controls and reacting to live data. There is no need to rediscover metadata once the app is running.

## Roslyn-Driven View-Model Analysis
The first phase is Roslyn analysis. `PropertyDiscoveryUtility` walks every property symbol surfaced by the view model and records rich metadata in a `PropertyAnalysis` structure. Each `PropertyMetadata` entry contains:

- Safe names for code generation (variables and property access paths).
- Type categorization (simple, boolean, enum, collection, or complex).
- Nullability, array detection, and collection sizing semantics.
- UI hints such as recommended control types or read-only status.

Because this inspection happens inside the source generator, we can reason about full Roslyn symbols (including nested generic types and attributes) without emitting any reflection code. The analysis result becomes the authoritative catalog of the view model for every downstream generator.

## Intermediate Representation of the UI
Once we understand the view model, we produce a pair of framework-neutral descriptions that capture both the layout shell and the property tree.

### Layout DSL
Layout is described with the `UIComponent` tree: a minimal domain-specific language that records the element type, optional name, textual content, attributes, and children. Generators compose this tree to express panes, grids, buttons, and detail panels once. Translators then project the same structure into different targets (WinForms or WPF) without duplicating layout logic.

### Property Tree Commands
The property hierarchy is encoded as an ordered list of `TreeCommand` records. Each command represents an operation such as creating nodes, attaching children, or setting metadata tags. During generation, we emit framework-specific code by replaying these commands through a translator that knows how to produce `TreeViewItem`, `TreeNode`, or other equivalents.

Every node in the tree carries a generated `PropertyNodeInfo` payload so that runtime interactions can map a selected node back to the property it represents. The payload includes the property name, the backing object instance, and flags describing the property category or collection index. Because this metadata is baked into the generated code, runtime selection handlers can focus purely on presenting editors.

## From Intermediate Representation to Concrete UI
Two translator layers consume the intermediate representation:

1. **Layout translators** (`WpfUITranslator`, `WinFormsUITranslator`) walk the `UIComponent` tree and emit XAML or procedural C#. This keeps structural decisions (split panes, toolbars, detail panels) identical across frameworks.
2. **Tree command emitters** convert the command list into framework-specific `LoadTree` implementations. The generator writes the entire method inline, including try/catch blocks, null handling, and collection expansion limits. The runtime simply calls `LoadTree()` and binds event handlers to use the `PropertyNodeInfo` tags.

Because both layers operate on framework-neutral inputs, adding another UI technology only requires implementing new translators. The Roslyn analysis and overall property semantics stay untouched.

## Runtime Responsibilities
At runtime, the generated application performs a narrow set of tasks:

- Instantiate the predeclared controls and wire standard events (selection changes, refresh buttons, expand/collapse commands).
- Invoke the generated `LoadTree` method to populate the tree view using compile-time knowledge.
- Respond to UI events by reading `PropertyNodeInfo` and presenting editors with precomputed hints.

No runtime reflection or ad hoc property inspection is necessary—the generator has already codified the complete shape of the view model.

## Extensibility
This architecture scales naturally:

- **New frameworks** only need new `IUiTranslator` and tree command renderers.
- **New property semantics** can be introduced by enriching `PropertyDiscoveryUtility` and extending `PropertyNodeInfo` and `TreeCommand` flags.
- **Custom UI policies** (for example, different editor experiences) can be built atop the same metadata without reanalyzing the model.

By committing to Roslyn-driven analysis and framework-agnostic representations, we deliver consistent, low-overhead user interfaces across platforms while keeping runtime code lean and predictable.
