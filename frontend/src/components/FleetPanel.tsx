"use client";

import { useCallback, useEffect, useRef, useState, type FormEvent } from "react";
import { api, ApiError, type Mission, type MissionStatus } from "@/lib/api/client";

const POLL_INTERVAL_MS = 3000;

// The fake payment gateway (UseFakes=true) answers with a link on this host; it is not a real checkout.
const FAKE_CHECKOUT_HOST = "fake-payments.local";

const priceFormat = new Intl.NumberFormat("es-AR", {
  style: "currency",
  currency: "ARS",
  currencyDisplay: "code",
  maximumFractionDigits: 0,
});

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
    return error.status === 422 ? `Rejected: ${error.message}` : `Request failed (${error.status}): ${error.message}`;
  }
  return "Could not reach the API.";
}

function parseHttpUrl(value: string): URL | undefined {
  try {
    const url = new URL(value);
    return url.protocol === "https:" || url.protocol === "http:" ? url : undefined;
  } catch {
    return undefined;
  }
}

/** Fleet panel: request a delivery, and follow every mission (polled) with its price and a Pay button. */
export function FleetPanel() {
  const [missions, setMissions] = useState<Mission[] | null>(null);
  const [listFailed, setListFailed] = useState(false);
  const loading = useRef(false);

  const [originLat, setOriginLat] = useState("-26.8305");
  const [originLon, setOriginLon] = useState("-65.2039");
  const [destinationLat, setDestinationLat] = useState("-26.8240");
  const [destinationLon, setDestinationLon] = useState("-65.2200");
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
      setMissions(await api.listMissions());
      setListFailed(false);
    } catch {
      setListFailed(true);
    } finally {
      loading.current = false;
    }
  }, []);

  useEffect(() => {
    void refresh();
    const timer = setInterval(() => void refresh(), POLL_INTERVAL_MS);
    return () => clearInterval(timer);
  }, [refresh]);

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
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
        text: `Mission #${mission.id.slice(0, 8)} requested: ${mission.status}, ${priceFormat.format(mission.price)}.`,
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
          text: "Simulated payment (fake gateway): it is confirmed automatically in a few seconds.",
        };
      } else if (url) {
        window.open(url.href, "_blank", "noopener,noreferrer");
        note = { kind: "checkout", text: "Checkout opened. The status updates once the payment is confirmed.", url: url.href };
      } else {
        note = { kind: "error", text: "The API returned an unusable checkout link." };
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
      <form onSubmit={submit} className="flex flex-col gap-3" aria-label="Request a mission">
        <span className="label">Request mission</span>

        <fieldset className="grid grid-cols-2 gap-2">
          <legend className="mb-1 font-mono text-xs text-muted">Origin</legend>
          <input aria-label="Origin latitude" placeholder="lat" type="number" step="any" required className={inputClass} value={originLat} onChange={(e) => setOriginLat(e.target.value)} />
          <input aria-label="Origin longitude" placeholder="lon" type="number" step="any" required className={inputClass} value={originLon} onChange={(e) => setOriginLon(e.target.value)} />
        </fieldset>

        <fieldset className="grid grid-cols-2 gap-2">
          <legend className="mb-1 font-mono text-xs text-muted">Destination</legend>
          <input aria-label="Destination latitude" placeholder="lat" type="number" step="any" required className={inputClass} value={destinationLat} onChange={(e) => setDestinationLat(e.target.value)} />
          <input aria-label="Destination longitude" placeholder="lon" type="number" step="any" required className={inputClass} value={destinationLon} onChange={(e) => setDestinationLon(e.target.value)} />
        </fieldset>

        <div className="grid grid-cols-2 gap-2">
          <label className="flex flex-col gap-1 font-mono text-xs text-muted">
            Weight (kg)
            <input type="number" step="any" required className={inputClass} value={weight} onChange={(e) => setWeight(e.target.value)} />
          </label>
          <label className="flex flex-col gap-1 font-mono text-xs text-muted">
            Recipient DNI
            <input type="text" inputMode="numeric" required className={inputClass} value={dni} onChange={(e) => setDni(e.target.value)} />
          </label>
        </div>

        <button
          type="submit"
          disabled={submitting}
          className="bg-amber px-3 py-1.5 font-display text-sm font-semibold tracking-[0.14em] text-on-amber uppercase disabled:opacity-50"
        >
          {submitting ? "Requesting…" : "Request mission"}
        </button>

        {notice && (
          <p role={notice.kind === "error" ? "alert" : "status"} className={`text-xs ${notice.kind === "error" ? "text-bad" : "text-ok"}`}>
            {notice.text}
          </p>
        )}
      </form>

      <section aria-label="Missions" className="flex flex-col gap-2">
        <span className="label">Missions</span>
        {listFailed && <p className="text-xs text-bad">Could not load the missions. Retrying…</p>}
        {missions?.length === 0 && <p className="text-xs text-muted">No missions yet.</p>}
        <ul className="flex flex-col gap-2">
          {/* The API lists in creation order; show the newest first. */}
          {missions?.toReversed().map((mission) => {
            const note = mission.status === "Validated" ? payNotes[mission.id] : undefined;
            return (
              <li key={mission.id} className="border border-line bg-panel-2 p-2.5">
                <div className="flex items-center justify-between gap-2">
                  <span className="font-mono text-xs" title={mission.id}>
                    #{mission.id.slice(0, 8)}
                  </span>
                  <span className={`font-display text-[13px] font-semibold tracking-[0.12em] uppercase ${statusColor[mission.status]}`}>
                    {mission.status}
                  </span>
                </div>
                <div className="mt-1.5 flex items-center justify-between gap-2">
                  <span className="font-mono text-xs text-muted">{priceFormat.format(mission.price)}</span>
                  {mission.status === "Validated" && (
                    <button
                      type="button"
                      onClick={() => void pay(mission.id)}
                      disabled={payingId === mission.id}
                      className="border border-amber px-2.5 py-0.5 font-display text-[13px] font-semibold tracking-[0.14em] text-amber uppercase disabled:opacity-50"
                    >
                      {payingId === mission.id ? "Paying…" : "Pay"}
                    </button>
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
                          Open checkout
                        </a>
                      </>
                    )}
                  </p>
                )}
              </li>
            );
          })}
        </ul>
      </section>
    </div>
  );
}
