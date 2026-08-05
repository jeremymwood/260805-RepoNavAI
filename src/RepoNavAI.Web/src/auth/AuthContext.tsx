import { createContext, useCallback, useContext, useEffect, useMemo, useState, type PropsWithChildren } from 'react';
import { api, TOKEN_KEY } from '../api/client';
import type { AuthResponse, Credentials, Registration, User } from './types';
interface AuthValue { user: User|null; isLoading: boolean; login(input: Credentials): Promise<void>; register(input: Registration): Promise<void>; logout(): void }
const AuthContext = createContext<AuthValue|null>(null);
export function AuthProvider({ children }: PropsWithChildren) {
  const [user,setUser]=useState<User|null>(null); const [isLoading,setLoading]=useState(()=>Boolean(sessionStorage.getItem(TOKEN_KEY)));
  const logout=useCallback(()=>{sessionStorage.removeItem(TOKEN_KEY);setUser(null);},[]);
  useEffect(()=>{const unauthorized=()=>logout();window.addEventListener('auth:unauthorized',unauthorized);const token=sessionStorage.getItem(TOKEN_KEY);if(token)api.get<User>('/auth/me').then(({data})=>setUser(data)).catch(logout).finally(()=>setLoading(false));return()=>window.removeEventListener('auth:unauthorized',unauthorized);},[logout]);
  const authenticate=useCallback(async(path:string,input:Credentials|Registration)=>{const{data}=await api.post<AuthResponse>(path,input);sessionStorage.setItem(TOKEN_KEY,data.accessToken);setUser(data.user);},[]);
  const value=useMemo<AuthValue>(()=>({user,isLoading,login:(input)=>authenticate('/auth/login',input),register:(input)=>authenticate('/auth/register',input),logout}),[user,isLoading,authenticate,logout]);
  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}
export function useAuth(){const value=useContext(AuthContext);if(!value)throw new Error('useAuth must be used inside AuthProvider');return value;}
