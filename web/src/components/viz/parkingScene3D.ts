import * as THREE from 'three';

export interface ParkingSlot {
  id: string;
  f: string; // floor
  r: string; // row
  n: number; // index
  i: number; // global index
  s: 'available' | 'reserved' | 'occupied' | 'maintenance' | 'verify'; // status
  p: string; // plate
}

export function mk3D(el: HTMLElement, spots: ParkingSlot[], onPick: (id: string) => void) {
  const ST: Record<string, [string, string]> = {
    available: ['Trống', '#16a36a'],
    reserved: ['Đã đặt trước', '#2f80ed'],
    occupied: ['Có xe', '#e5484d'],
    maintenance: ['Bảo trì', '#f2a007'],
    verify: ['Cần xác minh', '#8a5cf6']
  };
  const K = Object.keys(ST);
  const FL = ['T1', 'T2', 'T3'];

  const FH = 3.2, B0 = .3, BX = 2.5, CX = 6, PI = Math.PI;
  const R = new THREE.WebGLRenderer({ antialias: true, alpha: true });
  R.setPixelRatio(Math.min(window.devicePixelRatio, 2));
  // In r128 it was outputEncoding = sRGBEncoding. In newer three.js it's outputColorSpace = SRGBColorSpace.
  if ('outputColorSpace' in R) {
    (R as any).outputColorSpace = (THREE as any).SRGBColorSpace;
  } else {
    (R as any).outputEncoding = (THREE as any).sRGBEncoding;
  }
  R.toneMapping = THREE.ACESFilmicToneMapping;
  R.toneMappingExposure = 1;
  R.shadowMap.enabled = true;
  R.shadowMap.type = THREE.PCFShadowMap;
  R.shadowMap.autoUpdate = false;
  el.appendChild(R.domElement);
  const sc = new THREE.Scene(), cam = new THREE.PerspectiveCamera(36, 1, .5, 400), tg = new THREE.Vector3(CX, 4.2, 0), rc = new THREE.Raycaster();
  const tx = (w: number, h: number, fn: (g: CanvasRenderingContext2D, w: number, h: number) => void) => {
    const c = document.createElement('canvas'); c.width = w; c.height = h;
    const ctx = c.getContext('2d');
    if(ctx) fn(ctx, w, h);
    const t = new THREE.CanvasTexture(c);
    if ('colorSpace' in t) {
      (t as any).colorSpace = (THREE as any).SRGBColorSpace;
    } else {
      (t as any).encoding = (THREE as any).sRGBEncoding;
    }
    return t;
  };
  /* môi trường studio */
  const et = tx(1024, 512, (g, w, h) => {
    const q = g.createLinearGradient(0, 0, 0, h);
    q.addColorStop(0, '#7fa9e0'); q.addColorStop(.45, '#f4f1ea'); q.addColorStop(.55, '#d9d4c8'); q.addColorStop(1, '#8d8f88');
    g.fillStyle = q; g.fillRect(0, 0, w, h); g.fillStyle = '#fff';
    [[60, 60, 260, 60], [460, 40, 200, 90], [760, 70, 230, 50]].forEach(r => g.fillRect(r[0], r[1], r[2], r[3]));
  });
  et.mapping = THREE.EquirectangularReflectionMapping; const pm = new THREE.PMREMGenerator(R); sc.environment = pm.fromEquirectangular(et).texture; pm.dispose();
  sc.add(new THREE.HemisphereLight(0xeaf1ff, 0x8a8576, .5));
  const dl = new THREE.DirectionalLight(0xfff4e6, 1); dl.position.set(-20, 40, 26); dl.target.position.set(CX, 2, 0); sc.add(dl, dl.target); dl.castShadow = true; dl.shadow.mapSize.set(2048, 2048);
  Object.assign(dl.shadow.camera, { left: -40, right: 40, top: 34, bottom: -34, near: 5, far: 130 }); dl.shadow.bias = -.0004; dl.shadow.normalBias = .04;
  /* vật liệu */
  const conc = tx(256, 256, g => {
    g.fillStyle = '#d9d4c8'; g.fillRect(0, 0, 256, 256);
    for (let i = 0; i < 2200; i++) {
      g.fillStyle = `rgba(${90 + Math.random() * 90 | 0},${90 + Math.random() * 90 | 0},${80 + Math.random() * 80 | 0},.16)`;
      g.fillRect(Math.random() * 256, Math.random() * 256, 2, 2);
    }
  });
  conc.wrapS = conc.wrapT = THREE.RepeatWrapping; conc.repeat.set(8, 4);
  const S = (c: any, o?: any) => new THREE.MeshStandardMaterial(Object.assign({ color: c, roughness: .8, metalness: 0 }, o || {}));
  const bx = (w: number, h: number, d: number) => new THREE.BoxGeometry(w, h, d), mk = (geo: any, mat: any, x: number, y: number, z: number, g: any, sh?: boolean) => { const m = new THREE.Mesh(geo, mat); m.position.set(x, y, z); m.castShadow = sh !== false; m.receiveShadow = true; g.add(m); return m };
  const cm = S(0xffffff, { map: conc, roughness: .92 }), pm2 = S(0xf3efe5, { map: conc, roughness: .9 }), steel = S(0x9aa3ad, { metalness: .9, roughness: .35 }), asph = S(0x2c2f34, { roughness: .96 }), lawn = S(0x7fae4f, { roughness: 1 }), lineM = S(0xffffff, { roughness: .7 }),
    glassB = new THREE.MeshPhysicalMaterial({ color: 0xa9c7d6, metalness: .2, roughness: .05, transparent: true, opacity: .3 }), glass = S(0x131a25, { metalness: .9, roughness: .05 }), tire = S(0x121418, { roughness: .95 }), dark = S(0x1a1d22, { roughness: .7 }),
    rimT = tx(128, 128, g => {
      g.fillStyle = '#16181d'; g.fillRect(0, 0, 128, 128); g.fillStyle = '#cfd5df'; g.beginPath(); g.arc(64, 64, 62, 0, 7); g.fill(); g.fillStyle = '#16181d';
      for (let i = 0; i < 5; i++) { const a = i * PI * 2 / 5 + .3; g.beginPath(); g.moveTo(64, 64); g.arc(64, 64, 52, a, a + .75); g.closePath(); g.fill() }
      g.fillStyle = '#e8ecf2'; g.beginPath(); g.arc(64, 64, 14, 0, 7); g.fill();
    }),
    shT = tx(64, 64, g => { const q = g.createRadialGradient(32, 32, 4, 32, 32, 32); q.addColorStop(0, 'rgba(0,0,0,.55)'); q.addColorStop(1, 'rgba(0,0,0,0)'); g.fillStyle = q; g.fillRect(0, 0, 64, 64) }),
    hzT = tx(128, 32, g => { g.fillStyle = '#f2b705'; g.fillRect(0, 0, 128, 32); g.fillStyle = '#111'; for (let i = -1; i < 8; i++) { g.beginPath(); g.moveTo(i * 20, 32); g.lineTo(i * 20 + 10, 32); g.lineTo(i * 20 + 26, 0); g.lineTo(i * 20 + 16, 0); g.fill() } }),
    rdT = tx(128, 128, g => { g.fillStyle = '#2f80ed'; g.fillRect(8, 8, 112, 112); g.fillStyle = '#fff'; g.font = '700 84px sans-serif'; g.textAlign = 'center'; g.textBaseline = 'middle'; g.fillText('R', 64, 68) });
  const rimM = S(0xffffff, { map: rimT, metalness: .6, roughness: .35 }), shM = new THREE.MeshBasicMaterial({ map: shT, transparent: true, depthWrite: false }), hzM = S(0xffffff, { map: hzT }), rdM = new THREE.MeshBasicMaterial({ map: rdT }), coneM = S(0xff7a1a, { roughness: .5 }), stopM = S(0xbdb7aa);
  const padM: any = {}, ledM: any = {}; K.forEach(q => { const c = ST[q][1]; padM[q] = S(c, { transparent: true, opacity: q === 'occupied' ? .12 : .22, roughness: .6, depthWrite: false }); ledM[q] = S(c, { emissive: c, emissiveIntensity: 1.6 }) });
  const bm: any = {}, body = (c: any) => bm[c] || (bm[c] = new THREE.MeshPhysicalMaterial({ color: c, metalness: .5, roughness: .28, clearcoat: 1, clearcoatRoughness: .08 }));
  /* 3 kiểu xe */
  const shp = (a: any) => { const s = new THREE.Shape(); a.forEach((p: any, i: number) => p.length == 2 ? (i ? s.lineTo(p[0], p[1]) : s.moveTo(p[0], p[1])) : s.quadraticCurveTo(p[0], p[1], p[2], p[3])); s.closePath(); return s };
  const TY: any = {
    sedan: { L: 4.7, W: 1.85, wr: .34, ax: [.95, 3.7], bt: .9, ct: 1.43, hy: .62, rl: 1.4, rz: .05, pz: -.15, b: [[.25, .3], [.05, .38], [0, .62], [.06, .8, .6, .86], [3.9, .9], [4.55, .88, 4.68, .68], [4.7, .42], [4.5, .3]], c: [[1.05, .86], [1.55, 1.4, 2, 1.43], [3, 1.42], [3.55, 1.36, 3.95, .9]] },
    suv: { L: 4.75, W: 1.95, wr: .38, ax: [.95, 3.75], bt: 1.08, ct: 1.74, hy: .85, rl: 2.2, rz: -.62, pz: -.42, b: [[.3, .38], [.1, .45], [0, .75], [.08, .98, .7, 1.05], [4.1, 1.08], [4.65, 1.05, 4.74, .85], [4.75, .5], [4.55, .38]], c: [[1.1, 1.05], [1.45, 1.72, 1.9, 1.74], [4.1, 1.74], [4.55, 1.7, 4.6, 1.05]] },
    ev: { L: 4.65, W: 1.9, wr: .36, ax: [.85, 3.8], bt: .9, ct: 1.52, hy: .66, rl: 1.5, rz: -.17, pz: -.37, b: [[.3, .3], [.1, .36], [0, .58], [.05, .78, .7, .84], [3.9, .9], [4.5, .88, 4.63, .7], [4.65, .42], [4.45, .3]], c: [[1, .84], [1.7, 1.52, 2.4, 1.52], [3.4, 1.47, 4.35, .82]] }
  };
  const ex = (sh: any, w: number, b: number, L: number) => { const g = new THREE.ExtrudeGeometry(sh, { depth: w, bevelEnabled: true, bevelThickness: b, bevelSize: b, bevelSegments: 3, curveSegments: 10 }); g.translate(-L / 2, 0, -w / 2); g.rotateY(PI / 2); return g };
  const CC = [0xf4f6fa, 0x1c2438, 0xb9c2d3, 0x2c5fd0, 0xc23030, 0x4a5568, 0x0f7a5a, 0xe8b13a];
  function car(t: string, c: number, g: any, x: number, y: number, z: number, ry: number) {
    const T = TY[t], q = new THREE.Group(), hw = T.W / 2, L2 = T.L / 2, B = body(c);
    if (!T.GB) { T.GB = ex(shp(T.b), T.W - .14, .07, T.L); T.GC = ex(shp(T.c), T.W - .5, .06, T.L); T.GT = new THREE.CylinderGeometry(T.wr, T.wr, .26, 24).rotateZ(PI / 2); T.GR = new THREE.CircleGeometry(T.wr * .72, 28).rotateY(PI / 2); T.GA = new THREE.CylinderGeometry(T.wr + .07, T.wr + .07, .02, 24).rotateZ(PI / 2) }
    q.position.set(x, y, z); q.rotation.y = ry; g.add(q);
    mk(T.GB, B, 0, 0, 0, q); mk(T.GC, glass, 0, 0, 0, q); mk(bx(T.W - .44, .05, T.rl), B, 0, T.ct + .045, T.rz, q); mk(bx(T.W - .4, (T.ct - T.bt) * .85, .09), B, 0, T.bt + (T.ct - T.bt) * .5, T.pz, q);
    [1, -1].forEach(s => T.ax.forEach((a: number) => { const wz = L2 - a, wx = s * (hw - .07); mk(T.GT, tire, wx, T.wr, wz, q); mk(T.GA, dark, s * (hw + .005), T.wr, wz, q, false); const r = mk(T.GR, rimM, s * (hw + .066), T.wr, wz, q, false); if (s < 0) r.rotation.y = PI }));
    [-1, 1].forEach(s => {
      mk(bx(.34, .1, .06), S(0xffffff, { emissive: 0xfff2c8, emissiveIntensity: 1.4 }), s * (hw - .45), T.hy, L2 + .06, q, false); if (t !== 'ev') mk(bx(.34, .1, .05), S(0xff2a2a, { emissive: 0xff1010, emissiveIntensity: 1 }), s * (hw - .4), T.bt - .25, -L2 - .06, q, false); mk(bx(.12, .1, .2), B, s * (hw + .06), T.bt + .08, L2 - 1.15, q, false);
      if (t === 'suv') mk(bx(.05, .05, T.rl + .4), dark, s * (hw - .3), T.ct + .09, T.rz, q, false)
    });
    if (t === 'ev') mk(bx(T.W - .5, .06, .05), S(0xff2a2a, { emissive: 0xff1010, emissiveIntensity: 1.2 }), 0, T.bt - .1, -L2 - .06, q, false);
    mk(bx(T.W * .35, .12, .05), dark, 0, T.hy - .2, L2 + .06, q, false); mk(bx(.5, .12, .03), lineM, 0, .5, -L2 - .07, q, false);
    const sd = new THREE.Mesh(new THREE.PlaneGeometry(T.W + .7, T.L + .7).rotateX(-PI / 2), shM); sd.position.y = .012; q.add(sd)
  }
  /* công trình */
  const fg: any = [], dg: any = [], hits: any = [];
  mk(bx(64, .3, 48), asph, CX, -.15, 0, sc, false); [-1, 1].forEach(o => mk(bx(40, .05, 9), lawn, CX, .02, o * 19, sc, false));
  const P = (s: ParkingSlot) => { const fi = FL.indexOf(s.f), o = s.r === 'A' ? -1 : 1; return { x: (s.n - 4.5) * BX, z: o * 5.75, y: B0 + fi * FH, o, fi } };
  FL.forEach((f, i) => {
    const g = new THREE.Group(), d = new THREE.Group(), y = B0 + i * FH, sh = i ? .25 : .3; g.add(d); fg.push(g); dg.push(d); sc.add(g);
    mk(bx(25.4, sh, 16.8), cm, 0, y - sh / 2, 0, g);
    [-1, 1].forEach(o => { mk(bx(25.4, .5, .3), cm, 0, y - .25, o * 8.3, g); mk(bx(25.4, .65, .2), pm2, 0, y + .325, o * 8.3, g); mk(bx(25.4, .05, .1), steel, 0, y + 1.1, o * 8.3, g); for (let k = 0; k <= 10; k++)mk(bx(.06, .45, .06), steel, -12.5 + k * 2.5, y + .875, o * 8.3, g) });
    for (let k = 0; k < 6; k++) { const x = -12.5 + k * 5; if (i) mk(bx(.35, .5, 16.5), cm, x, y - .5, 0, g); if (i < 2) [-1, 1].forEach(o => mk(bx(.5, FH - .25, .5), cm, x, y + (FH - .25) / 2, o * 8.1, g)) }
    const ch = i === 2 ? FH + 3 : FH; mk(bx(4.4, .25, 6.6), cm, -14.9, y - .125, 0, g); mk(bx(4.2, ch, 6.4), glassB, -14.9, y + ch / 2, 0, g, false); mk(bx(1.8, ch, 1.8), cm, -15.6, y + ch / 2, 0, g);
    [-1, 1].forEach(a => [-1, 0, 1].forEach(b => mk(bx(.1, ch, .1), steel, -14.9 + a * 2.1, y + ch / 2, b * 3.2, g, false))); if (i === 2) mk(bx(4.4, .25, 6.6), cm, -14.9, y + ch, 0, g);
    if (i === 2) [-1, 1].forEach(o => { for (let k = 0; k < 25; k++)mk(bx(.4, .05, 6), dark, -12 + k, y + 3.62, o * 5.75, g); [3.3, 8.2].forEach(zz => mk(bx(25.4, .15, .2), steel, 0, y + 3.5, o * zz, g)); [-12.5, 0, 12.5].forEach(x => mk(bx(.2, 3.6, .2), steel, x, y + 1.8, o * 8.3, g)) })
  });
  /* đường dốc xoắn ốc */
  const da = PI * 2 / 40, rm = 5.5, CXR = 20.95, pit = Math.atan(FH / (2 * PI * rm)), len = rm * da * 1.04;
  for (let j = 0; j < 80; j++) {
    const a = PI + (j + .5) * da, yT = B0 + (a - PI) / (2 * PI) * FH, s = new THREE.Group(); s.position.set(CXR + rm * Math.cos(a), yT, rm * Math.sin(a)); s.rotation.set(0, -a - PI / 2, pit);
    mk(bx(len, .3, 5.5), cm, 0, -.15, 0, s); mk(bx(len, 1.1, .22), pm2, 0, .55, -2.64, s); mk(bx(len, .9, .2), pm2, 0, .45, 2.64, s); fg[j < 40 ? 0 : 1].add(s)
  }
  [0, 1].forEach(k => mk(new THREE.CylinderGeometry(2.5, 2.5, FH, 28), cm, CXR, B0 + k * FH + FH / 2, 0, fg[k]));
  spots.forEach(s => {
    const p = P(s), g = fg[p.fi]; mk(bx(.07, .014, 4.9), lineM, p.x - 1.25, p.y + .008, p.z, g, false); if (s.n === 9) mk(bx(.07, .014, 4.9), lineM, p.x + 1.25, p.y + .008, p.z, g, false); mk(bx(2.5, .014, .07), lineM, p.x, p.y + .008, p.z + p.o * 2.45, g, false);
    if (s.r === 'A' && s.n % 2 === 0) mk(bx(1.2, .014, .1), lineM, p.x, p.y + .008, 0, g, false);
    const h = new THREE.Mesh(bx(2.4, 2.2, 4.9), new THREE.MeshBasicMaterial({ visible: false })); h.position.set(p.x, p.y + 1.1, p.z); h.userData = { id: s.id, fi: p.fi, x: p.x, y: p.y, z: p.z }; g.add(h); hits.push(h)
  });
  const PG = bx(2.3, .012, 4.8), CO = new THREE.ConeGeometry(.2, .5, 14), CB = new THREE.CylinderGeometry(.14, .14, .6, 14), QG = new THREE.PlaneGeometry(2.4, 4.9).rotateX(-PI / 2);
  const qm = (o: number) => new THREE.MeshBasicMaterial({ color: 0xb8f34a, transparent: true, opacity: o, depthWrite: false }), sq = new THREE.Mesh(QG, qm(.5)), hq = new THREE.Mesh(QG, qm(.28)); sq.visible = hq.visible = false; sc.add(sq, hq);
  const hl = new THREE.Mesh(new THREE.ConeGeometry(.4, .9, 18).rotateX(PI), S(0x19301d, { emissive: 0x6fcf2e, emissiveIntensity: .7 })); hl.visible = false; sc.add(hl);
  const TL = ['sedan', 'suv', 'ev', 'sedan', 'ev', 'suv', 'sedan', 'ev', 'suv'];
  
  let sel: string | null = null;
  const api = {
    rebuild() {
      sq.visible = hl.visible = false; dg.forEach((d: any) => d.clear()); spots.forEach(s => {
        const p = P(s), d = dg[p.fi], { x, z, y, o } = p;
        mk(PG, padM[s.s], x, y + .012, z, d, false); mk(bx(1.6, .13, .2), stopM, x, y + .065, z + o * 2.32, d); mk(bx(1.3, .03, .1), ledM[s.s], x, y + .145, z + o * 2.32, d, false);
        if (s.s === 'occupied') car(TL[s.i % 9], CC[(s.i * 3) % CC.length], d, x + ((s.i * 13) % 5 - 2) * .05, y, z - o * .2, (s.r === 'A' ? PI : 0) + ((s.i * 37) % 7 - 3) * .011);
        if (s.s === 'reserved') { mk(new THREE.PlaneGeometry(1.1, 1.1).rotateX(-PI / 2), rdM, x, y + .02, z - o * .6, d, false) }
        if (s.s === 'maintenance') { [-.8, .8].forEach(c => mk(CO, coneM, x + c, y + .25, z - o * 2.2, d)); mk(bx(1.6, .14, .02), hzM, x, y + .43, z - o * 2.2, d) }
        if (s.s === 'verify') mk(CB, S(0x8a5cf6, { emissive: 0x8a5cf6, emissiveIntensity: .8 }), x, y + .3, z, d);
        if (s.id === sel && fg[p.fi].visible) { sq.position.set(x, y + .03, z); sq.visible = true; hl.position.set(x, y + 2.6, z); hl.userData.by = y + 2.6; hl.visible = true }
      }); R.shadowMap.needsUpdate = true
    },
    setSpots(newSpots: ParkingSlot[]) {
      spots = newSpots;
      api.rebuild();
    },
    setFloor(f: string) {
      fg.forEach((g: any, i: number) => g.visible = f === 'all' || FL[i] === f); tg.set(CX, f === 'all' ? 4.2 : B0 + FL.indexOf(f) * FH + 1, 0); rad = f === 'all' ? 62 : 38; api.rebuild()
    },
    setSelection(id: string | null) {
      sel = id;
      api.rebuild();
    },
    dispose() {
      dead = true; ro.disconnect(); R.dispose()
    }
  };
  let th = .75, ph = 1, rad = 62, auto = true, dead = false, mv = 0, t = 0;
  const pos = () => { cam.position.set(tg.x + rad * Math.sin(ph) * Math.sin(th), tg.y + rad * Math.cos(ph), tg.z + rad * Math.sin(ph) * Math.cos(th)); cam.lookAt(tg) };
  const cv = R.domElement; let lx: number, ly: number, dn = false;
  const pick = (e: any) => { const r = cv.getBoundingClientRect(); rc.setFromCamera(new THREE.Vector2((e.clientX - r.left) / r.width * 2 - 1, -((e.clientY - r.top) / r.height) * 2 + 1), cam); const h = rc.intersectObjects(hits.filter((m: any) => fg[m.userData.fi].visible), false)[0]; return h ? h.object.userData : null };
  cv.onpointerdown = e => { dn = true; lx = e.clientX; ly = e.clientY; mv = 0; auto = false; hq.visible = false; cv.setPointerCapture(e.pointerId) };
  cv.onpointermove = e => {
    if (!dn) { const u = pick(e); hq.visible = !!u; cv.style.cursor = u ? 'pointer' : 'grab'; if (u) hq.position.set(u.x, u.y + .03, u.z); return }
    const dx = e.clientX - lx, dy = e.clientY - ly; mv += Math.abs(dx) + Math.abs(dy); th -= dx * .008; ph = Math.max(.25, Math.min(1.45, ph - dy * .006)); lx = e.clientX; ly = e.clientY
  };
  cv.onpointerup = e => { dn = false; if (mv < 6) { const u = pick(e); if (u) onPick(u.id) } };
  cv.onwheel = e => { e.preventDefault(); rad = Math.max(18, Math.min(100, rad + e.deltaY * .05)) };
  const ro = new ResizeObserver(() => { R.setSize(el.clientWidth, el.clientHeight); cam.aspect = el.clientWidth / el.clientHeight; cam.updateProjectionMatrix() }); ro.observe(el);
  const hint = document.createElement('div'); hint.className = 'absolute left-3 bottom-2 text-[11px] text-[#6a7491] pointer-events-none'; hint.textContent = 'Kéo để xoay · cuộn để phóng to · bấm vào ô để xem chi tiết'; el.appendChild(hint);
  (function loop() { if (dead) return; t += .04; if (auto) th += .0016; if (hl.visible) hl.position.y = hl.userData.by + Math.sin(t) * .15; pos(); R.render(sc, cam); requestAnimationFrame(loop) })(); return api
}
