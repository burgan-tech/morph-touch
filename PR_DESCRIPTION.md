# Versioning Improvements

## Overview

This PR introduces comprehensive improvements to the versioning system, schema validation, and project initialization workflow. It enhances the template's ability to handle versioning for schemas and workflows, while improving the developer experience with better tooling and validation.

## 🎯 Key Changes

### ✨ New Features

- **Schema Version Synchronization**: Added `sync-schema-version.js` script to automatically sync schema versions across the project
- **Enhanced Project Initialization**: 
  - New `init.js` script for creating projects via `npx @burgan-tech/vnext-template <domain-name>`
  - New `setup.js` script for interactive domain name replacement
  - Support for domain name via command-line argument or `DOMAIN_NAME` environment variable
  - Support for installing specific versions: `npx @burgan-tech/vnext-template@<version> <domain-name>`
- **Comprehensive Validation**: Enhanced `validate.js` with improved schema validation and project structure checks
- **VS Code Integration**: Added `.vscode/settings.json` for better IDE support

### 🔧 Improvements

- **Package Configuration**: 
  - Updated `package.json` with new binary commands (`@burgan-tech/vnext-template`, `vnext-template`, `vnext-setup`)
  - Added `postinstall` script for automatic setup
  - Improved `files` array to exclude development-only files
- **Configuration Updates**: Enhanced `vnext.config.json` with versioning support
- **Documentation**: Updated README and CHANGELOG with new features and usage instructions

### 🗑️ Removed

- Removed deprecated `sys-flows.1.0.0.json` workflow
- Removed `available-transitions.1.0.0.json` task

## 📋 Detailed Changes

### New Scripts

1. **`init.js`** (201 lines)
   - Handles project creation via npx
   - Supports domain name as command-line argument
   - Automatically runs setup and installs dependencies

2. **`setup.js`** (217 lines)
   - Interactive domain name replacement
   - Replaces `touch` placeholder in all template files
   - Supports environment variable `DOMAIN_NAME`

3. **`sync-schema-version.js`** (58 lines)
   - Synchronizes schema versions across the project
   - Ensures version consistency

4. **`validate.js`** (621 lines)
   - Comprehensive validation for:
     - Domain directory structure
     - JSON file syntax
     - Schema validation
     - Configuration files
     - Version consistency

### Configuration Updates

- **`package.json`**: Added bin entries, new scripts, and improved file exclusions
- **`vnext.config.json`**: Enhanced with versioning configuration
- **`.vscode/settings.json`**: Added IDE-specific settings for better development experience

## 🚀 Usage

### Create a new project:
```bash
npx @burgan-tech/vnext-template <domain-name>
```

### Install specific version:
```bash
npx @burgan-tech/vnext-template@<version> <domain-name>
```

### Validate project:
```bash
npm run validate
```

### Sync schema versions:
```bash
npm run sync-schema
```

## 📊 Statistics

- **Files Changed**: 12 files
- **Additions**: +1,279 lines
- **Deletions**: -604 lines
- **Net Change**: +675 lines

## ✅ Testing

- All existing tests pass
- New validation checks implemented
- Schema version synchronization verified

## 📝 Related Issues

This PR addresses versioning improvements and enhances the developer experience for working with vNext templates.

## 🔍 Review Checklist

- [ ] Code follows project conventions
- [ ] Documentation updated
- [ ] Tests pass
- [ ] Validation scripts work correctly
- [ ] Schema version synchronization works as expected
- [ ] Project initialization flow works end-to-end

