# Changelog

All notable changes to the Road Precision mod will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Changed
- GuideLine angle should be easier to read.
- Tooltip systems now use Harmony patches with priority to ensure compatibility with other mods
- Vanilla tooltips no longer appear alongside precision tooltips (now properly hidden)

### Fixed
- Duplicate tooltip path errors when drawing long roads
- Duplicate angle tooltips appearing on top of each other
- Shouldn't crash with **ExtendedTooltip** mod installed.

### Aside
- Looked into UI modding approach for tooltip color customization but no luck.

## [0.1.0] - Initial Release

### Added
- Basic precision tooltip functionality
- String-based tooltips with decimal values (oof)
