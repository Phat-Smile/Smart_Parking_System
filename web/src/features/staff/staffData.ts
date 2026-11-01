"use client";

import { useSyncExternalStore } from "react";
import type {
    AssignedLotResponse,
    OccupancyStats,
    SessionStatus,
    StaffSession,
    VehicleType,
    VerificationIssue,
} from "./types";
import {
    calculateParkingFee,
    DEFAULT_STAFF_LOT_ID,
    getAssignedLot,
    listLotSessions,
} from "./staffApi";

// 100% Real API: Zero initial mock data
const initialSessions: StaffSession[] = [];
const initialIssues: VerificationIssue[] = [];

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

const initialActivities: GateActivity[] = [];

let assignedLotState: AssignedLotResponse = {
    lotId: DEFAULT_STAFF_LOT_ID,
    lotName: "Đang tải bãi đỗ xe...",
    address: "",
    availableMotorbikeSlots: 0,
    availableCarSlots: 0,
    totalCapacity: 0,
    occupiedSlots: 0,
};

let sessionsState: StaffSession[] = [...initialSessions];
let issuesState: VerificationIssue[] = [...initialIssues];
let activitiesState: GateActivity[] = [...initialActivities];
const listeners = new Set<() => void>();

function notify() {
    listeners.forEach((l) => l());
}

export const staffStore = {
    getSessions: () => sessionsState,
    getIssues: () => issuesState,
    getActivities: () => activitiesState,
    getAssignedLot: () => assignedLotState,

    setAssignedLot: (lot: AssignedLotResponse) => {
        assignedLotState = lot;
        notify();
    },

    /**
     * 100% Real API: Fetch assigned lot and active sessions from DB
     */
    fetchAssignedLot: async (preferredLotId?: string) => {
        const lot = await getAssignedLot(preferredLotId);
        if (lot) {
            assignedLotState = lot;
        }

        const targetLotId = lot?.lotId || assignedLotState.lotId;
        if (targetLotId) {
            try {
                // Fetch real sessions from database
                const page = await listLotSessions(targetLotId, undefined, 1, 100);
                if (page && Array.isArray(page.items)) {
                    sessionsState = page.items.map((item): StaffSession => {
                        const vType: VehicleType =
                            item.vehicleType === "Motorbike" ? "Motorbike" : "Car";
                        const sStatus: SessionStatus =
                            item.status === "Completed"
                                ? "Completed"
                                : item.status === "Cancelled"
                                  ? "Cancelled"
                                  : "Active";
                        const fees = calculateParkingFee(item.entryTime, vType);
                        const isCompleted = sStatus === "Completed";
                        return {
                            id: item.sessionId,
                            lotId: targetLotId,
                            plateNumber: item.plateNumber,
                            vehicleType: vType,
                            customerType: "Guest",
                            driverName: "Khách gửi xe",
                            entryTime: item.entryTime,
                            status: sStatus,
                            qrToken: `QR-${item.sessionId}`,
                            slotCode:
                                item.slotCode ||
                                (vType === "Car" ? "CAR-A" : "MOTO-B"),
                            paymentStatus: isCompleted ? "Paid" : "Pending",
                            totalFeeVnd: fees.totalFeeVnd,
                            prepaidVnd: isCompleted ? fees.totalFeeVnd : 0,
                            amountDueVnd: isCompleted ? 0 : fees.amountDueVnd,
                        };
                    });

                    // Build real activity timeline from sessions
                    activitiesState = page.items.slice(0, 15).map((item) => ({
                        id: `act-${item.sessionId}`,
                        type: item.status === "Completed" ? ("exit" as const) : ("entry" as const),
                        plateNumber: item.plateNumber,
                        vehicleType: item.vehicleType,
                        customerType: "Guest",
                        timestamp: item.entryTime,
                        slotCode: item.slotCode || undefined,
                        amount: item.status === "Completed" ? calculateParkingFee(item.entryTime, item.vehicleType === "Motorbike" ? "Motorbike" : "Car").totalFeeVnd : undefined,
                    }));
                }
            } catch {
                // Keep empty on error
            }
        }
        notify();
        return lot;
    },

    addSession: (session: StaffSession) => {
        sessionsState = [session, ...sessionsState.filter((s) => s.id !== session.id)];
        if (
            session.vehicleType === "Motorbike" &&
            assignedLotState.availableMotorbikeSlots > 0
        ) {
            assignedLotState = {
                ...assignedLotState,
                availableMotorbikeSlots:
                    assignedLotState.availableMotorbikeSlots - 1,
            };
        }
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
        const found = sessionsState.find((s) => s.id === sessionId);
        if (found && found.vehicleType === "Motorbike") {
            assignedLotState = {
                ...assignedLotState,
                availableMotorbikeSlots:
                    assignedLotState.availableMotorbikeSlots + 1,
            };
        }
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
        if (found && found.vehicleType === "Motorbike") {
            assignedLotState = {
                ...assignedLotState,
                availableMotorbikeSlots:
                    assignedLotState.availableMotorbikeSlots + 1,
            };
        }

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

        if (found) {
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
        }
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
    const assignedLot = useSyncExternalStore(
        staffStore.subscribe,
        staffStore.getAssignedLot,
        () => assignedLotState,
    );

    const activeSessions = sessions.filter((s) => s.status === "Active");
    const pendingIssues = issues.filter((i) => i.status === "Pending");

    const totalCap = assignedLot.totalCapacity || 50;
    const occupied = activeSessions.length;

    const occupancy: OccupancyStats = {
        totalCapacity: totalCap,
        occupied,
        carAvailable: Math.max(
            0,
            (assignedLot.availableCarSlots || 50) -
                activeSessions.filter((s) => s.vehicleType === "Car").length,
        ),
        motorbikeAvailable: Math.max(0, assignedLot.availableMotorbikeSlots),
        activeSessionsCount: activeSessions.length,
        needsVerificationCount: pendingIssues.length,
    };

    return {
        sessions,
        activeSessions,
        issues: pendingIssues,
        activities,
        occupancy,
        assignedLot,
        fetchAssignedLot: staffStore.fetchAssignedLot,
        setAssignedLot: staffStore.setAssignedLot,
        addSession: staffStore.addSession,
        cancelSession: staffStore.cancelSession,
        completeExit: staffStore.completeExit,
        resolveIssue: staffStore.resolveIssue,
    };
}
