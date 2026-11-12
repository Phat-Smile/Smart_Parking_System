import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { ArrivalTimeScrubber } from "./ArrivalTimeScrubber";
import { LotPin } from "./LotPin";

const copy = (english: string) => english;

const slots = [8, 9, 10, 11].map((hour, index) => ({
    hour,
    percent: [70, 62, 82, null][index],
}));

describe("ArrivalTimeScrubber", () => {
    it("shows the selected hour and a spoken value with the probability", () => {
        render(
            <ArrivalTimeScrubber
                slots={slots}
                selectedHour={10}
                onChange={() => {}}
                loading={false}
                copy={copy}
            />,
        );
        expect(screen.getByText(/Arrival time/)).toHaveTextContent("10:00");
        const range = screen.getByRole("slider", { name: "Arrival hour" });
        expect(range).toHaveAttribute("min", "8");
        expect(range).toHaveAttribute("max", "11");
        expect(range).toHaveAttribute(
            "aria-valuetext",
            "10:00, 82% chance of a free space",
        );
        expect(screen.getByText("AI, simulated data")).toBeInTheDocument();
    });

    it("reports the new hour when the slider moves", () => {
        const onChange = vi.fn();
        render(
            <ArrivalTimeScrubber
                slots={slots}
                selectedHour={10}
                onChange={onChange}
                loading={false}
                copy={copy}
            />,
        );
        fireEvent.change(screen.getByRole("slider"), {
            target: { value: "9" },
        });
        expect(onChange).toHaveBeenCalledWith(9);
    });

    it("says the forecast is loading and speaks only the hour when no figure exists", () => {
        render(
            <ArrivalTimeScrubber
                slots={slots}
                selectedHour={11}
                onChange={() => {}}
                loading
                copy={copy}
            />,
        );
        expect(screen.getByText("Loading forecast")).toBeInTheDocument();
        expect(screen.getByRole("slider")).toHaveAttribute(
            "aria-valuetext",
            "11:00",
        );
    });

    it("renders nothing without slots", () => {
        const { container } = render(
            <ArrivalTimeScrubber
                slots={[]}
                selectedHour={10}
                onChange={() => {}}
                loading={false}
                copy={copy}
            />,
        );
        expect(container).toBeEmptyDOMElement();
    });
});

describe("LotPin", () => {
    it("names the lot and the probability, and selects on click", () => {
        const onSelect = vi.fn();
        render(
            <LotPin
                name="Bãi B"
                percent={82}
                selected={false}
                onSelect={onSelect}
            />,
        );
        const pin = screen.getByRole("button", {
            name: "Select parking lot Bãi B, 82% chance of a free space",
        });
        expect(pin).toHaveClass("lot-pin-good");
        expect(screen.getByText("82%")).toBeInTheDocument();
        fireEvent.click(pin);
        expect(onSelect).toHaveBeenCalled();
    });

    it("uses the BR-08 tones and a neutral pin when the figure is unknown", () => {
        const { rerender } = render(
            <LotPin name="C" percent={31} selected onSelect={() => {}} />,
        );
        expect(screen.getByRole("button")).toHaveClass("lot-pin-warn");
        expect(screen.getByRole("button")).toHaveAttribute(
            "aria-pressed",
            "true",
        );
        rerender(
            <LotPin
                name="C"
                percent={12}
                selected={false}
                onSelect={() => {}}
            />,
        );
        expect(screen.getByRole("button")).toHaveClass("lot-pin-risk");
        rerender(
            <LotPin
                name="C"
                percent={null}
                selected={false}
                onSelect={() => {}}
            />,
        );
        expect(screen.getByRole("button")).toHaveClass("lot-pin-unknown");
        expect(screen.getByText("?")).toBeInTheDocument();
    });
});
