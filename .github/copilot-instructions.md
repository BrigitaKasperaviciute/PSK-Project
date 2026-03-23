# Copilot Workspace Instructions

## Integration Test Experiments

- When the user prompt mentions an iteration such as "experiment 1", "experiment 2", etc., create that iteration in a separate folder under `WorthBoards.IntegrationTests/ExperimentN/`.
- Generate both scenario test files and supporting test infrastructure files (for example factory, fakes, helpers) inside the same `ExperimentN` folder.
- Keep previous experiment folders unchanged unless the user explicitly asks to update them.
- Ensure the experiment folder exists in the open workspace before adding files.

## Language Compatibility

- Prefer stable, broadly supported C# language features that match the project target framework and compiler defaults.
- Avoid preview-only language features unless the user explicitly requests them.
