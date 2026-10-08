import { useState } from "react";
import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { SegmentedOtpInput } from "./SegmentedOtpInput";

function OtpHarness() {
    const [value, setValue] = useState(() => Array<string>(6).fill(""));
    return (
        <>
            <span id="parkwise-otp-label">Verification code</span>
            <SegmentedOtpInput value={value} onChange={setValue} />
            <button disabled={!value.every(Boolean)}>Verify Code</button>
        </>
    );
}

describe("segmented OTP input", () => {
    it("moves focus for typing, Backspace, and arrow keys", () => {
        render(<OtpHarness />);
        const digits = screen.getAllByRole("textbox") as HTMLInputElement[];

        expect(digits[0]).toHaveFocus();
        fireEvent.change(digits[0], { target: { value: "1" } });
        expect(digits[1]).toHaveFocus();
        fireEvent.keyDown(digits[1], { key: "Backspace" });
        expect(digits[0]).toHaveFocus();
        expect(digits[0]).toHaveValue("");
        fireEvent.keyDown(digits[0], { key: "ArrowRight" });
        expect(digits[1]).toHaveFocus();
        fireEvent.keyDown(digits[1], { key: "ArrowLeft" });
        expect(digits[0]).toHaveFocus();
    });

    it("distributes a pasted code, strips non-digits, and preserves a leading zero", () => {
        render(<OtpHarness />);
        const digits = screen.getAllByRole("textbox") as HTMLInputElement[];

        fireEvent.paste(digits[2], {
            clipboardData: { getData: () => "0a1b2345" },
        });
        expect(digits.map((digit) => digit.value).join("")).toBe("012345");
        expect(
            screen.getByRole("button", { name: "Verify Code" }),
        ).toBeEnabled();
    });
});
