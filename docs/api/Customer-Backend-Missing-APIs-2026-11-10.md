# Customer FE â€” pháº§n API backend thá»±c sá»± cÃ²n thiáº¿u

> **BÃ n giao cho BE, DRAFT Ä‘á»ƒ BA/PO chá»‘t pháº¡m vi.**
> **Snapshot:** `develop` = `origin/develop`, HEAD `e375f27`, 2026-11-10.
> **CÃ¡ch kiá»ƒm tra:** Ä‘á»‘i chiáº¿u mÃ n Customer trong `web/src`, route Ä‘Ã£ map vÃ  DTO/handler trong source. ÄÃ£ xem Postman MCP: cÃ¡c collection Parking/Identity chá»‰ cÃ³ má»™t pháº§n request, Booking/Payment khÃ´ng cÃ³ request trong collection hiá»‡n táº¡i; **khÃ´ng dÃ¹ng viá»‡c thiáº¿u request á»Ÿ Postman Ä‘á»ƒ káº¿t luáº­n thiáº¿u API trong code**.
> **Má»©c xÃ¡c minh:** STATIC REVIEW; chÆ°a cháº¡y API hoáº·c E2E.

## Káº¿t luáº­n trÆ°á»›c khi giao task

**KhÃ´ng xÃ¡c nháº­n Ä‘Æ°á»£c endpoint má»›i nÃ o báº¯t buá»™c Ä‘á»ƒ cháº¡y luá»“ng Customer cá»‘t lÃµi**: Ä‘Äƒng kÃ½/Ä‘Äƒng nháº­p â†’ há»“ sÆ¡/xe â†’ tÃ¬m bÃ£i â†’ táº¡o/xÃ¡c nháº­n booking â†’ xem phiÃªn â†’ thanh toÃ¡n phÃ­ â†’ hÃ³a Ä‘Æ¡n/thÃ´ng bÃ¡o. CÃ¡c route cáº§n cho luá»“ng nÃ y Ä‘Ã£ cÃ³ trong backend. Viá»‡c FE chÆ°a gá»i route Ä‘Ã£ cÃ³ sáº½ xá»­ lÃ½ á»Ÿ nhÃ¡nh FE riÃªng, **khÃ´ng giao BE viáº¿t láº¡i**.

Pháº§n BE cÃ²n thiáº¿u á»Ÿ Ä‘Ã¢y lÃ  **trÆ°á»ng/query trong contract hiá»‡n cÃ³** náº¿u muá»‘n nhá»¯ng Ä‘iá»u khiá»ƒn Ä‘ang hiá»ƒn thá»‹ trÃªn Customer UI hoáº¡t Ä‘á»™ng tháº­t. Nhá»¯ng tÃ­nh nÄƒng demo á»Ÿ cuá»‘i file chá»‰ thÃ nh task BE sau khi BA/PO Ä‘Æ°a vÃ o MVP.

## A. Contract BE cáº§n bá»• sung cho UI hiá»‡n cÃ³

### CUST-BE-01 â€” TÃ¬m bÃ£i theo ngÃ y/giá» Ä‘áº¿n cÃ¹ng bá»™ lá»c nÃ¢ng cao

- **FE cáº§n:** trang tÃ¬m bÃ£i cÃ³ Ã´ ngÃ y vÃ  giá» Ä‘áº¿n, cÃ¹ng bá»™ lá»c giÃ¡, xe, chá»— trá»‘ng, tiá»‡n Ã­ch, khoáº£ng cÃ¡ch vÃ  sort. Hiá»‡n ngÃ y/giá» chá»‰ Ä‘Æ°á»£c giá»¯ trong state; lá»i gá»i khÃ´ng gá»­i chÃºng vÃ o API tÃ¬m bÃ£i.
- **Code BE hiá»‡n cÃ³:** `GET /api/parking/lots?arrivalAt=...&vehicleType=...` lá»c bÃ£i má»Ÿ cá»­a táº¡i giá» Ä‘áº¿n; `GET /api/parking/lots/search` nháº­n bá»™ lá»c nÃ¢ng cao nhÆ°ng **khÃ´ng nháº­n `arrivalAt`**. ChÆ°a cÃ³ má»™t request há»— trá»£ cáº£ hai.
- **Äá» xuáº¥t contract tá»‘i thiá»ƒu:** thÃªm `arrivalAt` tÃ¹y chá»n (ISO 8601 cÃ³ offset) vÃ o `GET /api/parking/lots/search`, giá»¯ nguyÃªn hÃ nh vi request cÅ© khi bá» param. Trong MVP, chá»‘t rÃµ `arrivalAt` dÃ¹ng lá»c **giá» má»Ÿ cá»­a**; `availableOnly` váº«n pháº£n Ã¡nh chá»— trá»‘ng hiá»‡n táº¡i trá»« khi BA/PO duyá»‡t cÃ¡ch dá»± bÃ¡o táº¡i giá» Ä‘áº¿n. KhÃ´ng trÃ¬nh bÃ y chá»— trá»‘ng hiá»‡n táº¡i nhÆ° báº£o Ä‘áº£m cÃ³ chá»— trong tÆ°Æ¡ng lai.
- **BE cáº§n xÃ¡c nháº­n/test:** bÃ£i Approved, ngÃ y qua Ä‘Ãªm, timezone Viá»‡t Nam, ngÃ y khÃ´ng há»£p lá»‡, `vehicleType`, káº¿t há»£p `maxPrice`/`amenities`/`compatibility`, vÃ  backward compatibility. FE sáº½ ná»‘i sau khi contract Ä‘Æ°á»£c chá»‘t.
- **Báº±ng chá»©ng:** [FE ngÃ y/giá» vÃ  filter](../../web/src/features/customer/CustomerFindParkingPage.tsx), [hai route Parking](../../src/Services/Parking/Parking.WebAPI/Endpoints/ParkingLotEndpoints.cs), [SearchCustomerLotsQuery](../../src/Services/Parking/Parking.Application/ParkingLots/SearchCustomerLots/SearchCustomerLotsQuery.cs).

### CUST-BE-02 â€” Dá»¯ liá»‡u tÃ­nh nÄƒng bÃ£i trong chi tiáº¿t public

- **FE cáº§n:** trang chi tiáº¿t bÃ£i tháº­t cÃ³ vÃ¹ng tiá»‡n Ã­ch vÃ  chá»n xe trÆ°á»›c khi Ä‘áº·t chá»—. Component nháº­n `compatible` vÃ  `amenities`; vá»›i bÃ£i tháº­t cÃ¡c trÆ°á»ng nÃ y khÃ´ng Ä‘Æ°á»£c truyá»n, nÃªn tiá»‡n Ã­ch hiá»ƒn thá»‹ â€œchÆ°a cÃ´ng bá»‘â€ vÃ  FE khÃ´ng lá»c xe theo bÃ£i.
- **Code BE hiá»‡n cÃ³:** `GET /api/parking/lots/{parkingLotId}` tráº£ thÃ´ng tin cÆ¡ báº£n, giá» má»Ÿ cá»­a, giÃ¡, sá»‘ chá»— trá»‘ng; DTO **khÃ´ng cÃ³** `compatibility`, `amenities`, `prebookable`. Trong khi `GET /api/parking/lots/search` Ä‘Ã£ tráº£ ba trÆ°á»ng nÃ y, suy ra dá»¯ liá»‡u ná»n Ä‘Ã£ cÃ³; khÃ´ng cáº§n táº¡o endpoint chi tiáº¿t thá»© hai.
- **Äá» xuáº¥t contract tá»‘i thiá»ƒu:** bá»• sung `compatibility: string[]`, `amenities: string[]`, `prebookable: bool` vÃ o response chi tiáº¿t public, cÃ¹ng Ä‘á»‹nh nghÄ©a giÃ¡ trá»‹ khi bÃ£i chÆ°a khai bÃ¡o. FE dÃ¹ng cÃ¡c trÆ°á»ng nÃ y Ä‘á»ƒ áº©n/khÃ³a lá»±a chá»n khÃ´ng phÃ¹ há»£p. **Booking hiá»‡n chá»‰ nháº­n Ã´ tÃ´**; khÃ´ng suy ra ráº±ng `compatibility` cho xe mÃ¡y nghÄ©a lÃ  xe mÃ¡y Ä‘Æ°á»£c Ä‘áº·t trÆ°á»›c.
- **BE cáº§n xÃ¡c nháº­n/test:** chá»‰ bÃ£i Approved Ä‘Æ°á»£c public, giÃ¡ trá»‹ chi tiáº¿t khá»›p káº¿t quáº£ search cho cÃ¹ng bÃ£i, bÃ£i khÃ´ng cÃ³ feature tráº£ máº£ng rá»—ng/giÃ¡ trá»‹ rÃµ rÃ ng, khÃ´ng lá»™ dá»¯ liá»‡u cá»§a bÃ£i Pending/Rejected. FE pháº£i kiá»ƒm tra Car-only theo contract Booking.
- **Báº±ng chá»©ng:** [FE chi tiáº¿t bÃ£i](../../web/src/features/customer/CustomerParkingLotDetailPage.tsx), [component tiá»‡n Ã­ch/chá»n xe](../../web/src/features/customer/ParkingLotDetailExperience.tsx), [PublicParkingLotDetailResponse](../../src/Services/Parking/Parking.WebAPI/Endpoints/ParkingLotEndpoints.cs), [SearchCustomerLotsQuery](../../src/Services/Parking/Parking.Application/ParkingLots/SearchCustomerLots/SearchCustomerLotsQuery.cs).

## B. TÃ­nh nÄƒng trÃªn UI nhÆ°ng **chÆ°a cÃ³ API** â€” chá»‰ giao BE náº¿u Ä‘Æ°a vÃ o MVP

| Äá» má»¥c | Báº±ng chá»©ng FE / hiá»‡n tráº¡ng BE | API cáº§n thiáº¿t náº¿u Ä‘Æ°á»£c duyá»‡t |
|---|---|---|
| Äá»‹a Ä‘iá»ƒm Ä‘Ã£ lÆ°u cá»§a Customer | Dashboard dÃ¹ng máº£ng `savedPlaces` cá»‘ Ä‘á»‹nh; khÃ´ng tÃ¬m tháº¥y entity/route saved places hoáº·c favorites trong cÃ¡c service. UI hiá»‡n gáº¯n nhÃ£n dá»¯ liá»‡u máº«u. | Cáº§n contract tÃ i nguyÃªn thuá»™c account: liá»‡t kÃª, lÆ°u vÃ  xÃ³a Ä‘á»‹a Ä‘iá»ƒm; sá»­a nhÃ£n náº¿u UI cho sá»­a. BE/BA chá»‘t service sá»Ÿ há»¯u, giá»›i háº¡n sá»‘ báº£n ghi, chá»‘ng trÃ¹ng, Ä‘á»‹a chá»‰/tá»a Ä‘á»™ vÃ  quyá»n chá»‰ chá»§ tÃ i khoáº£n Ä‘á»c/ghi. **ChÆ°a áº¥n Ä‘á»‹nh path** trÆ°á»›c khi chá»‘t. |
| áº¢nh tháº­t cá»§a tá»«ng bÃ£i | Chi tiáº¿t bÃ£i tháº­t Ä‘ang dÃ¹ng cÃ¹ng má»™t áº£nh tÄ©nh. Response public vÃ  luá»“ng Ä‘Äƒng kÃ½ bÃ£i chÆ°a cÃ³ media/photo. | Chá»‰ náº¿u áº£nh bÃ£i thuá»™c MVP: BE thiáº¿t káº¿ upload private cho Owner, kiá»ƒm duyá»‡t/public URL an toÃ n vÃ  `photos` trong chi tiáº¿t bÃ£i; FE thay áº£nh máº«u. Chá»‘t size/type, quyá»n sá»Ÿ há»¯u, sá»‘ áº£nh vÃ  vÃ²ng Ä‘á»i file trÆ°á»›c khi lÃ m. |

**Báº±ng chá»©ng:** [Dashboard Customer](../../web/src/features/customer/CustomerDashboardPage.tsx), [áº£nh tÄ©nh trong chi tiáº¿t](../../web/src/features/customer/CustomerParkingLotDetailPage.tsx), [ParkingLotFeatures hiá»‡n cÃ³](../../src/Services/Parking/Parking.Domain/ParkingLots/ParkingLotFeatures.cs).

## C. KhÃ´ng giao thÃ nh â€œAPI má»›iâ€

- **Thanh toÃ¡n phÃ­ phiÃªn, sÆ¡ Ä‘á»“ bÃ£i, tÃ¬m bÃ£i nhiá»u bá»™ lá»c, vÃ­, hÃ³a Ä‘Æ¡n, phiÃªn, booking, xe, thÃ´ng bÃ¡o:** endpoint Ä‘Ã£ cÃ³ trong source. FE chÆ°a gá»i má»™t sá»‘ route, nhÆ°ng Ä‘Ã³ lÃ  viá»‡c tÃ­ch há»£p/kiá»ƒm thá»­, khÃ´ng pháº£i thiáº¿u API.
- **Ã” Ä‘á»— cá»¥ thá»ƒ khi booking:** UI preview cÃ³ chá»n Ã´, nhÆ°ng trang chi tiáº¿t bÃ£i tháº­t khÃ´ng hiá»ƒn thá»‹ sÆ¡ Ä‘á»“ chá»n Ã´. `POST /api/booking/holds` hiá»‡n giá»¯ chá»— theo bÃ£i, khÃ´ng nháº­n `slotId`. FE Ä‘ang cÃ³ thÃ´ng bÃ¡o máº·c Ä‘á»‹nh â€œA-01â€ sau booking; cáº§n sá»­a ná»™i dung FE. Chá»‰ náº¿u BA/PO Ä‘á»•i nghiá»‡p vá»¥ sang **Ä‘áº·t Ã´ cá»¥ thá»ƒ**, má»›i giao BE thiáº¿t káº¿ contract, transaction vÃ  concurrency cho slot, rá»“i cáº­p nháº­t FE.
- **Upcoming Booking trÃªn Dashboard:** hiá»‡n hiá»ƒn thá»‹ dá»¯ liá»‡u máº«u; cÃ³ thá»ƒ dÃ¹ng `GET /api/booking/reservations/me` Ä‘Ã£ cÃ³, khÃ´ng cáº§n endpoint dashboard riÃªng chá»‰ Ä‘á»ƒ láº¥y má»™t booking.
- **VNeID trÃªn Complete Profile:** hiá»‡n ghi rÃµ lÃ  demo qua simulator/session storage; khÃ´ng gá»i Ä‘Ã¢y lÃ  API production cÃ²n thiáº¿u náº¿u chÆ°a cÃ³ scope xÃ¡c minh VNeID tháº­t.

## D. Blocker BE Ä‘á»™c láº­p vá»›i API má»›i: phÃ¢n quyá»n payment status

`GET /api/payment/parking-fee-payments/{transactionId}` **Ä‘Ã£ tá»“n táº¡i**, nhÆ°ng policy hiá»‡n chá»‰ xÃ©t role; handler chá»‰ nháº­n transaction ID vÃ  tráº£ dá»¯ liá»‡u giao dá»‹ch mÃ  khÃ´ng kiá»ƒm tra tÃ i khoáº£n/phiÃªn thuá»™c ngÆ°á»i gá»i. Cáº§n sá»­a phÃ¢n quyá»n theo resource vÃ  test Customer A khÃ´ng Ä‘á»c Ä‘Æ°á»£c giao dá»‹ch cá»§a Customer B **trÆ°á»›c khi FE ná»‘i endpoint**. Endpoint receipt theo session Ä‘Ã£ Ä‘Æ°á»£c FE gá»i cÅ©ng cáº§n test trÆ°á»ng há»£p payment khÃ´ng gáº¯n account; handler hiá»‡n chá»‰ tá»« chá»‘i khi hai account ID Ä‘á»u cÃ³ giÃ¡ trá»‹ vÃ  khÃ¡c nhau. ÄÃ¢y lÃ  viá»‡c BE sá»­a/kiá»ƒm chá»©ng API hiá»‡n cÃ³, khÃ´ng pháº£i táº¡o endpoint má»›i.

**Báº±ng chá»©ng:** [PaymentAuthorization](../../src/Services/Payment/Payment.WebAPI/Security/PaymentAuthorization.cs), [payment status endpoint](../../src/Services/Payment/Payment.WebAPI/Endpoints/ParkingFeeEndpoints.cs), [status handler](../../src/Services/Payment/Payment.Application/Features/ParkingFeePayments/Queries/GetParkingFeePaymentStatus/GetParkingFeePaymentStatusQueryHandler.cs), [receipt handler](../../src/Services/Payment/Payment.Application/Features/ParkingSessions/Queries/GetParkingFeeReceipt/GetParkingFeeReceiptQueryHandler.cs).

## Quyáº¿t Ä‘á»‹nh cáº§n chá»‘t trÆ°á»›c khi BE code

1. BA/PO xÃ¡c nháº­n `arrivalAt` trong search chá»‰ lá»c giá» má»Ÿ cá»­a hay cáº§n dá»± bÃ¡o chá»— trá»‘ng tÆ°Æ¡ng lai.
2. BE/FE thá»‘ng nháº¥t `compatibility`, `amenities`, `prebookable` trong response chi tiáº¿t vÃ  quy táº¯c Car-only cá»§a booking.
3. BA/PO quyáº¿t Ä‘á»‹nh Saved Places vÃ  áº£nh bÃ£i cÃ³ thuá»™c MVP hay váº«n lÃ  ná»™i dung demo. Danh sÃ¡ch â€œSuggested placesâ€ hiá»‡n lÃ  gá»£i Ã½ cá»‘ Ä‘á»‹nh; khÃ´ng suy ra cáº§n API lá»‹ch sá»­ tÃ¬m kiáº¿m cÃ¡ nhÃ¢n.
4. BE xá»­ lÃ½ phÃ¢n quyá»n payment status/receipt trÆ°á»›c E2E thanh toÃ¡n Customer.

KhÃ´ng tá»± Ä‘Ã¡nh dáº¥u Done tá»« tÃ i liá»‡u nÃ y. Sau khi contract Ä‘Æ°á»£c duyá»‡t, cáº§n test API trÃªn mÃ´i trÆ°á»ng cÃ´ láº­p vÃ  smoke E2E FEâ€“Gatewayâ€“BE; ghi rÃµ PASS/FAIL vÃ  mÃ´i trÆ°á»ng.
