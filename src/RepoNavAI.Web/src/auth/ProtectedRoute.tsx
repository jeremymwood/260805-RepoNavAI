import { useEffect, useRef, useState } from 'react';
import { CheckCircle2, RefreshCw, WifiOff } from 'lucide-react';
import { Navigate, Outlet, useLocation } from 'react-router-dom';
import { useAuth } from './AuthContext';

export function ProtectedRoute(){
  const{user,isLoading,hasSession,connectionStatus,retryConnection}=useAuth();
  const location=useLocation();
  const wasReconnecting=useRef(false);
  const [showRecovered,setShowRecovered]=useState(false);
  useEffect(()=>{
    if(connectionStatus==='reconnecting'){wasReconnecting.current=true;setShowRecovered(false);return;}
    if(!wasReconnecting.current)return;
    wasReconnecting.current=false;setShowRecovered(true);
    const timer=window.setTimeout(()=>setShowRecovered(false),4_000);
    return()=>window.clearTimeout(timer);
  },[connectionStatus]);
  if(isLoading)return <Loading label="Checking your session"/>;
  if(!user&&hasSession&&connectionStatus==='reconnecting')return <Reconnect onRetry={()=>void retryConnection()}/>;
  if(!user)return <Navigate to="/login" replace state={{from:location}}/>;
  return <>{connectionStatus==='reconnecting'&&<OfflineBanner/>}{showRecovered&&<RecoveredBanner/>}<Outlet/></>;
}

function Loading({label}:{label:string}){return <div className="grid min-h-screen place-items-center"><div className="text-center"><div className="mx-auto h-8 w-8 animate-spin rounded-full border-2 border-brand-600 border-t-transparent"/><p className="mt-3 text-sm text-slate-500">{label}</p></div></div>;}

function Reconnect({onRetry}:{onRetry:()=>void}){return <main className="grid min-h-screen place-items-center bg-canvas px-6"><div className="max-w-md text-center" role="status" aria-live="polite"><WifiOff className="mx-auto text-brand-600" size={42}/><h1 className="mt-5 text-2xl font-semibold text-ink">RepoNav AI is reconnecting</h1><p className="mt-3 text-sm leading-6 text-slate-500">Your session is safe. We’ll restore your workspace automatically when the API is available.</p><button type="button" className="primary-button mx-auto mt-6 max-w-48" onClick={onRetry}><RefreshCw size={17}/>Retry now</button></div></main>;}

function OfflineBanner(){return <div className="connection-banner connection-banner-offline" role="status" aria-live="polite"><RefreshCw className="animate-spin motion-reduce:animate-none" size={15}/>Connection lost. Reconnecting…</div>;}

function RecoveredBanner(){return <div className="connection-banner connection-banner-recovered" role="status" aria-live="polite"><CheckCircle2 size={16}/>Successfully reconnected</div>;}
