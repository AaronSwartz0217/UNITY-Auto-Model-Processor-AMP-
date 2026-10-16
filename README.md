# Auto Model Processor (AMP)

AMP is an editor-only Unity 2022.3 LTS package that turns plant and obstacle FBX files into standardized, editable prefabs without modifying source mesh data.

## Installation

This repository already installs AMP as an embedded package at `Packages/com.codex.auto-model-processor`. For another project, import the delivered `.unitypackage`, or copy this package folder into that project's `Packages` directory.

## Quick start

1. Put plant FBX files in `Assets/AMP_RawInput/Plants` and obstacles in `Assets/AMP_RawInput/Obstacles`.
2. Open **Tools > AutoModelProcessor > Batch Processing Window**.
3. Select the model type, review the scanned files and processing options, then click **开始批量处理**.
4. Find generated prefabs under `Assets/AMP_Output/Plants_Prefabs` or `Assets/AMP_Output/Obstacles_Prefabs`.

Choose **RootObjects** as the prefab generation mode to turn every parallel root-level FBX object into an independent prefab. AMP can preserve object names or generate stable names such as `Source_01`, place results in a per-FBX folder, and correct every object's pivot independently.

AMP automatically creates the `Grass` and `Obstacle` layers and the `NavMesh` tag when missing. Plants receive non-static convex mesh colliders (with configurable primitive fallback for complex meshes); obstacles receive static concave mesh colliders. Materials are copied into editable project assets and standardized to URP/Lit when available.

## Configuration and extension

Use the window's JSON import/export buttons to share rules. Runtime window settings are stored per editor user. Integrations can implement `IAMPProcessingExtension` and register an instance in `AMPProcessor.Extensions`.

## Notes

- Animation data in an FBX is left untouched and is not added to the generated root.
- The original FBX and mesh assets are never modified.
- A material template overrides source material properties; without a template, source properties are copied and the shader is switched to URP/Lit.
