"use client";

import { Canvas, useThree } from "@react-three/fiber";
import { useCallback, useEffect, useLayoutEffect, useRef } from "react";
import { Color, InstancedMesh, Object3D } from "three";
import type { DestinationScrollController } from "./useParkWiseLandingMotion";

type BoxInstance = {
    position: [number, number, number];
    scale: [number, number, number];
    color?: string;
};

const SPACE_Z = [-4, -2.4, -0.8, 0.8, 2.4, 4];
const SLOTS = [-3.55, 3.55].flatMap((x, side) =>
    SPACE_Z.map((z, index) => ({
        x,
        z,
        occupied: (side + index) % 2 === 0,
    })),
);
const CARS = SLOTS.filter((slot) => slot.occupied);
const CAR_COLORS = [
    "#eafe89",
    "#a8b8ad",
    "#748a7c",
    "#d6ded0",
    "#899a90",
    "#b6c989",
];

const SLOT_PADS: BoxInstance[] = SLOTS.map((slot) => ({
    position: [slot.x, 0.045, slot.z],
    scale: [2.63, 0.025, 1.46],
    color: slot.occupied ? "#314235" : "#426a3a",
}));

const PARKING_LINES: BoxInstance[] = [
    ...[-1, 1].flatMap((side) => [
        ...Array.from({ length: 7 }, (_, index) => ({
            position: [side * 3.55, 0.07, -4.8 + index * 1.6] as [
                number,
                number,
                number,
            ],
            scale: [2.74, 0.025, 0.035] as [number, number, number],
        })),
        {
            position: [side * 2.18, 0.07, 0] as [number, number, number],
            scale: [0.035, 0.025, 9.64] as [number, number, number],
        },
        {
            position: [side * 4.92, 0.07, 0] as [number, number, number],
            scale: [0.035, 0.025, 9.64] as [number, number, number],
        },
    ]),
    ...SPACE_Z.map((z) => ({
        position: [0, 0.075, z] as [number, number, number],
        scale: [0.055, 0.025, 0.62] as [number, number, number],
    })),
];

const CAR_BODIES: BoxInstance[] = CARS.map((car, index) => ({
    position: [car.x, 0.3, car.z],
    scale: [1.58, 0.33, 0.82],
    color: CAR_COLORS[index],
}));
const CAR_CABINS: BoxInstance[] = CARS.map((car) => ({
    position: [car.x - 0.08, 0.57, car.z],
    scale: [0.78, 0.23, 0.66],
}));
const CAR_WHEELS: BoxInstance[] = CARS.flatMap((car) =>
    [-0.5, 0.5].flatMap((x) =>
        [-0.42, 0.42].map((z) => ({
            position: [car.x + x, 0.15, car.z + z] as [number, number, number],
            scale: [0.22, 0.24, 0.12] as [number, number, number],
        })),
    ),
);
const CAR_SHADOWS: BoxInstance[] = CARS.map((car) => ({
    position: [car.x + 0.1, 0.075, car.z + 0.09],
    scale: [1.74, 0.008, 0.98],
}));

function setInstances(mesh: InstancedMesh | null, instances: BoxInstance[]) {
    if (!mesh) return;

    const object = new Object3D();
    const color = new Color();
    instances.forEach((instance, index) => {
        object.position.set(...instance.position);
        object.scale.set(...instance.scale);
        object.updateMatrix();
        mesh.setMatrixAt(index, object.matrix);
        if (instance.color) {
            mesh.setColorAt(index, color.set(instance.color));
        }
    });
    mesh.instanceMatrix.needsUpdate = true;
    if (mesh.instanceColor) mesh.instanceColor.needsUpdate = true;
}

function ParkingLot() {
    const pads = useRef<InstancedMesh>(null);
    const lines = useRef<InstancedMesh>(null);
    const bodies = useRef<InstancedMesh>(null);
    const cabins = useRef<InstancedMesh>(null);
    const wheels = useRef<InstancedMesh>(null);
    const carShadows = useRef<InstancedMesh>(null);

    useLayoutEffect(() => {
        setInstances(pads.current, SLOT_PADS);
        setInstances(lines.current, PARKING_LINES);
        setInstances(bodies.current, CAR_BODIES);
        setInstances(cabins.current, CAR_CABINS);
        setInstances(wheels.current, CAR_WHEELS);
        setInstances(carShadows.current, CAR_SHADOWS);
    }, []);

    return (
        <group>
            <mesh position={[0, -0.2, 0]}>
                <boxGeometry args={[12.8, 0.38, 11.35]} />
                <meshStandardMaterial color="#2b3b2d" roughness={0.95} />
            </mesh>
            <mesh position={[0, 0.005, 0]}>
                <boxGeometry args={[12.25, 0.025, 10.75]} />
                <meshStandardMaterial color="#202c23" roughness={1} />
            </mesh>
            <mesh position={[0, 0.025, 0]}>
                <boxGeometry args={[3.25, 0.026, 9.7]} />
                <meshStandardMaterial color="#28392b" roughness={1} />
            </mesh>
            <instancedMesh
                ref={pads}
                args={[undefined, undefined, SLOT_PADS.length]}
            >
                <boxGeometry args={[1, 1, 1]} />
                <meshStandardMaterial color="#ffffff" roughness={0.9} />
            </instancedMesh>
            <instancedMesh
                ref={lines}
                args={[undefined, undefined, PARKING_LINES.length]}
            >
                <boxGeometry args={[1, 1, 1]} />
                <meshBasicMaterial color="#b6c4af" />
            </instancedMesh>
            <instancedMesh
                ref={carShadows}
                args={[undefined, undefined, CAR_SHADOWS.length]}
            >
                <boxGeometry args={[1, 1, 1]} />
                <meshBasicMaterial
                    color="#050805"
                    transparent
                    opacity={0.38}
                    depthWrite={false}
                />
            </instancedMesh>
            <instancedMesh
                ref={wheels}
                args={[undefined, undefined, CAR_WHEELS.length]}
            >
                <boxGeometry args={[1, 1, 1]} />
                <meshStandardMaterial color="#0a0d0b" roughness={1} />
            </instancedMesh>
            <instancedMesh
                ref={bodies}
                args={[undefined, undefined, CAR_BODIES.length]}
            >
                <boxGeometry args={[1, 1, 1]} />
                <meshStandardMaterial
                    color="#ffffff"
                    roughness={0.58}
                    metalness={0.06}
                />
            </instancedMesh>
            <instancedMesh
                ref={cabins}
                args={[undefined, undefined, CAR_CABINS.length]}
            >
                <boxGeometry args={[1, 1, 1]} />
                <meshStandardMaterial
                    color="#425e50"
                    roughness={0.3}
                    metalness={0.08}
                />
            </instancedMesh>
        </group>
    );
}

function CameraMotion({
    signal,
    isMobile,
    isVisible,
}: {
    signal: DestinationScrollController;
    isMobile: boolean;
    isVisible: boolean;
}) {
    const { camera, invalidate } = useThree();
    const updateCamera = useCallback(
        (progress: number) => {
            const start = isMobile ? [9.5, 12, 16] : [11, 13, 17];
            const end = isMobile ? [7.7, 10.5, 13.5] : [8.4, 10.8, 14];
            camera.position.set(
                start[0] + (end[0] - start[0]) * progress,
                start[1] + (end[1] - start[1]) * progress,
                start[2] + (end[2] - start[2]) * progress,
            );
            camera.lookAt(0, 0, 0);
            if (isVisible) invalidate();
        },
        [camera, invalidate, isMobile, isVisible],
    );

    useEffect(() => {
        return signal.subscribe(updateCamera);
    }, [signal, updateCamera]);

    return null;
}

function SceneLifecycle({
    isVisible,
    onReady,
    onFailure,
}: {
    isVisible: boolean;
    onReady: () => void;
    onFailure: () => void;
}) {
    const { gl, invalidate } = useThree();

    useEffect(() => {
        const handleContextLoss = (event: Event) => {
            event.preventDefault();
            onFailure();
        };
        gl.domElement.addEventListener("webglcontextlost", handleContextLoss);
        return () =>
            gl.domElement.removeEventListener(
                "webglcontextlost",
                handleContextLoss,
            );
    }, [gl, onFailure]);

    useEffect(() => {
        if (!isVisible) return;
        const frame = requestAnimationFrame(() => {
            invalidate();
            onReady();
        });
        return () => cancelAnimationFrame(frame);
    }, [invalidate, isVisible, onReady]);

    return null;
}

export default function DestinationParkingScene({
    signal,
    isMobile,
    isVisible,
    onReady,
    onFailure,
}: {
    signal: DestinationScrollController;
    isMobile: boolean;
    isVisible: boolean;
    onReady: () => void;
    onFailure: () => void;
}) {
    return (
        <Canvas
            orthographic
            camera={{
                position: [11, 13, 17],
                zoom: isMobile ? 24 : 36,
                near: 0.1,
                far: 80,
            }}
            dpr={isMobile ? 1 : [1, 1.5]}
            frameloop={isVisible ? "demand" : "never"}
            gl={{
                alpha: true,
                antialias: !isMobile,
                powerPreference: "low-power",
                stencil: false,
            }}
        >
            <ambientLight intensity={1.15} />
            <directionalLight position={[5, 9, 7]} intensity={1.65} />
            <ParkingLot />
            <CameraMotion
                signal={signal}
                isMobile={isMobile}
                isVisible={isVisible}
            />
            <SceneLifecycle
                isVisible={isVisible}
                onReady={onReady}
                onFailure={onFailure}
            />
        </Canvas>
    );
}
