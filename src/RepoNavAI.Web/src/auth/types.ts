export interface User { id: string; email: string; displayName: string; roles: string[] }
export interface AuthResponse { accessToken: string; expiresAtUtc: string; user: User }
export interface Credentials { email: string; password: string }
export interface Registration extends Credentials { displayName: string }
