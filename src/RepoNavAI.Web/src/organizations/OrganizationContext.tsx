import { createContext, useContext, useEffect, useMemo, useRef, useState, type PropsWithChildren } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../api/client';
import { useAuth } from '../auth/AuthContext';
import type { OrganizationSummary } from './types';

const CURRENT_ORGANIZATION_KEY = 'reponav.current_organization';
interface OrganizationContextValue { organizations: OrganizationSummary[]; current: OrganizationSummary|null; isLoading: boolean; setCurrent(id:string):void; create(name:string):Promise<OrganizationSummary> }
const OrganizationContext = createContext<OrganizationContextValue|null>(null);

export function OrganizationProvider({children}:PropsWithChildren){
  const {user}=useAuth(); const queryClient=useQueryClient(); const[selectedId,setSelectedId]=useState(()=>sessionStorage.getItem(CURRENT_ORGANIZATION_KEY));
  const previousUserId=useRef<string|null>(null);
  const query=useQuery({queryKey:['organizations',user?.id],queryFn:async()=> (await api.get<OrganizationSummary[]>('/organizations')).data,enabled:Boolean(user)});
  const organizations=useMemo(()=>user?(query.data??[]):[],[user,query.data]);
  const current=organizations.find(x=>x.id===selectedId)??organizations[0]??null;
  useEffect(()=>{const userId=user?.id??null;if(previousUserId.current!==userId){setSelectedId(null);sessionStorage.removeItem(CURRENT_ORGANIZATION_KEY);previousUserId.current=userId;}},[user?.id]);
  useEffect(()=>{if(current){sessionStorage.setItem(CURRENT_ORGANIZATION_KEY,current.id);if(current.id!==selectedId)setSelectedId(current.id);}else if(!user){sessionStorage.removeItem(CURRENT_ORGANIZATION_KEY);setSelectedId(null);}},[current,selectedId,user]);
  const mutation=useMutation({mutationFn:async(name:string)=>(await api.post<OrganizationSummary>('/organizations',{name})).data,onSuccess:async organization=>{setSelectedId(organization.id);sessionStorage.setItem(CURRENT_ORGANIZATION_KEY,organization.id);await queryClient.invalidateQueries({queryKey:['organizations']});}});
  const value=useMemo<OrganizationContextValue>(()=>({organizations,current,isLoading:query.isLoading,setCurrent:(id)=>setSelectedId(id),create:mutation.mutateAsync}),[organizations,current,query.isLoading,mutation.mutateAsync]);
  return <OrganizationContext.Provider value={value}>{children}</OrganizationContext.Provider>;
}
export function useOrganization(){const value=useContext(OrganizationContext);if(!value)throw new Error('useOrganization must be used inside OrganizationProvider');return value;}
