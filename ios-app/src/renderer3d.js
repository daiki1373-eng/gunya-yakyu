import * as T from 'three';
import {mergeGeometries} from 'three/addons/utils/BufferGeometryUtils.js';
const Y=new T.Vector3(0,1,0),tmp=new T.Vector3(),matrix=new T.Matrix4();
const geo={sphere:new T.SphereGeometry(1,16,12),cylinder:new T.CylinderGeometry(1,1,1,12),box:new T.BoxGeometry(1,1,1)};
const materialCache=new Map();
const material=(color,extra={})=>{const key=JSON.stringify([color,extra]);if(!materialCache.has(key))materialCache.set(key,new T.MeshStandardMaterial({color,roughness:.78,metalness:0,...extra}));return materialCache.get(key)};
const materials={skin:material('#e9b38b'),white:material('#f1ead8'),dark:material('#172832'),wood:material('#bd8748'),leather:material('#8a512a'),grass:material('#417347'),soil:material('#b7986d'),chalk:material('#fff9e7'),concrete:material('#8e9d92'),fence:material('#184e39'),steel:material('#627c7c',{metalness:.65,roughness:.4})};
function mesh(g,m,parent,p=[0,0,0],scale=[1,1,1]){const o=new T.Mesh(g,m);o.position.set(...p);o.scale.set(...scale);o.castShadow=true;o.receiveShadow=true;parent.add(o);return o}
const sphere=(parent,m,p,s)=>mesh(geo.sphere,m,parent,p,s);
const box=(parent,m,p,s)=>mesh(geo.box,m,parent,p,s);
function rod(parent,m,a,b,r=.04){const o=mesh(geo.cylinder,m,parent);link(o,a,b,r);return o}
function link(o,a,b,r){const p=new T.Vector3(...a),q=new T.Vector3(...b),d=q.sub(p);o.position.copy(p.addScaledVector(d,.5));o.quaternion.setFromUnitVectors(Y,d.clone().normalize());o.scale.set(r,Math.max(.001,d.length()),r)}
function flatPlane(parent,m,width,height,x,z,y=.01){const o=new T.Mesh(new T.PlaneGeometry(width,height),m);o.rotation.x=-Math.PI/2;o.position.set(x,y,z);o.receiveShadow=true;parent.add(o);return o}
function texture(w,h,paint){const canvas=document.createElement('canvas');canvas.width=w;canvas.height=h;const c=canvas.getContext('2d');paint(c,w,h);const t=new T.CanvasTexture(canvas);t.colorSpace=T.SRGBColorSpace;t.anisotropy=4;return t}
function labelTexture(title,bg='#eee4c6',fg='#254d3e'){return texture(512,128,(c,w,h)=>{c.fillStyle=bg;c.fillRect(0,0,w,h);c.font='bold 64px sans-serif';c.fillStyle=fg;c.textAlign='center';c.textBaseline='middle';c.fillText(title,w/2,h/2,w-24)})}
function mergeStaticGroup(group){group.updateMatrixWorld(true);const inverse=group.matrixWorld.clone().invert(),batches=new Map(),remove=[];group.traverse(o=>{if(!o.isMesh||o.isInstancedMesh)return;const key=o.material.uuid+'-'+o.castShadow;if(!batches.has(key))batches.set(key,{mat:o.material,cast:o.castShadow,geometries:[]});const local=new T.Matrix4().multiplyMatrices(inverse,o.matrixWorld);batches.get(key).geometries.push(o.geometry.clone().applyMatrix4(local));remove.push(o)});for(const o of remove)o.removeFromParent();for(const batch of batches.values()){const m=new T.Mesh(mergeGeometries(batch.geometries),batch.mat);m.castShadow=batch.cast;group.add(m);for(const g of batch.geometries)g.dispose()}}
class Player{
 constructor(parent){
  this.root=new T.Group();parent.add(this.root);this.upper=new T.Group();this.root.add(this.upper);this.color=material('#216747').clone();this.accent=material('#fff5d7');
  this.jersey=new T.MeshStandardMaterial({color:'#f3efe2',roughness:.92});
  this.torso=mesh(new T.LatheGeometry([new T.Vector2(.19,-.5),new T.Vector2(.215,-.35),new T.Vector2(.24,0),new T.Vector2(.28,.3),new T.Vector2(.265,.4),new T.Vector2(.14,.5)],24),this.jersey,this.upper);
  this.belt=mesh(geo.cylinder,materials.dark,this.upper,[0,.5,0],[.19,.04,.19]);
  this.neck=mesh(geo.cylinder,materials.skin,this.upper,[0,1.2,0],[.09,.1,.09]);
  this.head=new T.Group();this.upper.add(this.head);sphere(this.head,materials.skin,[0,0,0],[.2,.235,.2]);
  this.face=new T.Group();this.head.add(this.face);for(const x of [-.085,.085]){sphere(this.face,materials.white,[x,.025,.205],[.048,.064,.025]);sphere(this.face,materials.dark,[x,.025,.229],[.023,.037,.015])}
  sphere(this.face,materials.skin,[0,-.025,.233],[.045,.05,.06]);rod(this.face,materials.dark,[-.055,-.115,.205],[.055,-.115,.205],.009);
  mesh(new T.SphereGeometry(1,24,12,0,Math.PI*2,0,Math.PI/2),this.color,this.head,[0,.14,-.015],[.215,.14,.21]);this.brim=mesh(geo.box,this.color,this.head,[0,.14,.2],[.38,.026,.25]);
  const emblem=new T.Mesh(new T.PlaneGeometry(.12,.1),new T.MeshBasicMaterial({map:labelTexture('G','#216747','#fff2c6'),transparent:false}));emblem.position.set(0,.22,.182);this.head.add(emblem);
  this.ears=new T.Group();this.head.add(this.ears);this.patches=new T.Group();this.face.add(this.patches);
  this.arms=[mesh(geo.cylinder,this.color,this.upper),mesh(geo.cylinder,this.color,this.upper)];this.forearms=[mesh(geo.cylinder,materials.skin,this.upper),mesh(geo.cylinder,materials.skin,this.upper)];
  this.hands=[sphere(this.upper,materials.skin,[0,0,0],[.07,.08,.07]),sphere(this.upper,materials.skin,[0,0,0],[.07,.08,.07])];
  this.glove=new T.Group();this.upper.add(this.glove);sphere(this.glove,materials.leather,[0,0,0],[.12,.14,.065]);for(let i=0;i<4;i++)rod(this.glove,materials.leather,[-.09+i*.055,-.02,.02],[-.09+i*.055,.14,.015],.024);
  this.legs=[mesh(geo.cylinder,this.accent,this.root),mesh(geo.cylinder,this.accent,this.root)];this.shins=[mesh(geo.cylinder,this.accent,this.root),mesh(geo.cylinder,this.accent,this.root)];this.shoes=[sphere(this.root,materials.dark,[0,0,0],[.105,.065,.19]),sphere(this.root,materials.dark,[0,0,0],[.105,.065,.19])];
  this.bat=new T.Group();this.upper.add(this.bat);rod(this.bat,materials.wood,[0,0,0],[0,1.02,0],.035);rod(this.bat,materials.dark,[0,-.08,0],[0,.2,0],.021);
  this.mask=new T.Group();this.head.add(this.mask);for(let i=0;i<4;i++)rod(this.mask,materials.steel,[-.2,-.15+i*.1,.24],[.2,-.15+i*.1,.24],.009);for(const x of [-.13,0,.13])rod(this.mask,materials.steel,[x,-.19,.25],[x,.18,.25],.009);
  mergeStaticGroup(this.glove);mergeStaticGroup(this.mask);this.mask.visible=false;this.mask.removeFromParent();this.face.traverse(o=>{if(o.isMesh)o.castShadow=false});mergeStaticGroup(this.head);this.head.add(this.mask);for(const o of [...this.forearms,...this.hands])o.castShadow=false;
  this.chest=box(this.upper,this.color,[0,.72,.2],[.36,.45,.09]);this.setSpecies('human');this.pose(0,'idle');
 }
 setTeam(color,secondary){this.color.color.set(color||'#276546');this.secondary=secondary;const key=color+'-'+(this.number||1);if(this.jerseyKey!==key){this.jerseyKey=key;this.jersey.map?.dispose();this.jersey.map=texture(512,512,(c,w,h)=>{c.fillStyle='#f1eddf';c.fillRect(0,0,w,h);c.strokeStyle='#8a9a8a30';c.lineWidth=1;for(let x=0;x<w;x+=22){c.beginPath();c.moveTo(x,0);c.lineTo(x,h);c.stroke()}c.fillStyle=color||'#276546';c.fillRect(0,0,w,24);c.font='bold 155px sans-serif';c.textAlign='center';c.fillText(String(this.number||1),w*.5,h*.72);c.fillText(String(this.number||1),w*.02,h*.72)});this.jersey.needsUpdate=true}}
 setSpecies(sp){if(sp===this.species)return;this.species=sp;this.ears.clear();this.patches.clear();
  if(sp==='dog')for(const x of [-.23,.23])sphere(this.ears,materials.leather,[x,-.02,-.01],[.07,.2,.09]);
  if(sp==='panda'){for(const x of [-.18,.18])sphere(this.ears,materials.dark,[x,.25,0],[.09,.09,.07]);for(const x of [-.085,.085])sphere(this.patches,materials.dark,[x,.026,.213],[.068,.09,.018])}
  if(sp==='beetle'){rod(this.ears,materials.leather,[0,.26,0],[0,.6,0],.035);rod(this.ears,materials.leather,[0,.6,0],[-.12,.73,0],.025);rod(this.ears,materials.leather,[0,.6,0],[.12,.73,0],.025)}
 }
 armsTo(left,right){for(let i=0;i<2;i++){const s=[i?.22:-.22,this.shoulder,0],h=i?right:left,e=[(s[0]+h[0])*.5,(s[1]+h[1])*.5,(s[2]+h[2])*.5+.08];link(this.arms[i],s,e,.075);link(this.forearms[i],e,h,.055);this.hands[i].position.set(...h)}this.glove.position.set(...left)}
 legsTo(knees,feet){for(let i=0;i<2;i++){const hip=[i?.12:-.12,.5,0];link(this.legs[i],hip,knees[i],.085);link(this.shins[i],knees[i],feet[i],.065);this.shoes[i].position.set(feet[i][0],feet[i][1]+.03,feet[i][2]+.055)}}
 body(height=.8){this.torso.position.y=.5+height*.5;this.torso.scale.set(1,height,1);this.neck.position.y=.5+height+.025;this.head.position.y=.5+height+.3;this.shoulder=.5+height-.08;this.chest.position.y=.5+height*.5;this.chest.scale.y=height*.7}
 pose(time,kind='idle',opts={}){
  this.root.rotation.set(0,opts.facing||0,0);this.root.position.y=0;this.upper.rotation.set(0,0,0);this.upper.position.set(0,0,0);this.face.visible=true;
  this.body(.78);this.bat.visible=kind==='bat';this.glove.visible=kind!=='bat'&&kind!=='run';this.mask.visible=kind==='catch';this.chest.visible=kind==='catch';
  const stride=kind==='run'?Math.sin(time*13)*.35:0;this.legsTo([[-.12,.28,stride],[.12,.28,-stride]],[[-.12,.025,stride*.6],[.12,.025,-stride*.6]]);
  this.armsTo([-.32,.9,-stride],[.32,.9,stride]);
  if(kind==='catch'){this.root.scale.setScalar(.92);this.upper.position.y=-.35;this.legsTo([[-.3,.2,.12],[.3,.2,.12]],[[-.32,.025,.12],[.32,.025,.12]]);this.armsTo([-.38,.72,.2],[.3,.72,.1])}else this.root.scale.setScalar(1);
  if(kind==='dive'){this.root.position.y=.6;this.root.rotation.z=-1.25;this.armsTo([-.42,1.55,.1],[.4,1.55,.1])}
  if(kind==='scoop'){this.upper.rotation.x=.6;this.upper.position.y=-.2;this.armsTo([-.18,.45,.5],[.2,.55,.35])}
  if(kind==='catch-high')this.armsTo([-.2,1.85,.15],[.25,1.5,.1]);
  if(kind==='throw'){const u=opts.u||0;this.upper.rotation.y=-.55+u*1.1;this.armsTo([-.35,1.2,.2],[.25+Math.cos(-1.2+u*2.2)*.42,1.25+Math.sin(-1.2+u*2.2)*.48,.15+u*.25])}
  if(kind==='bat'){
   const swing=opts.swing||0,bend=opts.bend||0;this.upper.rotation.z=bend*.35;this.upper.rotation.y=-.4+swing*2.8;
   if(opts.spin){this.root.rotation.y+=opts.spin*2*Math.PI;this.root.position.y=Math.abs(Math.sin(opts.spin*Math.PI))* .3}
   this.armsTo([.1,1.14,.25],[.22,1.22,.24]);this.bat.position.set(.16,1.2,.25);this.bat.rotation.set(.15,0,-.5-swing*2.5);this.bat.scale.setScalar(opts.giant?2.3:1);
  }
 }
 pitch(time,rig,form,handedness=1){
  this.pose(time,'idle');const unit=.027,height=rig.h*unit;this.body(height);
  this.root.position.y=rig.jump*unit;this.root.scale.set(handedness,1,1);const flip=/宙返り|逆立ち/.test(form.name||'');this.root.rotation.y=0;
  if(flip)this.root.rotation.z=rig.spin;else this.root.rotation.y+=rig.spin;
  this.root.rotation.x=(form.twist||0)*2*Math.PI*(rig.w*rig.w*(3-2*rig.w));this.upper.rotation.z=-rig.body;
  this.upper.position.y=.48;this.torso.position.y=height*.5;this.neck.position.y=height+.025;this.head.position.y=height+.3;this.shoulder=height-.08;this.belt.position.y=.02;
  this.armsTo([-.35,height-.2,.05],[rig.hand[0]*unit,-rig.hand[1]*unit,.05]);this.glove.position.set(-.35,height-.2,.05);
  this.legsTo([[-.12,.25,0],[rig.knee[0]*unit,-rig.knee[1]*unit,0]],[[-.18,.025,.05],[rig.foot[0]*unit,Math.max(.025,-rig.foot[1]*unit),.08]]);
 }
}
class Ballpark{
 constructor(parent){
  const grass=texture(256,256,(c,w,h)=>{c.fillStyle='#51804a';c.fillRect(0,0,w,h);for(let i=0;i<15000;i++){const x=(i*73)%w,y=(i*43+Math.floor(i/256)*19)%h;c.fillStyle=i%2?'#86a86655':'#254f3c44';c.fillRect(x,y,1,2)}});grass.wrapS=grass.wrapT=T.RepeatWrapping;grass.repeat.set(40,40);
  flatPlane(parent,new T.MeshStandardMaterial({map:grass,roughness:1}),340,340,0,-60);
  for(let i=0;i<12;i++)flatPlane(parent,material(i%2?'#4f7c47':'#6d9550',{transparent:true,opacity:.16}),12,180,-66+i*12,-50,.018);
  const soil=texture(256,256,(c,w,h)=>{c.fillStyle='#b69a73';c.fillRect(0,0,w,h);for(let i=0;i<10000;i++){c.fillStyle=i%2?'#755a3130':'#f1d5a337';c.fillRect((i*37)%w,(i*53+Math.floor(i/64))%h,2,1)}});soil.wrapS=soil.wrapT=T.RepeatWrapping;soil.repeat.set(10,10);
  const dirt=new T.Mesh(new T.CircleGeometry(37,64),new T.MeshStandardMaterial({map:soil,roughness:1}));dirt.rotation.x=-Math.PI/2;dirt.position.set(0,.025,-18);dirt.receiveShadow=true;parent.add(dirt);
  for(const p of [[19.4,-19.4],[0,-38.8],[-19.4,-19.4]])box(parent,materials.chalk,[p[0],.07,p[1]],[.55,.1,.55]);
  flatPlane(parent,materials.chalk,.65,.65,0,0,.055);for(const sign of [-1,1])rod(parent,materials.chalk,[0,.05,0],[sign*79,.05,-79],.065);
  for(const sign of [-1,1])for(const [a,b] of [[[-1,0],[1,0]],[[1,0],[1,2]],[[1,2],[-1,2]],[[-1,2],[-1,0]]])rod(parent,materials.chalk,[sign*1.35+a[0]*.42,.06,a[1]*.95-.7],[sign*1.35+b[0]*.42,.06,b[1]*.95-.7],.028);
  sphere(parent,materials.soil,[0,.05,-18.4],[2.8,.25,2]);box(parent,materials.chalk,[0,.28,-18.4],[.6,.05,.15]);
  // Original procedural stadium, no downloaded or copyrighted game imagery.
  const ring=new T.Group();parent.add(ring);const panelMat=material('#235b44');
  for(let i=0;i<32;i++){
   const angle=(-135+i*270/31)*Math.PI/180,r=111,x=Math.sin(angle)*r,z=-Math.cos(angle)*r-16;
   const stand=new T.Group();stand.position.set(x,0,z);stand.rotation.y=-angle;ring.add(stand);
   box(stand,panelMat,[0,2,0],[18,4,1]);
   for(let row=0;row<5;row++){const h=3+row*1.4;box(stand,materials.concrete,[0,h*.5,-2-row*2],[18,h,2]);box(stand,material(row%2?'#59766c':'#8b9b87'),[0,h+.05,-2-row*2],[18,.1,1.8])}
   box(stand,materials.concrete,[0,11,-12],[18,.45,4]);
   const sign=new T.Mesh(new T.PlaneGeometry(10,1.7),new T.MeshBasicMaterial({map:labelTexture(['GUNYA','青春の一球','若葉スポーツ','FIELD 97'][i%4],i%2?'#173d50':'#ede5c9',i%2?'#f8df9f':'#315640')}));sign.position.set(0,2.4,.55);stand.add(sign);
  }
  const colors=['#f1e5ca','#2a4760','#d98452','#a6c4bf','#d9b763'],crowdCount=2400,bodyGeo=new T.BoxGeometry(.42,.55,.3),crowd=new T.InstancedMesh(bodyGeo,material('#fff'),crowdCount);
  for(let i=0;i<crowdCount;i++){const angle=(-133+(i%120)*266/119)*Math.PI/180,row=Math.floor(Math.floor(i/120)/4),sub=Math.floor(i/120)%4,r=112.5+row*2+sub*.25;matrix.makeTranslation(Math.sin(angle)*r,3.4+row*1.4+sub*.07,-Math.cos(angle)*r-16);crowd.setMatrixAt(i,matrix);crowd.setColorAt(i,new T.Color(colors[(i*7+Math.floor(i/17))%colors.length]))}parent.add(crowd);
  for(const angle of [-1.05,1.05,-2.15,2.15]){const x=Math.sin(angle)*125,z=-Math.cos(angle)*125-16;rod(parent,materials.steel,[x,0,z],[x,32,z],.28);box(parent,materials.steel,[x,32,z],[7,3,.5]);for(let n=0;n<12;n++)sphere(parent,material('#eaf3e2',{emissive:'#eaf3e2',emissiveIntensity:.2}),[x-2.7+(n%4)*1.8,31.2+Math.floor(n/4)*.8,z+.35],[.4,.3,.2])}
  for(const x of [-9,9])rod(parent,materials.steel,[x,0,6],[x,10,6],.07);
  const wires=[];for(let x=-9;x<=9;x+=.6)wires.push(x,0,6,x,10,6);for(let y=0;y<=10;y+=.6)wires.push(-9,y,6,9,y,6);const netGeo=new T.BufferGeometry();netGeo.setAttribute('position',new T.Float32BufferAttribute(wires,3));parent.add(new T.LineSegments(netGeo,new T.LineBasicMaterial({color:'#627d77',transparent:true,opacity:.28})));
  this.scoreCanvas=document.createElement('canvas');this.scoreCanvas.width=1024;this.scoreCanvas.height=512;this.scoreTexture=new T.CanvasTexture(this.scoreCanvas);this.scoreTexture.colorSpace=T.SRGBColorSpace;
  box(parent,materials.dark,[0,17,-126],[28,15,1]);const score=new T.Mesh(new T.PlaneGeometry(27,13.5),new T.MeshBasicMaterial({map:this.scoreTexture}));score.position.set(0,17,-125.4);parent.add(score);this.updateScore(null);
 }
 updateScore(G){const key=G?JSON.stringify([G.sc,G.inn,G.top,G.line]):'title';if(this.scoreKey===key)return;this.scoreKey=key;const c=this.scoreCanvas.getContext('2d');c.fillStyle='#12372e';c.fillRect(0,0,1024,512);c.fillStyle='#ead99e';c.textAlign='center';c.font='bold 52px sans-serif';c.fillText('ぐにゃ野球',512,75);c.font='32px monospace';c.fillText('TEAM    1  2  3  4  5    R',512,157);
  const away=G?(G.myTop?'me':'you'):null,home=G?(G.myTop?'you':'me'):null;[away,home].forEach((k,i)=>{const y=278+i*132;c.textAlign='left';c.font='bold 39px sans-serif';c.fillText(k?G[k].short:i?'若葉':'青空',35,y);c.textAlign='center';c.font='46px monospace';for(let j=0;j<5;j++)c.fillText(k?(G.line[k][j]??'·'):'·',370+j*103,y);c.fillStyle='#ffe182';c.fillText(k?G.sc[k]:0,953,y);c.fillStyle='#ead99e'});this.scoreTexture.needsUpdate=true}
}
export function createRenderer(canvas){
 const renderer=new T.WebGLRenderer({canvas,antialias:true,alpha:false,powerPreference:'high-performance'});renderer.setSize(960,540,false);renderer.setPixelRatio(Math.min(devicePixelRatio||1,1.5));renderer.shadowMap.enabled=true;renderer.shadowMap.type=T.PCFSoftShadowMap;renderer.outputColorSpace=T.SRGBColorSpace;renderer.toneMapping=T.ACESFilmicToneMapping;renderer.toneMappingExposure=1.12;
 const scene=new T.Scene();scene.background=new T.Color('#a8d4e7');scene.fog=new T.Fog('#b9d9d8',100,260);scene.add(new T.HemisphereLight('#eaf6ff','#7c6a47',2));
 const sky=new T.Mesh(new T.SphereGeometry(240,24,16),new T.ShaderMaterial({side:T.BackSide,depthWrite:false,vertexShader:'varying vec3 vDir; void main(){vDir=position;gl_Position=projectionMatrix*modelViewMatrix*vec4(position,1.0);}',fragmentShader:'varying vec3 vDir;void main(){float h=max(0.0,normalize(vDir).y);vec3 c=mix(vec3(.76,.86,.86),vec3(.25,.53,.74),pow(h,.42));gl_FragColor=vec4(c,1.0);}'}));scene.add(sky);
 const clouds=new T.InstancedMesh(geo.sphere,material('#eef4ef'),24);for(let i=0;i<24;i++){const a=i*.72,r=135+(i%3)*17;matrix.compose(new T.Vector3(Math.sin(a)*r,30+(i%4)*7,-Math.cos(a)*r-15),new T.Quaternion(),new T.Vector3(10+i%4*3,2+i%2,5+i%3));clouds.setMatrixAt(i,matrix)}scene.add(clouds);
 const sun=new T.DirectionalLight('#fff1d4',3.2);sun.position.set(-30,65,22);sun.castShadow=true;sun.shadow.mapSize.set(2048,2048);Object.assign(sun.shadow.camera,{left:-45,right:45,top:75,bottom:-25,near:1,far:200});sun.shadow.normalBias=.04;sun.shadow.bias=-.0002;scene.add(sun);scene.add(sun.target);
 const park=new Ballpark(scene);scene.updateMatrixWorld(true);const batches=new Map(),remove=[];
 scene.traverse(o=>{if(!o.isMesh||o.isInstancedMesh)return;const key=o.material.uuid+'-'+o.castShadow+'-'+o.receiveShadow;if(!batches.has(key))batches.set(key,{mat:o.material,cast:o.castShadow,receive:o.receiveShadow,geometries:[]});batches.get(key).geometries.push(o.geometry.clone().applyMatrix4(o.matrixWorld));remove.push(o)});
 for(const o of remove)o.removeFromParent();for(const batch of batches.values()){const g=mergeGeometries(batch.geometries);if(g){const m=new T.Mesh(g,batch.mat);m.castShadow=batch.cast;m.receiveShadow=batch.receive;scene.add(m)}for(const g of batch.geometries)g.dispose()}
 const camera=new T.PerspectiveCamera(54,960/540,.08,400),players=new Map(),runners=[],bat=new Player(scene),ball=mesh(geo.sphere,materials.chalk,scene,[0,1,0],[.045,.045,.045]);const ballShadow=flatPlane(scene,new T.MeshBasicMaterial({color:'#20352b',transparent:true,opacity:.22}),.3,.3,0,0,.06);const target=new T.Vector3();let ready=false,lastMode='title';
 const get=(pos)=>{if(!players.has(pos))players.set(pos,new Player(scene));return players.get(pos)};
 function put(player,position,color,secondary,sp,time,kind,options={}){player.root.visible=true;player.setTeam(color,secondary);player.setSpecies(sp||'human');player.pose(time,kind,options);player.root.position.x=position[0];player.root.position.z=-position[1]}
 function render(state){
  const {G,V,FP,pitchRig,pitchAppearance,pathPos}=state,t=state.t,mode=G?(V.mode==='field'?'field':'bat'):'title',cut=mode!==lastMode;lastMode=mode;camera.fov=mode==='field'?42:54;camera.updateProjectionMatrix();park.updateScore(G);for(const p of players.values())p.root.visible=false;for(const p of runners)p.root.visible=false;bat.root.visible=false;ball.visible=false;ballShadow.visible=false;
  if(G){const offKey=G.top===G.myTop?'me':'you',off=G[offKey],def=G[offKey==='me'?'you':'me'];
   if(V.mode==='field'&&V.f){
    const f=V.f;for(let pos=1;pos<=9;pos++){const person=def.lineup.find(p=>p.pos===pos),p=get(pos);p.number=person?.num||pos;put(p,f.fl[pos],def.color,def.color2,person?.sp,t,f.dive===pos?'dive':'idle');const motion=f.motion?.[pos];if(motion){p.pose(t,motion.kind||'run',{facing:motion.facing||0,u:motion.u||0})}}
    f.runners.forEach((r,i)=>{if(!r.vis)return;const p=runners[i]||(runners[i]=new Player(scene)),point=pathPos(r.a,r.b,r.u);put(p,point,off.color,off.color2,r.id.sp,t,Math.abs(r.a-r.b)>.001&&r.u<1?'run':'idle',{facing:r.facing||Math.atan2((r.b>=r.a?1:-1),0)})});
    if(f.ball.vis){ball.visible=true;ball.position.set(f.ball.x,Math.max(.12,f.ball.z),-f.ball.y);ballShadow.visible=true;ballShadow.position.set(f.ball.x,.065,-f.ball.y)}
    target.set((f.ball.x||0)*.25,0,-18-(f.ball.y||0)*.25);camera.position.lerp(new T.Vector3((f.ball.x||0)*.2,30,26-(f.ball.y||0)*.15),cut?1:ready?.08:1);camera.lookAt(target);
   }else{
    for(let pos=1;pos<=9;pos++){const person=def.lineup.find(p=>p.pos===pos),p=get(pos);p.number=person?.num||pos;put(p,FP[pos],def.color,def.color2,person?.sp,t,pos===2?'catch':'idle',{facing:pos===2?Math.PI:0})}
    const pitcher=get(1),a=pitchAppearance(),pi=V.pit,u=pi?Math.max(0,Math.min(1.25,(state.now-pi.t0)/pi.wind)):0;pitcher.pitch(t,pitchRig(u,a.form),a.form,pi?.sd||(a.pt?.thr==='L'?-1:1));pitcher.root.position.set(0,pitcher.root.position.y,-18.4);
    if(V.bat){const s=V.bat,elapsed=state.now-s.t0,swing=s.pose==='swing'?Math.max(0,Math.min(1,elapsed/180)):0,bend=s.bendT&&state.now-s.bendT<480?-Math.sin((state.now-s.bendT)/480*Math.PI)*.85:0;bat.number=off.lineup[G.idx[offKey]]?.num||1;put(bat,[s.side==='R'?-1.4:1.4,0],off.color,off.color2,s.sp,t,'bat',{facing:s.side==='R'?-Math.PI/2:Math.PI/2,swing,bend,spin:G.fx==='spin'&&s.pose==='swing'?elapsed/230:0,giant:G.fx==='giant'||G.arm[s.mine?'me':'you']==='giant'});if(s.twistT&&state.now-s.twistT<420)bat.root.rotation.z=Math.sin((state.now-s.twistT)/420*Math.PI)*Math.PI*2;}
    if(pi&&!pi.hit){const q=(state.now-pi.t0-pi.wind)/(pi.p.T*1000);if(q>=0&&q<=1.15&&!(pi.p.sp==='vanish'&&q>.3&&q<.72)){const hand=pitcher.hands[1];hand.updateWorldMatrix(true,false);const origin=hand.getWorldPosition(new T.Vector3()),u=Math.min(1,q)**2,curve=pi.p.sp==='boomerang'&&q>.5?-pi.p.bx*.85:pi.p.bx;ball.visible=true;ball.position.set(origin.x*(1-u)+pi.p.cx*.28*u+curve*Math.sin(Math.PI*u)*.9,origin.y*(1-u)+(1.1+pi.p.cy*.25)*u+pi.p.by*Math.sin(Math.PI*u)*.22,origin.z*(1-u)+.1*u)}}
    const right=!V.bat||V.bat.side==='R';camera.position.lerp(new T.Vector3(right?.1:-.1,1.85,3.8),cut?1:ready?.14:1);camera.lookAt(0,1.02,-18);
   }
  }else{for(let i=1;i<=9;i++){const p=get(i);put(p,FP[i],'#276a49','#fff','human',t,'idle')}camera.position.set(-28,18,35);camera.lookAt(0,1,-20)}
  ready=true;renderer.render(scene,camera);return true;
 }
 return{render,dispose:()=>renderer.dispose(),info:()=>({calls:renderer.info.render.calls,triangles:renderer.info.render.triangles,webgl:renderer.capabilities.isWebGL2!==false})};
}
