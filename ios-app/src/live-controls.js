let LIVE=null;
function liveThrow(base){if(LIVE&&!LIVE.off.mine)LIVE.play.queueThrow(base)}
function liveRun(action){if(LIVE&&LIVE.off.mine)LIVE.play.command(action,LIVE.runner)}
function cycleLiveRunner(){if(!LIVE)return;const candidates=LIVE.play.runners.map((r,i)=>!r.out&&!r.scored?i:null).filter(i=>i!==null),at=candidates.indexOf(LIVE.runner);LIVE.runner=at===candidates.length-1?-1:candidates[at+1];liveButtons()}
function liveButtons(){if(!LIVE)return;$('#bl').innerHTML='';const br=$('#br');
 if(LIVE.off.mine){const chosen=LIVE.runner<0?'全員':LIVE.play.runners[LIVE.runner].player.name;br.innerHTML=`<button class="gb" style="background:#258565" onpointerdown="liveRun('advance')">進塁</button><button class="gb" style="background:#3274aa" onpointerdown="liveRun('retreat')">帰塁</button><button class="gb" style="background:#6f6896" onpointerdown="liveRun('hold')">止まる</button><button class="gb" style="background:#6b663e;font-size:16px" onpointerdown="cycleLiveRunner()">${chosen}</button>`}
 else br.innerHTML=[[1,'一塁'],[2,'二塁'],[3,'三塁'],[4,'本塁']].map(([i,n])=>`<button class="gb" style="background:${['','#236f8f','#277c64','#9a673b','#984651'][i]}" onpointerdown="liveThrow(${i})">${n}送球</button>`).join('');
}
addEventListener('keydown',e=>{if(!LIVE||e.target.matches('input,select,textarea'))return;if(!LIVE.off.mine&&'1234'.includes(e.key))liveThrow(+e.key);if(LIVE.off.mine){if(e.key==='ArrowUp')liveRun('advance');if(e.key==='ArrowDown')liveRun('retreat');if(e.key===' ')liveRun('hold');if(e.key==='Tab'){e.preventDefault();cycleLiveRunner()}}});
async function playAnim(r,off,def){
 if(r.foul)return playAnimLegacy(r,off,def);
 initField(off,def);V.mode='field';const f=V.f,defenders={};for(let p=1;p<=9;p++){const person=def.lineup.find(q=>q.pos===p)||{field:50,arm:50};defenders[p]={position:FP[p].slice(),current:f.fl[p],field:person.field,arm:person.arm}}
 const play=new window.BaseballPlay.LivePlay({result:r,batter:r.noBatter?null:r.bat||mkRun(off.lineup[G.idx[off===G.me?'me':'you']]),bases:G.bases,outs:G.outs,defenders});LIVE={play,off,def,runner:-1};liveButtons();
 const initialTargets=Object.fromEntries(Array.from({length:9},(_,i)=>[i+1,FP[i+1].slice()]));initialTargets[2]=[0,-.5];initialTargets[3]=BP[1].slice();initialTargets[4]=BP[2].slice();initialTargets[5]=BP[3].slice();initialTargets[6]=r.fp>=7?[r.fl.to[0]*.45,r.fl.to[1]*.5]:[-4,27];if(r.fp===3)initialTargets[1]=BP[1].slice();
 let last=nw(),lastOuts=0,tagSet=false;const flight=r.fl||{to:[0,18.4],H:0,dur:700},flightTime=Math.max(flight.dur/1000,Math.sqrt(Math.max(0,8*(flight.H||0)/9.81))*.65);
 await new Promise(resolve=>{function tick(){const now=nw(),dt=Math.min(.1,Math.max(0,(now-last)/1000));last=now;
  if(!play.finished){
   const targets=Object.fromEntries(Object.entries(initialTargets).map(([p,a])=>[p,a.slice()]));if(!play.caught&&r.fp)targets[r.fp]=flight.to.slice();if(play.ballFlight)targets[play.ballFlight.receiver]=play.ballFlight.to.slice();
   f.motion={};for(let p=1;p<=9;p++){const at=f.fl[p],to=targets[p],dx=to[0]-at[0],dy=to[1]-at[1],distance=Math.hypot(dx,dy),step=Math.min(distance,dt*(5.5+defenders[p].field*.04));if(distance>.01){at[0]+=dx/distance*step;at[1]+=dy/distance*step}defenders[p].current=at;f.motion[p]={kind:distance>.2?'run':'idle',facing:Math.atan2(dx,-dy)}}
   play.update(dt*(r.hr?2.4:1),{autoDefense:off.mine});if(!play.caught&&r.fp&&play.catchAt-play.time<.45)f.motion[r.fp].kind=flight.type==='fly'?'catch-high':'scoop';if(play.ballFlight){const q=play.ballFlight;f.motion[q.thrower]={kind:'throw',u:cl((play.time-q.start)/.5,0,1),facing:Math.atan2(q.to[0]-q.from[0],-(q.to[1]-q.from[1]))}}
   if(play.caught&&!tagSet){tagSet=true;if(play.cleanCatch&&!off.mine)for(const runner of play.runners.slice(1)){const desired=r.mv.find(m=>m.from===runner.origin)?.to??runner.origin;if(desired>runner.origin)runner.goal=desired}}
   f.runners=play.runners.map(p=>{const i=Math.min(3,Math.floor(p.progress)),direction=p.goal>=p.progress?1:-1,a=BP[i],b=BP[i+1];return{id:p.player,a:p.progress,b:p.progress,u:0,vis:!p.out&&!p.scored,facing:Math.atan2((b[0]-a[0])*direction,-(b[1]-a[1])*direction)}});
   const bl=f.ball;bl.vis=true;
   if(!play.caught){const u=cl(play.time/Math.max(.1,flightTime),0,1);bl.x=flight.to[0]*u;bl.y=flight.to[1]*u;bl.z=flight.type==='fly'?1.2+4*(flight.H||1)*u*(1-u):.12+Math.abs(Math.sin(u*Math.PI*4))*(1-u);if(r.hr&&u>=1)bl.vis=false}
   else if(play.ballFlight){const q=play.ballFlight,u=cl((play.time-q.start)/q.duration,0,1);bl.x=q.from[0]+(q.to[0]-q.from[0])*u;bl.y=q.from[1]+(q.to[1]-q.from[1])*u;bl.z=1.2+Math.sin(u*Math.PI)*.8}
   else{const p=f.fl[play.ballHolder||r.fp||1];bl.x=p[0];bl.y=p[1];bl.z=1.2}
   if(play.outs.length>lastOuts){lastOuts=play.outs.length;flash(lastOuts>1?'ダブルプレー！':'アウト！');sndOut()}
   if(play.finished){Object.assign(r,play.finalize());LIVE=null;setBtns('');flash(r.text);tel1(r.text);if(r.hit)sndCheer(.5);resolve();return}
  }requestAnimationFrame(tick)}requestAnimationFrame(tick)});
 await sleep(350);
}
async function animateAward(r,off,def){initField(off,def);V.mode='field';V.f.ball.vis=false;V.f.runners=r.mv.filter(m=>!m.out).map(m=>({id:m.r,a:m.from,b:m.to,u:0,vis:true}));await tween(1200,u=>V.f.runners.forEach(p=>{p.u=u}));V.mode='bat';V.f=null}
async function stealRunLive(def){const i=G.bases[1]&&!G.bases[2]?1:G.bases[0]&&!G.bases[1]?0:-1;if(i<0)return;const runner=G.bases[i],off=def===G.me?G.you:G.me,k=off===G.me?'me':'you',r={ab:false,noBatter:true,stealOrigin:i+1,fp:2,fl:{type:'gr',to:[0,-1.8],H:0,dur:150},mv:hold().map(m=>m.from===i+1?{...m,to:i+2}:m)};await playAnim(r,off,def);const success=r.mv.some(m=>m.r===runner&&!m.out&&m.to>i+1);applyRes(r,{hero:false},pit(def),k);if(success&&runner.hero&&runner.ref)runner.ref.rec.sb++;updHUD();V.mode='bat';V.f=null;await sleep(500)}
