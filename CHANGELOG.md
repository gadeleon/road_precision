# Changelog

All notable changes to the Road Precision mod will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Changed
- Both vanilla and precision tooltips now display simultaneously
- NetCourse precision tooltips now offset to the right of vanilla tooltips
- GuideLine precision tooltips now offset below vanilla tooltips
- Precision tooltip systems now use unique tooltip paths to prevent conflicts

### Fixed
- Compatible with versions 1.5.*
- Removed duplicate angle tooltips at control points during complex curves (now shown in [P] tooltips only)
- Removed duplicate angle tooltips when connecting to road edges (now shown in [P] tooltips only)
- Duplicate tooltip path errors when drawing long roads
- Duplicate angle tooltips appearing on top of each other at control points
- Precise angles now work correctly when connecting to intersections/corners (Nodes)
- [Needs Testing] Should be compatibile with **ExtendedTooltip** and other tooltip mods

### Technical
- Looked into UI modding approach for tooltip color customization but no luck

## [0.0.1] - Initial Release

### Added
- Basic precision tooltip functionality
- String-based tooltips with decimal values (oof)
