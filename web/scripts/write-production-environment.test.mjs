import assert from 'node:assert/strict';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { test } from 'node:test';

import {
  renderProductionEnvironment,
  writeProductionEnvironment,
} from './write-production-environment.mjs';

test('renders a valid production environment module with the site key', () => {
  const source = renderProductionEnvironment('0x4AAAAAAAATESTKEY');

  assert.match(source, /production: true/);
  assert.match(source, /apiBaseUrl: '\/api\/v1'/);
  assert.match(source, /turnstileSiteKey: "0x4AAAAAAAATESTKEY"/);
});

test('JSON-escapes quotes and control characters in the site key', () => {
  const source = renderProductionEnvironment("key'with\"quotes\n");

  assert.match(source, /turnstileSiteKey: "key'with\\"quotes\\n"/);
});

test('refuses to write an empty or missing site key', () => {
  assert.throws(() => renderProductionEnvironment(''), /TURNSTILE_SITE_KEY/);
  assert.throws(() => renderProductionEnvironment('   '), /TURNSTILE_SITE_KEY/);
  assert.throws(() => renderProductionEnvironment(), /TURNSTILE_SITE_KEY/);
});

test('writes the environment file for the Docker build step', () => {
  const destination = path.join(os.tmpdir(), `amanah-env-${Date.now()}.ts`);

  try {
    writeProductionEnvironment('0x4AAAAAAAATESTKEY', destination);
    assert.equal(
      fs.readFileSync(destination, 'utf8'),
      renderProductionEnvironment('0x4AAAAAAAATESTKEY'),
    );
  } finally {
    fs.rmSync(destination, { force: true });
  }
});
