import * as T from 'three';
import {mergeGeometries} from 'three/addons/utils/BufferGeometryUtils.js';
import {Player} from './characters.js';
import {Ballpark} from './ballpark.js';

const clamp=(n,a,b)=>Math.max(a,Math.min(b,n));
const turn=(from,to,amount)=>from+Math.atan2(Math.sin(to-from),Math.cos(to-from))*amount;
const ease=(rate,dt)=>1-Math.exp(-rate*dt);

// Batch compatible scenery; differently attributed shapes must remain visible.
function batchScenery(scene){
 scene.updateMatrixWorld(true);const batches=new Map();
 scene.traverse(o=>{
  if(!o.isMesh||o.isInstancedMesh||o.material.isShaderMaterial||o.userData.dynamic)return;
  const signature=Object.entries(o.geometry.attributes).map(([k,a])=>k+':'+a.itemSize).sort().join(',');
  const key=[o.material.uuid,o.castShadow,o.receiveShadow,!!o.geometry.index,signature].join('|');
  if(!batches.has(key))batches.set(key,[]);batches.get(key).push(o);
 });
 for(const meshes of batches.values()){
  if(meshes.length<2)continue;
  const geometries=meshes.map(o=>o.geometry.clone().applyMatrix4(o.matrixWorld)),geometry=mergeGeometries(geometries);
  if(geometry){const first=meshes[0],mesh=new T.Mesh(geometry,first.material);mesh.castShadow=first.castShadow;mesh.receiveShadow=first.receiveShadow;scene.add(mesh);for(const o of meshes)o.removeFromParent()}
  for(const g of geometries)g.dispose();
 }
}

function shadowTexture(){
 const c=document.createElement('canvas');c.width=c.height=64;const x=c.getContext('2d'),g=x.createRadialGradient(32,32,0,32,32,32);
 g.addColorStop(0,'rgba(23,43,32,.4)');g.addColorStop(.45,'rgba(23,43,32,.2)');g.addColorStop(1,'rgba(23,43,32,0)');x.fillStyle=g;x.fillRect(0,0,64,64);return new T.CanvasTexture(c);
}

export function createRenderer(canvas){
 const renderer=new T.WebGLRenderer({canvas,antialias:true,alpha:false,powerPreference:'high-performance'});
 renderer.setSize(960,540,false);renderer.setPixelRatio(Math.min(devicePixelRatio||1,1.5));renderer.shadowMap.enabled=true;renderer.shadowMap.type=T.PCFSoftShadowMap;
 renderer.outputColorSpace=T.SRGBColorSpace;renderer.toneMapping=T.ACESFilmicToneMapping;renderer.toneMappingExposure=1.03;
 const scene=new T.Scene();scene.background=new T.Color('#b9dbdf');scene.fog=new T.Fog('#c3ded8',125,330);
 scene.add(new T.HemisphereLight('#edf7ff','#899e66',1.9));
 const sky=new T.Mesh(new T.SphereGeometry(360,24,16),new T.ShaderMaterial({side:T.BackSide,depthWrite:false,
  vertexShader:'varying vec3 vDir;void main(){vDir=position;gl_Position=projectionMatrix*modelViewMatrix*vec4(position,1.);}',
  fragmentShader:'varying vec3 vDir;void main(){float h=max(0.,normalize(vDir).y);gl_FragColor=vec4(mix(vec3(.82,.89,.82),vec3(.35,.65,.82),pow(h,.55)),1.);}'}));scene.add(sky);
 const cloudGeometry=new T.SphereGeometry(1,12,8),cloudMaterial=new T.MeshStandardMaterial({color:'#fff9ea',roughness:1});
 const clouds=new T.InstancedMesh(cloudGeometry,cloudMaterial,36),matrix=new T.Matrix4();
 for(let i=0;i<36;i++){const cluster=Math.floor(i/3),a=cluster*.73,r=180+(cluster%3)*14;matrix.compose(new T.Vector3(Math.sin(a)*r+(i%3-1)*7,37+(cluster%4)*6+(i%3===1?2:0),-Math.cos(a)*r-35),new T.Quaternion(),new T.Vector3(9,2.5+(i%3),5));clouds.setMatrixAt(i,matrix)}scene.add(clouds);
 const sun=new T.DirectionalLight('#fff0cd',2.5);sun.position.set(-36,58,28);sun.castShadow=true;sun.shadow.mapSize.set(2048,2048);
 Object.assign(sun.shadow.camera,{left:-48,right:48,top:88,bottom:-24,near:1,far:200});sun.shadow.normalBias=.035;sun.shadow.bias=-.0002;scene.add(sun,sun.target);
 const park=new Ballpark(scene);batchScenery(scene);
 const camera=new T.PerspectiveCamera(50,960/540,.08,500),players=new Map(),runners=[],batter=new Player(scene);
 const ball=new T.Mesh(new T.SphereGeometry(.068,16,12),new T.MeshStandardMaterial({color:'#fff8df',roughness:.65}));ball.castShadow=true;scene.add(ball);
 const ballShadow=new T.Mesh(new T.PlaneGeometry(.4,.4),new T.MeshBasicMaterial({map:shadowTexture(),transparent:true,depthWrite:false}));ballShadow.rotation.x=-Math.PI/2;scene.add(ballShadow);
 const activeRing=new T.Mesh(new T.RingGeometry(.58,.66,36),new T.MeshBasicMaterial({color:'#ffe5a3',transparent:true,opacity:.7,depthWrite:false}));activeRing.rotation.x=-Math.PI/2;scene.add(activeRing);
 const cameraTarget=new T.Vector3(),desiredTarget=new T.Vector3(),desiredCamera=new T.Vector3(),release=new WeakMap();
 let ready=false,lastMode='title',lastTime=null;
 const get=pos=>{if(!players.has(pos))players.set(pos,new Player(scene));return players.get(pos)};

 function place(player,position,color,secondary,species,time,kind,options,dt){
  const x=position[0],z=-position[1],old=player.locomotion,dist=old?Math.hypot(x-old.x,z-old.z):0;
  // Gait follows actual travel rather than a clock shared by every player.
  const moved=old&&dt>0&&dist<4,actualSpeed=moved?dist/dt:0;
  const phase=(old?.phase||0)+(moved?dist*4.8:0),desiredFacing=options.facing??0;
  const facing=old&&kind==='run'?turn(old.facing,desiredFacing,ease(14,dt)):desiredFacing;
  player.locomotion={x,z,phase,facing};player.root.visible=true;player.setTeam(color,secondary);player.setSpecies(species||'human');
  player.pose(time,kind,{...options,facing,phase,speed:kind==='run'?clamp(options.speed??actualSpeed,0,12):0});player.root.position.x=x;player.root.position.z=z;
 }

 function render(state){
  const {G,V,FP,pitchRig,pitchAppearance,pathPos}=state,t=state.t,now=state.now??t*1000;
  const dt=lastTime===null?1/60:clamp((now-lastTime)/1000,0,.1);lastTime=now;
  const mode=G?(V.mode==='field'?'field':'bat'):'title',cut=mode!==lastMode;lastMode=mode;
  const fov=mode==='field'?40:50;if(camera.fov!==fov){camera.fov=fov;camera.updateProjectionMatrix()}
  park.updateScore(G);park.update?.(t,G);
  for(const p of players.values())p.root.visible=false;for(const p of runners)p.root.visible=false;batter.root.visible=false;ball.visible=false;ballShadow.visible=false;activeRing.visible=false;ball.scale.setScalar(mode==='field'?1.65:1);
  if(G){
   const offKey=G.top===G.myTop?'me':'you',off=G[offKey],def=G[offKey==='me'?'you':'me'];
   if(V.mode==='field'&&V.f){
    const f=V.f;
    for(let pos=1;pos<=9;pos++){
     const person=def.lineup.find(p=>p.pos===pos),p=get(pos),motion=f.motion?.[pos];p.number=person?.num||pos;
     place(p,f.fl[pos],def.color,def.color2,person?.sp,t,f.dive===pos?'dive':motion?.kind||'idle',{...motion,facing:motion?.facing??Math.atan2(-f.fl[pos][0],f.fl[pos][1])},dt);
    }
    f.runners.forEach((r,i)=>{
     if(!r.vis)return;const p=runners[i]||(runners[i]=new Player(scene)),point=pathPos(r.a,r.b,r.u);
     const moving=r.moving??(Math.abs(r.a-r.b)>.001&&r.u<1);p.number=r.id.num||i+1;
     place(p,point,off.color,off.color2,r.id.sp,t,moving?'run':'idle',{facing:r.facing??0,speed:r.speed},dt);
    });
    if(f.ball.vis){ball.visible=true;ball.position.set(f.ball.x,Math.max(.09,f.ball.z),-f.ball.y);ball.rotation.x=t*15;ballShadow.visible=true;ballShadow.position.set(f.ball.x,.075,-f.ball.y);const scale=1+Math.min(2,f.ball.z*.08);ballShadow.scale.setScalar(scale);ballShadow.material.opacity=1/(1+f.ball.z*.09)}
    if(f.active&&f.fl[f.active]){activeRing.visible=true;activeRing.position.set(f.fl[f.active][0],.095,-f.fl[f.active][1])}
    const focus=f.focus||[(f.ball.x||0)*.5,19+(f.ball.y||0)*.23];
    desiredTarget.set(clamp(focus[0],-52,52),.6,-clamp(focus[1],15,76));
    desiredCamera.set(desiredTarget.x+5,27,desiredTarget.z+39);
   }else{
    for(let pos=1;pos<=9;pos++){
     const person=def.lineup.find(p=>p.pos===pos),p=get(pos);p.number=person?.num||pos;
     place(p,FP[pos],def.color,def.color2,person?.sp,t,pos===2?'catch':'idle',{facing:pos===2?Math.PI:Math.atan2(-FP[pos][0],FP[pos][1])},dt);
    }
    const pitcher=get(1),appearance=pitchAppearance(),pi=V.pit,progress=pi?clamp((now-pi.t0)/pi.wind,0,1.8):0,handedness=pi?.sd||(appearance.pt?.thr==='L'?-1:1);
    // A moving follow-through hand must not drag an airborne ball.
    if(pi&&!release.has(pi)&&progress>=1){pitcher.pitch(t,pitchRig(1,appearance.form),appearance.form,handedness,{progress:1});pitcher.root.position.set(0,pitcher.root.position.y,-18.4);pitcher.root.updateMatrixWorld(true);release.set(pi,pitcher.hands[1].getWorldPosition(new T.Vector3()))}
    pitcher.pitch(t,pitchRig(Math.min(1,progress),appearance.form),appearance.form,handedness,{progress});pitcher.root.position.set(0,pitcher.root.position.y,-18.4);
    if(V.bat){
     const s=V.bat,elapsed=Math.max(0,now-s.t0),swing=s.pose==='swing'?clamp(elapsed/180,0,1):0;
     const bend=s.bendT&&now-s.bendT<620?-Math.sin((now-s.bendT)/620*Math.PI)*.55:0;
     batter.number=off.lineup[G.idx[offKey]]?.num||1;
     place(batter,[s.side==='R'?-1.25:1.25,0],off.color,off.color2,s.sp,t,'bat',{facing:s.side==='R'?-Math.PI/2:Math.PI/2,swing,bend,spin:G.fx==='spin'&&s.pose==='swing'?Math.min(1,elapsed/480):0,giant:G.fx==='giant'||G.arm[s.mine?'me':'you']==='giant'},dt);
     if(s.twistT&&now-s.twistT<560){const u=(now-s.twistT)/560;batter.upper.rotation.z+=Math.sin(u*Math.PI*2)*.18*Math.sin(u*Math.PI)}
    }
    if(pi&&!pi.hit){
     const q=(now-pi.t0-pi.wind)/(pi.p.T*1000),origin=release.get(pi);
     if(origin&&q>=0&&q<=1.15&&!(pi.p.sp==='vanish'&&q>.3&&q<.72)){
      const u=Math.min(1,q),curve=pi.p.sp==='boomerang'&&q>.5?-pi.p.bx*.85:pi.p.bx;
      ball.visible=true;ball.position.set(T.MathUtils.lerp(origin.x,pi.p.cx*.28,u)+curve*Math.sin(Math.PI*u)*.45,T.MathUtils.lerp(origin.y,1.02+pi.p.cy*.25,u)+(.15+pi.p.by*.13)*Math.sin(Math.PI*u),T.MathUtils.lerp(origin.z,.1,u));ball.rotation.x=t*21;
     }
    }
    const right=!V.bat||V.bat.side==='R';desiredCamera.set(right?.15:-.15,2.35,5.5);desiredTarget.set(0,1.12,-17.8);
   }
  }else{
   for(let i=1;i<=9;i++){const p=get(i);p.number=i;place(p,FP[i],'#276a49','#f6edd8',['human','panda','dog','beetle'][i%4],t,'idle',{facing:Math.atan2(-FP[i][0],FP[i][1])},dt)}
   desiredCamera.set(-30+Math.sin(t*.07)*2,19,34);desiredTarget.set(0,1,-23);
  }
  const amount=!ready||cut?1:ease(mode==='field'?3.8:6,dt);camera.position.lerp(desiredCamera,amount);cameraTarget.lerp(desiredTarget,amount);camera.lookAt(cameraTarget);
  ready=true;renderer.render(scene,camera);return true;
 }
 return{render,dispose:()=>{scene.traverse(o=>{o.geometry?.dispose();if(o.material){for(const m of Array.isArray(o.material)?o.material:[o.material]){m.map?.dispose();m.dispose()}}});renderer.dispose()},info:()=>({calls:renderer.info.render.calls,triangles:renderer.info.render.triangles,webgl:renderer.capabilities.isWebGL2!==false})};
}
