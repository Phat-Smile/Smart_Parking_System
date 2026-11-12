"use client";

import { useSyncExternalStore } from "react";
import type {
    AssignedLotResponse,
    LotParkingSessionItem,
    OccupancyStats,
    SessionStatus,
    StaffSession,
    VehicleType,
    VerificationIssue,
} from "./types";
import {
    calculateParkingFee,
    getAssignedLot,
    listLotInsideSessions,
} from "./staffApi";

export interface GateActivity {
    id: string;
    type: "entry" | "exit";
    plateNumber: string;
    vehicleType: string;
    customerType?: string;
    timestamp: string;
    slotCode?: string | null;
    amount?: number;
}

let assignedLotState: AssignedLotResponse | null = null;
let selectedLotIdState: string | null = null;
let assignedLotRequestSeq = 0;
let sessionsState: StaffSession[] = [];
const issuesState: VerificationIssue[] = [];
let activitiesState: GateActivity[] = [];
let loadErrorState: unknown = null;
let loaded = false;
let refreshing: Promise<AssignedLotResponse | null> | null = null;
const listeners = new Set<() => void>();

function notify() {
    listeners.forEach((l) => l());
}

function toStaffSession(
    item: LotParkingSessionItem,
    lotId: string,
): StaffSession {
    return {
        id: item.sessionId,
        lotId,
        plateNumber: item.plateNumber,
        vehicleType: item.vehicleType as VehicleType,
        entryTime: item.entryTime,
        status: item.status as SessionStatus,
        slotCode: item.slotCode,
        // Session payment does not exist before Sprint 3.
        paymentStatus: "Pending",
    };
}

function refresh(): Promise<AssignedLotResponse | null> {
    refreshing ??= (async () => {
        const requestSeq = ++assignedLotRequestSeq;
        try {
            const lot = await getAssignedLot(selectedLotIdState ?? undefined);
            if (requestSeq !== assignedLotRequestSeq) {
                return assignedLotState;
            }
            const page = lot ? await listLotInsideSessions(lot.lotId) : null;
            if (requestSeq !== assignedLotRequestSeq) {
                return assignedLotState;
            }
            assignedLotState = lot;
            if (lot?.lotId) {
                selectedLotIdState = lot.lotId;
            }
            sessionsState = page
                ? page.items.map((item) => toStaffSession(item, lot!.lotId))
                : [];
            loadErrorState = null;
        } catch (error) {
            if (requestSeq === assignedLotRequestSeq) {
                loadErrorState = error;
            }
        } finally {
            refreshing = null;
            if (requestSeq === assignedLotRequestSeq) {
                loaded = true;
                notify();
            }
        }
        return assignedLotState;
    })();
    return refreshing;
}

function addActivity(activity: GateActivity) {
    activitiesState = [activity, ...activitiesState].slice(0, 20);
}

export const staffStore = {
    getSessions: () => sessionsState,
    getIssues: () => issuesState,
    getActivities: () => activitiesState,
    getAssignedLot: () => assignedLotState,
    getLoadError: () => loadErrorState,

    setAssignedLot: (lot: AssignedLotResponse | null) => {
        assignedLotState = lot;
        selectedLotIdState = lot?.lotId ?? null;
        notify();
    },

    fetchAssignedLot: async (preferredLotId?: string) => {
        const requestSeq = ++assignedLotRequestSeq;
        try {
            const targetPreferred =
                preferredLotId ?? selectedLotIdState ?? undefined;
            const lot = await getAssignedLot(targetPreferred);
            if (requestSeq !== assignedLotRequestSeq) {
                return assignedLotState;
            }
            const targetLotId = lot?.lotId ?? "";
            const page = targetLotId
                ? await listLotInsideSessions(targetLotId)
                : null;
            if (requestSeq !== assignedLotRequestSeq) {
                return assignedLotState;
            }
            assignedLotState = lot;
            selectedLotIdState =
                lot?.lotId ?? preferredLotId ?? selectedLotIdState;
            sessionsState =
                page && targetLotId
                    ? page.items.map((item) =>
                          toStaffSession(item, targetLotId),
                      )
                    : [];
            loadErrorState = null;
        } catch (error) {
            if (requestSeq === assignedLotRequestSeq) {
                loadErrorState = error;
            }
        } finally {
            if (requestSeq === assignedLotRequestSeq) {
                loaded = true;
                notify();
            }
        }
        return assignedLotState;
    },
    refresh,

    /** Called after Parking opened the session; counts and the list are read again from Parking. */
    addSession: (session: StaffSession) => {
        sessionsState = [
            session,
            ...sessionsState.filter((s) => s.id !== session.id),
        ];
        addActivity({
            id: `act-${session.id || Date.now()}`,
            type: "entry",
            plateNumber: session.plateNumber,
            vehicleType: session.vehicleType,
            customerType: session.customerType,
            timestamp: session.entryTime,
            slotCode: session.slotCode,
        });
        notify();
        void refresh();
    },

    /** Called after Parking cancelled the session. */
    cancelSession: (sessionId?: string) => {
        void sessionId;
        void refresh();
    },

    /** Called after Parking completed the exit (not available before Sprint 3). */
    completeExit: (sessionId: string, paymentMethod?: string) => {
        void paymentMethod;
        const found = sessionsState.find((s) => s.id === sessionId);
        if (found) {
            addActivity({
                id: `act-exit-${sessionId}`,
                type: "exit",
                plateNumber: found.plateNumber,
                vehicleType: found.vehicleType,
                customerType: found.customerType,
                timestamp: new Date().toISOString(),
                amount: found.amountDueVnd,
            });
        }
        void refresh();
    },

    resolveIssue: (issueId?: string) => {
        // No gate plate readings yet.
        void issueId;
    },

    subscribe: (listener: () => void) => {
        listeners.add(listener);
        if (!loaded) void refresh();
        return () => listeners.delete(listener);
    },
};

const EMPTY_SESSIONS: StaffSession[] = [];
const EMPTY_ISSUES: VerificationIssue[] = [];
const EMPTY_ACTIVITIES: GateActivity[] = [];

export function useStaffData() {
    const sessions = useSyncExternalStore(
        staffStore.subscribe,
        staffStore.getSessions,
        () => EMPTY_SESSIONS,
    );
    const issues = useSyncExternalStore(
        staffStore.subscribe,
        staffStore.getIssues,
        () => EMPTY_ISSUES,
    );
    const activities = useSyncExternalStore(
        staffStore.subscribe,
        staffStore.getActivities,
        () => EMPTY_ACTIVITIES,
    );
    const assignedLot = useSyncExternalStore(
        staffStore.subscribe,
        staffStore.getAssignedLot,
        () => null,
    );
    const loadError = useSyncExternalStore(
        staffStore.subscribe,
        staffStore.getLoadError,
        () => null,
    );

    // Active and Paid are both vehicles still inside the lot (Paid = paid, not exited yet).
    const activeSessions = sessions.filter(
        (s) => s.status === "Active" || s.status === "Paid",
    );
    const pendingIssues = issues.filter((i) => i.status === "Pending");

    const totalCap =
        assignedLot?.totalCapacity ?? assignedLot?.motorbikeCapacity ?? 50;
    const occupied = activeSessions.length;

    const occupancy: OccupancyStats = {
        totalCapacity: assignedLot?.motorbikeCapacity ?? null,
        occupied: activeSessions.length,
        carAvailable: assignedLot?.availableCarSlots ?? null,
        motorbikeAvailable: assignedLot?.availableMotorbikeSlots ?? null,
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
        loadError,
        fetchAssignedLot: staffStore.fetchAssignedLot,
        setAssignedLot: staffStore.setAssignedLot,
        addSession: staffStore.addSession,
        cancelSession: staffStore.cancelSession,
        completeExit: staffStore.completeExit,
        resolveIssue: staffStore.resolveIssue,
    };
}
