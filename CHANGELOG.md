# WebConnect Changelog

All notable changes to WebConnect will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.2] - 2025-06-27

### Added
- **Enhanced Submit Button Detection**: Added support for ARIA role-based button elements (`role="button"`)
- **Improved Dojo Widget Compatibility**: Now properly detects and interacts with Dojo Toolkit submit buttons
- **Enhanced Scoring Algorithm**: Significantly improved scoring for role-based buttons with context-aware bonuses
- **Better Fallback Error Handling**: Enhanced JavaScript click fallback with improved logging and error recovery

### Fixed
- **ElementNotInteractableException Timeouts**: Resolved 10-second timeout issues when clicking submit buttons on websites using non-standard button implementations (e.g., Dojo widgets)
- **Submit Button Detection Gaps**: Fixed detection logic that previously missed `<span role="button">`, `<div role="button">`, and other ARIA-compliant button elements

### Technical Improvements
- **Expanded CSS Selectors**: Updated all submit button detection logic to include `*[role='button']` elements
- **FastPath Scoring Enhancement**: Added role-based scoring with +3000 base points for `role="button"` elements, plus contextual bonuses
- **Standard Scoring Enhancement**: Increased role-based button scoring from +10 to +500 points with additional context bonuses
- **Test Coverage**: Updated all test mocks and validation to support the new enhanced detection logic

### Compatibility
- **Backward Compatible**: All existing functionality and websites continue to work as before
- **Framework Support**: Improved compatibility with modern JavaScript frameworks (Dojo, Angular, React components using role attributes)
- **ARIA Compliance**: Better support for accessibility-compliant web applications

### For Developers
- **Enhanced Logging**: More detailed logging for role-based button detection and scoring decisions
- **Fallback Robustness**: Improved error handling for both standard click and JavaScript fallback methods
- **Performance**: No performance impact on existing detection logic

## [1.0.1] - Previous Release
- Initial stable release with core login detection functionality

## [1.0.0] - Initial Release
- Basic web authentication automation
- Field detection and login verification
- CyberArk PSM integration 