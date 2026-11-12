"use client";

import { OrbitControls } from "@react-three/drei";
import { Canvas, useThree } from "@react-three/fiber";
import { useEffect } from "react";
import { parkingSpots, type ParkingSpot } from "./parkingLayoutData";

function CameraZoom({ zoom }: { zoom: number }) {
    const { camera, invalidate } = useThree();
    useEffect(() => {
        const scale = 1 / zoom;
        camera.position.set(5 * scale, 8 * scale, 9 * scale);
        camera.lookAt(0, 0, 0);
        camera.updateProjectionMatrix();
        invalidate();
    }, [camera, invalidate, zoom]);
    return null;
}

function ParkedCar({ color }: { color: string }) {
    return (
        <group position={[0, 0.15, 0]}>
            <mesh position={[0, 0.22, 0]} castShadow>
                <boxGeometry args={[0.72, 0.3, 1.27]} />
                <meshStandardMaterial
                    color={color}
                    metalness={0.18}
                    roughness={0.45}
                />
            </mesh>
            <mesh position={[0, 0.44, -0.08]} castShadow>
                <boxGeometry args={[0.61, 0.24, 0.64]} />
                <meshStandardMaterial
                    color="#253d50"
                    metalness={0.3}
                    roughness={0.3}
                />
            </mesh>
            {[-0.42, 0.42].flatMap((x) =>
                [-0.42, 0.42].map((z) => (
                    <mesh key={`${x}-${z}`} position={[x, 0.11, z]}>
                        <boxGeometry args={[0.12, 0.2, 0.23]} />
                        <meshStandardMaterial color="#182331" />
                    </mesh>
                )),
            )}
        </group>
    );
}

function Spot3D({
    spot,
    selected,
    onSelect,
}: {
    spot: ParkingSpot;
    selected: boolean;
    onSelect: (id: string) => void;
}) {
    const x = (spot.index - 5.5) * 1.22;
    const z = spot.side === "north" ? -2.55 : 2.55;
    const color = selected
        ? "#2877ed"
        : spot.status === "available"
          ? "#29bd68"
          : spot.status === "reserved"
            ? "#a6afbd"
            : "#e95755";
    return (
        <group position={[x, 0, z]}>
            <mesh
                position={[0, 0.028, 0]}
                rotation={[-Math.PI / 2, 0, 0]}
                onClick={(event) => {
                    event.stopPropagation();
                    if (spot.status === "available") onSelect(spot.id);
                }}
            >
                <planeGeometry args={[1.12, 2.12]} />
                <meshStandardMaterial
                    color={color}
                    transparent
                    opacity={selected ? 0.82 : 0.34}
                />
            </mesh>
            <mesh position={[-0.58, 0.035, 0]}>
                <boxGeometry args={[0.035, 0.02, 2.16]} />
                <meshBasicMaterial color="#f5f8fb" />
            </mesh>
            <mesh position={[0.58, 0.035, 0]}>
                <boxGeometry args={[0.035, 0.02, 2.16]} />
                <meshBasicMaterial color="#f5f8fb" />
            </mesh>
            {spot.status === "occupied" && (
                <ParkedCar
                    color={
                        spot.index % 3 === 0
                            ? "#e86b54"
                            : spot.index % 3 === 1
                              ? "#e8edf2"
                              : "#2c3947"
                    }
                />
            )}
            {selected && (
                <mesh position={[0, 0.08, 0]}>
                    <boxGeometry args={[1.16, 0.08, 2.18]} />
                    <meshBasicMaterial color="#1260df" wireframe />
                </mesh>
            )}
        </group>
    );
}

export default function ParkingLayout3D({
    selectedSpot,
    onSelectSpot,
    zoom,
}: {
    selectedSpot: string | null;
    onSelectSpot: (id: string) => void;
    zoom: number;
}) {
    return (
        <Canvas
            shadows
            dpr={[1, 1.5]}
            camera={{ position: [5, 8, 9], fov: 42, near: 0.1, far: 100 }}
            aria-label="Interactive 3D parking layout. Drag to rotate and scroll to zoom."
        >
            <color attach="background" args={["#e6eef4"]} />
            <ambientLight intensity={1.5} />
            <directionalLight
                position={[8, 15, 10]}
                intensity={2.2}
                castShadow
                shadow-mapSize={[1024, 1024]}
            />
            <CameraZoom zoom={zoom} />
            <OrbitControls
                target={[0, 0, 0]}
                minDistance={8}
                maxDistance={38}
                minPolarAngle={0.35}
                maxPolarAngle={1.45}
                enablePan={false}
            />
            <mesh position={[0, -0.16, 0]} receiveShadow>
                <boxGeometry args={[16.8, 0.3, 9.3]} />
                <meshStandardMaterial color="#b9c7ba" />
            </mesh>
            <mesh position={[0, 0.001, 0]} receiveShadow>
                <boxGeometry args={[15.3, 0.025, 8.2]} />
                <meshStandardMaterial color="#53626c" roughness={1} />
            </mesh>
            <mesh position={[0, 0.025, 0]} receiveShadow>
                <boxGeometry args={[15.2, 0.02, 2.05]} />
                <meshStandardMaterial color="#3e4e5a" />
            </mesh>
            {Array.from({ length: 8 }, (_, index) => (
                <mesh key={index} position={[(index - 3.5) * 1.82, 0.041, 0]}>
                    <boxGeometry args={[0.82, 0.012, 0.045]} />
                    <meshBasicMaterial color="#f0f5f6" />
                </mesh>
            ))}
            {parkingSpots.map((spot) => (
                <Spot3D
                    key={spot.id}
                    spot={spot}
                    selected={spot.id === selectedSpot}
                    onSelect={onSelectSpot}
                />
            ))}
            {[-1, 1].map((side) => (
                <mesh key={side} position={[0, 0.28, side * 4.14]} castShadow>
                    <boxGeometry args={[15.4, 0.56, 0.12]} />
                    <meshStandardMaterial color="#f5f8f8" />
                </mesh>
            ))}
            {[-7.85, 7.85].map((x) => (
                <mesh key={x} position={[x, 0.28, 0]} castShadow>
                    <boxGeometry args={[0.12, 0.56, 8.3]} />
                    <meshStandardMaterial color="#f5f8f8" />
                </mesh>
            ))}
        </Canvas>
    );
}
