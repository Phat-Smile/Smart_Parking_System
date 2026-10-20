"use client";

import React, { useMemo } from "react";
import { MeshStandardMaterial, MeshPhysicalMaterial, Color } from "three";

// In a real implementation, we would load GLB files using useGLTF.
// For this procedural version, we create detailed boxes to represent cars.
// sedan: 4.7 x 1.85 x 1.45
// suv: 4.75 x 1.95 x 1.75
// ev: 4.65 x 1.9 x 1.52

interface CarProps {
  type: string;
  color?: string;
  position: [number, number, number];
  rotation: [number, number, number];
}

const CarFactory = React.memo(({ type, color = "#ffffff", position, rotation }: CarProps) => {
  const dimensions = useMemo(() => {
    switch (type) {
      case "suv": return [1.95, 1.75, 4.75]; // [width, height, length]
      case "ev": return [1.9, 1.52, 4.65];
      case "sedan":
      default: return [1.85, 1.45, 4.7];
    }
  }, [type]);

  const bodyMaterial = useMemo(() => new MeshPhysicalMaterial({
    color: new Color(color),
    roughness: 0.2,
    metalness: 0.6,
    clearcoat: 1.0,
    clearcoatRoughness: 0.1,
  }), [color]);

  const glassMaterial = useMemo(() => new MeshStandardMaterial({
    color: new Color("#111"),
    roughness: 0.1,
    metalness: 0.9,
    transparent: true,
    opacity: 0.8,
  }), []);

  return (
    <group position={position} rotation={rotation}>
      {/* Main Body */}
      <mesh position={[0, dimensions[1] / 3, 0]} material={bodyMaterial} castShadow receiveShadow>
        <boxGeometry args={[dimensions[0], dimensions[1] * 0.4, dimensions[2]]} />
      </mesh>
      
      {/* Greenhouse (Roof + Glass) */}
      <mesh position={[0, dimensions[1] * 0.75, 0]} material={glassMaterial} castShadow>
        <boxGeometry args={[dimensions[0] * 0.8, dimensions[1] * 0.35, dimensions[2] * 0.5]} />
      </mesh>
      <mesh position={[0, dimensions[1] * 0.95, 0]} material={bodyMaterial} castShadow>
        <boxGeometry args={[dimensions[0] * 0.8, 0.05, dimensions[2] * 0.4]} />
      </mesh>

      {/* Wheels */}
      {[-1, 1].map((x) =>
        [-1, 1].map((z) => (
          <mesh key={`${x}-${z}`} position={[x * (dimensions[0] / 2 - 0.1), 0.3, z * (dimensions[2] / 3)]} castShadow>
            <cylinderGeometry args={[0.3, 0.3, 0.2, 16]} />
            <meshStandardMaterial color="#222" roughness={0.8} />
          </mesh>
        ))
      )}
    </group>
  );
});

CarFactory.displayName = "CarFactory";

export { CarFactory };
