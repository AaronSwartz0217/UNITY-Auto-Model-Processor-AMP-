# Changelog

## 2.6.2 - 2026-08-10

- Newly scanned FBX files are now unchecked by default instead of selected automatically.
- The exact selected path list is frozen before setup and cannot expand during processing.
- The execution button and log now show the precise number and paths authorized for processing.
- Processing is disabled when no FBX is selected.

## 2.6.1 - 2026-08-10

- Fixed Start Batch Processing rescanning the list and silently selecting every FBX again.
- Manual rescans now preserve the checked state of files already in the list.
- The file-list heading now shows both total and selected counts.

## 2.6.0 - 2026-08-10

- Prompt conflict handling now asks only once per batch.
- Added `Overwrite All`, `Skip All`, and `Cancel Batch` choices for the current selection.
- The selected decision applies to all remaining conflicting prefabs, including split objects.

## 2.5.0 - 2026-08-10

- Fixed RootObjects mode for FBX files whose only Mesh is attached directly to the imported root.
- Output subfolders are now created only after at least one valid split candidate is found.
- Added an explicit error message when the selected input folder does not exist.

## 2.4.1 - 2026-08-06

- Restored the visual specification exactly: both plant and obstacle profiles default to MeshCollider with Convex enabled and Is Trigger disabled.
- Automatic primitive fallback remains disabled by default so the requested MeshCollider component is preserved.

## 2.4.0 - 2026-08-06

- Superseded by 2.4.1 before delivery due to a misinterpretation of the requested defaults.

## 2.3.0 - 2026-08-06

- Split collider mode and Convex settings into independent plant and obstacle profiles.
- Plant profile remains Untagged/Grass/non-static; obstacle profile remains NavMesh/Obstacle/static.
- Both profiles default to Convex MeshCollider per the updated visual specification, while remaining user-configurable.

## 2.2.0 - 2026-08-06

- Added selectable collider modes: `MeshCollider` and `None`.
- `None` removes imported colliders and generates prefabs without any Collider component.

## 2.1.0 - 2026-08-06

- Reuses one editable material asset for every renderer that references the same source material.
- Added imported/shared/per-prefab material modes; material templates create explicit per-prefab instances.
- Fixed collider generation for mesh and skinned-mesh objects, including validation and hierarchy logging.

## 2.0.0 - 2026-08-06

- Added splitting of parallel root-level FBX objects into independent prefabs.
- Added per-FBX output folders, object/index naming and per-object pivot correction.
- Added automatic unwrapping of a single empty exporter wrapper node.

## 1.0.0 - 2026-08-06

- Initial release implementing AMP PRD v2.1.
