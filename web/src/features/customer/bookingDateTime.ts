export function isFutureBookingStart(date: string, time: string) {
    const start = new Date(`${date}T${time}:00`).getTime();
    return Number.isFinite(start) && start > Date.now();
}
