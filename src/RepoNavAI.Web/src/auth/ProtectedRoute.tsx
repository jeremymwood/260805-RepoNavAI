import { Navigate, Outlet, useLocation } from 'react-router-dom';
import { useAuth } from './AuthContext';
export function ProtectedRoute(){const{user,isLoading}=useAuth();const location=useLocation();if(isLoading)return <div className="grid min-h-screen place-items-center"><div className="h-8 w-8 animate-spin rounded-full border-2 border-brand-600 border-t-transparent" aria-label="Loading"/></div>;return user?<Outlet/>:<Navigate to="/login" replace state={{from:location}}/>;}
