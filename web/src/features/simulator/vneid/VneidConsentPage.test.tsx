import { beforeEach, describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen } from "@testing-library/react";
import { VneidConsentPage } from "./VneidConsentPage";
import {
    readVneidDemoShare,
    VNEID_DEMO_PROFILES,
    VNEID_SHARE_KEY,
} from "./vneidDemoSession";

const { replace } = vi.hoisted(() => ({ replace: vi.fn() }));
vi.mock("next/navigation", () => ({ useRouter: () => ({ replace }) }));
vi.mock("@/features/auth/authSession", () => ({
    getAuthSessionSnapshot: () => ({ account: { id: "customer-demo" } }),
}));

beforeEach(() => {
    sessionStorage.clear();
    replace.mockClear();
});

describe("VNeID demo consent", () => {
    it("starts without consent and prevents submitting without it", () => {
        const { container } = render(<VneidConsentPage />);
        expect(screen.getByRole("checkbox")).not.toBeChecked();
        expect(
            screen.getByRole("button", { name: "Xác nhận liên kết" }),
        ).toBeDisabled();
        fireEvent.submit(container.querySelector("form")!);
        expect(replace).not.toHaveBeenCalled();
        expect(sessionStorage.getItem(VNEID_SHARE_KEY)).toBeNull();
    });

    it("shows the selected sample data and resets consent when the account changes", () => {
        render(<VneidConsentPage />);
        expect(screen.getByText(VNEID_DEMO_PROFILES[0].cccd)).toBeInTheDocument();
        expect(screen.getByText(VNEID_DEMO_PROFILES[0].residence)).toBeInTheDocument();
        fireEvent.click(screen.getByRole("checkbox"));
        fireEvent.change(screen.getByLabelText("Tài khoản"), {
            target: { value: VNEID_DEMO_PROFILES[1].cccd },
        });
        expect(screen.getByRole("checkbox")).not.toBeChecked();
        expect(screen.getByText(VNEID_DEMO_PROFILES[1].cccd)).toBeInTheDocument();
        expect(screen.queryByText(VNEID_DEMO_PROFILES[0].cccd)).not.toBeInTheDocument();
    });

    it("shares only a sample identifier after consent and returns to the profile", () => {
        render(<VneidConsentPage />);
        fireEvent.click(screen.getByRole("checkbox"));
        fireEvent.click(
            screen.getByRole("button", { name: "Xác nhận liên kết" }),
        );
        const raw = sessionStorage.getItem(VNEID_SHARE_KEY)!;
        expect(readVneidDemoShare(raw, "customer-demo")).toEqual(
            VNEID_DEMO_PROFILES[0],
        );
        expect(JSON.parse(raw)).not.toHaveProperty("fullName");
        expect(replace).toHaveBeenCalledWith(
            "/customer/complete-profile?vneid=shared",
        );
    });

    it("cancels without sharing and clears a stale result", () => {
        sessionStorage.setItem(VNEID_SHARE_KEY, "stale");
        render(<VneidConsentPage />);
        fireEvent.click(screen.getByRole("button", { name: "Hủy" }));
        expect(sessionStorage.getItem(VNEID_SHARE_KEY)).toBeNull();
        expect(replace).toHaveBeenCalledWith("/customer/complete-profile");
    });

    it("keeps the page open and explains storage failure", () => {
        vi.spyOn(Storage.prototype, "setItem").mockImplementation(() => {
            throw new Error("blocked");
        });
        render(<VneidConsentPage />);
        fireEvent.click(screen.getByRole("checkbox"));
        fireEvent.click(
            screen.getByRole("button", { name: "Xác nhận liên kết" }),
        );
        expect(screen.getByRole("alert")).toHaveTextContent(
            "Không thể chia sẻ dữ liệu mẫu",
        );
        expect(replace).not.toHaveBeenCalled();
    });
});

describe("demo result validation", () => {
    const envelope = () => ({
        version: 1,
        accountId: "customer-demo",
        consent: true,
        seedId: VNEID_DEMO_PROFILES[0].cccd,
        createdAt: Date.now(),
    });
    it("rejects corrupted data and full profiles from the legacy flow", () => {
        expect(readVneidDemoShare("invalid", "customer-demo")).toBeNull();
        expect(
            readVneidDemoShare(
                JSON.stringify(VNEID_DEMO_PROFILES[0]),
                "customer-demo",
            ),
        ).toBeNull();
    });
    it("rejects another account, missing consent and unknown seeds", () => {
        expect(
            readVneidDemoShare(JSON.stringify(envelope()), "someone-else"),
        ).toBeNull();
        expect(
            readVneidDemoShare(
                JSON.stringify({ ...envelope(), consent: false }),
                "customer-demo",
            ),
        ).toBeNull();
        expect(
            readVneidDemoShare(
                JSON.stringify({ ...envelope(), seedId: "unknown" }),
                "customer-demo",
            ),
        ).toBeNull();
    });
    it("rejects expired and future results", () => {
        expect(
            readVneidDemoShare(
                JSON.stringify({
                    ...envelope(),
                    createdAt: Date.now() - 600001,
                }),
                "customer-demo",
            ),
        ).toBeNull();
        expect(
            readVneidDemoShare(
                JSON.stringify({
                    ...envelope(),
                    createdAt: Date.now() + 10000,
                }),
                "customer-demo",
            ),
        ).toBeNull();
    });
});
