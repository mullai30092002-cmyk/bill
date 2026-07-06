import { existsSync, readFileSync, readdirSync } from 'node:fs';
import { join, resolve } from 'node:path';

import { describe, expect, it } from 'vitest';

const forbiddenVendorImports = [
  '@chakra-ui/react',
  'antd',
  '@mui/material',
  'react-bootstrap',
];

const walkTsx = (dir: string, results: string[] = []): string[] => {
  if (!existsSync(dir)) {
    return results;
  }

  for (const entry of readdirSync(dir, { withFileTypes: true })) {
    const full = join(dir, entry.name);
    if (entry.isDirectory()) {
      walkTsx(full, results);
    } else if (entry.isFile() && entry.name.endsWith('.tsx') && !entry.name.endsWith('.test.tsx')) {
      results.push(full);
    }
  }

  return results;
};

const scanFiles = () => [
  ...walkTsx(resolve(process.cwd(), 'src/features')),
  resolve(process.cwd(), 'src/App.tsx'),
];

describe('frontend boundary', () => {
  it('keeps feature and app code off direct vendor UI imports', () => {
    const violations = scanFiles().filter(file => {
      const content = readFileSync(file, 'utf8');
      return forbiddenVendorImports.some(importPath => content.includes(importPath));
    });

    expect(violations).toEqual([]);
  });
});
