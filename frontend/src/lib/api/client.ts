import type { components } from "./schema";

// Types generated from the API's OpenAPI contract. Regenerate with `pnpm api:types`.
type Schemas = components["schemas"];

export type Coordinates = Schemas["Coordinates"];
export type Drone = Schemas["DroneResponse"];
export type DroneStatus = Schemas["DroneStatus"];
export type Mission = Schemas["MissionResponse"];
export type MissionStatus = Schemas["MissionStatus"];
export type MissionRequest = Schemas["RequestMissionRequest"];
export type PaymentResponse = Schemas["PaymentResponse"];
export type ProblemDetails = Schemas["ProblemDetails"];

// Host of the API; every route lives under /api.
const baseUrl = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5113";

/** A non-2xx response. `message` carries the API's reason (ProblemDetails `detail`) when it sends one. */
export class ApiError extends Error {
  readonly status: number;
  readonly problem: ProblemDetails | undefined;

  constructor(status: number, problem: ProblemDetails | undefined) {
    super(problem?.detail ?? problem?.title ?? `Request failed with status ${status}`);
    this.name = "ApiError";
    this.status = status;
    this.problem = problem;
  }
}

async function request<T>(path: string, init: RequestInit = {}): Promise<T> {
  const response = await fetch(`${baseUrl}${path}`, {
    ...init,
    // Only send a content type with a body, so plain GETs need no CORS preflight.
    headers: init.body ? { "Content-Type": "application/json" } : undefined,
  });

  if (!response.ok) {
    const problem = (await response.json().catch(() => undefined)) as ProblemDetails | undefined;
    throw new ApiError(response.status, problem);
  }

  return (await response.json()) as T;
}

export const api = {
  listDrones: () => request<Drone[]>("/api/drones"),

  listMissions: () => request<Mission[]>("/api/missions"),

  getMission: (id: string) => request<Mission>(`/api/missions/${encodeURIComponent(id)}`),

  requestMission: (body: MissionRequest) =>
    request<Mission>("/api/missions", { method: "POST", body: JSON.stringify(body) }),

  /** Starts the Padi payment and returns its checkout link; follow the mission with `getMission`. */
  payMission: (id: string) =>
    request<PaymentResponse>(`/api/missions/${encodeURIComponent(id)}/payment`, { method: "POST" }),
};
