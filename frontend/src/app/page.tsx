"use client";

import { useState } from "react";
import { FleetPanel } from "@/components/FleetPanel";
import { LiveMap } from "@/components/LiveMap";
import { MissionDetails } from "@/components/MissionDetails";
import type { Mission } from "@/lib/api/client";
import type { TelemetryMessage } from "@/lib/telemetry";

export default function Home() {
  // The selection, the polled missions and the live drones are shared by the three panels.
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [missions, setMissions] = useState<Mission[] | null>(null);
  const [drones, setDrones] = useState<Record<string, TelemetryMessage>>({});
  const selected = missions?.find((mission) => mission.id === selectedId);

  return (
    <div className="grid h-dvh grid-rows-[auto_minmax(0,1fr)]">
      <header className="flex items-center gap-x-6 border-b border-line bg-panel px-[18px] py-3">
        <span className="flex items-center gap-2.5 font-display text-xl font-bold tracking-[0.2em] uppercase before:size-2 before:bg-amber before:content-['']">
          SkyRoute
        </span>
        <span className="ml-auto font-mono text-xs text-muted">San Miguel de Tucumán</span>
      </header>

      <main className="grid min-h-0 grid-rows-[auto_minmax(320px,1fr)_auto] min-[960px]:grid-cols-[300px_minmax(0,1fr)_250px] min-[960px]:grid-rows-1">
        <aside
          aria-label="Fleet"
          className="max-h-[45dvh] overflow-y-auto border-b border-line bg-panel p-3.5 min-[960px]:max-h-none min-[960px]:border-r min-[960px]:border-b-0"
        >
          <span className="label">Fleet</span>
          <FleetPanel selectedId={selectedId} onSelect={setSelectedId} onMissionsChange={setMissions} />
        </aside>

        <section className="min-h-80 min-w-0">
          <LiveMap selectedMission={selected} onDronesChange={setDrones} />
        </section>

        <aside
          aria-label="Details"
          className="border-t border-line bg-panel p-4 min-[960px]:border-t-0 min-[960px]:border-l"
        >
          <span className="label">Details</span>
          <MissionDetails mission={selected} drones={drones} />
        </aside>
      </main>
    </div>
  );
}
