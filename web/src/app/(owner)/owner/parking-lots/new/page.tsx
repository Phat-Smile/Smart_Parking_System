"use client";

import React, { useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { parkingLotSchema, ParkingLotFormData } from "../../../../../domain/schemas";
import { useRouter } from "next/navigation";

const steps = ["Thông tin chung", "Bảng giá & Chính sách", "Xác nhận"];

export default function NewParkingLotWizard() {
  const [currentStep, setCurrentStep] = useState(0);
  const router = useRouter();

  const {
    register,
    handleSubmit,
    trigger,
    formState: { errors, isValid },
  } = useForm<ParkingLotFormData>({
    resolver: zodResolver(parkingLotSchema),
    mode: "onBlur",
    defaultValues: {
      location: { lat: 10.7946, lng: 106.7225 },
      operatingHours: { open: "08:00", close: "22:00", is24Hours: false },
      vehicleTypes: ["sedan"],
      pricing: { baseRate: 20000 },
      holdDeposit: 50000,
      reservationPolicy: { allowReservations: true, holdDurationMinutes: 30 },
    }
  });

  const onNext = async () => {
    const fieldsToValidate = currentStep === 0 
      ? ["name", "address"] as const
      : currentStep === 1
      ? ["pricing.baseRate", "holdDeposit"] as const
      : [];
      
    const isStepValid = await trigger(fieldsToValidate);
    if (isStepValid) {
      setCurrentStep((prev) => Math.min(prev + 1, steps.length - 1));
    }
  };

  const onSubmit = (data: ParkingLotFormData) => {
    // In a real scenario, this would be a mutation
    console.log("Submitting:", data);
    alert("Đã gửi yêu cầu phê duyệt bãi đỗ xe thành công!");
    router.push("/owner/parking-lots");
  };

  return (
    <div className="max-w-3xl mx-auto space-y-8">
      <div>
        <h1 className="text-2xl font-bold tracking-tight text-owner-dark">Thêm bãi đỗ xe mới</h1>
        <p className="text-owner-text-secondary mt-1">
          Làm theo các bước để thiết lập bãi đỗ xe và gửi yêu cầu phê duyệt.
        </p>
      </div>

      {/* Progress */}
      <nav aria-label="Progress">
        <ol role="list" className="space-y-4 md:flex md:space-y-0 md:space-x-8">
          {steps.map((step, index) => (
            <li key={step} className="md:flex-1">
              <div className={`group flex flex-col border-l-4 py-2 pl-4 md:border-l-0 md:border-t-4 md:pl-0 md:pt-4 md:pb-0 ${
                index <= currentStep ? 'border-owner-primary' : 'border-owner-border'
              }`}>
                <span className={`text-sm font-medium ${index <= currentStep ? 'text-owner-dark' : 'text-owner-text-secondary'}`}>
                  Bước {index + 1}
                </span>
                <span className="text-sm font-medium">{step}</span>
              </div>
            </li>
          ))}
        </ol>
      </nav>

      {/* Form */}
      <form onSubmit={handleSubmit(onSubmit)} className="bg-white shadow-sm border border-owner-border rounded-xl p-6 sm:p-8 space-y-6">
        {currentStep === 0 && (
          <div className="space-y-6">
            <h2 className="text-lg font-medium text-owner-dark">Thông tin chung</h2>
            
            <div className="space-y-4">
              <div>
                <label className="block text-sm font-medium text-owner-dark">Tên bãi đỗ</label>
                <input
                  {...register("name")}
                  className="mt-1 block w-full border border-owner-border rounded-md shadow-sm py-2 px-3 focus:outline-none focus:ring-owner-primary focus:border-owner-primary sm:text-sm"
                  placeholder="Vd: Vincom Landmark 81"
                />
                {errors.name && <p className="mt-1 text-sm text-status-occupied">{errors.name.message}</p>}
              </div>

              <div>
                <label className="block text-sm font-medium text-owner-dark">Địa chỉ</label>
                <input
                  {...register("address")}
                  className="mt-1 block w-full border border-owner-border rounded-md shadow-sm py-2 px-3 focus:outline-none focus:ring-owner-primary focus:border-owner-primary sm:text-sm"
                  placeholder="Vd: 720A Điện Biên Phủ..."
                />
                {errors.address && <p className="mt-1 text-sm text-status-occupied">{errors.address.message}</p>}
              </div>
            </div>
          </div>
        )}

        {currentStep === 1 && (
          <div className="space-y-6">
            <h2 className="text-lg font-medium text-owner-dark">Bảng giá & Chính sách</h2>
            
            <div className="space-y-4">
              <div>
                <label className="block text-sm font-medium text-owner-dark">Giá cơ bản (VNĐ/giờ)</label>
                <input
                  type="number"
                  {...register("pricing.baseRate", { valueAsNumber: true })}
                  className="mt-1 block w-full border border-owner-border rounded-md shadow-sm py-2 px-3 focus:outline-none focus:ring-owner-primary focus:border-owner-primary sm:text-sm"
                />
                {errors.pricing?.baseRate && <p className="mt-1 text-sm text-status-occupied">{errors.pricing.baseRate.message}</p>}
              </div>
              
              <div>
                <label className="block text-sm font-medium text-owner-dark">Tiền cọc giữ chỗ (VNĐ)</label>
                <input
                  type="number"
                  {...register("holdDeposit", { valueAsNumber: true })}
                  className="mt-1 block w-full border border-owner-border rounded-md shadow-sm py-2 px-3 focus:outline-none focus:ring-owner-primary focus:border-owner-primary sm:text-sm"
                />
              </div>
            </div>
          </div>
        )}

        {currentStep === 2 && (
          <div className="space-y-6">
            <h2 className="text-lg font-medium text-owner-dark">Xác nhận thông tin</h2>
            <p className="text-sm text-owner-text-secondary">
              Vui lòng kiểm tra kỹ thông tin trước khi gửi phê duyệt. 
              Bạn có thể xem trước mô hình 2D/3D (Placeholder) tại đây.
            </p>
            <div className="h-64 bg-owner-bg rounded-lg border border-dashed border-owner-border flex items-center justify-center text-owner-text-secondary">
              Layout Preview (2D/3D)
            </div>
          </div>
        )}

        <div className="pt-5 flex justify-end space-x-3 border-t border-owner-border">
          {currentStep > 0 && (
            <button
              type="button"
              onClick={() => setCurrentStep((prev) => prev - 1)}
              className="bg-white py-2 px-4 border border-owner-border rounded-md shadow-sm text-sm font-medium text-owner-dark hover:bg-gray-50 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-owner-primary"
            >
              Quay lại
            </button>
          )}
          {currentStep < steps.length - 1 ? (
            <button
              type="button"
              onClick={onNext}
              className="ml-3 inline-flex justify-center py-2 px-4 border border-transparent shadow-sm text-sm font-medium rounded-md text-white bg-owner-dark hover:bg-opacity-90 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-owner-primary"
            >
              Tiếp tục
            </button>
          ) : (
            <button
              type="submit"
              className="ml-3 inline-flex justify-center py-2 px-4 border border-transparent shadow-sm text-sm font-medium rounded-md text-white bg-owner-primary !text-owner-dark hover:bg-opacity-90 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-owner-primary"
            >
              Gửi phê duyệt
            </button>
          )}
        </div>
      </form>
    </div>
  );
}
