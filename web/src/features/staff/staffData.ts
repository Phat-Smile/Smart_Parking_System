"use client";

import { useSyncExternalStore } from "react";
import type { OccupancyStats, StaffSession, VerificationIssue } from "./types";
import { DEFAULT_STAFF_LOT_ID } from "./staffApi";

const initialSessions: StaffSession[] = [
    {
        id: "ses-101",
        lotId: DEFAULT_STAFF_LOT_ID,
        plateNumber: "51F-782.91",
        vehicleType: "Car",
        customerType: "Registered",
        driverName: "Lê Hoàng Long",
        entryTime: new Date(Date.now() - 3600000 * 3.5).toISOString(),
        status: "Active",
        qrToken: "QR-PARKWISE-REG-101",
        slotCode: "B1-A04",
        paymentStatus: "Paid",
        totalFeeVnd: 75000,
        prepaidVnd: 75000,
        amountDueVnd: 0,
        paymentMethod: "Wallet",
    },
    {
        id: "ses-102",
        lotId: DEFAULT_STAFF_LOT_ID,
        plateNumber: "59T1-883.12",
        vehicleType: "Motorbike",
        customerType: "Guest",
        driverName: "Guest",
        entryTime: new Date(Date.now() - 3600000 * 1.8).toISOString(),
        status: "Active",
        qrToken: "QR-GUEST-59T1-88312",
        slotCode: "B2-M12",
        paymentStatus: "Pending",
        totalFeeVnd: 12000,
        prepaidVnd: 0,
        amountDueVnd: 12000,
    },
    {
        id: "ses-103",
        lotId: DEFAULT_STAFF_LOT_ID,
        plateNumber: "51K-892.41",
        vehicleType: "Car",
        customerType: "Guest",
        driverName: "Guest",
        entryTime: new Date(Date.now() - 3600000 * 2.2).toISOString(),
        status: "Active",
        qrToken: "QR-GUEST-51K-89241",
        slotCode: "B1-B10",
        paymentStatus: "Pending",
        totalFeeVnd: 60000,
        prepaidVnd: 0,
        amountDueVnd: 60000,
    },
    {
        id: "ses-104",
        lotId: DEFAULT_STAFF_LOT_ID,
        plateNumber: "50E-124.99",
        vehicleType: "Car",
        customerType: "Registered",
        driverName: "Trần Anh Vũ",
        entryTime: new Date(Date.now() - 3600000 * 0.5).toISOString(),
        status: "Active",
        qrToken: "QR-PARKWISE-REG-104",
        slotCode: "B1-A12",
        paymentStatus: "Paid",
        totalFeeVnd: 30000,
        prepaidVnd: 30000,
        amountDueVnd: 0,
        paymentMethod: "Wallet",
    },
];

const initialIssues: VerificationIssue[] = [
    {
        id: "issue-1",
        sessionId: "ses-103",
        spot: "Gate In 01",
        plateNumber: "51K-892.41",
        problem: "Low Camera OCR Confidence (64%)",
        detectedPlate: "51K-892.4?",
        confidence: 0.64,
        currentState: "Waiting for staff confirmation",
        createdAt: new Date(Date.now() - 1000 * 180).toISOString(),
        status: "Pending",
    },
    {
        id: "issue-2",
        spot: "Gate Out 02",
        plateNumber: "59C2-441.80",
        problem: "QR Damaged / Unreadable on Guest Phone",
        detectedPlate: "59C2-441.80",
        confidence: 0.98,
        currentState: "Vehicle waiting at barrier",
        createdAt: new Date(Date.now() - 1000 * 60).toISOString(),
        status: "Pending",
    },
];

export interface GateActivity {
    id: string;
    type: "entry" | "exit";
    plateNumber: string;
    vehicleType: string;
    customerType: string;
    timestamp: string;
    slotCode?: string;
    amount?: number;
}

const initialActivities: GateActivity[] = [
    {
        id: "act-1",
        type: "entry",
        plateNumber: "50E-124.99",
        vehicleType: "Car",
        customerType: "Registered",
        timestamp: new Date(Date.now() - 1000 * 60 * 30).toISOString(),
        slotCode: "B1-A12",
    },
    {
        id: "act-2",
        type: "exit",
        plateNumber: "51A-932.18",
        vehicleType: "Car",
        customerType: "Guest",
        timestamp: new Date(Date.now() - 1000 * 60 * 15).toISOString(),
        amount: 60000,
    },
    {
        id: "act-3",
        type: "entry",
        plateNumber: "51K-892.41",
        vehicleType: "Car",
        customerType: "Guest",
        timestamp: new Date(Date.now() - 1000 * 60 * 8).toISOString(),
        slotCode: "B1-B10",
    },
];

let sessionsState = [...initialSessions];
let issuesState = [...initialIssues];
let activitiesState = [...initialActivities];
const listeners = new Set<() => void>();

function notify() {
    listeners.forEach((l) => l());
}

export const staffStore = {
    getSessions: () => sessionsState,
    getIssues: () => issuesState,
    getActivities: () => activitiesState,

    addSession: (session: StaffSession) => {
        sessionsState = [session, ...sessionsState];
        activitiesState = [
            {
                id: `act-${Date.now()}`,
                type: "entry",
                plateNumber: session.plateNumber,
                vehicleType: session.vehicleType,
                customerType: session.customerType,
                timestamp: session.entryTime,
                slotCode: session.slotCode,
            },
            ...activitiesState,
        ];
        notify();
    },

    cancelSession: (sessionId: string) => {
        sessionsState = sessionsState.map((s) =>
            s.id === sessionId ? { ...s, status: "Cancelled" as const } : s,
        );
        notify();
    },

    completeExit: (
        sessionId: string,
        paymentMethod: "Cash" | "VNPay" | "Wallet",
    ) => {
        const found = sessionsState.find((s) => s.id === sessionId);
        if (!found) return;

        sessionsState = sessionsState.map((s) =>
            s.id === sessionId
                ? {
                      ...s,
                      status: "Completed" as const,
                      paymentStatus: "Paid" as const,
                      amountDueVnd: 0,
                      exitTime: new Date().toISOString(),
                      paymentMethod,
                  }
                : s,
        );

        activitiesState = [
            {
                id: `act-${Date.now()}`,
                type: "exit",
                plateNumber: found.plateNumber,
                vehicleType: found.vehicleType,
                customerType: found.customerType,
                timestamp: new Date().toISOString(),
                amount: found.amountDueVnd,
            },
            ...activitiesState,
        ];
        notify();
    },

    resolveIssue: (issueId: string) => {
        issuesState = issuesState.map((issue) =>
            issue.id === issueId
                ? { ...issue, status: "Resolved" as const }
                : issue,
        );
        notify();
    },

    subscribe: (listener: () => void) => {
        listeners.add(listener);
        return () => listeners.delete(listener);
    },
};

export function useStaffData() {
    const sessions = useSyncExternalStore(
        staffStore.subscribe,
        staffStore.getSessions,
        () => initialSessions,
    );
    const issues = useSyncExternalStore(
        staffStore.subscribe,
        staffStore.getIssues,
        () => initialIssues,
    );
    const activities = useSyncExternalStore(
        staffStore.subscribe,
        staffStore.getActivities,
        () => initialActivities,
    );

    const activeSessions = sessions.filter((s) => s.status === "Active");
    const pendingIssues = issues.filter((i) => i.status === "Pending");

    const occupancy: OccupancyStats = {
        totalCapacity: 240,
        occupied: activeSessions.length + 86, // base active in lot
        carAvailable:
            140 -
            activeSessions.filter((s) => s.vehicleType === "Car").length -
            45,
        motorbikeAvailable:
            100 -
            activeSessions.filter((s) => s.vehicleType === "Motorbike").length -
            41,
        activeSessionsCount: activeSessions.length,
        needsVerificationCount: pendingIssues.length,
    };

    return {
        sessions,
        activeSessions,
        issues: pendingIssues,
        activities,
        occupancy,
        addSession: staffStore.addSession,
        cancelSession: staffStore.cancelSession,
        completeExit: staffStore.completeExit,
        resolveIssue: staffStore.resolveIssue,
    };
}
