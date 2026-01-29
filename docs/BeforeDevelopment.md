This repository is intended for a global game jam game.
The development should be done in the following way:
- "Quick and dirty" - spend less on architecture and more on working features
- Use VContainer for dependency injection. Don't have to create an interface for everything. DI is for convinience and not testability
- Always follow the coding conventions defined in docs/CodingConventions.md
- Split work into logical commits with clear messages so it'll be easier to roll back
- Use async code with UniTasks
- Use the new input system only - no legacy input manager
- Use ScriptableObjects for configuration data
- Use Addressables for asset management (specially for dynamically loading assets)
- Use URP
- The game is not targeting mobile platforms