/* The provider and hook intentionally share one identity boundary. */
/* eslint-disable react-refresh/only-export-components */
import type { ReactNode } from "react";
import { createContext, useContext, useMemo, useState } from "react";

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

export function IdentityProvider({ children }: { children: ReactNode }) {
  const [identity, setIdentity] = useState<Identity | null>(null);
  const value = useMemo(
    () => ({ identity, setIdentity, clearIdentity: () => setIdentity(null) }),
    [identity],
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
