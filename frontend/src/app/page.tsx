"use client";

import { useCallback, useState } from "react";
import { FleetPanel } from "@/components/FleetPanel";
import { LiveMap } from "@/components/LiveMap";
import { MissionDetails } from "@/components/MissionDetails";
import type { Coordinates, Mission } from "@/lib/api/client";
import type { TelemetryMessage } from "@/lib/telemetry";

type Section = "operations" | "delivery" | "missions";

export default function Home() {
  const [section, setSection] = useState<Section>("operations");
  const [night, setNight] = useState(true);
  const [armedEndpoint, setArmedEndpoint] = useState<"origin" | "destination" | null>(null);
  const [endpoints, setEndpoints] = useState<{ origin?: Coordinates; destination?: Coordinates }>({});
  const [selectedId, setSelectedId] = useState<string | null>(null);
  // Summary of the last request, shown in Operaciones after switching there to pay.
  const [requested, setRequested] = useState<string | null>(null);
  const [missions, setMissions] = useState<Mission[] | null>(null);
  const [drones, setDrones] = useState<Record<string, TelemetryMessage>>({});
  const selected = missions?.find((mission) => mission.id === selectedId);
  const updateEndpoints = useCallback((origin?: Coordinates, destination?: Coordinates) => setEndpoints({ origin, destination }), []);
  const sections: { id: Section; label: string }[] = [
    { id: "operations", label: "Operaciones" },
    { id: "delivery", label: "Nueva entrega" },
    { id: "missions", label: "Misiones" },
  ];

  return (
    <div data-theme={night ? "night" : "day"} className="grid h-dvh grid-rows-[auto_auto_minmax(0,1fr)]">
      <header className="flex flex-wrap items-center gap-x-6 gap-y-2 border-b border-line bg-panel px-4 py-3 sm:px-6">
        <span className="flex items-center gap-2.5 font-display text-xl font-bold tracking-[0.2em] uppercase before:size-2 before:bg-amber before:content-['']">SkyRoute</span>
        <span className="ml-auto font-mono text-xs text-muted">San Miguel de Tucumán</span>
        <button type="button" aria-pressed={!night} onClick={() => setNight(!night)} className="rounded border border-line px-3 py-1.5 text-xs text-fg hover:border-amber">{night ? "Modo día" : "Modo noche"}</button>
      </header>
      <nav aria-label="Secciones principales" className="flex overflow-x-auto border-b border-line bg-panel px-3 sm:px-6">
        {sections.map(({ id, label }) => <button key={id} type="button" aria-pressed={section === id} onClick={() => { setSection(id); setRequested(null); if (id !== "delivery") setArmedEndpoint(null); }} className={`whitespace-nowrap border-b-2 px-4 py-3 font-display text-sm font-semibold tracking-wider ${section === id ? "border-amber text-amber" : "border-transparent text-muted hover:text-fg"}`}>{label}</button>)}
      </nav>
      <main className={`relative min-h-0 ${section === "operations" ? "grid grid-rows-[minmax(220px,1fr)_auto] min-[960px]:grid-cols-[280px_minmax(0,1fr)_250px] min-[960px]:grid-rows-1" : section === "delivery" ? "grid grid-rows-[auto_minmax(320px,1fr)] min-[960px]:grid-cols-[360px_minmax(0,1fr)] min-[960px]:grid-rows-1" : "grid grid-rows-[minmax(0,1fr)_auto] min-[960px]:grid-cols-[minmax(0,1fr)_300px] min-[960px]:grid-rows-1"}`}>
        <aside aria-label={section === "operations" ? "Flota y misiones" : section === "delivery" ? "Nueva entrega" : "Misiones"} className={`z-10 min-h-0 overflow-y-auto border-line bg-panel p-4 ${section === "operations" ? "min-[960px]:border-r" : "min-[960px]:border-r"}`}>
          {section === "operations" && <h1 className="label">Flota y misiones</h1>}
          {section === "operations" && requested && <p role="status" className="mt-2 border border-ok/40 p-2 text-xs text-ok">{requested} Pagala desde su tarjeta.</p>}
          {section === "missions" && <h1 className="label">Misiones</h1>}
          <FleetPanel selectedId={selectedId} onSelect={setSelectedId} onMissionsChange={setMissions} mode={section === "delivery" ? "delivery" : "missions"} compact={section === "operations"} selectedEndpoint={armedEndpoint} onArmedChange={setArmedEndpoint} onEndpointsChange={updateEndpoints} onRequested={(mission, summary) => { setSelectedId(mission.id); setRequested(summary); setArmedEndpoint(null); setSection("operations"); }} />
        </aside>
        <section aria-label="Mapa de operaciones" className={`min-h-[220px] min-w-0 ${section === "missions" ? "invisible absolute inset-0" : ""}`}>
          <LiveMap selectedMission={selected} onDronesChange={setDrones} theme={night ? "night" : "day"} picking={section === "delivery" && armedEndpoint !== null} origin={endpoints.origin} destination={endpoints.destination} />
        </section>
        {(section === "operations" || section === "missions") && <aside aria-label="Detalle de misión seleccionada" className="z-10 min-h-0 overflow-y-auto border-t border-line bg-panel p-4 min-[960px]:border-l min-[960px]:border-t-0"><h2 className="label">Detalle de misión</h2><MissionDetails mission={selected} drones={drones} /></aside>}
      </main>
    </div>
  );
}
