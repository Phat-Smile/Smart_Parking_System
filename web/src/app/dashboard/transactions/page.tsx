"use client";

import { vi } from "../../../features/customer/i18n/vi";
import { useState } from "react";
import { ArrowDownLeft, ArrowUpRight, Info } from "lucide-react";

type TxType = 'topup' | 'deposit' | 'prepay' | 'fee' | 'refund' | 'apply_prepay' | 'lose_prepay';

interface Transaction {
    id: string;
    type: TxType;
    amount: number;
    balanceAfter: number;
    reason: string;
    refCode: string;
    date: string;
}

const mockTransactions: Transaction[] = [
    { id: "TX10", type: "lose_prepay", amount: 50000, balanceAfter: 150000, reason: "Mất trả trước do không đến đỗ xe (BK-1002)", refCode: "SYS-LP-881", date: "Hôm nay 16:30" },
    { id: "TX09", type: "apply_prepay", amount: 50000, balanceAfter: 150000, reason: "Áp dụng trả trước vào phí đỗ xe (BK-0999)", refCode: "SYS-AP-882", date: "Hôm qua 11:45" },
    { id: "TX08", type: "fee", amount: -25000, balanceAfter: 150000, reason: "Thanh toán phần phí đỗ xe còn lại (BK-0999)", refCode: "SYS-FEE-883", date: "Hôm qua 11:45" },
    { id: "TX07", type: "prepay", amount: -50000, balanceAfter: 175000, reason: "Trả trước 1 giờ đỗ xe (BK-0999)", refCode: "SYS-PRE-884", date: "Hôm qua 09:00" },
    { id: "TX06", type: "refund", amount: 20000, balanceAfter: 225000, reason: "Hoàn cọc giữ chỗ (BK-0999)", refCode: "SYS-REF-885", date: "Hôm qua 09:00" },
    { id: "TX05", type: "deposit", amount: -20000, balanceAfter: 205000, reason: "Cọc giữ chỗ 15 phút (BK-0999)", refCode: "SYS-DEP-886", date: "Hôm qua 08:30" },
    { id: "TX04", type: "topup", amount: 200000, balanceAfter: 225000, reason: "Nạp tiền từ VNPay", refCode: "VNP-12345678", date: "Hôm qua 08:25" },
];

export default function TransactionsPage() {
    const [filter, setFilter] = useState<string>('all');

    const filteredTxs = filter === 'all' 
        ? mockTransactions 
        : mockTransactions.filter(tx => tx.type === filter);

    const getIcon = (type: TxType) => {
        if (['topup', 'refund'].includes(type)) return <ArrowDownLeft className="text-[#34d399]" size={20} />;
        if (['deposit', 'prepay', 'fee'].includes(type)) return <ArrowUpRight className="text-[#ff9388]" size={20} />;
        return <Info className="text-[#8790a3]" size={20} />; // for apply/lose prepay
    };

    const formatMoney = (amount: number, type: TxType) => {
        const isNeutral = type === 'apply_prepay' || type === 'lose_prepay';
        const sign = amount > 0 && !isNeutral ? '+' : '';
        const color = isNeutral ? 'text-[#8790a3]' : (amount > 0 ? 'text-[#34d399]' : 'text-white');
        return <span className={`font-bold ${color}`}>{sign}{new Intl.NumberFormat('vi-VN').format(amount)}đ</span>;
    };

    return (
        <div className="animate-in fade-in duration-500 space-y-6">
            <h1 className="text-2xl font-bold text-white mb-6">{vi.transactionsPage.title}</h1>

            {/* Filter Tabs */}
            <div className="flex overflow-x-auto pb-2 gap-2 hide-scrollbar">
                {Object.entries(vi.transactionsPage.filters).map(([key, label]) => (
                    <button
                        key={key}
                        onClick={() => setFilter(key)}
                        className={`px-4 py-2 rounded-full text-sm font-medium whitespace-nowrap transition-colors ${
                            filter === key 
                                ? "bg-[#6246ea] text-white" 
                                : "bg-[#101426] text-[#8790a3] border border-white/10 hover:bg-white/10"
                        }`}
                    >
                        {label}
                    </button>
                ))}
            </div>

            {/* List */}
            <div className="bg-[#0b0c17] border border-white/10 rounded-2xl shadow-card-light overflow-hidden">
                {filteredTxs.length === 0 ? (
                    <div className="p-8 text-center text-[#8790a3]">{vi.transactionsPage.empty}</div>
                ) : (
                    <div className="divide-y divide-white/5">
                        {filteredTxs.map(tx => {
                            const isNeutral = tx.type === 'apply_prepay' || tx.type === 'lose_prepay';
                            return (
                                <div key={tx.id} className={`p-4 md:p-6 flex flex-col md:flex-row md:items-center justify-between gap-4 transition-colors hover:bg-white/[0.02] ${isNeutral ? 'opacity-70' : ''}`}>
                                    <div className="flex items-start gap-4">
                                        <div className={`w-10 h-10 rounded-full flex items-center justify-center flex-shrink-0 mt-1 ${isNeutral ? 'bg-white/5' : (tx.amount > 0 ? 'bg-[#10b981]/10' : 'bg-white/10')}`}>
                                            {getIcon(tx.type)}
                                        </div>
                                        <div>
                                            <div className="flex items-center gap-2 mb-1">
                                                <h4 className="text-white font-medium">{tx.reason}</h4>
                                                {isNeutral && (
                                                    <span className="text-[10px] bg-[#6246ea]/20 text-[#8a6bff] border border-[#6246ea]/30 px-1.5 py-0.5 rounded">
                                                        {vi.transactionsPage.explain}
                                                    </span>
                                                )}
                                            </div>
                                            <div className="text-[#8790a3] text-sm flex gap-3">
                                                <span>{tx.date}</span>
                                                <span>•</span>
                                                <span className="font-mono">Mã: {tx.refCode}</span>
                                            </div>
                                        </div>
                                    </div>
                                    <div className="flex flex-row md:flex-col items-center md:items-end justify-between md:justify-center pl-14 md:pl-0">
                                        <div className="text-lg">{formatMoney(tx.amount, tx.type)}</div>
                                        <div className="text-[#8790a3] text-sm mt-1">
                                            SD: {new Intl.NumberFormat('vi-VN').format(tx.balanceAfter)}đ
                                        </div>
                                    </div>
                                </div>
                            );
                        })}
                    </div>
                )}
            </div>
        </div>
    );
}
