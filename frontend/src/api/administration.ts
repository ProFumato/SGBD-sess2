import { apiRequest } from "./client";

export type MembershipCategory = "Global" | "Site" | "Free";
export type AdministratorScope = "Global" | "Site";

export interface AdminMember {
  memberId: number;
  matricule: string;
  displayName: string;
  membershipCategory: MembershipCategory;
  homeSiteId: number | null;
  isActive: boolean;
}

export function getMembers(actorMatricule: string): Promise<AdminMember[]> {
  return apiRequest<AdminMember[]>("/api/admin/members", { actorMatricule });
}

export function createMember(actorMatricule: string, input: Omit<AdminMember, "memberId">): Promise<AdminMember> {
  return apiRequest<AdminMember>("/api/admin/members", { method: "POST", actorMatricule, body: input });
}

export function updateMember(
  actorMatricule: string,
  matricule: string,
  input: Omit<AdminMember, "memberId">,
): Promise<AdminMember> {
  return apiRequest<AdminMember>(`/api/admin/members/${encodeURIComponent(matricule)}`, {
    method: "PUT",
    actorMatricule,
    body: input,
  });
}

export function setMemberActivation(actorMatricule: string, matricule: string, isActive: boolean): Promise<void> {
  return apiRequest<void>(`/api/admin/members/${encodeURIComponent(matricule)}/activation`, {
    method: "PUT",
    actorMatricule,
    body: { isActive },
  });
}

export function setAdministratorRole(
  actorMatricule: string,
  matricule: string,
  scope: AdministratorScope,
  siteId: number | null,
): Promise<void> {
  return apiRequest<void>(`/api/admin/members/${encodeURIComponent(matricule)}/administrator-role`, {
    method: "PUT",
    actorMatricule,
    body: { scope, siteId },
  });
}

export function removeAdministratorRole(actorMatricule: string, matricule: string): Promise<void> {
  return apiRequest<void>(`/api/admin/members/${encodeURIComponent(matricule)}/administrator-role`, {
    method: "DELETE",
    actorMatricule,
  });
}
