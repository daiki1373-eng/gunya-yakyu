export const BASES=[[0,0],[19.4,19.4],[0,38.8],[-19.4,19.4],[0,0]];
export const clamp=(v,a,b)=>Math.max(a,Math.min(b,v));
export function basePosition(progress){const i=clamp(Math.floor(progress),0,3),u=clamp(progress-i,0,1),a=BASES[i],b=BASES[i+1];return[a[0]+(b[0]-a[0])*u,a[1]+(b[1]-a[1])*u]}
export class LivePlay{
 constructor({result,batter,bases,outs=0,defenders}){
  this.result=result;this.initialOuts=outs;this.defenders=defenders;this.time=0;this.outs=[];this.runs=[];this.ballHolder=0;this.heldBase=null;this.ballFlight=null;this.throwQueue=null;this.caught=false;this.finished=false;this.lastEvent=0;
  const moves=result.mv||[],byOrigin=new Map(moves.map(m=>[m.from,m]));
  this.runners=bases.map((p,i)=>p?this.makeRunner(p,i+1,byOrigin.get(i+1)?.to??i+1):null).filter(Boolean);
  if(batter)this.runners.unshift(this.makeRunner(batter,0,result.hr?4:Math.max(1,byOrigin.get(0)?.to||1)));
  if(result.stealOrigin)for(const r of this.runners)if(r.origin===result.stealOrigin)r.progress+=clamp(.5+((r.player.speed||50)-50)/500,.4,.65);
  this.fly=result.fl?.type==='fly'&&!result.foul;this.cleanCatch=this.fly&&!result.hit&&!result.err&&!result.hr;
  const flight=result.fl||{to:[0,18.4],H:0,dur:700};this.landing=flight.to;
  this.catchAt=Math.max(flight.dur/1000,this.fly?Math.sqrt(Math.max(0,8*(flight.H||0)/9.81))*.65:0);
  const f=defenders[result.fp];if(f&&!this.cleanCatch&&!result.hr)this.catchAt=Math.max(this.catchAt,Math.hypot(f.position[0]-flight.to[0],f.position[1]-flight.to[1])/(5.5+f.field*.04));
  if(this.cleanCatch)for(const r of this.runners)if(r.origin>0)r.goal=r.origin;
  if(result.hr)this.catchAt=Infinity;
 }
 makeRunner(player,origin,goal){return{player,origin,progress:origin,goal,base:origin,speed:5.2+(player.speed||50)*.041,out:false,scored:false,retouch:false,queuedGoal:null,at:null}}
 command(action,index=-1){
  for(const [i,r] of this.runners.entries()){
   if((index>=0&&i!==index)||r.out||r.scored)continue;
   if(action==='advance'){const next=clamp(Math.max(r.goal,Math.floor(r.progress)+1),0,4);if(r.retouch)r.queuedGoal=next;else r.goal=next}
   if(action==='retreat'){r.queuedGoal=null;r.goal=clamp(Math.ceil(r.progress)-1,r.origin===0?1:r.origin,3)}
   if(action==='hold'){r.queuedGoal=null;r.goal=clamp(Math.round(r.progress),r.origin===0?1:r.origin,3)}
  }
 }
 queueThrow(base){if(base>=1&&base<=4)this.throwQueue=base}
 forceAt(r,base){
  if(r.origin!==base-1||r.progress>=base-.001)return false;
  for(let i=0;i<r.origin;i++)if(!this.runners.some(p=>p.origin===i&&!p.out&&!p.scored))return false;
  return true;
 }
 retire(r,cause,force=false){if(r.out||r.scored)return;r.out=true;r.at=this.time;this.outs.push({runner:r,cause,force,at:this.time,beforeFirst:r.origin===0&&r.progress<1});this.lastEvent=this.time}
 receive(base,receiver){
  this.ballHolder=receiver;this.heldBase=base;this.ballFlight=null;
  for(const r of this.runners){if(r.out||r.scored)continue;
   if(this.forceAt(r,base)){this.retire(r,'force',true);break}
   if(r.retouch&&r.origin===base&&r.progress>base+.025){this.retire(r,'appeal');break}
   const position=basePosition(r.progress),onBag=Math.abs(r.progress-Math.round(r.progress))<1e-5;
   if(!onBag&&Math.hypot(position[0]-BASES[base][0],position[1]-BASES[base][1])<1.65){this.retire(r,'tag');break}
  }
 }
 launch(base){
  if(!this.ballHolder||this.ballFlight)return false;
  if(base===this.heldBase){this.throwQueue=null;return false}
  const thrower=this.ballHolder,holder=this.defenders[thrower],from=holder.current||holder.position;let to=BASES[base],receiver=this.receiver(base),distance=Math.hypot(to[0]-from[0],to[1]-from[1]),relayBase=null;
  if(thrower>=7&&distance>42){receiver=from[0]<0?6:4;const relay=this.defenders[receiver];to=(relay.current||relay.position).slice();relayBase=base;distance=Math.hypot(to[0]-from[0],to[1]-from[1])}
  const cover=this.defenders[receiver],at=cover.current||cover.position,coverTime=Math.hypot(at[0]-to[0],at[1]-to[1])/(5.5+(cover.field||50)*.04);
  this.ballFlight={base,receiver,thrower,relayBase,from:[...from],to:[...to],start:this.time,duration:Math.max(.22+distance/(20+(holder.arm||50)*.18),coverTime)};this.ballHolder=0;this.heldBase=null;this.throwQueue=null;this.lastEvent=this.time;return true;
 }
 receiver(base){const preferred=base===1?3:base===2?4:base===3?5:2;return preferred===this.result.fp?(base===1?1:6):preferred}
 aiThrow(){
  if(!this.ballHolder||this.ballFlight||this.time<this.lastEvent+.22)return;
  const threats=this.runners.filter(r=>!r.out&&!r.scored&&Math.abs(r.progress-r.goal)>.01);
  let best=null;for(const r of threats){const base=r.retouch?r.origin:clamp(Math.ceil(r.progress),1,4),force=this.forceAt(r,base),left=Math.abs(base-r.progress)*27.44/r.speed;
   const f=this.defenders[this.ballHolder],p=f.current||f.position,distance=Math.hypot(p[0]-BASES[base][0],p[1]-BASES[base][1]),arrival=.22+distance/(20+(f.arm||50)*.18);
   const rating=(r.retouch?4:force?3:1)+(left-arrival)*.2+base*.05;if(!best||rating>best.rating)best={base,rating};
  }if(best&&best.base!==this.heldBase)this.launch(best.base);
 }
 update(dt,{autoDefense=false}={}){
  if(this.finished)return;this.time+=Math.min(dt,.1);
  for(const r of this.runners){if(r.out||r.scored)continue;const distance=r.goal-r.progress,step=r.speed/27.44*Math.min(dt,.1);r.progress+=Math.sign(distance)*Math.min(Math.abs(distance),step);
   if(Math.abs(r.progress-r.goal)<1e-6){r.progress=r.goal;r.base=r.goal;if(r.retouch&&r.goal===r.origin){r.retouch=false;if(r.queuedGoal!==null){r.goal=r.queuedGoal;r.queuedGoal=null}}
    if(r.progress===4&&!r.scored){r.scored=true;r.at=this.time;this.runs.push({runner:r,at:this.time});this.lastEvent=this.time}
   }
  }
  const ordered=this.runners.filter(r=>!r.out&&!r.scored).sort((a,b)=>b.origin-a.origin);for(let i=1;i<ordered.length;i++){const lead=ordered[i-1],trail=ordered[i];if(trail.progress>=lead.progress-.035&&trail.progress>trail.origin){trail.progress=Math.max(trail.origin,lead.progress-.04);if(Math.abs(lead.progress-lead.goal)<1e-6)trail.goal=Math.max(trail.origin===0?1:trail.origin,Math.floor(lead.progress)-1)}}
  if(!this.caught&&this.time>=this.catchAt){this.caught=true;this.ballHolder=this.result.fp||1;this.lastEvent=this.time;
   if(this.cleanCatch){this.retire(this.runners[0],'catch');for(const r of this.runners.slice(1))if(!r.out&&r.progress>r.origin+.001){r.retouch=true;r.goal=r.origin;r.queuedGoal=null}}
  }
  if(this.ballFlight&&this.time>=this.ballFlight.start+this.ballFlight.duration){const q=this.ballFlight;if(q.relayBase){this.ballHolder=q.receiver;this.ballFlight=null;this.launch(q.relayBase)}else this.receive(q.base,q.receiver)}
  if(this.ballHolder&&this.throwQueue!==null)this.launch(this.throwQueue);
  if(this.ballHolder&&this.heldBase){const bag=BASES[this.heldBase];for(const r of this.runners){if(r.out||r.scored||Math.abs(r.progress-Math.round(r.progress))<1e-5)continue;const at=basePosition(r.progress);if(Math.hypot(at[0]-bag[0],at[1]-bag[1])<1.65)this.retire(r,'tag')}}
  if(autoDefense&&this.caught)this.aiThrow();
  const active=this.runners.some(r=>!r.out&&!r.scored&&Math.abs(r.goal-r.progress)>1e-6);
  if(this.initialOuts+this.outs.length>=3||((this.caught||this.result.hr)&&!active&&!this.ballFlight&&this.time-this.lastEvent>.9))this.finished=true;
  if(this.time>28){for(const r of this.runners)if(!r.out&&!r.scored){r.goal=clamp(Math.round(r.progress),1,3);r.progress=r.goal;r.base=r.goal}this.finished=true}
 }
 finalize(){
  const r={...this.result},bat=this.runners.find(p=>p.origin===0),occupied=new Set();
  r.mv=[];for(const p of [...this.runners].sort((a,b)=>b.progress-a.progress)){
   if(p.out)r.mv.push({r:p.player,from:p.origin,to:clamp(Math.round(p.progress),0,4),out:true,at:p.at});
   else if(p.scored)r.mv.push({r:p.player,from:p.origin,to:4,at:p.at});
   else{let base=clamp(Math.round(p.progress),1,3);while(occupied.has(base)&&base>0)base--;if(base>0){occupied.add(base);r.mv.push({r:p.player,from:p.origin,to:base})}}
  }
  r.outEvents=this.outs.map(o=>({cause:o.cause,force:o.force,at:o.at,beforeFirst:o.beforeFirst}));
  if(bat&&!r.hr){r.dp=this.outs.length>=2;r.hit=!this.cleanCatch&&!r.err&&!bat.out&&bat.progress>=1&&!this.outs.some(o=>o.force);r.ab=true;if(this.initialOuts<2&&((this.cleanCatch&&this.runs.length>0)||(r.sac&&bat.out&&!r.dp)))r.ab=false;r.text=r.dp?'ダブルプレー！':this.cleanCatch?'フライ捕球！':bat.out?'アウト！':r.hit?'ヒット！':this.outs.length?'フィルダースチョイス':'セーフ！'}else if(!bat){r.ab=false;r.text=this.outs.length?'盗塁アウト！':this.runners.some(p=>p.progress>p.origin)?'盗塁成功！':'帰塁！'}
  return r;
 }
}
export function validRuns(result,priorOuts){
 const third=result.outEvents?.[Math.max(0,2-priorOuts)];if(!third)return result.mv.filter(m=>!m.out&&m.to===4).length;
 if(third.force||third.beforeFirst)return 0;return result.mv.filter(m=>!m.out&&m.to===4&&(m.at??Infinity)<third.at).length;
}
