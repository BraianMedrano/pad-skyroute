"use client";

import { useCallback, useEffect, useRef, useState, type FormEvent } from "react";
import { api, ApiError, type Coordinates, type Mission, type MissionStatus } from "@/lib/api/client";

const POLL_INTERVAL_MS = 3000;

// The fake payment gateway (UseFakes=true) answers with a link on this host; it is not a real checkout.
const FAKE_CHECKOUT_HOST = "fake-payments.local";

const priceFormat = new Intl.NumberFormat("es-AR", {
  style: "currency",
  currency: "ARS",
  currencyDisplay: "code",
  maximumFractionDigits: 0,
});

const statusLabel: Record<MissionStatus, string> = { Requested: "Solicitada", Validated: "Validada", Paid: "Pagada", Assigned: "Dron asignado", InProgress: "En curso", Completed: "Completada", Failed: "Fallida" };

const statusColor: Record<MissionStatus, string> = {
  Requested: "text-muted",
  Validated: "text-amber",
  Paid: "text-cyan",
  Assigned: "text-cyan",
  InProgress: "text-cyan",
  Completed: "text-ok",
  Failed: "text-bad",
};

const inputClass =
  "w-full min-w-0 border border-line bg-bg px-2 py-1 font-mono text-xs text-fg placeholder:text-muted";

type Notice = { kind: "ok" | "error"; text: string };
type PayNote = { kind: "simulated" | "checkout" | "error"; text: string; url?: string };

function describeError(error: unknown): string {
  if (error instanceof ApiError) {
    return error.status === 422 ? `Solicitud rechazada: ${error.message}` : `La solicitud falló (${error.status}): ${error.message}`;
  }
  return "No se pudo conectar con el servicio.";
}

function parseHttpUrl(value: string): URL | undefined {
  try {
    const url = new URL(value);
    return url.protocol === "https:" || url.protocol === "http:" ? url : undefined;
  } catch {
    return undefined;
  }
}

type FleetPanelProps = {
  selectedId?: string | null;
  onSelect?: (id: string) => void;
  /** Called with every refreshed list, so other panels can show the same missions. */
  onMissionsChange?: (missions: Mission[]) => void;
  mode?: "all" | "delivery" | "missions";
  compact?: boolean;
  onArmedChange?: (endpoint: "origin" | "destination" | null) => void;
  selectedEndpoint?: "origin" | "destination" | null;
  onEndpointsChange?: (origin?: Coordinates, destination?: Coordinates) => void;
};

/** Fleet panel: request a delivery, and follow every mission (polled) with its price and a Pay button. */
export function FleetPanel({ selectedId, onSelect, onMissionsChange, mode = "all", compact = false, onArmedChange, selectedEndpoint, onEndpointsChange }: FleetPanelProps = {}) {
  const [missions, setMissions] = useState<Mission[] | null>(null);
  const [listFailed, setListFailed] = useState(false);
  const loading = useRef(false);

  const [originLat, setOriginLat] = useState("");
  const [originLon, setOriginLon] = useState("");
  const [destinationLat, setDestinationLat] = useState("");
  const [destinationLon, setDestinationLon] = useState("");
  const [weight, setWeight] = useState("1.5");
  const [dni, setDni] = useState("12345678");
  const [submitting, setSubmitting] = useState(false);
  const [notice, setNotice] = useState<Notice | null>(null);

  const [payingId, setPayingId] = useState<string | null>(null);
  const [payNotes, setPayNotes] = useState<Record<string, PayNote>>({});

  const refresh = useCallback(async () => {
    if (loading.current) return;
    loading.current = true;
    try {
      const list = await api.listMissions();
      setMissions(list);
      onMissionsChange?.(list);
      setListFailed(false);
    } catch {
      setListFailed(true);
    } finally {
      loading.current = false;
    }
  }, [onMissionsChange]);

  useEffect(() => {
    void refresh();
    const timer = setInterval(() => void refresh(), POLL_INTERVAL_MS);
    return () => clearInterval(timer);
  }, [refresh]);

  useEffect(() => {
    onEndpointsChange?.(originLat && originLon ? { lat: Number(originLat), lon: Number(originLon) } : undefined, destinationLat && destinationLon ? { lat: Number(destinationLat), lon: Number(destinationLon) } : undefined);
  }, [originLat, originLon, destinationLat, destinationLon, onEndpointsChange]);

  useEffect(() => {
    if (!selectedEndpoint) return;
    const handlePoint = (event: Event) => {
      const { lat, lon } = (event as CustomEvent<{ lat: number; lon: number }>).detail;
      if (selectedEndpoint === "origin") { setOriginLat(String(lat)); setOriginLon(String(lon)); }
      else { setDestinationLat(String(lat)); setDestinationLon(String(lon)); }
      onArmedChange?.(null);
    };
    window.addEventListener("skyroute-map-point", handlePoint);
    return () => window.removeEventListener("skyroute-map-point", handlePoint);
  }, [selectedEndpoint, onArmedChange]);

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!originLat || !originLon || !destinationLat || !destinationLon) return;
    setSubmitting(true);
    setNotice(null);
    try {
      const mission = await api.requestMission({
        origin: { lat: Number(originLat), lon: Number(originLon) },
        destination: { lat: Number(destinationLat), lon: Number(destinationLon) },
        packageWeightKg: Number(weight),
        recipientDni: dni.trim(),
      });
      setNotice({
        kind: "ok",
        text: `Entrega #${mission.id.slice(0, 8)} solicitada: ${statusLabel[mission.status]}, ${priceFormat.format(mission.price)}.`,
      });
      void refresh();
    } catch (error) {
      setNotice({ kind: "error", text: describeError(error) });
    } finally {
      setSubmitting(false);
    }
  }

  async function pay(id: string) {
    setPayingId(id);
    try {
      const { checkoutUrl } = await api.payMission(id);
      const url = parseHttpUrl(checkoutUrl);
      let note: PayNote;
      if (url?.hostname === FAKE_CHECKOUT_HOST) {
        note = {
          kind: "simulated",
          text: "Pago simulado (pasarela ficticia): se confirma automáticamente en unos segundos.",
        };
      } else if (url) {
        window.open(url.href, "_blank", "noopener,noreferrer");
        note = { kind: "checkout", text: "Esperando confirmación del pago.", url: url.href };
      } else {
        note = { kind: "error", text: "La API devolvió un enlace de pago no válido." };
      }
      setPayNotes((current) => ({ ...current, [id]: note }));
    } catch (error) {
      setPayNotes((current) => ({ ...current, [id]: { kind: "error", text: describeError(error) } }));
    } finally {
      setPayingId(null);
      void refresh();
    }
  }

  return (
    <div className="mt-3 flex flex-col gap-5">
      {mode !== "missions" && <form onSubmit={submit} className={`flex flex-col gap-3 ${compact ? "hidden" : ""}`} aria-label="Solicitar una entrega">
        <span className="label">Nueva entrega</span>
        <div className="grid grid-cols-2 gap-2">
          <button type="button" aria-pressed={selectedEndpoint === "origin"} onClick={() => onArmedChange?.(selectedEndpoint === "origin" ? null : "origin")} className={`border p-2 text-left text-xs ${selectedEndpoint === "origin" ? "border-amber text-amber" : "border-line text-fg"}`}>Elegir origen<span aria-live="polite" className="block font-mono text-muted">{originLat ? `${Number(originLat).toFixed(4)}, ${Number(originLon).toFixed(4)}` : "Sin seleccionar"}</span></button>
          <button type="button" aria-pressed={selectedEndpoint === "destination"} onClick={() => onArmedChange?.(selectedEndpoint === "destination" ? null : "destination")} className={`border p-2 text-left text-xs ${selectedEndpoint === "destination" ? "border-cyan text-cyan" : "border-line text-fg"}`}>Elegir destino<span aria-live="polite" className="block font-mono text-muted">{destinationLat ? `${Number(destinationLat).toFixed(4)}, ${Number(destinationLon).toFixed(4)}` : "Sin seleccionar"}</span></button>
        </div>
        {selectedEndpoint && <p role="status" className="text-xs text-amber">Hacé clic en el mapa para elegir el {selectedEndpoint === "origin" ? "origen" : "destino"}.</p>}

        <div className="grid grid-cols-2 gap-2">
          <label className="flex flex-col gap-1 font-mono text-xs text-muted">
            Peso (kg)
            <input type="number" step="any" required className={inputClass} value={weight} onChange={(e) => setWeight(e.target.value)} />
          </label>
          <label className="flex flex-col gap-1 font-mono text-xs text-muted">
            DNI del destinatario
            <input type="text" inputMode="numeric" required className={inputClass} value={dni} onChange={(e) => setDni(e.target.value)} />
          </label>
        </div>

        <button
          type="submit"
          disabled={submitting || !originLat || !originLon || !destinationLat || !destinationLon}
          className="bg-amber px-3 py-1.5 font-display text-sm font-semibold tracking-[0.14em] text-on-amber uppercase disabled:opacity-50"
        >
          {submitting ? "Enviando…" : "Solicitar entrega"}
        </button>

        {notice && (
          <p role={notice.kind === "error" ? "alert" : "status"} className={`text-xs ${notice.kind === "error" ? "text-bad" : "text-ok"}`}>
            {notice.text}
          </p>
        )}
      </form>}

      {mode !== "delivery" && <section aria-label="Misiones" className="flex flex-col gap-2">
        <span className="label">Misiones</span>
        {listFailed && <p className="text-xs text-bad">No se pudieron cargar las misiones. Reintentando…</p>}
        {missions?.length === 0 && <p className="text-xs text-muted">Todavía no hay misiones.</p>}
        <ul className="flex flex-col gap-2">
          {/* The API lists in creation order; show the newest first. */}
          {missions?.toReversed().map((mission) => {
            const note = payNotes[mission.id];
            const selected = mission.id === selectedId;
            return (
              <li key={mission.id} className={`border bg-panel-2 p-2.5 ${selected ? "border-amber" : "border-line"}`}>
                <button
                  type="button"
                  onClick={() => onSelect?.(mission.id)}
                  aria-pressed={selected}
                  aria-label={`Seleccionar misión ${mission.id.slice(0, 8)}`}
                  className="flex w-full items-center justify-between gap-2 text-left"
                >
                  <span className="font-mono text-xs" title={mission.id}>
                    #{mission.id.slice(0, 8)}
                  </span>
                  <span className={`font-display text-[13px] font-semibold tracking-[0.12em] uppercase ${statusColor[mission.status]}`}>
                    {statusLabel[mission.status]}
                  </span>
                </button>
                <div className="mt-1.5 flex flex-col items-start gap-1.5">
                  <span className="font-mono text-xs text-muted">{priceFormat.format(mission.price)}</span>
                  {mission.status === "Validated" && !note && (
                    <>
                    <p className="text-xs text-muted">Padi se abrirá en otra pestaña; puede pedirte que inicies sesión.</p>
                    <button
                      type="button"
                      onClick={() => void pay(mission.id)}
                      disabled={payingId === mission.id}
                      className="border border-amber px-2.5 py-0.5 font-display text-[13px] font-semibold tracking-[0.14em] text-amber uppercase disabled:opacity-50"
                    >
                      {payingId === mission.id ? "Conectando con Padi…" : "Pagar con Padi"}
                    </button>
                    </>
                  )}
                </div>
                {mission.status === "Failed" && mission.failureReason && (
                  <p className="mt-1.5 text-xs text-bad">{mission.failureReason}</p>
                )}
                {note && (
                  <p className={`mt-1.5 text-xs ${note.kind === "error" ? "text-bad" : "text-muted"}`}>
                    {note.text}
                    {note.url && (
                      <>
                        {" "}
                        <a href={note.url} target="_blank" rel="noopener noreferrer" className="text-cyan underline">
                          Abrir Padi
                        </a>
                      </>
                    )}
                  </p>
                )}
              </li>
            );
          })}
        </ul>
      </section>}
    </div>
  );
}
