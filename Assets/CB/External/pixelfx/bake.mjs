// NeonFault - pixelfx bake script (upstream: patch-gg/pixelfx, MIT).
//
// Bakes the 6 spike VFX into Assets/CB/External/pixelfx/Resources as
// <category>_<name>.png + <category>_<name>.json, which CBPixelFx + CBPixelFxImporter
// consume at runtime. Palettes are the canonical NeonFault neon ramps (art-director,
// VFX-STYLE-01). No npm install needed: --sheet only uses pixelfx's in-repo tooling.
//
// USAGE (dev machine only - not CI):
//   1. git -c http.sslVerify=false clone --depth 1 https://github.com/patch-gg/pixelfx D:/unity111/pixelfx_tmp
//   2. (optional) set PIXELFX_REPO if you cloned elsewhere; or pass the repo path as argv[3]
//   3. node Assets/CB/External/pixelfx/bake.mjs
//
// The script locates the pixelfx repo via PIXELFX_REPO (default D:/unity111/pixelfx_tmp)
// or argv[3], and writes to the Resources folder next to this file (or argv[2]).

import { fileURLToPath } from 'node:url';
import { join, dirname } from 'node:path';
import { pathToFileURL } from 'node:url';
import { mkdirSync } from 'node:fs';

const __dirname = dirname(fileURLToPath(import.meta.url));
const REPO = process.argv[3] || process.env.PIXELFX_REPO || 'D:/unity111/pixelfx_tmp';
const OUT = process.argv[2] || join(__dirname, 'Resources');

// Canonical NeonFault palettes (dark -> bright hex). See docs/pixelfx-style.md.
const PALETTES = {
  neon_fire:   ['#1a0800', '#4d1400', '#a83200', '#ff6a00', '#ffb02e', '#fff0bf'],
  neon_holy:   ['#012a26', '#03534a', '#0a9b86', '#2fe0c0', '#8ff7e6', '#e8fff9'],
  neon_arcane: ['#2a0026', '#5e0058', '#a3139b', '#e83fd6', '#ff8ff0', '#ffe6fb'],
  neon_ice:    ['#021830', '#064a7a', '#0c87c4', '#46c4f0', '#aee8ff', '#eafaff'],
  neon_shadow: ['#05010a', '#16082b', '#2c1147', '#451c6b', '#5e2e8c', '#7a45ab'],
  neon_blood:  ['#2a0000', '#6e0505', '#b31212', '#ef2b2b', '#ff6b6b', '#ffd6d6'],
  neon_coin:   ['#2a1a00', '#6b4400', '#b87a00', '#ffb627', '#ffe08a', '#fff6d6'],
};

// [effect id, palette key, output name]
const TARGETS = [
  ['explosions/barrel-explosion', 'neon_fire',   'explosions_barrel-explosion'],
  ['holy/heal',                  'neon_holy',   'holy_heal'],
  ['ui/coin-collect',            'neon_coin',   'ui_coin-collect'],
  ['projectiles/arrow',          'neon_arcane', 'projectiles_arrow'],
  ['impacts/crit-hit',           'neon_blood',  'impacts_critical-hit'],
  ['melee/heavy-cleave',         'neon_coin',   'melee_heavy-cleave'],
];

async function main() {
  const fxlib = await import(pathToFileURL(join(REPO, 'tools/lib/fxlib.mjs')).href);
  const { loadEffect, renderFrames, spriteSheet, save } = fxlib;
  if (!loadEffect || !renderFrames || !spriteSheet || !save) {
    throw new Error(`Could not load pixelfx fxlib from "${REPO}". Clone it first (see header).`);
  }

  mkdirSync(OUT, { recursive: true });

  for (const [id, palKey, name] of TARGETS) {
    const fx = await loadEffect(id);                       // { id, file, mod, meta }
    const r = renderFrames(fx, { options: { palette: PALETTES[palKey] } });
    const { png, json } = spriteSheet(fx, r, name);          // png: Uint8Array, json: object
    save(join(OUT, name + '.png'), png);
    save(join(OUT, name + '.json'), JSON.stringify(json));
    console.log(`baked ${name}: ${r.w}x${r.h} x ${r.n} frames @ ${r.fps}fps`);
  }

  console.log(`\nDone. ${TARGETS.length} effects -> ${OUT}`);
  console.log('In Unity, CBPixelFxImporter fixes import settings automatically,');
  console.log('or run the menu item: CB > Reimport PixelFx.');
}

main().catch((e) => {
  console.error('BAKE FAILED:', e && e.message ? e.message : e);
  process.exit(1);
});
