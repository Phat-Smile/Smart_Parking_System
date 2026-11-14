"use client";

import {
    useCallback,
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
import { useRouter } from "next/navigation";
import { ShieldCheck } from "lucide-react";
import {
    getAuthSessionSnapshot,
    subscribeAuthSession,
} from "@/features/auth/authSession";
import { useCustomerLanguage } from "./i18n/customerLanguage";
import { ParkWiseFooter } from "@/features/parkwise/ParkWiseFooter";
import { CustomerHeader } from "./CustomerShell";
import type { VneidCitizenProfile } from "@/features/simulator/vneid/vneidSeedData";
import {
    readVneidDemoShare,
    VNEID_SHARE_KEY,
} from "@/features/simulator/vneid/vneidDemoSession";
import {
    getMyProfile,
    saveMyProfile,
    uploadCustomerMedia,
} from "./customerProfileApi";

type IdType = "citizenId" | "driverLicense";
type DocumentSide = "front" | "back";
type UploadedFile = { file: File; previewUrl: string } | null;
const PROFILE_DRAFT_KEY = "parkwise:customer:complete-profile:draft";
// Keep unsaved fields and selected files in memory during the local demo round trip.
// No identity document or file is persisted to browser storage by this draft.
type VneidReturnDraft = {
    accountId: string | null;
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
    authorityOther: boolean;
    vneidDemoApplied: boolean;
    profilePhoto: UploadedFile;
    documentPhotos: Record<DocumentSide, UploadedFile>;
    savedMedia: {
        avatarUrl: string | null;
        frontId: string | null;
        backId: string | null;
    };
};
let vneidReturnDraft: VneidReturnDraft | null = null;

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
    const { language, copy } = useCustomerLanguage();
    const router = useRouter();
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
    const [saving, setSaving] = useState(false);
    const [savedMedia, setSavedMedia] = useState<{
        avatarUrl: string | null;
        frontId: string | null;
        backId: string | null;
    }>({ avatarUrl: null, frontId: null, backId: null });
    const [vneidDemoApplied, setVneidDemoApplied] = useState(false);
    const vneidAppliedRef = useRef(false);

    const applyVneidProfile = useCallback(
        (profile: VneidCitizenProfile) => {
            vneidAppliedRef.current = true;
            setFullName(profile.fullName);
            setBirthDate(profile.dobDisplay);
            setAddress(profile.residence);
            setIdType("citizenId");
            setIncludeDocument(true);
            setDocumentNumber(profile.cccd);
            setIssueDate(profile.issueDate);
            setIssuedBy(
                copy(
                    "Sample issuing authority (demo)",
                    "Cơ quan cấp mẫu (mô phỏng)",
                ),
            );
            setAuthorityOther(true);
            setDocumentPhotos({ front: null, back: null });
            setSavedMedia((current) => ({
                ...current,
                frontId: null,
                backId: null,
            }));
            setVneidDemoApplied(true);
            setNotice(
                copy(
                    "Sample data filled in. ParkWise is not connected to VNeID and this information is not verified.",
                    "Đã điền dữ liệu mẫu. ParkWise chưa kết nối VNeID và thông tin này chưa được xác minh.",
                ),
            );
        },
        [copy],
    );
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
        const draft = vneidReturnDraft;
        if (
            !draft ||
            draft.accountId !== (getAuthSessionSnapshot()?.account.id ?? null)
        )
            return;
        let active = true;
        vneidAppliedRef.current = true;
        queueMicrotask(() => {
            if (!active) return;
            vneidReturnDraft = null;
            setFullName(draft.fullName);
            setPhone(draft.phone);
            setDocumentNumber(draft.documentNumber);
            setIssueDate(draft.issueDate);
            setIssuedBy(draft.issuedBy);
            setBirthDate(draft.birthDate);
            setAddress(draft.address);
            setConsent(draft.consent);
            setIncludeDocument(draft.includeDocument);
            setIdType(draft.idType);
            setAuthorityOther(draft.authorityOther);
            setVneidDemoApplied(draft.vneidDemoApplied);
            setProfilePhoto(draft.profilePhoto);
            setDocumentPhotos(draft.documentPhotos);
            setSavedMedia(draft.savedMedia);
        });
        return () => {
            active = false;
        };
    }, []);

    useEffect(() => {
        let cancelled = false;
        void getMyProfile()
            .then((profile) => {
                if (cancelled || vneidAppliedRef.current) return;
                setFullName(profile.fullName || authSession?.displayName || "");
                setPhone(
                    profile.phoneNumber ||
                        authSession?.account.phoneNumber ||
                        "",
                );
                setBirthDate(
                    profile.dateOfBirth
                        ? isoToDateDisplay(profile.dateOfBirth)
                        : "",
                );
                setAddress(profile.address || "");
                setIdType(profile.identityDocumentType);
                setIncludeDocument(Boolean(profile.identityDocumentType));
                const maskedNumber =
                    profile.identityDocumentType === "citizenId"
                        ? profile.citizenIdNumber
                        : profile.driverLicenseNumber;
                setDocumentNumber(
                    maskedNumber && !maskedNumber.includes("*")
                        ? maskedNumber
                        : "",
                );
                setIssueDate(
                    profile.identityIssueDate
                        ? isoToDateDisplay(profile.identityIssueDate)
                        : "",
                );
                setIssuedBy(profile.identityIssuedBy || "");
                setSavedMedia({
                    avatarUrl: profile.avatarUrl,
                    frontId: profile.identityFrontMediaId,
                    backId: profile.identityBackMediaId,
                });
            })
            .catch(() => {
                /* New accounts may not have a profile row yet. */
            });
        return () => {
            cancelled = true;
        };
    }, [authSession]);

    useEffect(() => {
        let active = true;
        let timer: number | undefined;
        try {
            const sharedProfile = sessionStorage.getItem(VNEID_SHARE_KEY);
            if (!sharedProfile) return;
            const profile =
                new URLSearchParams(window.location.search).get("vneid") ===
                "shared"
                    ? readVneidDemoShare(
                          sharedProfile,
                          getAuthSessionSnapshot()?.account.id ?? null,
                      )
                    : null;
            if (profile) {
                vneidAppliedRef.current = true;
                timer = window.setTimeout(() => {
                    if (!active) return;
                    applyVneidProfile(profile);
                    sessionStorage.removeItem(VNEID_SHARE_KEY);
                    router.replace("/customer/complete-profile", {
                        scroll: false,
                    });
                }, 0);
            } else sessionStorage.removeItem(VNEID_SHARE_KEY);
        } catch {
            // Storage can be unavailable; the manual profile form remains usable.
        }
        return () => {
            active = false;
            if (timer !== undefined) window.clearTimeout(timer);
        };
    }, [applyVneidProfile, router]);

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
        const timer = window.setTimeout(() => setNotice(""), 6000);
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
        if (draft && !vneidAppliedRef.current) {
            // Defer browser-only draft hydration until after the first render to keep SSR markup stable.
            queueMicrotask(() => {
                if (cancelled || !draft || vneidAppliedRef.current) return;
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
        Boolean(
            issuedBy.trim() &&
            (vneidDemoApplied || (documentPhotos.front && documentPhotos.back)),
        );
    const personalComplete =
        fullName.trim().length >= 2 &&
        (!phone.trim() || isValidVietnamesePhone(phone));
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
            setError(
                copy(
                    "Each file must be 5 MB or smaller.",
                    "Mỗi tệp phải có dung lượng từ 5 MB trở xuống.",
                ),
            );
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

    async function handleSubmit(event: FormEvent<HTMLFormElement>) {
        event.preventDefault();
        setError("");
        setNotice("");
        const nextErrors = {
            fullName:
                fullName.trim().length < 2
                    ? copy(
                          "Enter your full name (at least 2 characters).",
                          "Nhập họ tên đầy đủ (ít nhất 2 ký tự).",
                      )
                    : "",
            phone:
                phone.trim() && !isValidVietnamesePhone(phone)
                    ? copy(
                          "Enter a valid Vietnamese phone number.",
                          "Nhập số điện thoại Việt Nam hợp lệ.",
                      )
                    : "",
            birthDate:
                birthDate &&
                (!isValidDateDisplay(birthDate) ||
                    dateDisplayToIso(birthDate) > today)
                    ? copy(
                          "Enter a valid date in DD/MM/YYYY format that is not in the future.",
                          "Nhập ngày hợp lệ theo định dạng DD/MM/YYYY và không ở tương lai.",
                      )
                    : "",
        };
        setValidationErrors(nextErrors);
        if (Object.values(nextErrors).some(Boolean)) return;
        if (!consent) {
            setError(
                copy(
                    "Please agree to the Terms of Service and Privacy Policy to continue.",
                    "Vui lòng đồng ý với Điều khoản dịch vụ và Chính sách quyền riêng tư để tiếp tục.",
                ),
            );
            return;
        }
        if (!emailVerified) {
            setError(
                copy(
                    "Sign in with a verified email account to complete your profile.",
                    "Đăng nhập bằng tài khoản email đã xác minh để hoàn thiện hồ sơ.",
                ),
            );
            return;
        }

        if (
            includeDocument &&
            (!idType ||
                !/^[0-9]{9,12}$/.test(documentNumber.trim()) ||
                !isValidDateDisplay(issueDate) ||
                dateDisplayToIso(issueDate) > today ||
                !issuedBy.trim() ||
                (!vneidDemoApplied &&
                    !(documentPhotos.front || savedMedia.frontId)) ||
                (!vneidDemoApplied &&
                    !(documentPhotos.back || savedMedia.backId)))
        ) {
            setError(
                copy(
                    "Complete the identity document details and upload both sides.",
                    "Điền đầy đủ thông tin giấy tờ và tải ảnh hai mặt.",
                ),
            );
            return;
        }
        setSaving(true);
        try {
            const [avatar, front, back] = await Promise.all([
                profilePhoto
                    ? uploadCustomerMedia("avatar", profilePhoto.file)
                    : Promise.resolve(null),
                documentPhotos.front
                    ? uploadCustomerMedia(
                          "identity-front",
                          documentPhotos.front.file,
                      )
                    : Promise.resolve(null),
                documentPhotos.back
                    ? uploadCustomerMedia(
                          "identity-back",
                          documentPhotos.back.file,
                      )
                    : Promise.resolve(null),
            ]);
            const saved = await saveMyProfile({
                fullName: fullName.trim(),
                phoneNumber: phone.trim() || null,
                dateOfBirth: birthDate ? dateDisplayToIso(birthDate) : null,
                address: address.trim() || null,
                avatarUrl: avatar?.url ?? savedMedia.avatarUrl,
                identityDocumentType: includeDocument ? idType : null,
                citizenIdNumber:
                    includeDocument && idType === "citizenId"
                        ? documentNumber.trim()
                        : null,
                driverLicenseNumber:
                    includeDocument && idType === "driverLicense"
                        ? documentNumber.trim()
                        : null,
                identityIssueDate: includeDocument
                    ? dateDisplayToIso(issueDate)
                    : null,
                identityIssuedBy: includeDocument ? issuedBy.trim() : null,
                identityFrontMediaId: includeDocument
                    ? (front?.id ?? savedMedia.frontId)
                    : null,
                identityBackMediaId: includeDocument
                    ? (back?.id ?? savedMedia.backId)
                    : null,
            });
            setSavedMedia({
                avatarUrl: saved.avatarUrl,
                frontId: saved.identityFrontMediaId,
                backId: saved.identityBackMediaId,
            });
            window.dispatchEvent(
                new CustomEvent("customer-profile-name-changed", {
                    detail: saved.fullName,
                }),
            );
            localStorage.removeItem(PROFILE_DRAFT_KEY);
            router.push("/customer/dashboard");
        } catch {
            setError(
                copy(
                    "Could not save your profile. Please try again.",
                    "Không thể lưu hồ sơ. Vui lòng thử lại.",
                ),
            );
        } finally {
            setSaving(false);
        }
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
            setNotice(
                copy(
                    "Draft saved on this device.",
                    "Đã lưu bản nháp trên thiết bị này.",
                ),
            );
        } catch {
            setError(
                copy(
                    "Draft could not be saved. Your entries are still on this page.",
                    "Không thể lưu bản nháp. Thông tin bạn nhập vẫn còn trên trang.",
                ),
            );
        }
    }

    function openVneidDemo() {
        vneidReturnDraft = {
            accountId: authSession?.account.id ?? null,
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
            authorityOther,
            vneidDemoApplied,
            profilePhoto,
            documentPhotos,
            savedMedia,
        };
        router.push("/simulator/vneid?return=complete-profile");
    }

    return (
        <div
            lang={language}
            className={`parkwise-page customer-shell-page parkwise-profile-page ${fontClassName}`}
        >
            <CustomerHeader
                active="completeProfile"
                profilePhotoUrl={profilePhoto?.previewUrl}
            />

            <main className="parkwise-profile-main">
                <div className="parkwise-profile-grid">
                    <div className="parkwise-profile-primary">
                        <div className="parkwise-profile-heading">
                            <span className="parkwise-profile-eyebrow">
                                {copy("ACCOUNT SETUP", "THIẾT LẬP TÀI KHOẢN")}
                            </span>
                            <h1>
                                {copy(
                                    "Complete Your Profile",
                                    "Hoàn thiện hồ sơ",
                                )}
                            </h1>
                            <p>
                                {copy(
                                    "Add your details to make your ParkWise account ready to use.",
                                    "Bổ sung thông tin để hoàn tất và sử dụng tài khoản ParkWise.",
                                )}
                            </p>
                        </div>
                        <section
                            className="parkwise-profile-progress-card"
                            aria-label={copy(
                                "Profile completion progress",
                                "Tiến độ hoàn thiện hồ sơ",
                            )}
                        >
                            <div className="parkwise-profile-progress-top">
                                <strong>
                                    {copy(
                                        "Profile completion",
                                        "Hoàn thiện hồ sơ",
                                    )}
                                </strong>
                                <span>
                                    {personalComplete
                                        ? copy(
                                              "Required details added",
                                              "Đã thêm thông tin bắt buộc",
                                          )
                                        : copy(
                                              "Add your full name",
                                              "Thêm họ và tên",
                                          )}
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
                                    <h2>
                                        {copy(
                                            "Personal Information",
                                            "Thông tin cá nhân",
                                        )}
                                    </h2>
                                    <p>
                                        {copy(
                                            "Use accurate details to help us keep your account up to date.",
                                            "Nhập thông tin chính xác để hồ sơ luôn được cập nhật.",
                                        )}
                                    </p>
                                </div>
                            </div>

                            <div className="parkwise-profile-photo-row">
                                <div className="parkwise-profile-photo-field">
                                    <label>
                                        {copy("Profile Photo", "Ảnh đại diện")}{" "}
                                        <em>
                                            {copy(
                                                "(Optional)",
                                                "(Không bắt buộc)",
                                            )}
                                        </em>
                                    </label>
                                    <div className="parkwise-profile-photo-assets">
                                        <div className="parkwise-profile-avatar-wrap">
                                            <button
                                                className="parkwise-profile-avatar-large"
                                                type="button"
                                                onClick={() =>
                                                    profilePhotoInput.current?.click()
                                                }
                                                aria-label={copy(
                                                    "Choose profile photo",
                                                    "Chọn ảnh đại diện",
                                                )}
                                            >
                                                {profilePhoto?.previewUrl ? (
                                                    <Image
                                                        src={
                                                            profilePhoto.previewUrl
                                                        }
                                                        alt={copy(
                                                            "Profile photo preview",
                                                            "Ảnh đại diện xem trước",
                                                        )}
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
                                                    ? copy(
                                                          "Change Avatar",
                                                          "Đổi ảnh đại diện",
                                                      )
                                                    : copy(
                                                          "Upload Avatar",
                                                          "Tải ảnh đại diện",
                                                      )}
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
                                                    {copy(
                                                        "Remove photo",
                                                        "Xóa ảnh",
                                                    )}
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
                                        {copy("Full Name", "Họ và tên")}{" "}
                                        <span>*</span>
                                    </label>
                                    <div className="parkwise-profile-input-with-icon">
                                        <ProfileIcon />
                                        <input
                                            id="profile-full-name"
                                            name="fullName"
                                            type="text"
                                            autoComplete="name"
                                            placeholder={copy(
                                                "Enter your full name",
                                                "Nhập họ và tên",
                                            )}
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
                                <h3>
                                    {copy(
                                        "Contact Information",
                                        "Thông tin liên hệ",
                                    )}
                                </h3>
                            </div>
                            <div className="parkwise-profile-contact-grid">
                                <div className="parkwise-profile-field">
                                    <label htmlFor="profile-phone">
                                        {copy("Phone Number", "Số điện thoại")}
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
                                            placeholder={copy(
                                                "Sign in to load your email",
                                                "Đăng nhập để tải email",
                                            )}
                                            readOnly
                                            title={accountEmail}
                                        />
                                    </div>
                                    {emailVerified && (
                                        <small>
                                            {copy(
                                                "This verified email cannot be changed.",
                                                "Email đã xác minh không thể thay đổi.",
                                            )}
                                        </small>
                                    )}
                                </div>
                            </div>
                            <div className="parkwise-profile-fields profile-additional-fields">
                                <div className="parkwise-profile-field">
                                    <label htmlFor="profile-birth-date">
                                        {copy("Date of Birth", "Ngày sinh")}{" "}
                                        <em>
                                            {copy(
                                                "(Optional)",
                                                "(Không bắt buộc)",
                                            )}
                                        </em>
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
                                        {copy("Address", "Địa chỉ")}{" "}
                                        <em>
                                            {copy(
                                                "(Optional)",
                                                "(Không bắt buộc)",
                                            )}
                                        </em>
                                    </label>
                                    <div className="parkwise-profile-input-with-icon">
                                        <PinIcon />
                                        <input
                                            id="profile-address"
                                            name="address"
                                            type="text"
                                            autoComplete="street-address"
                                            placeholder={copy(
                                                "e.g. 23 Nguyen Hue, District 1",
                                                "Ví dụ: 23 Nguyễn Huệ, Quận 1",
                                            )}
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
                                        {copy(
                                            "Identity Documents",
                                            "Giấy tờ tùy thân",
                                        )}{" "}
                                        <em>
                                            {copy(
                                                "(Optional)",
                                                "(Không bắt buộc)",
                                            )}
                                        </em>
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
                                        aria-label={copy(
                                            "Add identity documents",
                                            "Thêm giấy tờ tùy thân",
                                        )}
                                    />
                                    <span />
                                </label>
                                <div className="parkwise-profile-switch-copy">
                                    <strong>
                                        {copy(
                                            "Add identity documents",
                                            "Thêm giấy tờ tùy thân",
                                        )}
                                    </strong>
                                    <small>
                                        {copy(
                                            "Choose CCCD or driver's license",
                                            "Chọn CCCD hoặc giấy phép lái xe",
                                        )}
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
                                                {copy(
                                                    "Identity Verification",
                                                    "Xác minh danh tính",
                                                )}{" "}
                                                <em>
                                                    {copy(
                                                        "(Optional)",
                                                        "(Không bắt buộc)",
                                                    )}
                                                </em>
                                            </h3>
                                            <p>
                                                {copy(
                                                    "Documents are optional for basic profile completion.",
                                                    "Giấy tờ không bắt buộc để hoàn thiện hồ sơ cơ bản.",
                                                )}
                                            </p>
                                        </div>
                                        <ShieldIcon />
                                    </div>
                                    <div
                                        className="parkwise-profile-id-toggle"
                                        role="group"
                                        aria-label={copy(
                                            "Document type",
                                            "Loại giấy tờ",
                                        )}
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
                                                {copy(
                                                    "Citizen ID Card (CCCD)",
                                                    "Căn cước công dân (CCCD)",
                                                )}
                                            </strong>
                                            <small>
                                                {copy(
                                                    "Citizen identification",
                                                    "Giấy tờ định danh",
                                                )}
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
                                                {copy(
                                                    "Driver's License (GPLX)",
                                                    "Giấy phép lái xe (GPLX)",
                                                )}
                                            </strong>
                                            <small>
                                                {copy(
                                                    "Driving license",
                                                    "Bằng lái xe",
                                                )}
                                            </small>
                                            <i />
                                        </button>
                                    </div>

                                    <div className="parkwise-profile-vneid-actions">
                                        <button
                                            type="button"
                                            onClick={openVneidDemo}
                                            className="parkwise-profile-vneid-button"
                                        >
                                            <ShieldCheck
                                                size={16}
                                                aria-hidden="true"
                                            />
                                            {copy(
                                                "Try VNeID demo",
                                                "Dùng thử mô phỏng VNeID",
                                            )}
                                        </button>
                                        {vneidDemoApplied && (
                                            <span
                                                data-testid="customer-vneid-demo-badge"
                                                className="parkwise-profile-vneid-badge"
                                            >
                                                <ShieldCheck
                                                    size={15}
                                                    aria-hidden="true"
                                                />
                                                {copy(
                                                    "Sample data · not verified",
                                                    "Dữ liệu mẫu · chưa xác minh",
                                                )}
                                            </span>
                                        )}
                                    </div>

                                    {idType && (
                                        <>
                                            <div className="parkwise-profile-fields document-fields">
                                                <div className="parkwise-profile-field">
                                                    <label htmlFor="document-number">
                                                        {copy(
                                                            "Document Number",
                                                            "Số giấy tờ",
                                                        )}
                                                    </label>
                                                    <input
                                                        id="document-number"
                                                        name="documentNumber"
                                                        type="text"
                                                        inputMode="numeric"
                                                        autoComplete="off"
                                                        placeholder={copy(
                                                            "Enter document number",
                                                            "Nhập số giấy tờ",
                                                        )}
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
                                                        {copy(
                                                            "Issue Date",
                                                            "Ngày cấp",
                                                        )}
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
                                                        {copy(
                                                            "Issuing Authority",
                                                            "Nơi cấp",
                                                        )}
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
                                                                {copy(
                                                                    "Select an authority",
                                                                    "Chọn nơi cấp",
                                                                )}
                                                            </option>
                                                            <option value="other">
                                                                {copy(
                                                                    "Other (enter manually)",
                                                                    "Khác (nhập thủ công)",
                                                                )}
                                                            </option>
                                                        </select>
                                                        <ChevronDownIcon />
                                                    </div>
                                                    {authorityOther && (
                                                        <input
                                                            aria-label={copy(
                                                                "Enter issuing authority",
                                                                "Nhập nơi cấp",
                                                            )}
                                                            name="issuedBy"
                                                            type="text"
                                                            placeholder={copy(
                                                                "Enter the issuing authority",
                                                                "Nhập nơi cấp",
                                                            )}
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
                                                <label>
                                                    {copy(
                                                        "Document Images",
                                                        "Ảnh giấy tờ",
                                                    )}
                                                </label>
                                                <small>
                                                    {copy(
                                                        "Upload clear, readable images. JPG, PNG, WebP or PDF · Max 5 MB per file.",
                                                        "Tải ảnh rõ nét, dễ đọc. JPG, PNG, WebP hoặc PDF · tối đa 5 MB mỗi tệp.",
                                                    )}
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
                                                                ? copy(
                                                                      "Front image",
                                                                      "Mặt trước",
                                                                  )
                                                                : copy(
                                                                      "Back image",
                                                                      "Mặt sau",
                                                                  );
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
                                                                            ? copy(
                                                                                  "Change file",
                                                                                  "Đổi tệp",
                                                                              )
                                                                            : copy(
                                                                                  "↑ Upload image",
                                                                                  "↑ Tải ảnh lên",
                                                                              )}
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
                                        {copy(
                                            "I agree to the",
                                            "Tôi đồng ý với",
                                        )}
                                    </label>{" "}
                                    <Link
                                        href="/terms-and-conditions?from=complete-profile"
                                        target="_blank"
                                        rel="noopener noreferrer"
                                    >
                                        {copy(
                                            "Terms & Conditions",
                                            "Điều khoản sử dụng",
                                        )}
                                    </Link>{" "}
                                    {copy("and", "và")}{" "}
                                    <Link
                                        href="/privacy-policy?from=complete-profile"
                                        target="_blank"
                                        rel="noopener noreferrer"
                                    >
                                        {copy(
                                            "Privacy Policy",
                                            "Chính sách bảo mật",
                                        )}
                                    </Link>{" "}
                                    {copy("of ParkWise.", "của ParkWise.")}
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
                                    <IdCardIcon />{" "}
                                    {copy("Save Draft", "Lưu bản nháp")}
                                </button>
                                <button
                                    className="parkwise-profile-submit"
                                    type="submit"
                                    disabled={saving}
                                >
                                    {copy(
                                        saving ? "Saving…" : "Complete Profile",
                                        "Hoàn thiện hồ sơ",
                                    )}
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
                                <IdCardIcon />{" "}
                                {copy("Profile Status", "Trạng thái hồ sơ")}
                            </h2>
                            <div className="parkwise-profile-score">
                                <div
                                    className="parkwise-profile-ring"
                                    style={progressStyle}
                                >
                                    <span>{completion}%</span>
                                </div>
                                <p>
                                    {copy("Your profile", "Hồ sơ của bạn")}
                                    <strong>
                                        {completion === 100 ? (
                                            <>
                                                {copy(
                                                    "Ready to submit",
                                                    "Sẵn sàng gửi",
                                                )}
                                            </>
                                        ) : (
                                            <>
                                                {copy(
                                                    "In progress",
                                                    "Đang hoàn thiện",
                                                )}{" "}
                                                <em>{completion}%</em>
                                            </>
                                        )}
                                    </strong>
                                    <small>
                                        {completion === 100
                                            ? copy(
                                                  "Your required details are ready to submit.",
                                                  "Thông tin bắt buộc đã sẵn sàng để gửi.",
                                              )
                                            : copy(
                                                  "Add your required details to continue.",
                                                  "Thêm thông tin bắt buộc để tiếp tục.",
                                              )}
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
                                                : copy(
                                                      "Sign in to verify your email",
                                                      "Đăng nhập để xác minh email",
                                                  )}
                                        </small>
                                    </p>
                                    <b>
                                        {emailVerified
                                            ? copy("Verified", "Đã xác minh")
                                            : copy("Required", "Bắt buộc")}
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
                                        <strong>
                                            {copy(
                                                "Basic Information",
                                                "Thông tin cơ bản",
                                            )}
                                        </strong>
                                        <small>
                                            {copy(
                                                "Full name (phone optional)",
                                                "Họ tên (số điện thoại tùy chọn)",
                                            )}
                                        </small>
                                    </p>
                                    <b>
                                        {personalComplete
                                            ? copy("Complete", "Hoàn tất")
                                            : copy("Required", "Bắt buộc")}
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
                                        <strong>
                                            {copy(
                                                "Identity Documents",
                                                "Giấy tờ tùy thân",
                                            )}
                                        </strong>
                                        <small>
                                            {copy(
                                                "CCCD/GPLX (optional)",
                                                "CCCD/GPLX (không bắt buộc)",
                                            )}
                                        </small>
                                    </p>
                                    <b>
                                        {identityComplete
                                            ? copy("Added", "Đã thêm")
                                            : copy(
                                                  "Optional",
                                                  "Không bắt buộc",
                                              )}
                                    </b>
                                </div>
                            </div>
                        </section>

                        <section className="parkwise-profile-privacy-card">
                            <h2>
                                <span>
                                    <LockIcon />
                                </span>{" "}
                                {copy(
                                    "Your information is protected",
                                    "Thông tin của bạn được bảo vệ",
                                )}
                            </h2>
                            <ul>
                                <li>
                                    <span>
                                        <ShieldIcon />
                                    </span>
                                    <p>
                                        {copy(
                                            "We use your details to provide ParkWise services.",
                                            "Chúng tôi dùng thông tin này để cung cấp dịch vụ ParkWise.",
                                        )}
                                    </p>
                                </li>
                                <li>
                                    <span>
                                        <LockIcon />
                                    </span>
                                    <p>
                                        {copy(
                                            "You can review or update your profile information at any time.",
                                            "Bạn có thể xem hoặc cập nhật hồ sơ bất cứ lúc nào.",
                                        )}
                                    </p>
                                </li>
                            </ul>
                        </section>

                        <section className="parkwise-profile-support-card">
                            <span className="parkwise-profile-support-icon">
                                <SupportIcon />
                            </span>
                            <div>
                                <h2>{copy("Need Help?", "Cần hỗ trợ?")}</h2>
                                <p>
                                    {copy(
                                        "Visit ParkWise to learn more about our services.",
                                        "Truy cập ParkWise để tìm hiểu thêm về dịch vụ.",
                                    )}
                                </p>
                                <Link href="/#about">
                                    {copy("About ParkWise", "Về ParkWise")}{" "}
                                    <span aria-hidden="true">→</span>
                                </Link>
                            </div>
                        </section>
                    </aside>
                </div>
            </main>
            <ParkWiseFooter language={language} />
        </div>
    );
}
