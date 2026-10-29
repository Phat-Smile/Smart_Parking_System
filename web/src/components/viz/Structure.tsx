"use client";

import React, { useMemo } from "react";
import { MeshStandardMaterial, Color } from "three";
import { useParkingStore } from "../../store/useParkingStore";

const Structure = React.memo(() => {
    const { activeFloor, exploded } = useParkingStore();

    const concreteMaterial = useMemo(
        () =>
            new MeshStandardMaterial({
                color: new Color("#e0e0e0"),
                roughness: 0.8,
                metalness: 0.1,
            }),
        [],
    );

    const asphaltMaterial = useMemo(
        () =>
            new MeshStandardMaterial({
                color: new Color("#333333"),
                roughness: 0.9,
                metalness: 0.0,
            }),
        [],
    );

    // Simplified geometry for 3 floors
    const floors = [0, 1, 2];

    return (
        <group>
            {/* Ground plinth */}
            <mesh
                position={[0, -0.15, 0]}
                receiveShadow
                material={asphaltMaterial}
            >
                <boxGeometry args={[40, 0.3, 30]} />
            </mesh>

            {floors.map((floorIdx) => {
                const floorId = `f${floorIdx + 1}`;
                const isHidden =
                    activeFloor !== "all" && activeFloor !== floorId;
                const yOffset = exploded ? floorIdx * 5 : floorIdx * 3.2;

                if (isHidden) return null;

                return (
                    <group key={floorIdx} position={[0, yOffset, 0]}>
                        {/* Slab */}
                        <mesh
                            position={[0, 0, 0]}
                            receiveShadow
                            castShadow
                            material={concreteMaterial}
                        >
                            <boxGeometry args={[30, 0.25, 20]} />
                        </mesh>

                        {/* Columns (4 corners for simplicity) */}
                        {floorIdx < 2 && (
                            <>
                                <mesh
                                    position={[-14, 1.6, -9]}
                                    castShadow
                                    material={concreteMaterial}
                                >
                                    <boxGeometry args={[0.5, 3.2, 0.5]} />
                                </mesh>
                                <mesh
                                    position={[14, 1.6, -9]}
                                    castShadow
                                    material={concreteMaterial}
                                >
                                    <boxGeometry args={[0.5, 3.2, 0.5]} />
                                </mesh>
                                <mesh
                                    position={[-14, 1.6, 9]}
                                    castShadow
                                    material={concreteMaterial}
                                >
                                    <boxGeometry args={[0.5, 3.2, 0.5]} />
                                </mesh>
                                <mesh
                                    position={[14, 1.6, 9]}
                                    castShadow
                                    material={concreteMaterial}
                                >
                                    <boxGeometry args={[0.5, 3.2, 0.5]} />
                                </mesh>
                            </>
                        )}
                    </group>
                );
            })}
        </group>
    );
});

Structure.displayName = "Structure";

export { Structure };
