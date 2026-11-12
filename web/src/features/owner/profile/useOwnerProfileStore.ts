import { create } from "zustand";

export interface OwnerProfileData {
    fullName: string;
    phone: string;
    email: string;
    address: string;
    businessName: string;
    taxCode: string;
    businessAddress: string;
    avatarUrl: string | null;
    joinedDate: string;
}

export const INITIAL_PROFILE: OwnerProfileData = {
    fullName: "Nguyễn Văn Minh",
    phone: "0912 345 678",
    email: "minh.owner@example.com",
    address: "12 Nguyễn Huệ, Quận 1, TP.HCM",
    businessName: "Công ty TNHH Bãi xe Minh Phát",
    taxCode: "0312345678",
    businessAddress: "45 Lê Lợi, Quận 1, TP.HCM",
    avatarUrl: null,
    joinedDate: "Tháng 10, 2025",
};

interface OwnerProfileStoreState {
    savedProfile: OwnerProfileData;
    draftProfile: OwnerProfileData;
    errors: Record<string, string>;
    isDirty: boolean;
    setDraftField: <K extends keyof OwnerProfileData>(
        field: K,
        value: OwnerProfileData[K],
    ) => void;
    setAvatar: (url: string | null) => void;
    setErrors: (errors: Record<string, string>) => void;
    clearErrors: () => void;
    saveProfile: () => void;
    resetDraft: () => void;
    getShortName: () => string;
}

export const useOwnerProfileStore = create<OwnerProfileStoreState>(
    (set, get) => ({
        savedProfile: INITIAL_PROFILE,
        draftProfile: INITIAL_PROFILE,
        errors: {},
        isDirty: false,
        setDraftField: (field, value) => {
            set((state) => {
                const nextDraft = { ...state.draftProfile, [field]: value };
                const dirty =
                    JSON.stringify(nextDraft) !==
                    JSON.stringify(state.savedProfile);
                return { draftProfile: nextDraft, isDirty: dirty };
            });
        },
        setAvatar: (url) => {
            set((state) => {
                const nextDraft = { ...state.draftProfile, avatarUrl: url };
                const dirty =
                    JSON.stringify(nextDraft) !==
                    JSON.stringify(state.savedProfile);
                return { draftProfile: nextDraft, isDirty: dirty };
            });
        },
        setErrors: (errors) => set({ errors }),
        clearErrors: () => set({ errors: {} }),
        saveProfile: () => {
            set((state) => ({
                savedProfile: { ...state.draftProfile },
                isDirty: false,
                errors: {},
            }));
        },
        resetDraft: () => {
            set((state) => ({
                draftProfile: { ...state.savedProfile },
                isDirty: false,
                errors: {},
            }));
        },
        getShortName: () => {
            const full = get().savedProfile.fullName.trim();
            if (!full) return "Minh";
            const parts = full.split(/\s+/);
            return parts[parts.length - 1] || "Minh";
        },
    }),
);
