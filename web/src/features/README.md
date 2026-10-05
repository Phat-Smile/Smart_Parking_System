# Feature ownership

Business UI belongs in `features/<feature-name>/`. Future owners include `auth`, `profile`, `vehicles`, `parking`, `map`, `parking-3d`, `booking`, `parking-session`, `payment`, and `notifications`. Create each directory only when its Vertical Slice begins. Shared transport and rendering primitives live under `src/shared/` and contain no parking business behavior.
