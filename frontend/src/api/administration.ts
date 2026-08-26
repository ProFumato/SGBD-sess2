import { apiRequest } from "./client";

export type MembershipCategory = "Global" | "Site" | "Free";
export type AdministratorScope = "Global" | "Site";
export type ClosureScope = "Global" | "Site";

export interface AdminMember {
  memberId: number;
  matricule: string;
  displayName: string;
  membershipCategory: MembershipCategory;
  homeSiteId: number | null;
  isActive: boolean;
}

export interface Site { siteId: number; name: string; }
export interface Court { courtId: number; siteId: number; name: string; isActive: boolean; }
export interface Schedule { siteAnnualScheduleId: number; siteId: number; calendarYear: number; openingTime: string; closingTime: string; }
export interface Closure { closureId: number; scope: ClosureScope; siteId: number | null; startsAt: string; endsAt: string; reason: string; }

export function getSites(actorMatricule: string): Promise<Site[]> {
  return apiRequest<Site[]>("/api/admin/sites", { actorMatricule });
}
export function createSite(actorMatricule: string, name: string): Promise<Site> {
  return apiRequest<Site>("/api/admin/sites", { method: "POST", actorMatricule, body: { name } });
}
export function updateSite(actorMatricule: string, siteId: number, name: string): Promise<Site> {
  return apiRequest<Site>(`/api/admin/sites/${siteId}`, { method: "PUT", actorMatricule, body: { name } });
}
export function getCourts(actorMatricule: string, siteId: number): Promise<Court[]> {
  return apiRequest<Court[]>(`/api/admin/sites/${siteId}/courts`, { actorMatricule });
}
export function createCourt(actorMatricule: string, siteId: number, name: string, isActive = true): Promise<Court> {
  return apiRequest<Court>(`/api/admin/sites/${siteId}/courts`, { method: "POST", actorMatricule, body: { name, isActive } });
}
export function updateCourt(actorMatricule: string, courtId: number, name: string, isActive: boolean): Promise<Court> {
  return apiRequest<Court>(`/api/admin/courts/${courtId}`, { method: "PUT", actorMatricule, body: { name, isActive } });
}
export function getSchedules(actorMatricule: string, siteId: number): Promise<Schedule[]> {
  return apiRequest<Schedule[]>(`/api/admin/sites/${siteId}/schedules`, { actorMatricule });
}
export function setSchedule(actorMatricule: string, siteId: number, year: number, openingTime: string, closingTime: string): Promise<Schedule> {
  return apiRequest<Schedule>(`/api/admin/sites/${siteId}/schedules/${year}`, { method: "PUT", actorMatricule, body: { openingTime, closingTime } });
}
export function deleteSchedule(actorMatricule: string, siteId: number, year: number): Promise<void> {
  return apiRequest<void>(`/api/admin/sites/${siteId}/schedules/${year}`, { method: "DELETE", actorMatricule });
}
export function getClosures(actorMatricule: string): Promise<Closure[]> {
  return apiRequest<Closure[]>("/api/admin/closures", { actorMatricule });
}
export function createClosure(actorMatricule: string, input: Omit<Closure, "closureId">): Promise<Closure> {
  return apiRequest<Closure>("/api/admin/closures", { method: "POST", actorMatricule, body: input });
}
export function deleteClosure(actorMatricule: string, closureId: number): Promise<void> {
  return apiRequest<void>(`/api/admin/closures/${closureId}`, { method: "DELETE", actorMatricule });
}
export function updateClosure(actorMatricule: string, closureId: number, input: Omit<Closure, "closureId">): Promise<Closure> {
  return apiRequest<Closure>(`/api/admin/closures/${closureId}`, { method: "PUT", actorMatricule, body: input });
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
