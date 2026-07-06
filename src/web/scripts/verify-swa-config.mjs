import { existsSync } from 'node:fs';
import { resolve } from 'node:path';

const configPath = resolve(process.cwd(), 'dist/staticwebapp.config.json');

if (!existsSync(configPath)) {
  throw new Error('staticwebapp.config.json missing from build output');
}

console.log('SWA config verified');
