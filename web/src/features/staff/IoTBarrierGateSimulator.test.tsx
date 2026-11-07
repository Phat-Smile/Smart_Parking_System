import React from "react";
import { describe, it, expect, vi } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { IoTBarrierGateSimulator } from "./IoTBarrierGateSimulator";

describe("IoTBarrierGateSimulator Component", () => {
    it("renders initial closed state with 0 degree boom angle and Red traffic light", () => {
        render(
            <IoTBarrierGateSimulator
                gateId="GATE-TEST-01"
                gateName="Cổng Thử Nghiệm"
                physicalState="CLOSED"
                vehicleState="AT_STOP_LINE"
                detectedPlate="51F-782.91"
            />,
        );

        expect(screen.getByText("Cổng Thử Nghiệm")).toBeInTheDocument();
        expect(screen.getByText(/GATE-TEST-01/)).toBeInTheDocument();
        expect(screen.getByText("CỔNG ĐANG ĐÓNG (0°)")).toBeInTheDocument();
        expect(screen.getByText(/VACH DUNG XE/)).toBeInTheDocument();
        expect(screen.getByText(/LOOP A/)).toBeInTheDocument();
    });

    it("renders open state with 90 degree boom angle and Green traffic light", () => {
        render(
            <IoTBarrierGateSimulator
                gateId="GATE-TEST-01"
                physicalState="OPEN"
                vehicleState="PASSING"
                detectedPlate="51F-782.91"
            />,
        );

        expect(screen.getByText("CỔNG ĐANG MỞ (90°)")).toBeInTheDocument();
        expect(screen.getByText(/MỜI XE QUA CỔNG/i)).toBeInTheDocument();
    });

    it("triggers open gate command when clicking '2. Nâng Cần (90°)' button", async () => {
        const onGateStateChange = vi.fn();
        render(
            <IoTBarrierGateSimulator
                gateId="GATE-TEST-01"
                onGateStateChange={onGateStateChange}
                enableControls={true}
            />,
        );

        const openBtn = screen.getByText("2. Nâng Cần (90°)");
        fireEvent.click(openBtn);

        expect(screen.getByText("ĐANG NÂNG CẦN...")).toBeInTheDocument();

        await waitFor(
            () => {
                expect(onGateStateChange).toHaveBeenCalledWith("OPEN");
            },
            { timeout: 2000 },
        );
    });

    it("requires safety cover opening before emergency manual override can be activated", () => {
        const onManualOverride = vi.fn();
        const onGateStateChange = vi.fn();

        render(
            <IoTBarrierGateSimulator
                gateId="GATE-TEST-01"
                onManualOverride={onManualOverride}
                onGateStateChange={onGateStateChange}
                enableEmergencyOverride={true}
            />,
        );

        // Safety cover is closed initially
        const openCoverBtn = screen.getByText(/Mở Nắp An Toàn Cần Gạt/);
        expect(openCoverBtn).toBeInTheDocument();

        // Clicking open cover reveals the override trigger
        fireEvent.click(openCoverBtn);
        const triggerOverrideBtn = screen.getByText("KÍCH HOẠT MỞ CƯỠNG BỨC");
        expect(triggerOverrideBtn).toBeInTheDocument();

        // Clicking override activates emergency mode
        fireEvent.click(triggerOverrideBtn);
        expect(onManualOverride).toHaveBeenCalledWith(true);
        expect(onGateStateChange).toHaveBeenCalledWith("EMERGENCY_OVERRIDE");
        expect(screen.getByText("MỞ KHẨN CẤP (OVERRIDE)")).toBeInTheDocument();
        expect(
            screen.getByText(/⚠ MỞ CƯỠNG BỨC KHẨN CẤP ⚠/),
        ).toBeInTheDocument();

        // Allows restoring auto mode
        const restoreBtn = screen.getByText(/ĐÓNG OVERRIDE · VỀ TỰ ĐỘNG/);
        fireEvent.click(restoreBtn);
        expect(onManualOverride).toHaveBeenCalledWith(false);
        expect(onGateStateChange).toHaveBeenCalledWith("CLOSED");
    });

    it("simulates vehicle approach and vehicle passing correctly", async () => {
        const onVehicleStateChange = vi.fn();
        render(
            <IoTBarrierGateSimulator
                gateId="GATE-TEST-01"
                isOpen={true}
                physicalState="OPEN"
                onVehicleStateChange={onVehicleStateChange}
                enableControls={true}
            />,
        );

        const crossBtn = screen.getByText("3. Xe Đi Qua Cổng");
        fireEvent.click(crossBtn);

        expect(onVehicleStateChange).toHaveBeenCalledWith("PASSING");

        await waitFor(
            () => {
                expect(onVehicleStateChange).toHaveBeenCalledWith("CLEARED");
            },
            { timeout: 3000 },
        );
    });
});
