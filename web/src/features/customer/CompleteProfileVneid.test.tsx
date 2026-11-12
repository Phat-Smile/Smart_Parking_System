import { StrictMode } from "react";
import { beforeEach, expect, it, vi } from "vitest";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { CompleteProfilePage } from "./CompleteProfilePage";
import {
    VNEID_DEMO_PROFILES,
    VNEID_SHARE_KEY,
} from "@/features/simulator/vneid/vneidDemoSession";

const { push, getMyProfile, session, copy } = vi.hoisted(() => ({
    push: vi.fn(),
    getMyProfile: vi.fn(),
    session: {
        account: {
            id: "customer-demo",
            email: "sample@demo.local",
            phoneNumber: null,
            status: "EmailVerified",
            role: "Customer",
        },
        displayName: "Original Name",
    },
    copy: (en: string) => en,
}));
vi.mock("next/navigation", () => ({
    useRouter: () => ({ push, replace: vi.fn() }),
}));
vi.mock("@/features/auth/authSession", () => ({
    getAuthSessionSnapshot: () => session,
    subscribeAuthSession: () => () => {},
}));
vi.mock("./i18n/customerLanguage", () => ({
    useCustomerLanguage: () => ({ language: "en", copy }),
}));
vi.mock("./CustomerShell", () => ({ CustomerHeader: () => null }));
vi.mock("@/features/parkwise/ParkWiseFooter", () => ({
    ParkWiseFooter: () => null,
}));
vi.mock("./customerProfileApi", () => ({
    getMyProfile,
    saveMyProfile: vi.fn(),
    uploadCustomerMedia: vi.fn(),
}));

beforeEach(() => {
    localStorage.clear();
    sessionStorage.clear();
    push.mockClear();
    window.history.replaceState({}, "", "/customer/complete-profile");
    getMyProfile.mockReset().mockImplementation(() => new Promise(() => {}));
});

it("restores unsaved fields when returning without sharing, including in Strict Mode", async () => {
    const first = render(
        <StrictMode>
            <CompleteProfilePage fontClassName="" />
        </StrictMode>,
    );
    fireEvent.change(screen.getByLabelText(/Full Name/), {
        target: { value: "Unsaved Demo Name" },
    });
    fireEvent.change(screen.getByLabelText(/Phone Number/), {
        target: { value: "0900000000" },
    });
    fireEvent.click(
        screen.getByRole("checkbox", { name: "Add identity documents" }),
    );
    fireEvent.click(screen.getByRole("button", { name: "Try VNeID demo" }));
    expect(push).toHaveBeenCalledWith(
        "/simulator/vneid?return=complete-profile",
    );
    first.unmount();
    render(
        <StrictMode>
            <CompleteProfilePage fontClassName="" />
        </StrictMode>,
    );
    await waitFor(() =>
        expect(screen.getByLabelText(/Full Name/)).toHaveValue(
            "Unsaved Demo Name",
        ),
    );
    expect(screen.getByLabelText(/Phone Number/)).toHaveValue("0900000000");
    expect(
        screen.queryByTestId("customer-vneid-demo-badge"),
    ).not.toBeInTheDocument();
});

it("fills the sample once and does not let a delayed server profile overwrite it", async () => {
    let resolveProfile!: (value: object) => void;
    getMyProfile.mockImplementation(
        () =>
            new Promise((resolve) => {
                resolveProfile = resolve;
            }),
    );
    window.history.replaceState(
        {},
        "",
        "/customer/complete-profile?vneid=shared",
    );
    sessionStorage.setItem(
        VNEID_SHARE_KEY,
        JSON.stringify({
            version: 1,
            accountId: "customer-demo",
            consent: true,
            seedId: VNEID_DEMO_PROFILES[0].cccd,
            createdAt: Date.now(),
        }),
    );
    render(<CompleteProfilePage fontClassName="" />);
    await waitFor(() =>
        expect(screen.getByLabelText(/Full Name/)).toHaveValue(
            VNEID_DEMO_PROFILES[0].fullName,
        ),
    );
    resolveProfile({ fullName: "Old Server Name", identityDocumentType: null });
    await waitFor(() =>
        expect(
            screen.getByTestId("customer-vneid-demo-badge"),
        ).toHaveTextContent("not verified"),
    );
    expect(screen.getByLabelText(/Full Name/)).toHaveValue(
        VNEID_DEMO_PROFILES[0].fullName,
    );
    expect(sessionStorage.getItem(VNEID_SHARE_KEY)).toBeNull();
});

it("does not apply a stale share when there is no confirmation return marker", async () => {
    sessionStorage.setItem(
        VNEID_SHARE_KEY,
        JSON.stringify({
            version: 1,
            accountId: "customer-demo",
            consent: true,
            seedId: VNEID_DEMO_PROFILES[0].cccd,
            createdAt: Date.now(),
        }),
    );
    render(<CompleteProfilePage fontClassName="" />);
    await waitFor(() =>
        expect(sessionStorage.getItem(VNEID_SHARE_KEY)).toBeNull(),
    );
    expect(screen.getByLabelText(/Full Name/)).toHaveValue("Original Name");
});
