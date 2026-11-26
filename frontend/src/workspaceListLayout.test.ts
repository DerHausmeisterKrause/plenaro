// @ts-ignore: Node is available in Vitest, but browser-only tsconfig omits @types/node.
import {readFileSync} from 'node:fs';
import {describe,expect,it} from 'vitest';

// The CI runs Vitest from frontend/. Read the real stylesheet, not a CSS stub.
const styles=readFileSync('src/styles.css','utf8');

/** Guard the actual CSS contract: jsdom cannot calculate grid/flex heights. */
describe('Workplace task-list height',()=>{
  it('fills its stretching card instead of stopping at a fixed viewport height',()=>{
    const desktop=styles.match(/\.plenaro-today-list\s*>\s*\.plenaro-rows\s*\{([^}]+)\}/);
    expect(desktop?.[1]).toBeDefined();
    expect(desktop![1]).toMatch(/flex:\s*1\s+1\s+0px\s*;/);
    expect(desktop![1]).toMatch(/height:\s*0\s*;/);
    expect(desktop![1]).toMatch(/min-height:\s*0\s*;/);
    expect(desktop![1]).toMatch(/max-height:\s*none\s*;/);
    expect(desktop![1]).toMatch(/overflow-y:\s*auto\s*;/);
    expect(styles).toMatch(/\.plenaro-today-columns\s*\{[^}]*align-items:\s*stretch/);
    expect(styles).toMatch(/\.plenaro-today-list\s*\{[^}]*flex:\s*1\s+1\s+auto/);
  });

  it('keeps the previous viewport-limited scrolling in the stacked mobile view',()=>{
    const mobile=styles.match(/@media\s*\(max-width:\s*1050px\)\s*\{[\s\S]*?\.plenaro-today-list\s*>\s*\.plenaro-rows\s*\{([^}]+)\}/);
    expect(mobile?.[1]).toBeDefined();
    expect(mobile![1]).toMatch(/height:\s*auto\s*;/);
    expect(mobile![1]).toMatch(/flex:\s*0\s+1\s+auto\s*;/);
    expect(mobile![1]).toMatch(/max-height:\s*66vh\s*;/);
  });
});
