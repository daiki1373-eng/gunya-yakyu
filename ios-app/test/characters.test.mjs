import test from 'node:test';
import assert from 'node:assert/strict';
import * as T from 'three';
import {Player} from '../src/characters.js';

// Character numbers are painted on a tiny canvas; no WebGL context is needed
// to exercise the public pose interface and inspect the resulting scene graph.
globalThis.document = {createElement: () => ({getContext: () => ({fillText() {}})})};
const create = () => new Player(new T.Scene());
const rig = (overrides = {}) => ({w: .5, arm: -.7, leg: .5, body: 0, spin: 0, jump: 0, ...overrides});
const form = {stretch: 1, loose: 25};
function point(player, object) {
  player.root.updateMatrixWorld(true);
  return object.getWorldPosition(new T.Vector3());
}

test('editable arm and leg keyframes change the 3D pitcher while the support foot stays grounded', () => {
  const p = create();
  p.pitch(0, rig({arm: -Math.PI / 2, leg: 0}), form);
  const highHand = point(p, p.hands[1]), lowFoot = point(p, p.shoes[1]), supportFoot = point(p, p.shoes[0]);
  p.pitch(0, rig({arm: Math.PI / 2, leg: Math.PI / 2}), form);
  const lowHand = point(p, p.hands[1]), highFoot = point(p, p.shoes[1]);
  assert(highHand.y - lowHand.y > .6, 'overhand and underhand edits must have visibly different hand heights');
  assert(highFoot.y - lowFoot.y > .3, 'the leg editor must lift the free foot');
  assert(point(p, p.shoes[0]).distanceTo(supportFoot) < .001, 'editing the free leg must not slide the support foot');
});

test('windmill arm motion retains every complete revolution and special forms still rotate the body', () => {
  const p = create();
  let previous = null, travelled = 0;
  for (let i = 0; i <= 160; i++) {
    p.pitch(0, rig({arm: -Math.PI / 2 + i / 160 * Math.PI * 8}), {...form, windmill: 4});
    const hand = p.hands[1].position;
    const angle = Math.atan2(-(hand.y - p.shoulder), hand.z);
    if (previous !== null) travelled += Math.atan2(Math.sin(angle - previous), Math.cos(angle - previous));
    previous = angle;
  }
  assert(Math.abs(travelled - Math.PI * 8) < .05, 'four turns must not be reduced to a small arm twitch');
  p.pitch(0, rig({spin: Math.PI * 3, body: 2.5}), {...form, spin: 3, twist: 1});
  assert(Math.abs(p.root.rotation.y) > Math.PI, 'special body spins must remain visible');
  assert(Math.abs(p.upperPivot.rotation.z) > .6, 'extreme torso keyframes must stay rubbery and expressive');
});

test('switching species retains attached, visible ears or horns', () => {
  const p = create();
  for (const species of ['panda', 'dog', 'beetle', 'human', 'dog', 'panda']) {
    p.setSpecies(species); p.pose(.4);
    assert.equal(p.ears.parent, p.head);
    assert.equal(p.ears.visible, true);
    assert(p.headRigid.children.length > 0);
    if (species !== 'human') assert(p.ears.children.length > 0, `${species} must retain its species silhouette`);
  }
});

test('standard actions, post-release wobble and exaggerated pitches produce finite poses', () => {
  const p = create();
  for (const species of ['human', 'panda', 'dog', 'beetle']) {
    p.setSpecies(species);
    for (const kind of ['idle', 'run', 'catch', 'scoop', 'dive', 'catch-high', 'throw', 'bat']) {
      for (const u of [0, .25, .65, 1]) {
        p.pose(u, kind, {phase: u * 10, speed: 6, u, swing: u, reaction: .4});
        assertFinite(p, `${species}/${kind}/${u}`);
      }
    }
    for (const progress of [0, .5, 1, 1.2, 1.6]) {
      p.pitch(progress, rig({w: Math.min(1, progress), arm: progress * Math.PI * 8, leg: progress * Math.PI * 2, body: progress * 3, spin: progress * Math.PI * 4, jump: 12}),
        {...form, name: '宙返り投法', windmill: 4, twist: 2, stretch: 2.25}, -1, {progress});
      assertFinite(p, `${species}/special/${progress}`);
    }
  }
});

test('somersault and tornado presentations keep the visible character above the dirt', () => {
  const p = create();
  for (const species of ['human', 'panda', 'dog', 'beetle']) {
    p.setSpecies(species);
    for (const special of [
      {name: 'ぐるぐるトルネード', twist: 1, spin: 1},
      {name: '宙返り投法', twist: 0, spin: 2},
      {name: 'ヘリコプター', twist: 2, spin: 3}
    ]) {
      for (let i = 0; i <= 24; i++) {
        const w = i / 24, turn = w * w * (3 - 2 * w);
        p.pitch(w, rig({w, arm: w * Math.PI * 8, body: Math.sin(w * Math.PI * 2), leg: w * Math.PI, spin: special.spin * Math.PI * 2 * turn}),
          {...form, ...special}, 1, {progress: w});
        const bounds = visibleBox(p.root);
        assert(bounds.min.y >= -.01, `${species}/${special.name}/${w} disappeared beneath the field`);
      }
    }
  }
  p.pitch(0, rig({w: 0, spin: 0}), form);
  assert.equal(p.root.position.y, 0, 'ordinary pitching must not float above its support foot');
});

function visibleBox(root) {
  root.updateWorldMatrix(true, true);
  const bounds = new T.Box3();
  root.traverseVisible(object => {
    if (!object.isMesh) return;
    object.geometry.computeBoundingBox();
    bounds.union(object.geometry.boundingBox.clone().applyMatrix4(object.matrixWorld));
  });
  return bounds;
}

function assertFinite(player, label) {
  player.root.updateMatrixWorld(true);
  player.root.traverse(object => {
    assert(object.matrixWorld.elements.every(Number.isFinite), `${label} has a non-finite transform`);
  });
}
