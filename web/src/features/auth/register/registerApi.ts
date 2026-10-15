import {
    registerParkWiseAccount,
    type ParkWiseRegisteredAccount,
    type RegisterAccountRequest,
} from "../registerParkWiseAccount";

export type RegisteredAccount = ParkWiseRegisteredAccount;
export function registerAccount(request: RegisterAccountRequest) {
    return registerParkWiseAccount(request);
}
