# RSF Framework — Codex Development Instructions

## 1. Project Overview

RSF is an experimental C# game framework focused on entity-based simulation, reusable systems, components, and gameplay mechanics.

The project is being developed incrementally, with an emphasis on understanding C# and maintaining an extensible architecture.

The existing repository is the source of truth. Inspect actual implementations rather than assuming that planned systems already exist.

## 2. Core Development Rules

1. Preserve the existing architecture, naming conventions, and coding style.
2. Do not rewrite, rename, relocate, or restructure existing code without explicit permission.
3. Prefer simple, readable, beginner-friendly C#.
4. Avoid unnecessary abstractions, design patterns, dependencies, and generic frameworks.
5. Implement one feature or bug fix at a time.
6. Reuse existing systems and utilities whenever practical.
7. Explain proposed changes before making significant modifications.
8. Never silently introduce unrelated changes.
9. Do not delete existing functionality or tests without permission.
10. If requirements are unclear, ask before making consequential design decisions.

## 3. Architecture Guidelines

Preserve the existing responsibilities and relationships of:

- Entity and EntityId
- Components and IComponent
- Systems and ISystem
- Services and IService
- World and ComponentStore
- Simulation
- Blueprints
- Commands, CommandHandlers, and CommandExecutors
- Debugging utilities

Do not introduce a replacement architecture or external ECS framework without approval.

Follow the existing implementation when these guidelines differ from actual code. Report any conflicts rather than silently resolving them.

### Entities

- Preserve the existing EntityId value-object approach.
- Use existing entity creation and registration mechanisms.
- Do not introduce competing entity identification systems.

### Components

- Components should primarily represent data.
- Keep gameplay orchestration and complex behavior in appropriate systems.
- Reuse existing component storage and lookup mechanisms.

### Systems

- Keep system responsibilities focused.
- Follow the existing update lifecycle and interfaces.
- Avoid unnecessary dependencies between systems.
- Do not introduce a new scheduling or event architecture without approval.

### Simulation and World

- Preserve the existing Simulation and World responsibilities.
- Use existing registration and update mechanisms.
- Avoid bypassing the established simulation workflow.

### Commands

- Follow existing command and handler conventions.
- Reuse current validation and execution paths.
- Avoid creating a second command framework.

## 4. Program.cs Rules

Program.cs is the primary place for demonstrating and manually testing gameplay behavior unless the user requests another approach.

Preserve the existing style:

- Explicitly initialize Simulation and systems.
- Use `simulation.World` for world access.
- Use `simulation.Update(...)` to advance simulation behavior.
- Spawn entities through existing Blueprints where applicable.
- Use WorldDebugger and existing debugging utilities to inspect results.

Do not replace this workflow with an unfamiliar test harness, dependency injection container, or new application entry point without permission.

## 5. Gameplay Development Principles

When implementing gameplay features:

1. Identify relevant existing components and systems.
2. Explain the intended data flow.
3. Reuse existing mechanisms.
4. Implement the smallest complete change.
5. Preserve compatibility with existing gameplay features.
6. Demonstrate the behavior through the established simulation workflow.
7. Explain how the new feature interacts with existing systems.

Respect existing game-time conventions, including GameTime and GameDuration where applicable.

## 6. Required Workflow

### Before Coding

1. Inspect relevant files and their dependencies.
2. Summarize the current behavior.
3. Describe the proposed implementation.
4. Identify files that will change.
5. Explain any architectural trade-offs.
6. For significant or ambiguous changes, wait for approval.

### During Coding

1. Make focused, minimal changes.
2. Preserve unrelated functionality.
3. Follow existing conventions.
4. Avoid unrelated cleanup and refactoring.
5. Add comments where they help explain non-obvious logic.

### After Coding

1. Run `dotnet build` against the relevant solution or project when the environment permits.
2. Run relevant existing tests, if available.
3. Do not claim success when compilation or tests were not executed.
4. Summarize files changed.
5. Explain the important C# concepts involved.
6. Report remaining issues and potential risks.

## 7. Learning and Explanation Style

The developer is learning C#.

When explaining code:

- Use clear, straightforward English.
- Explain unfamiliar syntax and language features.
- Provide small, concrete examples when useful.
- Explain why a design choice fits RSF.
- Distinguish between required changes and optional improvements.
- Avoid introducing advanced patterns solely for theoretical elegance.

Do not sacrifice correctness, but prioritize understandable implementations.

## 8. Code Documentation and Comments

All new or modified C# code must include clear, beginner-friendly comments explaining its purpose and behavior.

### 1. Class and Interface Comments
- Every new class and interface must have an XML documentation comment (`/// <summary>`) explaining its purpose.
- Explain how the class relates to the RSF architecture.
- Document important properties and fields where their purpose is not obvious.

### 2. Method and Function Comments
- Every new method, constructor, and function must have an XML documentation comment (`/// <summary>`).
- Explain what the method does and why it is needed.
- Use `/// <param>` to describe parameters.
- Use `/// <returns>` for methods that return values.
- When modifying an existing method, add or update its documentation to reflect its behavior.

### 3. Logic Comments
- Add regular `//` comments to explain important calculations, validations, conditions, and gameplay logic.
- Explain why a particular operation is necessary, not merely what the code already says.
- Avoid commenting on obvious operations such as simple assignments.

### 4. Comment Language and Style
- Write all comments in clear, simple English.
- Use beginner-friendly explanations without unnecessary technical jargon.
- Keep comments accurate and update them whenever the corresponding code changes.
- Do not remove existing useful comments without a reason.

### 5. Documentation Requirements
- Do not consider newly implemented code complete until its documentation is added.
- Comments must explain actual behavior, not assumptions or planned functionality.
- Do not modify unrelated code solely to add comments unless requested.

## 9. Safety and Version Control

- Treat existing code and user data as valuable.
- Never run destructive Git commands without explicit approval.
- Never discard uncommitted changes.
- Do not automatically commit, push, or create branches unless requested.
- Never modify credentials, secrets, or unrelated configuration files without approval.
- Show or summarize the diff after implementing changes.

## 10. Feature Completion Criteria

A feature is considered complete only when:

- It follows the existing RSF architecture.
- Its behavior has been demonstrated or tested.
- Relevant build and test results are reported.
- Existing functionality has not knowingly been broken.
- The developer understands the major implementation changes.

If any condition is unverified, report it explicitly.