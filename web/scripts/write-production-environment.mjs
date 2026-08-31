import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const productionEnvironmentPath = path.resolve(
  path.dirname(fileURLToPath(import.meta.url)),
  '../src/environments/environment.production.ts',
);

export function renderProductionEnvironment(siteKey) {
  if (typeof siteKey !== 'string' || siteKey.trim() === '') {
    throw new Error(
      'TURNSTILE_SITE_KEY is required to build the production Angular app.',
    );
  }

  return `export const environment = {
  production: true,
  apiBaseUrl: '/api/v1',
  turnstileSiteKey: ${JSON.stringify(siteKey)},
};
`;
}

export function writeProductionEnvironment(
  siteKey = process.env.TURNSTILE_SITE_KEY,
  destinationPath = productionEnvironmentPath,
) {
  fs.writeFileSync(destinationPath, renderProductionEnvironment(siteKey ?? ''));
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  writeProductionEnvironment();
}
