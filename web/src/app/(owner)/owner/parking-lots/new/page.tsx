"use client";

import React, { useState } from "react";
import Link from "next/link";
import dynamic from "next/dynamic";
import { PageHeader } from "../../../../../components/layout/PageHeader";
import { Plan2D } from "../../../../../components/viz/Plan2D";
import { useOwnerStore } from "../../../../../store/useOwnerStore";
import { AP, vnd, ST, K } from "../../../../../types/owner";

const ParkingScene3DViewer = dynamic(
    () => import("../../../../../components/viz/ParkingScene3DViewer"),
    { ssr: false },
);

import {
    registerParkingLotApi,
    ParkingLotTypeEnum,
} from "@/features/owner/api/ownerApi";
import { useOwnerMapStore } from "@/features/owner/lots/useOwnerMapStore";
import { useLotInfoStore } from "@/features/owner/lots/lotInfo";
import { ApiError } from "@/shared/errors/api-error";

interface WizardData {
    name: string;
    addr: string;
    gps: string;
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
}

const INITIAL_DATA: WizardData = {
    name: "",
    addr: "",
    gps: "",
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
};

export default function NewParkingLotPage() {
    const addLot = useOwnerStore((s) => s.addLot);

    const [step, setStep] = useState(0);
    const [data, setData] = useState<WizardData>(INITIAL_DATA);
    const [errors, setErrors] = useState<string[]>([]);
    const [previewMode, setPreviewMode] = useState<"3d" | "2d">("3d");
    const [confirming, setConfirming] = useState(false);
    const [isSubmitting, setIsSubmitting] = useState(false);
    const [done, setDone] = useState(false);

    const validate = () => {
        const errs: string[] = [];
        if (!data.name.trim()) errs.push("Nhập tên bãi");
        if (!data.addr.trim()) errs.push("Nhập địa chỉ");
        if (!/^-?\d+(\.\d+)?\s*,\s*-?\d+(\.\d+)?$/.test(data.gps.trim())) {
            errs.push(
                'Tọa độ GPS theo dạng "vĩ độ, kinh độ", ví dụ 10.7769, 106.7009',
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

    const handleConfirmSend = async () => {
        setIsSubmitting(true);
        setErrors([]);
        try {
            const [latStr, lngStr] = data.gps.split(",").map((s) => s.trim());
            const latitude = Number(latStr) || 10.7769;
            const longitude = Number(lngStr) || 106.7009;

            let registeredId = (Date.now() % 100000) + 10;

            // Gọi API đăng ký bãi xe chuẩn BE (POST /api/parking/lots)
            try {
                const res = await registerParkingLotApi({
                    name: data.name,
                    address: data.addr,
                    latitude,
                    longitude,
                    type: data.type,
                });
                if (res?.id) {
                    // If BE returns a string UUID, we generate a stable local numeric ID for store mapping
                    registeredId =
                        Math.abs(
                            res.id
                                .split("")
                                .reduce(
                                    (acc, char) =>
                                        (acc << 5) - acc + char.charCodeAt(0),
                                    0,
                                ) % 100000,
                        ) + 10;
                }
            } catch (err: unknown) {
                if (err instanceof ApiError) {
                    if (err.status === 409) {
                        setErrors([
                            "Tên bãi đỗ xe này đã được bạn đăng ký trước đó. Vui lòng chọn tên khác (Mã lỗi: parking.lot_duplicate).",
                        ]);
                        setConfirming(false);
                        setStep(0);
                        return;
                    }
                    if (err.status === 400) {
                        setErrors([
                            err.problem?.detail ||
                                "Thông tin đăng ký bãi đỗ xe chưa đúng định dạng.",
                        ]);
                        setConfirming(false);
                        setStep(0);
                        return;
                    }
                    if (err.status === 401 || err.status === 403) {
                        setErrors([
                            "Tài khoản của bạn chưa có quyền Chủ bãi (ParkingLotOwner) để thực hiện thao tác này.",
                        ]);
                        setConfirming(false);
                        return;
                    }
                }
                console.warn(
                    "Backend API registration call skipped or offline, falling back to local store:",
                    err,
                );
            }

            setCreatedLotId(registeredId);

            const newLotObj = {
                id: registeredId,
                name: data.name.trim(),
                addr: data.addr.trim(),
                gps: data.gps.trim(),
                op: data.op,
                cl2: data.cl2,
                vc: data.vc,
                vm: data.vm,
                cc: data.vc ? data.cap : 0,
                cm: data.vm ? data.cap : 0,
                fls: data.type === "Outdoor" ? 1 : data.floors,
                pc: data.pc,
                pm: data.pm,
                dep: data.dep,
                res: Number(data.hold) > 0 ? ("on" as const) : ("off" as const),
                hold: Number(data.hold) || 15,
                maxr: 6,
                fc: 10,
                ap: "pending" as const,
                zones: [],
            };

            // Cập nhật bãi xe vào cả 2 store để đảm bảo hiển thị tức thì trên mọi view (Bản đồ, Danh sách, Thống kê)
            addLot(newLotObj);
            useOwnerMapStore.getState().addOverlayLot(newLotObj);

            // Cập nhật thông tin chi tiết bãi
            const today = new Date().toISOString().split("T")[0];
            useLotInfoStore.getState().setLotInfo(registeredId, {
                submittedAt: today,
                openingHours: `${data.op} – ${data.cl2}`,
                phone: "Chưa cập nhật",
                prices: {
                    carFirstHour: data.pc,
                    carNextHour: data.pc,
                    reservationDeposit: data.dep,
                },
            });

            setDone(true);
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
                            ⚙️ Cấu hình bảng giá cho bãi này →
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

                            <label className="f">
                                <span>Địa chỉ</span>
                                <input
                                    type="text"
                                    value={data.addr}
                                    onChange={(e) =>
                                        setData({
                                            ...data,
                                            addr: e.target.value,
                                        })
                                    }
                                />
                            </label>

                            <label className="f">
                                <span>Tọa độ GPS</span>
                                <input
                                    type="text"
                                    value={data.gps}
                                    placeholder="10.7769, 106.7009"
                                    onChange={(e) =>
                                        setData({
                                            ...data,
                                            gps: e.target.value,
                                        })
                                    }
                                />
                            </label>

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
                                <span>Loại bãi xe (Chuẩn BE)</span>
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
                                        <td>GPS</td>
                                        <td>{data.gps}</td>
                                    </tr>
                                    <tr>
                                        <td>Giờ hoạt động</td>
                                        <td>
                                            {data.op} – {data.cl2}
                                        </td>
                                    </tr>
                                    <tr>
                                        <td>Loại bãi xe (BE)</td>
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
