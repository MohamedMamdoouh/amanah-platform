import { writeFileSync } from 'node:fs';

const turnstileSiteKey = process.env.TURNSTILE_SITE_KEY ?? '';
const escaped = turnstileSiteKey.replace(/\\/g, '\\\\').replace(/'/g, "\\'");

const contents = `export const environment = {
  production: true,
  apiBaseUrl: '/api/v1',
  turnstileSiteKey: '${escaped}',
};
`;

writeFileSync('src/environments/environment.production.ts', contents);
