"use client";

import React, { useMemo } from "react";
import { useParkingStore } from "../../store/useParkingStore";
import { CarFactory } from "./CarFactory";
import { Html } from "@react-three/drei";
import { Color } from "three";

const STATUS_COLORS: Record<string, string> = {
  available: "#6fcf2e",
  assigned: "#2f80ed",
  occupied: "#e5484d",
  maintenance: "#f2a007",
  unavailable: "#a8b0a8",
  needs_verification: "#8a5cf6",
};

const SlotMarkers = React.memo(() => {
  const { slots, activeFloor, exploded, hoverSlot, hoverSlotId, selectSlot, selectedSlotId } = useParkingStore();

  const slotsList = useMemo(() => Object.values(slots), [slots]);

  return (
    <group>
      {slotsList.map((slot) => {
        const floorIdx = parseInt(slot.floorId.replace("f", "")) - 1;
        const isHidden = activeFloor !== "all" && activeFloor !== slot.floorId;
        
        if (isHidden || isNaN(floorIdx)) return null;

        const yOffset = exploded ? floorIdx * 5 : floorIdx * 3.2;
        
        // Map row and index to local coordinates
        // Row 0 is at z = -5, Row 1 is at z = 5
        // Index 0 to 9 map from x = -11.25 to x = 11.25 (2.5m spacing)
        const xPos = -11.25 + (slot.index * 2.5);
        const zPos = slot.row === 0 ? -5 : 5;
        const position: [number, number, number] = [xPos, yOffset + 0.125, zPos];
        const rotY = slot.row === 0 ? 0 : Math.PI;

        const isHovered = hoverSlotId === slot.id;
        const isSelected = selectedSlotId === slot.id;

        const ledColor = STATUS_COLORS[slot.status] || "#fff";

        return (
          <group key={slot.id} position={position}>
            {/* Bay lines (visual only) */}
            <mesh position={[0, 0, 0]} rotation={[-Math.PI / 2, 0, 0]} receiveShadow>
              <planeGeometry args={[2.4, 4.8]} />
              <meshStandardMaterial 
                color={new Color(ledColor)} 
                transparent 
                opacity={isSelected ? 0.6 : isHovered ? 0.3 : 0.05} 
                depthWrite={false}
              />
            </mesh>

            {/* LED Indicator (wheel stop) */}
            <mesh position={[0, 0.1, -2.2]} castShadow>
              <boxGeometry args={[1.5, 0.2, 0.2]} />
              <meshStandardMaterial color={new Color(ledColor)} emissive={new Color(ledColor)} emissiveIntensity={0.5} />
            </mesh>

            {/* Car Instance */}
            {slot.status === "occupied" && slot.vehicle && (
              <CarFactory 
                type={slot.vehicle.type || "sedan"} 
                position={[0, 0, 0]} 
                rotation={[0, rotY, 0]} 
                // Add a slight random color offset based on slot ID for variety
                color={`hsl(${(parseInt(slot.id.replace('slot-', '')) * 37) % 360}, 60%, 50%)`}
              />
            )}

            {/* Interaction Hitbox */}
            <mesh 
              position={[0, 1, 0]} 
              visible={false} 
              onPointerOver={(e) => { e.stopPropagation(); hoverSlot(slot.id); }}
              onPointerOut={(e) => { e.stopPropagation(); hoverSlot(null); }}
              onClick={(e) => { e.stopPropagation(); selectSlot(slot.id); }}
            >
              <boxGeometry args={[2.5, 2, 5]} />
              <meshBasicMaterial transparent opacity={0} />
            </mesh>

            {/* Tooltip on Hover */}
            {isHovered && (
              <Html position={[0, 2.5, 0]} center style={{ pointerEvents: 'none' }}>
                <div className="bg-owner-dark text-white text-xs px-2 py-1 rounded shadow-lg whitespace-nowrap">
                  {slot.code} - {slot.status}
                  {slot.vehicle && <div className="font-mono text-owner-primary">{slot.vehicle.plate}</div>}
                </div>
              </Html>
            )}
          </group>
        );
      })}
    </group>
  );
});

SlotMarkers.displayName = "SlotMarkers";

export { SlotMarkers };
