# Service boundaries

This document assigns logical responsibility only; entities, tables, endpoints, and business behavior are not implemented here.

## Identity

Owns:
- Account
- Authentication
- Profile
- Roles
- Registered Vehicles
- OTP lifecycle
- Refresh sessions/tokens

Must not own:
- ParkingSession
- Reservation
- Wallet
- Payment

Authentication baseline: email and password are required; phone number is optional. Email registration uses OTP, and Google Authentication is supported. Profile FullName is present; PhoneNumber, CitizenIdNumber, and DriverLicenseNumber are optional. CCCD/GPLX are not mandatory.

## Parking

Owns:
- ParkingLot
- Floor
- Zone
- Spot
- Layout
- OperatingHours
- Pricing configuration
- ParkingSession
- SessionQR
- Entry/Exit
- Occupancy
- Spot Assignment
- Realtime parking state

Must not own:
- Account authentication
- Reservation lifecycle
- Wallet
- Payment transaction

## Booking

Owns:
- Hold
- Scheduled Booking
- Temporary Capacity Hold
- Reservation Capacity
- Cancellation
- No-show
- Reservation lifecycle

## Payment

Owns:
- Wallet
- Ledger
- Payment Transaction
- VNPay Transaction
- Deposit
- Booking Prepayment
- Parking Payment
- Refund
- Payment Idempotency

## Notification

Owns:
- Notification
- Delivery
- Email notification
- Reminder

OTP lifecycle remains owned by Identity.

## Registered users and guests

Registered users have an account and may search parking, use AI recommendations and view 2D/3D layouts, then drive in, hold, or book. A guest needs no account and goes directly to the lot; guests do not use search, AI, holds, or bookings in the main flow. Guest is not a database role and must not receive a synthetic account.

## Parking session QR invariants

Each ParkingSession receives its own session QR. SessionQR belongs to ParkingSession only, not to User or Vehicle, and is not fixed or reused.

Exit requires a license plate match, a valid session QR, and completed payment. On successful exit, the ParkingSession closes, its QR is invalidated, spot/capacity is released, and realtime state is updated.
