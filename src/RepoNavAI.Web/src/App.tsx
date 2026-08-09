import { Navigate, Route, Routes } from 'react-router-dom';
import { ProtectedRoute } from './auth/ProtectedRoute';
import { DashboardPage, OrganizationSettingsRoute, ProfileSettingsPage, WorkspaceOverviewPage } from './pages/DashboardPage';
import { OrganizationMembersPage, OrganizationSettingsPage } from './pages/OrganizationPages';
import { LoginPage } from './pages/LoginPage';
import { RegisterPage } from './pages/RegisterPage';
import { AcceptInvitationPage } from './pages/AcceptInvitationPage';
export function App(){return <Routes><Route path="/login" element={<LoginPage/>}/><Route path="/register" element={<RegisterPage/>}/><Route element={<ProtectedRoute/>}><Route element={<DashboardPage/>}><Route index element={<WorkspaceOverviewPage/>}/><Route path="repositories" element={<Navigate to="/#repositories" replace/>}/><Route path="organization/members" element={<OrganizationMembersPage/>}/><Route element={<OrganizationSettingsRoute/>}><Route path="organization/settings" element={<OrganizationSettingsPage/>}/></Route><Route path="settings/profile" element={<ProfileSettingsPage/>}/></Route><Route path="/invitations/:token" element={<AcceptInvitationPage/>}/></Route><Route path="*" element={<Navigate to="/" replace/>}/></Routes>;}
