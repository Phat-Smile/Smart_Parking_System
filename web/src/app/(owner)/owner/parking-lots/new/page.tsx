"use client";

import React, { useState } from "react";
import Link from "next/link";
import dynamic from "next/dynamic";
import { PageHeader } from "../../../../../components/layout/PageHeader";
import { Plan2D } from "../../../../../components/viz/Plan2D";
import { AP, vnd, ST, K } from "../../../../../types/owner";

const ParkingScene3DViewer = dynamic(
    () => import("../../../../../components/viz/ParkingScene3DViewer"),
    { ssr: false },
);

import {
    registerParkingLotApi,
    confirmOwnerLotEntrance,
    ParkingLotTypeEnum,
} from "@/features/owner/api/ownerApi";
import { OwnerLocationPicker } from "@/features/owner/components/OwnerLocationPicker";
import { useLotInfoStore } from "@/features/owner/lots/lotInfo";
import { ApiError } from "@/shared/errors/api-error";
import { loadOwnerLots, localLotId } from "@/features/owner/lots/ownerLotsSync";
import { saveInitialLotSetup } from "@/features/owner/lots/initialLotSetup";
import { CheckCircle2, AlertCircle, Loader2 } from "lucide-react";

interface WizardData {
    name: string;
    addr: string;
    latitude: number | null;
    longitude: number | null;
    confirmedOnMap: boolean;
    op: string;
    cl2: string;
    vc: boolean;
    vm: boolean;
    cap: number;
    pc: number;
    pm: number;
    dep: number;
    hold: number;
    type: ParkingLotTypeEnum;
    floors: number;
    rowsPerFloor: number;
    features: {
        supportsElectricVehicles: boolean;
        supportsLargeVehicles: boolean;
        hasEvCharging: boolean;
        isCovered: boolean;
        hasCctv: boolean;
    };
}

const INITIAL_DATA: WizardData = {
    name: "",
    addr: "",
    latitude: null,
    longitude: null,
    confirmedOnMap: false,
    op: "06:00",
    cl2: "22:00",
    vc: true,
    vm: true,
    cap: 500,
    pc: 30000,
    pm: 5000,
    dep: 20000,
    hold: 15,
    type: "MultiStorey",
    floors: 5,
    rowsPerFloor: 4,
    features: {
        supportsElectricVehicles: false,
        supportsLargeVehicles: false,
        hasEvCharging: false,
        isCovered: false,
        hasCctv: false,
    },
};

export default function NewParkingLotPage() {
    const [step, setStep] = useState(0);
    const [data, setData] = useState<WizardData>(INITIAL_DATA);
    const [errors, setErrors] = useState<string[]>([]);
    const [previewMode, setPreviewMode] = useState<"3d" | "2d">("3d");
    const [confirming, setConfirming] = useState(false);
    const [isSubmitting, setIsSubmitting] = useState(false);
    const [done, setDone] = useState(false);

    // State quản lý token xác nhận cổng vào (WP-OW-1 / PR #278 / BE2 Spec)
    const [locationConfirmationToken, setLocationConfirmationToken] = useState<
        string | null
    >(null);
    const [tokenExpiresAt, setTokenExpiresAt] = useState<string | null>(null);
    const [isConfirmingLocation, setIsConfirmingLocation] = useState(false);
    const [locationConfirmError, setLocationConfirmError] = useState<
        string | null
    >(null);

    const handleConfirmEntrance = async () => {
        setLocationConfirmError(null);
        const trimmedAddr = data.addr.trim();
        if (!trimmedAddr || trimmedAddr.length < 3) {
            setLocationConfirmError(
                "Địa chỉ cần ít nhất 3 ký tự trước khi xác nhận cổng vào.",
            );
            return;
        }
        if (trimmedAddr.length > 500) {
            setLocationConfirmError("Địa chỉ không được vượt quá 500 ký tự.");
            return;
        }
        if (data.latitude === null || data.longitude === null) {
            setLocationConfirmError(
                "Vui lòng chạm bản đồ hoặc kéo ghim để chọn cổng vào trước khi xác nhận.",
            );
            return;
        }
        if (data.latitude === 0 && data.longitude === 0) {
            setLocationConfirmError(
                "Tọa độ (0, 0) không hợp lệ. Vui lòng chọn vị trí cổng vào thực tế.",
            );
            return;
        }

        setIsConfirmingLocation(true);
        try {
            const res = await confirmOwnerLotEntrance({
                address: trimmedAddr,
                latitude: data.latitude,
                longitude: data.longitude,
                confirmedOnMap: true,
            });
            setLocationConfirmationToken(res.locationConfirmationToken);
            setTokenExpiresAt(res.expiresAtUtc);
            setData((prev) => ({ ...prev, confirmedOnMap: true }));
        } catch (err: unknown) {
            if (err instanceof ApiError) {
                if (err.status === 400) {
                    setLocationConfirmError(
                        "Dữ liệu vị trí không hợp lệ. Vui lòng kiểm tra lại địa chỉ và ghim cổng vào.",
                    );
                    return;
                }
                if (err.status === 401 || err.status === 403) {
                    setLocationConfirmError(
                        "Phiên làm việc đã hết hạn hoặc tài khoản không có quyền Chủ bãi.",
                    );
                    return;
                }
            }
            setLocationConfirmError(
                "Không thể kết nối đến máy chủ để xác nhận cổng vào. Vui lòng kiểm tra mạng và thử lại.",
            );
        } finally {
            setIsConfirmingLocation(false);
        }
    };

    const validate = () => {
        const errs: string[] = [];
        if (!data.name.trim()) errs.push("Nhập tên bãi");
        if (!data.addr.trim()) errs.push("Nhập địa chỉ");
        if (
            data.latitude === null ||
            data.longitude === null ||
            (data.latitude === 0 && data.longitude === 0)
        ) {
            errs.push("Vui lòng chọn vị trí cổng vào trên bản đồ");
        } else if (!locationConfirmationToken) {
            errs.push(
                "Vui lòng nhấn 'Xác nhận vị trí cổng vào' trên bản đồ trước khi tiếp tục.",
            );
        } else if (tokenExpiresAt && new Date(tokenExpiresAt) <= new Date()) {
            errs.push(
                "Xác nhận vị trí cổng vào đã hết hạn. Vui lòng xác nhận lại.",
            );
        }
        if (!(data.cap > 0)) errs.push("Sức chứa phải lớn hơn 0");
        if (data.type !== "Outdoor" && !(data.floors > 0))
            errs.push("Số tầng phải lớn hơn 0");
        if (!(data.rowsPerFloor > 0))
            errs.push("Số dãy mỗi tầng phải lớn hơn 0");
        if (!data.vc && !data.vm) errs.push("Chọn ít nhất một loại xe");
        return errs;
    };

    const handleNext = () => {
        if (step === 0) {
            const errs = validate();
            if (errs.length > 0) {
                setErrors(errs);
                return;
            }
            setErrors([]);
            setStep(1);
        } else if (step === 1) {
            setStep(2);
        } else if (step === 2) {
            setConfirming(true);
        }
    };

    const handleBack = () => {
        if (confirming) {
            setConfirming(false);
            return;
        }
        if (step > 0) {
            setStep(step - 1);
        }
    };

    const [createdLotId, setCreatedLotId] = useState<number>(1);
    const [unsaved, setUnsaved] = useState<string[]>([]);

    const handleConfirmSend = async () => {
        if (isSubmitting) return;
        setErrors([]);
        if (!locationConfirmationToken) {
            setErrors([
                "Vui lòng nhấn 'Xác nhận vị trí cổng vào' trên bản đồ trước khi gửi duyệt.",
            ]);
            setConfirming(false);
            setStep(0);
            return;
        }
        if (tokenExpiresAt && new Date(tokenExpiresAt) <= new Date()) {
            setErrors([
                "Xác nhận vị trí cổng vào đã hết hạn. Vui lòng xác nhận lại vị trí trước khi gửi duyệt.",
            ]);
            setConfirming(false);
            setStep(0);
            return;
        }

        setIsSubmitting(true);
        try {
            const res = await registerParkingLotApi({
                name: data.name,
                address: data.addr.trim(),
                latitude: data.latitude!,
                longitude: data.longitude!,
                type: data.type,
                features: data.features,
                locationConfirmationToken: locationConfirmationToken,
            });

            // Hours, prices and deposit are separate calls; the lot already exists, so a failure here
            // is reported on the result screen instead of undoing the registration.
            setUnsaved(
                await saveInitialLotSetup(res.id, {
                    openAt: data.op,
                    closeAt: data.cl2,
                    acceptsCars: data.vc,
                    acceptsMotorbikes: data.vm,
                    carPricePerHour: data.pc,
                    motorbikePricePerHour: data.pm,
                    depositVnd: data.dep,
                    capacity: data.cap,
                    floors: data.type === "Outdoor" ? 1 : data.floors,
                    rowsPerFloor: data.rowsPerFloor,
                }),
            );
            await loadOwnerLots();
            const registeredId = localLotId(res.id);
            setCreatedLotId(registeredId);
            useLotInfoStore.getState().setLotInfo(registeredId, {
                submittedAt: new Date().toISOString().split("T")[0],
            });
            setDone(true);
        } catch (err: unknown) {
            setConfirming(false);
            if (err instanceof ApiError && err.status === 409) {
                setErrors([
                    "Tên bãi đỗ xe này đã được bạn đăng ký trước đó. Vui lòng chọn tên khác (Mã lỗi: parking.lot_duplicate).",
                ]);
                setStep(0);
            } else if (err instanceof ApiError && err.status === 400) {
                const isTokenError = Boolean(
                    err.problem?.errors?.locationConfirmationToken ||
                    err.problem?.validationErrors?.locationConfirmationToken ||
                    JSON.stringify(err.problem || "").includes(
                        "locationConfirmationToken",
                    ),
                );
                if (isTokenError) {
                    setLocationConfirmationToken(null);
                    setTokenExpiresAt(null);
                    setData((prev) => ({ ...prev, confirmedOnMap: false }));
                    setErrors([
                        "Xác nhận vị trí không khớp hoặc đã hết hạn. Vui lòng quay lại Bước 1 và nhấn 'Xác nhận vị trí cổng vào'.",
                    ]);
                    setStep(0);
                    return;
                }
                setErrors([
                    err.problem?.detail ||
                        "Thông tin đăng ký bãi đỗ xe chưa đúng định dạng.",
                ]);
                setStep(0);
            } else if (
                err instanceof ApiError &&
                (err.status === 401 || err.status === 403)
            ) {
                setErrors([
                    "Tài khoản của bạn chưa có quyền Chủ bãi (ParkingLotOwner) để thực hiện thao tác này.",
                ]);
                setStep(0);
            } else {
                setErrors([
                    "Đã xảy ra lỗi khi tạo bãi đỗ xe. Vui lòng thử lại sau.",
                ]);
            }
        } finally {
            setIsSubmitting(false);
        }
    };

    if (done) {
        return (
            <>
                <PageHeader
                    breadcrumb="/owner/parking-lots/new"
                    title="Đăng ký bãi mới"
                    subtitle="Gửi duyệt thành công"
                />
                <div className="ok">
                    <h3 style={{ marginTop: 0 }}>
                        Đã gửi duyệt bãi xe thành công!
                    </h3>
                    <p>
                        Bãi &quot;{data.name}&quot; đã được tiếp nhận và đang ở
                        trạng thái{" "}
                        <span
                            className="tag"
                            style={
                                { "--c": AP.pending[1] } as React.CSSProperties
                            }
                        >
                            {AP.pending[0]}
                        </span>
                        . Ban quản trị hệ thống sẽ tiến hành kiểm duyệt vị trí
                        và thông số kỹ thuật.
                    </p>
                    {unsaved.length > 0 && (
                        <div className="err">
                            Bãi đã được tạo nhưng chưa lưu được:{" "}
                            {unsaved.join(", ")}. Hãy cấu hình lại ở các trang
                            Giờ hoạt động, Bảng giá và Sơ đồ.
                        </div>
                    )}
                    <p>
                        Sơ đồ ô đỗ được dựng sẵn từ số tầng, số dãy và sức chứa;
                        bạn có thể chỉnh trong trình chỉnh sửa sơ đồ.
                    </p>
                    <div
                        style={{
                            display: "flex",
                            gap: "10px",
                            marginTop: "16px",
                            flexWrap: "wrap",
                        }}
                    >
                        <Link
                            href={`/owner/lots/${createdLotId}/pricing`}
                            className="btn p"
                        >
                            Cấu hình bảng giá cho bãi này →
                        </Link>
                        <Link href="/owner/parking-lots" className="btn">
                            Về danh sách bãi của tôi
                        </Link>
                    </div>
                </div>
            </>
        );
    }

    const steps = ["Cấu hình", "Xem trước", "Gửi duyệt"];

    return (
        <>
            <PageHeader
                breadcrumb="/owner/parking-lots/new"
                title="Đăng ký bãi mới"
                subtitle="Bản nháp → Xem lại → Gửi → Chờ duyệt"
            />

            <div className="steps">
                {steps.map((s, i) => (
                    <span key={i} className={i === step ? "on" : ""}>
                        {i + 1}. {s}
                    </span>
                ))}
            </div>

            {/* Step 0: Cấu hình */}
            {step === 0 && (
                <>
                    {errors.length > 0 && (
                        <div className="err">
                            {errors.map((e, idx) => (
                                <div key={idx}>• {e}</div>
                            ))}
                        </div>
                    )}

                    <div className="card">
                        <div className="form">
                            <label className="f">
                                <span>Tên bãi</span>
                                <input
                                    type="text"
                                    value={data.name}
                                    onChange={(e) =>
                                        setData({
                                            ...data,
                                            name: e.target.value,
                                        })
                                    }
                                />
                            </label>

                            <div className="f">
                                <span className="block mb-1 font-medium text-sm text-gray-700">
                                    Địa chỉ & Vị trí cổng vào trên bản đồ
                                </span>
                                <OwnerLocationPicker
                                    address={data.addr}
                                    latitude={data.latitude}
                                    longitude={data.longitude}
                                    confirmedOnMap={data.confirmedOnMap}
                                    onChange={(loc) => {
                                        // Nếu địa chỉ hoặc tọa độ thay đổi, hủy token xác nhận cũ
                                        if (
                                            loc.address !== data.addr ||
                                            loc.latitude !== data.latitude ||
                                            loc.longitude !== data.longitude
                                        ) {
                                            setLocationConfirmationToken(null);
                                            setTokenExpiresAt(null);
                                            setLocationConfirmError(null);
                                        }
                                        setData({
                                            ...data,
                                            addr: loc.address,
                                            latitude: loc.latitude,
                                            longitude: loc.longitude,
                                            confirmedOnMap: loc.confirmedOnMap,
                                        });
                                    }}
                                />

                                {/* Nút và trạng thái xác nhận cổng vào (BE2 Spec Mục 3.1 & 3.2) */}
                                <div className="mt-3 p-3 bg-gray-50 border rounded-xl flex flex-col sm:flex-row items-start sm:items-center justify-between gap-3">
                                    <div>
                                        <div className="text-xs font-semibold text-gray-700">
                                            Xác nhận vị trí cổng vào bãi xe:
                                        </div>
                                        {locationConfirmationToken &&
                                        tokenExpiresAt ? (
                                            <div
                                                className="flex items-center gap-1.5 text-xs text-emerald-700 font-medium mt-0.5"
                                                data-testid="confirmed-token-badge"
                                            >
                                                <CheckCircle2
                                                    size={14}
                                                    className="text-emerald-600"
                                                />
                                                <span>
                                                    Đã xác nhận cổng vào (hiệu
                                                    lực 15 phút)
                                                </span>
                                            </div>
                                        ) : (
                                            <div className="text-xs text-gray-500 mt-0.5">
                                                Hãy chọn đúng vị trí xe đi vào
                                                bãi và bấm nút xác nhận bên
                                                cạnh.
                                            </div>
                                        )}
                                        {locationConfirmError && (
                                            <div
                                                className="text-xs text-red-600 mt-1 flex items-center gap-1"
                                                role="alert"
                                                data-testid="location-confirm-error"
                                            >
                                                <AlertCircle size={13} />
                                                <span>
                                                    {locationConfirmError}
                                                </span>
                                            </div>
                                        )}
                                    </div>
                                    <button
                                        type="button"
                                        data-testid="btn-confirm-entrance"
                                        disabled={
                                            isConfirmingLocation ||
                                            !data.addr.trim() ||
                                            data.addr.trim().length < 3 ||
                                            data.latitude === null ||
                                            data.longitude === null ||
                                            (data.latitude === 0 &&
                                                data.longitude === 0)
                                        }
                                        onClick={handleConfirmEntrance}
                                        className="shrink-0 px-3.5 py-2 bg-emerald-600 text-white rounded-lg text-xs font-medium hover:bg-emerald-700 disabled:bg-gray-300 disabled:cursor-not-allowed transition-colors flex items-center gap-1.5"
                                    >
                                        {isConfirmingLocation ? (
                                            <>
                                                <Loader2
                                                    size={14}
                                                    className="animate-spin"
                                                />
                                                <span>Đang xác nhận...</span>
                                            </>
                                        ) : locationConfirmationToken ? (
                                            <>
                                                <CheckCircle2 size={14} />
                                                <span>Xác nhận lại vị trí</span>
                                            </>
                                        ) : (
                                            <>
                                                <CheckCircle2 size={14} />
                                                <span>
                                                    Xác nhận vị trí cổng vào
                                                </span>
                                            </>
                                        )}
                                    </button>
                                </div>
                            </div>

                            <label className="f">
                                <span>Giờ mở cửa</span>
                                <input
                                    type="time"
                                    value={data.op}
                                    onChange={(e) =>
                                        setData({ ...data, op: e.target.value })
                                    }
                                />
                            </label>

                            <label className="f">
                                <span>Giờ đóng cửa</span>
                                <input
                                    type="time"
                                    value={data.cl2}
                                    onChange={(e) =>
                                        setData({
                                            ...data,
                                            cl2: e.target.value,
                                        })
                                    }
                                />
                            </label>

                            <label className="f">
                                <span>Sức chứa tổng</span>
                                <input
                                    type="number"
                                    value={data.cap}
                                    onChange={(e) =>
                                        setData({
                                            ...data,
                                            cap: Number(e.target.value),
                                        })
                                    }
                                />
                            </label>

                            <label className="f">
                                <span>Giá ô tô (đ/giờ)</span>
                                <input
                                    type="number"
                                    value={data.pc}
                                    onChange={(e) =>
                                        setData({
                                            ...data,
                                            pc: Number(e.target.value),
                                        })
                                    }
                                />
                            </label>

                            <label className="f">
                                <span>Giá xe máy (đ/giờ)</span>
                                <input
                                    type="number"
                                    value={data.pm}
                                    onChange={(e) =>
                                        setData({
                                            ...data,
                                            pm: Number(e.target.value),
                                        })
                                    }
                                />
                            </label>

                            <label className="f">
                                <span>Tiền cọc giữ chỗ (đ)</span>
                                <input
                                    type="number"
                                    value={data.dep}
                                    onChange={(e) =>
                                        setData({
                                            ...data,
                                            dep: Number(e.target.value),
                                        })
                                    }
                                />
                            </label>

                            <label className="f">
                                <span>Chính sách đặt trước</span>
                                <select
                                    value={data.hold}
                                    onChange={(e) =>
                                        setData({
                                            ...data,
                                            hold: Number(e.target.value),
                                        })
                                    }
                                >
                                    <option value={0}>
                                        Không cho đặt trước
                                    </option>
                                    <option value={15}>Giữ chỗ 15 phút</option>
                                    <option value={30}>Giữ chỗ 30 phút</option>
                                </select>
                            </label>

                            <label className="f">
                                <span>Loại bãi xe</span>
                                <select
                                    value={data.type}
                                    onChange={(e) => {
                                        const newType = e.target
                                            .value as ParkingLotTypeEnum;
                                        const newFloors =
                                            newType === "Outdoor"
                                                ? 1
                                                : data.floors > 1
                                                  ? data.floors
                                                  : 3;
                                        setData({
                                            ...data,
                                            type: newType,
                                            floors: newFloors,
                                        });
                                    }}
                                >
                                    <option value="MultiStorey">
                                        Nhà xe cao tầng (MultiStorey)
                                    </option>
                                    <option value="Underground">
                                        Hầm xe (Underground)
                                    </option>
                                    <option value="Outdoor">
                                        Ngoài trời (Outdoor)
                                    </option>
                                </select>
                            </label>

                            <label className="f">
                                <span>Số tầng</span>
                                <input
                                    type="number"
                                    min={1}
                                    max={30}
                                    disabled={data.type === "Outdoor"}
                                    value={
                                        data.type === "Outdoor"
                                            ? 1
                                            : data.floors
                                    }
                                    onChange={(e) =>
                                        setData({
                                            ...data,
                                            floors: Math.max(
                                                1,
                                                Number(e.target.value),
                                            ),
                                        })
                                    }
                                />
                            </label>

                            <label className="f">
                                <span>Số dãy mỗi tầng</span>
                                <input
                                    type="number"
                                    min={1}
                                    max={20}
                                    value={data.rowsPerFloor}
                                    onChange={(e) =>
                                        setData({
                                            ...data,
                                            rowsPerFloor: Math.max(
                                                1,
                                                Number(e.target.value),
                                            ),
                                        })
                                    }
                                />
                            </label>
                        </div>

                        {/* Tiện ích & Đặc tính bãi xe (US-MAP-005, BR-Features) */}
                        <div
                            style={{
                                marginTop: 24,
                                paddingTop: 20,
                                borderTop: "1px solid var(--line)",
                            }}
                        >
                            <div style={{ marginBottom: 12 }}>
                                <strong
                                    style={{
                                        fontSize: 14,
                                        color: "var(--ink)",
                                    }}
                                >
                                    Tiện ích & Đặc tính bãi đỗ (Parking Lot
                                    Features)
                                </strong>
                                <p
                                    style={{
                                        fontSize: 12,
                                        color: "var(--muted)",
                                        margin: "4px 0 0",
                                    }}
                                >
                                    Các đặc tính này sẽ hiển thị trên bộ lọc tìm
                                    kiếm cho khách hàng sau khi bãi được duyệt.
                                </p>
                            </div>
                            <div
                                style={{
                                    display: "grid",
                                    gridTemplateColumns:
                                        "repeat(auto-fit, minmax(240px, 1fr))",
                                    gap: 12,
                                }}
                            >
                                {[
                                    {
                                        key: "supportsElectricVehicles" as const,
                                        title: "Hỗ trợ xe điện (EV)",
                                        desc: "Tiếp nhận và có vị trí đỗ dành cho xe điện",
                                    },
                                    {
                                        key: "hasEvCharging" as const,
                                        title: "Trạm sạc xe điện (EV Charging)",
                                        desc: "Có sẵn trụ sạc tại chỗ cho xe điện",
                                    },
                                    {
                                        key: "hasCctv" as const,
                                        title: "Camera an ninh (CCTV)",
                                        desc: "Giám sát 24/7 toàn bộ khu vực bãi",
                                    },
                                    {
                                        key: "isCovered" as const,
                                        title: "Có mái che / Trong nhà",
                                        desc: "Bảo vệ xe tránh mưa nắng",
                                    },
                                    {
                                        key: "supportsLargeVehicles" as const,
                                        title: "Xe kích thước lớn / SUV",
                                        desc: "Có ô đỗ rộng cho xe bán tải, SUV cỡ lớn",
                                    },
                                ].map((item) => {
                                    const checked = Boolean(
                                        data.features?.[item.key],
                                    );
                                    return (
                                        <label
                                            key={item.key}
                                            style={{
                                                display: "flex",
                                                alignItems: "flex-start",
                                                gap: 10,
                                                padding: "10px 12px",
                                                borderRadius: 10,
                                                border: checked
                                                    ? "1.5px solid #0284c7"
                                                    : "1px solid var(--line)",
                                                background: checked
                                                    ? "#f0f9ff"
                                                    : "var(--card)",
                                                cursor: "pointer",
                                                transition: "all 0.15s ease",
                                            }}
                                        >
                                            <input
                                                type="checkbox"
                                                checked={checked}
                                                onChange={(e) =>
                                                    setData({
                                                        ...data,
                                                        features: {
                                                            ...data.features,
                                                            [item.key]:
                                                                e.target
                                                                    .checked,
                                                        },
                                                    })
                                                }
                                                style={{ marginTop: 3 }}
                                            />
                                            <div>
                                                <div
                                                    style={{
                                                        fontSize: 13,
                                                        fontWeight: 700,
                                                        color: "var(--ink)",
                                                    }}
                                                >
                                                    {item.title}
                                                </div>
                                                <div
                                                    style={{
                                                        fontSize: 11,
                                                        color: "var(--muted)",
                                                        marginTop: 2,
                                                    }}
                                                >
                                                    {item.desc}
                                                </div>
                                            </div>
                                        </label>
                                    );
                                })}
                            </div>
                        </div>

                        <div className="form" style={{ marginTop: "14px" }}>
                            <label className="f ck">
                                <input
                                    type="checkbox"
                                    checked={data.vc}
                                    onChange={(e) =>
                                        setData({
                                            ...data,
                                            vc: e.target.checked,
                                        })
                                    }
                                />
                                Nhận ô tô
                            </label>
                            <label className="f ck">
                                <input
                                    type="checkbox"
                                    checked={data.vm}
                                    onChange={(e) =>
                                        setData({
                                            ...data,
                                            vm: e.target.checked,
                                        })
                                    }
                                />
                                Nhận xe máy
                            </label>
                        </div>
                    </div>
                </>
            )}

            {/* Step 1: Xem trước */}
            {step === 1 && (
                <>
                    <div style={{ marginBottom: "12px" }}>
                        <div className="segs">
                            <button
                                className={previewMode === "3d" ? "on" : ""}
                                onClick={() => setPreviewMode("3d")}
                            >
                                3D
                            </button>
                            <button
                                className={previewMode === "2d" ? "on" : ""}
                                onClick={() => setPreviewMode("2d")}
                            >
                                2D
                            </button>
                        </div>
                    </div>

                    <div className="card">
                        {previewMode === "3d" ? (
                            <ParkingScene3DViewer />
                        ) : (
                            <Plan2D />
                        )}
                        <div className="lgd">
                            {K.map((k) => (
                                <span key={k}>
                                    <i style={{ background: ST[k][1] }} />
                                    {ST[k][0]}
                                </span>
                            ))}
                        </div>
                    </div>
                    <p className="mut" style={{ marginTop: "8px" }}>
                        Bố cục xem trước theo mẫu bạn đã chọn. Kiểm tra rồi tiếp
                        tục.
                    </p>
                </>
            )}

            {/* Step 2: Gửi duyệt */}
            {step === 2 && (
                <>
                    <div className="card">
                        <h3 style={{ marginTop: 0 }}>Kiểm tra thông tin</h3>
                        <div className="tw">
                            <table>
                                <thead>
                                    <tr>
                                        <th>Mục</th>
                                        <th>Giá trị</th>
                                    </tr>
                                </thead>
                                <tbody>
                                    <tr>
                                        <td>Tên bãi</td>
                                        <td>{data.name}</td>
                                    </tr>
                                    <tr>
                                        <td>Địa chỉ</td>
                                        <td>{data.addr}</td>
                                    </tr>
                                    <tr>
                                        <td>Tọa độ cổng vào</td>
                                        <td>
                                            {data.latitude !== null &&
                                            data.longitude !== null
                                                ? `${data.latitude.toFixed(6)}, ${data.longitude.toFixed(6)} (Đã xác nhận)`
                                                : "Chưa xác nhận"}
                                        </td>
                                    </tr>
                                    <tr>
                                        <td>Giờ hoạt động</td>
                                        <td>
                                            {data.op} – {data.cl2}
                                        </td>
                                    </tr>
                                    <tr>
                                        <td>Loại bãi xe</td>
                                        <td>
                                            {data.type === "MultiStorey"
                                                ? "Nhà xe cao tầng (MultiStorey)"
                                                : data.type === "Underground"
                                                  ? "Hầm xe (Underground)"
                                                  : "Ngoài trời (Outdoor)"}
                                        </td>
                                    </tr>
                                    <tr>
                                        <td>Mẫu sơ đồ</td>
                                        <td>
                                            {data.type === "Outdoor"
                                                ? "1 tầng (Ngoài trời)"
                                                : `${data.floors} tầng`}{" "}
                                            · {data.rowsPerFloor} dãy/tầng (
                                            {data.cap} ô đỗ)
                                        </td>
                                    </tr>
                                    <tr>
                                        <td>Loại xe</td>
                                        <td>
                                            {[
                                                data.vc ? "Ô tô" : "",
                                                data.vm ? "Xe máy" : "",
                                            ]
                                                .filter(Boolean)
                                                .join(", ")}
                                        </td>
                                    </tr>
                                    <tr>
                                        <td>Sức chứa</td>
                                        <td>{data.cap} chỗ</td>
                                    </tr>
                                    <tr>
                                        <td>Giá</td>
                                        <td>
                                            {vnd(data.pc)} / giờ (ô tô),{" "}
                                            {vnd(data.pm)} / giờ (xe máy)
                                        </td>
                                    </tr>
                                    <tr>
                                        <td>Tiền cọc</td>
                                        <td>{vnd(data.dep)}</td>
                                    </tr>
                                    <tr>
                                        <td>Đặt trước</td>
                                        <td>
                                            {data.hold > 0
                                                ? `Giữ chỗ ${data.hold} phút`
                                                : "Không"}
                                        </td>
                                    </tr>
                                    <tr>
                                        <td>Tiện ích khai báo</td>
                                        <td>
                                            {Object.values(
                                                data.features || {},
                                            ).some(Boolean) ? (
                                                <div
                                                    style={{
                                                        display: "flex",
                                                        gap: 6,
                                                        flexWrap: "wrap",
                                                    }}
                                                >
                                                    {data.features
                                                        ?.supportsElectricVehicles && (
                                                        <span
                                                            className="pw-badge"
                                                            style={{
                                                                background:
                                                                    "#e0f2fe",
                                                                color: "#0369a1",
                                                                padding:
                                                                    "2px 8px",
                                                                borderRadius: 6,
                                                                fontSize: 11,
                                                            }}
                                                        >
                                                            Xe điện (EV)
                                                        </span>
                                                    )}
                                                    {data.features
                                                        ?.hasEvCharging && (
                                                        <span
                                                            className="pw-badge"
                                                            style={{
                                                                background:
                                                                    "#fef3c7",
                                                                color: "#b45309",
                                                                padding:
                                                                    "2px 8px",
                                                                borderRadius: 6,
                                                                fontSize: 11,
                                                            }}
                                                        >
                                                            Trạm sạc EV
                                                        </span>
                                                    )}
                                                    {data.features?.hasCctv && (
                                                        <span
                                                            className="pw-badge"
                                                            style={{
                                                                background:
                                                                    "#f3e8ff",
                                                                color: "#6b21a8",
                                                                padding:
                                                                    "2px 8px",
                                                                borderRadius: 6,
                                                                fontSize: 11,
                                                            }}
                                                        >
                                                            Camera 24/7
                                                        </span>
                                                    )}
                                                    {data.features
                                                        ?.isCovered && (
                                                        <span
                                                            className="pw-badge"
                                                            style={{
                                                                background:
                                                                    "#dcfce7",
                                                                color: "#15803d",
                                                                padding:
                                                                    "2px 8px",
                                                                borderRadius: 6,
                                                                fontSize: 11,
                                                            }}
                                                        >
                                                            Có mái che
                                                        </span>
                                                    )}
                                                    {data.features
                                                        ?.supportsLargeVehicles && (
                                                        <span
                                                            className="pw-badge"
                                                            style={{
                                                                background:
                                                                    "#f1f5f9",
                                                                color: "#334155",
                                                                padding:
                                                                    "2px 8px",
                                                                borderRadius: 6,
                                                                fontSize: 11,
                                                            }}
                                                        >
                                                            Xe cỡ lớn
                                                        </span>
                                                    )}
                                                </div>
                                            ) : (
                                                <span
                                                    style={{
                                                        color: "var(--muted)",
                                                    }}
                                                >
                                                    Chưa chọn tiện ích bổ sung
                                                </span>
                                            )}
                                        </td>
                                    </tr>
                                </tbody>
                            </table>
                        </div>
                    </div>

                    {confirming && (
                        <div className="ok" style={{ marginTop: "14px" }}>
                            <b>Xác nhận gửi duyệt?</b>
                            <p>
                                Sau khi gửi, thông tin bãi xe sẽ được gửi lên hệ
                                thống ở trạng thái Chờ duyệt (Pending).
                            </p>
                            <div style={{ display: "flex", gap: "8px" }}>
                                <button
                                    className="btn p"
                                    onClick={handleConfirmSend}
                                    disabled={isSubmitting}
                                >
                                    {isSubmitting
                                        ? "Đang gửi duyệt..."
                                        : "Xác nhận gửi"}
                                </button>
                                <button
                                    className="btn"
                                    onClick={() => setConfirming(false)}
                                    disabled={isSubmitting}
                                >
                                    Quay lại
                                </button>
                            </div>
                        </div>
                    )}
                </>
            )}

            {/* Action buttons */}
            <div className="ph" style={{ marginTop: "16px" }}>
                {step === 0 ? (
                    <Link href="/owner/parking-lots" className="btn">
                        Hủy
                    </Link>
                ) : (
                    <button className="btn" onClick={handleBack}>
                        Quay lại
                    </button>
                )}

                <button className="btn p" onClick={handleNext}>
                    {step === 2 ? "Gửi duyệt" : "Tiếp tục"}
                </button>
            </div>
        </>
    );
}
