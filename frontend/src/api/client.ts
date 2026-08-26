export type HttpMethod = "DELETE" | "GET" | "PATCH" | "POST" | "PUT";

export interface ApiProblem {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  instance?: string;
  errors?: Record<string, string[]>;
}

export class ApiError extends Error {
  readonly status: number;
  readonly problem?: ApiProblem;

  constructor(status: number, problem?: ApiProblem) {
    super(problem?.detail ?? problem?.title ?? `Request failed with status ${status}.`);
    this.name = "ApiError";
    this.status = status;
    this.problem = problem;
  }
}

export interface ApiRequestOptions extends Omit<RequestInit, "body" | "method"> {
  method?: HttpMethod;
  body?: unknown;
  actorMatricule?: string;
  signal?: AbortSignal;
}

const apiBaseUrl = (import.meta.env.VITE_API_BASE_URL ?? "").replace(/\/$/, "");

export async function apiRequest<T>(
  path: string,
  options: ApiRequestOptions = {},
): Promise<T> {
  const { actorMatricule, body, method, ...requestInit } = options;
  const headers = new Headers(requestInit.headers);
  headers.set("Accept", "application/json");
  if (body !== undefined) {
    headers.set("Content-Type", "application/json");
  }
  if (actorMatricule) {
    headers.set("X-Actor-Matricule", actorMatricule);
  }

  const response = await fetch(`${apiBaseUrl}${path}`, {
    ...requestInit,
    method: method ?? "GET",
    headers,
    body: body === undefined ? undefined : JSON.stringify(body),
  });

  if (!response.ok) {
    const problem = await readProblem(response);
    throw new ApiError(response.status, problem);
  }

  if (response.status === 204) {
    return undefined as T;
  }

  try {
    return (await response.json()) as T;
  } catch (error) {
    throw new ApiError(response.status, {
      title: "Invalid API response",
      detail: error instanceof Error ? error.message : "The server returned invalid JSON.",
    });
  }
}

async function readProblem(response: Response): Promise<ApiProblem | undefined> {
  const contentType = response.headers.get("content-type") ?? "";
  if (!contentType.includes("json")) {
    return undefined;
  }

  try {
    return (await response.json()) as ApiProblem;
  } catch {
    return undefined;
  }
}

export function getApiBaseUrl(): string {
  return apiBaseUrl;
}
