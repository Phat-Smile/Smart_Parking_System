
"use client";

import Link from "next/link";
import { useParams } from "next/navigation";

type ParkingLot = {
    id: string;
    name: string;
    owner: string;
    email: string;
    phone: string;
    submittedAt: string;
    status: string;
    address: string;
    latitude: string;
    longitude: string;
    operatingHours: string;
    vehicleTypes: string[];
    capacity: number;
    availableCapacity: number;
    zones: { name: string; capacity: number; type: string }[];
    pricing: { vehicle: string; firstHour: string; nextHour: string }[];
    holdDeposit: string;
    reservationPolicy: string[];
    layoutType: string;
    layoutDescription: string;
};

// Dữ liệu demo cho giao diện.
// Khi tích hợp backend, thay bằng API lấy parking lot theo ID.
const demoParkingLot: ParkingLot = {
    id: "PL-001",
    name: "SmartPark Central",
    owner: "Nguyen Van An",
    email: "owner@example.com",
    phone: "0900 123 456",
    submittedAt: "12 Oct 2026",
    status: "Pending Review",
    address: "123 Nguyen Van Linh, District 7, Ho Chi Minh City",
    latitude: "10.732800",
    longitude: "106.701200",
    operatingHours: "06:00 AM – 10:00 PM",
    vehicleTypes: ["Motorcycle", "Car"],
    capacity: 150,
    availableCapacity: 150,
    zones: [
        { name: "Zone A", capacity: 60, type: "Motorcycle" },
        { name: "Zone B", capacity: 50, type: "Car" },
        { name: "Zone C", capacity: 40, type: "Mixed" },
    ],
    pricing: [
        {
            vehicle: "Motorcycle",
            firstHour: "5,000 VND",
            nextHour: "3,000 VND",
        },
        {
            vehicle: "Car",
            firstHour: "20,000 VND",
            nextHour: "10,000 VND",
        },
    ],
    holdDeposit: "50,000 VND",
    reservationPolicy: [
        "Reservation is subject to parking capacity.",
        "The reservation holder must arrive within the allowed time window.",
        "Deposit and refund conditions follow the configured policy.",
    ],
    layoutType: "2D layout",
    layoutDescription: "Parking zone layout preview",
};

function Section({
    title,
    description,
    children,
}: {
    title: string;
    description?: string;
    children: React.ReactNode;
}) {
    return (
        <section className="rounded-2xl border border-slate-200 bg-white shadow-sm">
            <div className="border-b border-slate-100 px-5 py-4 sm:px-6">
                <h2 className="text-base font-bold text-slate-900">{title}</h2>
                {description && (
                    <p className="mt-1 text-sm text-slate-500">{description}</p>
                )}
            </div>
            <div className="p-5 sm:p-6">{children}</div>
        </section>
    );
}

function InfoItem({
    label,
    value,
}: {
    label: string;
    value: React.ReactNode;
}) {
    return (
        <div className="min-w-0 rounded-xl bg-slate-50 p-4">
            <p className="text-xs font-medium uppercase tracking-wide text-slate-500">
                {label}
            </p>
            <div className="mt-2 break-words text-sm font-semibold text-slate-800">
                {value}
            </div>
        </div>
    );
}

export default function ParkingLotApprovalDetailPage() {
    const params = useParams<{ id: string }>();
    const routeId = decodeURIComponent(params.id);

    // Replace this demo lookup with the backend response.
    const parkingLot = {
        ...demoParkingLot,
        id: routeId,
    };

    return (
        <main className="min-h-screen bg-slate-50 text-slate-900">
            {/* Page header */}
            <header className="border-b border-slate-200 bg-white">
                <div className="mx-auto max-w-7xl px-4 py-5 sm:px-6 lg:px-8">
                    <Link
                        href="/admin/parking-lots/pending"
                        className="inline-flex items-center gap-2 text-sm font-medium text-slate-500 hover:text-blue-600"
                    >
                        <span aria-hidden="true">←</span>
                        Back to Pending Parking Lots
                    </Link>

                    <div className="mt-5 flex flex-col justify-between gap-4 md:flex-row md:items-center">
                        <div>
                            <div className="flex flex-wrap items-center gap-3">
                                <h1 className="text-2xl font-bold tracking-tight sm:text-3xl">
                                    Parking Lot Approval Detail
                                </h1>
                                <span className="rounded-full border border-amber-200 bg-amber-50 px-3 py-1 text-xs font-semibold text-amber-700">
                                    {parkingLot.status}
                                </span>
                            </div>
                            <p className="mt-2 text-sm text-slate-500">
                                Review all submitted information before making an approval
                                decision.
                            </p>
                        </div>

                        <div className="flex gap-3">
                            <span className="rounded-lg border border-slate-200 bg-white px-4 py-2 text-sm font-medium text-slate-600">
                                ID: {parkingLot.id}
                            </span>
                        </div>
                    </div>
                </div>
            </header>

            <div className="mx-auto max-w-7xl space-y-6 px-4 py-6 sm:px-6 lg:px-8">
                {/* Overview */}
                <section className="overflow-hidden rounded-2xl border border-slate-200 bg-white shadow-sm">
                    <div className="bg-slate-900 px-6 py-6 text-white sm:px-8">
                        <div className="flex flex-col justify-between gap-5 sm:flex-row sm:items-center">
                            <div>
                                <p className="text-sm text-slate-300">
                                    Parking lot application
                                </p>
                                <h2 className="mt-2 text-2xl font-bold">
                                    {parkingLot.name}
                                </h2>
                                <p className="mt-2 text-sm text-slate-300">
                                    Submitted on {parkingLot.submittedAt}
                                </p>
                            </div>

                            <div className="rounded-xl border border-white/15 bg-white/10 p-4">
                                <p className="text-xs text-slate-300">Total capacity</p>
                                <p className="mt-1 text-3xl font-bold">
                                    {parkingLot.capacity}
                                    <span className="ml-2 text-sm font-normal text-slate-300">
                                        vehicles
                                    </span>
                                </p>
                            </div>
                        </div>
                    </div>

                    <div className="grid gap-4 p-5 sm:grid-cols-3 sm:p-6">
                        <InfoItem
                            label="Application ID"
                            value={parkingLot.id}
                        />
                        <InfoItem
                            label="Application status"
                            value={parkingLot.status}
                        />
                        <InfoItem
                            label="Operating hours"
                            value={parkingLot.operatingHours}
                        />
                    </div>
                </section>

                {/* Parking information */}
                <Section
                    title="1. Parking Information"
                    description="Basic information submitted by the parking lot owner."
                >
                    <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
                        <InfoItem label="Parking name" value={parkingLot.name} />
                        <InfoItem label="Owner name" value={parkingLot.owner} />
                        <InfoItem label="Owner email" value={parkingLot.email} />
                        <InfoItem label="Contact phone" value={parkingLot.phone} />
                        <InfoItem
                            label="Submission date"
                            value={parkingLot.submittedAt}
                        />
                        <InfoItem
                            label="Current status"
                            value={parkingLot.status}
                        />
                    </div>
                </Section>

                {/* Address and GPS */}
                <Section
                    title="2. Address & GPS Location"
                    description="Verify the submitted address and geographic coordinates."
                >
                    <div className="grid gap-4 md:grid-cols-2">
                        <InfoItem label="Full address" value={parkingLot.address} />
                        <div className="grid grid-cols-2 gap-3">
                            <InfoItem label="Latitude" value={parkingLot.latitude} />
                            <InfoItem label="Longitude" value={parkingLot.longitude} />
                        </div>
                    </div>

                    <a
                        href={`https://www.google.com/maps?q=${parkingLot.latitude},${parkingLot.longitude}`}
                        target="_blank"
                        rel="noreferrer"
                        className="mt-4 inline-flex items-center gap-2 rounded-lg border border-slate-200 px-4 py-2.5 text-sm font-semibold text-blue-700 hover:bg-blue-50"
                    >
                        View location on Google Maps ↗
                    </a>
                </Section>

                {/* Operating hours and vehicles */}
                <Section
                    title="3. Operating Hours & Vehicle Types"
                    description="Check the operating schedule and supported vehicle categories."
                >
                    <div className="grid gap-4 sm:grid-cols-2">
                        <InfoItem
                            label="Operating schedule"
                            value={parkingLot.operatingHours}
                        />
                        <InfoItem
                            label="Supported vehicles"
                            value={
                                <div className="flex flex-wrap gap-2">
                                    {parkingLot.vehicleTypes.map((vehicle) => (
                                        <span
                                            key={vehicle}
                                            className="rounded-full bg-blue-50 px-3 py-1 text-xs font-semibold text-blue-700"
                                        >
                                            {vehicle}
                                        </span>
                                    ))}
                                </div>
                            }
                        />
                    </div>
                </Section>

                {/* Capacity and zones */}
                <Section
                    title="4. Capacity & Parking Zones"
                    description="Review the total capacity and the configuration of each zone."
                >
                    <div className="mb-5 grid gap-4 sm:grid-cols-3">
                        <InfoItem
                            label="Total capacity"
                            value={`${parkingLot.capacity} vehicles`}
                        />
                        <InfoItem
                            label="Reported available capacity"
                            value={`${parkingLot.availableCapacity} vehicles`}
                        />
                        <InfoItem
                            label="Number of zones"
                            value={`${parkingLot.zones.length} zones`}
                        />
                    </div>

                    <div className="overflow-x-auto rounded-xl border border-slate-200">
                        <table className="w-full min-w-[520px] text-left text-sm">
                            <thead className="bg-slate-50 text-xs uppercase text-slate-500">
                                <tr>
                                    <th className="px-4 py-3 font-semibold">Zone</th>
                                    <th className="px-4 py-3 font-semibold">Vehicle type</th>
                                    <th className="px-4 py-3 text-right font-semibold">
                                        Capacity
                                    </th>
                                </tr>
                            </thead>
                            <tbody className="divide-y divide-slate-100">
                                {parkingLot.zones.map((zone) => (
                                    <tr key={zone.name}>
                                        <td className="px-4 py-4 font-semibold">{zone.name}</td>
                                        <td className="px-4 py-4 text-slate-600">{zone.type}</td>
                                        <td className="px-4 py-4 text-right font-semibold">
                                            {zone.capacity}
                                        </td>
                                    </tr>
                                ))}
                            </tbody>
                        </table>
                    </div>
                </Section>

                {/* Pricing */}
                <Section
                    title="5. Parking Pricing"
                    description="Review the submitted rates by vehicle type."
                >
                    <div className="overflow-x-auto rounded-xl border border-slate-200">
                        <table className="w-full min-w-[520px] text-left text-sm">
                            <thead className="bg-slate-50 text-xs uppercase text-slate-500">
                                <tr>
                                    <th className="px-4 py-3 font-semibold">Vehicle type</th>
                                    <th className="px-4 py-3 font-semibold">
                                        First hour
                                    </th>
                                    <th className="px-4 py-3 font-semibold">
                                        Each additional hour
                                    </th>
                                </tr>
                            </thead>
                            <tbody className="divide-y divide-slate-100">
                                {parkingLot.pricing.map((price) => (
                                    <tr key={price.vehicle}>
                                        <td className="px-4 py-4 font-semibold">
                                            {price.vehicle}
                                        </td>
                                        <td className="px-4 py-4">{price.firstHour}</td>
                                        <td className="px-4 py-4">{price.nextHour}</td>
                                    </tr>
                                ))}
                            </tbody>
                        </table>
                    </div>

                    <div className="mt-4 rounded-xl border border-blue-100 bg-blue-50 p-4">
                        <p className="text-sm font-semibold text-blue-900">
                            Hold deposit
                        </p>
                        <p className="mt-1 text-xl font-bold text-blue-800">
                            {parkingLot.holdDeposit}
                        </p>
                    </div>
                </Section>

                {/* Reservation policy */}
                <Section
                    title="6. Reservation Policy"
                    description="Review the submitted reservation rules and conditions."
                >
                    <ul className="space-y-3">
                        {parkingLot.reservationPolicy.map((policy, index) => (
                            <li key={policy} className="flex gap-3 text-sm text-slate-700">
                                <span className="flex h-6 w-6 shrink-0 items-center justify-center rounded-full bg-slate-100 text-xs font-bold">
                                    {index + 1}
                                </span>
                                <span className="pt-1">{policy}</span>
                            </li>
                        ))}
                    </ul>
                </Section>

                {/* 2D / 3D layout */}
                <Section
                    title="7. Parking Layout (2D / 3D)"
                    description="Inspect the parking zones and the submitted layout."
                >
                    <div className="mb-4 flex flex-wrap items-center justify-between gap-3">
                        <div>
                            <p className="font-semibold text-slate-800">
                                {parkingLot.layoutDescription}
                            </p>
                            <p className="mt-1 text-sm text-slate-500">
                                Preview placeholder — connect the actual layout data when
                                provided by the backend.
                            </p>
                        </div>
                        <span className="rounded-lg bg-slate-100 px-3 py-1.5 text-xs font-semibold text-slate-600">
                            {parkingLot.layoutType}
                        </span>
                    </div>

                    <div className="rounded-xl border-2 border-dashed border-slate-300 bg-slate-50 p-4 sm:p-6">
                        <div className="grid grid-cols-3 gap-3">
                            {parkingLot.zones.map((zone, index) => (
                                <div
                                    key={zone.name}
                                    className={`flex min-h-28 flex-col items-center justify-center rounded-xl border p-3 text-center ${index === 0
                                        ? "border-blue-200 bg-blue-50"
                                        : index === 1
                                            ? "border-violet-200 bg-violet-50"
                                            : "border-emerald-200 bg-emerald-50"
                                        }`}
                                >
                                    <span className="text-sm font-bold">{zone.name}</span>
                                    <span className="mt-2 text-xs text-slate-600">
                                        {zone.capacity} spaces
                                    </span>
                                    <span className="mt-1 text-xs text-slate-500">
                                        {zone.type}
                                    </span>
                                </div>
                            ))}
                        </div>

                        <div className="mt-4 rounded-lg bg-slate-200 px-4 py-3 text-center text-xs font-semibold uppercase tracking-widest text-slate-600">
                            Vehicle entrance / exit — illustrative only
                        </div>
                    </div>
                </Section>


                {/* Review actions */}
                <div className="flex flex-col justify-between gap-4 rounded-2xl border border-slate-200 bg-white p-5 sm:flex-row sm:items-center sm:p-6">
                    <div>
                        <p className="font-semibold text-slate-900">
                            Ready for review
                        </p>
                        <p className="mt-1 max-w-2xl text-sm text-slate-500">
                            Review the submitted information and choose an action.
                        </p>
                    </div>

                    <div className="flex flex-wrap gap-3">
                        <Link
                            href="/admin/parking-lots/pending"
                            className="inline-flex items-center justify-center rounded-lg border border-slate-300 bg-white px-5 py-3 text-sm font-semibold text-slate-700 hover:bg-slate-50"
                        >
                            Back to applications
                        </Link>

                        <Link
                            href={`/admin/parking-lots/${encodeURIComponent(parkingLot.id)}/reject`}
                            className="inline-flex items-center justify-center rounded-lg bg-red-600 px-5 py-3 text-sm font-semibold text-white hover:bg-red-700"
                        >
                            Reject
                        </Link>

                        <Link
                            href={`/admin/parking-lots/${encodeURIComponent(parkingLot.id)}/approve`}
                            className="inline-flex items-center justify-center rounded-lg bg-emerald-600 px-5 py-3 text-sm font-semibold text-white hover:bg-emerald-700"
                        >
                            Approve
                        </Link>
                    </div>
                </div>

            </div>
        </main>
    );
}
