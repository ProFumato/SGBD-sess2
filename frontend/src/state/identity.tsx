/* The provider and hook intentionally share one identity boundary. */
/* eslint-disable react-refresh/only-export-components */
import type { ReactNode } from "react";
import { createContext, useCallback, useContext, useMemo, useState } from "react";

export interface Identity {
  member: {
    matricule: string;
    displayName: string;
    membershipCategory: "Global" | "Site" | "Free";
    homeSiteId: number | null;
    isActive: boolean;
  };
  administratorRole?: {
    scope: "Global" | "Site";
    siteId: number | null;
  };
}

interface IdentityContextValue {
  identity: Identity | null;
  setIdentity: (identity: Identity) => void;
  clearIdentity: () => void;
}

const IdentityContext = createContext<IdentityContextValue | undefined>(undefined);
const identityStorageKey = "padel-court-management.identity";

function readStoredIdentity(): Identity | null {
  const stored = sessionStorage.getItem(identityStorageKey);
  if (!stored) return null;

  try {
    const parsed: unknown = JSON.parse(stored);
    if (
      typeof parsed === "object" &&
      parsed !== null &&
      "member" in parsed &&
      typeof parsed.member === "object" &&
      parsed.member !== null &&
      "matricule" in parsed.member &&
      typeof parsed.member.matricule === "string"
    ) {
      return parsed as Identity;
    }
  } catch {
    sessionStorage.removeItem(identityStorageKey);
  }

  return null;
}

export function IdentityProvider({ children }: { children: ReactNode }) {
  const [identity, setIdentityState] = useState<Identity | null>(readStoredIdentity);
  const setIdentity = useCallback((nextIdentity: Identity) => {
    sessionStorage.setItem(identityStorageKey, JSON.stringify(nextIdentity));
    setIdentityState(nextIdentity);
  }, []);
  const clearIdentity = useCallback(() => {
    sessionStorage.removeItem(identityStorageKey);
    setIdentityState(null);
  }, []);
  const value = useMemo(
    () => ({ identity, setIdentity, clearIdentity }),
    [clearIdentity, identity, setIdentity],
  );
  return <IdentityContext.Provider value={value}>{children}</IdentityContext.Provider>;
}

export function useIdentity(): IdentityContextValue {
  const context = useContext(IdentityContext);
  if (!context) {
    throw new Error("useIdentity must be used inside IdentityProvider.");
  }
  return context;
}
