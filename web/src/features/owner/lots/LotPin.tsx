"use client";

import React from "react";
import { ParkingLot } from "@/types/owner";

interface LotPinProps {
    lot: ParkingLot;
    isSelected: boolean;
    onClick: () => void;
}

export function LotPin({ lot, isSelected, onClick }: LotPinProps) {
    const bgColor =
        lot.ap === "approved"
            ? "#16a36a"
            : lot.ap === "pending"
              ? "#f59e0b"
              : "#e5484d";

    return (
        <button
            type="button"
            onClick={onClick}
            onKeyDown={(e) => {
                if (e.key === "Enter" || e.key === " ") {
                    e.preventDefault();
                    onClick();
                }
            }}
            aria-label={`Bãi xe ${lot.name}, trạng thái ${lot.ap === "approved" ? "Đang hoạt động" : lot.ap === "pending" ? "Chờ duyệt" : "Bị từ chối"}`}
            style={{
                border: "none",
                background: "none",
                padding: 0,
                cursor: "pointer",
                outline: "none",
                transform: isSelected ? "scale(1.35)" : "scale(1)",
                transition: "transform 0.15s ease",
                zIndex: isSelected ? 50 : 1,
            }}
        >
            <div
                style={{
                    width: 32,
                    height: 32,
                    borderRadius: "50%",
                    backgroundColor: bgColor,
                    display: "flex",
                    alignItems: "center",
                    justifyContent: "center",
                    color: "#fff",
                    fontWeight: 800,
                    fontSize: 14,
                    boxShadow: isSelected
                        ? "0 0 0 5px rgba(37,99,235,0.45), 0 6px 14px rgba(0,0,0,0.3)"
                        : "0 3px 8px rgba(0,0,0,0.2)",
                    border: "2px solid #fff",
                }}
            >
                P
            </div>
        </button>
    );
}
