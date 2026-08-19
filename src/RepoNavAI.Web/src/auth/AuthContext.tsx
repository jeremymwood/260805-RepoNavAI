import { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState, type PropsWithChildren } from 'react';
import { api, probeApi } from '../api/client';
import type { AuthResponse, Credentials, Registration, User } from './types';

export type ConnectionStatus = 'online' | 'reconnecting';
interface AuthValue {
  user: User|null;
  isLoading: boolean;
  hasSession: boolean;
  connectionStatus: ConnectionStatus;
  login(input: Credentials): Promise<void>;
  register(input: Registration): Promise<void>;
  acceptExternalCode(code: string): Promise<void>;
  logout(): Promise<void>;
  retryConnection(): Promise<void>;
}

const AuthContext = createContext<AuthValue|null>(null);

export function AuthProvider({ children }: PropsWithChildren) {
  const [user,setUser]=useState<User|null>(null);
  const [hasSession,setHasSession]=useState(()=>window.location.pathname !== '/auth/callback');
  const [isLoading,setLoading]=useState(true);
  const [connectionStatus,setConnectionStatus]=useState<ConnectionStatus>('online');
  const validationInFlight=useRef(false);
  const connectionStatusRef=useRef<ConnectionStatus>('online');

  const clearSession=useCallback(()=>{setHasSession(false);setUser(null);setConnectionStatus('online');setLoading(false);},[]);
  const logout=useCallback(async()=>{try{await api.post('/auth/logout',undefined,{headers:{'X-RepoNavAI-Logout':'explicit'}});}finally{clearSession();}},[clearSession]);
  const retryConnection=useCallback(async()=>{
    if(!hasSession||validationInFlight.current) return;
    validationInFlight.current=true;
    try {
      const {data}=await api.get<User>('/auth/me',{timeout:5_000});
      setUser(data);setHasSession(true);setConnectionStatus('online');
    } catch {
      // The response interceptor distinguishes unavailable APIs from rejected sessions.
    } finally { validationInFlight.current=false;setLoading(false); }
  },[hasSession]);

  useEffect(()=>{
    const unauthorized=()=>clearSession();
    const unavailable=()=>{connectionStatusRef.current='reconnecting';setConnectionStatus('reconnecting');};
    const available=()=>{connectionStatusRef.current='online';setConnectionStatus('online');};
    const online=()=>{void retryConnection();};
    window.addEventListener('auth:unauthorized',unauthorized);
    window.addEventListener('api:unavailable',unavailable);
    window.addEventListener('api:available',available);
    window.addEventListener('online',online);
    void retryConnection();
    return()=>{window.removeEventListener('auth:unauthorized',unauthorized);window.removeEventListener('api:unavailable',unavailable);window.removeEventListener('api:available',available);window.removeEventListener('online',online);};
  },[clearSession,retryConnection]);

  useEffect(()=>{
    if(!hasSession)return;
    const check=async()=>{
      const available=await probeApi();
      if(!available){connectionStatusRef.current='reconnecting';setConnectionStatus('reconnecting');return;}
      if(connectionStatusRef.current==='reconnecting')await retryConnection();
    };
    void check();
    const timer=window.setInterval(()=>{void check();},5_000);
    return()=>window.clearInterval(timer);
  },[hasSession,retryConnection]);

  const authenticate=useCallback(async(path:string,input:Credentials|Registration)=>{const{data}=await api.post<AuthResponse>(path,input);setHasSession(true);setUser(data.user);setConnectionStatus('online');setLoading(false);},[]);
  const acceptExternalCode=useCallback(async(code:string)=>{const{data}=await api.post<AuthResponse>('/auth/external/exchange',{code});setHasSession(true);setUser(data.user);setConnectionStatus('online');setLoading(false);},[]);
  const value=useMemo<AuthValue>(()=>({user,isLoading,hasSession,connectionStatus,login:(input)=>authenticate('/auth/login',input),register:(input)=>authenticate('/auth/register',input),acceptExternalCode,logout,retryConnection}),[user,isLoading,hasSession,connectionStatus,authenticate,acceptExternalCode,logout,retryConnection]);
  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth(){const value=useContext(AuthContext);if(!value)throw new Error('useAuth must be used inside AuthProvider');return value;}
