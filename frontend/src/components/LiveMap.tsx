"use client";

import { useEffect, useMemo, useRef, useState } from "react";
import { api, apiBaseUrl, type Mission } from "@/lib/api/client";
import { connectTelemetry, type TelemetryMessage } from "@/lib/telemetry";
import { FlightMap, type MissionRoute } from "./FlightMap";

/** Live map: drones from the telemetry hub, and the route of each mission being flown. */
export function LiveMap() {
  const [drones, setDrones] = useState<Record<string, TelemetryMessage>>({});
  const [missions, setMissions] = useState<Record<string, Mission>>({});
  const requestedMissions = useRef(new Set<string>());

  useEffect(
    () =>
      connectTelemetry(`${apiBaseUrl}/api/hubs/telemetry`, (message) =>
        setDrones((current) => ({ ...current, [message.droneId]: message })),
      ),
    [],
  );

  // Telemetry only carries the mission id, so fetch each mission once for its route.
  useEffect(() => {
    for (const { missionId } of Object.values(drones)) {
      if (!missionId || requestedMissions.current.has(missionId)) continue;
      requestedMissions.current.add(missionId);
      api.getMission(missionId).then(
        (mission) => setMissions((current) => ({ ...current, [missionId]: mission })),
        // Try again on a later update.
        () => requestedMissions.current.delete(missionId),
      );
    }
  }, [drones]);

  const droneList = useMemo(() => Object.values(drones), [drones]);

  const routes = useMemo(
    () =>
      droneList.flatMap((drone): MissionRoute[] => {
        const mission = drone.missionId ? missions[drone.missionId] : undefined;
        return mission ? [{ missionId: mission.id, origin: mission.origin, destination: mission.destination }] : [];
      }),
    [droneList, missions],
  );

  return <FlightMap drones={droneList} routes={routes} />;
}
