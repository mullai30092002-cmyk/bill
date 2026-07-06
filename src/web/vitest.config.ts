import { defineConfig } from 'vitest/config';

export default defineConfig({
  test: {
    environment: 'jsdom',
    globals: true,
    setupFiles: ['./src/test/setup.ts'],
    include: ['src/**/*.{test,spec}.{ts,tsx}'],
    css: true,
    // Route-level admin tests render the full app shell and can run near 10s under full-suite load.
    testTimeout: 15000,
    // The full BillSoft web suite is heavy enough that running test files in parallel causes load-related flakes.
    fileParallelism: false,
  },
});
