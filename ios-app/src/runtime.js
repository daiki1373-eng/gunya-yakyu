import {Capacitor} from '@capacitor/core';
import {App} from '@capacitor/app';
import {Haptics,ImpactStyle,NotificationType} from '@capacitor/haptics';
import {Preferences} from '@capacitor/preferences';
import {createRenderer} from './renderer3d.js';
import {LivePlay,validRuns,basePosition} from './live-play.js';
window.BaseballPlay={LivePlay,validRuns,basePosition};
window.Gunya3D={instance:null,error:null,init(canvas){try{this.instance=createRenderer(canvas);canvas.addEventListener('webglcontextlost',e=>{e.preventDefault();this.error='context-lost';this.instance=null;canvas.hidden=true;document.getElementById('cv').hidden=false});return true}catch(e){this.error=String(e);return false}},render(state){return this.instance?.render(state)||false}};
window.NativeGame={
 native:Capacitor.isNativePlatform(),
 queue:Promise.resolve(),
 async init(hooks){
  document.addEventListener('visibilitychange',()=>{if(document.hidden)hooks.pause('background')});
  if(this.native)await App.addListener('appStateChange',({isActive})=>{if(!isActive)hooks.pause('background')});
 },
 async restore(key){if(!this.native)return;try{const {value}=await Preferences.get({key});if(!value)return;const saved=JSON.parse(value);if(!saved.data||!saved.at)return;const local=localStorage.getItem(key),at=Number(localStorage.getItem(key+'.at')||0);let valid=false;try{valid=!!JSON.parse(local)?.hero}catch{}if(!valid||saved.at>at){localStorage.setItem(key,JSON.stringify(saved.data));localStorage.setItem(key+'.at',String(saved.at))}}catch(e){console.warn('Native save restore unavailable',e)}},
 persist(key,data){if(!this.native)return;const at=Date.now();localStorage.setItem(key+'.at',String(at));const value=JSON.stringify({at,data});this.queue=this.queue.catch(()=>{}).then(()=>Preferences.set({key,value})).catch(e=>console.warn('Native save backup unavailable',e));},
 impact(kind='hit'){if(!this.native)return;const p=kind==='hr'?Haptics.notification({type:NotificationType.Success}):Haptics.impact({style:ImpactStyle.Medium});p.catch(()=>{})}
};
