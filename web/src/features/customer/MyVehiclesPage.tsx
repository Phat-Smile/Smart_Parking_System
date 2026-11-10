"use client";

import { useEffect, useState, type FormEvent } from "react";
import { CustomerHeader } from "./CustomerShell";
import { ParkWiseFooter } from "@/features/parkwise/ParkWiseFooter";

import { useCustomerLanguage } from "./i18n/customerLanguage";
import {
    addMyVehicle,
    deleteMyVehicle,
    getMyVehicles,
    setDefaultVehicle,
    type RegisteredVehicle,
} from "./myVehiclesApi";
import {
    AlertCircle,
    Bike,
    Car,
    Check,
    Loader2,
    Plus,
    RefreshCw,
    Star,
    Trash2,
    X,
} from "lucide-react";
import { ApiError } from "@/shared/errors/api-error";

export function MyVehiclesPage({ fontClassName }: { fontClassName?: string }) {
    const { copy } = useCustomerLanguage();
    const [vehicles, setVehicles] = useState<RegisteredVehicle[]>([]);
    const [filterType, setFilterType] = useState<"All" | "Car" | "Motorbike">(
        "All",
    );
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);

    // Modal state
    const [isAddModalOpen, setIsAddModalOpen] = useState(false);
    const [plateInput, setPlateInput] = useState("");
    const [typeInput, setTypeInput] = useState<"Car" | "Motorbike">("Car");
    const [nicknameInput, setNicknameInput] = useState("");
    const [submitting, setSubmitting] = useState(false);
    const [modalError, setModalError] = useState<string | null>(null);

    // Delete confirmation state
    const [deletingId, setDeletingId] = useState<string | null>(null);
    const [deleteLoading, setDeleteLoading] = useState(false);

    async function loadVehicles() {
        setLoading(true);
        setError(null);
        try {
            const data = await getMyVehicles();
            setVehicles(data.vehicles || []);
        } catch (err: unknown) {
            if (err instanceof ApiError) {
                setError(
                    err.problem?.detail ||
                        err.message ||
                        "Không thể tải danh sách phương tiện.",
                );
            } else if (err instanceof TypeError) {
                setError(
                    "Không thể kết nối đến máy chủ. Vui lòng thử lại sau.",
                );
            } else if (err instanceof Error) {
                setError(err.message);
            } else {
                setError("Đã xảy ra lỗi khi tải danh sách phương tiện.");
            }
        } finally {
            setLoading(false);
        }
    }

    useEffect(() => {
        let active = true;
        queueMicrotask(() => {
            if (active) void loadVehicles();
        });
        return () => {
            active = false;
        };
    }, []);

    async function handleAddVehicle(e: FormEvent) {
        e.preventDefault();
        const trimmedPlate = plateInput.trim().toUpperCase();
        if (!trimmedPlate) {
            setModalError(
                copy(
                    "Please enter a license plate.",
                    "Vui lòng nhập biển số xe.",
                ),
            );
            return;
        }

        setSubmitting(true);
        setModalError(null);
        try {
            const newVehicle = await addMyVehicle(
                trimmedPlate,
                typeInput,
                nicknameInput.trim() || null,
            );
            setVehicles((prev) => [newVehicle, ...prev]);
            setIsAddModalOpen(false);
            setPlateInput("");
            setNicknameInput("");
        } catch (err: unknown) {
            if (err instanceof ApiError) {
                if (
                    err.problem?.errorCode ===
                    "identity.vehicle_plate_already_registered"
                ) {
                    setModalError(
                        copy(
                            "This license plate is already registered.",
                            "Biển số xe này đã được đăng ký trong hệ thống.",
                        ),
                    );
                } else {
                    setModalError(
                        err.problem?.detail ||
                            err.message ||
                            "Không thể thêm phương tiện.",
                    );
                }
            } else if (err instanceof Error) {
                setModalError(err.message);
            } else {
                setModalError("Đã xảy ra lỗi khi thêm phương tiện.");
            }
        } finally {
            setSubmitting(false);
        }
    }

    async function handleDeleteVehicle(vehicleId: string) {
        setDeleteLoading(true);
        try {
            await deleteMyVehicle(vehicleId);
            setVehicles((prev) =>
                prev.filter((v) => v.vehicleId !== vehicleId),
            );
            setDeletingId(null);
        } catch (err: unknown) {
            alert(
                err instanceof Error
                    ? err.message
                    : "Không thể xóa phương tiện.",
            );
        } finally {
            setDeleteLoading(false);
        }
    }

    async function handleSetDefault(vehicleId: string) {
        try {
            await setDefaultVehicle(vehicleId);
            setVehicles((prev) =>
                prev.map((v) => ({
                    ...v,
                    isDefault: v.vehicleId === vehicleId,
                })),
            );
        } catch (err: unknown) {
            alert(
                err instanceof Error
                    ? err.message
                    : "Không thể đặt làm xe mặc định.",
            );
        }
    }

    const filteredVehicles = vehicles.filter((v) => {
        if (filterType === "All") return true;
        return v.vehicleType === filterType;
    });

    return (
        <div
            className={`min-h-screen bg-[#070810] text-[#e1e4ed] flex flex-col ${fontClassName || ""}`}
        >
            <CustomerHeader active="vehicles" />

            <main className="flex-1 max-w-5xl w-full mx-auto px-4 py-8 space-y-8">
                {/* Header */}
                <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4 border-b border-white/10 pb-6">
                    <div>
                        <div className="flex items-center gap-3">
                            <span className="text-xs font-bold tracking-widest text-[#a28aff] uppercase">
                                {copy("MANAGEMENT", "QUẢN LÝ")}
                            </span>
                        </div>
                        <h1 className="text-3xl font-black text-white mt-1 tracking-tight">
                            {copy("My Vehicles", "Phương tiện của tôi")}
                        </h1>
                        <p className="text-sm text-[#8790a3] mt-1">
                            {copy(
                                "Manage license plates for automatic gate entry and quick parking reservations.",
                                "Quản lý biển số xe để nhận diện mở barrier tự động và đặt chỗ nhanh chóng.",
                            )}
                        </p>
                    </div>

                    <div className="flex items-center gap-3">
                        <button
                            type="button"
                            onClick={() => void loadVehicles()}
                            disabled={loading}
                            className="p-2.5 rounded-xl bg-white/5 border border-white/10 text-[#8790a3] hover:text-white hover:border-white/20 transition-colors"
                            title={copy("Refresh", "Làm mới")}
                            aria-label={copy("Refresh", "Làm mới")}
                        >
                            <RefreshCw
                                size={18}
                                className={loading ? "animate-spin" : ""}
                            />
                        </button>
                        <button
                            type="button"
                            onClick={() => {
                                setModalError(null);
                                setIsAddModalOpen(true);
                            }}
                            className="inline-flex items-center gap-2 px-4 py-2.5 rounded-xl bg-[#6246ea] hover:bg-[#7c5cff] text-white font-bold text-sm shadow-glow-purple transition-all"
                        >
                            <Plus size={18} />
                            {copy("Add vehicle", "Thêm xe mới")}
                        </button>
                    </div>
                </div>

                {/* Filter Tabs */}
                <div className="flex items-center gap-2 border-b border-white/5 pb-2">
                    {(["All", "Car", "Motorbike"] as const).map((type) => (
                        <button
                            key={type}
                            type="button"
                            onClick={() => setFilterType(type)}
                            className={`px-4 py-2 rounded-xl text-xs font-bold transition-colors ${
                                filterType === type
                                    ? "bg-[#6246ea]/20 text-[#a28aff] border border-[#6246ea]/40"
                                    : "text-[#8790a3] hover:text-white hover:bg-white/5"
                            }`}
                        >
                            {type === "All" &&
                                copy(
                                    `All (${vehicles.length})`,
                                    `Tất cả (${vehicles.length})`,
                                )}
                            {type === "Car" &&
                                copy(
                                    `Cars (${vehicles.filter((v) => v.vehicleType === "Car").length})`,
                                    `Ô tô (${vehicles.filter((v) => v.vehicleType === "Car").length})`,
                                )}
                            {type === "Motorbike" &&
                                copy(
                                    `Motorbikes (${vehicles.filter((v) => v.vehicleType === "Motorbike").length})`,
                                    `Xe máy (${vehicles.filter((v) => v.vehicleType === "Motorbike").length})`,
                                )}
                        </button>
                    ))}
                </div>

                {/* Error Banner */}
                {error && (
                    <div className="p-4 bg-red-500/10 border border-red-500/30 rounded-2xl flex items-center justify-between">
                        <div className="flex items-center gap-3 text-red-300 text-sm font-medium">
                            <AlertCircle
                                size={20}
                                className="text-red-400 shrink-0"
                            />
                            <span>{error}</span>
                        </div>
                        <button
                            type="button"
                            onClick={() => void loadVehicles()}
                            className="px-3 py-1.5 bg-red-500/20 hover:bg-red-500/30 text-white rounded-lg text-xs font-semibold"
                        >
                            {copy("Retry", "Thử lại")}
                        </button>
                    </div>
                )}

                {/* Loading Skeleton */}
                {loading && (
                    <div className="grid grid-cols-1 md:grid-cols-2 gap-5">
                        {[1, 2].map((i) => (
                            <div
                                key={i}
                                className="bg-[#0b0c17] border border-white/10 rounded-2xl p-6 h-40 animate-pulse flex flex-col justify-between"
                            >
                                <div className="space-y-3">
                                    <div className="h-6 w-36 bg-white/10 rounded-md" />
                                    <div className="h-4 w-24 bg-white/5 rounded-md" />
                                </div>
                                <div className="h-8 w-full bg-white/5 rounded-xl" />
                            </div>
                        ))}
                    </div>
                )}

                {/* Empty State */}
                {!loading && !error && filteredVehicles.length === 0 && (
                    <div className="bg-[#0b0c17] border border-white/10 rounded-2xl p-12 text-center space-y-4">
                        <div className="w-16 h-16 rounded-full bg-[#6246ea]/10 border border-[#6246ea]/20 flex items-center justify-center mx-auto text-[#a28aff]">
                            <Car size={30} />
                        </div>
                        <div>
                            <h2 className="text-lg font-bold text-white mb-1">
                                {copy(
                                    "No vehicles registered",
                                    "Chưa có phương tiện nào",
                                )}
                            </h2>
                            <p className="text-sm text-[#8790a3] max-w-md mx-auto">
                                {copy(
                                    "Add your car or motorbike license plate so the parking barrier opens automatically upon entry.",
                                    "Đăng ký biển số ô tô hoặc xe máy của bạn để hệ thống nhận diện và mở barrier tự động.",
                                )}
                            </p>
                        </div>
                        <button
                            type="button"
                            onClick={() => {
                                setModalError(null);
                                setIsAddModalOpen(true);
                            }}
                            className="inline-flex items-center gap-2 px-5 py-2.5 rounded-xl bg-[#6246ea] hover:bg-[#7c5cff] text-white font-bold text-sm shadow-glow-purple transition-all"
                        >
                            <Plus size={18} />
                            {copy("Add your first vehicle", "Thêm xe đầu tiên")}
                        </button>
                    </div>
                )}

                {/* Vehicle Grid */}
                {!loading && !error && filteredVehicles.length > 0 && (
                    <div className="grid grid-cols-1 md:grid-cols-2 gap-5">
                        {filteredVehicles.map((vehicle) => {
                            const isCar = vehicle.vehicleType === "Car";
                            const Icon = isCar ? Car : Bike;

                            return (
                                <div
                                    key={vehicle.vehicleId}
                                    className="bg-[#0b0c17] border border-white/10 hover:border-white/20 rounded-2xl p-6 transition-all flex flex-col justify-between shadow-card-light"
                                >
                                    <div>
                                        <div className="flex items-start justify-between mb-4">
                                            <div className="flex items-center gap-3">
                                                <div className="w-12 h-12 rounded-xl bg-white/5 border border-white/10 flex items-center justify-center text-[#a28aff]">
                                                    <Icon size={24} />
                                                </div>
                                                <div>
                                                    <div className="flex items-center gap-2">
                                                        <span className="font-mono text-lg font-black text-white px-2.5 py-1 bg-white/5 border border-white/10 rounded-lg tracking-wider">
                                                            {
                                                                vehicle.licensePlate
                                                            }
                                                        </span>
                                                        {vehicle.isDefault && (
                                                            <span className="inline-flex items-center gap-1 text-[11px] font-bold px-2 py-0.5 rounded-md bg-amber-500/20 text-amber-300 border border-amber-500/30">
                                                                <Star
                                                                    size={11}
                                                                    className="fill-amber-300"
                                                                />
                                                                {copy(
                                                                    "Default",
                                                                    "Mặc định",
                                                                )}
                                                            </span>
                                                        )}
                                                    </div>
                                                    <p className="text-xs text-[#8790a3] mt-1">
                                                        {vehicle.nickName ||
                                                            (isCar
                                                                ? copy(
                                                                      "Car",
                                                                      "Ô tô",
                                                                  )
                                                                : copy(
                                                                      "Motorbike",
                                                                      "Xe máy",
                                                                  ))}
                                                    </p>
                                                </div>
                                            </div>

                                            <button
                                                type="button"
                                                onClick={() =>
                                                    setDeletingId(
                                                        vehicle.vehicleId,
                                                    )
                                                }
                                                className="p-2 text-[#8790a3] hover:text-red-400 hover:bg-red-500/10 rounded-lg transition-colors"
                                                title={copy(
                                                    "Delete vehicle",
                                                    "Xóa xe",
                                                )}
                                                aria-label={copy(
                                                    "Delete vehicle",
                                                    "Xóa xe",
                                                )}
                                            >
                                                <Trash2 size={16} />
                                            </button>
                                        </div>
                                    </div>

                                    <div className="pt-4 border-t border-white/5 flex items-center justify-between text-xs text-[#8790a3]">
                                        <span>
                                            {copy("Registered", "Đã đăng ký")}:{" "}
                                            {new Date(
                                                vehicle.createdAtUtc,
                                            ).toLocaleDateString("vi-VN")}
                                        </span>
                                        {!vehicle.isDefault && (
                                            <button
                                                type="button"
                                                onClick={() =>
                                                    void handleSetDefault(
                                                        vehicle.vehicleId,
                                                    )
                                                }
                                                className="text-[#a28aff] hover:underline font-semibold"
                                            >
                                                {copy(
                                                    "Set as default",
                                                    "Đặt làm mặc định",
                                                )}
                                            </button>
                                        )}
                                    </div>
                                </div>
                            );
                        })}
                    </div>
                )}
            </main>

            {/* Add Vehicle Modal */}
            {isAddModalOpen && (
                <div className="fixed inset-0 z-50 bg-black/70 backdrop-blur-sm flex items-center justify-center p-4">
                    <div className="bg-[#0b0c17] border border-white/15 rounded-3xl max-w-md w-full p-6 shadow-2xl space-y-5 animate-in fade-in zoom-in-95 duration-200">
                        <div className="flex items-center justify-between border-b border-white/10 pb-4">
                            <h2 className="text-lg font-bold text-white">
                                {copy(
                                    "Add New Vehicle",
                                    "Đăng ký phương tiện mới",
                                )}
                            </h2>
                            <button
                                type="button"
                                onClick={() => setIsAddModalOpen(false)}
                                className="p-1.5 text-[#8790a3] hover:text-white rounded-lg hover:bg-white/5"
                            >
                                <X size={20} />
                            </button>
                        </div>

                        {modalError && (
                            <div className="p-3 bg-red-500/10 border border-red-500/30 rounded-xl text-xs text-red-300 flex items-center gap-2">
                                <AlertCircle
                                    size={16}
                                    className="shrink-0 text-red-400"
                                />
                                <span>{modalError}</span>
                            </div>
                        )}

                        <form onSubmit={handleAddVehicle} className="space-y-4">
                            {/* Vehicle Type Choice */}
                            <div>
                                <label className="block text-xs font-semibold text-[#8790a3] mb-2">
                                    {copy("Vehicle Type", "Loại phương tiện")} *
                                </label>
                                <div className="grid grid-cols-2 gap-3">
                                    <button
                                        type="button"
                                        onClick={() => setTypeInput("Car")}
                                        className={`flex items-center justify-center gap-2 p-3 rounded-xl border text-sm font-bold transition-all ${
                                            typeInput === "Car"
                                                ? "bg-[#6246ea]/20 border-[#6246ea] text-white"
                                                : "bg-white/5 border-white/10 text-[#8790a3] hover:border-white/20"
                                        }`}
                                    >
                                        <Car size={18} />
                                        {copy("Car", "Ô tô")}
                                    </button>
                                    <button
                                        type="button"
                                        onClick={() =>
                                            setTypeInput("Motorbike")
                                        }
                                        className={`flex items-center justify-center gap-2 p-3 rounded-xl border text-sm font-bold transition-all ${
                                            typeInput === "Motorbike"
                                                ? "bg-[#6246ea]/20 border-[#6246ea] text-white"
                                                : "bg-white/5 border-white/10 text-[#8790a3] hover:border-white/20"
                                        }`}
                                    >
                                        <Bike size={18} />
                                        {copy("Motorbike", "Xe máy")}
                                    </button>
                                </div>
                            </div>

                            {/* License Plate Input */}
                            <div>
                                <label className="block text-xs font-semibold text-[#8790a3] mb-1.5">
                                    {copy("License Plate", "Biển số xe")} *
                                </label>
                                <input
                                    type="text"
                                    required
                                    value={plateInput}
                                    onChange={(e) =>
                                        setPlateInput(
                                            e.target.value.toUpperCase(),
                                        )
                                    }
                                    placeholder={
                                        typeInput === "Car"
                                            ? "51A-123.45"
                                            : "59X1-234.56"
                                    }
                                    className="w-full font-mono text-base font-bold tracking-wider px-4 py-3 bg-white/5 border border-white/10 rounded-xl text-white placeholder-white/20 focus:outline-none focus:border-[#6246ea]"
                                />
                                <span className="text-[11px] text-[#8790a3] mt-1 block">
                                    {copy(
                                        "Enter standard Vietnamese plate format (e.g. 51A-123.45 or 59X1-234.56)",
                                        "Định dạng biển số chuẩn Việt Nam (VD: 51A-123.45 hoặc 59X1-234.56)",
                                    )}
                                </span>
                            </div>

                            {/* Nickname Input */}
                            <div>
                                <label className="block text-xs font-semibold text-[#8790a3] mb-1.5">
                                    {copy(
                                        "Nickname (Optional)",
                                        "Tên gợi nhớ (Không bắt buộc)",
                                    )}
                                </label>
                                <input
                                    type="text"
                                    value={nicknameInput}
                                    onChange={(e) =>
                                        setNicknameInput(e.target.value)
                                    }
                                    placeholder={copy(
                                        "e.g. Daily commuter, White Mazda",
                                        "VD: Xe đi làm, Honda City trắng",
                                    )}
                                    maxLength={50}
                                    className="w-full px-4 py-2.5 bg-white/5 border border-white/10 rounded-xl text-white placeholder-white/20 text-sm focus:outline-none focus:border-[#6246ea]"
                                />
                            </div>

                            <div className="flex items-center justify-end gap-3 pt-3 border-t border-white/10">
                                <button
                                    type="button"
                                    onClick={() => setIsAddModalOpen(false)}
                                    disabled={submitting}
                                    className="px-4 py-2.5 rounded-xl bg-white/5 hover:bg-white/10 text-white text-xs font-bold transition-colors"
                                >
                                    {copy("Cancel", "Hủy")}
                                </button>
                                <button
                                    type="submit"
                                    disabled={submitting}
                                    className="inline-flex items-center gap-2 px-5 py-2.5 rounded-xl bg-[#6246ea] hover:bg-[#7c5cff] text-white text-xs font-bold transition-colors disabled:opacity-50 shadow-glow-purple"
                                >
                                    {submitting ? (
                                        <>
                                            <Loader2
                                                size={16}
                                                className="animate-spin"
                                            />
                                            {copy("Saving...", "Đang lưu...")}
                                        </>
                                    ) : (
                                        <>
                                            <Check size={16} />
                                            {copy(
                                                "Register vehicle",
                                                "Đăng ký xe",
                                            )}
                                        </>
                                    )}
                                </button>
                            </div>
                        </form>
                    </div>
                </div>
            )}

            {/* Delete Confirmation Modal */}
            {deletingId && (
                <div className="fixed inset-0 z-50 bg-black/70 backdrop-blur-sm flex items-center justify-center p-4">
                    <div className="bg-[#0b0c17] border border-white/15 rounded-3xl max-w-sm w-full p-6 shadow-2xl space-y-4 animate-in fade-in zoom-in-95 duration-200 text-center">
                        <div className="w-12 h-12 rounded-full bg-red-500/10 border border-red-500/20 flex items-center justify-center mx-auto text-red-400">
                            <Trash2 size={24} />
                        </div>
                        <div>
                            <h2 className="text-base font-bold text-white mb-1">
                                {copy(
                                    "Delete this vehicle?",
                                    "Xác nhận xóa phương tiện?",
                                )}
                            </h2>
                            <p className="text-xs text-[#8790a3]">
                                {copy(
                                    "This vehicle will no longer be recognized at the parking gates.",
                                    "Phương tiện này sẽ không còn được nhận diện tự động tại cổng bãi đỗ.",
                                )}
                            </p>
                        </div>
                        <div className="flex items-center justify-center gap-3 pt-2">
                            <button
                                type="button"
                                onClick={() => setDeletingId(null)}
                                disabled={deleteLoading}
                                className="px-4 py-2 rounded-xl bg-white/5 hover:bg-white/10 text-white text-xs font-bold"
                            >
                                {copy("Cancel", "Hủy")}
                            </button>
                            <button
                                type="button"
                                onClick={() =>
                                    void handleDeleteVehicle(deletingId)
                                }
                                disabled={deleteLoading}
                                className="px-4 py-2 rounded-xl bg-red-600 hover:bg-red-500 text-white text-xs font-bold transition-colors"
                            >
                                {deleteLoading
                                    ? copy("Deleting...", "Đang xóa...")
                                    : copy("Delete", "Xóa")}
                            </button>
                        </div>
                    </div>
                </div>
            )}

            <ParkWiseFooter />
        </div>
    );
}
