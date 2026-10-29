import { describe, it, expect, vi } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import React from "react";
import { VneidCardSimulator } from "./VneidCardSimulator";
import { VneidAuthModal } from "./VneidAuthModal";
import {
    VNEID_SEED_PROFILES,
    findProfileByCccd,
    generateVneidQrString,
} from "./vneidSeedData";

describe("VNeID Seed Data", () => {
    it("should contain valid seed profiles matching backend seed.json", () => {
        expect(VNEID_SEED_PROFILES.length).toBeGreaterThanOrEqual(8);
        const an = findProfileByCccd("079201234567");
        expect(an).toBeDefined();
        expect(an?.fullName).toBe("NGUYỄN VĂN AN");
        expect(an?.gender).toBe("M");
    });

    it("should find profile by formatted or raw CCCD", () => {
        const raw = findProfileByCccd("001199123456");
        const formatted = findProfileByCccd("001 199 123 456");
        expect(raw).toBeDefined();
        expect(formatted).toBeDefined();
        expect(raw?.fullName).toBe("TRẦN THỊ BÍCH");
        expect(raw?.dob).toBe("1991-07-22");
    });

    it("should generate standard VNeID QR string format", () => {
        const profile = VNEID_SEED_PROFILES[0];
        const qr = generateVneidQrString(profile);
        expect(qr).toContain("079201234567");
        expect(qr).toContain("NGUYỄN VĂN AN");
        expect(qr).toContain("Nam");
    });
});

describe("VneidCardSimulator Component", () => {
    it("should render CCCD card details accurately", () => {
        const profile = VNEID_SEED_PROFILES[0];
        render(<VneidCardSimulator profile={profile} />);

        expect(screen.getByTestId("vneid-card-simulator")).toBeInTheDocument();
        expect(screen.getByTestId("vneid-card-fullname")).toHaveTextContent(
            "NGUYỄN VĂN AN",
        );
        expect(screen.getByTestId("vneid-card-cccd")).toHaveTextContent(
            "079 201 234 567",
        );
        expect(screen.getByText("15/03/2001")).toBeInTheDocument();
        expect(screen.getByText("ĐÃ XÁC THỰC MỨC 2")).toBeInTheDocument();
    });
});

describe("VneidAuthModal Component", () => {
    it("should render and handle profile selection and confirmation", () => {
        const handleConfirm = vi.fn();
        const handleClose = vi.fn();

        render(
            <VneidAuthModal
                isOpen={true}
                onClose={handleClose}
                onConfirm={handleConfirm}
            />,
        );

        expect(screen.getByTestId("vneid-auth-modal")).toBeInTheDocument();
        expect(
            screen.getByText(/Định danh điện tử Quốc gia \(VNeID\)/i),
        ).toBeInTheDocument();

        // Click confirm button
        const confirmButton = screen.getByText("Xác nhận chia sẻ thông tin");
        fireEvent.click(confirmButton);

        expect(handleConfirm).toHaveBeenCalledTimes(1);
        expect(handleConfirm).toHaveBeenCalledWith(
            expect.objectContaining({
                cccd: "079201234567",
                fullName: "NGUYỄN VĂN AN",
            }),
        );
        expect(handleClose).toHaveBeenCalledTimes(1);
    });

    it("should not render when isOpen is false", () => {
        render(
            <VneidAuthModal
                isOpen={false}
                onClose={() => {}}
                onConfirm={() => {}}
            />,
        );
        expect(
            screen.queryByTestId("vneid-auth-modal"),
        ).not.toBeInTheDocument();
    });
});
