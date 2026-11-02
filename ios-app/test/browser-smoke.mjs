import {chromium} from 'playwright';
import {readFile,mkdir} from 'node:fs/promises';
import {createServer} from 'node:http';
import {fileURLToPath} from 'node:url';
import path from 'node:path';
import assert from 'node:assert/strict';
const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'..'),out=path.join(root,'build/browser-check');await mkdir(out,{recursive:true});
const html=await readFile(path.join(root,'www/index.html'));
const server=createServer((req,res)=>{res.writeHead(200,{'content-type':'text/html; charset=utf-8'});res.end(html)});await new Promise(r=>server.listen(0,'127.0.0.1',r));
const browser=await chromium.launch({headless:true,executablePath:process.env.BROWSER_EXECUTABLE||undefined,args:['--enable-unsafe-swiftshader']}),context=await browser.newContext({viewport:{width:844,height:390},isMobile:true,hasTouch:true}),page=await context.newPage(),errors=[],external=[];
page.on('pageerror',e=>errors.push(e.message));page.on('request',r=>{if(!r.url().startsWith('http://127.0.0.1:')&&!r.url().startsWith('data:'))external.push(r.url())});
try{
 await page.goto(`http://127.0.0.1:${server.address().port}/`);await page.getByRole('button',{name:'はじめから',exact:true}).click();await page.locator('#sp').selectOption('panda');await page.locator('#inn').selectOption('3');await page.getByRole('button',{name:'この内容で始める！',exact:true}).click();
 await page.evaluate(()=>{G={me:buildMine(),you:mkOpp(0),myTop:true,top:false,inn:1,outs:0,b:0,s:0,sc:{me:0,you:0},line:{me:[],you:[]},gauge:{me:0,you:0},arm:{me:null,you:null},idx:{me:0,you:0},bases:[null,null,null],fx:null,over:false};G.lastPt=pit(G.me);V.mode='bat';V.bat={side:'R',col:G.you.color,col2:G.you.color2,pose:'stand',t0:0,sp:'human',mine:false};uiOff();hudOn();updHUD();setBtns('pitch',G.lastPt);pitchRes=()=>{};});
 await page.waitForFunction(()=>window.Gunya3D.error||!document.querySelector('#gl').hidden);assert.equal(await page.evaluate(()=>window.Gunya3D.error),null);await page.screenshot({path:path.join(out,'iphone-batting.png')});
 const renderInfo=await page.evaluate(()=>window.Gunya3D.instance.info());assert(renderInfo.calls<600,`Too many render calls: ${renderInfo.calls}`);
 await page.getByRole('button',{name:'一時停止',exact:true}).click();const paused=await page.evaluate(()=>nw());await page.waitForTimeout(180);assert(Math.abs(await page.evaluate(()=>nw())-paused)<1);await page.getByRole('button',{name:'つづける',exact:true}).click();await page.waitForTimeout(60);assert(await page.evaluate(()=>nw())>paused+40);
 await page.evaluate(()=>{pitchRes=null;const bt=G.you.lineup[0];window.__result={ab:true,hit:false,fp:6,bat:mkRun(bt),fl:{type:'gr',to:FP[6].slice(),H:0,dur:700},mv:[{r:mkRun(bt),from:0,to:1,out:true}]};window.__playDone=false;playAnim(window.__result,G.you,G.me).then(()=>window.__playDone=true);});
 await page.waitForFunction(()=>LIVE?.play.caught);await page.screenshot({path:path.join(out,'iphone-fielding.png')});await page.getByRole('button',{name:'一塁送球',exact:true}).click();await page.waitForFunction(()=>window.__playDone,{},{timeout:12000});assert(await page.evaluate(()=>window.__result.mv.some(m=>m.out&&m.from===0)));
 await page.evaluate(()=>{G.top=true;G.bases=[mkRun(G.me.lineup[2]),null,null];const bt=G.me.lineup[0];window.__result={ab:true,hit:true,fp:7,bat:mkRun(bt),fl:{type:'fly',to:[-45,70],H:10,dur:1900},mv:[{r:mkRun(bt),from:0,to:1},{r:G.bases[0],from:1,to:2}]};playAnim(window.__result,G.me,G.you);});
 await page.getByRole('button',{name:'進塁',exact:true}).click();assert(await page.evaluate(()=>LIVE.play.runners.some(r=>r.goal>r.origin)));await page.getByRole('button',{name:'帰塁',exact:true}).click();assert(await page.evaluate(()=>LIVE.play.runners.find(r=>r.origin===1).goal===1));await page.screenshot({path:path.join(out,'iphone-running.png')});
 await context.setOffline(true);await page.getByRole('button',{name:'一時停止',exact:true}).click();await page.getByRole('button',{name:'つづける',exact:true}).click();assert.deepEqual(external,[]);
 await page.setViewportSize({width:390,height:844});const stage=await page.locator('#stage').boundingBox();assert(stage.x>=-1&&stage.y>=-1&&stage.x+stage.width<=391&&stage.y+stage.height<=845);await page.screenshot({path:path.join(out,'portrait-layout.png')});
 assert.deepEqual(errors,[]);console.log(JSON.stringify({passed:true,renderInfo,checks:['3D描画','iPhone横・縦画面','手動送球でアウト','進塁・帰塁','一時停止の時計','オフライン','外部通信なし'],errors},null,2));
}catch(e){console.error({errors,renderer:await page.evaluate(()=>window.Gunya3D?.error)});throw e}finally{await browser.close();await new Promise(r=>server.close(r))}
