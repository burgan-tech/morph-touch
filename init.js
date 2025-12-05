#!/usr/bin/env node

const fs = require('fs');
const path = require('path');
const { execSync } = require('child_process');

// Get the package directory (where this script is located)
function getPackageDir() {
  // When run via npx, __dirname points to the package directory
  // This could be in node_modules or a temporary directory
  const scriptPath = __filename;
  const scriptDir = path.dirname(path.resolve(scriptPath));
  
  // Check if template files exist in the script directory
  const templateDir = path.join(scriptDir, 'touch');
  if (fs.existsSync(templateDir)) {
    return scriptDir;
  }
  
  // If not found, try to find it relative to node_modules
  // This handles cases where npx creates a temporary directory
  const cwd = process.cwd();
  const nodeModulesPath = path.join(cwd, 'node_modules', '@burgan-tech', 'vnext-template');
  if (fs.existsSync(path.join(nodeModulesPath, 'touch'))) {
    return nodeModulesPath;
  }
  
  // Last resort: use script directory
  return scriptDir;
}

// Validate domain name
function validateDomainName(domainName) {
  if (!domainName) {
    console.error('❌ Domain name cannot be empty');
    return false;
  }
  // Validate domain name format (alphanumeric, hyphens, underscores)
  if (!/^[a-zA-Z0-9_-]+$/.test(domainName)) {
    console.error('❌ Domain name can only contain letters, numbers, hyphens, and underscores');
    return false;
  }
  return true;
}

// Replace touch in file content (domain name placeholder)
function replaceInContent(content, domainName) {
  // Replace "touch" in quotes, touch/ paths, and standalone touch as domain placeholder
  return content.replace(/"touch"/g, `"${domainName}"`)
                .replace(/'touch'/g, `'${domainName}'`)
                .replace(/touch\//g, `${domainName}/`)
                .replace(/\btouch\b/g, domainName);
}

// Copy directory recursively and replace touch
function copyAndReplace(src, dest, domainName, skipDirs = []) {
  if (!fs.existsSync(src)) {
    return;
  }

  const stat = fs.statSync(src);
  
  if (stat.isDirectory()) {
    // Skip certain directories
    const dirName = path.basename(src);
    if (skipDirs.includes(dirName) || dirName.startsWith('.')) {
      return;
    }
    
    // Create destination directory
    if (!fs.existsSync(dest)) {
      fs.mkdirSync(dest, { recursive: true });
    }
    
    // Copy contents
    const entries = fs.readdirSync(src);
    for (const entry of entries) {
      const srcPath = path.join(src, entry);
      const destPath = path.join(dest, entry);
      copyAndReplace(srcPath, destPath, domainName, skipDirs);
    }
  } else if (stat.isFile()) {
    // Read and replace content
    const ext = path.extname(src).toLowerCase();
    const fileName = path.basename(src);
    
    // Process text files
    if (['.json', '.js', '.md', '.sh', '.txt', '.yml', '.yaml'].includes(ext) ||
        fileName === '.gitignore' ||
        fileName === '.gitattributes' ||
        fileName === '.cursorrules') {
      const content = fs.readFileSync(src, 'utf8');
      const replaced = replaceInContent(content, domainName);
      fs.writeFileSync(dest, replaced, 'utf8');
    } else {
      // Copy binary files as-is
      fs.copyFileSync(src, dest);
    }
  }
}

// Main init function
function init() {
  const args = process.argv.slice(2);
  
  if (args.length === 0) {
    console.error('❌ Usage: npx @burgan-tech/vnext-template <domain-name>');
    console.error('   Example: npx @burgan-tech/vnext-template user-management');
    process.exit(1);
  }
  
  const domainName = args[0].trim();
  
  if (!validateDomainName(domainName)) {
    process.exit(1);
  }
  
  const targetDir = path.resolve(process.cwd(), domainName);
  
  // Check if target directory already exists
  if (fs.existsSync(targetDir)) {
    console.error(`❌ Error: Directory "${domainName}" already exists`);
    process.exit(1);
  }
  
  console.log('🚀 vNext Template Initialization');
  console.log('=================================\n');
  console.log(`📝 Creating project: ${domainName}\n`);
  
  // Get package directory
  const packageDir = getPackageDir();
  
  // Verify template directory exists
  const templateDir = path.join(packageDir, 'touch');
  if (!fs.existsSync(templateDir)) {
    console.error(`❌ Error: Template directory not found in package`);
    console.error(`   Expected: ${templateDir}`);
    console.error(`   Package directory: ${packageDir}`);
    process.exit(1);
  }
  
  // Create target directory
  fs.mkdirSync(targetDir, { recursive: true });
  
  // Files and directories to copy
  const itemsToCopy = [
    'touch',
    'vnext.config.json',
    'package.json',
    'index.js',
    'README.md',
    'CHANGELOG.md',
    'LICENSE',
    'test.js',
    'validate.js',
    'sync-schema-version.js',
    'test-domain-detection.sh',
    '.gitignore',
    '.gitattributes',
    '.cursorrules'
  ];
  
  console.log('📦 Copying template files...');
  
  // Copy each item
  for (const item of itemsToCopy) {
    const srcPath = path.join(packageDir, item);
    const destPath = path.join(targetDir, item === 'touch' ? domainName : item);
    
    if (fs.existsSync(srcPath)) {
      if (fs.statSync(srcPath).isDirectory()) {
        copyAndReplace(srcPath, destPath, domainName, ['node_modules', '.git', 'dist']);
      } else {
        const content = fs.readFileSync(srcPath, 'utf8');
        const replaced = replaceInContent(content, domainName);
        fs.writeFileSync(destPath, replaced, 'utf8');
      }
    }
  }
  
  console.log(`  ✓ Created project directory: ${domainName}/`);
  console.log(`  ✓ Replaced touch in all files`);
  console.log(`  ✓ Renamed touch/ to ${domainName}/\n`);
  
  // Install dependencies
  console.log('📥 Installing dependencies...');
  try {
    process.chdir(targetDir);
    execSync('npm install', { stdio: 'inherit' });
    console.log('\n✅ Project initialized successfully!\n');
    console.log(`📁 Project location: ${targetDir}`);
    console.log(`\n🚀 Next steps:`);
    console.log(`   cd ${domainName}`);
    console.log(`   npm run validate`);
    console.log(`   npm test\n`);
  } catch (error) {
    console.error('\n⚠️  Warning: Failed to install dependencies automatically');
    console.error(`   Please run: cd ${domainName} && npm install\n`);
    process.exit(1);
  }
}

// Run init
init();

