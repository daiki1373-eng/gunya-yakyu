import {readFile,writeFile,mkdir} from 'node:fs/promises';
import {fileURLToPath} from 'node:url';
import path from 'node:path';
import {build} from 'esbuild';
import {Script} from 'node:vm';
const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'..');
const result=await build({entryPoints:[path.join(root,'src/runtime.js')],bundle:true,minify:true,format:'iife',target:['safari15'],legalComments:'inline',write:false});
const notices=[];
for(const name of ['three','@capacitor/core','@capacitor/app','@capacitor/haptics','@capacitor/preferences']){
  let license;for(const filename of ['LICENSE','LICENSE.md','LICENSE.txt']){try{license=await readFile(path.join(root,'node_modules',name,filename),'utf8');break}catch{}}
  if(!license)throw Error(`Missing license for ${name}`);notices.push(`${name}\n${license}`);
}
const runtime=result.outputFiles[0].text.replace(/<\/script/gi,'<\\/script');
let template=await readFile(path.join(root,'src/game.html'),'utf8');
const controls=await readFile(path.join(root,'src/live-controls.js'),'utf8');
template=template.replace('/* LIVE_CONTROLS */',()=>controls);
if(!template.includes('<!-- BUNDLED_RUNTIME -->'))throw Error('Missing runtime marker');
const noticeText=notices.join('\n\n');
const html=template.replace('<!-- BUNDLED_RUNTIME -->',()=>'<script>'+runtime+'</script>').replace('</head>',()=>`<!-- Third-party notices\n${noticeText.replace(/--/g,'- -')}\n-->\n</head>`);
for(const [i,script] of [...html.matchAll(/<script[^>]*>([\s\S]*?)<\/script>/g)].entries())new Script(script[1],{filename:`inline-script-${i}.js`});
await mkdir(path.join(root,'www'),{recursive:true});
await writeFile(path.join(root,'www/index.html'),html);
await writeFile(path.join(root,'www/THIRD-PARTY-NOTICES.txt'),noticeText);
await writeFile(path.join(root,'../landscape/claude-v.html'),html);
console.log(`Built identical offline web/iPhone game (${(Buffer.byteLength(html)/1024).toFixed(0)} KiB)`);
