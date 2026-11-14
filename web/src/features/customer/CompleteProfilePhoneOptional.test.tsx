import { beforeEach, expect, it, vi } from "vitest";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { CompleteProfilePage } from "./CompleteProfilePage";

const { push, getMyProfile, saveMyProfile, session, copy } = vi.hoisted(() => ({
    push: vi.fn(),
    getMyProfile: vi.fn(),
    saveMyProfile: vi.fn(),
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
    saveMyProfile,
    uploadCustomerMedia: vi.fn(),
}));

beforeEach(() => {
    localStorage.clear();
    sessionStorage.clear();
    push.mockClear();
    saveMyProfile.mockReset().mockResolvedValue({
        accountId: "customer-demo",
        fullName: "Nguyen Van A",
        dateOfBirth: null,
        citizenIdNumber: null,
        driverLicenseNumber: null,
        avatarUrl: null,
        createdAtUtc: new Date().toISOString(),
        updatedAtUtc: null,
        phoneNumber: null,
        address: null,
        identityDocumentType: null,
        identityIssueDate: null,
        identityIssuedBy: null,
        identityFrontMediaId: null,
        identityBackMediaId: null,
    });
    window.history.replaceState({}, "", "/customer/complete-profile");
    getMyProfile.mockReset().mockResolvedValue({
        accountId: "customer-demo",
        fullName: "",
        dateOfBirth: null,
        citizenIdNumber: null,
        driverLicenseNumber: null,
        avatarUrl: null,
        createdAtUtc: new Date().toISOString(),
        updatedAtUtc: null,
        phoneNumber: null,
        address: null,
        identityDocumentType: null,
        identityIssueDate: null,
        identityIssuedBy: null,
        identityFrontMediaId: null,
        identityBackMediaId: null,
    });
});

it("allows saving profile when phone number is empty (optional)", async () => {
    render(<CompleteProfilePage fontClassName="" />);

    await waitFor(() => {
        expect(screen.getByLabelText(/Full Name/)).toBeInTheDocument();
    });

    fireEvent.change(screen.getByLabelText(/Full Name/), {
        target: { value: "Nguyen Van A" },
    });
    fireEvent.change(screen.getByLabelText(/Phone Number/), {
        target: { value: "" },
    });

    const consentCheckbox = screen.getByLabelText(/I agree to the/i);
    fireEvent.click(consentCheckbox);

    const submitBtn = screen.getByRole("button", { name: /Complete Profile/i });
    fireEvent.click(submitBtn);

    await waitFor(() => {
        expect(saveMyProfile).toHaveBeenCalledTimes(1);
    });

    expect(saveMyProfile).toHaveBeenCalledWith(
        expect.objectContaining({
            fullName: "Nguyen Van A",
            phoneNumber: null,
        }),
    );
    expect(screen.queryByText(/Enter a valid Vietnamese phone number/i)).toBeNull();
});

it("shows validation error when phone number has invalid format", async () => {
    render(<CompleteProfilePage fontClassName="" />);

    await waitFor(() => {
        expect(screen.getByLabelText(/Full Name/)).toBeInTheDocument();
    });

    fireEvent.change(screen.getByLabelText(/Full Name/), {
        target: { value: "Nguyen Van A" },
    });
    fireEvent.change(screen.getByLabelText(/Phone Number/), {
        target: { value: "12345" },
    });

    const consentCheckbox = screen.getByLabelText(/I agree to the/i);
    fireEvent.click(consentCheckbox);

    const submitBtn = screen.getByRole("button", { name: /Complete Profile/i });
    fireEvent.click(submitBtn);

    await waitFor(() => {
        expect(
            screen.getByText(/Enter a valid Vietnamese phone number/i),
        ).toBeInTheDocument();
    });

    expect(saveMyProfile).not.toHaveBeenCalled();
});

it("allows saving profile when phone number is valid", async () => {
    render(<CompleteProfilePage fontClassName="" />);

    await waitFor(() => {
        expect(screen.getByLabelText(/Full Name/)).toBeInTheDocument();
    });

    fireEvent.change(screen.getByLabelText(/Full Name/), {
        target: { value: "Nguyen Van A" },
    });
    fireEvent.change(screen.getByLabelText(/Phone Number/), {
        target: { value: "0901234567" },
    });

    const consentCheckbox = screen.getByLabelText(/I agree to the/i);
    fireEvent.click(consentCheckbox);

    const submitBtn = screen.getByRole("button", { name: /Complete Profile/i });
    fireEvent.click(submitBtn);

    await waitFor(() => {
        expect(saveMyProfile).toHaveBeenCalledTimes(1);
    });

    expect(saveMyProfile).toHaveBeenCalledWith(
        expect.objectContaining({
            fullName: "Nguyen Van A",
            phoneNumber: "0901234567",
        }),
    );
});
