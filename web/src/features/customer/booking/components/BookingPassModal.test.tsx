import { describe, expect, it, vi } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { BookingPassModal } from "./BookingPassModal";

describe("BookingPassModal", () => {
    const defaultProps = {
        isOpen: true,
        onClose: vi.fn(),
        reservationId: "12345678-abcd-ef01-2345-6789abcdef01",
        lotName: "ParkWise Central Tower",
        lotAddress: "123 Hai Bà Trưng, Q1",
        plateNumber: "51H-999.88",
        appointmentTime: "2026-10-30T14:30:00Z",
        durationMinutes: 120,
        qrCodeToken: "RES-QR-12345678",
        prepaidAmount: 50000,
    };

    it("renders booking pass details correctly", () => {
        render(<BookingPassModal {...defaultProps} />);

        expect(screen.getByText("Booking Pass")).toBeInTheDocument();
        expect(screen.getByText("BK-12345678")).toBeInTheDocument();
        expect(screen.getByText("ParkWise Central Tower")).toBeInTheDocument();
        expect(screen.getByText("51H-999.88")).toBeInTheDocument();
        expect(screen.getByText("50.000 đ")).toBeInTheDocument();
        expect(
            screen.getByText(/Scan this code at the gate/),
        ).toBeInTheDocument();
    });

    it("calls onClose when clicking close button", () => {
        const onClose = vi.fn();
        render(<BookingPassModal {...defaultProps} onClose={onClose} />);

        const closeBtns = screen.getAllByRole("button", { name: "Close" });
        fireEvent.click(closeBtns[0]);
        expect(onClose).toHaveBeenCalled();
    });

    it("does not render when isOpen is false", () => {
        const { container } = render(
            <BookingPassModal {...defaultProps} isOpen={false} />,
        );
        expect(container).toBeEmptyDOMElement();
    });
});
