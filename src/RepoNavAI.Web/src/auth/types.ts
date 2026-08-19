export interface User { id: string; email: string; displayName: string; roles: string[] }
export interface AuthResponse { expiresAtUtc: string; user: User }
export interface Credentials { email: string; password: string }
export interface Registration extends Credentials { displayName: string }
export interface ExternalProvider { id: string; displayName: string; enabled: boolean }
