"use client";

import React, { useState } from "react";
import Link from "next/link";
import { useParams, useRouter } from "next/navigation";
import { PageHeader } from "../../../../../components/layout/PageHeader";
import {
    useOwnerStore,
    getSpotCounts,
} from "../../../../../store/useOwnerStore";
import { AP } from "../../../../../types/owner";

export default function ParkingLotDetailPage() {
    const params = useParams();
    const router = useRouter();
    const id = Number(params?.id) || 1;

    const lots = useOwnerStore((s) => s.lots);
    const spots = useOwnerStore((s) => s.spots);
    const updateLot = useOwnerStore((s) => s.updateLot);
    const deleteZone = useOwnerStore((s) => s.deleteZone);
    const addZone = useOwnerStore((s) => s.addZone);
    const updateZoneStatus = useOwnerStore((s) => s.updateZoneStatus);

    const lot = lots.find((l) => l.id === id) || lots[0];

    const [tab, setTab] = useState<
        "info" | "zones" | "types" | "price" | "policy" | "appr"
    >("info");
    const [editing, setEditing] = useState(false);
    const [toastMsg, setToastMsg] = useState<string | null>(null);

    // New zone form state
    const [zoneName, setZoneName] = useState("");
    const [zoneType, setZoneType] = useState<"car" | "moto">("car");
    const [zoneCap, setZoneCap] = useState(10);

    if (!lot) {
        return (
            <div
                style={{
                    maxWidth: 800,
                    margin: "60px auto",
                    textAlign: "center",
                    padding: "32px 20px",
                }}
            >
                <h2
                    style={{
                        fontSize: 20,
                        fontWeight: 700,
                        color: "#0f1b3d",
                        marginBottom: 12,
                    }}
                >
                    Không tìm thấy bãi đỗ xe
                </h2>
                <p style={{ color: "#6a7491", fontSize: 14, marginBottom: 24 }}>
                    Bãi đỗ xe này chưa được tạo hoặc danh sách bãi đang được
                    đồng bộ từ hệ thống.
                </p>
                <Link
                    href="/owner/parking-lots"
                    style={{
                        display: "inline-block",
                        padding: "10px 20px",
                        background: "#0f1b3d",
                        color: "#fff",
                        borderRadius: 8,
                        fontWeight: 600,
                        textDecoration: "none",
                    }}
                >
                    ← Quay lại danh sách bãi
                </Link>
            </div>
        );
    }

    const showToast = (msg: string) => {
        setToastMsg(msg);
        setTimeout(() => setToastMsg(null), 2200);
    };

    const isPending = lot?.ap === "pending";
    const isLocked = isPending || !editing;

    const counts = getSpotCounts(spots);
    const zonesSum = lot?.zones.reduce((a, z) => a + z.c, 0) || 0;
    const totalCap = lot ? lot.cc + lot.cm : 0;

    const tabs: Array<{ id: typeof tab; label: string }> = [
        { id: "info", label: "Thông tin" },
        { id: "zones", label: "Khu vực" },
        { id: "types", label: "Loại xe & sức chứa" },
        { id: "price", label: "Giá" },
        { id: "policy", label: "Đặt chỗ" },
        { id: "appr", label: "Duyệt" },
    ];

    const handleAddZone = () => {
        if (!zoneName.trim()) {
            showToast("Nhập tên khu vực");
            return;
        }
        addZone(lot.id, {
            n: zoneName.trim(),
            t: zoneType,
            c: Number(zoneCap) || 10,
            s: "open",
        });
        setZoneName("");
        setZoneCap(10);
        showToast("Đã thêm khu vực mới");
    };

    const timelineSteps = [
        "Bản nháp",
        "Xem lại",
        "Gửi duyệt",
        "Chờ duyệt",
        lot?.ap === "rejected" ? "Bị từ chối" : "Đã duyệt",
    ];

    const getTimelineActiveIndex = () => {
        if (lot?.ap === "draft") return 0;
        if (lot?.ap === "pending") return 3;
        return 4; // approved or rejected
    };

    return (
        <>
            {toastMsg && <div className="toast">{toastMsg}</div>}

            <PageHeader
                breadcrumb={`/owner/parking-lots/${id}${editing ? "/edit" : ""}`}
                title={lot?.name || "Chi tiết bãi"}
                subtitle={lot?.addr || ""}
                actions={
                    <div
                        style={{
                            display: "flex",
                            gap: "8px",
                            alignItems: "center",
                        }}
                    >
                        <Link href="/owner/parking-lots" className="btn">
                            ← Danh sách
                        </Link>
                        <Link
                            href={`/owner/lots/${lot?.backendId || id}/policies`}
                            className="btn"
                            style={{ background: "#eef2ff", color: "#1e19d8", fontWeight: 700 }}
                        >
                            ⚖️ Chính sách đặt chỗ & cọc
                        </Link>
                        <Link
                            href={`/owner/lots/${lot?.backendId || id}/pricing`}
                            className="btn"
                        >
                            Biểu giá
                        </Link>
                        <Link
                            href={`/owner/lots/${lot?.backendId || id}/staff`}
                            className="btn"
                            style={{ background: "#f0fdfa", color: "#0f766e", fontWeight: 700 }}
                        >
                            👥 Nhân viên
                        </Link>
                        {!isPending &&
                            (editing ? (
                                <button
                                    className="btn p"
                                    onClick={() => {
                                        setEditing(false);
                                        showToast("Đã lưu thay đổi");
                                    }}
                                >
                                    Lưu thay đổi
                                </button>
                            ) : (
                                <button
                                    className="btn p"
                                    onClick={() => setEditing(true)}
                                >
                                    Chỉnh sửa
                                </button>
                            ))}
                    </div>
                }
            />

            {isPending && (
                <div
                    className="err"
                    style={{
                        borderColor: "var(--line)",
                        background: "var(--soft)",
                        color: "var(--ink)",
                    }}
                >
                    Bãi đang chờ duyệt nên các thông tin tạm thời bị khóa.
                </div>
            )}

            <div className="card">
                {/* Tab switcher */}
                <div style={{ marginBottom: "14px" }}>
                    <div className="segs">
                        {tabs.map((t) => (
                            <button
                                key={t.id}
                                className={tab === t.id ? "on" : ""}
                                onClick={() => setTab(t.id)}
                            >
                                {t.label}
                            </button>
                        ))}
                    </div>
                </div>

                {/* Tab 1: Thông tin */}
                {tab === "info" && (
                    <div className="form">
                        <label className="f">
                            <span>Tên bãi</span>
                            <input
                                type="text"
                                disabled={isLocked}
                                value={lot?.name || ""}
                                onChange={(e) =>
                                    updateLot(lot.id, { name: e.target.value })
                                }
                            />
                        </label>
                        <label className="f">
                            <span>Địa chỉ</span>
                            <input
                                type="text"
                                disabled={isLocked}
                                value={lot?.addr || ""}
                                onChange={(e) =>
                                    updateLot(lot.id, { addr: e.target.value })
                                }
                            />
                        </label>
                        <label className="f">
                            <span>GPS</span>
                            <input
                                type="text"
                                disabled={isLocked}
                                value={lot?.gps || ""}
                                onChange={(e) =>
                                    updateLot(lot.id, { gps: e.target.value })
                                }
                            />
                        </label>
                        <label className="f">
                            <span>Giờ mở cửa</span>
                            <input
                                type="time"
                                disabled={isLocked}
                                value={lot?.op || ""}
                                onChange={(e) =>
                                    updateLot(lot.id, { op: e.target.value })
                                }
                            />
                        </label>
                        <label className="f">
                            <span>Giờ đóng cửa</span>
                            <input
                                type="time"
                                disabled={isLocked}
                                value={lot?.cl2 || ""}
                                onChange={(e) =>
                                    updateLot(lot.id, { cl2: e.target.value })
                                }
                            />
                        </label>
                        <label className="f">
                            <span>Số tầng</span>
                            <input
                                type="number"
                                disabled={isLocked}
                                value={lot?.fls || 0}
                                onChange={(e) =>
                                    updateLot(lot.id, {
                                        fls: Number(e.target.value),
                                    })
                                }
                            />
                        </label>
                    </div>
                )}

                {/* Tab 2: Khu vực */}
                {tab === "zones" && (
                    <>
                        <div className="tw">
                            <table>
                                <thead>
                                    <tr>
                                        <th>Khu vực</th>
                                        <th>Loại xe</th>
                                        <th>Sức chứa</th>
                                        <th>Trạng thái</th>
                                        <th></th>
                                    </tr>
                                </thead>
                                <tbody>
                                    {lot?.zones.map((z, idx) => (
                                        <tr key={idx}>
                                            <td>{z.n}</td>
                                            <td>
                                                {z.t === "car"
                                                    ? "Ô tô"
                                                    : "Xe máy"}
                                            </td>
                                            <td>{z.c}</td>
                                            <td>
                                                <select
                                                    className="sr"
                                                    disabled={isLocked}
                                                    value={z.s}
                                                    onChange={(e) =>
                                                        updateZoneStatus(
                                                            lot.id,
                                                            idx,
                                                            e.target.value as
                                                                | "open"
                                                                | "closed",
                                                        )
                                                    }
                                                >
                                                    <option value="open">
                                                        Mở
                                                    </option>
                                                    <option value="closed">
                                                        Đóng
                                                    </option>
                                                </select>
                                            </td>
                                            <td>
                                                <button
                                                    className="btn sm"
                                                    disabled={isLocked}
                                                    onClick={() =>
                                                        deleteZone(lot.id, idx)
                                                    }
                                                >
                                                    Xóa
                                                </button>
                                            </td>
                                        </tr>
                                    ))}
                                </tbody>
                            </table>
                        </div>

                        <p className="mut" style={{ marginTop: "12px" }}>
                            Tổng sức chứa các khu: <b>{zonesSum}</b> /{" "}
                            {totalCap}{" "}
                            {zonesSum !== totalCap && (
                                <span style={{ color: "#d23c42" }}>
                                    · chưa khớp tổng sức chứa
                                </span>
                            )}
                        </p>

                        {!isLocked && (
                            <div style={{ marginTop: "16px" }}>
                                <div className="form">
                                    <label className="f">
                                        <span>Tên khu</span>
                                        <input
                                            type="text"
                                            value={zoneName}
                                            onChange={(e) =>
                                                setZoneName(e.target.value)
                                            }
                                        />
                                    </label>
                                    <label className="f">
                                        <span>Loại xe</span>
                                        <select
                                            value={zoneType}
                                            onChange={(e) =>
                                                setZoneType(
                                                    e.target.value as
                                                        "car" | "moto",
                                                )
                                            }
                                        >
                                            <option value="car">Ô tô</option>
                                            <option value="moto">Xe máy</option>
                                        </select>
                                    </label>
                                    <label className="f">
                                        <span>Sức chứa</span>
                                        <input
                                            type="number"
                                            value={zoneCap}
                                            onChange={(e) =>
                                                setZoneCap(
                                                    Number(e.target.value),
                                                )
                                            }
                                        />
                                    </label>
                                </div>
                                <p style={{ marginTop: "12px" }}>
                                    <button
                                        className="btn p"
                                        onClick={handleAddZone}
                                    >
                                        Thêm khu vực
                                    </button>
                                </p>
                            </div>
                        )}
                    </>
                )}

                {/* Tab 3: Loại xe & sức chứa */}
                {tab === "types" && (
                    <>
                        <div className="form">
                            <label className="f ck">
                                <input
                                    type="checkbox"
                                    disabled={isLocked}
                                    checked={lot?.vc || false}
                                    onChange={(e) =>
                                        updateLot(lot.id, {
                                            vc: e.target.checked,
                                        })
                                    }
                                />
                                Nhận ô tô
                            </label>
                            <label className="f ck">
                                <input
                                    type="checkbox"
                                    disabled={isLocked}
                                    checked={lot?.vm || false}
                                    onChange={(e) =>
                                        updateLot(lot.id, {
                                            vm: e.target.checked,
                                        })
                                    }
                                />
                                Nhận xe máy
                            </label>
                            <label className="f">
                                <span>Sức chứa ô tô</span>
                                <input
                                    type="number"
                                    disabled={isLocked}
                                    value={lot?.cc || 0}
                                    onChange={(e) =>
                                        updateLot(lot.id, {
                                            cc: Number(e.target.value),
                                        })
                                    }
                                />
                            </label>
                            <label className="f">
                                <span>Sức chứa xe máy</span>
                                <input
                                    type="number"
                                    disabled={isLocked}
                                    value={lot?.cm || 0}
                                    onChange={(e) =>
                                        updateLot(lot.id, {
                                            cm: Number(e.target.value),
                                        })
                                    }
                                />
                            </label>
                        </div>
                        <p style={{ marginTop: "14px" }}>
                            Tổng sức chứa: <b>{totalCap}</b> chỗ
                            {lot?.ap === "approved" && (
                                <>
                                    {" "}
                                    · Hiện có xe: <b>{counts.occupied}</b> · Đã
                                    đặt trước: <b>{counts.reserved}</b> · Trống:{" "}
                                    <b>{counts.available}</b>
                                </>
                            )}
                        </p>
                    </>
                )}

                {/* Tab 4: Giá */}
                {tab === "price" && (
                    <div className="form">
                        <label className="f">
                            <span>Giá ô tô (đ/giờ)</span>
                            <input
                                type="number"
                                disabled={isLocked}
                                value={lot?.pc || 0}
                                onChange={(e) =>
                                    updateLot(lot.id, {
                                        pc: Number(e.target.value),
                                    })
                                }
                            />
                        </label>
                        <label className="f">
                            <span>Giá xe máy (đ/giờ)</span>
                            <input
                                type="number"
                                disabled={isLocked}
                                value={lot?.pm || 0}
                                onChange={(e) =>
                                    updateLot(lot.id, {
                                        pm: Number(e.target.value),
                                    })
                                }
                            />
                        </label>
                        <label className="f">
                            <span>Tiền cọc giữ chỗ (đ)</span>
                            <input
                                type="number"
                                disabled={isLocked}
                                value={lot?.dep || 0}
                                onChange={(e) =>
                                    updateLot(lot.id, {
                                        dep: Number(e.target.value),
                                    })
                                }
                            />
                        </label>
                    </div>
                )}

                {/* Tab 5: Đặt chỗ */}
                {tab === "policy" && (
                    <div className="form">
                        <div
                            style={{
                                padding: "12px 16px",
                                backgroundColor: "#eff6ff",
                                border: "1px solid #bfdbfe",
                                borderRadius: "8px",
                                marginBottom: "16px",
                                display: "flex",
                                alignItems: "center",
                                justifyContent: "space-between",
                                gap: "12px",
                            }}
                        >
                            <div>
                                <div
                                    style={{
                                        fontWeight: 600,
                                        color: "#1e40af",
                                        fontSize: "14px",
                                    }}
                                >
                                    Trang Cấu hình Chính sách Nâng cao
                                </div>
                                <div
                                    style={{
                                        fontSize: "12px",
                                        color: "#3b82f6",
                                    }}
                                >
                                    Cấu hình tỷ lệ suất đặt trước (10% - 50%),
                                    mức tiền cọc 15 phút và đồng bộ trực tiếp
                                    với máy chủ.
                                </div>
                            </div>
                            <Link
                                href={`/owner/lots/${id}/policies`}
                                className="btn p"
                                style={{
                                    whiteSpace: "nowrap",
                                    fontSize: "13px",
                                }}
                            >
                                Mở trang chính sách →
                            </Link>
                        </div>
                        <label className="f">
                            <span>Cho phép đặt trước</span>
                            <select
                                disabled={isLocked}
                                value={lot?.res || "off"}
                                onChange={(e) =>
                                    updateLot(lot.id, {
                                        res: e.target.value as "on" | "off",
                                    })
                                }
                            >
                                <option value="on">Bật</option>
                                <option value="off">Tắt</option>
                            </select>
                        </label>
                        <label className="f">
                            <span>Thời gian giữ chỗ</span>
                            <select
                                disabled={isLocked}
                                value={lot?.hold || 15}
                                onChange={(e) =>
                                    updateLot(lot.id, {
                                        hold: Number(e.target.value),
                                    })
                                }
                            >
                                <option value={15}>15 phút</option>
                                <option value={30}>30 phút</option>
                                <option value={45}>45 phút</option>
                            </select>
                        </label>
                        <label className="f">
                            <span>Số ô tối đa cho đặt trước</span>
                            <input
                                type="number"
                                disabled={isLocked}
                                value={lot?.maxr || 0}
                                onChange={(e) =>
                                    updateLot(lot.id, {
                                        maxr: Number(e.target.value),
                                    })
                                }
                            />
                        </label>
                        <label className="f">
                            <span>Hủy miễn phí</span>
                            <select
                                disabled={isLocked}
                                value={lot?.fc || 0}
                                onChange={(e) =>
                                    updateLot(lot.id, {
                                        fc: Number(e.target.value),
                                    })
                                }
                            >
                                <option value={10}>Trước 10 phút</option>
                                <option value={0}>Không cho hủy</option>
                            </select>
                        </label>
                    </div>
                )}

                {/* Tab 6: Duyệt */}
                {tab === "appr" && (
                    <>
                        <div className="tl">
                            {timelineSteps.map((s, idx) => (
                                <span
                                    key={idx}
                                    className={
                                        idx <= getTimelineActiveIndex()
                                            ? "on"
                                            : ""
                                    }
                                >
                                    {s}
                                </span>
                            ))}
                        </div>

                        <p>
                            Trạng thái hiện tại:{" "}
                            <span
                                className="tag"
                                style={
                                    {
                                        "--c": AP[lot.ap][1],
                                    } as React.CSSProperties
                                }
                            >
                                {AP[lot.ap][0]}
                            </span>
                        </p>

                        {lot?.ap === "rejected" && (
                            <>
                                <div className="err">
                                    <b>Lý do từ chối</b>
                                    <br />
                                    {lot.why}
                                </div>
                                <button
                                    className="btn p"
                                    onClick={() =>
                                        router.push("/owner/parking-lots/new")
                                    }
                                >
                                    Chỉnh sửa và gửi lại
                                </button>
                            </>
                        )}
                    </>
                )}
            </div>
        </>
    );
}
