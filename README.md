# touch

A structured template package for vNext workflow components with domain-based architecture.

## 📦 Installation

Create a new project with your domain name:

```bash
npx @burgan-tech/vnext-template <domain-name>
```

### Example

```bash
npx @burgan-tech/vnext-template user-management
```

This will create a new directory with your domain name, copy all template files, replace `touch` instances, and install dependencies automatically.

### Install specific version

```bash
npx @burgan-tech/vnext-template@<version> <domain-name>
```

Example:

```bash
npx @burgan-tech/vnext-template@1.0.0 user-management
```

### Alternative: Install as dependency

```bash
npm install @burgan-tech/vnext-template
npm run setup <domain-name>
```

Or install specific version:

```bash
npm install @burgan-tech/vnext-template@<version>
npm run setup <domain-name>
```

Or use environment variable:

```bash
DOMAIN_NAME=user-management npm install @burgan-tech/vnext-template@<version>
```

## 🚀 Usage

After installation, your project structure will be:

```
<domain-name>/
├── Extensions/
├── Functions/
├── Schemas/
├── Tasks/
├── Views/
└── Workflows/
```

### Access components in code

```javascript
const vnextTemplate = require('@burgan-tech/vnext-template');

// Get domain configuration
const config = vnextTemplate.getDomainConfig();

// Get all schemas
const schemas = vnextTemplate.getSchemas();

// Get all workflows
const workflows = vnextTemplate.getWorkflows();

// Get all tasks
const tasks = vnextTemplate.getTasks();
```

### Available methods

- `getDomainConfig()` - Get domain configuration
- `getSchemas()` - Get all schemas
- `getWorkflows()` - Get all workflows
- `getTasks()` - Get all tasks
- `getViews()` - Get all views
- `getFunctions()` - Get all functions
- `getExtensions()` - Get all extensions
- `getDomainName()` - Get domain directory name

## ✅ Validation

Validate your project structure and schemas:

```bash
npm run validate
```

This will check:
- Domain directory structure
- JSON file syntax
- Schema validation
- Configuration files

## 📄 License

MIT
