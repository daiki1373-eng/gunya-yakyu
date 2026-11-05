import * as T from 'three';

// A small, well-loved neighbourhood ground. All assets are generated locally;
// keeping the silhouettes simple also makes a complete play readable on a phone.
const geometry = {
  box: new T.BoxGeometry(1, 1, 1),
  sphere: new T.SphereGeometry(1, 12, 8),
  cylinder: new T.CylinderGeometry(1, 1, 1, 8),
};
const palette = new Map();
const mat = (color, extra = {}) => {
  const key = JSON.stringify([color, extra]);
  if (!palette.has(key)) palette.set(key, new T.MeshStandardMaterial({color, roughness: .94, ...extra}));
  return palette.get(key);
};
const green = mat('#35684b'), cream = mat('#f2ead2'), wood = mat('#b18b58');
const steel = mat('#60766b'), concrete = mat('#c0b9a1'), dark = mat('#243f36');
const matrix = new T.Matrix4(), quaternion = new T.Quaternion(), up = new T.Vector3(0, 1, 0);

function mesh(parent, g, material, position, scale = [1, 1, 1], shadow = false) {
  const object = new T.Mesh(g, material);
  object.position.set(...position); object.scale.set(...scale);
  object.castShadow = shadow; object.receiveShadow = true;
  parent.add(object); return object;
}
const box = (parent, material, position, scale, shadow) => mesh(parent, geometry.box, material, position, scale, shadow);
const sphere = (parent, material, position, scale, shadow) => mesh(parent, geometry.sphere, material, position, scale, shadow);
function rod(parent, material, a, b, radius = .06, shadow = false) {
  const from = new T.Vector3(...a), delta = new T.Vector3(...b).sub(from);
  const object = mesh(parent, geometry.cylinder, material, from.addScaledVector(delta, .5).toArray(), [radius, delta.length(), radius], shadow);
  object.quaternion.setFromUnitVectors(up, delta.normalize()); return object;
}
function plane(parent, material, width, depth, x, z, height = .012) {
  const object = mesh(parent, new T.PlaneGeometry(width, depth), material, [x, height, z]);
  object.rotation.x = -Math.PI / 2; return object;
}
function polygon(parent, points, material, height = .025) {
  const shape = new T.Shape();
  points.forEach(([x, z], i) => i ? shape.lineTo(x, -z) : shape.moveTo(x, -z));
  shape.closePath();
  const object = mesh(parent, new T.ShapeGeometry(shape), material, [0, height, 0]);
  object.rotation.x = -Math.PI / 2; return object;
}
function disk(parent, material, x, z, radius, height = .03) {
  const object = mesh(parent, new T.CircleGeometry(radius, 48), material, [x, height, z]);
  object.rotation.x = -Math.PI / 2; return object;
}
function texture(width, height, paint) {
  const canvas = document.createElement('canvas'); canvas.width = width; canvas.height = height;
  paint(canvas.getContext('2d'), width, height);
  const result = new T.CanvasTexture(canvas); result.colorSpace = T.SRGBColorSpace; result.anisotropy = 4;
  return result;
}
function surfaceTexture(base, light, shade, repeat) {
  const result = texture(256, 256, (context, width, height) => {
    context.fillStyle = base; context.fillRect(0, 0, width, height);
    // A fixed integer generator avoids both a visible grid and changing scenery.
    let seed = 7381;
    for (let i = 0; i < 7200; i++) {
      seed = (Math.imul(seed, 1664525) + 1013904223) >>> 0; const x = seed % width;
      seed = (Math.imul(seed, 1664525) + 1013904223) >>> 0; const y = seed % height;
      context.fillStyle = i % 2 ? light : shade; context.fillRect(x, y, 1, 2);
    }
  });
  result.wrapS = result.wrapT = T.RepeatWrapping; result.repeat.set(repeat, repeat);
  return result;
}
function sign(parent, title, subtitle, width, height, position, background = '#eee2bd', ink = '#305c49') {
  const map = texture(768, 192, (context, w, h) => {
    context.fillStyle = background; context.fillRect(0, 0, w, h);
    context.strokeStyle = ink; context.lineWidth = 5; context.strokeRect(10, 10, w - 20, h - 20);
    context.fillStyle = ink; context.textAlign = 'center'; context.textBaseline = 'middle';
    context.font = 'bold 67px sans-serif'; context.fillText(title, w / 2, 77, w - 48);
    context.font = '27px sans-serif'; context.fillText(subtitle, w / 2, 144, w - 48);
  });
  return mesh(parent, new T.PlaneGeometry(width, height), new T.MeshBasicMaterial({map}), position);
}
function stripe(parent, a, b, width = .09, material = cream, height = .066) {
  const [ax, az] = a, [bx, bz] = b;
  const object = plane(parent, material, width, Math.hypot(bx - ax, bz - az), (ax + bx) / 2, (az + bz) / 2, height);
  object.rotation.z = -Math.atan2(bx - ax, -(bz - az));
  return object;
}

export class Ballpark {
  constructor(parent) {
    const grass = surfaceTexture('#78924f', '#a2b97131', '#3b6c3833', 48);
    const turf = new T.MeshStandardMaterial({map: grass, color: '#e3efd2', roughness: 1});
    plane(parent, turf, 360, 360, 0, -70, -.015);
    const soilMap = surfaceTexture('#bd9670', '#e7c69736', '#76563328', 5);
    const soil = new T.MeshStandardMaterial({map: soilMap, roughness: 1});

    // Concentric mowing bands follow the outfield, ending at the actual fence.
    for (let i = 0; i < 7; i++) {
      const inner = 39 + i * 8.3, outer = Math.min(99, inner + 8.3);
      const shape = [];
      for (let j = 0; j <= 32; j++) {
        const angle = -Math.PI / 4 + j * Math.PI / 64;
        shape.push([Math.sin(angle) * outer, -Math.cos(angle) * outer]);
      }
      for (let j = 32; j >= 0; j--) {
        const angle = -Math.PI / 4 + j * Math.PI / 64;
        shape.push([Math.sin(angle) * inner, -Math.cos(angle) * inner]);
      }
      polygon(parent, shape, mat(i % 2 ? '#73954c' : '#80a256'), .003);
    }

    // Dirt follows the diamond; the central grass island leaves real basepaths.
    polygon(parent, [[0, 3], [24, -19.4], [0, -43], [-24, -19.4]], soil, .025);
    disk(parent, soil, 0, .1, 4.5, .026);
    polygon(parent, [[0, -6.5], [13.1, -19.4], [0, -32.5], [-13.1, -19.4]], turf, .036);
    for (const [x, z] of [[19.4, -19.4], [0, -38.8], [-19.4, -19.4]]) {
      const base = box(parent, cream, [x, .088, z], [.55, .105, .55]);
      base.rotation.y = Math.PI / 4;
    }
    polygon(parent, [[-.22, -.22], [.22, -.22], [.22, .05], [0, .28], [-.22, .05]], cream, .073);
    for (const side of [-1, 1]) stripe(parent, [0, 0], [side * 70.7, -70.7], .095);
    for (const side of [-1, 1]) {
      const x = side * 1.05;
      for (const [a, b] of [
        [[x - .35, -.75], [x + .35, -.75]], [[x + .35, -.75], [x + .35, 1.05]],
        [[x + .35, 1.05], [x - .35, 1.05]], [[x - .35, 1.05], [x - .35, -.75]],
      ]) stripe(parent, a, b, .045);
      // The on-deck circle is outside the playing diamond.
      const circle = mesh(parent, new T.RingGeometry(1.45, 1.51, 40), cream, [side * 8.6, .065, 1.4]);
      circle.rotation.x = -Math.PI / 2;
    }
    disk(parent, soil, 0, -18.4, 2.7, .048);
    sphere(parent, mat('#bf9b75'), [0, -.06, -18.4], [2.4, .22, 2]);
    box(parent, cream, [0, .18, -18.4], [.61, .04, .15]);
    // A scuffed landing patch and rake marks give the mound a human scale.
    disk(parent, mat('#c6a481'), .3, -17.6, .35, .056);
    for (let i = 0; i < 7; i++) stripe(parent, [-1.6 + i * .16, -19.6], [-1.45 + i * .16, -19.2], .018, mat('#a98260'), .058);

    this.makeFence(parent);
    this.makeBleachers(parent);
    this.makeNeighbourhood(parent);
    this.makeScoreboard(parent);
    this.updateScore(null);
  }

  makeFence(parent) {
    // Inward-facing curved outfield fence, rather than repeated stadium towers.
    const wires = [], fenceTop = mat('#638665');
    for (let i = 0; i < 40; i++) {
      const a = -Math.PI / 4 + i * Math.PI / 80, b = a + Math.PI / 80;
      const from = [Math.sin(a) * 100, -Math.cos(a) * 100], to = [Math.sin(b) * 100, -Math.cos(b) * 100];
      const panel = box(parent, green, [(from[0] + to[0]) / 2, 1.02, (from[1] + to[1]) / 2], [3.94, 2.04, .18]);
      panel.rotation.y = -(a + b) / 2;
      rod(parent, fenceTop, [from[0], 0, from[1]], [from[0], 3.15, from[1]], .055);
      for (const y of [2.1, 2.6, 3.1]) wires.push(from[0], y, from[1], to[0], y, to[1]);
      for (let n = 1; n <= 6; n++) {
        const x = T.MathUtils.lerp(from[0], to[0], n / 7), z = T.MathUtils.lerp(from[1], to[1], n / 7);
        wires.push(x, 2.05, z, x, 3.1, z);
      }
    }
    const net = new T.BufferGeometry(); net.setAttribute('position', new T.Float32BufferAttribute(wires, 3));
    parent.add(new T.LineSegments(net, new T.LineBasicMaterial({color: '#6c8a71', transparent: true, opacity: .55})));
    for (const side of [-1, 1]) {
      rod(parent, mat('#e6bd60'), [side * 70.7, 0, -70.7], [side * 70.7, 12, -70.7], .11);
      // Foul-territory side fence stops behind the dugouts, leaving room for play.
      rod(parent, steel, [side * 72, 2, -70], [side * 13, 2, 7], .04);
      for (let i = 0; i <= 12; i++) {
        const x = side * (13 + i * 59 / 12), z = 7 - i * 77 / 12;
        rod(parent, steel, [x, 0, z], [x, 2, z], .045);
      }
    }
    const ads = [
      ['ぐにゃ整骨院', '曲がっても、気持ちはまっすぐ。', '#f0dfae', '#41614d'],
      ['おかわり食堂', '本日のおすすめ：もう一打席', '#dbb16d', '#443f32'],
      ['若葉スポーツ', 'ナイスプレーを、毎日に。', '#f1e9cd', '#315d4e'],
    ];
    for (let i = 0; i < ads.length; i++) {
      const angle = (i - 1) * .39, group = new T.Group();
      group.position.set(Math.sin(angle) * 99.85, 0, -Math.cos(angle) * 99.85); group.rotation.y = -angle; parent.add(group);
      sign(group, ads[i][0], ads[i][1], 12.8, 1.65, [0, 1.05, .13], ads[i][2], ads[i][3]);
    }
    // A low backstop is behind the batter, never between the camera and pitcher.
    const backstop = [];
    for (let x = -7; x <= 7; x += .5) backstop.push(x, 0, 5.8, x, 7, 5.8);
    for (let y = 0; y <= 7; y += .5) backstop.push(-7, y, 5.8, 7, y, 5.8);
    const backGeometry = new T.BufferGeometry(); backGeometry.setAttribute('position', new T.Float32BufferAttribute(backstop, 3));
    parent.add(new T.LineSegments(backGeometry, new T.LineBasicMaterial({color: '#59796a', transparent: true, opacity: .3})));
    for (const x of [-7, 7]) rod(parent, steel, [x, 0, 5.8], [x, 7.1, 5.8], .07);
  }

  makeBleachers(parent) {
    const audience = [], colors = ['#e8d7b2', '#507381', '#c58157', '#83a397', '#d9b360', '#6b7157'];
    for (const side of [-1, 1]) {
      const group = new T.Group(); group.position.set(side * 37, 0, -20); group.rotation.y = -side * Math.PI / 4; parent.add(group);
      for (let row = 0; row < 4; row++) {
        box(group, concrete, [0, .35 + row * .44, -row * 1.18], [17.6, .7 + row * .88, 1.18]);
        box(group, wood, [0, .78 + row * .88, -.08 - row * 1.18], [17.3, .12, .52]);
        for (let seat = 0; seat < 18; seat++) {
          if ((seat * 3 + row * 5 + (side + 1)) % 8 < 2) continue;
          const p = new T.Vector3(-7.8 + seat * .9, 1.22 + row * .88, -.02 - row * 1.18).applyAxisAngle(up, group.rotation.y).add(group.position);
          audience.push({p, rotation: group.rotation.y, color: colors[(seat * 7 + row * 3) % colors.length]});
        }
      }
      for (const x of [-8.9, 8.9]) rod(group, steel, [x, 0, -4.1], [x, 5.5, -4.1], .08);
      box(group, green, [0, 5.5, -3.6], [19, .2, 4]);
      // Dugouts are set in foul territory, with an open face and actual benches.
      const dugout = new T.Group(); dugout.position.set(side * 13.2, 0, -3); dugout.rotation.y = -side * .65; parent.add(dugout);
      box(dugout, green, [0, 1.25, -1.2], [7, 2.5, .15]);
      for (const x of [-3.4, 3.4]) box(dugout, concrete, [x, 1.2, -.15], [.14, 2.4, 2.3]);
      box(dugout, green, [0, 2.5, -.2], [7.5, .18, 2.6]);
      box(dugout, wood, [0, .52, -.7], [6, .15, .65]);
      for (const x of [-2.4, 2.4]) box(dugout, steel, [x, .25, -.7], [.1, .5, .5]);
      box(dugout, mat('#e1a762'), [2.55, .48, .35], [.45, .9, .45]);
    }
    // Bodies and heads share two instanced draw calls. Seats determine every
    // spectator position, so the audience never floats in front of the stands.
    const bodies = new T.InstancedMesh(geometry.sphere, mat('#ffffff'), audience.length);
    const heads = new T.InstancedMesh(geometry.sphere, mat('#dfb68b'), audience.length);
    const scale = new T.Vector3();
    audience.forEach(({p, rotation, color}, i) => {
      quaternion.setFromAxisAngle(up, rotation);
      matrix.compose(p, quaternion, scale.set(.23, .33, .21)); bodies.setMatrixAt(i, matrix); bodies.setColorAt(i, new T.Color(color));
      matrix.compose(new T.Vector3(p.x, p.y + .44, p.z), quaternion, scale.set(.17, .19, .17)); heads.setMatrixAt(i, matrix);
    });
    bodies.castShadow = heads.castShadow = false; bodies.receiveShadow = heads.receiveShadow = false;
    parent.add(bodies, heads);
  }

  makeNeighbourhood(parent) {
    // Irregular, low silhouettes make the park feel situated in a real town.
    for (let i = 0; i < 9; i++) {
      sphere(parent, mat(i % 2 ? '#91a47b' : '#7f9a75'), [-160 + i * 40, -3, -165 - (i % 3) * 10], [32 + i % 3 * 8, 14 + i % 4 * 2, 26]);
    }
    const foliage = [mat('#4d7651'), mat('#5e854f'), mat('#739052')], trunk = mat('#82735a');
    for (let i = 0; i < 43; i++) {
      const angle = -1.1 + i * 2.2 / 42, radius = 111 + (i * 13 % 9), x = Math.sin(angle) * radius, z = -Math.cos(angle) * radius - 3;
      const height = 4.2 + (i * 7 % 8) * .35;
      rod(parent, trunk, [x, 0, z], [x, height, z], .22);
      sphere(parent, foliage[i % 3], [x, height + 1.3, z], [2.4 + i % 2 * .4, 3 + i % 3 * .3, 2.5]);
      sphere(parent, foliage[(i + 1) % 3], [x - 1.4, height + .3, z + .4], [2, 2.1, 1.9]);
    }
    const wallColors = ['#d3c5a5', '#d7d4be', '#b5bda9'];
    for (let i = 0; i < 8; i++) {
      const x = (i < 4 ? -1 : 1) * (58 + (i % 4) * 16), z = -113 - (i * 11 % 27), height = 6 + i % 3 * 1.3;
      box(parent, mat(wallColors[i % 3]), [x, height / 2, z], [11, height, 9]);
      const roof = new T.ConeGeometry(9.1, 3.6, 4); roof.rotateY(Math.PI / 4);
      mesh(parent, roof, mat(i % 2 ? '#7b8070' : '#9d7c62'), [x, height + 1.8, z], [1, 1, .85]);
      for (const dx of [-3.1, 0, 3.1]) box(parent, mat('#71918b'), [x + dx, height - 2, z + 4.51], [1.4, 1.8, .025]);
    }
    // Modest light masts belong beside the field, rather than filling the sky.
    for (const side of [-1, 1]) {
      const x = side * 77, z = -47;
      rod(parent, steel, [x, 0, z], [x, 18, z], .16);
      box(parent, steel, [x, 18, z], [4.7, 1.8, .35]);
      for (let i = 0; i < 6; i++) sphere(parent, cream, [x - 1.5 + i % 3 * 1.5, 17.6 + Math.floor(i / 3) * .8, z + .22], [.4, .28, .15]);
    }
  }

  makeScoreboard(parent) {
    this.scoreCanvas = document.createElement('canvas'); this.scoreCanvas.width = 1024; this.scoreCanvas.height = 512;
    this.scoreTexture = new T.CanvasTexture(this.scoreCanvas); this.scoreTexture.colorSpace = T.SRGBColorSpace;
    box(parent, dark, [0, 9, -109], [20.8, 10.6, .7]);
    for (const x of [-7.3, 7.3]) box(parent, steel, [x, 2.8, -109], [.65, 5.6, .6]);
    mesh(parent, new T.PlaneGeometry(19.8, 9.6), new T.MeshBasicMaterial({map: this.scoreTexture}), [0, 9, -108.6]);
    sign(parent, '若葉市民球場', '今日も、いい感じにぐにゃっと。', 20.8, 2, [0, 15.4, -108.6], '#eee4c4', '#355b47');
    // The wind sock has a deliberately relaxed droop; no perpetual motion is
    // needed to make the environment feel lively or distract from the pitch.
    rod(parent, steel, [13.8, 0, -107], [13.8, 17.4, -107], .07);
    const sock = box(parent, mat('#d19760'), [15, 16.8, -107], [2.4, .55, .15]); sock.rotation.z = -.16;
    for (let i = 0; i < 3; i++) {
      const band = box(parent, cream, [14.4 + i * .6, 16.89 - i * .1, -106.91], [.25, .55, .035]); band.rotation.z = -.16;
    }
  }

  updateScore(G) {
    const key = G ? JSON.stringify([G.sc, G.inn, G.top, G.line, G.me?.short, G.you?.short]) : 'title';
    if (this.scoreKey === key) return; this.scoreKey = key;
    const context = this.scoreCanvas.getContext('2d');
    context.fillStyle = '#203c32'; context.fillRect(0, 0, 1024, 512);
    context.strokeStyle = '#718268'; context.lineWidth = 2;
    context.strokeRect(18, 18, 988, 476);
    context.fillStyle = '#ead9ac'; context.textAlign = 'center'; context.textBaseline = 'middle';
    context.font = 'bold 43px sans-serif'; context.fillText('WAKABA  BASEBALL  PARK', 512, 69);
    context.font = '29px monospace'; context.fillText('TEAM      1   2   3   4   5     R', 510, 152);
    const away = G ? G.myTop ? 'me' : 'you' : null, home = G ? G.myTop ? 'you' : 'me' : null;
    [away, home].forEach((team, row) => {
      const y = 259 + row * 126;
      context.textAlign = 'left'; context.font = 'bold 39px sans-serif';
      context.fillText(team ? G[team].short : row ? '若葉' : '青空', 47, y, 240);
      context.textAlign = 'center'; context.font = '42px monospace';
      for (let inning = 0; inning < 5; inning++) context.fillText(team ? G.line[team][inning] ?? '·' : '·', 378 + inning * 101, y);
      context.fillStyle = '#f5d985'; context.fillText(team ? G.sc[team] : 0, 944, y); context.fillStyle = '#ead9ac';
    });
    context.font = '23px sans-serif'; context.fillStyle = '#a9bb94';
    context.fillText(G ? `${G.inn}回 ${G.top ? '表' : '裏'}  •  ナイスプレーに拍手を！` : '肩の力をぬいて、プレイボール。', 512, 463);
    this.scoreTexture.needsUpdate = true;
  }
}
