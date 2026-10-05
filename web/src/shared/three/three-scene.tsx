"use client";

import { OrbitControls } from "@react-three/drei";
import { Canvas } from "@react-three/fiber";
import * as THREE from "three";

export default function ThreeScene() {
    const background = new THREE.Color("#f7f9fc");
    return (
        <Canvas aria-label="Technical 3D canvas">
            <color attach="background" args={[background]} />
            <ambientLight intensity={1} />
            <OrbitControls />
        </Canvas>
    );
}
