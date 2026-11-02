'use strict';

// index.htmlのCONFIG内にあるビルド固有値だけを置換し、本番HTMLを再生成する。
const fs = require('fs');
const path = require('path');

const root = __dirname;
const sourcePath = path.join(root, 'index.html');
const outputPath = path.join(root, 'index-prod.html');
const source = fs.readFileSync(sourcePath, 'utf8');
const pattern = /  \/\* BUILD_CONFIG_START: build-prod\.js はこの範囲だけを置換する \*\/[\s\S]*?  \/\* BUILD_CONFIG_END \*\//g;
const matches = source.match(pattern) || [];

if (matches.length !== 1) {
  throw new Error(`CONFIGのビルド固有範囲が1件ではありません（${matches.length}件）`);
}

const productionConfig = `  /* BUILD_CONFIG_START: build-prod.js はこの範囲だけを置換する */
  BUILD: 'production',
  STORAGE: Object.freeze({
    main: 'gunya.save.v2',
    bak: 'gunya.save.v2.bak',
    broken: 'gunya.save.v2.broken',
    legacyV1: 'gunya-yakyu-save-v3',
    backupV1: 'gunya.backup.v1',
    migrationEnabled: true,
  }),
  /* BUILD_CONFIG_END */`;

const output = source.replace(pattern, productionConfig);
fs.writeFileSync(outputPath, output);
console.log(`generated ${path.basename(outputPath)} from ${path.basename(sourcePath)}`);
