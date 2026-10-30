"use client";

import {
    useEffect,
    useRef,
    useState,
    useSyncExternalStore,
    type ChangeEvent,
    type CSSProperties,
    type FormEvent,
} from "react";
import Image from "next/image";
import Link from "next/link";
import {
    getAuthSessionSnapshot,
    subscribeAuthSession,
} from "@/features/auth/authSession";
import { ParkWiseNavbar } from "@/features/parkwise/ParkWiseNavbar";
import { ParkWiseFooter } from "@/features/parkwise/ParkWiseFooter";
import { CustomerAccountMenu } from "./CustomerShell";
import { VneidAuthModal } from "@/features/simulator/vneid/VneidAuthModal";
import type { VneidCitizenProfile } from "@/features/simulator/vneid/vneidSeedData";

type IdType = "citizenId" | "driverLicense";
type DocumentSide = "front" | "back";
type UploadedFile = { file: File; previewUrl: string } | null;
const PROFILE_DRAFT_KEY = "parkwise:customer:complete-profile:draft";

function ProfileIcon() {
    return (
        <svg
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            strokeWidth="1.8"
            aria-hidden="true"
        >
            <circle cx="12" cy="8" r="3.5" />
            <path
                strokeLinecap="round"
                d="M5 20c.4-3.5 3.1-5.5 7-5.5s6.6 2 7 5.5"
            />
        </svg>
    );
}

function ShieldIcon() {
    return (
        <svg
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            strokeWidth="1.7"
            aria-hidden="true"
        >
            <path
                strokeLinecap="round"
                strokeLinejoin="round"
                d="M12 3 4.5 6v5.4c0 4.5 3.1 7.8 7.5 9.6 4.4-1.8 7.5-5.1 7.5-9.6V6L12 3Z"
            />
            <path
                strokeLinecap="round"
                strokeLinejoin="round"
                d="m9 12 2 2 4-4"
            />
        </svg>
    );
}

function CheckIcon() {
    return (
        <svg
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            strokeWidth="2.5"
            aria-hidden="true"
        >
            <path
                strokeLinecap="round"
                strokeLinejoin="round"
                d="m5 12 4 4L19 6"
            />
        </svg>
    );
}

function PhoneIcon() {
    return (
        <svg
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            strokeWidth="1.7"
            aria-hidden="true"
        >
            <path
                strokeLinecap="round"
                strokeLinejoin="round"
                d="M5 4h3l2 5-2 1.5a15 15 0 005.5 5.5L15 14l5 2v3a2 2 0 01-2.2 2A17 17 0 013 6.2 2 2 0 015 4Z"
            />
        </svg>
    );
}

function PinIcon() {
    return (
        <svg
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            strokeWidth="1.7"
            aria-hidden="true"
        >
            <path
                strokeLinecap="round"
                strokeLinejoin="round"
                d="M20 10c0 5-8 12-8 12S4 15 4 10a8 8 0 1116 0Z"
            />
            <circle cx="12" cy="10" r="2.5" />
        </svg>
    );
}

function IdCardIcon() {
    return (
        <svg
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            strokeWidth="1.7"
            aria-hidden="true"
        >
            <rect x="3" y="5" width="18" height="14" rx="2" />
            <circle cx="8" cy="10" r="1.5" />
            <path
                strokeLinecap="round"
                d="M5.7 15c.3-1.3 1.1-2 2.3-2s2 .7 2.3 2M13 9h5M13 13h5"
            />
        </svg>
    );
}

function CarIcon() {
    return (
        <svg
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            strokeWidth="1.7"
            aria-hidden="true"
        >
            <path
                strokeLinecap="round"
                strokeLinejoin="round"
                d="m5 11 1.5-4h11L19 11l2 2v5h-2v-2H5v2H3v-5l2-2Z"
            />
            <path strokeLinecap="round" d="M6.5 11h11M7 14h.01M17 14h.01" />
        </svg>
    );
}

function LockIcon() {
    return (
        <svg
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            strokeWidth="1.7"
            aria-hidden="true"
        >
            <rect x="5" y="10" width="14" height="11" rx="2" />
            <path
                strokeLinecap="round"
                strokeLinejoin="round"
                d="M8 10V7a4 4 0 018 0v3M12 14v3"
            />
        </svg>
    );
}

function SupportIcon() {
    return (
        <svg
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            strokeWidth="1.8"
            aria-hidden="true"
        >
            <path
                strokeLinecap="round"
                strokeLinejoin="round"
                d="M3 13v-2a9 9 0 0118 0v2M3 13a2 2 0 012-2h1v6H5a2 2 0 01-2-2v-2Zm18 0a2 2 0 00-2-2h-1v6h1a2 2 0 002-2v-2ZM16 19c-.8 1.3-2.1 2-4 2"
            />
        </svg>
    );
}

function UploadIcon() {
    return (
        <svg
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            strokeWidth="1.8"
            aria-hidden="true"
        >
            <path
                strokeLinecap="round"
                strokeLinejoin="round"
                d="M12 16V4m0 0L7.5 8.5M12 4l4.5 4.5M5 14v5h14v-5"
            />
        </svg>
    );
}

function MailIcon() {
    return (
        <svg
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            strokeWidth="1.7"
            aria-hidden="true"
        >
            <rect x="3" y="5" width="18" height="14" rx="2" />
            <path
                strokeLinecap="round"
                strokeLinejoin="round"
                d="m4 7 8 6 8-6"
            />
        </svg>
    );
}

function ChevronDownIcon() {
    return (
        <svg
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            strokeWidth="1.8"
            aria-hidden="true"
        >
            <path
                strokeLinecap="round"
                strokeLinejoin="round"
                d="m6 9 6 6 6-6"
            />
        </svg>
    );
}

function CalendarIcon() {
    return (
        <svg
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            strokeWidth="1.7"
            aria-hidden="true"
        >
            <rect x="3" y="5" width="18" height="16" rx="2" />
            <path strokeLinecap="round" d="M16 3v4M8 3v4M3 10h18" />
        </svg>
    );
}

function formatPhone(value: string) {
    return value.replace(/[\s()-]/g, "");
}

function isValidVietnamesePhone(value: string) {
    return /^(?:[35789][0-9]{8}|0[35789][0-9]{8}|\+84[35789][0-9]{8})$/.test(
        formatPhone(value),
    );
}

function dateDisplayToIso(value: string) {
    const match = /^(\d{2})\/(\d{2})\/(\d{4})$/.exec(value);
    return match ? `${match[3]}-${match[2]}-${match[1]}` : "";
}

function isoToDateDisplay(value: string) {
    const match = /^(\d{4})-(\d{2})-(\d{2})$/.exec(value);
    return match ? `${match[3]}/${match[2]}/${match[1]}` : "";
}

function isValidDateDisplay(value: string) {
    const isoValue = dateDisplayToIso(value);
    const match = /^(\d{4})-(\d{2})-(\d{2})$/.exec(isoValue);
    if (!match) return false;
    const [, yearText, monthText, dayText] = match;
    const day = Number(dayText);
    const month = Number(monthText);
    const year = Number(yearText);
    const date = new Date(year, month - 1, day);
    return (
        date.getFullYear() === year &&
        date.getMonth() === month - 1 &&
        date.getDate() === day
    );
}

function migrateDateValue(value: string | undefined) {
    if (!value) return "";
    if (/^\d{4}-\d{2}-\d{2}$/.test(value)) return isoToDateDisplay(value);
    const match = /^(\d{2})\/(\d{2})\/(\d{4})$/.exec(value);
    return match ? value : "";
}

function localDateIso() {
    const now = new Date();
    return `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, "0")}-${String(now.getDate()).padStart(2, "0")}`;
}

export function CompleteProfilePage({
    fontClassName,
}: {
    fontClassName: string;
}) {
    const authSession = useSyncExternalStore(
        subscribeAuthSession,
        getAuthSessionSnapshot,
        () => null,
    );
    const [authorityOther, setAuthorityOther] = useState(false);
    const [idType, setIdType] = useState<IdType | null>(null);
    const [includeDocument, setIncludeDocument] = useState(false);
    const [profilePhoto, setProfilePhoto] = useState<UploadedFile>(null);
    const [documentPhotos, setDocumentPhotos] = useState<
        Record<DocumentSide, UploadedFile>
    >({ front: null, back: null });
    const [fullName, setFullName] = useState(
        () => authSession?.displayName ?? "",
    );
    const [phone, setPhone] = useState(
        () => authSession?.account.phoneNumber ?? "",
    );
    const [documentNumber, setDocumentNumber] = useState("");
    const [issueDate, setIssueDate] = useState("");
    const [issuedBy, setIssuedBy] = useState("");
    const [birthDate, setBirthDate] = useState("");
    const [address, setAddress] = useState("");
    const [consent, setConsent] = useState(false);
    const [error, setError] = useState("");
    const [notice, setNotice] = useState("");
    const [vneidModalOpen, setVneidModalOpen] = useState(false);
    const [vneidVerified, setVneidVerified] = useState(false);

    function handleVneidConfirm(profile: VneidCitizenProfile) {
        setFullName(profile.fullName);
        setBirthDate(profile.dobDisplay);
        setAddress(profile.residence);
        setDocumentNumber(profile.cccd);
        setIssueDate(profile.issueDate);
        setIssuedBy("Cục Cảnh sát QLHC về TTXH");
        setVneidVerified(true);
        setNotice(`Đã điền tự động và đối soát thành công qua VNeID Mức 2 (CCCD: ${profile.cccd}).`);
        setVneidModalOpen(false);
    }
    const [validationErrors, setValidationErrors] = useState({
        fullName: "",
        phone: "",
        birthDate: "",
    });
    const profilePhotoInput = useRef<HTMLInputElement>(null);
    const birthDatePickerRef = useRef<HTMLInputElement>(null);
    const issueDatePickerRef = useRef<HTMLInputElement>(null);
    const accountEmail = authSession?.account.email ?? "";
    const emailVerified = authSession?.account.status === "EmailVerified";
    const today = localDateIso();

    useEffect(() => {
        if (!authSession) return;
        queueMicrotask(() => {
            setFullName((current) => current || authSession.displayName);
            setPhone(
                (current) => current || authSession.account.phoneNumber || "",
            );
        });
    }, [authSession]);

    useEffect(() => {
        if (!notice) return;
        const timer = window.setTimeout(() => setNotice(""), 3000);
        return () => window.clearTimeout(timer);
    }, [notice]);

    useEffect(() => {
        let cancelled = false;
        let draft: Partial<{
            fullName: string;
            phone: string;
            documentNumber: string;
            issueDate: string;
            issuedBy: string;
            birthDate: string;
            address: string;
            consent: boolean;
            includeDocument: boolean;
            idType: IdType | null;
        }> | null = null;
        try {
            const stored = localStorage.getItem(PROFILE_DRAFT_KEY);
            if (stored) draft = JSON.parse(stored);
        } catch {
            localStorage.removeItem(PROFILE_DRAFT_KEY);
        }
        if (draft) {
            // Defer browser-only draft hydration until after the first render to keep SSR markup stable.
            queueMicrotask(() => {
                if (cancelled || !draft) return;
                setFullName(draft.fullName ?? "");
                setPhone(draft.phone ?? "");
                setDocumentNumber(draft.documentNumber ?? "");
                setIssueDate(migrateDateValue(draft.issueDate));
                setIssuedBy(draft.issuedBy ?? "");
                setAuthorityOther(Boolean(draft.issuedBy));
                setBirthDate(migrateDateValue(draft.birthDate));
                setAddress(draft.address ?? "");
                setConsent(draft.consent ?? false);
                setIncludeDocument(draft.includeDocument ?? false);
                setIdType(draft.idType ?? null);
            });
        }
        return () => {
            cancelled = true;
        };
    }, []);

    const identityComplete =
        includeDocument &&
        idType !== null &&
        /^[0-9]{9,12}$/.test(documentNumber.trim()) &&
        isValidDateDisplay(issueDate) &&
        dateDisplayToIso(issueDate) <= today &&
        Boolean(issuedBy.trim() && documentPhotos.front && documentPhotos.back);
    const personalComplete =
        fullName.trim().length >= 2 && isValidVietnamesePhone(phone);
    const completion = (emailVerified ? 25 : 0) + (personalComplete ? 75 : 0);
    const progressStyle = {
        "--profile-progress": `${completion}%`,
    } as CSSProperties;

    function readUpload(file: File | undefined, acceptPdf: boolean) {
        if (!file) return null;
        const allowed = acceptPdf
            ? ["image/jpeg", "image/png", "image/webp", "application/pdf"]
            : ["image/jpeg", "image/png", "image/webp"];
        if (!allowed.includes(file.type)) {
            setError(
                acceptPdf
                    ? "Choose a JPG, PNG, WebP or PDF file."
                    : "Choose a JPG, PNG or WebP image.",
            );
            return null;
        }
        if (file.size > 5 * 1024 * 1024) {
            setError("Each file must be 5 MB or smaller.");
            return null;
        }
        setError("");
        return {
            file,
            previewUrl: file.type.startsWith("image/")
                ? URL.createObjectURL(file)
                : "",
        };
    }

    function handleProfilePhoto(event: ChangeEvent<HTMLInputElement>) {
        const upload = readUpload(event.target.files?.[0], false);
        if (upload)
            setProfilePhoto((current) => {
                if (current?.previewUrl)
                    URL.revokeObjectURL(current.previewUrl);
                return upload;
            });
        event.target.value = "";
    }

    function handleDocumentPhoto(
        side: DocumentSide,
        event: ChangeEvent<HTMLInputElement>,
    ) {
        const upload = readUpload(event.target.files?.[0], true);
        if (upload)
            setDocumentPhotos((current) => {
                if (current[side]?.previewUrl)
                    URL.revokeObjectURL(current[side]!.previewUrl);
                return { ...current, [side]: upload };
            });
        event.target.value = "";
    }

    function removeProfilePhoto() {
        setProfilePhoto((current) => {
            if (current?.previewUrl) URL.revokeObjectURL(current.previewUrl);
            return null;
        });
    }

    function removeDocumentPhoto(side: DocumentSide) {
        setDocumentPhotos((current) => {
            if (current[side]?.previewUrl)
                URL.revokeObjectURL(current[side]!.previewUrl);
            return { ...current, [side]: null };
        });
    }

    function clearDocumentDetails() {
        setDocumentNumber("");
        setIssueDate("");
        setIssuedBy("");
        for (const upload of Object.values(documentPhotos)) {
            if (upload?.previewUrl) URL.revokeObjectURL(upload.previewUrl);
        }
        setDocumentPhotos({ front: null, back: null });
    }

    function changeDocumentType(nextType: IdType) {
        if (idType !== nextType) {
            clearDocumentDetails();
            setAuthorityOther(false);
        }
        setIdType(nextType);
    }

    function toggleDocumentDetails(enabled: boolean) {
        if (!enabled) {
            clearDocumentDetails();
            setIdType(null);
            setAuthorityOther(false);
        }
        setIncludeDocument(enabled);
    }

    function openDatePicker(input: HTMLInputElement | null) {
        if (!input) return;
        try {
            if (typeof input.showPicker === "function") input.showPicker();
            else input.click();
        } catch {
            input.click();
        }
    }

    function handleSubmit(event: FormEvent<HTMLFormElement>) {
        event.preventDefault();
        setError("");
        setNotice("");
        const nextErrors = {
            fullName:
                fullName.trim().length < 2
                    ? "Enter your full name (at least 2 characters)."
                    : "",
            phone: isValidVietnamesePhone(phone)
                ? ""
                : "Enter a valid Vietnamese phone number.",
            birthDate:
                birthDate &&
                (!isValidDateDisplay(birthDate) ||
                    dateDisplayToIso(birthDate) > today)
                    ? "Enter a valid date in DD/MM/YYYY format that is not in the future."
                    : "",
        };
        setValidationErrors(nextErrors);
        if (Object.values(nextErrors).some(Boolean)) return;
        if (!consent) {
            setError(
                "Please agree to the Terms of Service and Privacy Policy to continue.",
            );
            return;
        }
        if (!emailVerified) {
            setError(
                "Sign in with a verified email account to complete your profile.",
            );
            return;
        }

        setError(
            "Profile saving is unavailable because this project has no profile API yet. Your entries are still on this page.",
        );
    }

    function saveDraft() {
        setError("");
        try {
            localStorage.setItem(
                PROFILE_DRAFT_KEY,
                JSON.stringify({
                    fullName,
                    phone,
                    documentNumber,
                    issueDate,
                    issuedBy,
                    birthDate,
                    address,
                    consent,
                    includeDocument,
                    idType,
                }),
            );
            setNotice("Draft saved on this device.");
        } catch {
            setError(
                "Draft could not be saved. Your entries are still on this page.",
            );
        }
    }

    return (
        <div
            lang="en"
            className={`parkwise-page parkwise-profile-page ${fontClassName}`}
        >
            <ParkWiseNavbar
                variant="customer"
                navigationMode="customer"
                accountControl={
                    <CustomerAccountMenu
                        activeHref="/customer/complete-profile"
                        profilePhotoUrl={profilePhoto?.previewUrl}
                    />
                }
            />

            <main className="parkwise-profile-main">
                <div className="parkwise-profile-grid">
                    <div className="parkwise-profile-primary">
                        <div className="parkwise-profile-heading">
                            <h1>Complete Your Profile</h1>
                            <p>
                                Add a few details to get the most out of
                                ParkWise.
                            </p>
                        </div>
                        <section
                            className="parkwise-profile-progress-card"
                            aria-label="Profile completion progress"
                        >
                            <div className="parkwise-profile-progress-top">
                                <strong>Profile completion</strong>
                                <span>
                                    {personalComplete
                                        ? "Required details added"
                                        : "Add your name and phone number"}
                                </span>
                            </div>
                            <div
                                className="parkwise-profile-progress-track"
                                style={progressStyle}
                            >
                                <strong>{completion}%</strong>
                                <i />
                            </div>
                        </section>

                        <form
                            id="complete-profile-form"
                            className="parkwise-profile-form-card"
                            noValidate
                            onSubmit={handleSubmit}
                        >
                            <div className="parkwise-profile-form-heading">
                                <span className="parkwise-profile-heading-icon">
                                    <ProfileIcon />
                                </span>
                                <div>
                                    <h2>Personal Information</h2>
                                    <p>
                                        Use accurate details to help us keep
                                        your account up to date.
                                    </p>
                                </div>
                            </div>

                            <div className="parkwise-profile-photo-row">
                                <div className="parkwise-profile-photo-field">
                                    <label>
                                        Profile Photo <em>(Optional)</em>
                                    </label>
                                    <div className="parkwise-profile-photo-assets">
                                        <div className="parkwise-profile-avatar-wrap">
                                            <button
                                                className="parkwise-profile-avatar-large"
                                                type="button"
                                                onClick={() =>
                                                    profilePhotoInput.current?.click()
                                                }
                                                aria-label="Choose profile photo"
                                            >
                                                {profilePhoto?.previewUrl ? (
                                                    <Image
                                                        src={
                                                            profilePhoto.previewUrl
                                                        }
                                                        alt="Profile photo preview"
                                                        width={112}
                                                        height={112}
                                                        unoptimized
                                                    />
                                                ) : (
                                                    <ProfileIcon />
                                                )}
                                                <span>
                                                    <svg
                                                        viewBox="0 0 24 24"
                                                        fill="none"
                                                        stroke="currentColor"
                                                        strokeWidth="1.8"
                                                        aria-hidden="true"
                                                    >
                                                        <path d="M4 7h3l1.5-2h7L17 7h3v12H4z" />
                                                        <circle
                                                            cx="12"
                                                            cy="13"
                                                            r="3"
                                                        />
                                                    </svg>
                                                </span>
                                            </button>
                                        </div>
                                        <div className="parkwise-profile-photo-controls">
                                            <button
                                                type="button"
                                                className="parkwise-profile-upload-button"
                                                onClick={() =>
                                                    profilePhotoInput.current?.click()
                                                }
                                            >
                                                <UploadIcon />
                                                {profilePhoto
                                                    ? "Change Avatar"
                                                    : "Upload Avatar"}
                                            </button>
                                            <small>
                                                JPG, PNG or WebP · Max 5 MB
                                            </small>
                                            {profilePhoto && (
                                                <button
                                                    type="button"
                                                    className="parkwise-profile-file-remove"
                                                    onClick={removeProfilePhoto}
                                                >
                                                    Remove photo
                                                </button>
                                            )}
                                        </div>
                                        <input
                                            ref={profilePhotoInput}
                                            className="parkwise-profile-hidden-input"
                                            type="file"
                                            accept="image/jpeg,image/png,image/webp"
                                            onChange={handleProfilePhoto}
                                        />
                                    </div>
                                </div>
                                <div className="parkwise-profile-field parkwise-profile-name-field">
                                    <label htmlFor="profile-full-name">
                                        Full Name <span>*</span>
                                    </label>
                                    <div className="parkwise-profile-input-with-icon">
                                        <ProfileIcon />
                                        <input
                                            id="profile-full-name"
                                            name="fullName"
                                            type="text"
                                            autoComplete="name"
                                            placeholder="Enter your full name"
                                            minLength={2}
                                            maxLength={100}
                                            value={fullName}
                                            aria-invalid={Boolean(
                                                validationErrors.fullName,
                                            )}
                                            aria-describedby={
                                                validationErrors.fullName
                                                    ? "profile-name-error"
                                                    : undefined
                                            }
                                            onChange={(event) => {
                                                setFullName(event.target.value);
                                                setValidationErrors(
                                                    (current) => ({
                                                        ...current,
                                                        fullName: "",
                                                    }),
                                                );
                                            }}
                                        />
                                    </div>
                                    {validationErrors.fullName && (
                                        <small
                                            id="profile-name-error"
                                            className="parkwise-profile-inline-error"
                                            role="alert"
                                        >
                                            {validationErrors.fullName}
                                        </small>
                                    )}
                                </div>
                            </div>

                            <div className="parkwise-profile-section-title">
                                <h3>Contact Information</h3>
                            </div>
                            <div className="parkwise-profile-contact-grid">
                                <div className="parkwise-profile-field">
                                    <label htmlFor="profile-phone">
                                        Phone Number <span>*</span>
                                    </label>
                                    <div className="parkwise-profile-input-with-icon">
                                        <PhoneIcon />
                                        <input
                                            id="profile-phone"
                                            name="phone"
                                            type="tel"
                                            autoComplete="tel-national"
                                            placeholder="0901 234 567"
                                            value={phone}
                                            aria-invalid={Boolean(
                                                validationErrors.phone,
                                            )}
                                            aria-describedby={
                                                validationErrors.phone
                                                    ? "profile-phone-error"
                                                    : undefined
                                            }
                                            onChange={(event) => {
                                                setPhone(event.target.value);
                                                setValidationErrors(
                                                    (current) => ({
                                                        ...current,
                                                        phone: "",
                                                    }),
                                                );
                                            }}
                                        />
                                    </div>
                                    {validationErrors.phone && (
                                        <small
                                            id="profile-phone-error"
                                            className="parkwise-profile-inline-error"
                                            role="alert"
                                        >
                                            {validationErrors.phone}
                                        </small>
                                    )}
                                </div>
                                <div className="parkwise-profile-field">
                                    <label htmlFor="profile-email">Email</label>
                                    <div className="parkwise-profile-email">
                                        <MailIcon />
                                        <input
                                            id="profile-email"
                                            value={accountEmail}
                                            placeholder="Sign in to load your email"
                                            readOnly
                                            title={accountEmail}
                                        />
                                    </div>
                                    {emailVerified && (
                                        <small>
                                            This verified email cannot be
                                            changed.
                                        </small>
                                    )}
                                </div>
                            </div>
                            <div className="parkwise-profile-fields profile-additional-fields">
                                <div className="parkwise-profile-field">
                                    <label htmlFor="profile-birth-date">
                                        Date of Birth <em>(Optional)</em>
                                    </label>
                                    <div className="parkwise-profile-date-control">
                                        <input
                                            id="profile-birth-date"
                                            name="dateOfBirth"
                                            type="text"
                                            inputMode="numeric"
                                            autoComplete="bday"
                                            placeholder="DD/MM/YYYY"
                                            value={birthDate}
                                            aria-invalid={Boolean(
                                                validationErrors.birthDate,
                                            )}
                                            aria-describedby={
                                                validationErrors.birthDate
                                                    ? "profile-birth-date-error"
                                                    : undefined
                                            }
                                            onClick={() =>
                                                openDatePicker(
                                                    birthDatePickerRef.current,
                                                )
                                            }
                                            onChange={(event) => {
                                                setBirthDate(
                                                    event.target.value,
                                                );
                                                setValidationErrors(
                                                    (current) => ({
                                                        ...current,
                                                        birthDate: "",
                                                    }),
                                                );
                                            }}
                                        />
                                        <button
                                            type="button"
                                            aria-label="Open date of birth calendar"
                                            onClick={() =>
                                                openDatePicker(
                                                    birthDatePickerRef.current,
                                                )
                                            }
                                        >
                                            <CalendarIcon />
                                        </button>
                                        <input
                                            ref={birthDatePickerRef}
                                            className="parkwise-profile-native-date"
                                            type="date"
                                            aria-hidden="true"
                                            tabIndex={-1}
                                            max={today}
                                            value={dateDisplayToIso(birthDate)}
                                            onChange={(event) => {
                                                setBirthDate(
                                                    isoToDateDisplay(
                                                        event.target.value,
                                                    ),
                                                );
                                                setValidationErrors(
                                                    (current) => ({
                                                        ...current,
                                                        birthDate: "",
                                                    }),
                                                );
                                            }}
                                        />
                                    </div>
                                    {validationErrors.birthDate && (
                                        <small
                                            id="profile-birth-date-error"
                                            className="parkwise-profile-inline-error"
                                            role="alert"
                                        >
                                            {validationErrors.birthDate}
                                        </small>
                                    )}
                                </div>
                                <div className="parkwise-profile-field">
                                    <label htmlFor="profile-address">
                                        Address <em>(Optional)</em>
                                    </label>
                                    <div className="parkwise-profile-input-with-icon">
                                        <PinIcon />
                                        <input
                                            id="profile-address"
                                            name="address"
                                            type="text"
                                            autoComplete="street-address"
                                            placeholder="e.g. 23 Nguyen Hue, District 1"
                                            value={address}
                                            onChange={(event) =>
                                                setAddress(event.target.value)
                                            }
                                        />
                                    </div>
                                </div>
                            </div>

                            <div className="parkwise-profile-identity-toggle">
                                <span className="parkwise-profile-heading-icon">
                                    <ShieldIcon />
                                </span>
                                <div>
                                    <strong>
                                        Identity Documents <em>(Optional)</em>
                                    </strong>
                                </div>
                                <label className="parkwise-profile-switch">
                                    <input
                                        type="checkbox"
                                        checked={includeDocument}
                                        onChange={(event) =>
                                            toggleDocumentDetails(
                                                event.target.checked,
                                            )
                                        }
                                        aria-label="Add identity documents"
                                    />
                                    <span />
                                </label>
                                <div className="parkwise-profile-switch-copy">
                                    <strong>Add identity documents</strong>
                                    <small>
                                        Choose CCCD or driver&apos;s license
                                    </small>
                                </div>
                            </div>

                            {includeDocument && (
                                <section
                                    className="parkwise-profile-document-panel"
                                    aria-labelledby="document-heading"
                                >
                                    <div className="parkwise-profile-document-title">
                                        <div>
                                            <h3 id="document-heading">
                                                Identity Verification{" "}
                                                <em>(Optional)</em>
                                            </h3>
                                            <p>
                                                Documents are optional for basic
                                                profile completion.
                                            </p>
                                        </div>
                                        <ShieldIcon />
                                    </div>
                                    <div
                                        className="parkwise-profile-id-toggle"
                                        role="group"
                                        aria-label="Document type"
                                    >
                                        <button
                                            type="button"
                                            className={
                                                idType === "citizenId"
                                                    ? "selected"
                                                    : ""
                                            }
                                            aria-pressed={
                                                idType === "citizenId"
                                            }
                                            onClick={() =>
                                                changeDocumentType("citizenId")
                                            }
                                        >
                                            <IdCardIcon />
                                            <strong>
                                                Citizen ID Card (CCCD)
                                            </strong>
                                            <small>
                                                Citizen identification
                                            </small>
                                            <i />
                                        </button>
                                        <button
                                            type="button"
                                            className={
                                                idType === "driverLicense"
                                                    ? "selected"
                                                    : ""
                                            }
                                            aria-pressed={
                                                idType === "driverLicense"
                                            }
                                            onClick={() =>
                                                changeDocumentType(
                                                    "driverLicense",
                                                )
                                            }
                                        >
                                            <CarIcon />
                                            <strong>
                                                Driver&apos;s License (GPLX)
                                            </strong>
                                            <small>Driving license</small>
                                            <i />
                                        </button>
                                    </div>

                                    {idType === "citizenId" && (
                                        <div
                                            style={{
                                                margin: "14px 0 8px",
                                                display: "flex",
                                                flexWrap: "wrap",
                                                alignItems: "center",
                                                gap: "10px",
                                            }}
                                        >
                                            <button
                                                type="button"
                                                onClick={() => setVneidModalOpen(true)}
                                                style={{
                                                    background: "linear-gradient(135deg, #b91c1c, #991b1b)",
                                                    color: "#ffffff",
                                                    border: "1.5px solid #7f1d1d",
                                                    padding: "8px 16px",
                                                    borderRadius: "10px",
                                                    fontSize: "13px",
                                                    fontWeight: 700,
                                                    cursor: "pointer",
                                                    display: "inline-flex",
                                                    alignItems: "center",
                                                    gap: "6px",
                                                    boxShadow: "0 2px 6px rgba(185, 28, 28, 0.25)",
                                                }}
                                            >
                                                🇻🇳 Điền nhanh qua VNeID (Level 2 eID)
                                            </button>
                                            {vneidVerified && (
                                                <span
                                                    data-testid="customer-vneid-verified-badge"
                                                    style={{
                                                        display: "inline-flex",
                                                        alignItems: "center",
                                                        gap: "5px",
                                                        background: "#ecfdf5",
                                                        color: "#065f46",
                                                        border: "1px solid #a7f3d0",
                                                        padding: "6px 12px",
                                                        borderRadius: "8px",
                                                        fontSize: "12px",
                                                        fontWeight: 600,
                                                    }}
                                                >
                                                    ✓ Đã đối soát CSDL Dân cư Quốc gia VNeID
                                                </span>
                                            )}
                                        </div>
                                    )}

                                    {idType && (
                                        <>
                                            <div className="parkwise-profile-fields document-fields">
                                                <div className="parkwise-profile-field">
                                                    <label htmlFor="document-number">
                                                        Document Number
                                                    </label>
                                                    <input
                                                        id="document-number"
                                                        name="documentNumber"
                                                        type="text"
                                                        inputMode="numeric"
                                                        autoComplete="off"
                                                        placeholder="Enter document number"
                                                        maxLength={12}
                                                        value={documentNumber}
                                                        onChange={(event) =>
                                                            setDocumentNumber(
                                                                event.target
                                                                    .value,
                                                            )
                                                        }
                                                    />
                                                </div>
                                                <div className="parkwise-profile-field">
                                                    <label htmlFor="document-issue-date">
                                                        Issue Date
                                                    </label>
                                                    <div className="parkwise-profile-date-control">
                                                        <input
                                                            id="document-issue-date"
                                                            name="issueDate"
                                                            type="text"
                                                            inputMode="numeric"
                                                            placeholder="DD/MM/YYYY"
                                                            value={issueDate}
                                                            onClick={() =>
                                                                openDatePicker(
                                                                    issueDatePickerRef.current,
                                                                )
                                                            }
                                                            onChange={(event) =>
                                                                setIssueDate(
                                                                    event.target
                                                                        .value,
                                                                )
                                                            }
                                                        />
                                                        <button
                                                            type="button"
                                                            aria-label="Open issue date calendar"
                                                            onClick={() =>
                                                                openDatePicker(
                                                                    issueDatePickerRef.current,
                                                                )
                                                            }
                                                        >
                                                            <CalendarIcon />
                                                        </button>
                                                        <input
                                                            ref={
                                                                issueDatePickerRef
                                                            }
                                                            className="parkwise-profile-native-date"
                                                            type="date"
                                                            aria-hidden="true"
                                                            tabIndex={-1}
                                                            max={today}
                                                            value={dateDisplayToIso(
                                                                issueDate,
                                                            )}
                                                            onChange={(event) =>
                                                                setIssueDate(
                                                                    isoToDateDisplay(
                                                                        event
                                                                            .target
                                                                            .value,
                                                                    ),
                                                                )
                                                            }
                                                        />
                                                    </div>
                                                </div>
                                                <div className="parkwise-profile-field">
                                                    <label htmlFor="document-issued-by">
                                                        Issuing Authority
                                                    </label>
                                                    <div className="parkwise-profile-select">
                                                        <select
                                                            id="document-issued-by"
                                                            name="issuedByOption"
                                                            value={
                                                                authorityOther
                                                                    ? "other"
                                                                    : ""
                                                            }
                                                            onChange={(
                                                                event,
                                                            ) => {
                                                                setAuthorityOther(
                                                                    event.target
                                                                        .value ===
                                                                        "other",
                                                                );
                                                                setIssuedBy(
                                                                    event.target
                                                                        .value ===
                                                                        "other"
                                                                        ? issuedBy
                                                                        : "",
                                                                );
                                                            }}
                                                        >
                                                            <option value="">
                                                                Select an
                                                                authority
                                                            </option>
                                                            <option value="other">
                                                                Other (enter
                                                                manually)
                                                            </option>
                                                        </select>
                                                        <ChevronDownIcon />
                                                    </div>
                                                    {authorityOther && (
                                                        <input
                                                            aria-label="Enter issuing authority"
                                                            name="issuedBy"
                                                            type="text"
                                                            placeholder="Enter the issuing authority"
                                                            value={issuedBy}
                                                            onChange={(event) =>
                                                                setIssuedBy(
                                                                    event.target
                                                                        .value,
                                                                )
                                                            }
                                                        />
                                                    )}
                                                </div>
                                            </div>
                                            <div className="parkwise-profile-field parkwise-profile-document-uploads">
                                                <label>Document Images</label>
                                                <small>
                                                    Upload clear, readable
                                                    images. JPG, PNG, WebP or
                                                    PDF · Max 5 MB per file.
                                                </small>
                                                <div className="parkwise-profile-upload-grid">
                                                    {(
                                                        [
                                                            "front",
                                                            "back",
                                                        ] as const
                                                    ).map((side) => {
                                                        const upload =
                                                            documentPhotos[
                                                                side
                                                            ];
                                                        const label =
                                                            side === "front"
                                                                ? "Front image"
                                                                : "Back image";
                                                        const inputId = `document-${side}-upload`;
                                                        return (
                                                            <div
                                                                className={`parkwise-profile-doc-upload ${upload ? "has-file" : ""}`}
                                                                key={side}
                                                            >
                                                                {upload?.previewUrl ? (
                                                                    <Image
                                                                        src={
                                                                            upload.previewUrl
                                                                        }
                                                                        alt={`${label} preview`}
                                                                        width={
                                                                            240
                                                                        }
                                                                        height={
                                                                            160
                                                                        }
                                                                        unoptimized
                                                                    />
                                                                ) : (
                                                                    <span
                                                                        className="parkwise-profile-doc-placeholder"
                                                                        aria-hidden="true"
                                                                    >
                                                                        <IdCardIcon />
                                                                    </span>
                                                                )}
                                                                <label
                                                                    htmlFor={
                                                                        inputId
                                                                    }
                                                                >
                                                                    <strong>
                                                                        {upload
                                                                            ? "Change file"
                                                                            : "↑ Upload image"}
                                                                    </strong>
                                                                    <small>
                                                                        {upload
                                                                            ? upload
                                                                                  .file
                                                                                  .name
                                                                            : `${label} · JPG, PNG or PDF`}
                                                                    </small>
                                                                </label>
                                                                <input
                                                                    id={inputId}
                                                                    type="file"
                                                                    accept="image/jpeg,image/png,image/webp,application/pdf"
                                                                    onChange={(
                                                                        event,
                                                                    ) =>
                                                                        handleDocumentPhoto(
                                                                            side,
                                                                            event,
                                                                        )
                                                                    }
                                                                />
                                                                {upload && (
                                                                    <button
                                                                        type="button"
                                                                        aria-label={`Remove ${label.toLowerCase()}`}
                                                                        onClick={() =>
                                                                            removeDocumentPhoto(
                                                                                side,
                                                                            )
                                                                        }
                                                                    >
                                                                        ×
                                                                    </button>
                                                                )}
                                                            </div>
                                                        );
                                                    })}
                                                </div>
                                            </div>
                                        </>
                                    )}
                                </section>
                            )}

                            <div className="parkwise-profile-consent">
                                <input
                                    id="profile-consent"
                                    type="checkbox"
                                    name="consent"
                                    checked={consent}
                                    onChange={(event) =>
                                        setConsent(event.target.checked)
                                    }
                                />
                                <span>
                                    <label htmlFor="profile-consent">
                                        I agree to the
                                    </label>{" "}
                                    <Link
                                        href="/terms-and-conditions?from=complete-profile"
                                        target="_blank"
                                        rel="noopener noreferrer"
                                    >
                                        Terms &amp; Conditions
                                    </Link>{" "}
                                    and{" "}
                                    <Link
                                        href="/privacy-policy?from=complete-profile"
                                        target="_blank"
                                        rel="noopener noreferrer"
                                    >
                                        Privacy Policy
                                    </Link>{" "}
                                    of ParkWise.
                                </span>
                            </div>
                            {error && (
                                <p
                                    className="parkwise-profile-feedback error"
                                    role="alert"
                                >
                                    {error}
                                </p>
                            )}
                            {notice && (
                                <p
                                    className="parkwise-profile-feedback success"
                                    role="status"
                                    aria-live="polite"
                                >
                                    {notice}
                                </p>
                            )}
                            <div className="parkwise-profile-actions">
                                <button
                                    type="button"
                                    className="parkwise-profile-draft"
                                    onClick={saveDraft}
                                >
                                    <IdCardIcon /> Save Draft
                                </button>
                                <button
                                    className="parkwise-profile-submit"
                                    type="submit"
                                >
                                    Complete Profile
                                    <span aria-hidden="true">→</span>
                                </button>
                            </div>
                        </form>
                    </div>

                    <aside className="parkwise-profile-sidebar">
                        <section
                            className="parkwise-profile-status-card"
                            aria-labelledby="profile-status-title"
                        >
                            <h2 id="profile-status-title">
                                <IdCardIcon /> Profile Status
                            </h2>
                            <div className="parkwise-profile-score">
                                <div
                                    className="parkwise-profile-ring"
                                    style={progressStyle}
                                >
                                    <span>{completion}%</span>
                                </div>
                                <p>
                                    Your profile
                                    <strong>
                                        {completion === 100 ? (
                                            <>Ready to submit</>
                                        ) : (
                                            <>
                                                In progress{" "}
                                                <em>{completion}%</em>
                                            </>
                                        )}
                                    </strong>
                                    <small>
                                        {completion === 100
                                            ? "Your required details are ready to submit."
                                            : "Add your required details to continue."}
                                    </small>
                                </p>
                            </div>
                            <div className="parkwise-profile-status-list">
                                <div
                                    className={
                                        emailVerified ? "complete" : "pending"
                                    }
                                >
                                    <span>
                                        {emailVerified ? <CheckIcon /> : ""}
                                    </span>
                                    <p>
                                        <strong>Email</strong>
                                        <small>
                                            {emailVerified
                                                ? accountEmail
                                                : "Sign in to verify your email"}
                                        </small>
                                    </p>
                                    <b>
                                        {emailVerified
                                            ? "Verified"
                                            : "Required"}
                                    </b>
                                </div>
                                <div
                                    className={
                                        personalComplete
                                            ? "complete"
                                            : "pending"
                                    }
                                >
                                    <span>
                                        {personalComplete ? <CheckIcon /> : ""}
                                    </span>
                                    <p>
                                        <strong>Basic Information</strong>
                                        <small>
                                            Full name and phone number
                                        </small>
                                    </p>
                                    <b>
                                        {personalComplete
                                            ? "Complete"
                                            : "Required"}
                                    </b>
                                </div>
                                <div
                                    className={
                                        identityComplete
                                            ? "complete"
                                            : "pending"
                                    }
                                >
                                    <span>
                                        {identityComplete ? <CheckIcon /> : ""}
                                    </span>
                                    <p>
                                        <strong>Identity Documents</strong>
                                        <small>CCCD/GPLX (optional)</small>
                                    </p>
                                    <b>
                                        {identityComplete
                                            ? "Added"
                                            : "Optional"}
                                    </b>
                                </div>
                            </div>
                        </section>

                        <section className="parkwise-profile-privacy-card">
                            <h2>
                                <span>
                                    <LockIcon />
                                </span>{" "}
                                Your information is protected
                            </h2>
                            <ul>
                                <li>
                                    <span>
                                        <ShieldIcon />
                                    </span>
                                    <p>
                                        We use your details to provide ParkWise
                                        services.
                                    </p>
                                </li>
                                <li>
                                    <span>
                                        <LockIcon />
                                    </span>
                                    <p>
                                        You can review or update your profile
                                        information at any time.
                                    </p>
                                </li>
                            </ul>
                        </section>

                        <section className="parkwise-profile-support-card">
                            <span className="parkwise-profile-support-icon">
                                <SupportIcon />
                            </span>
                            <div>
                                <h2>Need Help?</h2>
                                <p>
                                    Visit ParkWise to learn more about our
                                    services.
                                </p>
                                <Link href="/#about">
                                    About ParkWise{" "}
                                    <span aria-hidden="true">→</span>
                                </Link>
                            </div>
                        </section>
                    </aside>
                </div>
            </main>
            <ParkWiseFooter />

            <VneidAuthModal
                isOpen={vneidModalOpen}
                onClose={() => setVneidModalOpen(false)}
                onConfirm={handleVneidConfirm}
                defaultCccd="000000000001"
                title="Xác thực Căn cước công dân qua VNeID"
                subtitle="Chia sẻ thông tin công dân để hoàn tất hồ sơ khách hàng ParkWise"
            />
        </div>
    );
}
