# Changelog
All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]
---
## [1.1.8339] - 2022-10-31
### Added
- in **_AssemblerLib.Utilities_**: a PurgeNullHandlesFromList method to prevent some errors in components when Handles have null Polylines
- in **_Deconstruct AssemblyObject_** Component: added output for Receiver value

### Changed
- **_Construct-Deconstruct AssemblyObject_** and **_Construct-Deconstruct XData_** Components inputs and outputs sorted for better coherence (previous Components marked as OLD);
- construction of AssemblyObjects and Handles is eased by setting several parameters with default values;
    - in **_Construct Handle_** component: Rotations, Type and Weight all have default values
    - in **_Construct AssemblyObject_** Component:
        - reference plane is set by default to an XY plane centered in the collision Mesh Volume centroid
        - direction vector is set by default to World X axis
- improved **_AssemblyObject_** preview:
    - direction vector is visible in dark violet;
    - an L-shaped polyline is drawn for each Handle, color follows Handle type using the same palette for AssemblyObject types
- several code refactoring to improve readability, modularity and efficiency

### Deprecated
- older version of changed components - they still work for old definitions but they are labelled with an "OLD" watermark

### Removed

### Fixed
- Weird behavior of an Assemblage growing outside a Field: Assemblages now no longer grow outside the extents of a Field
- uneven row-column distribution in **_Heuristics Display_** Component
---
## [1.1.8190] - 2022-06-04
### Added
- First major public release of Assembler
### Changed
- Too many changes from the previous, limited release - upgrade your example files as well!
### Deprecated

### Removed
- the Simplified Engine
- several specific Field construction components
- the AssemblyObject Transform component
### Fixed
- a looot of bugs, thanks to the contribution of all students and people who experimented with earlier versions