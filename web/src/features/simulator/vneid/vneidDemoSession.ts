import { VNEID_SEED_PROFILES } from "./vneidSeedData";

export const VNEID_SHARE_KEY = "parkwise:vneid-shared-profile";
export const VNEID_PROFILE_RETURN = "/customer/complete-profile";
export const VNEID_DEMO_PROFILES = VNEID_SEED_PROFILES.filter(
    (profile) => profile.category === "customer",
);

// Browser storage only selects local seeds; it is never evidence of verified identity.
export function readVneidDemoShare(raw: string, accountId: string | null) {
    try {
        const value = JSON.parse(raw);
        if (
            value?.version !== 1 ||
            value.accountId !== accountId ||
            value.consent !== true ||
            typeof value.createdAt !== "number" ||
            Date.now() - value.createdAt < 0 ||
            Date.now() - value.createdAt > 10 * 60 * 1000
        )
            return null;
        return (
            VNEID_DEMO_PROFILES.find(
                (profile) => profile.cccd === value.seedId,
            ) ?? null
        );
    } catch {
        return null;
    }
}
