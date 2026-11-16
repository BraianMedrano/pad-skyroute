import { HubConnectionBuilder, LogLevel } from "@microsoft/signalr";
import type { DroneStatus } from "./api/client";

// Payload of the hub's TelemetryUpdated event (docs/architecture.md, "Real-time telemetry").
// It is not part of the OpenAPI document, so it is typed by hand.
export type TelemetryMessage = {
  droneId: string;
  missionId: string | null;
  lat: number;
  lon: number;
  /** 0 = north, 90 = east. */
  headingDeg: number;
  batteryPct: number;
  status: DroneStatus;
  timestamp: string;
};

/**
 * Connects to the telemetry hub and calls `onMessage` for every update.
 * SignalR reconnects if an established connection is interrupted. An initial
 * connection failure is reported and requires a new connection attempt by the caller.
 * Returns a function that disconnects.
 */
export function connectTelemetry(hubUrl: string, onMessage: (message: TelemetryMessage) => void): () => void {
  const connection = new HubConnectionBuilder()
    .withUrl(hubUrl)
    // Use SignalR's standard retry policy for interrupted connections.
    .withAutomaticReconnect()
    // Keep SignalR's routine connection logs quiet; initial failures are reported below.
    .configureLogging(LogLevel.Critical)
    .build();

  connection.on("TelemetryUpdated", onMessage);

  let stopped = false;
  void connection.start().catch((error: unknown) => {
    if (!stopped) {
      console.error("Unable to connect to the telemetry hub. Check that the API is running and reload to retry.", error);
    }
  });

  return () => {
    stopped = true;
    void connection.stop();
  };
}
