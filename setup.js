#!/usr/bin/env node

const fs = require('fs');
const path = require('path');
const readline = require('readline');

// Check if already set up (domain directory exists and is not touch)
function isAlreadySetup() {
  // Check if touch directory exists and has vnext structure (already configured)
  if (fs.existsSync('touch')) {
    if (fs.existsSync(path.join('touch', 'Schemas')) ||
        fs.existsSync(path.join('touch', 'Workflows')) ||
        fs.existsSync(path.join('touch', 'Tasks'))) {
      return true;
    }
  }
  
  // Check for other domain directories
  const entries = fs.readdirSync('.', { withFileTypes: true });
  for (const entry of entries) {
    if (entry.isDirectory() && 
        !entry.name.startsWith('.') && 
        entry.name !== 'node_modules' &&
        entry.name !== 'dist' &&
        entry.name !== 'touch') {
      // Check if it contains typical vnext structure
      const domainPath = entry.name;
      if (fs.existsSync(path.join(domainPath, 'Schemas')) ||
          fs.existsSync(path.join(domainPath, 'Workflows')) ||
          fs.existsSync(path.join(domainPath, 'Tasks'))) {
        return true;
      }
    }
  }
  return false;
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

// Prompt for domain name
function promptDomainName() {
  const rl = readline.createInterface({
    input: process.stdin,
    output: process.stdout
  });

  return new Promise((resolve) => {
    rl.question('Enter your domain name (e.g., "user-management", "order-processing"): ', (answer) => {
      rl.close();
      const domainName = answer.trim();
      if (!validateDomainName(domainName)) {
        process.exit(1);
      }
      resolve(domainName);
    });
  });
}

// Get domain name from command line arguments, environment variable, or prompt
async function getDomainName() {
  // Check for command line argument
  const args = process.argv.slice(2);
  if (args.length > 0) {
    const domainName = args[0].trim();
    if (validateDomainName(domainName)) {
      return domainName;
    } else {
      process.exit(1);
    }
  }
  
  // Check for environment variable (for npm install usage)
  if (process.env.DOMAIN_NAME) {
    const domainName = process.env.DOMAIN_NAME.trim();
    if (validateDomainName(domainName)) {
      return domainName;
    } else {
      console.error(`❌ Invalid domain name in DOMAIN_NAME environment variable: ${domainName}`);
      process.exit(1);
    }
  }
  
  // In non-interactive mode, skip prompting
  if (isNonInteractive()) {
    return null;
  }
  
  // Otherwise prompt for it
  return await promptDomainName();
}

// Replace touch in a file (domain name placeholder)
function replaceInFile(filePath, domainName) {
  try {
    let content = fs.readFileSync(filePath, 'utf8');
    const originalContent = content;
    // Replace "touch" in quotes, touch/ paths, and standalone touch as domain placeholder
    content = content.replace(/"touch"/g, `"${domainName}"`)
                      .replace(/'touch'/g, `'${domainName}'`)
                      .replace(/touch\//g, `${domainName}/`)
                      .replace(/\btouch\b/g, domainName);
    
    if (content !== originalContent) {
      fs.writeFileSync(filePath, content, 'utf8');
      return true;
    }
    return false;
  } catch (error) {
    console.warn(`⚠️  Warning: Could not process ${filePath}: ${error.message}`);
    return false;
  }
}

// Replace touch in all files recursively
function replaceInDirectory(dirPath, domainName, processedFiles = new Set()) {
  if (!fs.existsSync(dirPath)) {
    return;
  }

  const entries = fs.readdirSync(dirPath, { withFileTypes: true });
  
  for (const entry of entries) {
    const fullPath = path.join(dirPath, entry.name);
    const relativePath = path.relative('.', fullPath);
    
    // Skip node_modules, .git, and other common directories
    if (entry.name === 'node_modules' || 
        entry.name === '.git' || 
        entry.name === 'dist' ||
        entry.name.startsWith('.')) {
      continue;
    }

    if (entry.isDirectory()) {
      replaceInDirectory(fullPath, domainName, processedFiles);
    } else if (entry.isFile()) {
      // Process text files
      const ext = path.extname(entry.name).toLowerCase();
      if (['.json', '.js', '.md', '.sh', '.txt', '.yml', '.yaml'].includes(ext) ||
          entry.name === '.gitignore' ||
          entry.name === '.gitattributes' ||
          entry.name === '.cursorrules') {
        if (!processedFiles.has(relativePath)) {
          if (replaceInFile(fullPath, domainName)) {
            processedFiles.add(relativePath);
          }
        }
      }
    }
  }
}

// Rename touch directory
function renameDomainDirectory(domainName) {
  const templateDir = 'touch';
  const targetDir = domainName;
  
  if (fs.existsSync(templateDir)) {
    if (fs.existsSync(targetDir)) {
      console.error(`❌ Error: Directory ${targetDir} already exists`);
      process.exit(1);
    }
    fs.renameSync(templateDir, targetDir);
    console.log(`  ✓ Renamed ${templateDir}/ to ${targetDir}/`);
    return true;
  }
  return false;
}

// Check if running in node_modules (installed as dependency)
function isInstalledAsDependency() {
  const cwd = process.cwd();
  return cwd.includes('node_modules');
}

// Check if running in non-interactive environment (like npm install)
function isNonInteractive() {
  return !process.stdin.isTTY || process.env.CI === 'true' || process.env.NPM_CONFIG_INTERACTIVE === 'false';
}

// Get domain name from vnext.config.json
function getDomainFromConfig() {
  try {
    if (fs.existsSync('vnext.config.json')) {
      const config = JSON.parse(fs.readFileSync('vnext.config.json', 'utf8'));
      if (config.domain && typeof config.domain === 'string') {
        return config.domain.trim();
      }
    }
  } catch (error) {
    // If config file doesn't exist or is invalid, return null
    return null;
  }
  return null;
}

// Check if domain folder from config exists
function isDomainFolderConfigured() {
  const domainFromConfig = getDomainFromConfig();
  if (!domainFromConfig) {
    return false;
  }
  
  // Check if the domain folder exists and has vnext structure
  if (fs.existsSync(domainFromConfig)) {
    if (fs.existsSync(path.join(domainFromConfig, 'Schemas')) ||
        fs.existsSync(path.join(domainFromConfig, 'Workflows')) ||
        fs.existsSync(path.join(domainFromConfig, 'Tasks'))) {
      return true;
    }
  }
  
  return false;
}

// Main setup function
async function setup() {
  // Skip if installed as a dependency
  if (isInstalledAsDependency()) {
    return;
  }

  console.log('🚀 vNext Template Setup');
  console.log('=======================\n');

  // Check if domain folder from vnext.config.json exists
  const domainFromConfig = getDomainFromConfig();
  if (domainFromConfig && isDomainFolderConfigured()) {
    console.log(`✅ Domain "${domainFromConfig}" is already configured`);
    console.log(`   Domain folder "${domainFromConfig}" exists and is set up.`);
    console.log('   Skipping setup. If you want to re-run setup, remove the domain directory first.\n');
    return;
  }

  // Check if already set up
  if (isAlreadySetup()) {
    console.log('✅ Template is already set up with a domain name');
    console.log('   Skipping setup. If you want to re-run setup, remove the domain directory first.\n');
    return;
  }

  // Check if touch directory exists
  if (!fs.existsSync('touch')) {
    console.log('⚠️  Template directory touch not found');
    console.log('   This might already be a configured project.\n');
    return;
  }

  // Get domain name from command line or prompt
  const domainName = await getDomainName();
  
  // If no domain name provided (non-interactive mode), skip setup
  if (!domainName) {
    console.log('⚠️  Skipping setup in non-interactive mode');
    console.log('   To set up, run: npm run setup <domain-name>\n');
    return;
  }
  
  console.log(`\n📝 Setting up domain: ${domainName}\n`);

  // Replace in all files
  console.log('🔄 Replacing touch in files...');
  const processedFiles = new Set();
  replaceInDirectory('.', domainName, processedFiles);
  console.log(`  ✓ Processed ${processedFiles.size} file(s)`);

  // Rename directory
  console.log('\n📁 Renaming domain directory...');
  renameDomainDirectory(domainName);

  console.log('\n✅ Setup complete!');
  console.log(`\nYour domain "${domainName}" is now configured.`);
  console.log('You can start adding your schemas, workflows, tasks, and other components.\n');
}

// Run setup
setup().catch((error) => {
  console.error('❌ Setup failed:', error.message);
  process.exit(1);
});

