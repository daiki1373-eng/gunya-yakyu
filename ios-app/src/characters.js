import * as T from 'three';
import {mergeGeometries} from 'three/addons/utils/BufferGeometryUtils.js';

// A deliberately handmade cast: baseball mechanics first, floppy ears second.
const UP = new T.Vector3(0, 1, 0);
const sphereGeometry = new T.SphereGeometry(1, 12, 9);
const cylinderGeometry = new T.CylinderGeometry(1, 1, 1, 10);
const boxGeometry = new T.BoxGeometry(1, 1, 1);
const palettes = new Map(), numberMaterials = new Map();
const clamp = (n, lo = 0, hi = 1) => Math.min(hi, Math.max(lo, n));
const smooth = n => { const t = clamp(n); return t * t * (3 - 2 * t); };
const lerp = T.MathUtils.lerp;
const blend = (a, b, u) => a.map((n, i) => lerp(n, b[i], u));

function mat(color, extra = {}) {
  const key = color + JSON.stringify(extra);
  if (!palettes.has(key)) palettes.set(key, new T.MeshStandardMaterial({color, roughness: .88, ...extra}));
  return palettes.get(key);
}
const M = {
  skin: mat('#edb685'), cream: mat('#fff1cf'), white: mat('#fff9e9'), ink: mat('#23363b'),
  fur: mat('#c48a53'), ear: mat('#8d563b'), pink: mat('#d28b75'), wood: mat('#bc8144'),
  glove: mat('#b56e38'), gloveDark: mat('#77492c'), horn: mat('#735039'), steel: mat('#72918d'),
  socks: mat('#f5eee1'), sole: mat('#ddd4b6')
};

function addMesh(parent, geometry, material, p = [0, 0, 0], s = [1, 1, 1], shadow = false) {
  const mesh = new T.Mesh(geometry, material);
  mesh.position.set(...p); mesh.scale.set(...s); mesh.castShadow = shadow; mesh.receiveShadow = true;
  parent.add(mesh); return mesh;
}
const ball = (p, m, xyz, s, shadow = false) => addMesh(p, sphereGeometry, m, xyz, s, shadow);
const cube = (p, m, xyz, s, shadow = false) => addMesh(p, boxGeometry, m, xyz, s, shadow);
function segment(parent, material, radius, shadow = false) {
  const mesh = addMesh(parent, cylinderGeometry, material, [0, 0, 0], [radius, 1, radius], shadow);
  mesh.userData.radius = radius; return mesh;
}
const vA = new T.Vector3(), vB = new T.Vector3(), vD = new T.Vector3();
const visibleBounds = new T.Box3(), partBounds = new T.Box3();
function link(mesh, a, b, radius = mesh.userData.radius || .05) {
  vA.set(...a); vB.set(...b); vD.subVectors(vB, vA);
  mesh.position.copy(vA).addScaledVector(vD, .5);
  mesh.quaternion.setFromUnitVectors(UP, vD.normalize());
  mesh.scale.set(radius, Math.max(.001, vA.distanceTo(vB)), radius);
}
function rod(parent, material, a, b, r) { const m = segment(parent, material, r); link(m, a, b); return m; }

// Merge only the supplied rigid part, never its animated children.
function mergePart(group) {
  group.updateMatrixWorld(true);
  const inverse = group.matrixWorld.clone().invert(), batches = new Map(), remove = [];
  group.traverse(o => {
    if (!o.isMesh) return;
    const key = o.material.uuid + '/' + o.castShadow;
    if (!batches.has(key)) batches.set(key, {material: o.material, shadow: o.castShadow, geometries: []});
    const transform = new T.Matrix4().multiplyMatrices(inverse, o.matrixWorld);
    batches.get(key).geometries.push(o.geometry.clone().applyMatrix4(transform)); remove.push(o);
  });
  remove.forEach(o => o.removeFromParent());
  for (const batch of batches.values()) {
    const merged = mergeGeometries(batch.geometries);
    if (merged) addMesh(group, merged, batch.material, undefined, undefined, batch.shadow);
    batch.geometries.forEach(g => g.dispose());
  }
}

// Analytic two-bone IK. The wrist/ankle is constrained to the limb's reach.
function joint(origin, goal, upper, lower, bend) {
  const a = new T.Vector3(...origin), target = new T.Vector3(...goal), direction = target.clone().sub(a);
  const distance = clamp(direction.length(), .045, upper + lower - .002);
  direction.normalize(); target.copy(a).addScaledVector(direction, distance);
  const along = (upper * upper - lower * lower + distance * distance) / (2 * distance);
  const perpendicular = new T.Vector3(...bend).addScaledVector(direction, -new T.Vector3(...bend).dot(direction)).normalize();
  if (perpendicular.lengthSq() < .01) perpendicular.set(0, 0, 1);
  const middle = a.clone().addScaledVector(direction, along).addScaledVector(perpendicular, Math.sqrt(Math.max(0, upper * upper - along * along)));
  return [middle.toArray(), target.toArray()];
}

function numberMaterial(color, number) {
  const key = color + '/' + number;
  if (!numberMaterials.has(key)) {
    const canvas = document.createElement('canvas'); canvas.width = canvas.height = 128;
    const c = canvas.getContext('2d');
    c.fillStyle = color; c.textAlign = 'center'; c.textBaseline = 'middle'; c.font = '900 98px sans-serif';
    c.fillText(String(number), 64, 68, 118);
    const map = new T.CanvasTexture(canvas); map.colorSpace = T.SRGBColorSpace;
    numberMaterials.set(key, new T.MeshStandardMaterial({map, transparent: true, depthWrite: false, roughness: 1}));
  }
  return numberMaterials.get(key);
}

export class Player {
  constructor(parent) {
    this.root = new T.Group(); parent.add(this.root);
    this.upperPivot = new T.Group(); this.root.add(this.upperPivot);
    this.upper = new T.Group(); this.upperPivot.add(this.upper);
    this.color = mat('#28634e').clone();
    this.phaseOffset = 0; this.number = 1;

    const bodyGeometry = new T.LatheGeometry([
      new T.Vector2(.17, -.25), new T.Vector2(.205, -.19), new T.Vector2(.24, .02),
      new T.Vector2(.255, .18), new T.Vector2(.21, .24), new T.Vector2(.12, .27)
    ], 16);
    this.torso = addMesh(this.upper, bodyGeometry, M.cream, [0, 1, 0], [1, 1, .85], true);
    this.belt = addMesh(this.upper, cylinderGeometry, M.ink, [0, .75, 0], [.18, .035, .16]);
    this.neck = ball(this.upper, M.skin, [0, 1.28, 0], [.085, .1, .085]);
    this.jerseyBadge = addMesh(this.upper, new T.PlaneGeometry(.19, .19), M.cream, [0, 1.02, .209]);
    // Two seams make the uniform read like fabric without relying on a face-wrapped texture.
    this.shirtDetails = new T.Group(); this.upper.add(this.shirtDetails);
    rod(this.shirtDetails, this.color, [0, .81, .179], [0, 1.23, .18], .009);
    for (const x of [-.045, .045]) cube(this.shirtDetails, this.color, [x, 1.23, .12], [.09, .035, .1]);
    mergePart(this.shirtDetails);

    this.head = new T.Group(); this.upper.add(this.head);
    this.headRigid = new T.Group(); this.head.add(this.headRigid);
    this.ears = new T.Group(); this.head.add(this.ears);
    this.cap = new T.Group(); this.head.add(this.cap);
    addMesh(this.cap, new T.SphereGeometry(1, 16, 9, 0, Math.PI * 2, 0, Math.PI / 2), this.color, [0, .13, -.015], [.275, .15, .26], true);
    ball(this.cap, this.color, [0, .145, .245], [.27, .025, .17]);
    ball(this.cap, M.cream, [0, .28, -.01], [.025, .025, .025]);
    mergePart(this.cap);
    this.eyes = new T.Group(); this.head.add(this.eyes);
    this.eyeWhites = []; this.pupils = [];
    for (const x of [-.097, .097]) {
      this.eyeWhites.push(ball(this.eyes, M.white, [x, .016, .247], [.048, .061, .023]));
      this.pupils.push(ball(this.eyes, M.ink, [x, .014, .267], [.022, .035, .012]));
      ball(this.eyes, M.white, [x - .008, .026, .278], [.008, .009, .006]);
    }
    mergePart(this.eyes);
    this.mask = new T.Group(); this.head.add(this.mask);
    for (const y of [-.17, -.065, .04, .14]) rod(this.mask, M.steel, [-.245, y, .31], [.245, y, .31], .009);
    for (const x of [-.15, 0, .15]) rod(this.mask, M.steel, [x, -.18, .315], [x, .18, .315], .009);
    mergePart(this.mask);
    this.chest = cube(this.upper, this.color, [0, 1.02, .22], [.37, .39, .09]);

    this.arms = [segment(this.upper, this.color, .085), segment(this.upper, this.color, .085)];
    this.forearms = [segment(this.upper, M.skin, .061), segment(this.upper, M.skin, .061)];
    this.hands = [ball(this.upper, M.skin, undefined, [.078, .08, .075]), ball(this.upper, M.skin, undefined, [.078, .08, .075])];
    this.glove = new T.Group(); this.upper.add(this.glove);
    ball(this.glove, M.glove, [0, 0, 0], [.13, .15, .08]);
    ball(this.glove, M.gloveDark, [0, .005, .07], [.08, .09, .015]);
    for (let i = 0; i < 4; i++) rod(this.glove, M.glove, [-.085 + i * .055, .04, 0], [-.095 + i * .06, .19 - Math.abs(i - 1.5) * .025, 0], .025);
    mergePart(this.glove);

    this.legs = [segment(this.root, M.cream, .108, true), segment(this.root, M.cream, .108, true)];
    this.shins = [segment(this.root, this.color, .075), segment(this.root, this.color, .075)];
    this.shoes = [ball(this.root, M.ink, undefined, [.115, .085, .18], true), ball(this.root, M.ink, undefined, [.115, .085, .18], true)];
    this.bat = new T.Group(); this.upper.add(this.bat);
    rod(this.bat, M.wood, [0, .12, 0], [0, 1.05, 0], .042);
    rod(this.bat, M.gloveDark, [0, -.09, 0], [0, .22, 0], .025);
    ball(this.bat, M.wood, [0, -.095, 0], [.042, .02, .042]);
    mergePart(this.bat);
    this.setSpecies('human'); this.setTeam('#28634e'); this.pose(0);
  }

  setTeam(color = '#28634e', secondary) {
    this.color.color.set(color); this.secondary = secondary;
    this.phaseOffset = (this.number || 1) * .71;
    const key = color + '/' + (this.number || 1);
    if (key !== this.jerseyKey) {
      this.jerseyKey = key; this.jerseyBadge.material = numberMaterial(color, this.number || 1);
    }
  }

  setSpecies(species = 'human') {
    if (this.species === species) return;
    this.species = species; this.headRigid.clear(); this.ears.clear(); this.floppyEars = [];
    const fur = species === 'panda' ? M.white : species === 'dog' ? M.fur : M.skin;
    this.neck.material = fur; this.forearms.forEach(o => { o.material = fur; }); this.hands.forEach(o => { o.material = fur; });
    ball(this.headRigid, fur, [0, -.025, 0], [species === 'panda' ? .295 : .265, .285, .25], true);
    for (const x of [-.267, .267]) ball(this.headRigid, fur, [x, -.025, 0], [.055, .076, .055]);
    if (species === 'panda') {
      for (const x of [-.098, .098]) {
        const patch = ball(this.headRigid, M.ink, [x, .012, .232], [.085, .115, .025]); patch.rotation.z = x > 0 ? -.25 : .25;
      }
      for (const x of [-.225, .225]) ball(this.ears, M.ink, [x, .215, -.015], [.105, .11, .07]);
      ball(this.headRigid, M.white, [0, -.098, .242], [.14, .095, .075]);
      ball(this.headRigid, M.ink, [0, -.074, .302], [.052, .038, .03]);
    } else if (species === 'dog') {
      ball(this.headRigid, M.cream, [0, -.09, .245], [.14, .102, .11]);
      ball(this.headRigid, M.ink, [0, -.055, .342], [.058, .042, .035]);
      for (const side of [-1, 1]) {
        const ear = new T.Group(); ear.position.set(side * .258, .12, -.045); this.ears.add(ear);
        ball(ear, M.ear, [side * .018, -.14, -.015], [.09, .195, .065]);
        ball(ear, M.fur, [side * .025, -.14, .034], [.045, .12, .012]);
        mergePart(ear); this.floppyEars.push(ear);
      }
    } else {
      ball(this.headRigid, M.skin, [0, -.05, .252], [.052, .061, .062]);
      for (const x of [-.165, .165]) ball(this.headRigid, M.pink, [x, -.082, .215], [.04, .025, .009]);
      if (species === 'beetle') {
        rod(this.ears, M.horn, [0, .24, .01], [0, .53, .025], .046);
        rod(this.ears, M.horn, [0, .53, .025], [-.115, .64, .04], .032);
        rod(this.ears, M.horn, [0, .53, .025], [.115, .64, .04], .032);
        ball(this.ears, M.horn, [0, .51, .03], [.065, .07, .045]);
      }
    }
    // A tiny crooked smile, visible in side view too.
    const smileZ = species === 'dog' ? .349 : species === 'panda' ? .304 : .244;
    rod(this.headRigid, M.ink, [-.055, -.145, smileZ], [0, -.157, smileZ + .005], .009);
    rod(this.headRigid, M.ink, [0, -.157, smileZ + .005], [.065, -.137, smileZ], .009);
    mergePart(this.headRigid);
    if (species !== 'dog') mergePart(this.ears);
  }

  armsTo(left, right, elbowForward = 1) {
    const requests = [left, right];
    for (let i = 0; i < 2; i++) {
      const side = i ? 1 : -1, shoulder = [side * .245, this.shoulder, 0];
      const [elbow, wrist] = joint(shoulder, requests[i], .29, .29, [side * .5, -.3, elbowForward]);
      link(this.arms[i], shoulder, elbow); link(this.forearms[i], elbow, wrist);
      this.hands[i].position.set(...wrist);
    }
    this.glove.position.copy(this.hands[0].position);
  }

  legsTo(knees, feet) {
    // Retained for compatibility; all internally generated knees use fixed-length IK.
    for (let i = 0; i < 2; i++) {
      link(this.legs[i], [(i ? 1 : -1) * .135, this.hipY, 0], knees[i]);
      link(this.shins[i], knees[i], feet[i]);
      this.shoes[i].position.set(feet[i][0], feet[i][1] + .065, feet[i][2] + .055);
    }
  }

  feetTo(left, right, hipY = .69, bends = null) {
    this.hipY = hipY;
    const knees = [], feet = [];
    [left, right].forEach((goal, i) => {
      const [knee, ankle] = joint([(i ? 1 : -1) * .135, hipY, 0], goal, .335, .335, bends?.[i] || [0, .1, 1]);
      knees.push(knee); feet.push(ankle);
    });
    this.legsTo(knees, feet);
  }

  body(height = .53, hipY = .69) {
    this.hipY = hipY; this.upper.position.y = hipY - .69;
    this.torso.scale.y = height / .53; this.torso.position.y = .75 + height * .5;
    this.neck.position.y = .75 + height + .04; this.head.position.y = .75 + height + .31;
    this.shoulder = .75 + height - .08; this.chest.position.y = .75 + height * .5;
  }

  settleUpper() {
    // Rotate the flexible spine around the pelvis instead of orbiting the whole
    // body around ground level. Feet and leg joints remain independent.
    this.upperPivot.position.y = this.hipY;
    this.upperPivot.rotation.copy(this.upper.rotation);
    this.upper.rotation.set(0, 0, 0);
    this.upper.position.y = -.69;
  }

  pose(time, kind = 'idle', opts = {}) {
    const t = Number.isFinite(time) ? time : 0, phase = opts.phase ?? (t * 8.5 + this.phaseOffset);
    const breath = Math.sin(t * 2.5 + this.phaseOffset), running = kind === 'run';
    this.root.rotation.set(0, opts.facing || 0, 0); this.root.position.y = 0; this.root.scale.setScalar(1);
    this.upper.rotation.set(0, 0, 0); this.upper.position.set(0, 0, 0);
    this.head.rotation.set(0, 0, 0); this.head.scale.setScalar(1); this.ears.rotation.set(0, 0, 0); this.cap.rotation.set(0, 0, 0);
    this.bat.visible = kind === 'bat'; this.glove.visible = kind !== 'bat' && !running;
    this.mask.visible = kind === 'catch'; this.chest.visible = kind === 'catch'; this.jerseyBadge.visible = kind !== 'catch';
    this.body(.53, .675 + breath * .006);
    this.feetTo([-.19, .035, .025], [.19, .035, -.025], this.hipY);
    this.upper.rotation.z = breath * .012; this.head.rotation.y = Math.sin(t * .75 + this.phaseOffset) * .035;
    this.armsTo([-.31, .81, .18], [.31, .81, .16]);

    if (running) {
      // Each planted foot sweeps backwards while its partner lifts for recovery.
      const speed = clamp((opts.speed ?? 5) / 5, .3, 1.5), p = opts.phase == null ? phase * speed : phase, feet = [];
      for (let i = 0; i < 2; i++) {
        const a = p + i * Math.PI;
        feet.push([(i ? 1 : -1) * .16, .035 + Math.max(0, Math.sin(a)) * .22, Math.cos(a) * .29]);
      }
      this.body(.53, .62 + Math.abs(Math.sin(p)) * .025);
      this.feetTo(feet[0], feet[1], this.hipY);
      this.upper.rotation.x = .15; this.upper.rotation.y = Math.sin(p) * .16;
      this.upper.rotation.z = Math.sin(p + .3) * .09;
      this.head.rotation.z = -Math.sin(p - .45) * .12;
      this.head.rotation.x = Math.sin(p * 2 - .6) * .065;
      this.armsTo([-.3, .96 + Math.sin(p - .5) * .11, -.05 + Math.cos(p - .3) * .26], [.3, .96 - Math.sin(p - .5) * .11, -.05 - Math.cos(p - .3) * .26]);
      this.cap.rotation.x = Math.sin(p + .7) * .04;
    }

    if (kind === 'catch') {
      this.body(.53, .4); this.feetTo([-.31, .035, .12], [.31, .035, .12], .4);
      this.upper.rotation.x = .12; this.armsTo([-.12, 1.03, .47], [.24, .87, .2]);
    }
    if (kind === 'scoop') {
      this.body(.53, .52); this.feetTo([-.3, .035, .18], [.25, .035, -.16], .52);
      this.upper.rotation.x = .52; this.armsTo([-.13, .73, .4], [.19, .8, .31]);
      this.head.rotation.x = -.35 + Math.sin((opts.u || 0) * Math.PI * 3) * .12;
      this.upper.rotation.z = Math.sin((opts.u || 0) * Math.PI * 2) * .12;
    }
    if (kind === 'catch-high') {
      this.armsTo([-.18, 1.63, .15], [.24, 1.35, .23]); this.head.rotation.x = -.18;
      this.upper.rotation.z = Math.sin((opts.u || 0) * Math.PI * 3) * .1;
    }
    if (kind === 'dive') {
      const u = clamp(opts.u ?? .5), flight = Math.sin(u * Math.PI);
      this.root.position.y = .5 + flight * .25; this.root.rotation.z = -1.12;
      this.upper.rotation.x = -.18; this.armsTo([-.38, 1.5, .19], [.38, 1.47, .16]);
    }
    if (kind === 'throw') {
      const u = smooth(opts.u || 0), release = smooth((u - .25) / .55);
      const wobble = u > .65 ? Math.sin((u - .65) * 21) * Math.exp(-(u - .65) * 6) : 0;
      this.body(.53 * (1 - Math.sin(u * Math.PI) * .065), .66);
      this.upper.rotation.y = -.48 + release * 1.05; this.upper.rotation.x = release * .2 + wobble * .16;
      this.upper.rotation.z = wobble * .2; this.head.rotation.z = -wobble * .24;
      this.armsTo(lerpPoints([-.34, 1.12, .3], [-.24, .9, .08], release), lerpPoints([.38, 1.42, -.16], [.12, 1.05, .52], release));
      this.feetTo([-.21, .035, .18], [.18, .035, -.14], .66);
    }
    if (kind === 'bat') {
      const u = smooth(opts.swing || 0), contact = smooth(u / .68), settle = smooth((u - .68) / .32);
      const bend = opts.bend || 0;
      this.body(.53, .65 - Math.sin(u * Math.PI) * .025);
      this.feetTo([-.24, .035, .08], [.24, .035 + Math.sin(u * Math.PI) * .025, -.08], this.hipY);
      this.upper.rotation.y = -.42 + contact * 1.3 + settle * .22;
      this.upper.rotation.z = bend * .24; this.upper.rotation.x = Math.sin(u * Math.PI) * .07;
      this.head.rotation.y = -this.upper.rotation.y * .5; this.head.rotation.z = -bend * .14;
      const grip = lerpPoints([.19, 1.14, .16], [-.03, 1.12, .4], contact);
      grip[0] -= settle * .15; grip[1] += settle * .14;
      this.bat.position.set(...grip);
      this.bat.rotation.set(.1 + contact * 1.12 - settle * .35, 0, -.38 - contact * 1.45 - settle * .45);
      this.bat.scale.setScalar(opts.giant ? 2.3 : 1);
      const axis = new T.Vector3(0, 1, 0).applyEuler(this.bat.rotation), center = new T.Vector3(...grip);
      this.armsTo(center.clone().addScaledVector(axis, -.055).toArray(), center.clone().addScaledVector(axis, .045).toArray());
      if (opts.spin) { this.root.rotation.y += opts.spin * Math.PI * 2; this.root.position.y = Math.abs(Math.sin(opts.spin * Math.PI)) * .22; }
    }
    const blink = Math.sin(t * .63 + this.phaseOffset);
    this.eyes.scale.y = blink > .997 ? .15 : 1;
    this.floppyEars.forEach((ear, i) => {
      ear.rotation.x = (running ? Math.sin(phase + .7 + i * .4) * .28 : breath * .045);
      ear.rotation.z = (i ? -1 : 1) * (.12 + (running ? Math.cos(phase + .8) * .11 : 0));
    });
    if (opts.reaction) {
      const amount = typeof opts.reaction === 'number' ? clamp(opts.reaction) : 1;
      const wiggle = Math.sin(t * 16 + this.phaseOffset) * amount;
      this.upper.rotation.z += wiggle * .13; this.head.rotation.z -= wiggle * .18;
      this.eyes.scale.y *= 1 - amount * .25;
    }
    this.settleUpper();
  }

  pitch(time, rig, form = {}, handedness = 1, opts = {}) {
    this.pose(time, 'idle');
    const progress = opts.progress ?? rig.w ?? 0, w = clamp(progress), lift = Math.sin(smooth(w / .52) * Math.PI * .5) * (1 - smooth((w - .48) / .34));
    const drive = smooth((w - .4) / .6), follow = smooth((progress - 1) / .45), stretch = clamp(form.stretch || 1, .75, 1.65);
    const loose = Math.max(.32, clamp((form.loose || 0) / 100));
    const recoil = Math.max(0, progress - 1), wobble = Math.sin(recoil * 19) * Math.exp(-recoil * 5.2);
    const squish = 1 - Math.sin(w * Math.PI) * (.055 + loose * .055) + wobble * .07;
    this.root.scale.set(handedness < 0 ? -1 : 1, 1, 1);
    this.body(.53 * stretch * squish, .66 - drive * .025);
    const legAngle = Number.isFinite(rig.leg) ? rig.leg : lift * 1.2;
    const legLift = clamp(Math.sin(legAngle) * .4 + (1 - Math.cos(legAngle)) * .16, 0, .49);
    const footForward = clamp(((rig.foot?.[0] ?? (4 + Math.sin(legAngle) * 18)) - 4) * .019, -.32, .36);
    const kneeForward = clamp(((rig.knee?.[0] ?? (4 + Math.sin(legAngle) * 14)) - 4) * .03, -.5, .5);
    this.feetTo([-.17, .035, -.12], [.2, .035 + legLift, footForward + drive * .38], this.hipY,
      [[0, .1, 1], [0, .15, .5 + kneeForward]]);
    this.upper.rotation.y = -.38 * lift + drive * .52;
    this.upper.rotation.x = -.12 * lift + drive * .24 + wobble * .18;
    this.upper.rotation.z = clamp(-(rig.body || 0) * .55, -.9, .9) + wobble * .24;
    this.head.rotation.y = -this.upper.rotation.y * .65; this.head.rotation.x = -this.upper.rotation.x * .5;
    // rig.arm is deliberately UNWRAPPED: edited keyframes and every full
    // windmill revolution trace a real shoulder-sized circle in 3D. Overhand
    // (-45°), sidearm (0°), and submarine (+65°) retain distinct release heights.
    // Mapping the editor's horizontal axis to forward (+Z) preserves its intent.
    const armAngle = Number.isFinite(rig.arm) ? rig.arm : lerp(-1.8, -.78, w);
    const reach = .43 + Math.sin(w * Math.PI) * .1;
    let hand = [.245 + Math.cos(armAngle) * .07, this.shoulder - Math.sin(armAngle) * reach, Math.cos(armAngle) * reach];
    const finish = [-.04, this.shoulder - .2, .41];
    hand = lerpPoints(hand, finish, follow);
    this.armsTo(lerpPoints([-.16, 1.13, .3], [-.3, .94, .08], drive), hand);
    this.head.rotation.z = Math.sin(w * Math.PI * 3) * loose * .17 - wobble * .3;
    this.head.rotation.x -= wobble * .17;
    this.cap.rotation.x = -Math.sin(w * Math.PI) * loose * .08 - wobble * .08;
    this.floppyEars.forEach((ear, i) => { ear.rotation.x += Math.sin(w * Math.PI * 2 + i * .3) * .15; });
    // Special moves stay intentionally absurd; everyday pitching remains grounded.
    this.root.position.y = Math.max(0, (rig.jump || 0) * .018);
    if (/宙返り|逆立ち/.test(form.name || '')) this.root.rotation.z = rig.spin || 0;
    else this.root.rotation.y = rig.spin || 0;
    this.root.rotation.x = (form.twist || 0) * Math.PI * 2 * smooth(w);
    this.settleUpper();
    // Special rotations pivot at the feet, so their head/torso would otherwise
    // sweep through the dirt. Lift only those flipped presentations, using the
    // visible geometry's actual clearance (including horns and outstretched
    // arms), rather than assuming every species has the same height.
    if (Math.abs(this.root.rotation.x) > .001 || Math.abs(this.root.rotation.z) > .001) {
      this.root.updateWorldMatrix(true, true);
      visibleBounds.makeEmpty();
      this.root.traverseVisible(object => {
        if (!object.isMesh) return;
        if (!object.geometry.boundingBox) object.geometry.computeBoundingBox();
        partBounds.copy(object.geometry.boundingBox).applyMatrix4(object.matrixWorld);
        visibleBounds.union(partBounds);
      });
      if (!visibleBounds.isEmpty() && visibleBounds.min.y < .015) this.root.position.y += .015 - visibleBounds.min.y;
    }
  }
}

function lerpPoints(a, b, u) { return blend(a, b, clamp(u)); }
