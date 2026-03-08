SkillCreator
Creates or refines hierarchical markdown skills from plain English prompts.

## Metadata
scope: Global
domain: SkillEngineering
language: en
category: Code
source: UserDefined
override: Merge
keywords: skill, creator, hierarchy, metadata, markdown
capabilities: prompt-to-skill generation, hierarchy-aware scaffolding, metadata normalization
restrictions: does not execute arbitrary code, does not modify protected files without confirmation

## Inputs
- prompt|string|true|Natural language request for the desired skill.
- target_scope|string|true|One of Global, Project, SubProject, Task.
- project|string|false|Project identifier when scope is Project/SubProject/Task.
- subproject|string|false|SubProject identifier when scope is SubProject/Task.
- task|string|false|Task identifier when scope is Task.

## Output
type: object
description: Structured markdown skill draft and placement recommendation.

## Instructions
Generate a markdown skill where line 1 is the skill name and line 2 is the short description.
Use explicit metadata fields and avoid ambiguity.
Always state domain, scope, language, inputs, outputs, capabilities, and restrictions.
When scope is not Global, include project hierarchy identifiers and explain inheritance intent.
