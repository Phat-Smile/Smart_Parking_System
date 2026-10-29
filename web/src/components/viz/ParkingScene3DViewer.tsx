"use client";

import React, { useEffect, useRef, useState } from "react";
import { mk3D, ParkingScene3DHandle } from "./parkingScene3D";
import { useOwnerStore } from "../../store/useOwnerStore";

interface ParkingScene3DViewerProps {
    onSlotPick?: (slotId: string) => void;
    overrideFloor?: string;
}

function checkWebGLSupport(): boolean {
    if (typeof window === "undefined") return true;
    try {
        const canvas = document.createElement("canvas");
        return Boolean(
            canvas.getContext("webgl") ||
            canvas.getContext("experimental-webgl"),
        );
    } catch {
        return false;
    }
}

export function ParkingScene3DViewer({
    onSlotPick,
    overrideFloor,
}: ParkingScene3DViewerProps) {
    const containerRef = useRef<HTMLDivElement>(null);
    const handleRef = useRef<ParkingScene3DHandle | null>(null);
    const onSlotPickRef = useRef(onSlotPick);
    useEffect(() => {
        onSlotPickRef.current = onSlotPick;
    }, [onSlotPick]);

    const [hasWebGL] = useState<boolean>(() => checkWebGLSupport());

    const spots = useOwnerStore((s) => s.spots);
    const storeFloor = useOwnerStore((s) => s.activeFloor);
    const activeFloor = overrideFloor ?? storeFloor;
    const selectedId = useOwnerStore((s) => s.selectedId);
    const selectSpot = useOwnerStore((s) => s.selectSpot);

    useEffect(() => {
        const el = containerRef.current;
        if (!el || !hasWebGL) return;

        try {
            const handle = mk3D(
                el,
                spots,
                (id: string) => {
                    selectSpot(id);
                    if (onSlotPickRef.current) {
                        onSlotPickRef.current(id);
                    }
                },
                activeFloor,
                selectedId,
            );
            handleRef.current = handle;
        } catch (err: unknown) {
            console.error("Failed to initialize 3D scene:", err);
        }

        return () => {
            if (handleRef.current) {
                handleRef.current.dispose();
                handleRef.current = null;
            }
        };
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [hasWebGL]);

    // Update spots
    useEffect(() => {
        if (handleRef.current) {
            handleRef.current.setSpots(spots);
        }
    }, [spots]);

    // Update floor
    useEffect(() => {
        if (handleRef.current) {
            handleRef.current.setFloor(activeFloor);
        }
    }, [activeFloor]);

    // Update selection
    useEffect(() => {
        if (handleRef.current) {
            handleRef.current.setSelection(selectedId);
        }
    }, [selectedId]);

    if (!hasWebGL) {
        return (
            <div
                id="v3"
                style={{
                    display: "flex",
                    flexDirection: "column",
                    alignItems: "center",
                    justifyContent: "center",
                    gap: 12,
                    padding: 24,
                    textAlign: "center",
                }}
            >
                <p style={{ color: "#d23c42", fontWeight: 600 }}>
                    WebGL không khả dụng trên trình duyệt của bạn.
                </p>
                <p className="mut">Đang hiển thị chế độ 2D thay thế.</p>
            </div>
        );
    }

    return <div id="v3" ref={containerRef} />;
}

export default ParkingScene3DViewer;
