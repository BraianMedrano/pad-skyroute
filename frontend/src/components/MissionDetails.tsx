import type { Mission, MissionStatus } from "@/lib/api/client";
import type { TelemetryMessage } from "@/lib/telemetry";

// Happy-path states of the contract, in order. `Failed` is a terminal state shown on its own.
const STEPS: MissionStatus[] = ["Requested", "Validated", "Paid", "Assigned", "InProgress", "Completed"];

type MissionDetailsProps = {
  mission?: Mission;
  drones: Record<string, TelemetryMessage>;
};

/** Details of the selected mission: state line, assigned drone, and its live position and battery. */
export function MissionDetails({ mission, drones }: MissionDetailsProps) {
  if (!mission) {
    return <p className="mt-3 text-xs text-muted">Select a mission in Fleet to follow it here.</p>;
  }

  const failed = mission.status === "Failed";
  const current = STEPS.indexOf(mission.status);
  const telemetry = mission.assignedDroneId ? drones[mission.assignedDroneId] : undefined;

  return (
    <div className="mt-3 flex flex-col gap-4">
      <dl className="flex flex-col gap-1.5 font-mono text-xs">
        <div className="flex justify-between gap-2">
          <dt className="text-muted">Mission</dt>
          <dd title={mission.id}>#{mission.id.slice(0, 8)}</dd>
        </div>
        <div className="flex justify-between gap-2">
          <dt className="text-muted">Status</dt>
          <dd className={failed ? "text-bad" : mission.status === "Completed" ? "text-ok" : "text-cyan"}>{mission.status}</dd>
        </div>
      </dl>

      <ol aria-label="Mission progress" className="flex flex-col">
        {STEPS.map((step, index) => {
          const done = !failed && index < current;
          const active = !failed && index === current;
          return (
            <li
              key={step}
              aria-current={active ? "step" : undefined}
              className={`flex items-center gap-2.5 py-1 font-mono text-xs ${active ? "text-cyan" : done ? "text-fg" : "text-muted"}`}
            >
              <span
                aria-hidden="true"
                className={`size-2.5 border ${active ? "border-cyan bg-cyan" : done ? "border-ok bg-ok" : "border-line"}`}
              />
              {step}
            </li>
          );
        })}
        {failed && (
          <li aria-current="step" className="flex items-center gap-2.5 py-1 font-mono text-xs text-bad">
            <span aria-hidden="true" className="size-2.5 border border-bad bg-bad" />
            Failed
          </li>
        )}
      </ol>
      {failed && mission.failureReason && <p className="text-xs text-bad">{mission.failureReason}</p>}

      <section aria-label="Drone" className="flex flex-col gap-1.5">
        <span className="label">Drone</span>
        {!mission.assignedDroneId ? (
          <p className="text-xs text-muted">No drone assigned yet.</p>
        ) : (
          <dl className="flex flex-col gap-1.5 font-mono text-xs">
            <div className="flex justify-between gap-2">
              <dt className="text-muted">Assigned</dt>
              <dd title={mission.assignedDroneId}>#{mission.assignedDroneId.slice(0, 8)}</dd>
            </div>
            {telemetry ? (
              <>
                <div className="flex justify-between gap-2">
                  <dt className="text-muted">Position</dt>
                  <dd>
                    {telemetry.lat.toFixed(5)}, {telemetry.lon.toFixed(5)}
                  </dd>
                </div>
                <div className="flex justify-between gap-2">
                  <dt className="text-muted">Battery</dt>
                  <dd className={telemetry.batteryPct < 20 ? "text-bad" : undefined}>{telemetry.batteryPct.toFixed(1)}%</dd>
                </div>
              </>
            ) : (
              <div className="text-muted">No telemetry received for this drone yet.</div>
            )}
          </dl>
        )}
      </section>
    </div>
  );
}
