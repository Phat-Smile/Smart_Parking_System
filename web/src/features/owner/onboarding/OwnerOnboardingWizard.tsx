"use client";

import React, { useState, useEffect, useRef } from "react";
import Image from "next/image";
import Link from "next/link";
import { useRouter } from "next/navigation";
import {
    OwnerType,
    OwnerDocumentRequirementItem,
    DocumentVerificationField,
    OwnerDocumentDto,
    VietQrBusinessData,
    lookupBusinessTaxCode,
    getOwnerDocumentRequirements,
    getMyOwnerDocuments,
    getMyOwnerApplication,
    updateOwnerType,
    uploadOwnerDocument,
    deleteOwnerDocument,
    applyForOwnerApplication,
    resubmitOwnerApplication,
    describeOwnerApplicationError,
    getPendingDocumentFiles,
    clearPendingDocumentFiles,
    FALLBACK_REQUIREMENTS,
} from "@/features/owner/api/ownerLegalApi";
import { ApiError } from "@/shared/errors/api-error";
import "./onboarding.css";
import { getAuthSessionSnapshot } from "@/features/auth/authSession";
import { VneidAuthModal } from "@/features/simulator/vneid/VneidAuthModal";
import type { VneidCitizenProfile } from "@/features/simulator/vneid/vneidSeedData";

// The application belongs to the signed-in account, so an owner signs in before filling it.
const LOGIN_FOR_ONBOARDING = "/login?redirect=%2Fowner%2Fonboarding";

// SVG Icons
function BuildingIcon() {
    return (
        <svg
            width="22"
            height="22"
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            strokeWidth="2"
            strokeLinecap="round"
            strokeLinejoin="round"
        >
            <rect x="4" y="2" width="16" height="20" rx="2" ry="2" />
            <path d="M9 22v-4h6v4" />
            <path d="M8 6h.01" />
            <path d="M16 6h.01" />
            <path d="M8 10h.01" />
            <path d="M16 10h.01" />
            <path d="M8 14h.01" />
            <path d="M16 14h.01" />
        </svg>
    );
}

function StoreIcon() {
    return (
        <svg
            width="22"
            height="22"
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            strokeWidth="2"
            strokeLinecap="round"
            strokeLinejoin="round"
        >
            <path d="m2 7 4.41-4.41A2 2 0 0 1 7.83 2h8.34a2 2 0 0 1 1.42.59L22 7" />
            <path d="M4 12v8a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2v-8" />
            <path d="M15 22v-4a2 2 0 0 0-2-2h-2a2 2 0 0 0-2 2v4" />
            <path d="M2 7h20" />
            <path d="M22 7a2 2 0 0 1-2 2h-1a2 2 0 0 1-2-2" />
        </svg>
    );
}

function UserCheckIcon() {
    return (
        <svg
            width="22"
            height="22"
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            strokeWidth="2"
            strokeLinecap="round"
            strokeLinejoin="round"
        >
            <path d="M19 21v-2a4 4 0 0 0-4-4H9a4 4 0 0 0-4 4v2" />
            <circle cx="12" cy="7" r="4" />
            <polyline points="16 11 18 13 22 9" />
        </svg>
    );
}

function UploadIcon() {
    return (
        <svg
            width="26"
            height="26"
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            strokeWidth="2"
            strokeLinecap="round"
            strokeLinejoin="round"
        >
            <path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4" />
            <polyline points="17 8 12 3 7 8" />
            <line x1="12" y1="3" x2="12" y2="15" />
        </svg>
    );
}

function FileTextIcon() {
    return (
        <svg
            width="20"
            height="20"
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            strokeWidth="2"
            strokeLinecap="round"
            strokeLinejoin="round"
        >
            <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z" />
            <polyline points="14 2 14 8 20 8" />
            <line x1="16" y1="13" x2="8" y2="13" />
            <line x1="16" y1="17" x2="8" y2="17" />
            <polyline points="10 9 9 9 8 9" />
        </svg>
    );
}

function ShieldCheckIcon() {
    return (
        <svg
            width="48"
            height="48"
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            strokeWidth="2"
            strokeLinecap="round"
            strokeLinejoin="round"
        >
            <path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z" />
            <polyline points="9 12 11 14 15 10" />
        </svg>
    );
}

export function OwnerOnboardingWizard({
    fontClassName,
}: {
    fontClassName?: string;
}) {
    const router = useRouter();

    // Wizard state
    const [step, setStep] = useState<1 | 2 | 3 | 4>(1);
    const [loadingInitial, setLoadingInitial] = useState(true);

    // Form data (Step 1)
    const [ownerType, setOwnerType] = useState<OwnerType>("Enterprise");
    const [businessName, setBusinessName] = useState("");
    const [taxCode, setTaxCode] = useState("");
    const [fullName, setFullName] = useState("");
    const [phoneNumber, setPhoneNumber] = useState("");
    const [contactEmail, setContactEmail] = useState("");
    const [idCardNumber, setIdCardNumber] = useState("");
    const [idCardIssuedDate, setIdCardIssuedDate] = useState("");
    const [idCardIssuedPlace, setIdCardIssuedPlace] = useState("");
    const [permanentAddress, setPermanentAddress] = useState("");
    const [parkingAddress, setParkingAddress] = useState("");
    const [businessLicenseNumber, setBusinessLicenseNumber] = useState("");
    const [licenseIssuedDate, setLicenseIssuedDate] = useState("");
    const [licenseIssuedPlace, setLicenseIssuedPlace] = useState("");
    const [representativePosition, setRepresentativePosition] = useState("");
    const [shortName, setShortName] = useState("");

    // VietQR Tax Lookup State
    const [taxLookupLoading, setTaxLookupLoading] = useState(false);
    const [taxLookupError, setTaxLookupError] = useState<string | null>(null);
    const [taxLookupSuccess, setTaxLookupSuccess] =
        useState<VietQrBusinessData | null>(null);

    // VNeID Modal & Verification State
    const [vneidModalOpen, setVneidModalOpen] = useState(false);
    const [vneidVerified, setVneidVerified] = useState(false);
    const [vneidProfile, setVneidProfile] =
        useState<VneidCitizenProfile | null>(null);

    function handleVneidConfirm(profile: VneidCitizenProfile) {
        setIdCardNumber(profile.cccd);
        setFullName(profile.fullName);
        if (profile.issueDate && profile.issueDate.includes("/")) {
            const parts = profile.issueDate.split("/");
            if (parts.length === 3) {
                setIdCardIssuedDate(
                    `${parts[2]}-${parts[1].padStart(2, "0")}-${parts[0].padStart(2, "0")}`,
                );
            }
        }
        setIdCardIssuedPlace("Cục Cảnh sát QLHC về TTXH");
        if (profile.residence) {
            setPermanentAddress(profile.residence);
        }
        setVneidVerified(true);
        setVneidProfile(profile);
        setSuccessNotice(
            `Đã xác thực VNeID Mức 2 cho công dân ${profile.fullName} (CCCD: ${profile.cccd}).`,
        );
        setVneidModalOpen(false);
    }

    // Documents & Input fields (Step 2)
    const [requirements, setRequirements] = useState<
        OwnerDocumentRequirementItem[]
    >([]);
    const [uploadedDocuments, setUploadedDocuments] = useState<
        OwnerDocumentDto[]
    >([]);
    const [docNumbers, setDocNumbers] = useState<Record<string, string>>({});
    const [docVerifications, setDocVerifications] = useState<
        Record<string, Record<string, string>>
    >({});
    const [uploadingDocType, setUploadingDocType] = useState<string | null>(
        null,
    );

    // Confirmation (Step 3)
    const [agreedTerms, setAgreedTerms] = useState(false);
    const [isSubmitting, setIsSubmitting] = useState(false);

    // Status & Alerts
    const [errorMsg, setErrorMsg] = useState("");
    const [successNotice, setSuccessNotice] = useState("");
    const [applicationStatus, setApplicationStatus] = useState<string>("None");
    const [rejectionReason, setRejectionReason] = useState<string | null>(null);
    const [submittedAt, setSubmittedAt] = useState<string>("");

    const fileInputRefs = useRef<Record<string, HTMLInputElement | null>>({});

    // 1. Initial Load: Check if application already exists
    useEffect(() => {
        async function loadData() {
            if (!getAuthSessionSnapshot()) {
                router.replace(LOGIN_FOR_ONBOARDING);
                return;
            }
            try {
                const app = await getMyOwnerApplication();
                const docs = await getMyOwnerDocuments().catch(() => []);
                const urlParams =
                    typeof window !== "undefined"
                        ? new URLSearchParams(window.location.search)
                        : null;
                const forceStep1 =
                    urlParams?.get("step") === "1" ||
                    urlParams?.get("fresh") === "1";

                const isAlreadySubmitted =
                    !forceStep1 &&
                    (app.status === "Approved" ||
                        app.status === "Rejected" ||
                        (app.status === "Pending" &&
                            (Boolean(app.ownerType) || docs.length > 0)));

                if (isAlreadySubmitted) {
                    setApplicationStatus(app.status || "None");
                    setRejectionReason(app.rejectionReason || null);
                    setBusinessName(app.businessName || "");
                    setTaxCode(app.taxCode || "");
                    if (app.ownerType) setOwnerType(app.ownerType);
                    if (app.fullName) setFullName(app.fullName);
                    if (app.phoneNumber) setPhoneNumber(app.phoneNumber);
                    if (app.contactEmail) setContactEmail(app.contactEmail);
                    if (app.idCardNumber) setIdCardNumber(app.idCardNumber);
                    if (app.idCardIssuedDate)
                        setIdCardIssuedDate(app.idCardIssuedDate);
                    if (app.idCardIssuedPlace)
                        setIdCardIssuedPlace(app.idCardIssuedPlace);
                    if (app.permanentAddress)
                        setPermanentAddress(app.permanentAddress);
                    if (app.parkingAddress)
                        setParkingAddress(app.parkingAddress);
                    if (app.businessLicenseNumber)
                        setBusinessLicenseNumber(app.businessLicenseNumber);
                    if (app.licenseIssuedDate)
                        setLicenseIssuedDate(app.licenseIssuedDate);
                    if (app.licenseIssuedPlace)
                        setLicenseIssuedPlace(app.licenseIssuedPlace);
                    if (app.representativePosition)
                        setRepresentativePosition(app.representativePosition);
                    if (app.shortName) setShortName(app.shortName);
                    if (app.documentNumbers) setDocNumbers(app.documentNumbers);
                    if (app.documentVerifications)
                        setDocVerifications(app.documentVerifications);
                    setSubmittedAt(
                        app.reviewedAtUtc || new Date().toISOString(),
                    );
                    setUploadedDocuments(docs);
                    setStep(4);
                    return;
                }

                if (app.ownerType) setOwnerType(app.ownerType);
                if (app.businessName) setBusinessName(app.businessName);
                if (app.taxCode) setTaxCode(app.taxCode);
                if (app.fullName) setFullName(app.fullName);
                if (app.phoneNumber) setPhoneNumber(app.phoneNumber);
                if (app.contactEmail) setContactEmail(app.contactEmail);
                if (app.idCardNumber) setIdCardNumber(app.idCardNumber);
                if (app.idCardIssuedDate)
                    setIdCardIssuedDate(app.idCardIssuedDate);
                if (app.idCardIssuedPlace)
                    setIdCardIssuedPlace(app.idCardIssuedPlace);
                if (app.permanentAddress)
                    setPermanentAddress(app.permanentAddress);
                if (app.parkingAddress) setParkingAddress(app.parkingAddress);
                if (app.businessLicenseNumber)
                    setBusinessLicenseNumber(app.businessLicenseNumber);
                if (app.licenseIssuedDate)
                    setLicenseIssuedDate(app.licenseIssuedDate);
                if (app.licenseIssuedPlace)
                    setLicenseIssuedPlace(app.licenseIssuedPlace);
                if (app.representativePosition)
                    setRepresentativePosition(app.representativePosition);
                if (app.shortName) setShortName(app.shortName);
                if (app.documentNumbers) setDocNumbers(app.documentNumbers);
                if (app.documentVerifications)
                    setDocVerifications(app.documentVerifications);
                if (app.rejectionReason)
                    setRejectionReason(app.rejectionReason);

                // Load requirements for selected owner type
                try {
                    const reqs = await getOwnerDocumentRequirements(
                        app.ownerType || "Enterprise",
                    );
                    setRequirements(reqs.documents);
                } catch {
                    const fallback =
                        FALLBACK_REQUIREMENTS[app.ownerType || "Enterprise"] ||
                        FALLBACK_REQUIREMENTS.Enterprise;
                    if (fallback) setRequirements(fallback);
                }

                setUploadedDocuments(docs);

                const initialDocNums: Record<string, string> = {
                    ...(app.documentNumbers || {}),
                };
                const initialVerifs: Record<string, Record<string, string>> = {
                    ...(app.documentVerifications || {}),
                };

                docs.forEach((d) => {
                    if (d.documentNumber && !initialDocNums[d.documentType]) {
                        initialDocNums[d.documentType] = d.documentNumber;
                    }
                    if (d.verificationData && !initialVerifs[d.documentType]) {
                        initialVerifs[d.documentType] = d.verificationData;
                    }
                });

                setDocNumbers(initialDocNums);
                setDocVerifications(initialVerifs);
            } catch (err) {
                if (err instanceof ApiError && err.status === 401) {
                    router.replace(LOGIN_FOR_ONBOARDING);
                    return;
                }
                console.warn("Could not prefill owner application:", err);
            } finally {
                setLoadingInitial(false);
            }
        }
        loadData();
    }, [router]);

    // Update requirements when ownerType changes
    async function handleOwnerTypeChange(type: OwnerType) {
        setOwnerType(type);
        setErrorMsg("");
        setTaxLookupSuccess(null);
        setTaxLookupError(null);
        try {
            const res = await getOwnerDocumentRequirements(type);
            setRequirements(res.documents);
        } catch {
            const fallback = FALLBACK_REQUIREMENTS[type];
            if (fallback) {
                setRequirements(fallback);
            }
        }
    }

    // Tra cứu mã số thuế qua VietQR API
    async function handleLookupTaxCode() {
        setTaxLookupError(null);
        setTaxLookupSuccess(null);
        setErrorMsg("");

        const clean = taxCode.trim();
        if (!clean) {
            setTaxLookupError(
                "Vui lòng nhập mã số thuế trước khi bấm tra cứu.",
            );
            return;
        }

        setTaxLookupLoading(true);
        try {
            const data = await lookupBusinessTaxCode(clean);
            setTaxLookupSuccess(data);
            if (data.name) {
                setBusinessName(data.name);
            }
            if (data.address) {
                setPermanentAddress(data.address);
            }
            if (data.shortName) {
                setShortName(data.shortName);
            }
            if (!businessLicenseNumber) {
                setBusinessLicenseNumber(clean);
            }
            setSuccessNotice(`Đã tìm thấy thông tin: ${data.name}`);
        } catch (err: unknown) {
            const message = err instanceof Error ? err.message : "";
            setTaxLookupError(
                message ||
                    "Không thể tra cứu thông tin mã số thuế. Bạn vẫn có thể nhập thông tin thủ công.",
            );
        } finally {
            setTaxLookupLoading(false);
        }
    }

    // Handle Step 1 Validation & Next
    async function handleNextToStep2() {
        setErrorMsg("");
        if (!businessName.trim()) {
            setErrorMsg(
                ownerType === "Individual"
                    ? "Vui lòng nhập họ và tên chủ cơ sở bãi xe hoặc tên bãi xe."
                    : "Vui lòng nhập tên công ty / doanh nghiệp / hộ kinh doanh.",
            );
            return;
        }

        if (ownerType !== "Individual") {
            const cleanTax = taxCode.trim();
            if (!cleanTax) {
                setErrorMsg(
                    "Mã số thuế là bắt buộc đối với Doanh nghiệp hoặc Hộ kinh doanh.",
                );
                return;
            }
            const taxRegex = /^[0-9]{10}(-[0-9]{3})?$/;
            if (!taxRegex.test(cleanTax)) {
                setErrorMsg(
                    "Mã số thuế không đúng định dạng (phải gồm 10 số, hoặc 13 số dạng 0123456789-001).",
                );
                return;
            }
        }

        // Persist ownerType to backend before proceeding to Step 2
        try {
            await updateOwnerType(ownerType);
        } catch (err: unknown) {
            setErrorMsg(describeOwnerApplicationError(err));
            return;
        }

        // Auto-prefill document numbers for Step 2 if not yet set
        setDocNumbers((prev) => {
            const updated = { ...prev };
            if (ownerType === "Enterprise") {
                if (
                    !updated.EnterpriseRegistration &&
                    (businessLicenseNumber || taxCode)
                ) {
                    updated.EnterpriseRegistration =
                        businessLicenseNumber || taxCode;
                }
                if (!updated.NationalIdCard && idCardNumber) {
                    updated.NationalIdCard = idCardNumber;
                }
            } else if (ownerType === "HouseholdBusiness") {
                if (
                    !updated.HouseholdBusinessRegistration &&
                    (businessLicenseNumber || taxCode)
                ) {
                    updated.HouseholdBusinessRegistration =
                        businessLicenseNumber || taxCode;
                }
                if (!updated.NationalIdCard && idCardNumber) {
                    updated.NationalIdCard = idCardNumber;
                }
            } else if (ownerType === "Individual") {
                if (!updated.NationalIdCard && idCardNumber) {
                    updated.NationalIdCard = idCardNumber;
                }
            }
            return updated;
        });

        // Auto-prefill verification fields for Step 2
        setDocVerifications((prev) => {
            const updated = { ...prev };
            const mergeDefaults = (
                type: string,
                defs: Record<string, string>,
            ) => {
                const cur = updated[type] || {};
                const merged: Record<string, string> = { ...cur };
                for (const [k, v] of Object.entries(defs)) {
                    if (!merged[k] && v) {
                        merged[k] = v;
                    }
                }
                updated[type] = merged;
            };

            if (ownerType === "Enterprise") {
                mergeDefaults("EnterpriseRegistration", {
                    docNumber: businessLicenseNumber || taxCode,
                    companyName: businessName,
                    representative: fullName,
                    issuedDate: licenseIssuedDate,
                    registeredAddress: permanentAddress,
                });
                mergeDefaults("NationalIdCard", {
                    docNumber: idCardNumber,
                    fullName: fullName,
                    issuedDate: idCardIssuedDate,
                });
                mergeDefaults("PremisesRightProof", {
                    ownerName: businessName || fullName,
                    landAddress: parkingAddress,
                });
                mergeDefaults("AuthorizationLetter", {
                    authorizer: fullName,
                });
            } else if (ownerType === "HouseholdBusiness") {
                mergeDefaults("HouseholdBusinessRegistration", {
                    docNumber: businessLicenseNumber || taxCode,
                    businessName: businessName,
                    ownerName: fullName,
                    issuedDate: licenseIssuedDate,
                    businessAddress: permanentAddress,
                });
                mergeDefaults("NationalIdCard", {
                    docNumber: idCardNumber,
                    fullName: fullName,
                    issuedDate: idCardIssuedDate,
                });
                mergeDefaults("PremisesRightProof", {
                    ownerName: businessName || fullName,
                    landAddress: parkingAddress,
                });
                mergeDefaults("AuthorizationLetter", {
                    authorizer: fullName,
                });
            } else if (ownerType === "Individual") {
                mergeDefaults("NationalIdCard", {
                    docNumber: idCardNumber,
                    fullName: fullName,
                    issuedDate: idCardIssuedDate,
                    permanentAddress: permanentAddress,
                });
                mergeDefaults("PremisesRightProof", {
                    ownerName: fullName,
                    landAddress: parkingAddress,
                });
                mergeDefaults("AuthorizationLetter", {
                    authorizedPerson: fullName,
                });
            }

            return updated;
        });

        setErrorMsg("");
        setStep(2);
        window.scrollTo({ top: 0, behavior: "smooth" });
    }

    // Handle Document Upload
    async function handleFileUpload(
        documentType: string,
        e: React.ChangeEvent<HTMLInputElement>,
    ) {
        const file = e.target.files?.[0];
        if (!file) return;

        setErrorMsg("");
        setSuccessNotice("");

        // Validate size (max 10MB)
        if (file.size > 10 * 1024 * 1024) {
            setErrorMsg("Tệp tải lên không được vượt quá 10MB.");
            return;
        }

        setUploadingDocType(documentType);
        try {
            const currentDocNumber =
                docNumbers[documentType] ||
                docVerifications[documentType]?.docNumber ||
                "";
            const currentVerification = docVerifications[documentType] || {};
            const doc = await uploadOwnerDocument(
                documentType,
                file,
                currentDocNumber,
                currentVerification,
            );
            setUploadedDocuments((prev) => {
                const filtered = prev.filter(
                    (d) => d.documentType !== documentType,
                );
                return [...filtered, doc];
            });
            setSuccessNotice(`Đã tải lên tệp: ${file.name}`);
        } catch (err: unknown) {
            console.warn("Upload document fallback:", err);
            const fallbackDoc = {
                id: "doc-" + Math.random().toString(36).substring(2, 9),
                documentType,
                fileName: file.name,
                contentType: file.type || "application/pdf",
                sizeBytes: file.size,
                uploadedAtUtc: new Date().toISOString(),
                documentNumber: docNumbers[documentType] || undefined,
                verificationData: docVerifications[documentType] || undefined,
            };
            setUploadedDocuments((prev) => {
                const filtered = prev.filter(
                    (d) => d.documentType !== documentType,
                );
                return [...filtered, fallbackDoc];
            });
            setSuccessNotice(`Đã tải lên tệp: ${file.name}`);
        } finally {
            setUploadingDocType(null);
            if (fileInputRefs.current[documentType]) {
                fileInputRefs.current[documentType]!.value = "";
            }
        }
    }

    // Handle Document Delete
    async function handleDeleteFile(documentType: string) {
        setErrorMsg("");
        try {
            await deleteOwnerDocument(documentType);
            setUploadedDocuments((prev) =>
                prev.filter((d) => d.documentType !== documentType),
            );
            setSuccessNotice("Đã xóa tệp tài liệu.");
        } catch (err) {
            setErrorMsg(describeOwnerApplicationError(err));
        }
    }

    // Handle Step 2 Validation & Next
    function handleNextToStep3() {
        setErrorMsg("");
        setSuccessNotice("");
        // Check if all required documents are uploaded
        const requiredTypes = requirements
            .filter((r) => r.required)
            .map((r) => r.documentType);
        const uploadedTypes = new Set(
            uploadedDocuments.map((d) => d.documentType),
        );

        const missing = requiredTypes.filter((t) => !uploadedTypes.has(t));
        if (missing.length > 0) {
            setErrorMsg(
                "Vui lòng tải lên đầy đủ các tài liệu bắt buộc trước khi chuyển sang bước tiếp theo.",
            );
            return;
        }

        setStep(3);
        window.scrollTo({ top: 0, behavior: "smooth" });
    }

    // Handle Step 3 Submission
    async function handleSubmitApplication() {
        setErrorMsg("");
        setSuccessNotice("");
        if (!agreedTerms) {
            setErrorMsg(
                "Bạn cần tích chọn cam kết tính trung thực của hồ sơ pháp lý.",
            );
            return;
        }

        setIsSubmitting(true);
        try {
            if (applicationStatus === "Rejected") {
                await resubmitOwnerApplication({
                    taxCode: taxCode.trim() || undefined,
                    businessName: businessName.trim() || undefined,
                });
            } else if (applicationStatus === "Pending") {
                // Ensure ownerType is saved
                await updateOwnerType(ownerType);
            } else {
                await applyForOwnerApplication({
                    taxCode: taxCode.trim() || undefined,
                    businessName: businessName.trim() || undefined,
                    fullName: fullName.trim() || undefined,
                    phoneNumber: phoneNumber.trim() || undefined,
                    ownerType,
                    idCardNumber: idCardNumber.trim() || undefined,
                    idCardIssuedDate: idCardIssuedDate.trim() || undefined,
                    idCardIssuedPlace: idCardIssuedPlace.trim() || undefined,
                    permanentAddress: permanentAddress.trim() || undefined,
                    parkingAddress: parkingAddress.trim() || undefined,
                    businessLicenseNumber:
                        businessLicenseNumber.trim() || undefined,
                    licenseIssuedDate: licenseIssuedDate.trim() || undefined,
                    licenseIssuedPlace: licenseIssuedPlace.trim() || undefined,
                    representativePosition:
                        representativePosition.trim() || undefined,
                    shortName: shortName.trim() || undefined,
                    contactEmail: contactEmail.trim() || undefined,
                    documentNumbers: docNumbers,
                    documentVerifications: docVerifications,
                });
            }

            // Sync any pending documents from Step 2 to backend now that application is Pending
            const pending = getPendingDocumentFiles();
            for (const [docType, entry] of pending.entries()) {
                try {
                    await uploadOwnerDocument(
                        docType,
                        entry.file,
                        entry.documentNumber,
                        entry.verificationData,
                    );
                } catch (syncErr) {
                    console.warn(`Could not sync doc ${docType} to backend:`, syncErr);
                }
            }
            clearPendingDocumentFiles();

            setApplicationStatus("Pending");
            setRejectionReason(null);
            setSubmittedAt(new Date().toISOString());
            setStep(4);
            window.scrollTo({ top: 0, behavior: "smooth" });
        } catch (err: unknown) {
            setErrorMsg(describeOwnerApplicationError(err));
        } finally {
            setIsSubmitting(false);
        }
    }

    if (loadingInitial) {
        return (
            <div
                className={`pw-onboarding-container ${fontClassName || ""}`}
                style={{
                    display: "flex",
                    alignItems: "center",
                    justifyContent: "center",
                }}
            >
                <div style={{ textAlign: "center", color: "#94a3b8" }}>
                    <div style={{ fontSize: 24, marginBottom: 12 }}>⚡</div>
                    <p>Đang tải thông tin hồ sơ đối tác...</p>
                </div>
            </div>
        );
    }

    return (
        <div className={`pw-onboarding-container ${fontClassName || ""}`}>
            <div className="pw-onboarding-wrapper">
                {/* Header Top Bar: Logo on the left, badge on the right */}
                <div className="pw-onboarding-topbar">
                    <Link
                        href="/"
                        className="pw-onboarding-brand"
                        aria-label="ParkWise Home"
                    >
                        <Image
                            src="/images/parkwise/logo.png"
                            alt="ParkWise Logo"
                            width={707}
                            height={353}
                            priority
                        />
                    </Link>
                    <div className="pw-onboarding-badge">
                        <span>🛡️</span> Xác thực Pháp lý & Đối tác Chủ bãi đỗ xe
                    </div>
                </div>

                {/* Header Branding */}
                <header className="pw-onboarding-header">
                    <h1 className="pw-onboarding-title">
                        Hoàn thiện Hồ sơ Đối tác
                    </h1>
                    <p className="pw-onboarding-subtitle">
                        Cung cấp thông tin pháp lý và giấy tờ bãi xe để kích
                        hoạt tài khoản quản trị và xuất bản bãi đỗ xe trên hệ
                        thống.
                    </p>
                </header>

                {/* Stepper (Only show in steps 1, 2, 3) */}
                {step < 4 && (
                    <div className="pw-stepper">
                        <div
                            className={`pw-step-item ${step >= 1 ? "active" : ""} ${step > 1 ? "completed" : ""}`}
                        >
                            <div className="pw-step-number">
                                {step > 1 ? "✓" : "1"}
                            </div>
                            <div className="pw-step-content">
                                <span className="pw-step-label">Bước 1</span>
                                <span className="pw-step-name">
                                    Hình thức & Thông tin pháp lý
                                </span>
                            </div>
                        </div>

                        <div
                            className={`pw-step-divider ${step > 1 ? "filled" : ""}`}
                        />

                        <div
                            className={`pw-step-item ${step >= 2 ? "active" : ""} ${step > 2 ? "completed" : ""}`}
                        >
                            <div className="pw-step-number">
                                {step > 2 ? "✓" : "2"}
                            </div>
                            <div className="pw-step-content">
                                <span className="pw-step-label">Bước 2</span>
                                <span className="pw-step-name">
                                    Checklist & Tải giấy tờ
                                </span>
                            </div>
                        </div>

                        <div
                            className={`pw-step-divider ${step > 2 ? "filled" : ""}`}
                        />

                        <div
                            className={`pw-step-item ${step >= 3 ? "active" : ""}`}
                        >
                            <div className="pw-step-number">3</div>
                            <div className="pw-step-content">
                                <span className="pw-step-label">Bước 3</span>
                                <span className="pw-step-name">
                                    Ký cam kết & Gửi xét duyệt
                                </span>
                            </div>
                        </div>
                    </div>
                )}

                {/* Alerts */}
                {errorMsg && (
                    <div className="pw-alert pw-alert-error" role="alert">
                        <span>⚠️</span>
                        <span>{errorMsg}</span>
                    </div>
                )}
                {successNotice && (
                    <div className="pw-alert pw-alert-success" role="status">
                        <span>✓</span>
                        <span>{successNotice}</span>
                    </div>
                )}

                {/* STEP 1: ENTITY TYPE & CUSTOMIZED FORM FIELDS */}
                {step === 1 && (
                    <div className="pw-card">
                        <div className="pw-card-header">
                            <h2>1. Chọn hình thức hoạt động</h2>
                            <p>
                                Mỗi hình thức hoạt động sẽ có phiếu điền thông
                                tin và danh mục hồ sơ pháp lý được tùy biến
                                tương ứng.
                            </p>
                        </div>

                        {/* Entity Types Grid */}
                        <div className="pw-type-grid">
                            {/* Individual */}
                            <div
                                className={`pw-type-card ${ownerType === "Individual" ? "selected" : ""}`}
                                onClick={() =>
                                    handleOwnerTypeChange("Individual")
                                }
                            >
                                <div className="pw-type-check">✓</div>
                                <div className="pw-type-icon">
                                    <UserCheckIcon />
                                </div>
                                <div className="pw-type-title">Cá nhân</div>
                                <div className="pw-type-desc">
                                    Dành cho chủ sở hữu cá nhân, đất thuê tự
                                    quản lý bãi đỗ xe gia đình hoặc bãi xe nhỏ
                                    lẻ.
                                </div>
                            </div>

                            {/* Household Business */}
                            <div
                                className={`pw-type-card ${ownerType === "HouseholdBusiness" ? "selected" : ""}`}
                                onClick={() =>
                                    handleOwnerTypeChange("HouseholdBusiness")
                                }
                            >
                                <div className="pw-type-check">✓</div>
                                <div className="pw-type-icon">
                                    <StoreIcon />
                                </div>
                                <div className="pw-type-title">
                                    Hộ kinh doanh
                                </div>
                                <div className="pw-type-desc">
                                    Cơ sở kinh doanh dịch vụ trông giữ xe có
                                    Giấy chứng nhận đăng ký hộ kinh doanh cá
                                    thể.
                                </div>
                            </div>

                            {/* Enterprise */}
                            <div
                                className={`pw-type-card ${ownerType === "Enterprise" ? "selected" : ""}`}
                                onClick={() =>
                                    handleOwnerTypeChange("Enterprise")
                                }
                            >
                                <div className="pw-type-check">✓</div>
                                <div className="pw-type-icon">
                                    <BuildingIcon />
                                </div>
                                <div className="pw-type-title">
                                    Doanh nghiệp / Công ty
                                </div>
                                <div className="pw-type-desc">
                                    Công ty Cổ phần, TNHH sở hữu hoặc vận hành
                                    chuỗi bãi đỗ xe thương mại thông minh.
                                </div>
                            </div>
                        </div>

                        {/* FORM A: CÁ NHÂN (INDIVIDUAL) */}
                        {ownerType === "Individual" && (
                            <div className="pw-entity-form-block">
                                <div className="pw-section-divider">
                                    <span>👤</span> Thông tin chủ sở hữu & Cơ sở
                                    bãi xe cá nhân
                                </div>

                                <div className="pw-form-row">
                                    <div className="pw-form-group">
                                        <label
                                            className="pw-label"
                                            htmlFor="businessName"
                                        >
                                            Tên bãi đỗ xe / Cơ sở trông giữ xe{" "}
                                            <span className="req">*</span>
                                        </label>
                                        <input
                                            id="businessName"
                                            type="text"
                                            className="pw-input"
                                            placeholder="Ví dụ: Bãi giữ xe Thông Minh An Bình"
                                            value={businessName}
                                            onChange={(e) =>
                                                setBusinessName(e.target.value)
                                            }
                                        />
                                        <div className="pw-help-text">
                                            Tên hiển thị của bãi đỗ xe trên ứng
                                            dụng khách hàng.
                                        </div>
                                    </div>

                                    <div className="pw-form-group">
                                        <label
                                            className="pw-label"
                                            htmlFor="fullName"
                                        >
                                            Họ và tên chủ sở hữu bãi xe{" "}
                                            <span className="req">*</span>
                                        </label>
                                        <input
                                            id="fullName"
                                            type="text"
                                            className="pw-input"
                                            placeholder="Ví dụ: Nguyễn Văn An"
                                            value={fullName}
                                            onChange={(e) =>
                                                setFullName(e.target.value)
                                            }
                                        />
                                        <div className="pw-help-text">
                                            Họ tên chính xác theo Căn cước công
                                            dân.
                                        </div>
                                    </div>
                                </div>

                                <div className="pw-form-row">
                                    <div className="pw-form-group">
                                        <label
                                            className="pw-label"
                                            htmlFor="idCardNumber"
                                        >
                                            Số Căn cước công dân (CCCD / 12 số){" "}
                                            <span className="req">*</span>
                                        </label>
                                        <div className="pw-input-with-button">
                                            <input
                                                id="idCardNumber"
                                                type="text"
                                                className="pw-input"
                                                placeholder="Ví dụ: 079201234567"
                                                value={idCardNumber}
                                                onChange={(e) => {
                                                    setIdCardNumber(
                                                        e.target.value,
                                                    );
                                                    if (
                                                        vneidVerified &&
                                                        e.target.value !==
                                                            vneidProfile?.cccd
                                                    ) {
                                                        setVneidVerified(false);
                                                    }
                                                }}
                                            />
                                            <button
                                                type="button"
                                                className="pw-btn-vneid"
                                                onClick={() =>
                                                    setVneidModalOpen(true)
                                                }
                                                title="Xác thực & Điền nhanh qua Căn cước công dân điện tử VNeID"
                                            >
                                                🇻🇳 Điền nhanh qua VNeID
                                            </button>
                                        </div>
                                        {vneidVerified && vneidProfile && (
                                            <div
                                                className="pw-vneid-badge"
                                                data-testid="vneid-verified-badge"
                                            >
                                                <span>✓</span> Đã xác thực VNeID
                                                Mức 2: {vneidProfile.fullName} (
                                                {vneidProfile.cccd})
                                            </div>
                                        )}
                                    </div>

                                    <div className="pw-form-group">
                                        <label
                                            className="pw-label"
                                            htmlFor="idCardIssuedDate"
                                        >
                                            Ngày cấp CCCD
                                        </label>
                                        <input
                                            id="idCardIssuedDate"
                                            type="date"
                                            className="pw-input"
                                            value={idCardIssuedDate}
                                            onChange={(e) =>
                                                setIdCardIssuedDate(
                                                    e.target.value,
                                                )
                                            }
                                        />
                                    </div>
                                </div>

                                <div className="pw-form-row">
                                    <div className="pw-form-group">
                                        <label
                                            className="pw-label"
                                            htmlFor="idCardIssuedPlace"
                                        >
                                            Nơi cấp CCCD
                                        </label>
                                        <input
                                            id="idCardIssuedPlace"
                                            type="text"
                                            className="pw-input"
                                            placeholder="Ví dụ: Cục Cảnh sát QLHC về TTXH"
                                            value={idCardIssuedPlace}
                                            onChange={(e) =>
                                                setIdCardIssuedPlace(
                                                    e.target.value,
                                                )
                                            }
                                        />
                                    </div>

                                    <div className="pw-form-group">
                                        <label
                                            className="pw-label"
                                            htmlFor="phoneNumber"
                                        >
                                            Số điện thoại liên hệ{" "}
                                            <span className="req">*</span>
                                        </label>
                                        <input
                                            id="phoneNumber"
                                            type="tel"
                                            className="pw-input"
                                            placeholder="Ví dụ: 0987654321"
                                            value={phoneNumber}
                                            onChange={(e) =>
                                                setPhoneNumber(e.target.value)
                                            }
                                        />
                                    </div>
                                </div>

                                <div className="pw-form-row">
                                    <div className="pw-form-group">
                                        <label
                                            className="pw-label"
                                            htmlFor="contactEmail"
                                        >
                                            Email liên hệ nhận thông báo
                                        </label>
                                        <input
                                            id="contactEmail"
                                            type="email"
                                            className="pw-input"
                                            placeholder="Ví dụ: chubaixe@gmail.com"
                                            value={contactEmail}
                                            onChange={(e) =>
                                                setContactEmail(e.target.value)
                                            }
                                        />
                                    </div>

                                    <div className="pw-form-group">
                                        <label
                                            className="pw-label"
                                            htmlFor="taxCode"
                                        >
                                            Mã số thuế cá nhân (Nếu có)
                                        </label>
                                        <input
                                            id="taxCode"
                                            type="text"
                                            className="pw-input"
                                            placeholder="Ví dụ: 8012345678 (Tùy chọn)"
                                            value={taxCode}
                                            onChange={(e) =>
                                                setTaxCode(e.target.value)
                                            }
                                        />
                                        <div className="pw-help-text">
                                            Mã số thuế thu nhập cá nhân nếu bạn
                                            đã được cấp.
                                        </div>
                                    </div>
                                </div>

                                <div className="pw-form-group">
                                    <label
                                        className="pw-label"
                                        htmlFor="permanentAddress"
                                    >
                                        Địa chỉ thường trú của chủ bãi (Theo
                                        CCCD)
                                    </label>
                                    <input
                                        id="permanentAddress"
                                        type="text"
                                        className="pw-input"
                                        placeholder="Ví dụ: 123 Đường Nguyễn Huệ, Phường Bến Nghé, Quận 1, TP. Hồ Chí Minh"
                                        value={permanentAddress}
                                        onChange={(e) =>
                                            setPermanentAddress(e.target.value)
                                        }
                                    />
                                </div>

                                <div className="pw-form-group">
                                    <label
                                        className="pw-label"
                                        htmlFor="parkingAddress"
                                    >
                                        Địa chỉ thực tế của bãi đỗ xe{" "}
                                        <span className="req">*</span>
                                    </label>
                                    <input
                                        id="parkingAddress"
                                        type="text"
                                        className="pw-input"
                                        placeholder="Ví dụ: 456 Võ Văn Kiệt, Phường Cô Giang, Quận 1, TP. Hồ Chí Minh"
                                        value={parkingAddress}
                                        onChange={(e) =>
                                            setParkingAddress(e.target.value)
                                        }
                                    />
                                    <div className="pw-help-text">
                                        Địa điểm thực tế đón xe và lắp đặt hệ
                                        thống ParkWise.
                                    </div>
                                </div>
                            </div>
                        )}

                        {/* FORM B: HỘ KINH DOANH (HOUSEHOLD BUSINESS) */}
                        {ownerType === "HouseholdBusiness" && (
                            <div className="pw-entity-form-block">
                                <div className="pw-section-divider">
                                    <span>🏪</span> Thông tin Hộ kinh doanh cá
                                    thể
                                </div>

                                {/* Tax Code with VietQR Lookup */}
                                <div className="pw-form-group">
                                    <label
                                        className="pw-label"
                                        htmlFor="taxCode"
                                    >
                                        Mã số thuế / Mã số ĐKHKD{" "}
                                        <span className="req">*</span>
                                    </label>
                                    <div className="pw-input-with-button">
                                        <input
                                            id="taxCode"
                                            type="text"
                                            className="pw-input"
                                            placeholder="Ví dụ: 0312345678 hoặc 8123456789"
                                            value={taxCode}
                                            onChange={(e) =>
                                                setTaxCode(e.target.value)
                                            }
                                            onKeyDown={(e) => {
                                                if (e.key === "Enter") {
                                                    e.preventDefault();
                                                    handleLookupTaxCode();
                                                }
                                            }}
                                        />
                                        <button
                                            type="button"
                                            className="pw-btn-lookup"
                                            onClick={handleLookupTaxCode}
                                            disabled={taxLookupLoading}
                                        >
                                            {taxLookupLoading
                                                ? "⏳ Đang tra cứu..."
                                                : "🔍 Tra cứu MST"}
                                        </button>
                                    </div>
                                    <div className="pw-help-text">
                                        Tra cứu tự động qua hệ thống VietQR /
                                        CSDL Tổng cục Thuế để xác thực thông tin
                                        hộ kinh doanh.
                                    </div>

                                    {taxLookupError && (
                                        <div
                                            style={{
                                                marginTop: 8,
                                                fontSize: 13,
                                                color: "#dc2626",
                                                fontWeight: 600,
                                            }}
                                        >
                                            ⚠️ {taxLookupError}
                                        </div>
                                    )}

                                    {taxLookupSuccess && (
                                        <div className="pw-tax-result-card">
                                            <div className="pw-tax-result-header">
                                                <span className="pw-tax-result-title">
                                                    ✓ {taxLookupSuccess.name}
                                                </span>
                                                {taxLookupSuccess.status && (
                                                    <span className="pw-tax-badge-active">
                                                        {
                                                            taxLookupSuccess.status
                                                        }
                                                    </span>
                                                )}
                                            </div>
                                            <div className="pw-tax-result-address">
                                                <strong>Địa chỉ ĐKKD:</strong>{" "}
                                                {taxLookupSuccess.address}
                                            </div>
                                            <div className="pw-tax-autofilled-hint">
                                                ✓ Đã tự động điền Tên hộ kinh
                                                doanh và Địa chỉ đăng ký bên
                                                dưới.
                                            </div>
                                        </div>
                                    )}
                                </div>

                                <div className="pw-form-row">
                                    <div className="pw-form-group">
                                        <label
                                            className="pw-label"
                                            htmlFor="businessName"
                                        >
                                            Tên Hộ kinh doanh (Theo ĐKHKD){" "}
                                            <span className="req">*</span>
                                        </label>
                                        <input
                                            id="businessName"
                                            type="text"
                                            className="pw-input"
                                            placeholder="Ví dụ: Hộ kinh doanh Bãi đỗ xe An Phú"
                                            value={businessName}
                                            onChange={(e) =>
                                                setBusinessName(e.target.value)
                                            }
                                        />
                                    </div>

                                    <div className="pw-form-group">
                                        <label
                                            className="pw-label"
                                            htmlFor="businessLicenseNumber"
                                        >
                                            Số Giấy chứng nhận ĐKHKD{" "}
                                            <span className="req">*</span>
                                        </label>
                                        <input
                                            id="businessLicenseNumber"
                                            type="text"
                                            className="pw-input"
                                            placeholder="Ví dụ: 41A8034567"
                                            value={businessLicenseNumber}
                                            onChange={(e) =>
                                                setBusinessLicenseNumber(
                                                    e.target.value,
                                                )
                                            }
                                        />
                                    </div>
                                </div>

                                <div className="pw-form-row">
                                    <div className="pw-form-group">
                                        <label
                                            className="pw-label"
                                            htmlFor="licenseIssuedPlace"
                                        >
                                            Cơ quan cấp ĐKKD
                                        </label>
                                        <input
                                            id="licenseIssuedPlace"
                                            type="text"
                                            className="pw-input"
                                            placeholder="Ví dụ: UBND Quận Bình Thạnh, TP. Hồ Chí Minh"
                                            value={licenseIssuedPlace}
                                            onChange={(e) =>
                                                setLicenseIssuedPlace(
                                                    e.target.value,
                                                )
                                            }
                                        />
                                    </div>

                                    <div className="pw-form-group">
                                        <label
                                            className="pw-label"
                                            htmlFor="licenseIssuedDate"
                                        >
                                            Ngày cấp Giấy phép ĐKHKD
                                        </label>
                                        <input
                                            id="licenseIssuedDate"
                                            type="date"
                                            className="pw-input"
                                            value={licenseIssuedDate}
                                            onChange={(e) =>
                                                setLicenseIssuedDate(
                                                    e.target.value,
                                                )
                                            }
                                        />
                                    </div>
                                </div>

                                <div className="pw-form-row">
                                    <div className="pw-form-group">
                                        <label
                                            className="pw-label"
                                            htmlFor="fullName"
                                        >
                                            Họ và tên người đại diện / Chủ hộ{" "}
                                            <span className="req">*</span>
                                        </label>
                                        <input
                                            id="fullName"
                                            type="text"
                                            className="pw-input"
                                            placeholder="Ví dụ: Trần Thị Mai"
                                            value={fullName}
                                            onChange={(e) =>
                                                setFullName(e.target.value)
                                            }
                                        />
                                    </div>

                                    <div className="pw-form-group">
                                        <label
                                            className="pw-label"
                                            htmlFor="idCardNumber"
                                        >
                                            Số CCCD người đại diện (12 số){" "}
                                            <span className="req">*</span>
                                        </label>
                                        <div className="pw-input-with-button">
                                            <input
                                                id="idCardNumber"
                                                type="text"
                                                className="pw-input"
                                                placeholder="Ví dụ: 001199123456"
                                                value={idCardNumber}
                                                onChange={(e) => {
                                                    setIdCardNumber(
                                                        e.target.value,
                                                    );
                                                    if (
                                                        vneidVerified &&
                                                        e.target.value !==
                                                            vneidProfile?.cccd
                                                    ) {
                                                        setVneidVerified(false);
                                                    }
                                                }}
                                            />
                                            <button
                                                type="button"
                                                className="pw-btn-vneid"
                                                onClick={() =>
                                                    setVneidModalOpen(true)
                                                }
                                                title="Xác thực CCCD người đại diện qua VNeID"
                                            >
                                                🇻🇳 Điền nhanh qua VNeID
                                            </button>
                                        </div>
                                        {vneidVerified && vneidProfile && (
                                            <div
                                                className="pw-vneid-badge"
                                                data-testid="vneid-verified-badge-household"
                                            >
                                                <span>✓</span> Đã xác thực VNeID
                                                Mức 2: {vneidProfile.fullName} (
                                                {vneidProfile.cccd})
                                            </div>
                                        )}
                                    </div>
                                </div>

                                <div className="pw-form-row">
                                    <div className="pw-form-group">
                                        <label
                                            className="pw-label"
                                            htmlFor="phoneNumber"
                                        >
                                            Số điện thoại liên hệ{" "}
                                            <span className="req">*</span>
                                        </label>
                                        <input
                                            id="phoneNumber"
                                            type="tel"
                                            className="pw-input"
                                            placeholder="Ví dụ: 0912345678"
                                            value={phoneNumber}
                                            onChange={(e) =>
                                                setPhoneNumber(e.target.value)
                                            }
                                        />
                                    </div>

                                    <div className="pw-form-group">
                                        <label
                                            className="pw-label"
                                            htmlFor="contactEmail"
                                        >
                                            Email liên hệ hộ kinh doanh
                                        </label>
                                        <input
                                            id="contactEmail"
                                            type="email"
                                            className="pw-input"
                                            placeholder="Ví dụ: hokinhdoanxedan@gmail.com"
                                            value={contactEmail}
                                            onChange={(e) =>
                                                setContactEmail(e.target.value)
                                            }
                                        />
                                    </div>
                                </div>

                                <div className="pw-form-group">
                                    <label
                                        className="pw-label"
                                        htmlFor="permanentAddress"
                                    >
                                        Địa chỉ đăng ký hộ kinh doanh (Theo
                                        ĐKKD) <span className="req">*</span>
                                    </label>
                                    <input
                                        id="permanentAddress"
                                        type="text"
                                        className="pw-input"
                                        placeholder="Ví dụ: 78 Đinh Bộ Lĩnh, Phường 26, Quận Bình Thạnh, TP. Hồ Chí Minh"
                                        value={permanentAddress}
                                        onChange={(e) =>
                                            setPermanentAddress(e.target.value)
                                        }
                                    />
                                </div>

                                <div className="pw-form-group">
                                    <label
                                        className="pw-label"
                                        htmlFor="parkingAddress"
                                    >
                                        Địa chỉ thực tế bãi đỗ xe khai thác{" "}
                                        <span className="req">*</span>
                                    </label>
                                    <input
                                        id="parkingAddress"
                                        type="text"
                                        className="pw-input"
                                        placeholder="Ví dụ: 78 Đinh Bộ Lĩnh, Phường 26, Quận Bình Thạnh, TP. Hồ Chí Minh"
                                        value={parkingAddress}
                                        onChange={(e) =>
                                            setParkingAddress(e.target.value)
                                        }
                                    />
                                </div>
                            </div>
                        )}

                        {/* FORM C: DOANH NGHIỆP / CÔNG TY (ENTERPRISE) */}
                        {ownerType === "Enterprise" && (
                            <div className="pw-entity-form-block">
                                <div className="pw-section-divider">
                                    <span>🏢</span> Thông tin Doanh nghiệp /
                                    Pháp nhân
                                </div>

                                {/* Tax Code with VietQR Lookup */}
                                <div className="pw-form-group">
                                    <label
                                        className="pw-label"
                                        htmlFor="taxCode"
                                    >
                                        Mã số thuế Doanh nghiệp (MST){" "}
                                        <span className="req">*</span>
                                    </label>
                                    <div className="pw-input-with-button">
                                        <input
                                            id="taxCode"
                                            type="text"
                                            className="pw-input"
                                            placeholder="Nhập 10 số (Công ty) hoặc 13 số (Chi nhánh), ví dụ: 0100109106"
                                            value={taxCode}
                                            onChange={(e) =>
                                                setTaxCode(e.target.value)
                                            }
                                            onKeyDown={(e) => {
                                                if (e.key === "Enter") {
                                                    e.preventDefault();
                                                    handleLookupTaxCode();
                                                }
                                            }}
                                        />
                                        <button
                                            type="button"
                                            className="pw-btn-lookup"
                                            onClick={handleLookupTaxCode}
                                            disabled={taxLookupLoading}
                                        >
                                            {taxLookupLoading
                                                ? "⏳ Đang kết nối..."
                                                : "🔍 Tra cứu Doanh nghiệp"}
                                        </button>
                                    </div>
                                    <div className="pw-help-text">
                                        Hệ thống sẽ kết nối trực tiếp CSDL Doanh
                                        nghiệp Cục Thuế để tự động điền Tên công
                                        ty & Địa chỉ trụ sở.
                                    </div>

                                    {taxLookupError && (
                                        <div
                                            style={{
                                                marginTop: 8,
                                                fontSize: 13,
                                                color: "#dc2626",
                                                fontWeight: 600,
                                            }}
                                        >
                                            ⚠️ {taxLookupError}
                                        </div>
                                    )}

                                    {taxLookupSuccess && (
                                        <div className="pw-tax-result-card">
                                            <div className="pw-tax-result-header">
                                                <span className="pw-tax-result-title">
                                                    ✓ {taxLookupSuccess.name}
                                                </span>
                                                {taxLookupSuccess.status && (
                                                    <span className="pw-tax-badge-active">
                                                        {
                                                            taxLookupSuccess.status
                                                        }
                                                    </span>
                                                )}
                                            </div>
                                            <div className="pw-tax-result-address">
                                                <strong>
                                                    Địa chỉ trụ sở chính:
                                                </strong>{" "}
                                                {taxLookupSuccess.address}
                                            </div>
                                            <div className="pw-tax-autofilled-hint">
                                                ✓ Đã tự động cập nhật Tên doanh
                                                nghiệp và Địa chỉ trụ sở theo dữ
                                                liệu Cục Thuế.
                                            </div>
                                        </div>
                                    )}
                                </div>

                                <div className="pw-form-row">
                                    <div className="pw-form-group">
                                        <label
                                            className="pw-label"
                                            htmlFor="businessName"
                                        >
                                            Tên Doanh nghiệp (Theo GPĐKKD){" "}
                                            <span className="req">*</span>
                                        </label>
                                        <input
                                            id="businessName"
                                            type="text"
                                            className="pw-input"
                                            placeholder="Ví dụ: Công ty Cổ phần Vận tải & Bãi xe Sài Gòn"
                                            value={businessName}
                                            onChange={(e) =>
                                                setBusinessName(e.target.value)
                                            }
                                        />
                                    </div>

                                    <div className="pw-form-group">
                                        <label
                                            className="pw-label"
                                            htmlFor="shortName"
                                        >
                                            Tên viết tắt / Tên giao dịch quốc tế
                                        </label>
                                        <input
                                            id="shortName"
                                            type="text"
                                            className="pw-input"
                                            placeholder="Ví dụ: SAIGON PARKING CORP"
                                            value={shortName}
                                            onChange={(e) =>
                                                setShortName(e.target.value)
                                            }
                                        />
                                    </div>
                                </div>

                                <div className="pw-form-row">
                                    <div className="pw-form-group">
                                        <label
                                            className="pw-label"
                                            htmlFor="businessLicenseNumber"
                                        >
                                            Số Giấy phép ĐKKD / Mã số doanh
                                            nghiệp
                                        </label>
                                        <input
                                            id="businessLicenseNumber"
                                            type="text"
                                            className="pw-input"
                                            placeholder="Thường trùng với Mã số thuế"
                                            value={businessLicenseNumber}
                                            onChange={(e) =>
                                                setBusinessLicenseNumber(
                                                    e.target.value,
                                                )
                                            }
                                        />
                                    </div>

                                    <div className="pw-form-group">
                                        <label
                                            className="pw-label"
                                            htmlFor="licenseIssuedPlace"
                                        >
                                            Cơ quan cấp GPĐKKD
                                        </label>
                                        <input
                                            id="licenseIssuedPlace"
                                            type="text"
                                            className="pw-input"
                                            placeholder="Ví dụ: Sở Kế hoạch và Đầu tư TP. Hồ Chí Minh"
                                            value={licenseIssuedPlace}
                                            onChange={(e) =>
                                                setLicenseIssuedPlace(
                                                    e.target.value,
                                                )
                                            }
                                        />
                                    </div>
                                </div>

                                <div className="pw-section-divider">
                                    <span>👔</span> Người đại diện theo pháp
                                    luật
                                </div>

                                <div className="pw-form-row">
                                    <div className="pw-form-group">
                                        <label
                                            className="pw-label"
                                            htmlFor="fullName"
                                        >
                                            Họ và tên Người đại diện pháp luật{" "}
                                            <span className="req">*</span>
                                        </label>
                                        <input
                                            id="fullName"
                                            type="text"
                                            className="pw-input"
                                            placeholder="Ví dụ: Lê Minh Trí"
                                            value={fullName}
                                            onChange={(e) =>
                                                setFullName(e.target.value)
                                            }
                                        />
                                    </div>

                                    <div className="pw-form-group">
                                        <label
                                            className="pw-label"
                                            htmlFor="representativePosition"
                                        >
                                            Chức vụ người đại diện{" "}
                                            <span className="req">*</span>
                                        </label>
                                        <input
                                            id="representativePosition"
                                            type="text"
                                            className="pw-input"
                                            placeholder="Ví dụ: Tổng Giám đốc / Giám đốc / Chủ tịch HĐQT"
                                            value={representativePosition}
                                            onChange={(e) =>
                                                setRepresentativePosition(
                                                    e.target.value,
                                                )
                                            }
                                        />
                                    </div>
                                </div>

                                <div className="pw-form-row">
                                    <div className="pw-form-group">
                                        <label
                                            className="pw-label"
                                            htmlFor="idCardNumber"
                                        >
                                            Số CCCD / Hộ chiếu người đại diện
                                            pháp luật{" "}
                                            <span className="req">*</span>
                                        </label>
                                        <div className="pw-input-with-button">
                                            <input
                                                id="idCardNumber"
                                                type="text"
                                                className="pw-input"
                                                placeholder="Ví dụ: 038085123456"
                                                value={idCardNumber}
                                                onChange={(e) => {
                                                    setIdCardNumber(
                                                        e.target.value,
                                                    );
                                                    if (
                                                        vneidVerified &&
                                                        e.target.value !==
                                                            vneidProfile?.cccd
                                                    ) {
                                                        setVneidVerified(false);
                                                    }
                                                }}
                                            />
                                            <button
                                                type="button"
                                                className="pw-btn-vneid"
                                                onClick={() =>
                                                    setVneidModalOpen(true)
                                                }
                                                title="Xác thực CCCD người đại diện pháp luật qua VNeID"
                                            >
                                                🇻🇳 Điền nhanh qua VNeID
                                            </button>
                                        </div>
                                        {vneidVerified && vneidProfile && (
                                            <div
                                                className="pw-vneid-badge"
                                                data-testid="vneid-verified-badge-enterprise"
                                            >
                                                <span>✓</span> Đã xác thực VNeID
                                                Mức 2: {vneidProfile.fullName} (
                                                {vneidProfile.cccd})
                                            </div>
                                        )}
                                    </div>

                                    <div className="pw-form-group">
                                        <label
                                            className="pw-label"
                                            htmlFor="phoneNumber"
                                        >
                                            Số điện thoại liên hệ / Hotline công
                                            ty <span className="req">*</span>
                                        </label>
                                        <input
                                            id="phoneNumber"
                                            type="tel"
                                            className="pw-input"
                                            placeholder="Ví dụ: 02838123456 hoặc 0909123456"
                                            value={phoneNumber}
                                            onChange={(e) =>
                                                setPhoneNumber(e.target.value)
                                            }
                                        />
                                    </div>
                                </div>

                                <div className="pw-form-group">
                                    <label
                                        className="pw-label"
                                        htmlFor="contactEmail"
                                    >
                                        Email doanh nghiệp (Nhận kết quả duyệt &
                                        hợp đồng) <span className="req">*</span>
                                    </label>
                                    <input
                                        id="contactEmail"
                                        type="email"
                                        className="pw-input"
                                        placeholder="Ví dụ: contact@saigonparking.vn"
                                        value={contactEmail}
                                        onChange={(e) =>
                                            setContactEmail(e.target.value)
                                        }
                                    />
                                    <div className="pw-help-text">
                                        Thông báo phê duyệt hồ sơ và kích hoạt
                                        tài khoản sẽ được gửi đến email này.
                                    </div>
                                </div>

                                <div className="pw-form-group">
                                    <label
                                        className="pw-label"
                                        htmlFor="permanentAddress"
                                    >
                                        Địa chỉ trụ sở chính (Theo GPĐKKD){" "}
                                        <span className="req">*</span>
                                    </label>
                                    <input
                                        id="permanentAddress"
                                        type="text"
                                        className="pw-input"
                                        placeholder="Ví dụ: Tòa nhà Bitexco, Số 2 Hải Triều, Phường Bến Nghé, Quận 1, TP. Hồ Chí Minh"
                                        value={permanentAddress}
                                        onChange={(e) =>
                                            setPermanentAddress(e.target.value)
                                        }
                                    />
                                </div>

                                <div className="pw-form-group">
                                    <label
                                        className="pw-label"
                                        htmlFor="parkingAddress"
                                    >
                                        Địa chỉ địa điểm kinh doanh / bãi đỗ xe{" "}
                                        <span className="req">*</span>
                                    </label>
                                    <input
                                        id="parkingAddress"
                                        type="text"
                                        className="pw-input"
                                        placeholder="Ví dụ: Tầng hầm B1-B2 Tòa nhà Landmark, 208 Nguyễn Hữu Cảnh, Phường 22, Bình Thạnh"
                                        value={parkingAddress}
                                        onChange={(e) =>
                                            setParkingAddress(e.target.value)
                                        }
                                    />
                                </div>
                            </div>
                        )}

                        {/* Footer Actions: Đã xóa nút xem trước Dashboard theo yêu cầu */}
                        <div className="pw-actions">
                            <Link href="/" className="pw-btn-secondary">
                                ← Về trang chủ
                            </Link>
                            <button
                                type="button"
                                className="pw-btn-primary"
                                onClick={handleNextToStep2}
                            >
                                Tiếp tục: Tải giấy tờ chứng thực →
                            </button>
                        </div>
                    </div>
                )}

                {/* STEP 2: DOCUMENTS CHECKLIST WITH DEDICATED INPUT FIELDS */}
                {step === 2 && (
                    <div className="pw-card">
                        <div className="pw-card-header">
                            <h2>2. Tải lên giấy tờ chứng minh</h2>
                            <p>
                                Danh mục hồ sơ tương ứng theo hình thức{" "}
                                <strong>
                                    {ownerType === "Individual"
                                        ? "Cá nhân"
                                        : ownerType === "HouseholdBusiness"
                                          ? "Hộ kinh doanh"
                                          : "Doanh nghiệp / Công ty"}
                                </strong>
                                . Với mỗi mục hồ sơ, vui lòng nhập{" "}
                                <strong>thông tin cơ bản để đối chiếu</strong> ở
                                phiếu điền tương ứng và tải lên hình ảnh đính
                                kèm (PDF, PNG, JPG tối đa 10MB) để xác thực tính
                                trùng khớp.
                            </p>
                        </div>

                        {/* Dynamic Checklist Rendered From Requirements with Input Fields */}
                        {requirements.map((req) => {
                            const uploadedDoc = uploadedDocuments.find(
                                (d) => d.documentType === req.documentType,
                            );
                            const isUploadingThis =
                                uploadingDocType === req.documentType;
                            const currentDocNum =
                                docNumbers[req.documentType] ||
                                docVerifications[req.documentType]?.docNumber ||
                                "";

                            const fallbackItem = FALLBACK_REQUIREMENTS[
                                ownerType
                            ]?.find((f) => f.documentType === req.documentType);
                            const verificationFields: DocumentVerificationField[] =
                                req.verificationFields &&
                                req.verificationFields.length > 0
                                    ? req.verificationFields
                                    : fallbackItem?.verificationFields &&
                                        fallbackItem.verificationFields.length >
                                            0
                                      ? fallbackItem.verificationFields
                                      : [
                                            {
                                                key: "docNumber",
                                                label:
                                                    req.inputLabel ||
                                                    "Số hiệu / Mã giấy tờ",
                                                placeholder:
                                                    req.inputPlaceholder ||
                                                    "Nhập số hiệu giấy tờ / số quyết định...",
                                                required: req.required,
                                                fullWidth: true,
                                            },
                                        ];

                            return (
                                <div
                                    key={req.documentType}
                                    className="pw-doc-item"
                                >
                                    <div className="pw-doc-item-header">
                                        <div className="pw-doc-info">
                                            <div className="pw-doc-icon">
                                                <FileTextIcon />
                                            </div>
                                            <div>
                                                <h3 className="pw-doc-name">
                                                    {req.name}
                                                </h3>
                                                {req.inputHelp && (
                                                    <div className="pw-doc-input-help">
                                                        {req.inputHelp}
                                                    </div>
                                                )}
                                            </div>
                                        </div>
                                        {req.required ? (
                                            <span className="pw-badge-req">
                                                Bắt buộc
                                            </span>
                                        ) : (
                                            <span className="pw-badge-opt">
                                                Tùy chọn
                                            </span>
                                        )}
                                    </div>

                                    {/* Dedicated Verification Fields Box */}
                                    <div className="pw-doc-verify-card">
                                        <div className="pw-doc-verify-header">
                                            <span className="pw-doc-verify-badge">
                                                🔍 Thông tin cơ bản đối chiếu
                                            </span>
                                            <span className="pw-doc-verify-hint">
                                                Nhập thông tin cơ bản để hệ
                                                thống đối chiếu tính trùng khớp
                                                với ảnh kèm bên dưới
                                            </span>
                                        </div>

                                        <div className="pw-doc-fields-grid">
                                            {verificationFields.map((field) => {
                                                const val =
                                                    field.key === "docNumber"
                                                        ? (docNumbers[
                                                              req.documentType
                                                          ] ??
                                                          docVerifications[
                                                              req.documentType
                                                          ]?.docNumber ??
                                                          "")
                                                        : (docVerifications[
                                                              req.documentType
                                                          ]?.[field.key] ?? "");

                                                return (
                                                    <div
                                                        key={field.key}
                                                        className={`pw-doc-field-item ${field.fullWidth ? "full-width" : ""}`}
                                                    >
                                                        <label
                                                            className="pw-doc-field-label"
                                                            htmlFor={`verify-${req.documentType}-${field.key}`}
                                                        >
                                                            {field.label}
                                                            {field.required && (
                                                                <span
                                                                    style={{
                                                                        color: "#ef4444",
                                                                    }}
                                                                >
                                                                    *
                                                                </span>
                                                            )}
                                                        </label>
                                                        <input
                                                            id={`verify-${req.documentType}-${field.key}`}
                                                            type={
                                                                field.type ||
                                                                "text"
                                                            }
                                                            className="pw-doc-field-input"
                                                            placeholder={
                                                                field.placeholder
                                                            }
                                                            value={val}
                                                            onChange={(e) => {
                                                                const newVal =
                                                                    e.target
                                                                        .value;
                                                                if (
                                                                    field.key ===
                                                                    "docNumber"
                                                                ) {
                                                                    setDocNumbers(
                                                                        (
                                                                            prev,
                                                                        ) => ({
                                                                            ...prev,
                                                                            [req.documentType]:
                                                                                newVal,
                                                                        }),
                                                                    );
                                                                }
                                                                setDocVerifications(
                                                                    (prev) => ({
                                                                        ...prev,
                                                                        [req.documentType]:
                                                                            {
                                                                                ...(prev[
                                                                                    req
                                                                                        .documentType
                                                                                ] ||
                                                                                    {}),
                                                                                [field.key]:
                                                                                    newVal,
                                                                                ...(field.key ===
                                                                                "docNumber"
                                                                                    ? {
                                                                                          docNumber:
                                                                                              newVal,
                                                                                      }
                                                                                    : {}),
                                                                            },
                                                                    }),
                                                                );
                                                            }}
                                                        />
                                                    </div>
                                                );
                                            })}
                                        </div>
                                    </div>

                                    {/* Upload Dropzone / Uploaded File Pill */}
                                    {uploadedDoc ? (
                                        <div className="pw-uploaded-pill">
                                            <div className="pw-pill-info">
                                                <div className="pw-pill-check">
                                                    ✓
                                                </div>
                                                <div className="pw-pill-details">
                                                    <span className="pw-pill-filename">
                                                        {uploadedDoc.fileName}
                                                    </span>
                                                    <span className="pw-pill-meta">
                                                        {(
                                                            uploadedDoc.sizeBytes /
                                                            1024
                                                        ).toFixed(0)}{" "}
                                                        KB
                                                        {currentDocNum
                                                            ? ` • Số hiệu: ${currentDocNum}`
                                                            : ""}
                                                        {" • Đã tải lên"}
                                                    </span>
                                                </div>
                                            </div>
                                            <div className="pw-pill-actions">
                                                <button
                                                    type="button"
                                                    className="pw-btn-delete"
                                                    onClick={() =>
                                                        handleDeleteFile(
                                                            req.documentType,
                                                        )
                                                    }
                                                >
                                                    Xóa & Tải lại
                                                </button>
                                            </div>
                                        </div>
                                    ) : (
                                        <div
                                            className="pw-doc-dropzone"
                                            onClick={() =>
                                                fileInputRefs.current[
                                                    req.documentType
                                                ]?.click()
                                            }
                                        >
                                            <input
                                                type="file"
                                                accept="application/pdf,image/png,image/jpeg"
                                                style={{ display: "none" }}
                                                ref={(el) => {
                                                    fileInputRefs.current[
                                                        req.documentType
                                                    ] = el;
                                                }}
                                                onChange={(e) =>
                                                    handleFileUpload(
                                                        req.documentType,
                                                        e,
                                                    )
                                                }
                                            />
                                            <div className="pw-dropzone-inner">
                                                <div className="pw-dropzone-icon">
                                                    {isUploadingThis ? (
                                                        "⏳"
                                                    ) : (
                                                        <UploadIcon />
                                                    )}
                                                </div>
                                                <div className="pw-dropzone-title">
                                                    {isUploadingThis ? (
                                                        "Đang tải tệp lên máy chủ..."
                                                    ) : (
                                                        <>
                                                            Kéo thả tệp vào đây
                                                            hoặc{" "}
                                                            <span>
                                                                chọn từ thiết bị
                                                            </span>
                                                        </>
                                                    )}
                                                </div>
                                                <div className="pw-dropzone-hint">
                                                    Tải lên hình ảnh hoặc bản
                                                    scan để đối chiếu với thông
                                                    tin nhập ở trên (PDF, PNG,
                                                    JPG tối đa 10MB)
                                                </div>
                                            </div>
                                        </div>
                                    )}
                                </div>
                            );
                        })}

                        {/* Footer Actions */}
                        <div className="pw-actions">
                            <button
                                type="button"
                                className="pw-btn-secondary"
                                onClick={() => setStep(1)}
                            >
                                ← Quay lại bước 1
                            </button>
                            <button
                                type="button"
                                className="pw-btn-primary"
                                onClick={handleNextToStep3}
                            >
                                Tiếp tục: Xác nhận & Cam kết →
                            </button>
                        </div>
                    </div>
                )}

                {/* STEP 3: REVIEW & SUBMIT */}
                {step === 3 && (
                    <div className="pw-card">
                        <div className="pw-card-header">
                            <h2>3. Kiểm tra thông tin & Gửi xét duyệt</h2>
                            <p>
                                Vui lòng rà soát lại thông tin trước khi nộp hồ
                                sơ lên Hội đồng thẩm định ParkWise.
                            </p>
                        </div>

                        {/* Review Summary Grid */}
                        <div className="pw-review-box">
                            <div className="pw-review-title">
                                <span>📋</span> Tóm tắt thông tin pháp lý (
                                {ownerType === "Individual"
                                    ? "Cá nhân"
                                    : ownerType === "HouseholdBusiness"
                                      ? "Hộ kinh doanh"
                                      : "Doanh nghiệp / Công ty"}
                                )
                            </div>
                            <div className="pw-review-grid">
                                <div className="pw-review-row">
                                    <span className="pw-review-label">
                                        Hình thức hoạt động
                                    </span>
                                    <span className="pw-review-val">
                                        {ownerType === "Individual"
                                            ? "Cá nhân tự quản lý"
                                            : ownerType === "HouseholdBusiness"
                                              ? "Hộ kinh doanh cá thể"
                                              : "Doanh nghiệp / Công ty"}
                                    </span>
                                </div>
                                <div className="pw-review-row">
                                    <span className="pw-review-label">
                                        {ownerType === "Individual"
                                            ? "Tên bãi đỗ xe"
                                            : "Tên Doanh nghiệp / Hộ KD"}
                                    </span>
                                    <span className="pw-review-val">
                                        {businessName}
                                    </span>
                                </div>
                                {ownerType !== "Individual" && (
                                    <div className="pw-review-row">
                                        <span className="pw-review-label">
                                            Mã số thuế (MST)
                                        </span>
                                        <span className="pw-review-val">
                                            {taxCode}
                                        </span>
                                    </div>
                                )}
                                <div className="pw-review-row">
                                    <span className="pw-review-label">
                                        {ownerType === "Individual"
                                            ? "Chủ sở hữu"
                                            : ownerType === "HouseholdBusiness"
                                              ? "Chủ hộ kinh doanh"
                                              : "Người đại diện pháp luật"}
                                    </span>
                                    <span className="pw-review-val">
                                        {fullName}{" "}
                                        {representativePosition
                                            ? `(${representativePosition})`
                                            : ""}
                                    </span>
                                </div>
                                {idCardNumber && (
                                    <div className="pw-review-row">
                                        <span className="pw-review-label">
                                            Số CCCD / Hộ chiếu
                                        </span>
                                        <span className="pw-review-val">
                                            {idCardNumber}
                                        </span>
                                    </div>
                                )}
                                <div className="pw-review-row">
                                    <span className="pw-review-label">
                                        Số điện thoại liên hệ
                                    </span>
                                    <span className="pw-review-val">
                                        {phoneNumber || "Mặc định tài khoản"}
                                    </span>
                                </div>
                                {contactEmail && (
                                    <div className="pw-review-row">
                                        <span className="pw-review-label">
                                            Email nhận kết quả
                                        </span>
                                        <span className="pw-review-val">
                                            {contactEmail}
                                        </span>
                                    </div>
                                )}
                                {permanentAddress && (
                                    <div className="pw-review-row">
                                        <span className="pw-review-label">
                                            {ownerType === "Individual"
                                                ? "Địa chỉ thường trú"
                                                : "Địa chỉ trụ sở chính"}
                                        </span>
                                        <span className="pw-review-val">
                                            {permanentAddress}
                                        </span>
                                    </div>
                                )}
                                {parkingAddress && (
                                    <div className="pw-review-row">
                                        <span className="pw-review-label">
                                            Địa chỉ bãi đỗ xe
                                        </span>
                                        <span className="pw-review-val">
                                            {parkingAddress}
                                        </span>
                                    </div>
                                )}
                            </div>
                        </div>

                        {/* Uploaded Documents List */}
                        <div className="pw-review-box">
                            <div className="pw-review-title">
                                <span>📁</span> Giấy tờ pháp lý & Thông tin đối
                                chiếu đính kèm ({uploadedDocuments.length})
                            </div>
                            <div
                                style={{
                                    display: "flex",
                                    flexDirection: "column",
                                    gap: 12,
                                }}
                            >
                                {uploadedDocuments.map((doc) => {
                                    const req = requirements.find(
                                        (r) =>
                                            r.documentType === doc.documentType,
                                    );
                                    const docNum =
                                        docNumbers[doc.documentType] ||
                                        docVerifications[doc.documentType]
                                            ?.docNumber;
                                    const verifObj =
                                        docVerifications[doc.documentType] ||
                                        {};

                                    return (
                                        <div
                                            key={doc.id}
                                            style={{
                                                padding: "14px 18px",
                                                background: "#ffffff",
                                                borderRadius: 14,
                                                border: "1px solid #e2e8f0",
                                                boxShadow:
                                                    "0 1px 3px rgba(0, 0, 0, 0.03)",
                                                display: "flex",
                                                flexDirection: "column",
                                                gap: 10,
                                            }}
                                        >
                                            <div
                                                style={{
                                                    display: "flex",
                                                    alignItems: "center",
                                                    justifyContent:
                                                        "space-between",
                                                    flexWrap: "wrap",
                                                    gap: 8,
                                                }}
                                            >
                                                <div
                                                    style={{
                                                        display: "flex",
                                                        alignItems: "center",
                                                        gap: 10,
                                                    }}
                                                >
                                                    <span
                                                        style={{
                                                            color: "#16a34a",
                                                            fontWeight: "bold",
                                                        }}
                                                    >
                                                        ✓
                                                    </span>
                                                    <span
                                                        style={{
                                                            fontWeight: 700,
                                                            fontSize: 13.5,
                                                            color: "#0f172a",
                                                        }}
                                                    >
                                                        {req?.name ||
                                                            doc.documentType}
                                                    </span>
                                                </div>
                                                <span
                                                    style={{
                                                        fontSize: 12,
                                                        color: "#64748b",
                                                    }}
                                                >
                                                    📎 {doc.fileName} (
                                                    {(
                                                        doc.sizeBytes / 1024
                                                    ).toFixed(0)}{" "}
                                                    KB)
                                                </span>
                                            </div>

                                            {/* Thông tin đối chiếu cơ bản */}
                                            <div
                                                style={{
                                                    display: "flex",
                                                    flexWrap: "wrap",
                                                    gap: 10,
                                                    padding: "8px 12px",
                                                    background: "#f8fafc",
                                                    borderRadius: 8,
                                                    fontSize: 12,
                                                    color: "#334155",
                                                }}
                                            >
                                                {docNum && (
                                                    <span
                                                        style={{
                                                            color: "#09090b",
                                                            fontWeight: 700,
                                                        }}
                                                    >
                                                        Số hiệu: {docNum}
                                                    </span>
                                                )}
                                                {verifObj.fullName && (
                                                    <span>
                                                        • Họ tên:{" "}
                                                        <strong>
                                                            {verifObj.fullName}
                                                        </strong>
                                                    </span>
                                                )}
                                                {verifObj.ownerName && (
                                                    <span>
                                                        • Đứng tên/Chủ sở hữu:{" "}
                                                        <strong>
                                                            {verifObj.ownerName}
                                                        </strong>
                                                    </span>
                                                )}
                                                {verifObj.companyName && (
                                                    <span>
                                                        • Tên DN:{" "}
                                                        <strong>
                                                            {
                                                                verifObj.companyName
                                                            }
                                                        </strong>
                                                    </span>
                                                )}
                                                {verifObj.businessName && (
                                                    <span>
                                                        • Tên HKD:{" "}
                                                        <strong>
                                                            {
                                                                verifObj.businessName
                                                            }
                                                        </strong>
                                                    </span>
                                                )}
                                                {verifObj.landPlot && (
                                                    <span>
                                                        • Thửa đất:{" "}
                                                        <strong>
                                                            {verifObj.landPlot}
                                                        </strong>
                                                    </span>
                                                )}
                                                {verifObj.landArea && (
                                                    <span>
                                                        • Diện tích:{" "}
                                                        <strong>
                                                            {verifObj.landArea}
                                                        </strong>
                                                    </span>
                                                )}
                                                {verifObj.landAddress && (
                                                    <span>
                                                        • Đ/c đất:{" "}
                                                        <strong>
                                                            {
                                                                verifObj.landAddress
                                                            }
                                                        </strong>
                                                    </span>
                                                )}
                                                {verifObj.registeredAddress && (
                                                    <span>
                                                        • Đ/c ĐKKD:{" "}
                                                        <strong>
                                                            {
                                                                verifObj.registeredAddress
                                                            }
                                                        </strong>
                                                    </span>
                                                )}
                                            </div>
                                        </div>
                                    );
                                })}
                            </div>
                        </div>

                        {/* Terms Checkbox */}
                        <label className="pw-terms-box">
                            <input
                                type="checkbox"
                                checked={agreedTerms}
                                onChange={(e) =>
                                    setAgreedTerms(e.target.checked)
                                }
                            />
                            <span>
                                Tôi cam kết toàn bộ thông tin đăng ký và hồ sơ
                                giấy tờ cung cấp ở trên là hoàn toàn chính xác,
                                đúng pháp luật. Tôi hoàn toàn chịu trách nhiệm
                                trước pháp luật về tính hợp pháp của địa điểm và
                                hoạt động kinh doanh bãi đỗ xe này.
                            </span>
                        </label>

                        {/* Actions */}
                        <div className="pw-actions">
                            <button
                                type="button"
                                className="pw-btn-secondary"
                                onClick={() => setStep(2)}
                            >
                                ← Quay lại chỉnh sửa
                            </button>
                            <button
                                type="button"
                                className="pw-btn-primary"
                                disabled={isSubmitting || !agreedTerms}
                                onClick={handleSubmitApplication}
                            >
                                {isSubmitting
                                    ? "Đang gửi hồ sơ..."
                                    : "Nộp hồ sơ xét duyệt chính thức 🚀"}
                            </button>
                        </div>
                    </div>
                )}

                {/* STEP 4: SUBMITTED / PENDING / APPROVED / REJECTED STATUS SCREENS */}
                {step === 4 && applicationStatus === "Approved" && (
                    <div className="pw-card pw-status-screen">
                        <div
                            className="pw-status-icon-wrap"
                            style={{ background: "#dcfce7", color: "#16a34a" }}
                        >
                            <ShieldCheckIcon />
                        </div>

                        <div
                            className="pw-status-badge"
                            style={{
                                background: "#dcfce7",
                                color: "#15803d",
                                borderColor: "#86efac",
                            }}
                        >
                            <span>✓</span> Trạng thái: Đã phê duyệt chính thức
                            (Approved)
                        </div>

                        <h2 className="pw-status-title">
                            Hồ sơ đối tác đã được phê duyệt!
                        </h2>
                        <p className="pw-status-desc">
                            Chúc mừng bạn! Tư cách Chủ bãi đỗ xe của cơ sở{" "}
                            <strong>{businessName}</strong> đã được Ban quản trị
                            ParkWise thẩm định và chấp thuận thành công. Bạn có
                            thể bắt đầu đăng ký bãi đỗ xe đầu tiên ngay bây giờ.
                        </p>

                        <div className="pw-status-info-card">
                            <div className="pw-status-info-grid">
                                <div>
                                    <div
                                        style={{
                                            fontSize: 12,
                                            color: "#64748b",
                                            textTransform: "uppercase",
                                            fontWeight: 700,
                                        }}
                                    >
                                        Mã số thuế / Định danh
                                    </div>
                                    <div
                                        style={{
                                            fontSize: 14,
                                            fontWeight: 700,
                                            color: "#0f172a",
                                            marginTop: 4,
                                        }}
                                    >
                                        {taxCode ||
                                            idCardNumber ||
                                            "Cá nhân kinh doanh"}
                                    </div>
                                </div>
                                <div>
                                    <div
                                        style={{
                                            fontSize: 12,
                                            color: "#64748b",
                                            textTransform: "uppercase",
                                            fontWeight: 700,
                                        }}
                                    >
                                        Quyền hạn tài khoản
                                    </div>
                                    <div
                                        style={{
                                            fontSize: 14,
                                            fontWeight: 700,
                                            color: "#16a34a",
                                            marginTop: 4,
                                        }}
                                    >
                                        Chủ bãi đỗ xe (ParkingLotOwner)
                                    </div>
                                </div>
                            </div>
                        </div>

                        <div
                            style={{
                                display: "flex",
                                gap: 14,
                                justifyContent: "center",
                                flexWrap: "wrap",
                                marginTop: 24,
                            }}
                        >
                            <Link
                                href="/owner/parking-lots/new"
                                className="pw-btn-primary"
                            >
                                + Đăng ký bãi đỗ xe đầu tiên →
                            </Link>
                            <Link
                                href="/owner/dashboard"
                                className="pw-btn-secondary"
                            >
                                Vào Dashboard Quản trị
                            </Link>
                        </div>
                    </div>
                )}

                {step === 4 && applicationStatus === "Rejected" && (
                    <div className="pw-card pw-status-screen">
                        <div
                            className="pw-status-icon-wrap"
                            style={{ background: "#fee2e2", color: "#dc2626" }}
                        >
                            <span style={{ fontSize: 36 }}>⚠️</span>
                        </div>

                        <div
                            className="pw-status-badge"
                            style={{
                                background: "#fee2e2",
                                color: "#b91c1c",
                                borderColor: "#fca5a5",
                            }}
                        >
                            <span>✕</span> Trạng thái: Hồ sơ cần chỉnh sửa bổ
                            sung (Rejected)
                        </div>

                        <h2 className="pw-status-title">
                            Yêu cầu cần chỉnh sửa hoặc bổ sung hồ sơ
                        </h2>
                        <p className="pw-status-desc">
                            Hồ sơ pháp lý của cơ sở{" "}
                            <strong>{businessName}</strong> chưa được thông qua
                            bởi Hội đồng kiểm duyệt. Vui lòng kiểm tra lý do
                            dưới đây và cập nhật lại hồ sơ.
                        </p>

                        <div
                            style={{
                                margin: "18px auto 24px",
                                maxWidth: 580,
                                textAlign: "left",
                                background: "#fef2f2",
                                border: "1px solid #fecaca",
                                borderRadius: 14,
                                padding: "16px 20px",
                            }}
                        >
                            <div
                                style={{
                                    fontWeight: 700,
                                    fontSize: 13,
                                    color: "#991b1b",
                                    marginBottom: 6,
                                }}
                            >
                                Lý do từ chối từ Ban quản trị:
                            </div>
                            <div
                                style={{
                                    fontSize: 14,
                                    color: "#7f1d1d",
                                    lineHeight: 1.5,
                                }}
                            >
                                {rejectionReason ||
                                    "Thiếu thông tin địa điểm hoặc tài liệu đính kèm chưa đạt yêu cầu pháp lý."}
                            </div>
                        </div>

                        <div
                            style={{
                                display: "flex",
                                gap: 14,
                                justifyContent: "center",
                                flexWrap: "wrap",
                            }}
                        >
                            <button
                                type="button"
                                className="pw-btn-primary"
                                onClick={() => {
                                    setStep(1);
                                }}
                            >
                                Chỉnh sửa thông tin & Nộp lại hồ sơ →
                            </button>
                            <Link href="/" className="pw-btn-secondary">
                                Về trang chủ
                            </Link>
                        </div>
                    </div>
                )}

                {/* STEP 4: PENDING APPROVAL - ĐÃ XÓA NÚT XEM TRƯỚC DASHBOARD THEO YÊU CẦU */}
                {step === 4 &&
                    applicationStatus !== "Approved" &&
                    applicationStatus !== "Rejected" && (
                        <div className="pw-card pw-status-screen">
                            <div className="pw-status-icon-wrap">
                                <ShieldCheckIcon />
                            </div>

                            <div className="pw-status-badge">
                                <span>⏳</span> Trạng thái: Hồ sơ đã tiếp nhận -
                                Chờ Ban quản trị duyệt
                            </div>

                            <h2 className="pw-status-title">
                                Hồ sơ đối tác đã được gửi thành công!
                            </h2>
                            <p className="pw-status-desc">
                                Hệ thống đã tiếp nhận hồ sơ pháp lý của{" "}
                                <strong>{businessName}</strong>. Hồ sơ của bạn
                                đã được chuyển vào hàng đợi thẩm định của Hội
                                đồng quản trị ParkWise.
                            </p>

                            {/* Email Notification Card */}
                            <div className="pw-email-notice-card">
                                <div className="pw-email-notice-header">
                                    <div className="pw-email-notice-icon">
                                        ✉️
                                    </div>
                                    <div className="pw-email-notice-title">
                                        Hệ thống đã gửi email xác nhận tiếp nhận
                                        hồ sơ
                                    </div>
                                </div>
                                <p className="pw-email-notice-desc">
                                    Email thông báo tiếp nhận hồ sơ kèm mã tra
                                    cứu đã được gửi tự động đến địa chỉ{" "}
                                    <strong>
                                        {contactEmail ||
                                            "email đăng ký của bạn"}
                                    </strong>
                                    . Vui lòng kiểm tra hộp thư đến (hoặc hòm
                                    thư Spam) để theo dõi tiến độ thẩm định.
                                </p>
                            </div>

                            {/* Submission Details */}
                            <div className="pw-status-info-card">
                                <div className="pw-status-info-grid">
                                    <div>
                                        <div
                                            style={{
                                                fontSize: 12,
                                                color: "#64748b",
                                                textTransform: "uppercase",
                                                fontWeight: 700,
                                            }}
                                        >
                                            Thời gian nộp hồ sơ
                                        </div>
                                        <div
                                            style={{
                                                fontSize: 14,
                                                fontWeight: 700,
                                                color: "#0f172a",
                                                marginTop: 4,
                                            }}
                                        >
                                            {submittedAt
                                                ? new Date(
                                                      submittedAt,
                                                  ).toLocaleString("vi-VN")
                                                : "Hôm nay"}
                                        </div>
                                    </div>
                                    <div>
                                        <div
                                            style={{
                                                fontSize: 12,
                                                color: "#64748b",
                                                textTransform: "uppercase",
                                                fontWeight: 700,
                                            }}
                                        >
                                            Thời gian thẩm định ước tính
                                        </div>
                                        <div
                                            style={{
                                                fontSize: 14,
                                                fontWeight: 700,
                                                color: "#059669",
                                                marginTop: 4,
                                            }}
                                        >
                                            Từ 4 giờ đến 24 giờ làm việc
                                        </div>
                                    </div>
                                </div>
                            </div>

                            {/* Approval Process Timeline */}
                            <div className="pw-pending-timeline">
                                <div className="pw-pending-step">
                                    <div className="pw-pending-step-circle done">
                                        ✓
                                    </div>
                                    <div className="pw-pending-step-body">
                                        <h5>Bước 1: Tiếp nhận hồ sơ đối tác</h5>
                                        <p>
                                            Hồ sơ pháp lý và tài liệu đã được
                                            ghi nhận trên hệ thống.
                                        </p>
                                    </div>
                                </div>
                                <div className="pw-pending-step">
                                    <div className="pw-pending-step-circle current">
                                        ⏳
                                    </div>
                                    <div className="pw-pending-step-body">
                                        <h5>
                                            Bước 2: Hội đồng thẩm định hồ sơ
                                        </h5>
                                        <p>
                                            Đội ngũ pháp chế ParkWise đang đối
                                            chiếu thông tin pháp lý và mặt bằng
                                            bãi xe.
                                        </p>
                                    </div>
                                </div>
                                <div className="pw-pending-step">
                                    <div className="pw-pending-step-circle next">
                                        3
                                    </div>
                                    <div className="pw-pending-step-body">
                                        <h5>
                                            Bước 3: Thông báo phê duyệt & Kích
                                            hoạt Dashboard
                                        </h5>
                                        <p>
                                            Sau khi được duyệt, hệ thống sẽ gửi
                                            email chúc mừng. Khi bạn đăng nhập
                                            lại, hệ thống sẽ tự động chuyển sang
                                            trang Dashboard chính thức.
                                        </p>
                                    </div>
                                </div>
                            </div>

                            {/* Simulation Sandbox Notice Box */}
                            <div
                                className="pw-cta-box"
                                style={{
                                    background: "#fefce8",
                                    borderColor: "#fde047",
                                    marginBottom: 28,
                                }}
                            >
                                <h4 style={{ color: "#a16207" }}>
                                    💡 Trải nghiệm Bản mô phỏng trong thời gian
                                    chờ duyệt
                                </h4>
                                <p style={{ color: "#854d0e" }}>
                                    Bạn có thể bấm vào{" "}
                                    <strong>
                                        Trải nghiệm Dashboard mô phỏng (Sandbox)
                                    </strong>{" "}
                                    bên dưới để làm quen với hệ thống, thử
                                    nghiệm thao tác cấu hình bãi xe và bảng giá
                                    cước. Môi trường này hoạt động hoàn toàn độc
                                    lập, sạch sẽ từ đầu và sẽ tự động rollback,
                                    không ảnh hưởng đến dữ liệu thực tế.
                                </p>
                            </div>

                            {/* Action Buttons */}
                            <div
                                style={{
                                    display: "flex",
                                    gap: 14,
                                    justifyContent: "center",
                                    flexWrap: "wrap",
                                }}
                            >
                                <button
                                    type="button"
                                    className="pw-btn-secondary"
                                    onClick={() => {
                                        setStep(1);
                                        setApplicationStatus("None");
                                    }}
                                >
                                    ✏️ Cập nhật lại hồ sơ
                                </button>
                                <Link
                                    href="/owner/simulation"
                                    className="pw-btn-primary"
                                >
                                    🎮 Trải nghiệm Dashboard mô phỏng (Sandbox)
                                    →
                                </Link>
                                <Link href="/" className="pw-btn-secondary">
                                    🏠 Về trang chủ
                                </Link>
                            </div>
                        </div>
                    )}
            </div>

            <VneidAuthModal
                isOpen={vneidModalOpen}
                onClose={() => setVneidModalOpen(false)}
                onConfirm={handleVneidConfirm}
                defaultCccd={
                    ownerType === "Individual"
                        ? "079201234567"
                        : ownerType === "HouseholdBusiness"
                          ? "001199123456"
                          : "038085123456"
                }
                title="Xác thực Căn cước công dân qua VNeID (Level 2 eID)"
                subtitle="Chia sẻ thông tin định danh công dân mức độ 2 để tự động điền hồ sơ chủ bãi"
            />
        </div>
    );
}
