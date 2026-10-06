"use client";

import React, { useState } from "react";
import Image from "next/image";

interface StallData {
    id: string;
    label: string;
    type: string;
    status: "available" | "occupied";
    level: string;
    price: string;
    distance: string;
    cctvImage: string;
}

const defaultStall: StallData = {
    id: "A12",
    label: "A12",
    type: "Standard Bay",
    status: "available",
    level: "Level 2",
    price: "$3/hour",
    distance: "2 min from entrance",
    cctvImage: "/images/guest/cctv-a12.png",
};

export function GuestLiveParkingViewer() {
    const [viewMode, setViewMode] = useState<"2D" | "3D">("2D");
    const [selectedFloor, setSelectedFloor] = useState("L2");
    const [selectedStall, setSelectedStall] = useState<StallData>(defaultStall);

    const floors = [
        { id: "L4", label: "L4" },
        { id: "L3", label: "L3" },
        { id: "L2", label: "L2" },
        { id: "L1", label: "L1" },
        { id: "B1", label: "B1" },
    ];

    const floorSelectOptions: Record<string, string> = {
        L4: "Level 4",
        L3: "Level 3",
        L2: "Level 2",
        L1: "Level 1",
        B1: "Basement 1",
    };

    const handleFloorSelect = (floorId: string) => {
        setSelectedFloor(floorId);
    };

    const handleBayClick = (stall: Partial<StallData>) => {
        setSelectedStall({
            ...defaultStall,
            ...stall,
            level: floorSelectOptions[selectedFloor] || "Level 2",
        });
    };

    return (
        <section id="live-preview" className="py-2 scroll-mt-20">
            <div className="grid grid-cols-1 lg:grid-cols-12 gap-8 items-center">
                {/* Left Column (~40%): Copy & 3 Stat Cards */}
                <div className="lg:col-span-5 space-y-5">
                    <div className="w-8 h-1 rounded-full bg-[#7C5CFF]" />
                    <h2
                        id="liveTitle"
                        className="font-heading font-extrabold text-2xl sm:text-3xl lg:text-4xl text-[#111322] dark:text-white tracking-tight leading-tight transition-colors"
                    >
                        Live parking view
                        <br />
                        at your fingertips
                    </h2>
                    <p
                        id="liveDesc"
                        className="text-xs sm:text-sm text-[#4B5565] dark:text-[#8790A3] leading-relaxed transition-colors"
                    >
                        Explore 2D or 3D maps, see real-time availability, and
                        find the best spot with ease.
                    </p>

                    {/* 3 Stat Cards in a single balanced row */}
                    <div className="grid grid-cols-3 gap-3 pt-2 max-w-[430px]">
                        <div
                            id="statCard1"
                            className="p-3.5 rounded-xl border border-black/[0.08] dark:border-white/[0.08] bg-white dark:bg-[#0c0d18] shadow-sm flex flex-col justify-center min-h-[88px] transition-colors"
                        >
                            <div
                                id="statNum1"
                                className="font-heading font-extrabold text-2xl text-[#111322] dark:text-white leading-none"
                            >
                                156
                            </div>
                            <div
                                id="statLabel1"
                                className="text-[10px] text-[#7C8496] dark:text-[#8790A3] mt-2 font-medium"
                            >
                                Available spaces
                            </div>
                        </div>

                        <div
                            id="statCard2"
                            className="p-3.5 rounded-xl border border-black/[0.08] dark:border-white/[0.08] bg-white dark:bg-[#0c0d18] shadow-sm flex flex-col justify-center min-h-[88px] transition-colors"
                        >
                            <div
                                id="statNum2"
                                className="font-heading font-extrabold text-2xl text-[#111322] dark:text-white leading-none"
                            >
                                2 min
                            </div>
                            <div
                                id="statLabel2"
                                className="text-[10px] text-[#7C8496] dark:text-[#8790A3] mt-2 font-medium"
                            >
                                from entrance
                            </div>
                        </div>

                        <div
                            id="statCard3"
                            className="p-3.5 rounded-xl border border-black/[0.08] dark:border-white/[0.08] bg-white dark:bg-[#0c0d18] shadow-sm flex flex-col justify-center min-h-[88px] transition-colors"
                        >
                            <div
                                id="statLabel3"
                                className="text-[10px] text-[#7C8496] dark:text-[#8790A3] mt-2 font-medium"
                            >
                                From
                            </div>
                            <div
                                id="statNum3"
                                className="font-heading font-extrabold text-2xl text-[#111322] dark:text-white leading-none"
                            >
                                $3/hour
                            </div>
                        </div>
                    </div>
                </div>

                {/* Right Column (~60%): Interactive Top-down Parking Viewer */}
                <div className="lg:col-span-7">
                    <div
                        id="viewerMainCard"
                        className="rounded-2xl p-4 border border-black/[0.08] dark:border-white/[0.08] bg-white dark:bg-[#0c0d18] shadow-card-light dark:shadow-card-dark transition-colors"
                    >
                        {/* Top Toolbar: Segmented 2D/3D switcher & Floor Selector */}
                        <div
                            id="viewerHeader"
                            className="flex items-center justify-between pb-3.5 mb-3.5 border-b border-black/[0.06] dark:border-white/[0.07]"
                        >
                            <div
                                id="pill2D3D"
                                className="inline-flex p-0.5 rounded-lg bg-[#F4F5F8] dark:bg-[#101426] border border-black/[0.08] dark:border-white/[0.08] text-xs font-semibold"
                            >
                                <button
                                    type="button"
                                    id="btn2D"
                                    onClick={() => setViewMode("2D")}
                                    className={`px-3.5 py-1 rounded-lg font-medium transition-all cursor-pointer ${
                                        viewMode === "2D"
                                            ? "bg-[#7C5CFF] text-white shadow-sm"
                                            : "text-[#7C8496] dark:text-[#8790A3] hover:text-[#111322] dark:hover:text-white"
                                    }`}
                                >
                                    2D Map
                                </button>
                                <button
                                    type="button"
                                    id="btn3D"
                                    onClick={() => setViewMode("3D")}
                                    className={`px-3.5 py-1 rounded-lg font-medium transition-all cursor-pointer ${
                                        viewMode === "3D"
                                            ? "bg-[#7C5CFF] text-white shadow-sm"
                                            : "text-[#7C8496] dark:text-[#8790A3] hover:text-[#111322] dark:hover:text-white"
                                    }`}
                                >
                                    3D View
                                </button>
                            </div>

                            <div className="relative">
                                <label
                                    htmlFor="floorSelect"
                                    className="sr-only"
                                >
                                    Select Parking Floor
                                </label>
                                <select
                                    id="floorSelect"
                                    value={
                                        floorSelectOptions[selectedFloor] ||
                                        "Level 2"
                                    }
                                    onChange={(e) => {
                                        const found = Object.entries(
                                            floorSelectOptions,
                                        ).find(
                                            ([, val]) => val === e.target.value,
                                        );
                                        if (found) {
                                            setSelectedFloor(found[0]);
                                        }
                                    }}
                                    className="appearance-none pl-3 pr-7 py-1 rounded-lg text-xs font-semibold border border-black/[0.08] dark:border-white/[0.08] bg-[#F8F9FC] dark:bg-[#101426] text-[#111322] dark:text-white cursor-pointer focus:outline-none focus:border-brand-500"
                                >
                                    <option>Level 2</option>
                                    <option>Level 1</option>
                                    <option>Level 3</option>
                                    <option>Level 4</option>
                                    <option>Basement 1</option>
                                </select>
                                <div className="pointer-events-none absolute inset-y-0 right-0 flex items-center px-2.5 text-[#7C8496] dark:text-[#8790A3]">
                                    <svg
                                        className="w-3.5 h-3.5"
                                        fill="currentColor"
                                        viewBox="0 0 20 20"
                                        aria-hidden="true"
                                    >
                                        <path
                                            fillRule="evenodd"
                                            clipRule="evenodd"
                                            d="M5.293 7.293a1 1 0 011.414 0L10 10.586l3.293-3.293a1 1 0 111.414 1.414l-4 4a1 1 0 01-1.414 0l-4-4a1 1 0 010-1.414z"
                                        />
                                    </svg>
                                </div>
                            </div>
                        </div>

                        {/* Main Viewer Body: Map Area and Detail Panel */}
                        <div className="grid grid-cols-12 gap-3.5 items-stretch">
                            {/* Floor Nav Rail */}
                            <div
                                id="floorRail"
                                className="col-span-1 flex flex-col justify-between py-1 items-center text-xs font-bold"
                            >
                                {floors.map((floor) => (
                                    <button
                                        key={floor.id}
                                        type="button"
                                        onClick={() =>
                                            handleFloorSelect(floor.id)
                                        }
                                        className={`w-7 h-7 rounded-lg flex items-center justify-center transition-colors floor-btn cursor-pointer ${
                                            selectedFloor === floor.id
                                                ? "bg-[#7C5CFF] text-white shadow-glow-purple active-floor"
                                                : "text-[#7C8496] dark:text-[#8790A3] hover:text-[#111322] dark:hover:text-white"
                                        }`}
                                    >
                                        {floor.label}
                                    </button>
                                ))}
                            </div>

                            {/* Center Viewer: 2D Floor Plan OR 3D View */}
                            {viewMode === "2D" ? (
                                <div
                                    id="deckMapContainer"
                                    className="col-span-11 sm:col-span-6 rounded-xl p-2.5 border border-[#E2E6EF] dark:border-white/[0.08] bg-[#F3F4F8] dark:bg-[#070810] flex flex-col justify-between relative shadow-inner"
                                >
                                    <div className="flex flex-col justify-between h-full gap-2.5 my-auto">
                                        {/* Top Row of Parking Bays (A01 - A06) */}
                                        <div
                                            className="grid grid-cols-6 gap-1.5"
                                            id="topBayRow"
                                        >
                                            {/* Bay A01 (occupied sedan) */}
                                            <div
                                                onClick={() =>
                                                    handleBayClick({
                                                        id: "A01",
                                                        label: "A01",
                                                        status: "occupied",
                                                    })
                                                }
                                                className="h-16 rounded-md border border-dashed border-[#CBD2E1] dark:border-white/20 bg-[#E9ECF3] dark:bg-[#0e101a] p-1 flex flex-col justify-between relative group hover:border-zinc-400 dark:hover:border-zinc-500 transition-colors bay-box cursor-pointer"
                                            >
                                                <span className="text-[8px] font-mono text-[#7C8496] dark:text-zinc-500 leading-none bay-label">
                                                    A01
                                                </span>
                                                <div className="flex items-center justify-center my-auto">
                                                    <svg
                                                        className="w-6 h-9 drop-shadow"
                                                        fill="none"
                                                        viewBox="0 0 28 44"
                                                    >
                                                        <rect
                                                            className="car-body"
                                                            fill="#334155"
                                                            height="40"
                                                            rx="6"
                                                            stroke="#475569"
                                                            strokeWidth="1"
                                                            width="22"
                                                            x="3"
                                                            y="2"
                                                        />
                                                        <path
                                                            d="M5 11 C8 9, 20 9, 23 11 L21 18 C18 16, 10 16, 7 18 Z"
                                                            fill="#0f172a"
                                                        />
                                                        <rect
                                                            fill="#1e293b"
                                                            height="12"
                                                            rx="2"
                                                            width="14"
                                                            x="7"
                                                            y="19"
                                                        />
                                                        <circle
                                                            cx="6"
                                                            cy="4"
                                                            fill="#fbbf24"
                                                            r="1"
                                                        />
                                                        <circle
                                                            cx="22"
                                                            cy="4"
                                                            fill="#fbbf24"
                                                            r="1"
                                                        />
                                                    </svg>
                                                </div>
                                            </div>

                                            {/* Bay A02 (occupied SUV) */}
                                            <div
                                                onClick={() =>
                                                    handleBayClick({
                                                        id: "A02",
                                                        label: "A02",
                                                        status: "occupied",
                                                    })
                                                }
                                                className="h-16 rounded-md border border-dashed border-[#CBD2E1] dark:border-white/20 bg-[#E9ECF3] dark:bg-[#0e101a] p-1 flex flex-col justify-between relative group hover:border-zinc-400 dark:hover:border-zinc-500 transition-colors bay-box cursor-pointer"
                                            >
                                                <span className="text-[8px] font-mono text-[#7C8496] dark:text-zinc-500 leading-none bay-label">
                                                    A02
                                                </span>
                                                <div className="flex items-center justify-center my-auto">
                                                    <svg
                                                        className="w-6 h-9 drop-shadow"
                                                        fill="none"
                                                        viewBox="0 0 28 44"
                                                    >
                                                        <rect
                                                            className="car-body"
                                                            fill="#1e293b"
                                                            height="40"
                                                            rx="5"
                                                            stroke="#334155"
                                                            strokeWidth="1"
                                                            width="24"
                                                            x="2"
                                                            y="2"
                                                        />
                                                        <path
                                                            d="M5 10 L23 10 L21 17 L7 17 Z"
                                                            fill="#090d16"
                                                        />
                                                        <rect
                                                            fill="#0f172a"
                                                            height="15"
                                                            rx="2"
                                                            width="16"
                                                            x="6"
                                                            y="18"
                                                        />
                                                        <circle
                                                            cx="5"
                                                            cy="4"
                                                            fill="#60a5fa"
                                                            r="1.2"
                                                        />
                                                        <circle
                                                            cx="23"
                                                            cy="4"
                                                            fill="#60a5fa"
                                                            r="1.2"
                                                        />
                                                    </svg>
                                                </div>
                                            </div>

                                            {/* Bay A03 (Empty) */}
                                            <div
                                                onClick={() =>
                                                    handleBayClick({
                                                        id: "A03",
                                                        label: "A03",
                                                        status: "available",
                                                    })
                                                }
                                                className="h-16 rounded-md border border-dashed border-[#CBD2E1] dark:border-white/20 bg-white dark:bg-[#0a0b14] p-1 flex flex-col justify-between relative group hover:border-[#7C5CFF]/40 transition-colors bay-box bay-empty shadow-sm dark:shadow-none cursor-pointer"
                                            >
                                                <span className="text-[8px] font-mono text-[#7C8496] dark:text-zinc-500 leading-none bay-label">
                                                    A03
                                                </span>
                                                <div className="my-auto flex items-center justify-center">
                                                    <span className="text-[8px] font-mono text-[#7C8496] dark:text-zinc-500 free-tag">
                                                        FREE
                                                    </span>
                                                </div>
                                            </div>

                                            {/* Bay A04 (Empty) */}
                                            <div
                                                onClick={() =>
                                                    handleBayClick({
                                                        id: "A04",
                                                        label: "A04",
                                                        status: "available",
                                                    })
                                                }
                                                className="h-16 rounded-md border border-dashed border-[#CBD2E1] dark:border-white/20 bg-white dark:bg-[#0a0b14] p-1 flex flex-col justify-between relative group hover:border-[#7C5CFF]/40 transition-colors bay-box bay-empty shadow-sm dark:shadow-none cursor-pointer"
                                            >
                                                <span className="text-[8px] font-mono text-[#7C8496] dark:text-zinc-500 leading-none bay-label">
                                                    A04
                                                </span>
                                                <div className="my-auto flex items-center justify-center">
                                                    <span className="text-[8px] font-mono text-[#7C8496] dark:text-zinc-500 free-tag">
                                                        FREE
                                                    </span>
                                                </div>
                                            </div>

                                            {/* Bay A05 (Occupied silver car) */}
                                            <div
                                                onClick={() =>
                                                    handleBayClick({
                                                        id: "A05",
                                                        label: "A05",
                                                        status: "occupied",
                                                    })
                                                }
                                                className="h-16 rounded-md border border-dashed border-[#CBD2E1] dark:border-white/20 bg-[#E9ECF3] dark:bg-[#0e101a] p-1 flex flex-col justify-between relative group hover:border-zinc-400 dark:hover:border-zinc-500 transition-colors bay-box cursor-pointer"
                                            >
                                                <span className="text-[8px] font-mono text-[#7C8496] dark:text-zinc-500 leading-none bay-label">
                                                    A05
                                                </span>
                                                <div className="flex items-center justify-center my-auto">
                                                    <svg
                                                        className="w-6 h-9 drop-shadow"
                                                        fill="none"
                                                        viewBox="0 0 28 44"
                                                    >
                                                        <rect
                                                            className="car-body"
                                                            fill="#94a3b8"
                                                            height="40"
                                                            rx="6"
                                                            stroke="#cbd5e1"
                                                            strokeWidth="0.8"
                                                            width="22"
                                                            x="3"
                                                            y="2"
                                                        />
                                                        <path
                                                            d="M5 11 C8 9, 20 9, 23 11 L21 17 C18 15, 10 15, 7 17 Z"
                                                            fill="#334155"
                                                        />
                                                        <rect
                                                            fill="#475569"
                                                            height="13"
                                                            rx="2"
                                                            width="14"
                                                            x="7"
                                                            y="18"
                                                        />
                                                    </svg>
                                                </div>
                                            </div>

                                            {/* Bay A06 (Occupied dark sedan) */}
                                            <div
                                                onClick={() =>
                                                    handleBayClick({
                                                        id: "A06",
                                                        label: "A06",
                                                        status: "occupied",
                                                    })
                                                }
                                                className="h-16 rounded-md border border-dashed border-[#CBD2E1] dark:border-white/20 bg-[#E9ECF3] dark:bg-[#0e101a] p-1 flex flex-col justify-between relative group hover:border-zinc-400 dark:hover:border-zinc-500 transition-colors bay-box cursor-pointer"
                                            >
                                                <span className="text-[8px] font-mono text-[#7C8496] dark:text-zinc-500 leading-none bay-label">
                                                    A06
                                                </span>
                                                <div className="flex items-center justify-center my-auto">
                                                    <svg
                                                        className="w-6 h-9 drop-shadow"
                                                        fill="none"
                                                        viewBox="0 0 28 44"
                                                    >
                                                        <rect
                                                            className="car-body"
                                                            fill="#475569"
                                                            height="40"
                                                            rx="6"
                                                            stroke="#64748b"
                                                            strokeWidth="0.8"
                                                            width="22"
                                                            x="3"
                                                            y="2"
                                                        />
                                                        <path
                                                            d="M5 11 L23 11 L21 18 L7 18 Z"
                                                            fill="#1e293b"
                                                        />
                                                        <rect
                                                            fill="#0f172a"
                                                            height="12"
                                                            rx="2"
                                                            width="14"
                                                            x="7"
                                                            y="19"
                                                        />
                                                    </svg>
                                                </div>
                                            </div>
                                        </div>

                                        {/* Central Driving Lane */}
                                        <div
                                            id="drivewayLane"
                                            className="h-8 rounded-lg bg-[#EAEBF2] dark:bg-[#0b0d18] border border-[#DDE2EB] dark:border-white/5 flex items-center justify-between px-3 text-[#7C8496] dark:text-zinc-500 relative"
                                        >
                                            <div
                                                id="drivewayText1"
                                                className="flex items-center gap-1.5 font-mono text-[9px] text-[#4B5565] dark:text-[#8790A3] font-semibold"
                                            >
                                                <span className="text-[#7C5CFF]">
                                                    ▲
                                                </span>
                                                <span className="tracking-wider hidden sm:inline">
                                                    DRIVEWAY
                                                </span>
                                            </div>
                                            <div
                                                id="drivewayDivider"
                                                className="flex items-center gap-2 border-b border-dashed border-[#B8C0D1] dark:border-white/20 w-16 sm:w-24"
                                            />
                                            <div
                                                id="drivewayText2"
                                                className="flex items-center gap-1.5 font-mono text-[9px] text-[#4B5565] dark:text-[#8790A3] font-semibold"
                                            >
                                                <span className="tracking-wider hidden sm:inline">
                                                    EXIT
                                                </span>
                                                <span className="text-[#7C5CFF]">
                                                    ▼
                                                </span>
                                            </div>
                                        </div>

                                        {/* Bottom Row of Parking Bays (A09 - A14) */}
                                        <div
                                            className="grid grid-cols-6 gap-1.5"
                                            id="bottomBayRow"
                                        >
                                            {/* Bay A09 (Occupied) */}
                                            <div
                                                onClick={() =>
                                                    handleBayClick({
                                                        id: "A09",
                                                        label: "A09",
                                                        status: "occupied",
                                                    })
                                                }
                                                className="h-16 rounded-md border border-dashed border-[#CBD2E1] dark:border-white/20 bg-[#E9ECF3] dark:bg-[#0e101a] p-1 flex flex-col justify-between relative group hover:border-zinc-400 dark:hover:border-zinc-500 transition-colors bay-box cursor-pointer"
                                            >
                                                <span className="text-[8px] font-mono text-[#7C8496] dark:text-zinc-500 leading-none bay-label">
                                                    A09
                                                </span>
                                                <div className="flex items-center justify-center my-auto">
                                                    <svg
                                                        className="w-6 h-9 drop-shadow"
                                                        fill="none"
                                                        viewBox="0 0 28 44"
                                                    >
                                                        <rect
                                                            className="car-body"
                                                            fill="#27272a"
                                                            height="40"
                                                            rx="6"
                                                            stroke="#3f3f46"
                                                            strokeWidth="0.8"
                                                            width="22"
                                                            x="3"
                                                            y="2"
                                                        />
                                                        <path
                                                            d="M5 11 L23 11 L21 18 L7 18 Z"
                                                            fill="#18181b"
                                                        />
                                                        <rect
                                                            fill="#09090b"
                                                            height="12"
                                                            rx="2"
                                                            width="14"
                                                            x="7"
                                                            y="19"
                                                        />
                                                    </svg>
                                                </div>
                                            </div>

                                            {/* Bay A10 (Occupied blue car) */}
                                            <div
                                                onClick={() =>
                                                    handleBayClick({
                                                        id: "A10",
                                                        label: "A10",
                                                        status: "occupied",
                                                    })
                                                }
                                                className="h-16 rounded-md border border-dashed border-[#CBD2E1] dark:border-white/20 bg-[#E9ECF3] dark:bg-[#0e101a] p-1 flex flex-col justify-between relative group hover:border-zinc-400 dark:hover:border-zinc-500 transition-colors bay-box cursor-pointer"
                                            >
                                                <span className="text-[8px] font-mono text-[#7C8496] dark:text-zinc-500 leading-none bay-label">
                                                    A10
                                                </span>
                                                <div className="flex items-center justify-center my-auto">
                                                    <svg
                                                        className="w-6 h-9 drop-shadow"
                                                        fill="none"
                                                        viewBox="0 0 28 44"
                                                    >
                                                        <rect
                                                            className="car-body"
                                                            fill="#1e3a5f"
                                                            height="40"
                                                            rx="6"
                                                            stroke="#2563eb"
                                                            strokeWidth="0.8"
                                                            width="22"
                                                            x="3"
                                                            y="2"
                                                        />
                                                        <path
                                                            d="M5 11 L23 11 L21 18 L7 18 Z"
                                                            fill="#0f172a"
                                                        />
                                                        <rect
                                                            fill="#020617"
                                                            height="12"
                                                            rx="2"
                                                            width="14"
                                                            x="7"
                                                            y="19"
                                                        />
                                                    </svg>
                                                </div>
                                            </div>

                                            {/* Bay A11 (Occupied dark car) */}
                                            <div
                                                onClick={() =>
                                                    handleBayClick({
                                                        id: "A11",
                                                        label: "A11",
                                                        status: "occupied",
                                                    })
                                                }
                                                className="h-16 rounded-md border border-dashed border-[#CBD2E1] dark:border-white/20 bg-[#E9ECF3] dark:bg-[#0e101a] p-1 flex flex-col justify-between relative group hover:border-zinc-400 dark:hover:border-zinc-500 transition-colors bay-box cursor-pointer"
                                            >
                                                <span className="text-[8px] font-mono text-[#7C8496] dark:text-zinc-500 leading-none bay-label">
                                                    A11
                                                </span>
                                                <div className="flex items-center justify-center my-auto">
                                                    <svg
                                                        className="w-6 h-9 drop-shadow"
                                                        fill="none"
                                                        viewBox="0 0 28 44"
                                                    >
                                                        <rect
                                                            className="car-body"
                                                            fill="#3f3f46"
                                                            height="40"
                                                            rx="5"
                                                            stroke="#52525b"
                                                            strokeWidth="0.8"
                                                            width="24"
                                                            x="2"
                                                            y="2"
                                                        />
                                                        <path
                                                            d="M5 10 L23 10 L21 17 L7 17 Z"
                                                            fill="#18181b"
                                                        />
                                                        <rect
                                                            fill="#09090b"
                                                            height="14"
                                                            rx="2"
                                                            width="16"
                                                            x="6"
                                                            y="18"
                                                        />
                                                    </svg>
                                                </div>
                                            </div>

                                            {/* Bay A12: TARGET SPOTLIGHT STALL */}
                                            <div
                                                id="spotlightBay"
                                                onClick={() =>
                                                    handleBayClick(defaultStall)
                                                }
                                                className={`h-16 rounded-md border-2 border-[#7C5CFF] bg-[#7C5CFF]/15 shadow-glow-spot p-1 flex flex-col justify-between relative z-10 transition-transform cursor-pointer ${
                                                    selectedStall.id === "A12"
                                                        ? "scale-[1.03]"
                                                        : ""
                                                }`}
                                            >
                                                <div className="flex items-center justify-between">
                                                    <span
                                                        id="spotA12Header"
                                                        className="text-[9px] font-mono font-bold leading-none text-[#6246EA] dark:text-white"
                                                    >
                                                        A12
                                                    </span>
                                                    <span className="w-1.5 h-1.5 rounded-full bg-emerald-400 animate-ping" />
                                                </div>
                                                <div className="my-auto flex flex-col items-center justify-center">
                                                    <span
                                                        id="spotA12Text"
                                                        className="text-[9px] font-heading font-extrabold text-[#6246EA] dark:text-[#cabeff] tracking-wide"
                                                    >
                                                        A12
                                                    </span>
                                                    <div className="flex items-center gap-1 mt-0.5">
                                                        <span className="w-1.5 h-1.5 rounded-full bg-emerald-400" />
                                                        <span className="text-emerald-500 dark:text-emerald-400 font-bold text-[7px] uppercase tracking-wider">
                                                            Free
                                                        </span>
                                                    </div>
                                                </div>
                                            </div>

                                            {/* Bay A13 (Empty) */}
                                            <div
                                                onClick={() =>
                                                    handleBayClick({
                                                        id: "A13",
                                                        label: "A13",
                                                        status: "available",
                                                    })
                                                }
                                                className="h-16 rounded-md border border-dashed border-[#CBD2E1] dark:border-white/20 bg-white dark:bg-[#0a0b14] p-1 flex flex-col justify-between relative group hover:border-[#7C5CFF]/40 transition-colors bay-box bay-empty shadow-sm dark:shadow-none cursor-pointer"
                                            >
                                                <span className="text-[8px] font-mono text-[#7C8496] dark:text-zinc-500 leading-none bay-label">
                                                    A13
                                                </span>
                                                <div className="my-auto flex items-center justify-center">
                                                    <span className="text-[8px] font-mono text-[#7C8496] dark:text-zinc-500 free-tag">
                                                        FREE
                                                    </span>
                                                </div>
                                            </div>

                                            {/* Bay A14 (Occupied sedan) */}
                                            <div
                                                onClick={() =>
                                                    handleBayClick({
                                                        id: "A14",
                                                        label: "A14",
                                                        status: "occupied",
                                                    })
                                                }
                                                className="h-16 rounded-md border border-dashed border-[#CBD2E1] dark:border-white/20 bg-[#E9ECF3] dark:bg-[#0e101a] p-1 flex flex-col justify-between relative group hover:border-zinc-400 dark:hover:border-zinc-500 transition-colors bay-box cursor-pointer"
                                            >
                                                <span className="text-[8px] font-mono text-[#7C8496] dark:text-zinc-500 leading-none bay-label">
                                                    A14
                                                </span>
                                                <div className="flex items-center justify-center my-auto">
                                                    <svg
                                                        className="w-6 h-9 drop-shadow"
                                                        fill="none"
                                                        viewBox="0 0 28 44"
                                                    >
                                                        <rect
                                                            className="car-body"
                                                            fill="#334155"
                                                            height="40"
                                                            rx="6"
                                                            stroke="#475569"
                                                            strokeWidth="0.8"
                                                            width="22"
                                                            x="3"
                                                            y="2"
                                                        />
                                                        <path
                                                            d="M5 11 L23 11 L21 18 L7 18 Z"
                                                            fill="#1e293b"
                                                        />
                                                        <rect
                                                            fill="#0f172a"
                                                            height="12"
                                                            rx="2"
                                                            width="14"
                                                            x="7"
                                                            y="19"
                                                        />
                                                    </svg>
                                                </div>
                                            </div>
                                        </div>
                                    </div>

                                    {/* Directional Guidance / Entrance Footer */}
                                    <div
                                        id="entranceFooter"
                                        className="pt-2 mt-2 border-t border-[#DDE2EB] dark:border-white/[0.08] flex items-center justify-center gap-2 text-[9px] font-bold tracking-widest text-[#6246EA] dark:text-[#7C5CFF] uppercase transition-colors"
                                    >
                                        <span>⟵</span>
                                        <span>Entrance</span>
                                        <span>⟶</span>
                                    </div>
                                </div>
                            ) : (
                                /* 3D View Mode */
                                <div
                                    id="deck3DContainer"
                                    className="col-span-11 sm:col-span-6 rounded-xl p-2.5 border border-[#E2E6EF] dark:border-white/[0.08] bg-[#F3F4F8] dark:bg-[#070810] flex flex-col justify-between relative shadow-inner overflow-hidden min-h-[200px]"
                                >
                                    <div className="relative w-full h-[180px] rounded-lg overflow-hidden">
                                        <Image
                                            src="/images/guest/hero-isometric.png"
                                            alt="3D Smart Parking Garage Layout"
                                            fill
                                            sizes="(max-width: 639px) 100vw, (max-width: 1023px) 50vw, 30vw"
                                            className="object-cover object-center"
                                        />
                                        <div className="absolute inset-0 bg-gradient-to-t from-black/70 via-transparent to-black/20" />
                                        <div className="absolute bottom-2 left-2 right-2 flex items-center justify-between text-[10px] font-mono text-white bg-black/60 backdrop-blur-sm px-2.5 py-1 rounded-md">
                                            <span>
                                                3D DECK VIEW • {selectedFloor}
                                            </span>
                                            <span className="text-emerald-400">
                                                ● 156 SPOTS ACTIVE
                                            </span>
                                        </div>
                                    </div>
                                    <div className="pt-2 mt-2 border-t border-[#DDE2EB] dark:border-white/[0.08] flex items-center justify-center gap-2 text-[9px] font-bold tracking-widest text-[#6246EA] dark:text-[#7C5CFF] uppercase">
                                        <span>3D SPATIAL PREVIEW</span>
                                    </div>
                                </div>
                            )}

                            {/* Stall Detail Panel (~34% width) */}
                            <div
                                id="detailCard"
                                className="col-span-12 sm:col-span-5 rounded-xl p-3 border border-black/[0.08] dark:border-white/[0.08] bg-[#F8F9FC] dark:bg-[#101426] shadow-sm flex flex-col justify-between transition-colors"
                            >
                                <div>
                                    {/* CCTV Camera Snapshot */}
                                    <div
                                        id="cctvWrapper"
                                        className="rounded-xl overflow-hidden relative mb-3 bg-black h-28 border border-black/[0.08] dark:border-white/[0.08] group"
                                    >
                                        <Image
                                            src={selectedStall.cctvImage}
                                            alt="Security camera viewpoint of stall"
                                            fill
                                            sizes="(max-width: 639px) calc(100vw - 64px), (max-width: 1023px) 40vw, 260px"
                                            className="object-cover object-center group-hover:scale-105 transition-transform duration-300"
                                        />
                                        <div className="absolute inset-0 bg-gradient-to-t from-black/80 via-transparent to-black/30 pointer-events-none" />
                                        <span className="absolute top-1.5 left-1.5 flex items-center gap-1 bg-black/70 backdrop-blur-sm px-2 py-0.5 rounded text-[8px] font-mono text-emerald-400 border border-emerald-500/30">
                                            <span className="w-1.5 h-1.5 rounded-full bg-emerald-400 animate-ping" />
                                            REC • LIVE
                                        </span>
                                        <span className="absolute bottom-1.5 right-1.5 text-[9px] font-mono text-zinc-300 bg-black/70 backdrop-blur-sm px-1.5 py-0.5 rounded border border-white/10">
                                            Deck A • Cam 3
                                        </span>
                                    </div>

                                    {/* Spot Information Details */}
                                    <div className="space-y-2">
                                        <div className="flex items-center justify-between">
                                            <div className="flex items-center gap-2">
                                                <span
                                                    id="stallTitle"
                                                    className="font-heading font-extrabold text-base text-[#111322] dark:text-white"
                                                >
                                                    {selectedStall.label}
                                                </span>
                                                <span
                                                    id="stallBayType"
                                                    className="text-[10px] font-mono text-[#7C8496] dark:text-[#8790A3]"
                                                >
                                                    {selectedStall.type}
                                                </span>
                                            </div>
                                            <span
                                                id="stallStatusBadge"
                                                className={`px-2.5 py-0.5 rounded-full text-[10px] font-semibold border flex items-center gap-1.5 ${
                                                    selectedStall.status ===
                                                    "available"
                                                        ? "bg-[#EAFBF3] dark:bg-emerald-500/15 text-[#17A36B] dark:text-emerald-400 border-[#17A36B]/20 dark:border-emerald-500/30"
                                                        : "bg-red-50 dark:bg-red-500/15 text-red-600 dark:text-red-400 border-red-200 dark:border-red-500/30"
                                                }`}
                                            >
                                                <span
                                                    className={`w-1.5 h-1.5 rounded-full ${
                                                        selectedStall.status ===
                                                        "available"
                                                            ? "bg-[#17A36B] dark:bg-emerald-400"
                                                            : "bg-red-500"
                                                    }`}
                                                />
                                                {selectedStall.status ===
                                                "available"
                                                    ? "Available"
                                                    : "Occupied"}
                                            </span>
                                        </div>

                                        <div
                                            id="stallMeta"
                                            className="text-xs space-y-1.5 text-[#4B5565] dark:text-[#8790A3] pt-1"
                                        >
                                            <div className="flex items-center gap-2">
                                                <svg
                                                    className="w-3.5 h-3.5 text-[#7C5CFF]"
                                                    fill="none"
                                                    stroke="currentColor"
                                                    strokeWidth="2"
                                                    viewBox="0 0 24 24"
                                                    aria-hidden="true"
                                                >
                                                    <path
                                                        d="M17.657 16.657L13.414 20.9a1.998 1.998 0 01-2.827 0l-4.244-4.243a8 8 0 1111.314 0z"
                                                        strokeLinecap="round"
                                                        strokeLinejoin="round"
                                                    />
                                                </svg>
                                                <span id="stallLevelText">
                                                    {selectedStall.level}
                                                </span>
                                            </div>
                                            <div className="flex items-center gap-2">
                                                <svg
                                                    className="w-3.5 h-3.5 text-[#7C5CFF]"
                                                    fill="none"
                                                    stroke="currentColor"
                                                    strokeWidth="2"
                                                    viewBox="0 0 24 24"
                                                    aria-hidden="true"
                                                >
                                                    <path
                                                        d="M12 8c-1.657 0-3 .895-3 2s1.343 2 3 2 3 .895 3 2-1.343 2-3 2m0-8c1.11 0 2.08.402 2.599 1M12 8V7m0 1v8m0 0v1m0-1c-1.11 0-2.08-.402-2.599-1M21 12a9 9 0 11-18 0 9 9 0 0118 0z"
                                                        strokeLinecap="round"
                                                        strokeLinejoin="round"
                                                    />
                                                </svg>
                                                <span
                                                    id="stallPrice"
                                                    className="font-semibold text-[#111322] dark:text-white"
                                                >
                                                    {selectedStall.price}
                                                </span>
                                            </div>
                                            <div className="flex items-center gap-2">
                                                <svg
                                                    className="w-3.5 h-3.5 text-[#7C5CFF]"
                                                    fill="none"
                                                    stroke="currentColor"
                                                    strokeWidth="2"
                                                    viewBox="0 0 24 24"
                                                    aria-hidden="true"
                                                >
                                                    <path
                                                        d="M13 7h8m0 0v8m0-8l-8 8-4-4-6 6"
                                                        strokeLinecap="round"
                                                        strokeLinejoin="round"
                                                    />
                                                </svg>
                                                <span id="stallDistanceText">
                                                    {selectedStall.distance}
                                                </span>
                                            </div>
                                        </div>
                                    </div>
                                </div>

                                {/* Action Button */}
                                <button
                                    type="button"
                                    onClick={() => {
                                        // Visual demo only - no unauthenticated booking backend call
                                    }}
                                    className="mt-4 w-full py-2.5 rounded-xl font-semibold text-xs text-white shadow-glow-purple bg-gradient-to-r from-brand-500 to-brand-600 hover:from-brand-400 hover:to-brand-700 transition-all active:scale-95 flex items-center justify-center gap-1.5 cursor-pointer"
                                >
                                    <span>View Details</span>
                                    <svg
                                        className="w-3.5 h-3.5"
                                        fill="none"
                                        stroke="currentColor"
                                        strokeWidth="2.5"
                                        viewBox="0 0 24 24"
                                        aria-hidden="true"
                                    >
                                        <path
                                            d="M14 5l7 7m0 0l-7 7m7-7H3"
                                            strokeLinecap="round"
                                            strokeLinejoin="round"
                                        />
                                    </svg>
                                </button>
                            </div>
                        </div>
                    </div>
                </div>
            </div>
        </section>
    );
}
