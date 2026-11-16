"use client";

import React, { useState, useEffect } from "react";
import {
    QrCode,
    Car,
    Bike,
    Search,
    RefreshCw,
    ShieldCheck,
    CheckCircle2,
    XCircle,
    AlertTriangle,
    Clock,
    DollarSign,
    Check,
    Smartphone,
    FileText,
    Camera,
    X,
} from "lucide-react";
import { RealCameraFeed } from "./RealCameraFeed";
import QRCode from "react-qr-code";
import {
    PlateBadge,
    feeText,
    formatDateTime,
    formatVnd,
    IoTBarrierGateSimulator,
    QrCodeModal,
} from "./StaffUi";
import {
    completeParkingSession,
    createVnPayParkingFee,
    describeStaffError,
    extendExitWindow,
    findOpenSessionByPlate,
    findSessionByQr,
    getParkingFeePaymentStatus,
    getParkingSession,
    getParkingSessionFee,
    manualVerifyExit,
    payParkingSessionCash,
    recordExitArrival,
    reissueParkingSessionQr,
} from "./staffApi";
import { useStaffData } from "./staffData";
import type { SessionByQrResponse, StaffSession } from "./types";
import { useLanguage } from "@/shared/i18n/LanguageContext";
import { useGateVoice } from "./audio";

export function StaffExitPage() {
    const { assignedLot, completeExit, activeSessions, fetchAssignedLot } =
        useStaffData();
    const { t, language } = useLanguage();
    const { announceCheckOut, announceWarning } = useGateVoice();

    // Reset confirmation state
    const [confirmResetOpen, setConfirmResetOpen] = useState(false);

    // Fetch assigned lot and live active sessions on mount
    React.useEffect(() => {
        let active = true;
        fetchAssignedLot()
            .catch(() => {})
            .finally(() => {
                if (!active) return;
            });
        return () => {
            active = false;
        };
    }, [fetchAssignedLot]);

    // Inputs (start empty, no hardcoded mock data)
    const [qrInput, setQrInput] = useState("");
    const [detectedExitPlate, setDetectedExitPlate] = useState("");
    const [loading, setLoading] = useState(false);
    const [errorMsg, setErrorMsg] = useState("");

    // Scanned session data
    const [scannedSession, setScannedSession] = useState<StaffSession | null>(
        null,
    );

    // Payment modal & handling
    const [paymentModal, setPaymentModal] = useState<"Cash" | "VNPay" | null>(
        null,
    );
    const [cashGiven, setCashGiven] = useState<number>(100000);
    const [paymentSuccess, setPaymentSuccess] = useState(false);
    const [exitAllowed, setExitAllowed] = useState(false);
    const [isPayingCash, setIsPayingCash] = useState(false);
    const [isCreatingVnPay, setIsCreatingVnPay] = useState(false);
    const [isCheckingVnPay, setIsCheckingVnPay] = useState(false);
    const [vnPayUrl, setVnPayUrl] = useState<string | null>(null);
    const [vnPayTxId, setVnPayTxId] = useState<string | null>(null);
    const [vnPayStatus, setVnPayStatus] = useState<string | null>(null);
    const [paymentAlertMsg, setPaymentAlertMsg] = useState<string>("");

    // V6-UI: Modal & Ticket states for QR reissue
    const [reissueModalOpen, setReissueModalOpen] = useState(false);
    const [reissueReason, setReissueReason] = useState(
        "Khách làm mất vé QR tại cổng ra",
    );
    const [reissueLoading, setReissueLoading] = useState(false);
    const [reissuedTicketData, setReissuedTicketData] = useState<{
        qrToken: string;
        plateNumber: string;
        entryTime: string;
        lotName?: string;
    } | null>(null);

    // Q-EXIT-WINDOW: Modal states for 15-minute exit window extension
    const [extendModalOpen, setExtendModalOpen] = useState(false);
    const [extendReason, setExtendReason] = useState(
        "Sự cố ùn tắc tại cổng xuất bãi",
    );
    const [extendLoading, setExtendLoading] = useState(false);
    const [extendSuccessMsg, setExtendSuccessMsg] = useState("");

    // Precise 5-state calculation for Checklist items
    type CheckStatus =
        "PENDING" | "PROCESSING" | "SUCCESS" | "FAILED" | "WARNING";

    // 1. QR Status - Bắt buộc mã QR từ khách hàng (quét hoặc nhập) phải khớp với phiên
    const currentQr = (qrInput ?? "").trim();
    const qrCheckStatus: CheckStatus = loading
        ? "PROCESSING"
        : !currentQr
          ? scannedSession
              ? "WARNING"
              : "PENDING"
          : !scannedSession
            ? "PENDING"
            : scannedSession.qrToken &&
                scannedSession.qrToken === currentQr
              ? "SUCCESS"
              : "FAILED";

    // 2. Plate Match Status
    const rawEntryPlate =
        scannedSession?.plateNumber?.replace(/[^A-Z0-9]/gi, "") || "";
    const rawExitPlate = detectedExitPlate.replace(/[^A-Z0-9]/gi, "");
    const plateCheckStatus: CheckStatus = loading
        ? "PROCESSING"
        : !scannedSession || !rawExitPlate
          ? "PENDING"
          : rawEntryPlate === rawExitPlate
            ? "SUCCESS"
            : !detectedExitPlate.trim()
              ? "WARNING"
              : "FAILED";

    // 3. Session Active Status
    const sessionActive =
        scannedSession?.status === "Active" ||
        scannedSession?.status === "Paid";
    const sessionCheckStatus: CheckStatus = loading
        ? "PROCESSING"
        : !scannedSession
          ? "PENDING"
          : sessionActive
            ? "SUCCESS"
            : scannedSession.status === "Cancelled"
              ? "WARNING"
              : "FAILED";

    // 4. Payment Completed Status
    const paymentCompleted =
        scannedSession?.paymentStatus === "Paid" || paymentSuccess;
    const amountDue = paymentCompleted
        ? 0
        : (scannedSession?.amountDueVnd ?? 0);
    const paymentCheckStatus: CheckStatus = loading
        ? "PROCESSING"
        : !scannedSession
          ? "PENDING"
          : paymentCompleted && amountDue === 0
            ? "SUCCESS"
            : amountDue > 0
              ? "FAILED"
              : "WARNING";

    const qrValid = qrCheckStatus === "SUCCESS";
    const plateMatch = plateCheckStatus === "SUCCESS";

    const canAllowExit =
        plateMatch &&
        sessionActive &&
        paymentCompleted &&
        amountDue === 0 &&
        (qrValid || exitAllowed);

    async function handleVerifyQr(tokenOverride?: string) {
        const token = (
            typeof tokenOverride === "string" ? tokenOverride : qrInput || ""
        ).trim();
        if (!token) {
            setErrorMsg("Vui lòng nhập hoặc quét mã QR.");
            return;
        }

        setLoading(true);
        setErrorMsg("");
        setExitAllowed(false);
        setPaymentSuccess(false);

        try {
            // Call real endpoint GET /api/parking/sessions/by-qr/{qrToken}
            const queryToken = (token || qrInput || "").trim();
            const res: SessionByQrResponse = await findSessionByQr(queryToken);
            recordExitArrival(res.sessionId).catch((err) => {
                console.warn("Failed to record exit arrival:", err);
            });
            const isPaid = res.status === "Paid" || res.amountDueVnd === 0;
            const scanned: StaffSession = {
                id: res.sessionId,
                lotId: res.lotId,
                plateNumber: res.plateNumber,
                vehicleType: res.vehicleType,
                customerType: res.customerType,
                entryTime: res.entryTime,
                status: res.status,
                qrToken: res.qrToken,
                slotCode: null,
                paymentStatus: isPaid ? "Paid" : "Pending",
                totalFeeVnd: res.totalFeeVnd,
                prepaidVnd: isPaid ? res.totalFeeVnd : 0,
                amountDueVnd: isPaid ? 0 : res.amountDueVnd,
            };

            setScannedSession(scanned);
            // Bất biến: Không tự ghi đè biển số đọc ở cổng bằng biển số của phiên
            setQrInput(res.qrToken);
            if (scanned.amountDueVnd === 0 || isPaid) {
                setPaymentSuccess(true);
            }
        } catch (err) {
            setErrorMsg(describeStaffError(err));
            setScannedSession(null);
        } finally {
            setLoading(false);
        }
    }

    // Tra cứu phiên bằng biển số khi khách làm mất vé / QR
    async function handleLookupByPlate() {
        const plate = detectedExitPlate.trim();
        if (!plate) {
            setErrorMsg("Vui lòng nhập biển số xe để tra cứu.");
            return;
        }

        setLoading(true);
        setErrorMsg("");
        setExitAllowed(false);
        setPaymentSuccess(false);

        try {
            const lotId = assignedLot?.lotId || "";
            const res = await findOpenSessionByPlate(lotId, plate);
            // Bất biến: Tra cứu theo biển số KHÔNG tự điền ô QR; khách bắt buộc phải xuất trình vé QR
            const paymentFee = await getParkingSessionFee(res.sessionId).catch(
                () => null,
            );
            const totalFee = paymentFee?.totalFee;
            const amountDue = paymentFee?.amountDue;
            const isPaid = res.status === "Paid" || amountDue === 0;
            const scanned: StaffSession = {
                id: res.sessionId,
                lotId: res.lotId,
                plateNumber: res.plateNumber,
                vehicleType: res.vehicleType,
                customerType: "Guest",
                entryTime: res.entryTime,
                status: res.status,
                qrToken: res.qrToken || "",
                slotCode: null,
                paymentStatus: isPaid ? "Paid" : "Pending",
                totalFeeVnd: totalFee,
                prepaidVnd: isPaid
                    ? totalFee
                    : (paymentFee?.depositAmount ?? 0),
                amountDueVnd: isPaid ? 0 : amountDue,
            };
            setScannedSession(scanned);
            if (isPaid) setPaymentSuccess(true);
        } catch (err) {
            setErrorMsg(describeStaffError(err));
        } finally {
            setLoading(false);
        }
    }

    // Cấp lại vé QR mới (V6-UI: mở modal yêu cầu lý do bắt buộc và in vé)
    function handleOpenReissueModal() {
        if (!scannedSession) return;
        setReissueReason("Khách làm mất vé QR tại cổng ra");
        setReissueModalOpen(true);
    }

    async function handleConfirmReissueQr() {
        if (!scannedSession || !reissueReason.trim()) return;
        setReissueLoading(true);
        setErrorMsg("");
        try {
            const res = await reissueParkingSessionQr(
                scannedSession.id,
                reissueReason.trim(),
            );
            setReissueModalOpen(false);
            setReissuedTicketData({
                qrToken: res.qrToken || "",
                plateNumber: scannedSession.plateNumber,
                entryTime: scannedSession.entryTime,
                lotName: assignedLot?.lotName || "ParkWise",
            });
            // Bất biến (Anti-Bypass): Cấp lại QR KHÔNG tự điền ô quét QR cổng ra; khách bắt buộc phải xuất trình vé mới để quét
            setQrInput("");
            setScannedSession({
                ...scannedSession,
                qrToken: res.qrToken || "",
            });
        } catch (err) {
            setErrorMsg(describeStaffError(err));
        } finally {
            setReissueLoading(false);
        }
    }

    // Q-EXIT-WINDOW: Gia hạn 15 phút thời gian xuất bãi
    function handleOpenExtendModal() {
        if (!scannedSession) return;
        setExtendReason("Sự cố ùn tắc tại cổng xuất bãi");
        setExtendModalOpen(true);
    }

    async function handleConfirmExtendExitWindow() {
        if (!scannedSession || !extendReason.trim()) return;
        setExtendLoading(true);
        setErrorMsg("");
        try {
            await extendExitWindow(
                scannedSession.id,
                extendReason.trim(),
            );
            setExtendModalOpen(false);
            setExtendSuccessMsg("Đã gia hạn thêm 15 phút xuất bãi thành công.");
            setTimeout(() => setExtendSuccessMsg(""), 6000);
            try {
                const detail = await getParkingSession(scannedSession.id);
                if (detail) {
                    setScannedSession((prev) =>
                        prev
                            ? {
                                  ...prev,
                                  status: detail.status,
                              }
                            : null,
                    );
                }
            } catch {
                // ignore
            }
        } catch (err) {
            setErrorMsg(describeStaffError(err));
        } finally {
            setExtendLoading(false);
        }
    }

    // Xác minh đối soát thủ công tại cổng ra
    async function handleManualVerify() {
        if (!scannedSession) return;
        const gatePlate = detectedExitPlate.trim();
        const qrToSend = qrInput.trim();
        if (!qrToSend) {
            setErrorMsg(
                "Chưa có QR phiên đỗ từ khách. Vui lòng quét hoặc nhập mã QR từ vé của khách.",
            );
            announceWarning("Chưa có QR phiên đỗ từ khách.");
            return;
        }

        setLoading(true);
        setErrorMsg("");
        try {
            // Bước 1: Ghi nhận kiểm toán sự cố / xác minh thủ công
            await manualVerifyExit(
                scannedSession.id,
                "Nhân viên đối soát giấy tờ xe thủ công tại quầy",
            );

            // Bước 2: Bắt buộc hoàn tất đóng phiên hợp lệ qua /complete
            await completeParkingSession(scannedSession.id, {
                plateNumber: gatePlate,
                qrToken: qrToSend,
            });
            completeExit(
                scannedSession.id,
                scannedSession.paymentMethod || "Cash",
            );
            setExitAllowed(true);
            announceCheckOut({
                plateNumber: gatePlate || scannedSession.plateNumber,
                lotName: assignedLot?.lotName,
                amountPaid: scannedSession.totalFeeVnd,
            });
            fetchAssignedLot().catch(() => {});
        } catch (err) {
            setErrorMsg(describeStaffError(err));
            announceWarning(describeStaffError(err));
        } finally {
            setLoading(false);
        }
    }

    // The amount sent is the amount Payment quoted for this session; Payment rejects the call when the fee
    // changed in the meantime, so the screen never marks a session paid on its own.
    async function handleCompleteCashPayment() {
        if (!scannedSession || isPayingCash || loading) return;
        setIsPayingCash(true);
        setLoading(true);
        setErrorMsg("");
        try {
            await payParkingSessionCash(
                scannedSession.id,
                scannedSession.amountDueVnd ?? 0,
            );
            setPaymentModal(null);
            setPaymentSuccess(true);
            setScannedSession({
                ...scannedSession,
                paymentStatus: "Paid",
                paymentMethod: "Cash",
                amountDueVnd: 0,
            });
        } catch (err) {
            setPaymentModal(null);
            setErrorMsg(describeStaffError(err));
        } finally {
            setLoading(false);
            setIsPayingCash(false);
        }
    }

    // 100% Real API: Tạo yêu cầu thanh toán VNPay QR
    async function handleOpenVnPayModal() {
        if (!scannedSession) return;
        setPaymentModal("VNPay");
        setIsCreatingVnPay(true);
        setErrorMsg("");
        setVnPayUrl(null);
        setVnPayTxId(null);
        setVnPayStatus(null);

        try {
            const amountDue = scannedSession.amountDueVnd ?? 0;
            const res = await createVnPayParkingFee(scannedSession.id, {
                expectedAmountDue: amountDue,
            });
            setVnPayUrl(res.paymentUrl);
            setVnPayTxId(res.transactionId);
            setVnPayStatus(res.status);
        } catch (err) {
            setErrorMsg(describeStaffError(err));
        } finally {
            setIsCreatingVnPay(false);
        }
    }

    // 100% Real API: Kiểm tra trạng thái thanh toán VNPay
    async function handleCheckVnPayPayment() {
        if (!scannedSession || !vnPayTxId) return;
        setIsCheckingVnPay(true);
        setErrorMsg("");

        try {
            const statusRes = await getParkingFeePaymentStatus(vnPayTxId);
            setVnPayStatus(statusRes.status);

            if (statusRes.status === "Success") {
                for (let i = 0; i < 4; i++) {
                    try {
                        const detail = await getParkingSession(
                            scannedSession.id,
                        );
                        if (detail && detail.status === "Paid") break;
                    } catch {
                        // ignore
                    }
                    await new Promise((r) => setTimeout(r, 600));
                }

                setScannedSession((prev) =>
                    prev
                        ? {
                              ...prev,
                              status: "Paid",
                              paymentStatus: "Paid",
                              amountDueVnd: 0,
                              paymentMethod: "VNPay",
                          }
                        : null,
                );
                setPaymentSuccess(true);
                setPaymentModal(null);
                setPaymentAlertMsg(
                    "Thanh toán VNPay thành công! Xe đã đủ điều kiện xuất bãi.",
                );
                setTimeout(() => setPaymentAlertMsg(""), 6000);
            } else if (statusRes.status === "Failed") {
                setErrorMsg(
                    "Giao dịch VNPay thất bại hoặc bị hủy. Vui lòng thử lại hoặc chọn thu tiền mặt.",
                );
            }
        } catch (err) {
            setErrorMsg(describeStaffError(err));
        } finally {
            setIsCheckingVnPay(false);
        }
    }

    // Tự động kiểm tra trạng thái thanh toán VNPay khi đang mở modal
    React.useEffect(() => {
        if (paymentModal !== "VNPay" || !vnPayTxId || paymentSuccess) return;

        const interval = setInterval(async () => {
            try {
                const statusRes = await getParkingFeePaymentStatus(vnPayTxId);
                if (statusRes.status === "Success") {
                    setVnPayStatus("Success");
                    for (let i = 0; i < 4; i++) {
                        try {
                            const detail = await getParkingSession(
                                scannedSession!.id,
                            );
                            if (detail && detail.status === "Paid") break;
                        } catch {
                            // ignore
                        }
                        await new Promise((r) => setTimeout(r, 600));
                    }
                    setScannedSession((prev) =>
                        prev
                            ? {
                                  ...prev,
                                  status: "Paid",
                                  paymentStatus: "Paid",
                                  amountDueVnd: 0,
                                  paymentMethod: "VNPay",
                              }
                            : null,
                    );
                    setPaymentSuccess(true);
                    setPaymentModal(null);
                    setPaymentAlertMsg(
                        "Khách đã thanh toán VNPay thành công! Xe đã đủ điều kiện xuất bãi.",
                    );
                    setTimeout(() => setPaymentAlertMsg(""), 6000);
                }
            } catch {
                // ignore
            }
        }, 3000);

        return () => clearInterval(interval);
    }, [paymentModal, vnPayTxId, paymentSuccess, scannedSession]);

    // 100% Real API: Close Parking Session
    async function handleAuthorizeExit() {
        if (!scannedSession || !canAllowExit) {
            if (scannedSession && !canAllowExit) {
                announceWarning(
                    "Xe chưa thanh toán phí đỗ hoặc chưa đủ điều kiện an toàn. Không thể mở barrier.",
                );
            }
            return;
        }

        // Bất biến: /complete nhận biển số đọc ở cổng (OCR hoặc nhân viên nhập), không lấy từ phiên
        const gatePlate = detectedExitPlate.trim();
        if (!gatePlate) {
            setErrorMsg(
                "Chưa có biển số đọc ở cổng ra. Vui lòng quét hoặc nhập biển số.",
            );
            announceWarning("Chưa có biển số đọc ở cổng ra.");
            return;
        }

        // Bất biến: QR gửi đi là QR khách đưa ra (quét hoặc nhập), không lấy từ phiên
        const qrToSend = qrInput.trim();
        if (!qrToSend) {
            setErrorMsg(
                "Chưa có QR phiên đỗ từ khách. Vui lòng quét hoặc nhập mã QR từ vé của khách.",
            );
            announceWarning("Chưa có QR phiên đỗ từ khách.");
            return;
        }

        setLoading(true);
        setErrorMsg("");
        try {
            await completeParkingSession(scannedSession.id, {
                plateNumber: gatePlate,
                qrToken: qrToSend,
            });
            completeExit(
                scannedSession.id,
                scannedSession.paymentMethod || "Cash",
            );
            setExitAllowed(true);
            announceCheckOut({
                plateNumber: gatePlate,
                lotName: assignedLot?.lotName,
                amountPaid: scannedSession.totalFeeVnd,
            });
            fetchAssignedLot().catch(() => {});
        } catch (err) {
            // Loại bỏ hoàn toàn đường mở rào chắn cứu cánh: Nếu backend từ chối thì hiện đúng lý do và giữ rào đóng
            setErrorMsg(describeStaffError(err));
            announceWarning(describeStaffError(err));
        } finally {
            setLoading(false);
        }
    }

    function resetExitGate() {
        setScannedSession(null);
        setQrInput("");
        setDetectedExitPlate("");
        setErrorMsg("");
        setPaymentSuccess(false);
        setExitAllowed(false);
        setPaymentModal(null);
        setVnPayUrl(null);
        setVnPayTxId(null);
        setVnPayStatus(null);
        setPaymentAlertMsg("");
        setExtendSuccessMsg("");
        setExtendModalOpen(false);
    }

    // Tự động chuyển tiếp sang xe tiếp theo sau khi mở barrier thành công 3.5 giây
    useEffect(() => {
        if (!exitAllowed) return;
        const timer = setTimeout(() => {
            resetExitGate();
        }, 3500);
        return () => clearTimeout(timer);
    }, [exitAllowed]);

    return (
        <div className="staff-page">
            <header className="staff-header">
                <div>
                    <h1 className="staff-title">
                        {t("exitPageTitle") || "Cổng Ra — Xác Thực & Thu Phí"}
                    </h1>
                    <p className="staff-subtitle">
                        {assignedLot?.lotName
                            ? `Bãi trực: ${assignedLot.lotName} · `
                            : ""}
                        {t("exitPageSubtitle") ||
                            "Quét QR, đối chiếu biển số, kiểm tra thanh toán và hoàn tất quy trình xuất bãi."}
                    </p>
                </div>

                <div className="staff-header-actions">
                    {confirmResetOpen ? (
                        <div className="staff-reset-confirm-box">
                            <span className="staff-reset-confirm-text">
                                Xóa phiên kiểm tra hiện tại?
                            </span>
                            <button
                                type="button"
                                className="staff-btn staff-btn-danger staff-btn-sm"
                                onClick={() => {
                                    resetExitGate();
                                    setConfirmResetOpen(false);
                                }}
                            >
                                <Check size={13} strokeWidth={2.4} />
                                <span>Xác nhận</span>
                            </button>
                            <button
                                type="button"
                                className="staff-btn staff-btn-secondary staff-btn-sm"
                                onClick={() => setConfirmResetOpen(false)}
                            >
                                ✕
                            </button>
                        </div>
                    ) : (
                        <button
                            type="button"
                            className="staff-btn staff-btn-secondary"
                            onClick={() => {
                                if (
                                    scannedSession ||
                                    qrInput ||
                                    detectedExitPlate
                                ) {
                                    setConfirmResetOpen(true);
                                } else {
                                    resetExitGate();
                                }
                            }}
                            title="Làm mới phiên đối soát tại cổng ra"
                        >
                            <RefreshCw size={14} strokeWidth={2.2} />
                            <span>
                                {t("exitClearBtn") || "Làm mới cổng ra"}
                            </span>
                        </button>
                    )}
                </div>
            </header>

            {errorMsg && (
                <div className="staff-alert staff-alert-danger">
                    <span
                        style={{
                            display: "inline-flex",
                            alignItems: "center",
                            gap: "6px",
                        }}
                    >
                        <AlertTriangle size={15} strokeWidth={2.2} />
                        <span>{errorMsg}</span>
                    </span>
                    <button type="button" onClick={() => setErrorMsg("")}>
                        ✕
                    </button>
                </div>
            )}

            {paymentAlertMsg && (
                <div
                    className="staff-alert staff-alert-success"
                    style={{ margin: "12px 0" }}
                >
                    <span
                        style={{
                            display: "inline-flex",
                            alignItems: "center",
                            gap: "6px",
                        }}
                    >
                        <CheckCircle2 size={15} strokeWidth={2.2} />
                        <span>{paymentAlertMsg}</span>
                    </span>
                    <button
                        type="button"
                        onClick={() => setPaymentAlertMsg("")}
                    >
                        ✕
                    </button>
                </div>
            )}

            {extendSuccessMsg && (
                <div
                    className="staff-alert staff-alert-success"
                    style={{ margin: "12px 0" }}
                >
                    <span
                        style={{
                            display: "inline-flex",
                            alignItems: "center",
                            gap: "6px",
                        }}
                    >
                        <CheckCircle2 size={15} strokeWidth={2.2} />
                        <span>{extendSuccessMsg}</span>
                    </span>
                    <button
                        type="button"
                        onClick={() => setExtendSuccessMsg("")}
                    >
                        ✕
                    </button>
                </div>
            )}

            {/* B. Live active sessions quick selector from real DB */}
            {activeSessions.length > 0 && (
                <div className="staff-presets-bar">
                    <span className="staff-presets-label">
                        <Car size={13} strokeWidth={2.3} />
                        Xe đang đỗ trong bãi ({activeSessions.length}):
                    </span>
                    <div className="staff-presets-list">
                        {activeSessions.slice(0, 6).map((s) => {
                            const isSelected = scannedSession?.id === s.id;
                            return (
                                <button
                                    key={s.id}
                                    type="button"
                                    className={`staff-preset-chip ${
                                        isSelected
                                            ? "chip-booking is-selected"
                                            : ""
                                    }`}
                                    onClick={() => {
                                        // Bất biến: Chip chỉ để xem và chọn phiên đối chiếu thông tin
                                        // Tuyệt đối không tự điền biển số cổng ra và không tự điền mã QR của khách (BR-11)
                                        setScannedSession(s);
                                        setErrorMsg("");
                                    }}
                                    title={`Xem thông tin phiên xe ${s.plateNumber} để đối chiếu`}
                                >
                                    {s.vehicleType === "Car" ? (
                                        <Car size={13} strokeWidth={2.2} />
                                    ) : (
                                        <Bike size={13} strokeWidth={2.2} />
                                    )}
                                    <span className="font-mono font-bold">
                                        {s.plateNumber}
                                    </span>
                                    <span className="opacity-70 text-[11px]">
                                        ({(s.qrToken || "").slice(-6)})
                                    </span>
                                </button>
                            );
                        })}
                    </div>
                </div>
            )}

            <div className="staff-grid-split">
                {/* Left column: Real Camera ANPR & QR Scanner */}
                <div className="staff-col">
                    <RealCameraFeed
                        title={
                            t("exitCameraTitle") ||
                            "Camera Cổng Ra 02"
                        }
                        mode="exit"
                        detectedPlate={detectedExitPlate}
                        confidence={0.99}
                        isScanning={loading}
                        onPlateDetected={(plate) => {
                            const clean = plate.trim().toUpperCase();
                            if (!clean) return;
                            setDetectedExitPlate(clean);
                            // Bất biến: Biển số đọc được ở cổng chỉ lưu làm biển số đối soát,
                            // tuyệt đối không tự động lấy QR trong bãi điền vào thay khách
                        }}
                        onQrCodeScanned={(code) => {
                            setQrInput(code);
                            handleVerifyQr(code);
                        }}
                    />

                    {/* D. QR Scanner – Xác thực vé */}
                    <div className="staff-card staff-exit-qr-card">
                        <div className="staff-ocr-card-header">
                            <div className="staff-ocr-title-wrap">
                                <div className="staff-ocr-icon-box">
                                    <QrCode size={18} strokeWidth={2.2} />
                                </div>
                                <div>
                                    <h3>
                                        {t("exitQrVerifTitle") ||
                                            "Xác thực vé QR / Vé khách"}
                                    </h3>
                                    <span className="staff-ocr-sub">
                                        Quét QR từ camera hoặc nhập mã thủ công
                                    </span>
                                </div>
                            </div>

                            <div className="staff-ocr-badges">
                                {loading ? (
                                    <span className="staff-scan-status-pill status-processing">
                                        <RefreshCw
                                            size={12}
                                            strokeWidth={2.4}
                                            className="animate-spin"
                                        />
                                        Đang xác thực vé...
                                    </span>
                                ) : scannedSession ? (
                                    <span className="staff-scan-status-pill status-success">
                                        <CheckCircle2
                                            size={12}
                                            strokeWidth={2.4}
                                        />
                                        Vé hợp lệ
                                    </span>
                                ) : errorMsg ? (
                                    <span className="staff-scan-status-pill status-failed">
                                        <XCircle size={12} strokeWidth={2.4} />
                                        Xác thực thất bại
                                    </span>
                                ) : (
                                    <span className="staff-scan-status-pill status-pending">
                                        <Clock size={12} strokeWidth={2.4} />
                                        Chờ quét mã
                                    </span>
                                )}
                            </div>
                        </div>

                        {/* Input form */}
                        <div className="staff-form-group">
                            <label htmlFor="staff-qr-input">
                                Mã vé QR (Tự động nhận diện từ Camera hoặc dán
                                mã)
                            </label>
                            <div className="staff-input-group staff-exit-input-group">
                                <div className="staff-input-icon-wrap">
                                    <QrCode size={16} strokeWidth={2} />
                                </div>
                                <input
                                    id="staff-qr-input"
                                    type="text"
                                    className="staff-input staff-input-mono staff-has-icon"
                                    value={qrInput || ""}
                                    onChange={(e) => setQrInput(e.target.value)}
                                    placeholder="Hướng camera vào mã QR hoặc dán mã QR-..."
                                    onKeyDown={(e) => {
                                        if (
                                            e.key === "Enter" &&
                                            qrInput.trim() &&
                                            !loading
                                        ) {
                                            handleVerifyQr();
                                        }
                                    }}
                                />
                                <button
                                    type="button"
                                    className="staff-btn staff-btn-primary staff-btn-check-qr"
                                    disabled={loading || !qrInput?.trim()}
                                    onClick={() => handleVerifyQr()}
                                >
                                    {loading ? (
                                        <>
                                            <RefreshCw
                                                size={14}
                                                strokeWidth={2.2}
                                                className="animate-spin"
                                            />
                                            <span>Đang kiểm tra...</span>
                                        </>
                                    ) : (
                                        <>
                                            <ShieldCheck
                                                size={14}
                                                strokeWidth={2.2}
                                            />
                                            <span>
                                                {t("exitVerifyQrBtn") ||
                                                    "Kiểm tra vé"}
                                            </span>
                                        </>
                                    )}
                                </button>
                            </div>
                        </div>

                        {/* E. Vehicle Plate Verification – Đối chiếu biển số */}
                        <div
                            className="staff-form-group"
                            style={{ marginTop: "14px" }}
                        >
                            <div className="staff-plate-label-row">
                                <label htmlFor="staff-exit-plate">
                                    {t("exitCameraPlateLabel") ||
                                        "Biển số xe camera cổng ra"}
                                </label>
                                {detectedExitPlate &&
                                    scannedSession?.plateNumber && (
                                        <span
                                            className={`staff-match-pill ${
                                                plateMatch
                                                    ? "match-success"
                                                    : "match-failed"
                                            }`}
                                        >
                                            {plateMatch ? (
                                                <>
                                                    <CheckCircle2
                                                        size={12}
                                                        strokeWidth={2.4}
                                                    />
                                                    Khớp biển số
                                                </>
                                            ) : (
                                                <>
                                                    <XCircle
                                                        size={12}
                                                        strokeWidth={2.4}
                                                    />
                                                    Không khớp biển số
                                                </>
                                            )}
                                        </span>
                                    )}
                            </div>

                            <div className="staff-input-group staff-exit-input-group">
                                <div className="staff-input-icon-wrap">
                                    <Camera size={16} strokeWidth={2} />
                                </div>
                                <input
                                    id="staff-exit-plate"
                                    type="text"
                                    className="staff-input staff-plate-input staff-has-icon"
                                    value={detectedExitPlate}
                                    onChange={(e) =>
                                        setDetectedExitPlate(
                                            e.target.value.toUpperCase(),
                                        )
                                    }
                                    placeholder="Nhập hoặc để camera nhận diện biển số ra..."
                                    onKeyDown={(e) => {
                                        if (
                                            e.key === "Enter" &&
                                            detectedExitPlate.trim() &&
                                            !loading
                                        ) {
                                            handleLookupByPlate();
                                        }
                                    }}
                                />
                                <button
                                    type="button"
                                    className="staff-btn staff-btn-secondary"
                                    disabled={
                                        loading || !detectedExitPlate.trim()
                                    }
                                    title="Tra cứu phiên đỗ bằng biển số khi khách làm mất vé QR"
                                    onClick={handleLookupByPlate}
                                >
                                    <Search size={14} strokeWidth={2.2} />
                                    <span>Tra cứu mất vé</span>
                                </button>
                            </div>
                        </div>

                        {/* Actions when QR is lost or manual check is needed */}
                        {scannedSession && (
                            <div className="staff-exit-secondary-actions">
                                <button
                                    type="button"
                                    className="staff-btn staff-btn-secondary staff-btn-sm"
                                    disabled={loading}
                                    onClick={handleOpenReissueModal}
                                    title="Hủy mã QR cũ và phát hành mã QR mới cho khách"
                                >
                                    <RefreshCw size={13} strokeWidth={2.2} />
                                    <span>Cấp lại vé QR</span>
                                </button>
                                <button
                                    type="button"
                                    className={`staff-btn staff-btn-sm ${
                                        exitAllowed
                                            ? "staff-btn-success"
                                            : "staff-btn-secondary"
                                    }`}
                                    disabled={loading}
                                    onClick={handleManualVerify}
                                    title="Nhân viên đối soát thủ công giấy tờ xe tại quầy"
                                >
                                    <ShieldCheck size={13} strokeWidth={2.2} />
                                    <span>
                                        {exitAllowed
                                            ? "Đã duyệt thủ công"
                                            : "Xác minh thủ công"}
                                    </span>
                                </button>
                                {(scannedSession.status === "Paid" ||
                                    paymentCompleted) && (
                                    <button
                                        type="button"
                                        className="staff-btn staff-btn-secondary staff-btn-sm"
                                        disabled={loading || extendLoading}
                                        onClick={handleOpenExtendModal}
                                        title="Gia hạn 15 phút thời gian rời bãi cho phiên xe đã thanh toán khi bãi có sự cố"
                                    >
                                        <Clock size={13} strokeWidth={2.2} />
                                        <span>Gia hạn 15 phút</span>
                                    </button>
                                )}
                            </div>
                        )}
                    </div>
                </div>

                {/* Right column: Exit Verification checklist & Parking Fee */}
                <div className="staff-col">
                    <div className="staff-card staff-exit-checklist-card">
                        <div className="staff-card-header">
                            <div className="staff-ocr-title-wrap">
                                <div className="staff-ocr-icon-box">
                                    <ShieldCheck size={18} strokeWidth={2.2} />
                                </div>
                                <div>
                                    <h2 style={{ fontSize: "16px", margin: 0 }}>
                                        {t("exitChecklistTitle") ||
                                            "Kiểm tra điều kiện xuất bãi"}
                                    </h2>
                                    <span className="staff-ocr-sub">
                                        4 tiêu chí an toàn trước khi kích hoạt
                                        mở rào chắn
                                    </span>
                                </div>
                            </div>
                            {canAllowExit ? (
                                <span className="staff-pill staff-pill-success">
                                    <CheckCircle2
                                        size={12}
                                        strokeWidth={2.4}
                                        style={{ marginRight: 4 }}
                                    />
                                    {t("exitAuthorized") ||
                                        "ĐỦ ĐIỀU KIỆN XUẤT BÃI"}
                                </span>
                            ) : (
                                <span className="staff-pill staff-pill-warning">
                                    <Clock
                                        size={12}
                                        strokeWidth={2.4}
                                        style={{ marginRight: 4 }}
                                    />
                                    {scannedSession
                                        ? t("exitDenied") || "CHƯA ĐỦ ĐIỀU KIỆN"
                                        : "CHỜ ĐỐI SOÁT"}
                                </span>
                            )}
                        </div>

                        {/* F. EXIT VALIDATION CHECKLIST (5 Distinct States: PENDING, PROCESSING, SUCCESS, FAILED, WARNING) */}
                        <div className="staff-checklist">
                            {/* Điều kiện 1: Vé QR phiên xe hợp lệ */}
                            <div
                                className={`staff-check-item status-${qrCheckStatus.toLowerCase()}`}
                            >
                                <div className="check-icon-wrap">
                                    {qrCheckStatus === "SUCCESS" && (
                                        <CheckCircle2
                                            size={18}
                                            strokeWidth={2.4}
                                        />
                                    )}
                                    {qrCheckStatus === "FAILED" && (
                                        <XCircle size={18} strokeWidth={2.4} />
                                    )}
                                    {qrCheckStatus === "WARNING" && (
                                        <AlertTriangle
                                            size={18}
                                            strokeWidth={2.4}
                                        />
                                    )}
                                    {qrCheckStatus === "PROCESSING" && (
                                        <RefreshCw
                                            size={18}
                                            strokeWidth={2.4}
                                            className="animate-spin"
                                        />
                                    )}
                                    {qrCheckStatus === "PENDING" && (
                                        <Clock size={18} strokeWidth={2.2} />
                                    )}
                                </div>
                                <div className="check-meta">
                                    <div className="check-title">
                                        1. Vé QR phiên xe hợp lệ
                                    </div>
                                    <div className="check-desc">
                                        {qrCheckStatus === "SUCCESS" && (
                                            <>
                                                <span>Mã QR:</span>
                                                <code className="staff-code-pill">
                                                    {scannedSession?.qrToken ||
                                                        "Đã xác minh thủ công"}
                                                </code>
                                            </>
                                        )}
                                        {qrCheckStatus === "WARNING" && (
                                            <span className="text-amber">
                                                Khách mất vé QR — Vui lòng cấp
                                                lại vé hoặc xác minh thủ công
                                            </span>
                                        )}
                                        {qrCheckStatus === "FAILED" && (
                                            <span className="text-danger">
                                                Mã vé không tồn tại hoặc không
                                                hợp lệ
                                            </span>
                                        )}
                                        {qrCheckStatus === "PROCESSING" && (
                                            <span className="text-blue">
                                                Đang truy vấn thông tin vé từ hệ
                                                thống...
                                            </span>
                                        )}
                                        {qrCheckStatus === "PENDING" && (
                                            <span>
                                                Chờ quét mã QR từ camera hoặc
                                                nhập mã tra cứu
                                            </span>
                                        )}
                                    </div>
                                </div>
                                <div className="check-badge-wrap">
                                    <span
                                        className={`staff-check-badge badge-${qrCheckStatus.toLowerCase()}`}
                                    >
                                        {qrCheckStatus === "SUCCESS" && "Đạt"}
                                        {qrCheckStatus === "FAILED" &&
                                            "Không đạt"}
                                        {qrCheckStatus === "WARNING" &&
                                            "Cần kiểm tra"}
                                        {qrCheckStatus === "PROCESSING" &&
                                            "Đang xác thực"}
                                        {qrCheckStatus === "PENDING" &&
                                            "Chờ kiểm tra"}
                                    </span>
                                </div>
                            </div>

                            {/* Điều kiện 2: Khớp biển số vào – ra */}
                            <div
                                className={`staff-check-item status-${plateCheckStatus.toLowerCase()}`}
                            >
                                <div className="check-icon-wrap">
                                    {plateCheckStatus === "SUCCESS" && (
                                        <CheckCircle2
                                            size={18}
                                            strokeWidth={2.4}
                                        />
                                    )}
                                    {plateCheckStatus === "FAILED" && (
                                        <XCircle size={18} strokeWidth={2.4} />
                                    )}
                                    {plateCheckStatus === "WARNING" && (
                                        <AlertTriangle
                                            size={18}
                                            strokeWidth={2.4}
                                        />
                                    )}
                                    {plateCheckStatus === "PROCESSING" && (
                                        <RefreshCw
                                            size={18}
                                            strokeWidth={2.4}
                                            className="animate-spin"
                                        />
                                    )}
                                    {plateCheckStatus === "PENDING" && (
                                        <Clock size={18} strokeWidth={2.2} />
                                    )}
                                </div>
                                <div className="check-meta">
                                    <div className="check-title">
                                        2. Khớp biển số vào – ra
                                    </div>
                                    <div className="check-desc">
                                        {plateCheckStatus === "SUCCESS" && (
                                            <>
                                                <span>Cổng ra:</span>
                                                <code className="staff-code-pill font-mono">
                                                    {detectedExitPlate}
                                                </code>
                                                <span className="check-desc-sep">
                                                    ✓ Khớp với vé:
                                                </span>
                                                <code className="staff-code-pill font-mono">
                                                    {
                                                        scannedSession?.plateNumber
                                                    }
                                                </code>
                                            </>
                                        )}
                                        {plateCheckStatus === "FAILED" && (
                                            <>
                                                <span className="text-danger">
                                                    Lệch biển số:
                                                </span>
                                                <code className="staff-code-pill text-danger">
                                                    {detectedExitPlate || "—"}
                                                </code>
                                                <span className="check-desc-sep">
                                                    ≠
                                                </span>
                                                <code className="staff-code-pill">
                                                    {scannedSession?.plateNumber ||
                                                        "—"}
                                                </code>
                                            </>
                                        )}
                                        {plateCheckStatus === "PROCESSING" && (
                                            <span className="text-blue">
                                                Đang đối soát ký tự quang học
                                                với cơ sở dữ liệu...
                                            </span>
                                        )}
                                        {plateCheckStatus === "PENDING" && (
                                            <span>
                                                {scannedSession
                                                    ? "Chờ camera hoặc nhân viên nhập biển số cổng ra"
                                                    : "Chưa đủ dữ liệu (chờ quét vé và đọc biển số)"}
                                            </span>
                                        )}
                                    </div>
                                </div>
                                <div className="check-badge-wrap">
                                    <span
                                        className={`staff-check-badge badge-${plateCheckStatus.toLowerCase()}`}
                                    >
                                        {plateCheckStatus === "SUCCESS" &&
                                            "Đạt"}
                                        {plateCheckStatus === "FAILED" &&
                                            "Không đạt"}
                                        {plateCheckStatus === "WARNING" &&
                                            "Cần kiểm tra"}
                                        {plateCheckStatus === "PROCESSING" &&
                                            "Đang xác thực"}
                                        {plateCheckStatus === "PENDING" &&
                                            "Chờ kiểm tra"}
                                    </span>
                                </div>
                            </div>

                            {/* Điều kiện 3: Trạng thái phiên xe */}
                            <div
                                className={`staff-check-item status-${sessionCheckStatus.toLowerCase()}`}
                            >
                                <div className="check-icon-wrap">
                                    {sessionCheckStatus === "SUCCESS" && (
                                        <CheckCircle2
                                            size={18}
                                            strokeWidth={2.4}
                                        />
                                    )}
                                    {sessionCheckStatus === "FAILED" && (
                                        <XCircle size={18} strokeWidth={2.4} />
                                    )}
                                    {sessionCheckStatus === "WARNING" && (
                                        <AlertTriangle
                                            size={18}
                                            strokeWidth={2.4}
                                        />
                                    )}
                                    {sessionCheckStatus === "PROCESSING" && (
                                        <RefreshCw
                                            size={18}
                                            strokeWidth={2.4}
                                            className="animate-spin"
                                        />
                                    )}
                                    {sessionCheckStatus === "PENDING" && (
                                        <Clock size={18} strokeWidth={2.2} />
                                    )}
                                </div>
                                <div className="check-meta">
                                    <div className="check-title">
                                        3. Trạng thái phiên xe
                                    </div>
                                    <div className="check-desc">
                                        {scannedSession ? (
                                            sessionActive ? (
                                                <span>
                                                    Phiên đang hiệu lực:{" "}
                                                    <strong className="text-emerald font-semibold">
                                                        {scannedSession.status ===
                                                        "Paid"
                                                            ? "Đã thanh toán"
                                                            : "Đang gửi xe"}
                                                    </strong>
                                                </span>
                                            ) : (
                                                <span className="text-danger">
                                                    Phiên không hợp lệ (
                                                    {scannedSession.status})
                                                </span>
                                            )
                                        ) : (
                                            <span>
                                                Chờ tải phiên xe từ hệ thống...
                                            </span>
                                        )}
                                    </div>
                                </div>
                                <div className="check-badge-wrap">
                                    <span
                                        className={`staff-check-badge badge-${sessionCheckStatus.toLowerCase()}`}
                                    >
                                        {sessionCheckStatus === "SUCCESS" &&
                                            "Đạt"}
                                        {sessionCheckStatus === "FAILED" &&
                                            "Không đạt"}
                                        {sessionCheckStatus === "WARNING" &&
                                            "Cần kiểm tra"}
                                        {sessionCheckStatus === "PROCESSING" &&
                                            "Đang xác thực"}
                                        {sessionCheckStatus === "PENDING" &&
                                            "Chờ kiểm tra"}
                                    </span>
                                </div>
                            </div>

                            {/* Điều kiện 4: Thanh toán đầy đủ */}
                            <div
                                className={`staff-check-item status-${paymentCheckStatus.toLowerCase()}`}
                            >
                                <div className="check-icon-wrap">
                                    {paymentCheckStatus === "SUCCESS" && (
                                        <CheckCircle2
                                            size={18}
                                            strokeWidth={2.4}
                                        />
                                    )}
                                    {paymentCheckStatus === "FAILED" && (
                                        <XCircle size={18} strokeWidth={2.4} />
                                    )}
                                    {paymentCheckStatus === "WARNING" && (
                                        <AlertTriangle
                                            size={18}
                                            strokeWidth={2.4}
                                        />
                                    )}
                                    {paymentCheckStatus === "PROCESSING" && (
                                        <RefreshCw
                                            size={18}
                                            strokeWidth={2.4}
                                            className="animate-spin"
                                        />
                                    )}
                                    {paymentCheckStatus === "PENDING" && (
                                        <Clock size={18} strokeWidth={2.2} />
                                    )}
                                </div>
                                <div className="check-meta">
                                    <div className="check-title">
                                        4. Thanh toán đầy đủ
                                    </div>
                                    <div className="check-desc">
                                        {scannedSession ? (
                                            paymentCompleted ? (
                                                <span className="text-emerald font-medium">
                                                    ✓ Đã hoàn tất thanh toán
                                                    cước phí gửi xe
                                                </span>
                                            ) : (
                                                <span className="text-danger font-semibold">
                                                    Còn cước phải thu:{" "}
                                                    {formatVnd(
                                                        scannedSession.amountDueVnd ??
                                                            0,
                                                    )}
                                                </span>
                                            )
                                        ) : (
                                            <span>
                                                Chờ xác định số tiền cước từ
                                                phiên xe...
                                            </span>
                                        )}
                                    </div>
                                </div>
                                <div className="check-badge-wrap">
                                    <span
                                        className={`staff-check-badge badge-${paymentCheckStatus.toLowerCase()}`}
                                    >
                                        {paymentCheckStatus === "SUCCESS" &&
                                            "Đạt"}
                                        {paymentCheckStatus === "FAILED" &&
                                            "Không đạt"}
                                        {paymentCheckStatus === "WARNING" &&
                                            "Cần kiểm tra"}
                                        {paymentCheckStatus === "PROCESSING" &&
                                            "Đang xác thực"}
                                        {paymentCheckStatus === "PENDING" &&
                                            "Chờ kiểm tra"}
                                    </span>
                                </div>
                            </div>
                        </div>

                        {/* G. EXIT SUMMARY – Tổng kết xuất bãi */}
                        {scannedSession && (
                            <div className="staff-fee-box staff-exit-summary-box">
                                <div className="staff-fee-header">
                                    <div
                                        style={{
                                            display: "flex",
                                            alignItems: "center",
                                            gap: 8,
                                        }}
                                    >
                                        <FileText size={16} strokeWidth={2.2} />
                                        <h3>
                                            {t("exitFeeTitle") ||
                                                "Tổng kết chi phí & phiên xe"}
                                        </h3>
                                    </div>
                                    <PlateBadge
                                        plate={scannedSession.plateNumber}
                                    />
                                </div>

                                <div className="staff-fee-rows">
                                    <div className="staff-fee-row">
                                        <span>Loại phương tiện</span>
                                        <strong
                                            style={{
                                                display: "inline-flex",
                                                alignItems: "center",
                                                gap: 6,
                                            }}
                                        >
                                            {scannedSession.vehicleType ===
                                            "Car" ? (
                                                <>
                                                    <Car
                                                        size={13}
                                                        strokeWidth={2}
                                                    />
                                                    {t("staffCar") ||
                                                        "Ô tô (4-7 chỗ)"}
                                                </>
                                            ) : (
                                                <>
                                                    <Bike
                                                        size={13}
                                                        strokeWidth={2}
                                                    />
                                                    {t("staffMotorbike") ||
                                                        "Xe máy"}
                                                </>
                                            )}
                                        </strong>
                                    </div>
                                    <div className="staff-fee-row">
                                        <span>Mã chỗ</span>
                                        <span className="staff-slot-code">
                                            {scannedSession.slotCode ??
                                                "Chưa phân ô"}
                                        </span>
                                    </div>
                                    <div className="staff-fee-row">
                                        <span>Giờ vào bãi</span>
                                        <strong suppressHydrationWarning>
                                            {formatDateTime(
                                                scannedSession.entryTime,
                                            )}
                                        </strong>
                                    </div>
                                    <div className="staff-fee-row">
                                        <span>Giờ xuất bãi</span>
                                        <strong suppressHydrationWarning>
                                            {formatDateTime(
                                                new Date().toISOString(),
                                            )}
                                        </strong>
                                    </div>
                                    <div className="staff-fee-row">
                                        <span>
                                            {t("exitTotalFee") ||
                                                "Tổng phí dịch vụ"}
                                        </span>
                                        <strong>
                                            {feeText(
                                                scannedSession.totalFeeVnd,
                                                formatVnd,
                                            )}
                                        </strong>
                                    </div>
                                    <div className="fee-row">
                                        <span>
                                            {t("exitPrepaidWallet") ||
                                                "Đã thanh toán trước / ví"}
                                        </span>
                                        <span className="text-emerald">
                                            -{" "}
                                            {feeText(
                                                scannedSession.prepaidVnd,
                                                formatVnd,
                                            )}
                                        </span>
                                    </div>
                                    <div className="fee-row fee-total">
                                        <span>
                                            {t("exitAmountDue") ||
                                                "Số tiền còn phải thanh toán"}
                                        </span>
                                        <strong
                                            className={
                                                amountDue > 0
                                                    ? "text-danger"
                                                    : "text-emerald"
                                            }
                                        >
                                            {formatVnd(amountDue)}
                                        </strong>
                                    </div>
                                </div>

                                {/* Payment Actions */}
                                {amountDue > 0 && (
                                    <div className="staff-pay-buttons">
                                        <button
                                            type="button"
                                            className="staff-btn staff-btn-primary staff-btn-flex"
                                            onClick={() => {
                                                setPaymentModal("Cash");
                                                setCashGiven(
                                                    scannedSession?.amountDueVnd ??
                                                        0,
                                                );
                                            }}
                                        >
                                            <DollarSign
                                                size={15}
                                                strokeWidth={2.2}
                                            />
                                            <span>
                                                {t("exitCollectCash") ||
                                                    "Thu tiền mặt"}
                                            </span>
                                        </button>
                                        <button
                                            type="button"
                                            className="staff-btn staff-btn-secondary staff-btn-flex"
                                            onClick={handleOpenVnPayModal}
                                        >
                                            <Smartphone
                                                size={15}
                                                strokeWidth={2.2}
                                            />
                                            <span>
                                                {t("exitVnPayQr") ||
                                                    "Quét VNPay QR"}
                                            </span>
                                        </button>
                                    </div>
                                )}
                            </div>
                        )}

                        {/* Barrier opened alert with Interactive IoT Simulator */}
                        {exitAllowed && (
                            <div style={{ marginTop: "1rem" }}>
                                <IoTBarrierGateSimulator
                                    gateId="GATE-OUT-02"
                                    gateName="Cổng Ra Xe 02"
                                    gateType="EXIT"
                                    isOpen={true}
                                    physicalState="OPEN"
                                    vehicleState="PASSING"
                                    detectedPlate={
                                        scannedSession?.plateNumber ||
                                        detectedExitPlate
                                    }
                                    vehicleType={
                                        scannedSession?.vehicleType || "Car"
                                    }
                                    compact={true}
                                    enableControls={false}
                                    enableEmergencyOverride={true}
                                />
                                <div
                                    className="staff-barrier-open-alert"
                                    style={{ marginTop: "8px" }}
                                >
                                    <div className="barrier-icon">
                                        <CheckCircle2
                                            size={18}
                                            strokeWidth={2.4}
                                        />
                                    </div>
                                    <div className="barrier-content">
                                        <strong>
                                            {t("exitBarrierRaised") ||
                                                "Cần Barrier đã mở — Xe được phép xuất bãi"}
                                        </strong>
                                        <p>
                                            {t("exitBarrierHint") ||
                                                "Rào chắn sẽ tự động hạ sau khi cảm biến xác nhận xe đã rời khỏi cổng."}
                                        </p>
                                    </div>
                                </div>
                            </div>
                        )}

                        {/* H. CONFIRM EXIT – Xác nhận xuất bãi */}
                        <div
                            className="staff-exit-actions"
                            style={{ marginTop: "16px" }}
                        >
                            {!exitAllowed ? (
                                <>
                                    <button
                                        type="button"
                                        className={`staff-btn staff-btn-lg staff-btn-checkin-primary ${
                                            canAllowExit ? "btn-exit-ready" : ""
                                        }`}
                                        disabled={!canAllowExit || loading}
                                        onClick={handleAuthorizeExit}
                                        title={
                                            canAllowExit
                                                ? "Đủ điều kiện — Bấm để mở rào chắn và xuất bãi"
                                                : "Chưa đủ 4 điều kiện an toàn (mã vé, khớp biển số, phiên và thanh toán)"
                                        }
                                    >
                                        {loading ? (
                                            <>
                                                <RefreshCw
                                                    size={16}
                                                    strokeWidth={2.3}
                                                    className="animate-spin"
                                                />
                                                <span>
                                                    {t("exitVerifying") ||
                                                        "Đang xử lý xuất bãi..."}
                                                </span>
                                            </>
                                        ) : (
                                            <>
                                                <ShieldCheck
                                                    size={18}
                                                    strokeWidth={2.2}
                                                />
                                                <span>
                                                    Xác nhận ra & Mở rào chắn
                                                </span>
                                            </>
                                        )}
                                    </button>
                                    {!canAllowExit && scannedSession && (
                                        <div className="staff-exit-blocked-hint">
                                            <AlertTriangle
                                                size={13}
                                                strokeWidth={2.2}
                                            />
                                            <span>
                                                {!plateMatch
                                                    ? "Chưa mở được rào: Biển số xe cổng ra chưa khớp với vé vào."
                                                    : amountDue > 0
                                                      ? "Chưa mở được rào: Phiên đỗ còn khoản cước chưa thanh toán."
                                                      : !qrValid
                                                        ? "Chưa mở được rào: Cần cấp lại vé QR hoặc xác minh thủ công."
                                                        : "Chưa mở được rào: Phiên xe không ở trạng thái hợp lệ."}
                                            </span>
                                        </div>
                                    )}
                                </>
                            ) : (
                                <button
                                    type="button"
                                    className="staff-btn staff-btn-primary staff-btn-lg"
                                    onClick={resetExitGate}
                                >
                                    <CheckCircle2 size={16} strokeWidth={2.2} />
                                    <span>
                                        {t("exitDoneBtn") ||
                                            "Hoàn tất xe này · Tiếp tục xe sau"}
                                    </span>
                                </button>
                            )}
                        </div>
                    </div>
                </div>
            </div>

            {/* Payment Modal */}
            {paymentModal && scannedSession && (
                <div
                    className="staff-modal-backdrop"
                    onClick={() => setPaymentModal(null)}
                >
                    <div
                        className="staff-modal"
                        onClick={(e) => e.stopPropagation()}
                    >
                        <div className="staff-modal-header">
                            <h2>
                                {paymentModal === "Cash"
                                    ? t("exitCashModalTitle")
                                    : t("exitVnPayModalTitle")}
                            </h2>
                            <button
                                type="button"
                                className="staff-modal-close"
                                onClick={() => setPaymentModal(null)}
                            >
                                ✕
                            </button>
                        </div>

                        <div className="staff-payment-modal-body">
                            {/* Receipt Summary Card */}
                            <div className="staff-modal-receipt-card">
                                <div className="staff-summary-row">
                                    <span className="staff-summary-label">
                                        {t("exitPlateLabel")}
                                    </span>
                                    <PlateBadge
                                        plate={scannedSession.plateNumber}
                                        size="normal"
                                    />
                                </div>
                                <div className="staff-summary-row">
                                    <span className="staff-summary-label">
                                        {t("exitSpotLabel")}
                                    </span>
                                    <span className="staff-slot-code">
                                        {t("sessionsSpot")}{" "}
                                        {scannedSession.slotCode ?? "—"}
                                    </span>
                                </div>
                                <div className="staff-summary-row staff-summary-due-row">
                                    <span className="staff-summary-label">
                                        {t("exitAmountDueLabel")}
                                    </span>
                                    <strong className="staff-modal-amount-due">
                                        {feeText(
                                            scannedSession.amountDueVnd,
                                            formatVnd,
                                        )}
                                    </strong>
                                </div>
                            </div>

                            {paymentModal === "Cash" ? (
                                <div className="staff-cash-flow">
                                    <div className="staff-form-group">
                                        <div className="staff-form-label-row">
                                            <label htmlFor="staff-cash-received">
                                                {t("exitCashReceivedLabel")}
                                            </label>
                                            <span className="staff-form-hint">
                                                {t("exitCashHint")}
                                            </span>
                                        </div>

                                        <div className="staff-input-cash-wrap">
                                            <input
                                                id="staff-cash-received"
                                                type="number"
                                                step="1000"
                                                className="staff-input staff-cash-input"
                                                value={cashGiven || ""}
                                                onChange={(e) =>
                                                    setCashGiven(
                                                        Number(e.target.value),
                                                    )
                                                }
                                                placeholder="100000"
                                            />
                                            <span className="staff-cash-unit">
                                                ₫
                                            </span>
                                        </div>

                                        {/* Quick tap cash presets */}
                                        <div className="staff-quick-cash-row">
                                            <button
                                                type="button"
                                                className="staff-quick-cash-btn"
                                                onClick={() =>
                                                    setCashGiven(
                                                        scannedSession.amountDueVnd ??
                                                            0,
                                                    )
                                                }
                                            >
                                                {t("exitExactCash")} (
                                                {feeText(
                                                    scannedSession.amountDueVnd,
                                                    formatVnd,
                                                )}
                                                )
                                            </button>
                                            {[100000, 200000, 500000].map(
                                                (preset) => (
                                                    <button
                                                        key={preset}
                                                        type="button"
                                                        className={`staff-quick-cash-btn ${
                                                            cashGiven === preset
                                                                ? "active"
                                                                : ""
                                                        }`}
                                                        onClick={() =>
                                                            setCashGiven(preset)
                                                        }
                                                    >
                                                        {preset.toLocaleString(
                                                            language === "vi"
                                                                ? "vi-VN"
                                                                : "en-US",
                                                        )}
                                                        ₫
                                                    </button>
                                                ),
                                            )}
                                        </div>
                                    </div>

                                    <div className="staff-change-box">
                                        <div className="staff-change-info">
                                            <span className="staff-change-label">
                                                {t("exitChangeDue")}
                                            </span>
                                            <span className="staff-change-sub">
                                                {cashGiven >=
                                                (scannedSession.amountDueVnd ??
                                                    0)
                                                    ? language === "vi"
                                                        ? "Khách đã trả đủ tiền mặt"
                                                        : "Sufficient cash received"
                                                    : language === "vi"
                                                      ? "Chưa đủ số tiền cước"
                                                      : "Insufficient cash"}
                                            </span>
                                        </div>
                                        <strong className="staff-change-val">
                                            {formatVnd(
                                                Math.max(
                                                    0,
                                                    cashGiven -
                                                        (scannedSession.amountDueVnd ??
                                                            0),
                                                ),
                                            )}
                                        </strong>
                                    </div>
                                    <div className="staff-modal-action-bar">
                                        <button
                                            type="button"
                                            className="staff-btn staff-btn-secondary"
                                            onClick={() =>
                                                setPaymentModal(null)
                                            }
                                        >
                                            {t("cancel")}
                                        </button>
                                        <button
                                            type="button"
                                            className="staff-btn staff-btn-primary staff-btn-flex"
                                            disabled={
                                                isPayingCash ||
                                                cashGiven <
                                                    (scannedSession.amountDueVnd ??
                                                        0)
                                            }
                                            onClick={handleCompleteCashPayment}
                                        >
                                            {isPayingCash ? (
                                                <>
                                                    <RefreshCw
                                                        size={14}
                                                        className="staff-spin"
                                                    />
                                                    <span>
                                                        Đang xử lý thu tiền...
                                                    </span>
                                                </>
                                            ) : (
                                                t("exitConfirmCashPayment")
                                            )}
                                        </button>
                                    </div>
                                </div>
                            ) : (
                                <div className="staff-vnpay-flow">
                                    <p className="staff-vnpay-desc">
                                        {language === "vi"
                                            ? "Tài xế mở ứng dụng Ngân hàng / VNPay / Ví điện tử quét mã QR bên dưới để thanh toán phí xuất bãi."
                                            : "Instruct the driver to scan the QR code below using any mobile banking or VNPay app."}
                                    </p>

                                    {isCreatingVnPay ? (
                                        <div
                                            style={{
                                                display: "flex",
                                                flexDirection: "column",
                                                alignItems: "center",
                                                justifyContent: "center",
                                                padding: "36px 12px",
                                                gap: "8px",
                                            }}
                                        >
                                            <RefreshCw
                                                size={24}
                                                className="staff-spin"
                                                style={{ color: "#3b82f6" }}
                                            />
                                            <span
                                                style={{
                                                    fontSize: "13px",
                                                    color: "#64748b",
                                                    fontWeight: 600,
                                                }}
                                            >
                                                Đang khởi tạo mã thanh toán từ
                                                cổng VNPay...
                                            </span>
                                        </div>
                                    ) : vnPayUrl ? (
                                        <div
                                            style={{
                                                display: "flex",
                                                flexDirection: "column",
                                                alignItems: "center",
                                                gap: "12px",
                                                padding: "16px 0",
                                            }}
                                        >
                                            <div
                                                style={{
                                                    background: "#ffffff",
                                                    padding: "14px",
                                                    borderRadius: "14px",
                                                    border: "2px solid #e2e8f0",
                                                    display: "inline-flex",
                                                    boxShadow:
                                                        "0 4px 12px rgba(0,0,0,0.06)",
                                                }}
                                            >
                                                <QRCode
                                                    value={vnPayUrl}
                                                    size={180}
                                                />
                                            </div>

                                            <div
                                                style={{
                                                    display: "flex",
                                                    flexDirection: "column",
                                                    alignItems: "center",
                                                    gap: "4px",
                                                }}
                                            >
                                                <span
                                                    style={{
                                                        fontSize: "14px",
                                                        fontWeight: 800,
                                                        color: "#0f172a",
                                                    }}
                                                >
                                                    Cước phí:{" "}
                                                    {feeText(
                                                        scannedSession.amountDueVnd,
                                                        formatVnd,
                                                    )}
                                                </span>
                                                <span
                                                    style={{
                                                        fontSize: "11px",
                                                        color: "#64748b",
                                                    }}
                                                >
                                                    Mã GD:{" "}
                                                    {vnPayTxId?.slice(0, 16)}...
                                                    · Trạng thái:{" "}
                                                    <span
                                                        style={{
                                                            color:
                                                                vnPayStatus ===
                                                                "Success"
                                                                    ? "#10b981"
                                                                    : "#f59e0b",
                                                            fontWeight: 700,
                                                        }}
                                                    >
                                                        {vnPayStatus ||
                                                            "Đang chờ quét"}
                                                    </span>
                                                </span>
                                                <a
                                                    href={vnPayUrl}
                                                    target="_blank"
                                                    rel="noreferrer"
                                                    style={{
                                                        fontSize: "12px",
                                                        color: "#2563eb",
                                                        textDecoration:
                                                            "underline",
                                                        marginTop: "2px",
                                                        fontWeight: 600,
                                                    }}
                                                >
                                                    Mở trang thanh toán VNPay
                                                    (Tab mới)
                                                </a>
                                            </div>
                                        </div>
                                    ) : (
                                        <div
                                            style={{
                                                textAlign: "center",
                                                padding: "24px 12px",
                                            }}
                                        >
                                            <p
                                                style={{
                                                    color: "#ef4444",
                                                    fontSize: "13px",
                                                    marginBottom: "14px",
                                                    lineHeight: "1.5",
                                                }}
                                            >
                                                {errorMsg ||
                                                    "Cổng VNPay tự động (Port 5004) đang ngoại tuyến hoặc không thể kết nối."}
                                            </p>
                                            <div
                                                style={{
                                                    display: "flex",
                                                    gap: "8px",
                                                    justifyContent: "center",
                                                    flexWrap: "wrap",
                                                }}
                                            >
                                                <button
                                                    type="button"
                                                    className="staff-btn staff-btn-secondary"
                                                    onClick={
                                                        handleOpenVnPayModal
                                                    }
                                                >
                                                    Thử tạo lại mã VNPay
                                                </button>
                                                <button
                                                    type="button"
                                                    className="staff-btn staff-btn-primary"
                                                    onClick={() => {
                                                        setPaymentModal("Cash");
                                                        setErrorMsg("");
                                                    }}
                                                >
                                                    Chuyển sang thu tiền mặt tại
                                                    quầy
                                                </button>
                                            </div>
                                        </div>
                                    )}

                                    <div className="staff-modal-action-bar">
                                        <button
                                            type="button"
                                            className="staff-btn staff-btn-secondary"
                                            onClick={() =>
                                                setPaymentModal(null)
                                            }
                                        >
                                            {t("cancel")}
                                        </button>
                                        <button
                                            type="button"
                                            className="staff-btn staff-btn-primary staff-btn-flex"
                                            disabled={
                                                isCheckingVnPay || !vnPayTxId
                                            }
                                            onClick={handleCheckVnPayPayment}
                                        >
                                            {isCheckingVnPay ? (
                                                <>
                                                    <RefreshCw
                                                        size={14}
                                                        className="staff-spin"
                                                    />
                                                    <span>
                                                        Đang kiểm tra...
                                                    </span>
                                                </>
                                            ) : (
                                                <span>
                                                    Kiểm tra trạng thái thanh
                                                    toán
                                                </span>
                                            )}
                                        </button>
                                    </div>
                                </div>
                            )}
                        </div>
                    </div>
                </div>
            )}
            {/* V6-UI: Modal nhập lý do cấp lại vé QR */}
            {reissueModalOpen && (
                <div
                    className="staff-modal-backdrop"
                    onClick={() => !reissueLoading && setReissueModalOpen(false)}
                >
                    <div
                        className="staff-modal-card staff-reissue-modal"
                        onClick={(e) => e.stopPropagation()}
                        role="dialog"
                        aria-modal="true"
                        aria-labelledby="reissue-modal-title"
                        style={{ maxWidth: 440 }}
                    >
                        <div className="staff-modal-head" style={{ padding: "16px 20px" }}>
                            <div className="staff-ticket-head-titles">
                                <h3 id="reissue-modal-title" style={{ fontSize: 16, fontWeight: 700 }}>
                                    Cấp lại vé QR mới
                                </h3>
                                <span className="staff-ticket-head-sub" style={{ fontSize: 12, color: "var(--pw-admin-muted)" }}>
                                    Hủy vé cũ và xuất vé thay thế cho phiên đỗ
                                </span>
                            </div>
                            <button
                                type="button"
                                className="staff-modal-close"
                                disabled={reissueLoading}
                                onClick={() => setReissueModalOpen(false)}
                                aria-label="Đóng hộp thoại"
                            >
                                <X size={16} strokeWidth={2.2} />
                            </button>
                        </div>
                        <div className="staff-modal-body" style={{ padding: "16px 20px" }}>
                            <p style={{ fontSize: 13, color: "var(--pw-admin-muted)", marginBottom: 14, lineHeight: 1.5 }}>
                                Lưu ý: Mã QR cũ sẽ bị vô hiệu hóa ngay lập tức. Khách hàng bắt buộc xuất trình vé mới tại cổng ra. Bắt buộc ghi nhận lý do kiểm toán hệ thống.
                            </p>
                            <div className="staff-form-group">
                                <label
                                    htmlFor="reissue-reason-input"
                                    style={{ fontWeight: 600, fontSize: 13, display: "block", marginBottom: 6 }}
                                >
                                    Lý do cấp lại vé (tối đa 500 ký tự) *
                                </label>
                                <textarea
                                    id="reissue-reason-input"
                                    className="staff-input font-sans"
                                    rows={3}
                                    maxLength={500}
                                    style={{ width: "100%", resize: "vertical" }}
                                    value={reissueReason}
                                    onChange={(e) => setReissueReason(e.target.value)}
                                    placeholder="Nhập lý do cấp lại vé..."
                                />
                                <div style={{ fontSize: 11, color: "var(--pw-admin-muted)", textAlign: "right", marginTop: 4 }}>
                                    {reissueReason.length}/500 ký tự
                                </div>
                            </div>
                        </div>
                        <div
                            className="staff-modal-actions"
                            style={{ display: "flex", justifyContent: "flex-end", gap: 10, padding: "12px 20px 16px" }}
                        >
                            <button
                                type="button"
                                className="staff-btn staff-btn-secondary"
                                disabled={reissueLoading}
                                onClick={() => setReissueModalOpen(false)}
                            >
                                Hủy bỏ
                            </button>
                            <button
                                type="button"
                                className="staff-btn staff-btn-primary"
                                disabled={reissueLoading || !reissueReason.trim() || reissueReason.length > 500}
                                onClick={handleConfirmReissueQr}
                            >
                                {reissueLoading ? "Đang xử lý..." : "Xác nhận & Cấp vé"}
                            </button>
                        </div>
                    </div>
                </div>
            )}

            {/* Q-EXIT-WINDOW: Modal nhập lý do gia hạn 15 phút xuất bãi */}
            {extendModalOpen && (
                <div
                    className="staff-modal-backdrop"
                    onClick={() => !extendLoading && setExtendModalOpen(false)}
                >
                    <div
                        className="staff-modal-card staff-reissue-modal"
                        onClick={(e) => e.stopPropagation()}
                        role="dialog"
                        aria-modal="true"
                        aria-labelledby="extend-modal-title"
                        style={{ maxWidth: 440 }}
                    >
                        <div
                            className="staff-modal-head"
                            style={{ padding: "16px 20px" }}
                        >
                            <div className="staff-ticket-head-titles">
                                <h3
                                    id="extend-modal-title"
                                    style={{ fontSize: 16, fontWeight: 700 }}
                                >
                                    Gia hạn 15 phút xuất bãi
                                </h3>
                                <span
                                    className="staff-ticket-head-sub"
                                    style={{
                                        fontSize: 12,
                                        color: "var(--pw-admin-muted)",
                                    }}
                                >
                                    Gia hạn một lần duy nhất cho phiên đỗ xe đã thanh toán
                                </span>
                            </div>
                            <button
                                type="button"
                                className="staff-modal-close"
                                disabled={extendLoading}
                                onClick={() => setExtendModalOpen(false)}
                                aria-label="Đóng hộp thoại"
                            >
                                <X size={16} strokeWidth={2.2} />
                            </button>
                        </div>
                        <div
                            className="staff-modal-body"
                            style={{ padding: "16px 20px" }}
                        >
                            <p
                                style={{
                                    fontSize: 13,
                                    color: "var(--pw-admin-muted)",
                                    marginBottom: 14,
                                    lineHeight: 1.5,
                                }}
                            >
                                Lưu ý: Thời gian xuất bãi sẽ được cộng thêm 15 phút. Bắt buộc ghi nhận lý do kiểm toán hệ thống khi bãi có sự cố hoặc ùn ứ.
                            </p>
                            <div className="staff-form-group">
                                <label
                                    htmlFor="extend-reason-input"
                                    style={{
                                        fontWeight: 600,
                                        fontSize: 13,
                                        display: "block",
                                        marginBottom: 6,
                                    }}
                                >
                                    Lý do gia hạn (tối đa 500 ký tự) *
                                </label>
                                <textarea
                                    id="extend-reason-input"
                                    className="staff-input font-sans"
                                    rows={3}
                                    maxLength={500}
                                    style={{
                                        width: "100%",
                                        resize: "vertical",
                                    }}
                                    value={extendReason}
                                    onChange={(e) =>
                                        setExtendReason(e.target.value)
                                    }
                                    placeholder="Nhập lý do gia hạn..."
                                />
                                <div
                                    style={{
                                        fontSize: 11,
                                        color: "var(--pw-admin-muted)",
                                        textAlign: "right",
                                        marginTop: 4,
                                    }}
                                >
                                    {extendReason.length}/500 ký tự
                                </div>
                            </div>
                        </div>
                        <div
                            className="staff-modal-actions"
                            style={{
                                display: "flex",
                                justifyContent: "flex-end",
                                gap: 10,
                                padding: "12px 20px 16px",
                            }}
                        >
                            <button
                                type="button"
                                className="staff-btn staff-btn-secondary"
                                disabled={extendLoading}
                                onClick={() => setExtendModalOpen(false)}
                            >
                                Hủy bỏ
                            </button>
                            <button
                                type="button"
                                className="staff-btn staff-btn-primary"
                                disabled={
                                    extendLoading ||
                                    !extendReason.trim() ||
                                    extendReason.length > 500
                                }
                                onClick={handleConfirmExtendExitWindow}
                            >
                                {extendLoading
                                    ? "Đang xử lý..."
                                    : "Xác nhận gia hạn"}
                            </button>
                        </div>
                    </div>
                </div>
            )}

            {/* V6-UI: Màn hình vé in thay thế */}
            {reissuedTicketData && (
                <QrCodeModal
                    qrToken={reissuedTicketData.qrToken}
                    plateNumber={reissuedTicketData.plateNumber}
                    entryTime={reissuedTicketData.entryTime}
                    vehicleType={scannedSession?.vehicleType || "Car"}
                    onClose={() => setReissuedTicketData(null)}
                />
            )}
        </div>
    );
}
