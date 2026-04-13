/**
 * patch-client.cjs
 * Post-processes the NSwag-generated AuthClient.ts to inject the
 * AuthClientBase import (NSwag doesn't emit ES module imports for base classes).
 */
const fs = require('fs')
const path = require('path')

const clientPath = path.join(__dirname, '..', 'src', 'api', 'AuthClient.ts')
const importLine = "import { AuthClientBase } from './AuthClientBase'"

let content = fs.readFileSync(clientPath, 'utf8')

// Skip if already patched
if (content.includes(importLine)) {
  console.log('[patch-client] Already patched, skipping.')
  process.exit(0)
}

// Inject after the auto-generated header comment block (after last */ line)
const lines = content.split('\n')
const lastCommentLine = lines.reduce((last, line, idx) =>
  line.trim().startsWith('//') || line.trim() === '*/' || line.trim().startsWith('/*') ? idx : last, -1)

const insertAt = lastCommentLine + 1
lines.splice(insertAt, 0, importLine)
fs.writeFileSync(clientPath, lines.join('\n'))
console.log(`[patch-client] Injected import at line ${insertAt + 1}`)
