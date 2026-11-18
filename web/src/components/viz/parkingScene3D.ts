import * as THREE from "three";
import { ParkingSpot, ST, K, FLOORS, Floor } from "../../types/owner";

export type ParkingSlot = ParkingSpot;

export interface ParkingScene3DHandle {
    rebuild: () => void;
    setFloor: (floor: string) => void;
    setSpots: (spots: ParkingSpot[]) => void;
    setSelection: (id: string | null) => void;
    dispose: () => void;
}

export function mk3D(
    el: HTMLElement,
    initialSpots: ParkingSpot[],
    onPick: (id: string) => void,
    initialFloor: string = "all",
    initialSelectedId: string | null = null,
): ParkingScene3DHandle {
    let spots = initialSpots;
    let sel: string | null = initialSelectedId;
    const FL = [...FLOORS];

    const FH = 3.2,
        B0 = 0.3,
        BX = 2.5,
        CX = 8,
        PI = Math.PI;

    const R = new THREE.WebGLRenderer({
        antialias: true,
        alpha: true,
        powerPreference: "high-performance",
    });
    R.setPixelRatio(
        Math.min(
            typeof window !== "undefined" ? window.devicePixelRatio : 1,
            2,
        ),
    );

    // Color space handling
    if ("outputColorSpace" in R) {
        (R as unknown as { outputColorSpace: string }).outputColorSpace =
            THREE.SRGBColorSpace;
    } else {
        const legacyThree = THREE as Record<string, unknown>;
        if (typeof legacyThree.sRGBEncoding === "number") {
            (R as unknown as { outputEncoding: number }).outputEncoding =
                legacyThree.sRGBEncoding as number;
        }
    }
    R.toneMapping = THREE.ACESFilmicToneMapping;
    R.toneMappingExposure = 1;
    R.shadowMap.enabled = true;
    R.shadowMap.type = THREE.PCFShadowMap;
    R.shadowMap.autoUpdate = false;
    el.appendChild(R.domElement);

    const sc = new THREE.Scene(),
        cam = new THREE.PerspectiveCamera(36, 1, 0.5, 400),
        tg = new THREE.Vector3(CX, 4.2, 0),
        rc = new THREE.Raycaster();

    const tx = (
        w: number,
        h: number,
        fn: (g: CanvasRenderingContext2D, w: number, h: number) => void,
    ) => {
        const c = document.createElement("canvas");
        c.width = w;
        c.height = h;
        const ctx = c.getContext("2d");
        if (ctx) fn(ctx, w, h);
        const t = new THREE.CanvasTexture(c);
        if ("colorSpace" in t) {
            (t as unknown as { colorSpace: string }).colorSpace =
                THREE.SRGBColorSpace;
        } else {
            const legacyThree = THREE as Record<string, unknown>;
            if (typeof legacyThree.sRGBEncoding === "number") {
                (t as unknown as { encoding: number }).encoding =
                    legacyThree.sRGBEncoding as number;
            }
        }
        return t;
    };

    /* môi trường studio: bầu trời + softbox để sơn xe có phản chiếu */
    const et = tx(1024, 512, (g, w, h) => {
        const q = g.createLinearGradient(0, 0, 0, h);
        q.addColorStop(0, "#7fa9e0");
        q.addColorStop(0.45, "#f4f1ea");
        q.addColorStop(0.55, "#d8d8d6");
        q.addColorStop(1, "#8d8f88");
        g.fillStyle = q;
        g.fillRect(0, 0, w, h);
        g.fillStyle = "#fff";
        [
            [60, 60, 260, 60],
            [460, 40, 200, 90],
            [760, 70, 230, 50],
        ].forEach((r) => g.fillRect(r[0], r[1], r[2], r[3]));
    });
    et.mapping = THREE.EquirectangularReflectionMapping;
    const pm = new THREE.PMREMGenerator(R);
    sc.environment = pm.fromEquirectangular(et).texture;
    pm.dispose();

    sc.add(new THREE.HemisphereLight(0xeaf1ff, 0x8a8576, 0.5));
    const dl = new THREE.DirectionalLight(0xfff4e6, 1);
    dl.position.set(-30, 70, 45);
    dl.target.position.set(CX, 2, 0);
    sc.add(dl, dl.target);
    dl.castShadow = true;
    dl.shadow.mapSize.set(4096, 4096);
    Object.assign(dl.shadow.camera, {
        left: -64,
        right: 64,
        top: 52,
        bottom: -52,
        near: 10,
        far: 220,
    });
    dl.shadow.bias = -0.0004;
    dl.shadow.normalBias = 0.04;

    /* vật liệu */
    const conc = tx(256, 256, (g) => {
        g.fillStyle = "#d9d4c8";
        g.fillRect(0, 0, 256, 256);
        for (let i = 0; i < 2200; i++) {
            g.fillStyle = `rgba(${90 + ((Math.random() * 90) | 0)},${90 + ((Math.random() * 90) | 0)},${80 + ((Math.random() * 80) | 0)},.16)`;
            g.fillRect(Math.random() * 256, Math.random() * 256, 2, 2);
        }
    });
    conc.wrapS = conc.wrapT = THREE.RepeatWrapping;
    conc.repeat.set(8, 4);

    const S = (c: number | string, o?: THREE.MeshStandardMaterialParameters) =>
        new THREE.MeshStandardMaterial(
            Object.assign({ color: c, roughness: 0.8, metalness: 0 }, o || {}),
        );
    const bx = (w: number, h: number, d: number) =>
        new THREE.BoxGeometry(w, h, d);
    const mk = (
        geo: THREE.BufferGeometry,
        mat: THREE.Material,
        x: number,
        y: number,
        z: number,
        g: THREE.Group | THREE.Scene,
        sh?: boolean,
    ) => {
        const m = new THREE.Mesh(geo, mat);
        m.position.set(x, y, z);
        m.castShadow = sh !== false;
        m.receiveShadow = true;
        g.add(m);
        return m;
    };

    const cm = S(0xffffff, { map: conc, roughness: 0.92 }),
        pm2 = S(0xf3efe5, { map: conc, roughness: 0.9 }),
        steel = S(0x9aa3ad, { metalness: 0.9, roughness: 0.35 }),
        asph = S(0x1b1e22, { roughness: 0.96 }),
        lawn = S(0x5fa043, { roughness: 1 }),
        lineM = S(0xffffff, { roughness: 0.7 }),
        glassB = new THREE.MeshPhysicalMaterial({
            color: 0xa9c7d6,
            metalness: 0.2,
            roughness: 0.05,
            transparent: true,
            opacity: 0.3,
        }),
        glass = S(0x131a25, { metalness: 0.9, roughness: 0.05 }),
        tire = S(0x121418, { roughness: 0.95 }),
        dark = S(0x1a1d22, { roughness: 0.7 }),
        rimT = tx(128, 128, (g) => {
            g.fillStyle = "#16181d";
            g.fillRect(0, 0, 128, 128);
            g.fillStyle = "#cfd5df";
            g.beginPath();
            g.arc(64, 64, 62, 0, 7);
            g.fill();
            g.fillStyle = "#16181d";
            for (let i = 0; i < 5; i++) {
                const a = (i * PI * 2) / 5 + 0.3;
                g.beginPath();
                g.moveTo(64, 64);
                g.arc(64, 64, 52, a, a + 0.75);
                g.closePath();
                g.fill();
            }
            g.fillStyle = "#e8ecf2";
            g.beginPath();
            g.arc(64, 64, 14, 0, 7);
            g.fill();
        }),
        shT = tx(64, 64, (g) => {
            const q = g.createRadialGradient(32, 32, 4, 32, 32, 32);
            q.addColorStop(0, "rgba(0,0,0,.55)");
            q.addColorStop(1, "rgba(0,0,0,0)");
            g.fillStyle = q;
            g.fillRect(0, 0, 64, 64);
        }),
        hzT = tx(128, 32, (g) => {
            g.fillStyle = "#f2b705";
            g.fillRect(0, 0, 128, 32);
            g.fillStyle = "#111";
            for (let i = -1; i < 8; i++) {
                g.beginPath();
                g.moveTo(i * 20, 32);
                g.lineTo(i * 20 + 10, 32);
                g.lineTo(i * 20 + 26, 0);
                g.lineTo(i * 20 + 16, 0);
                g.fill();
            }
        }),
        rdT = tx(128, 128, (g) => {
            g.fillStyle = "#2f80ed";
            g.fillRect(8, 8, 112, 112);
            g.fillStyle = "#fff";
            g.font = "700 84px sans-serif";
            g.textAlign = "center";
            g.textBaseline = "middle";
            g.fillText("R", 64, 68);
        });

    const rimM = S(0xffffff, { map: rimT, metalness: 0.6, roughness: 0.35 }),
        shM = new THREE.MeshBasicMaterial({
            map: shT,
            transparent: true,
            depthWrite: false,
        }),
        hzM = S(0xffffff, { map: hzT }),
        rdM = new THREE.MeshBasicMaterial({ map: rdT }),
        coneM = S(0xff7a1a, { roughness: 0.5 }),
        stopM = S(0xbdb7aa);

    const padM: Record<string, THREE.Material> = {},
        ledM: Record<string, THREE.Material> = {};
    K.forEach((q) => {
        const c = ST[q][1];
        padM[q] = S(new THREE.Color(c).getHex(), {
            transparent: true,
            opacity: q === "occupied" ? 0.12 : 0.22,
            roughness: 0.6,
            depthWrite: false,
        });
        ledM[q] = S(new THREE.Color(c).getHex(), {
            emissive: c,
            emissiveIntensity: 1.6,
        });
    });

    /* 3 kiểu xe, kích thước thật: biên dạng bên hông được đùn và bo cạnh */
    type CurvePoint = [number, number] | [number, number, number, number];
    const shp = (a: CurvePoint[]) => {
        const s = new THREE.Shape();
        a.forEach((p, i) =>
            p.length === 2
                ? i
                    ? s.lineTo(p[0], p[1])
                    : s.moveTo(p[0], p[1])
                : s.quadraticCurveTo(p[0], p[1], p[2], p[3]),
        );
        s.closePath();
        return s;
    };

    interface VehicleTypeSpec {
        L: number;
        W: number;
        wr: number;
        ax: number[];
        bt: number;
        ct: number;
        hy: number;
        rl: number;
        rz: number;
        pz: number;
        b: CurvePoint[];
        c: CurvePoint[];
        GB?: THREE.BufferGeometry;
        GC?: THREE.BufferGeometry;
    }

    const TY: Record<string, VehicleTypeSpec> = {
        sedan: {
            L: 4.7,
            W: 1.85,
            wr: 0.34,
            ax: [0.95, 3.7],
            bt: 0.9,
            ct: 1.43,
            hy: 0.62,
            rl: 1.4,
            rz: 0.05,
            pz: -0.15,
            b: [
                [0.25, 0.3],
                [0.05, 0.38],
                [0, 0.62],
                [0.06, 0.8, 0.6, 0.86],
                [3.9, 0.9],
                [4.55, 0.88, 4.68, 0.68],
                [4.7, 0.42],
                [4.5, 0.3],
            ],
            c: [
                [1.05, 0.86],
                [1.55, 1.4, 2, 1.43],
                [3, 1.42],
                [3.55, 1.36, 3.95, 0.9],
            ],
        },
        suv: {
            L: 4.75,
            W: 1.95,
            wr: 0.38,
            ax: [0.95, 3.75],
            bt: 1.08,
            ct: 1.74,
            hy: 0.85,
            rl: 2.2,
            rz: -0.62,
            pz: -0.42,
            b: [
                [0.3, 0.38],
                [0.1, 0.45],
                [0, 0.75],
                [0.08, 0.98, 0.7, 1.05],
                [4.1, 1.08],
                [4.65, 1.05, 4.74, 0.85],
                [4.75, 0.5],
                [4.55, 0.38],
            ],
            c: [
                [1.1, 1.05],
                [1.45, 1.72, 1.9, 1.74],
                [4.1, 1.74],
                [4.55, 1.7, 4.6, 1.05],
            ],
        },
        ev: {
            L: 4.65,
            W: 1.9,
            wr: 0.36,
            ax: [0.85, 3.8],
            bt: 0.9,
            ct: 1.52,
            hy: 0.66,
            rl: 1.5,
            rz: -0.17,
            pz: -0.37,
            b: [
                [0.3, 0.3],
                [0.1, 0.36],
                [0, 0.58],
                [0.05, 0.78, 0.7, 0.84],
                [3.9, 0.9],
                [4.5, 0.88, 4.63, 0.7],
                [4.65, 0.42],
                [4.45, 0.3],
            ],
            c: [
                [1, 0.84],
                [1.7, 1.52, 2.4, 1.52],
                [3.4, 1.47, 4.35, 0.82],
            ],
        },
    };

    const ex = (sh: THREE.Shape, w: number, b: number, L: number) => {
        const g = new THREE.ExtrudeGeometry(sh, {
            depth: w,
            bevelEnabled: true,
            bevelThickness: b,
            bevelSize: b,
            bevelSegments: 3,
            curveSegments: 10,
        });
        g.translate(-L / 2, 0, -w / 2);
        g.rotateY(PI / 2);
        return g;
    };

    const CC = [
        0xf7f7f5, 0x2a6df0, 0xd92d2d, 0x1b1d22, 0x1fa35c, 0xff8a1f, 0xf7f7f5,
        0xd92d2d, 0x2a6df0, 0xff8a1f,
    ];

    /* ===== 500 chỗ: 5 tầng x 4 dãy x 25 ô ===== */
    const NF = FL.length,
        X0 = -31.25,
        RZ: Record<string, number> = { A: -14, B: -2.5, C: 2.5, D: 14 },
        RO: Record<string, number> = { A: -1, B: 1, C: -1, D: 1 },
        TL = ["sedan", "suv", "ev", "sedan", "ev", "suv", "sedan", "ev", "suv"],
        TS = ["sedan", "suv", "ev"];

    const P = (s: ParkingSpot) => ({
        x: X0 + (s.n + 0.5) * BX,
        z: RZ[s.r],
        y: B0 + FL.indexOf(s.f) * FH,
        o: RO[s.r],
        fi: FL.indexOf(s.f),
    });

    const mx = new THREE.Matrix4(),
        m2 = new THREE.Matrix4(),
        m3 = new THREE.Matrix4(),
        cl = new THREE.Color();

    const im = (
        geo: THREE.BufferGeometry,
        mat: THREE.Material,
        n: number,
        g?: THREE.Group | THREE.Scene,
        sh?: boolean,
    ) => {
        const m = new THREE.InstancedMesh(geo, mat, n);
        m.frustumCulled = false;
        m.count = 0;
        m.castShadow = sh !== false;
        m.receiveShadow = sh !== false;
        (g || sc).add(m);
        return m;
    };

    const st = (
        m: THREE.InstancedMesh,
        i: number,
        x: number,
        y: number,
        z: number,
        ry?: number,
    ) => {
        mx.makeRotationY(ry || 0).setPosition(x, y, z);
        m.setMatrixAt(i, mx);
    };

    let th = 0.75,
        ph = 1,
        rad = initialFloor === "all" ? 105 : 70,
        auto = true,
        dead = false,
        mv = 0,
        t = 0;

    const PM = new THREE.MeshPhysicalMaterial({
            color: 0xffffff,
            metalness: 0.3,
            roughness: 0.32,
            clearcoat: 1,
            clearcoatRoughness: 0.08,
        }),
        headM = S(0xffffff, { emissive: 0xfff2c8, emissiveIntensity: 1.4 }),
        tailM = S(0xff2a2a, { emissive: 0xff1010, emissiveIntensity: 1.1 }),
        purM = S(0x8a5cf6, { emissive: 0x8a5cf6, emissiveIntensity: 0.8 });

    const defs = (k: string) => {
        const T = TY[k],
            hw = T.W / 2,
            L2 = T.L / 2,
            w = T.ax.flatMap((a: number) =>
                [1, -1].map((s: number) => [s, L2 - a]),
            );
        T.GB = ex(shp(T.b), T.W - 0.14, 0.07, T.L);
        T.GC = ex(shp(T.c), T.W - 0.5, 0.06, T.L);
        type CarPartDef = [
            THREE.BufferGeometry,
            THREE.Material,
            number[][],
            number?,
        ];
        const D: CarPartDef[] = [
            [T.GB, PM, [[0, 0, 0, 0]]],
            [T.GC, glass, [[0, 0, 0, 0]]],
            [bx(T.W - 0.44, 0.05, T.rl), PM, [[0, T.ct + 0.045, T.rz, 0]]],
            [
                bx(T.W - 0.4, (T.ct - T.bt) * 0.85, 0.09),
                PM,
                [[0, T.bt + (T.ct - T.bt) * 0.5, T.pz, 0]],
            ],
            [
                new THREE.CylinderGeometry(T.wr, T.wr, 0.26, 24).rotateZ(
                    PI / 2,
                ),
                tire,
                w.map((q: number[]) => [q[0] * (hw - 0.07), T.wr, q[1], 0]),
            ],
            [
                new THREE.CylinderGeometry(
                    T.wr + 0.07,
                    T.wr + 0.07,
                    0.02,
                    24,
                ).rotateZ(PI / 2),
                dark,
                w.map((q: number[]) => [q[0] * (hw + 0.005), T.wr, q[1], 0]),
            ],
            [
                new THREE.CircleGeometry(T.wr * 0.72, 28).rotateY(PI / 2),
                rimM,
                w.map((q: number[]) => [
                    q[0] * (hw + 0.066),
                    T.wr,
                    q[1],
                    q[0] < 0 ? PI : 0,
                ]),
            ],
            [
                bx(0.34, 0.1, 0.06),
                headM,
                [-1, 1].map((s) => [s * (hw - 0.45), T.hy, L2 + 0.06, 0]),
            ],
            k === "ev"
                ? [
                      bx(T.W - 0.5, 0.06, 0.05),
                      tailM,
                      [[0, T.bt - 0.1, -L2 - 0.06, 0]],
                  ]
                : [
                      bx(0.34, 0.1, 0.05),
                      tailM,
                      [-1, 1].map((s) => [
                          s * (hw - 0.4),
                          T.bt - 0.25,
                          -L2 - 0.06,
                          0,
                      ]),
                  ],
            [bx(T.W * 0.35, 0.12, 0.05), dark, [[0, T.hy - 0.2, L2 + 0.06, 0]]],
            [bx(0.5, 0.12, 0.03), lineM, [[0, 0.5, -L2 - 0.07, 0]]],
            [
                bx(0.12, 0.1, 0.2),
                PM,
                [-1, 1].map((s) => [
                    s * (hw + 0.06),
                    T.bt + 0.08,
                    L2 - 1.15,
                    0,
                ]),
            ],
            [
                new THREE.PlaneGeometry(T.W + 0.7, T.L + 0.7).rotateX(-PI / 2),
                shM,
                [[0, 0.012, 0, 0]],
                1,
            ],
        ];
        if (k === "suv")
            D.push([
                bx(0.05, 0.05, T.rl + 0.4),
                dark,
                [-1, 1].map((s) => [s * (hw - 0.3), T.ct + 0.09, T.rz, 0]),
            ]);
        return D;
    };

    interface CarInstanceItem {
        d: [THREE.BufferGeometry, THREE.Material, number[][], number?];
        m: THREE.InstancedMesh;
    }
    const CI: Record<string, CarInstanceItem[]> = {};
    TS.forEach(
        (k) =>
            (CI[k] = defs(k).map((d) => {
                const m = im(d[0], d[1], 400 * d[2].length, undefined, !d[3]);
                m.count = 400 * d[2].length;
                if (d[1] === PM) m.setColorAt(0, cl.set(0xffffff));
                m.count = 0;
                return { d, m };
            })),
    );

    const CO = new THREE.ConeGeometry(0.2, 0.5, 14),
        CB = new THREE.CylinderGeometry(0.14, 0.14, 0.6, 14);
    const RD = im(
            new THREE.PlaneGeometry(1.1, 1.1).rotateX(-PI / 2),
            rdM,
            500,
            undefined,
            false,
        ),
        CN = im(CO, coneM, 1000),
        HZ = im(bx(1.6, 0.14, 0.02), hzM, 500, undefined, false),
        BC = im(CB, purM, 500);

    /* công trình */
    interface FloorPlaneGroup {
        PD: THREE.InstancedMesh;
        LD: THREE.InstancedMesh;
        fs: ParkingSpot[];
    }
    const fg: THREE.Group[] = [],
        FP: FloorPlaneGroup[] = [];
    mk(bx(110, 0.3, 76), asph, CX, -0.15, 0, sc, false);
    [-1, 1].forEach((o) =>
        mk(bx(86, 0.05, 12), lawn, CX, 0.02, o * 27, sc, false),
    );
    const tr = S(0x3f8f4a, { roughness: 0.9 }),
        tk = S(0x6b4f35);
    [-1, 1].forEach((o) => {
        for (let k = 0; k < 8; k++) {
            const x = -34 + k * 11,
                h = 2.4 + (k % 3) * 0.5,
                z = o * 27 + (((k * 7) % 3) - 1) * 1.5;
            mk(
                new THREE.CylinderGeometry(0.18, 0.25, h, 8),
                tk,
                x,
                h / 2,
                z,
                sc,
            );
            mk(
                new THREE.IcosahedronGeometry(1.5 + (k % 2) * 0.4, 1),
                tr,
                x,
                h + 1,
                z,
                sc,
            );
        }
    });

    const LG = bx(1.3, 0.03, 0.1),
        PG = bx(2.3, 0.012, 4.8),
        padB = new THREE.MeshBasicMaterial({
            transparent: true,
            opacity: 0.3,
            depthWrite: false,
        }),
        ledB = new THREE.MeshBasicMaterial();

    FL.forEach((f, i) => {
        const g = new THREE.Group(),
            y = B0 + i * FH,
            sh = i ? 0.25 : 0.3;
        sc.add(g);
        fg.push(g);
        mk(bx(64, sh, 34), cm, 0, y - sh / 2, 0, g);
        [-1, 1].forEach((o) => {
            mk(bx(64, 0.5, 0.3), cm, 0, y - 0.25, o * 16.8, g);
            mk(bx(64, 0.65, 0.2), pm2, 0, y + 0.325, o * 16.8, g);
            mk(bx(64, 0.05, 0.1), steel, 0, y + 1.1, o * 16.8, g);
        });
        const ps = im(bx(0.06, 0.45, 0.06), steel, 52, g);
        for (let k = 0; k < 26; k++)
            [-1, 1].forEach((o, j) =>
                st(ps, k * 2 + j, X0 + k * BX, y + 0.875, o * 16.8),
            );
        ps.count = 52;
        for (let k = 0; k <= 12; k++) {
            const x = X0 + k * 5;
            if (i) mk(bx(0.35, 0.5, 33.5), cm, x, y - 0.5, 0, g);
            if (i < NF - 1)
                [-16.2, 0, 16.2].forEach((z) =>
                    mk(
                        bx(0.5, FH - 0.25, 0.5),
                        cm,
                        x,
                        y + (FH - 0.25) / 2,
                        z,
                        g,
                    ),
                );
        }
        const ch = i === NF - 1 ? FH + 3 : FH;
        mk(bx(4.4, 0.25, 6.6), cm, -34.4, y - 0.125, 0, g);
        mk(bx(4.2, ch, 6.4), glassB, -34.4, y + ch / 2, 0, g, false);
        mk(bx(1.8, ch, 1.8), cm, -35.1, y + ch / 2, 0, g);
        [-1, 1].forEach((a) =>
            [-1, 0, 1].forEach((b) =>
                mk(
                    bx(0.1, ch, 0.1),
                    steel,
                    -34.4 + a * 2.1,
                    y + ch / 2,
                    b * 3.2,
                    g,
                    false,
                ),
            ),
        );
        if (i === NF - 1) {
            mk(bx(4.4, 0.25, 6.6), cm, -34.4, y + ch, 0, g);
            const cs2 = im(bx(0.4, 0.05, 5), dark, 256, g);
            let q = 0;
            [-14, -2.5, 2.5, 14].forEach((z) => {
                for (let k = 0; k < 64; k++)
                    st(cs2, q++, -31.5 + k, y + 3.62, z);
            });
            cs2.count = 256;
            [-16.2, -11.7, -4.8, 4.8, 11.7, 16.2].forEach((z) =>
                mk(bx(64, 0.15, 0.2), steel, 0, y + 3.5, z, g),
            );
            [-31.5, 0, 31.5].forEach((x) =>
                [-16.2, 16.2].forEach((z) =>
                    mk(bx(0.2, 3.6, 0.2), steel, x, y + 1.8, z, g),
                ),
            );
        }
        const fs = spots.filter((s) => s.f === f),
            L1 = im(bx(0.07, 0.014, 4.9), lineM, 104, g, false),
            Lc = im(bx(2.5, 0.014, 0.07), lineM, 100, g, false),
            PD = im(PG, padB, 100, g, false),
            LD = im(LG, ledB, 100, g, false),
            SP = im(bx(1.6, 0.13, 0.2), stopM, 100, g);
        let e = 100;
        fs.forEach((s, j) => {
            const p = P(s);
            st(L1, j, p.x - 1.25, y + 0.008, p.z);
            if (s.n === 24) st(L1, e++, p.x + 1.25, y + 0.008, p.z);
            st(Lc, j, p.x, y + 0.008, p.z + p.o * 2.45);
            st(PD, j, p.x, y + 0.014, p.z);
            st(LD, j, p.x, y + 0.145, p.z + p.o * 2.32);
            st(SP, j, p.x, y + 0.065, p.z + p.o * 2.32);
        });
        L1.count = 104;
        Lc.count = PD.count = LD.count = SP.count = 100;
        PD.setColorAt(0, cl.set(0xffffff));
        LD.setColorAt(0, cl);
        FP.push({ PD, LD, fs });
    });

    /* đường dốc xoắn ốc: 4 vòng cho 5 tầng */
    const NS = 32,
        da = (PI * 2) / NS,
        rm = 5.5,
        CXR = 40.45,
        pit = Math.atan(FH / (2 * PI * rm)),
        len = rm * da * 1.04;
    for (let j = 0; j < NS * (NF - 1); j++) {
        const a = PI + (j + 0.5) * da,
            yT = B0 + ((a - PI) / (2 * PI)) * FH,
            s = new THREE.Group();
        s.position.set(CXR + rm * Math.cos(a), yT, rm * Math.sin(a));
        s.rotation.set(0, -a - PI / 2, pit);
        mk(bx(len, 0.3, 5.5), cm, 0, -0.15, 0, s);
        mk(bx(len, 1.1, 0.22), pm2, 0, 0.55, -2.64, s);
        mk(bx(len, 0.9, 0.2), pm2, 0, 0.45, 2.64, s);
        fg[Math.floor(j / NS)].add(s);
    }
    for (let k = 0; k < NF - 1; k++)
        mk(
            new THREE.CylinderGeometry(2.5, 2.5, FH, 28),
            cm,
            CXR,
            B0 + k * FH + FH / 2,
            0,
            fg[k],
        );

    const QG = new THREE.PlaneGeometry(2.4, 4.9).rotateX(-PI / 2),
        qm = (o: number) =>
            new THREE.MeshBasicMaterial({
                color: 0x111111,
                transparent: true,
                opacity: o,
                depthWrite: false,
            }),
        sq = new THREE.Mesh(QG, qm(0.5)),
        hq = new THREE.Mesh(QG, qm(0.28));
    sq.visible = hq.visible = false;
    sc.add(sq, hq);
    const hl = new THREE.Mesh(
        new THREE.ConeGeometry(0.4, 0.9, 18).rotateX(PI),
        S(0x19301d, { emissive: 0x888888, emissiveIntensity: 0.7 }),
    );
    hl.visible = false;
    sc.add(hl);

    const api: ParkingScene3DHandle = {
        rebuild() {
            sq.visible = hl.visible = false;
            const n: Record<string, number> = { sedan: 0, suv: 0, ev: 0 };
            let nr = 0,
                nc = 0,
                nh = 0,
                nb = 0;

            fg.forEach((g, i) => {
                if (!g.visible) return;
                const F = FP[i];
                if (!F) return;
                F.fs.forEach((s: ParkingSpot, j: number) => {
                    cl.set(ST[s.s][1]);
                    F.PD.setColorAt(j, cl);
                    F.LD.setColorAt(j, cl);
                });
                if (F.PD.instanceColor) F.PD.instanceColor.needsUpdate = true;
                if (F.LD.instanceColor) F.LD.instanceColor.needsUpdate = true;
            });

            spots.forEach((s) => {
                const p = P(s);
                if (!fg[p.fi] || !fg[p.fi].visible) return;
                const x = p.x,
                    z = p.z,
                    y = p.y,
                    o = p.o;
                if (s.s === "occupied") {
                    const k = TL[s.i % 9],
                        idx = n[k]++;
                    m3.makeRotationY(
                        (o > 0 ? 0 : PI) + (((s.i * 37) % 7) - 3) * 0.011,
                    ).setPosition(
                        x + (((s.i * 13) % 5) - 2) * 0.05,
                        y,
                        z - o * 0.2,
                    );
                    cl.setHex(CC[(s.i * 7 + (s.i >> 2)) % CC.length]);
                    CI[k].forEach((q) => {
                        const np = q.d[2].length,
                            paint = q.d[1] === PM;
                        q.d[2].forEach((pl: number[], j: number) => {
                            m2.makeRotationY(pl[3]).setPosition(
                                pl[0],
                                pl[1],
                                pl[2],
                            );
                            mx.multiplyMatrices(m3, m2);
                            q.m.setMatrixAt(idx * np + j, mx);
                            if (paint) q.m.setColorAt(idx * np + j, cl);
                        });
                    });
                } else if (s.s === "reserved")
                    st(RD, nr++, x, y + 0.02, z - o * 0.6);
                else if (s.s === "maintenance") {
                    st(CN, nc++, x - 0.8, y + 0.25, z - o * 2.2);
                    st(CN, nc++, x + 0.8, y + 0.25, z - o * 2.2);
                    st(HZ, nh++, x, y + 0.43, z - o * 2.2);
                } else if (s.s === "verify") st(BC, nb++, x, y + 0.3, z);

                if (s.id === sel) {
                    sq.position.set(x, y + 0.03, z);
                    sq.visible = true;
                    hl.position.set(x, y + 2.6, z);
                    hl.userData.by = y + 2.6;
                    hl.visible = true;
                }
            });

            TS.forEach((k) =>
                CI[k].forEach((q) => {
                    q.m.count = n[k] * q.d[2].length;
                    q.m.instanceMatrix.needsUpdate = true;
                    if (q.m.instanceColor) q.m.instanceColor.needsUpdate = true;
                }),
            );
            RD.count = nr;
            CN.count = nc;
            HZ.count = nh;
            BC.count = nb;
            [RD, CN, HZ, BC].forEach((m) => {
                m.instanceMatrix.needsUpdate = true;
            });
            R.shadowMap.needsUpdate = true;
        },

        setFloor(f: string) {
            fg.forEach((g, i) => (g.visible = f === "all" || FL[i] === f));
            tg.set(
                CX,
                f === "all" ? 6.5 : B0 + FL.indexOf(f as Floor) * FH + 1,
                0,
            );
            rad = f === "all" ? 105 : 70;
            api.rebuild();
        },

        setSpots(newSpots: ParkingSpot[]) {
            spots = newSpots;
            api.rebuild();
        },

        setSelection(id: string | null) {
            sel = id;
            api.rebuild();
        },

        dispose() {
            dead = true;
            ro.disconnect();
            R.dispose();
            R.forceContextLoss();
            if (el.contains(R.domElement)) {
                el.removeChild(R.domElement);
            }
        },
    };

    const pos = () => {
        cam.position.set(
            tg.x + rad * Math.sin(ph) * Math.sin(th),
            tg.y + rad * Math.cos(ph),
            tg.z + rad * Math.sin(ph) * Math.cos(th),
        );
        cam.lookAt(tg);
    };

    const cv = R.domElement,
        RB: [string, number, number][] = [
            ["A", -16.5, -11.5],
            ["B", -5, 0],
            ["C", 0, 5],
            ["D", 11.5, 16.5],
        ];
    let lx = 0,
        ly = 0,
        dn = false;

    interface PickResult {
        id: string;
        x: number;
        y: number;
        z: number;
    }

    const pick = (e: PointerEvent): PickResult | null => {
        const r = cv.getBoundingClientRect();
        rc.setFromCamera(
            new THREE.Vector2(
                ((e.clientX - r.left) / r.width) * 2 - 1,
                -((e.clientY - r.top) / r.height) * 2 + 1,
            ),
            cam,
        );
        const o = rc.ray.origin,
            d = rc.ray.direction;
        let best: PickResult | null = null,
            bt = 1e9;
        for (let i = 0; i < fg.length; i++) {
            const g = fg[i];
            if (!g.visible || Math.abs(d.y) < 1e-4) continue;
            const y = B0 + i * FH,
                tt = (y + 0.8 - o.y) / d.y;
            if (tt <= 0 || tt >= bt) continue;
            const x = o.x + d.x * tt,
                z = o.z + d.z * tt,
                n = Math.floor((x - X0) / BX),
                rb = RB.find((q) => z >= q[1] && z < q[2]);
            if (rb && n >= 0 && n < 25) {
                bt = tt;
                best = {
                    id: FL[i] + "-" + rb[0] + (n + 1),
                    x: X0 + (n + 0.5) * BX,
                    y,
                    z: RZ[rb[0]],
                };
            }
        }
        return best;
    };

    cv.onpointerdown = (e) => {
        dn = true;
        lx = e.clientX;
        ly = e.clientY;
        mv = 0;
        auto = false;
        hq.visible = false;
        cv.setPointerCapture(e.pointerId);
    };

    cv.onpointermove = (e) => {
        if (!dn) {
            const u = pick(e);
            hq.visible = !!u;
            cv.style.cursor = u ? "pointer" : "grab";
            if (u) hq.position.set(u.x, u.y + 0.03, u.z);
            return;
        }
        const dx = e.clientX - lx,
            dy = e.clientY - ly;
        mv += Math.abs(dx) + Math.abs(dy);
        th -= dx * 0.008;
        ph = Math.max(0.25, Math.min(1.45, ph - dy * 0.006));
        lx = e.clientX;
        ly = e.clientY;
    };

    cv.onpointerup = (e) => {
        dn = false;
        if (mv < 6) {
            const u = pick(e);
            if (u) onPick(u.id);
        }
    };

    cv.onwheel = (e) => {
        e.preventDefault();
        rad = Math.max(25, Math.min(170, rad + e.deltaY * 0.08));
    };

    const ro = new ResizeObserver(() => {
        if (!el.clientWidth || !el.clientHeight) return;
        R.setSize(el.clientWidth, el.clientHeight);
        cam.aspect = el.clientWidth / el.clientHeight;
        cam.updateProjectionMatrix();
    });
    ro.observe(el);

    const hint = document.createElement("div");
    hint.className = "hint";
    hint.textContent =
        "Kéo để xoay · cuộn để phóng to · bấm vào ô để xem chi tiết";
    el.appendChild(hint);

    // Initialize
    api.setFloor(initialFloor);

    (function loop() {
        if (dead) return;
        t += 0.04;
        if (auto) th += 0.0012;
        if (hl.visible) hl.position.y = hl.userData.by + Math.sin(t) * 0.15;
        pos();
        R.render(sc, cam);
        requestAnimationFrame(loop);
    })();

    return api;
}
