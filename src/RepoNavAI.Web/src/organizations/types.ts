export type OrganizationRole = 'Owner' | 'Administrator' | 'Member';
export interface OrganizationSummary { id: string; name: string; slug: string; role: OrganizationRole }
export interface OrganizationMember { userId: string; email: string; displayName: string; role: OrganizationRole }
export interface OrganizationDetails { id:string; name:string; slug:string; currentUserRole: OrganizationRole; members: OrganizationMember[] }
export interface InvitationResult { invitationId: string; token: string; expiresAtUtc: string }
